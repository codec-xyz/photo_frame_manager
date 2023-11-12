using UnityEngine;
using Unity.Burst;
using static Unity.Mathematics.math;
using Unity.Collections;
using Unity.Jobs;
using System.Linq;
using System.Collections.Generic;
using System;
using Unity.Mathematics;

namespace codec.PhotoFrame {
	public static class TextureBaker {
		public static Material photoBakeMat;

		public static bool isDebug = false;
		public static int d_cycleCount = 0;
		public static List<string> d_log = new List<string>(0);

		public struct Input {
			public Texture2D texture;
			public Vector3 point;
			public int sortGroup;
			public Vector2 uvMin;
			public Vector2 uvMax;
			public Vector2Int size;
		}

		public struct In2Sort {
			public int id;
			public Vector3 point;
			public int sortGroup;
			public Vector2Int size;
		}

		public struct Sort2Pack {
			public int id;
			public Vector2 size;
		}

		public struct Pack2Merge {
			public int id;
			public int texture;
			public Vector2Int pos;
			public bool rotated;
		}

		public struct Output {
			public int texture;
			public Vector2 uvMin;
			public Vector2 uvMax;
			public bool uvRotate;
		}

		public static Output[] Bake(Input[] bakeInputs, int textureSize, int margin, bool scaleMargin, float textureFit, float skylineMaxSpread, float overhangWeight, float neighborhoodWasteWeight, float topWasteWeight, float estimatedPackEfficiency, out Texture2D[] bakeTextures, PhotoFrameBaker.BakeProgressUpdate progressUpdate = null) {
			d_cycleCount = 0;
			d_log.Clear();
			photoBakeMat = new Material(Shader.Find("Hidden/Codec/PhotoFrames/PhotoBake"));
			AllocateNativeData(bakeInputs.Length);

			if(isDebug) d_log.Add("Input:\n" + string.Join("\n", bakeInputs.Select((node, i) => $"{i,5}: (point: ({node.point.x,8:0.00}, {node.point.y,8:0.00}, {node.point.z,8:0.00}), sortGroup: {node.sortGroup,5}, uvMin: ({node.uvMin.x:0.0000}, {node.uvMin.y:0.0000}), uvMax: ({node.uvMax.x:0.0000}, {node.uvMax.y:0.0000}), size: ({node.size.x,5}, {node.size.y,5}))")));

			var outputData = new Output[bakeInputs.Length];
			var outputTextures = new List<Texture2D>();

			try {
				In2Sort[] in2SortsOriginal = bakeInputs.Select((bakeInput, index) => Input2In2Sort(index, bakeInput, textureSize, margin)).ToArray();
				In2Sort[] in2Sorts = in2SortsOriginal;

				for(int _s = 0; _s < 4; _s++) {
					d_cycleCount++;
					if(isDebug) d_log.Add($"Start Cycle {_s}");

					if(progressUpdate != null) progressUpdate($"Sorting (Cycle {_s + 1})", (bakeInputs.Length - in2Sorts.Length) / (float)bakeInputs.Length);

					NativeSlice<Sort2Pack>[] sort2Pack = Sort(in2Sorts, textureSize, textureFit, estimatedPackEfficiency, out int[] packTexSizes, margin, scaleMargin);
					if(scaleMargin) Sort2Pack_ScaleMargin(sort2Pack, packTexSizes, textureSize, margin);

					if(progressUpdate != null) progressUpdate($"Packing (Cycle {_s + 1})", (bakeInputs.Length - in2Sorts.Length) / (float)bakeInputs.Length);

					Pack2Merge[][] pack2MergePacked, pack2MergeFailed;
					(pack2MergePacked, pack2MergeFailed) = Pack(sort2Pack, textureSize, skylineMaxSpread, overhangWeight, neighborhoodWasteWeight, topWasteWeight, packTexSizes, outputTextures.Count);

					float completed = bakeInputs.Length - in2Sorts.Length;
					for(int i = 0; i < pack2MergePacked.Length; i++) {
						if(progressUpdate != null) {
							completed += pack2MergePacked[i].Length;
							progressUpdate($"Merging textures {i + 1}/{pack2MergePacked.Length} (Cycle {_s + 1})", completed / (float)bakeInputs.Length);
						}

						int scaledMargin = scaleMargin ? ScaleMargin(margin, textureSize, packTexSizes[i]) : margin;
						foreach(var pOut in pack2MergePacked[i]) outputData[pOut.id] = MergeUvOut(pOut, packTexSizes[i], scaledMargin, bakeInputs);
						outputTextures.Add(Merge(pack2MergePacked[i], packTexSizes[i], scaledMargin, bakeInputs));
					}

					in2Sorts = pack2MergeFailed.SelectMany(a => a).Select(pOut => in2SortsOriginal[pOut.id]).ToArray();

					if(in2Sorts.Length == 0) break;
				}

				foreach(var failed in in2Sorts) outputData[failed.id] = new Output { texture = -1 };
			}
			catch(Exception e) {
				Debug.LogException(e);
				throw;
			}
			finally {
				DisposeNativeData();
			}

			bakeTextures = outputTextures.ToArray();
			return outputData;
		}

		public static int ScaleMargin(int margin, int baseTextureSize, int textureSize) {
			int scaledMargin = (int)(margin * textureSize / (float)baseTextureSize);
			if(margin != 0 && scaledMargin == 0) scaledMargin = 1;
			return scaledMargin;
		}

		public static NativeArray<In2Sort> na_in2Sorts;
		public static NativeArray<SortBinaryTreeNode> na_bt;
		public static NativeArray<int> na_sortTextureSizes;
		public static NativeArray<int> na_sort2PacksSlices;
		public static NativeArray<Sort2Pack> na_sort2Packs;

		public static void AllocateNativeData(int photoCount) {
			na_in2Sorts = new NativeArray<In2Sort>(photoCount, Allocator.TempJob);
			na_bt = new NativeArray<SortBinaryTreeNode>(photoCount * 2 - 1, Allocator.TempJob);
			na_sortTextureSizes = new NativeArray<int>(photoCount, Allocator.TempJob);
			na_sort2PacksSlices = new NativeArray<int>(photoCount, Allocator.TempJob);
			na_sort2Packs = new NativeArray<Sort2Pack>(photoCount, Allocator.TempJob);
		}

		public static void DisposeNativeData() {
			na_in2Sorts.Dispose();
			na_bt.Dispose();
			na_sortTextureSizes.Dispose();
			na_sort2PacksSlices.Dispose();
			na_sort2Packs.Dispose();
		}

		public static In2Sort Input2In2Sort(int index, Input input, int textureSize, int margin) {
			Vector2Int size = input.size + (margin * new Vector2Int(2, 2));

			var bigSize = max(size.x, size.y);
			if(bigSize > textureSize) size = size * textureSize / bigSize;

			return new In2Sort {
				id = index,
				point = input.point,
				sortGroup = input.sortGroup,
				size = size,
			};
		}

		public static NativeSlice<Sort2Pack>[] Sort(In2Sort[] inputs, int textureSize, float textureFit, float estimatedPackEfficiency, out int[] packTextureSizes, int margin, bool scaleMargin) {
			na_in2Sorts.GetSubArray(0, inputs.Length).CopyFrom(inputs);

			var job = new SortJob {
				inputCount = inputs.Length,
				textureFit = textureFit,
				maxPixels = (int)(textureSize * textureSize * estimatedPackEfficiency),
				textureSize = textureSize,
				inputs = na_in2Sorts,
				bt = na_bt,
				outputTextureSizes = na_sortTextureSizes,
				outputSlices = na_sort2PacksSlices,
				outputs = na_sort2Packs,
			};
			job.Run();

			if(isDebug) {
				string log = "Sort:\n" + string.Join("\n", na_bt.Take(inputs.Length * 2 - 1).Select((node, i) => $"{i,5}: (inputI: {node.in2SortI,5}, l1: {node.link1,5}, l2: {node.link2,5}, lb: {node.linkBack,5}, group: {node.sortGroup}, pixels: {node.pixels}, max: {node.maxSize})"));
				d_log.Add(log);
			}

			var texSizes = new List<int>();
			var groups = new List<NativeSlice<Sort2Pack>>();
			for(int i = 0; i < inputs.Length; i++) {
				int start = na_sort2PacksSlices[i], end = (i < inputs.Length - 1) ? na_sort2PacksSlices[i + 1] : inputs.Length;
				texSizes.Add(na_sortTextureSizes[i]);
				groups.Add(new NativeSlice<Sort2Pack>(na_sort2Packs, start, end - start));
				if(end == inputs.Length) break;
			}

			packTextureSizes = texSizes.ToArray();

			return groups.ToArray();
		}

		public static void Sort2Pack_ScaleMargin(NativeSlice<Sort2Pack>[] sort2Pack, int[] textureSizes, int textureSize, int margin) {
			for(int i = 0; i < sort2Pack.Length; i++) {
				int marginAdjust = margin - ScaleMargin(margin, textureSize, textureSizes[i]);
				for(int a = 0; a < sort2Pack[i].Length; a++) {
					Sort2Pack item = sort2Pack[i][a];
					item.size -= new Vector2(2 * marginAdjust, 2 * marginAdjust);
					sort2Pack[i][a] = item;
				}
			}
		}

		public static (Pack2Merge[][], Pack2Merge[][]) Pack(NativeSlice<Sort2Pack>[] sort2Pack, int textrueSize, float skylineMaxSpread, float overhangWeight, float neighborhoodWasteWeight, float topWasteWeight, int[] textureSizes, int textureIdStart) {
			var jobs = new PackJob[sort2Pack.Length];
			var na_jobs = new NativeArray<JobHandle>(sort2Pack.Length, Allocator.TempJob);
			var na_packInputPlaced = new NativeArray<bool>[sort2Pack.Length];
			var na_skylineYs = new NativeArray<ushort>[sort2Pack.Length];
			var na_skylineXMoves = new NativeArray<ushort>[sort2Pack.Length];
			var na_packFailSpots = new NativeArray<int>[sort2Pack.Length];
			var na_pack2Merges = new NativeArray<Pack2Merge>[sort2Pack.Length];
			for(int i = 0; i < sort2Pack.Length; i++) {
				na_packInputPlaced[i] = new NativeArray<bool>(sort2Pack[i].Length, Allocator.TempJob);
				na_skylineYs[i] = new NativeArray<ushort>(textrueSize, Allocator.TempJob);
				na_skylineXMoves[i] = new NativeArray<ushort>(textrueSize, Allocator.TempJob);
				na_packFailSpots[i] = new NativeArray<int>(1, Allocator.TempJob);
				na_pack2Merges[i] = new NativeArray<Pack2Merge>(sort2Pack[i].Length, Allocator.TempJob);
			}

			try {
				for(int i = 0; i < sort2Pack.Length; i++) {
					jobs[i] = new PackJob {
						textureId = textureIdStart + i,
						textureSize = (ushort)textureSizes[i],
						skylineMaxSpread = (ushort)(textureSizes[i] * skylineMaxSpread),
						overhangWeight = overhangWeight,
						neighborhoodWasteWeight = neighborhoodWasteWeight,
						topWasteWeight = topWasteWeight,
						inputs = sort2Pack[i],
						inputPlaced = na_packInputPlaced[i],
						skylineY = na_skylineYs[i],
						skylineXMove = na_skylineXMoves[i],
						failSpot = na_packFailSpots[i],
						outputs = na_pack2Merges[i],
					};

					na_jobs[i] = jobs[i].Schedule();
				}

				JobHandle.CompleteAll(na_jobs);

				var packed = new Pack2Merge[sort2Pack.Length][];
				var failed = new Pack2Merge[sort2Pack.Length][];

				for(int i = 0; i < sort2Pack.Length; i++) {
					var arr = na_pack2Merges[i].ToArray();
					packed[i] = arr.Take(na_packFailSpots[i][0]).ToArray();
					failed[i] = arr.Skip(na_packFailSpots[i][0]).ToArray();

					if(isDebug) {
						string log = $"Pack (texture {i}):\n- skyline: {PrintSkyline(jobs[i])}\n";
						log += "- packed: " + string.Join(", ", packed[i].Select(o => $"(i: {o.id}, x: {o.pos.x}, y: {o.pos.y}, r: {(o.rotated ? 1 : 0)})")) + "\n";
						log += "- failed: " + string.Join(", ", failed[i].Select(o => $"{o.id}"));

						d_log.Add(log);
					}
				}

				return (packed, failed);
			}
			catch(Exception e) {
				Debug.LogException(e);
				throw;
			}
			finally {
				na_jobs.Dispose();
				for(int i = 0; i < sort2Pack.Length; i++) {
					na_packInputPlaced[i].Dispose();
					na_skylineYs[i].Dispose();
					na_skylineXMoves[i].Dispose();
					na_packFailSpots[i].Dispose();
					na_pack2Merges[i].Dispose();
				}
			}
		}

		public static Output MergeUvOut(in Pack2Merge input, in int textureSize, in int margin, in Input[] bakeInputs) {
			Vector2 offset = (input.pos + new Vector2(margin, margin)) / textureSize;
			Vector2 scale = (Vector2)bakeInputs[input.id].size / textureSize;

			if(input.rotated) return new Output {
				texture = input.texture,
				uvMin = offset,
				uvMax = offset + new Vector2(scale.y, scale.x),
				uvRotate = true,
			};

			return new Output {
				texture = input.texture,
				uvMin = offset,
				uvMax = offset + scale,
				uvRotate = false,
			};
		}

		public static Texture2D Merge(in Pack2Merge[] inputs, in int textureSize, in int margin, in Input[] bakeInputs) {
			RenderTexture rt = RenderTexture.GetTemporary(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
			RenderTexture.active = rt;
			GL.Clear(true, true, Color.black);

			foreach(var input in inputs) {
				Input bakeInput = bakeInputs[input.id];

				Vector2 offset = (input.pos + new Vector2(margin, margin)) / textureSize;
				Vector2 scale = (Vector2)bakeInput.size / textureSize;

				Matrix4x4 uvMatrix;
				Vector2 marginWidth = new Vector2(margin / (float)bakeInput.size.x, margin / (float)bakeInput.size.y);

				if(input.rotated) uvMatrix = Matrix4x4.TRS(new Vector3(scale.y, 0, 0), Quaternion.Euler(0, 0, 90), new Vector3(scale.x, scale.y, 1));
				else uvMatrix = Matrix4x4.Scale(new Vector3(scale.x, scale.y, 1));

				uvMatrix = Matrix4x4.Translate(new Vector3(offset.x, offset.y, 0)) * uvMatrix;
				uvMatrix = uvMatrix.inverse;

				photoBakeMat.SetMatrix("_UV", uvMatrix);
				photoBakeMat.SetVector("_Margin", new Vector4(-marginWidth.x, -marginWidth.y, 1 + marginWidth.x, 1 + marginWidth.y));
				photoBakeMat.SetVector("_Crop", new Vector4(bakeInput.uvMin.x, bakeInput.uvMin.y, bakeInput.uvMax.x, bakeInput.uvMax.y));

				Graphics.Blit(bakeInput.texture, rt, photoBakeMat);
			}

			Texture2D texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, true);
			RenderTexture.active = rt;
			texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
			texture.Apply();

			RenderTexture.ReleaseTemporary(rt);

			return texture;
		}

		public struct SortBinaryTreeNode {
			public int in2SortI;
			public int sortGroup;
			public Vector3 point;
			public int pixels;
			public int maxSize;
			public int link1, link2, linkBack;
		}

		[BurstCompile(CompileSynchronously = true)]
		public struct SortJob : IJob {
			[ReadOnly] public int inputCount;
			[ReadOnly] public float textureFit;
			[ReadOnly] public int maxPixels;
			[ReadOnly] public int textureSize;
			[ReadOnly] public NativeArray<In2Sort> inputs;

			private int btGI;
			public NativeArray<SortBinaryTreeNode> bt;

			private int outputsGI;
			[WriteOnly] public NativeArray<int> outputTextureSizes;
			[WriteOnly] public NativeArray<int> outputSlices;
			[WriteOnly] public NativeArray<Sort2Pack> outputs;

			public const bool Link1 = true;
			public const bool Link2 = false;

			public SortBinaryTreeNode nodeFromSrc(int pfI) {
				var src = inputs[pfI];
				var node = new SortBinaryTreeNode();
				node.in2SortI = pfI;
				node.sortGroup = src.sortGroup;
				node.point = src.point;
				node.pixels = src.size.x * src.size.y;
				node.maxSize = max(src.size.x, src.size.y);
				node.link1 = -1;
				node.link2 = -1;
				node.linkBack = -1;
				return node;
			}

			private void bt_splitNode(int btI, ref SortBinaryTreeNode srcNode) {
				var parentNode = bt[btI];

				parentNode.link1 = btGI;
				var node1 = bt[btI];
				node1.linkBack = btI;
				bt[btGI] = node1;
				btGI++;

				parentNode.link2 = btGI;
				srcNode.linkBack = btI;
				bt[btGI] = srcNode;
				btGI++;

				bt[btI] = parentNode;
			}

			private bool bt_pickPathForPoint(int btI, in SortBinaryTreeNode srcNode) {
				var node = bt[btI];
				var nodeLink1 = bt[node.link1];
				var nodeLink2 = bt[node.link2];

				bool link1DifGroupId = nodeLink1.sortGroup != srcNode.sortGroup;
				bool link2DifGroupId = nodeLink2.sortGroup != srcNode.sortGroup;

				if(link1DifGroupId != link2DifGroupId) return link1DifGroupId ? Link2 : Link1;

				float sortPercent = srcNode.in2SortI / (float)inputCount;

				double link1PixelRoom = (maxPixels - ((nodeLink1.pixels + srcNode.pixels) % maxPixels)) / (double)maxPixels;
				double link2PixelRoom = (maxPixels - ((nodeLink2.pixels + srcNode.pixels) % maxPixels)) / (double)maxPixels;

				Vector3 link1Dist = nodeLink1.point - srcNode.point;
				Vector3 link2Dist = nodeLink2.point - srcNode.point;

				float dist1 = dot(link1Dist, link1Dist);
				float dist2 = dot(link2Dist, link2Dist);

				double link1Score = dist1 + (link1PixelRoom - link2PixelRoom) * sortPercent * textureFit;
				double link2Score = dist2 + (link2PixelRoom - link1PixelRoom) * sortPercent * textureFit;

				return link1Score < link2Score ? Link1 : Link2;
			}

			public void bt_recalcNodeFromChildren(int btI) {
				var node = bt[btI];
				var nodeLink1 = bt[node.link1];
				var nodeLink2 = bt[node.link2];

				node.in2SortI = -1;
				node.sortGroup = nodeLink1.sortGroup == nodeLink2.sortGroup ? nodeLink1.sortGroup : -1;
				node.point = (nodeLink1.point + nodeLink2.point) / 2.0f;
				node.pixels = nodeLink1.pixels + nodeLink2.pixels;
				node.maxSize = max(nodeLink1.maxSize, nodeLink2.maxSize);
				bt[btI] = node;
			}

			private void bt_addSrc(int pfI) {
				var srcNode = nodeFromSrc(pfI);
				int btI = 0;

				for(int _s = 0; _s < inputCount * 2; _s++) { // safety limit
					if(bt[btI].link1 == -1) {
						bt_splitNode(btI, ref srcNode);
						break;
					}

					btI = bt_pickPathForPoint(btI, srcNode) == Link1 ? bt[btI].link1 : bt[btI].link2;
				}

				for(int _s = 0; _s < inputCount * 2; _s++) { // safety limit
					bt_recalcNodeFromChildren(btI);
					btI = bt[btI].linkBack;
					if(btI == -1) break;
				}
			}

			private int bt_next(int btI) {
				return bt[btI].link1;
			}

			private int bt_nextOtherPath(int btI, int stopAt = -1) {
				for(int _s = 0; _s < inputCount * 2; _s++) { // safety limit
					int fromBtI = btI;
					btI = bt[btI].linkBack;
					if(btI == -1 || btI == stopAt) break;
					if(fromBtI == bt[btI].link1) {
						btI = bt[btI].link2;
						break;
					}
				}

				return btI;
			}

			private int bt_nextEnd(int btI) {
				for(int _s = 0; _s < inputCount * 2; _s++) { // safety limit
					if(bt[btI].in2SortI != -1) break;
					btI = bt_next(btI);
				}

				return btI;
			}

			private int bt_nextGroupRootNode(int btI) {
				btI = (btI == -1 ? 0 : bt_nextOtherPath(btI));
				if(btI == -1) return btI;

				for(int _s = 0; _s < inputCount * 2; _s++) { // safty limit
					bool fits = bt[btI].pixels <= maxPixels
					|| (bt[btI].in2SortI != -1 && bt[btI].pixels <= textureSize * textureSize);
					if(fits && bt[btI].sortGroup != -1) break;
					btI = bt_next(btI);
				}

				return btI;
			}

			private int addGroup(int btI) {
				int btRootParentI = bt[btI].linkBack;
				int startValue_outputsGI = outputsGI;

				for(int _s = 0; _s < inputCount; _s++) { // safety limit
					btI = bt_nextEnd(btI);

					In2Sort in2Sort = inputs[bt[btI].in2SortI];
					outputs[outputsGI] = new Sort2Pack {
						id = in2Sort.id,
						size = in2Sort.size,
					};
					outputsGI++;

					btI = bt_nextOtherPath(btI, btRootParentI);

					if(btI == btRootParentI) break;
				}

				return outputsGI - startValue_outputsGI;
			}

			public void Execute() {
				bt[0] = nodeFromSrc(0);
				btGI = 1;
				for(int i = 1; i < inputCount; i++) bt_addSrc(i);

				int btI = -1, outputSlicesI = 0;
				outputsGI = 0;
				for(int _s = 0; _s < inputCount; _s++) { // safty limit
					btI = bt_nextGroupRootNode(btI);
					if(btI == -1) break;

					int curTexSize = textureSize;
					int curMaxPixels = maxPixels;
					while(bt[btI].pixels * 4 <= curMaxPixels
					&& bt[btI].maxSize * 2 <= curTexSize
					&& curTexSize > 16) {
						curTexSize >>= 1;
						curMaxPixels >>= 2;
					}

					outputTextureSizes[outputSlicesI] = curTexSize;
					outputSlices[outputSlicesI] = outputsGI;
					int count = addGroup(btI);

					if(count == 1) {
						while(bt[btI].maxSize * 2 <= curTexSize && curTexSize > 16) curTexSize >>= 1;
						outputTextureSizes[outputSlicesI] = curTexSize;
					}

					outputSlicesI++;
				}

				if(outputSlicesI < inputCount) outputSlices[outputSlicesI] = outputsGI;
			}
		}

		public struct SkylinePos {
			public ushort x;
			public bool dir;
		}

		public static string PrintSkyline(PackJob job) {
			string skylineStr = "";
			for(int pos = 1; !job.s_outside(pos); pos = job.skyline_nextSpan(pos)) {
				skylineStr += $"span(x: {pos}, width: {job.s_spanWidth(pos)}, y: {job.s_y(pos)}), ";
			}
			return skylineStr;
		}

		[BurstCompile(CompileSynchronously = true)]
		public struct PackJob : IJob {
			[ReadOnly] public int textureId;
			[ReadOnly] public ushort textureSize;
			[ReadOnly] public ushort skylineMaxSpread;
			[ReadOnly] public float overhangWeight;
			[ReadOnly] public float neighborhoodWasteWeight;
			[ReadOnly] public float topWasteWeight;
			[ReadOnly] public NativeSlice<Sort2Pack> inputs;
			public NativeArray<bool> inputPlaced;
			public NativeArray<ushort> skylineY;
			public NativeArray<ushort> skylineXMove;
			[WriteOnly] public NativeArray<int> failSpot;
			private int outputsGI;
			[WriteOnly] public NativeArray<Pack2Merge> outputs;

			// directionality must be known when traversing
			// positions hold directionality in their sign
			// positions start at 1 since 0 has no sign
			//  < - right dir
			//       left dir - >
			// █ █ █ █ █ █ █ █ █ █                   █ █ █
			//                     █ █ █ █         █
			//                               █ █ █
			// 3                 3 2     2 0 1   1 2 3   3 - skylineY (y pos of empty block above)
			// 9                 9 3     3 0 2   2 0 2   2 - skylineXMove (width of span - 1)

			public const bool DirLeft = true;
			public const bool DirRight = false;

			public bool s_dir(in int pos) => pos >= 0 ? DirRight : DirLeft;
			public int s_posToIndex(in int pos) => abs(pos) - 1;
			public bool s_outside(in int pos) => abs(pos) > textureSize || pos == 0;
			public int s_swapNeighbor(in int pos) => s_outside(pos) || pos == -textureSize || pos == 1 ? int.MaxValue : -(pos - 1);
			public int s_swapSpan(in int pos, in int xMove) => -(pos + xMove);
			public int s_swapSpan(in int pos) => s_outside(pos) ? int.MaxValue : s_swapSpan(pos, skylineXMove[s_posToIndex(pos)]);
			public ushort s_y(in int pos) => s_outside(pos) ? textureSize : skylineY[s_posToIndex(pos)];
			public int s_spanWidth(in int pos) => s_outside(pos) ? 0 : skylineXMove[s_posToIndex(pos)] + 1;

			public int skyline_nextSpan(in int pos) => s_swapNeighbor(s_swapSpan(pos));
			public int skyline_previousSpan(in int pos) => s_swapSpan(s_swapNeighbor(pos));
			public int skyline_traverseForward(in int pos) => s_dir(pos) == DirRight ? s_swapSpan(pos) : s_swapNeighbor(pos);
			public int skyline_traverseBackward(in int pos) => s_dir(pos) == DirRight ? s_swapNeighbor(pos) : s_swapSpan(pos);

			[BurstDiscard]
			public void check_skyline_set(int pos1, int pos2, ushort y) {
				bool pos1Good = false, pos2Good = false;
				bool dirGood = s_dir(pos1) != s_dir(pos2);
				bool yGood = y <= textureSize;

				for(int pos = 1; !s_outside(pos); pos = skyline_traverseForward(pos)) {
					if(pos == pos1) pos1Good = true;
					if(pos == pos2) pos2Good = true;
				}

				if(!pos1Good || !pos2Good || !dirGood || !yGood) {
					string errorMsg = $"skyline_setY pre check failed!!!; requested(pos1: {pos1}, pos2: {pos2}, y: {y}); skyline...\n{PrintSkyline(this)}";
					throw new Exception(errorMsg);
				}
			}

			[BurstDiscard]
			public void check_skyline_setCut(int3 cutPos, ushort y) {
				bool pos1Good = false, pos2Good = false;
				bool dirGood = s_dir(cutPos[0]) != s_dir(cutPos[1]) && s_dir(cutPos[1]) == s_dir(cutPos[2]);
				bool orderGood = -cutPos[0] < cutPos[1] == cutPos[1] < cutPos[2] || -cutPos[0] == cutPos[1];
				bool yGood = y <= textureSize;

				for(int pos = 1; !s_outside(pos); pos = skyline_traverseForward(pos)) {
					if(pos == cutPos[0]) pos1Good = true;
					if(pos == cutPos[2]) pos2Good = true;
				}

				if(!pos1Good || !pos2Good || !dirGood || !orderGood || !yGood) {
					string errorMsg = $"skyline_setCut pre check failed!!!; requested(cut[0]: {cutPos[0]}, cut[1]: {cutPos[1]}, cut[2]: {cutPos[2]}, y: {y}); skyline...\n{PrintSkyline(this)}";
					throw new Exception(errorMsg);
				}
			}

			public void skyline_setUnchecked(int pos1, int pos2, ushort y) {
				int totalWidth = abs(s_posToIndex(pos1) - s_posToIndex(pos2));

				skylineY[s_posToIndex(pos1)] = y;
				skylineY[s_posToIndex(pos2)] = y;

				skylineXMove[s_posToIndex(pos1)] = (ushort)totalWidth;
				skylineXMove[s_posToIndex(pos2)] = (ushort)totalWidth;
			}

			public void skyline_set(int pos1, int pos2, ushort y) {
				check_skyline_set(pos1, pos2, y);
				skyline_setUnchecked(pos1, pos2, y);
			}

			public void skyline_setCut(int3 cutPos, ushort y) {
				check_skyline_setCut(cutPos, y);
				skyline_setUnchecked(cutPos[0], cutPos[1], y);
				skyline_setUnchecked(s_swapNeighbor(cutPos[1]), cutPos[2], s_y(cutPos[2]));
			}

			public ushort skyline_getMinYPos() {
				ushort minY = textureSize;
				ushort minYPos = 0;

				for(int pos = 1; !s_outside(pos); pos = skyline_nextSpan(pos)) {
					if(s_y(pos) < minY) {
						minY = s_y(pos);
						minYPos = (ushort)pos;
					}
				}

				return minYPos;
			}

			public void skyline_reset() {
				skylineY[0] = 0;
				skylineXMove[0] = (ushort)(textureSize - 1);
				skylineY[textureSize - 1] = 0;
				skylineXMove[textureSize - 1] = (ushort)(textureSize - 1);
			}

			public void skyline_addRect(ushort rectX, ushort rectY, int pos) {
				ushort finalSpanY = (ushort)(s_y(pos) + rectY);
				int3 cutPos = new int3(pos, s_swapSpan(pos, rectX - 1), s_swapSpan(pos));

				int _s = 0; // safety limit
				while(cutPos.z > cutPos.y && _s++ < textureSize) {
					cutPos.z = skyline_previousSpan(cutPos.z);
				}

				if(finalSpanY == s_y(s_swapNeighbor(pos)) && !s_outside(skyline_previousSpan(pos))) cutPos.x = skyline_previousSpan(pos);

				if(cutPos.z == cutPos.y) {
					if(finalSpanY == s_y(s_swapNeighbor(cutPos.y)) && !s_outside(skyline_previousSpan(cutPos.y))) cutPos.y = skyline_previousSpan(cutPos.y);

					skyline_set(cutPos.x, cutPos.y, finalSpanY);
				}
				else skyline_setCut(cutPos, finalSpanY);
			}

			public void skyline_fillMinY(in int pos) {
				int4 minArea = new int4(skyline_previousSpan(pos), pos, s_swapSpan(pos), s_swapSpan(skyline_nextSpan(pos)));
				int2 minAreaY = new int2(s_y(minArea[0]), s_y(minArea[3]));

				if(minArea[0] == int.MaxValue) minArea[0] = minArea[1];
				if(minArea[3] == int.MaxValue) minArea[3] = minArea[2];

				if(minArea[1] == 1 && -minArea[2] == textureSize) {
					skyline_set(1, -textureSize, textureSize);
				}
				else if(minAreaY[0] < minAreaY[1]) {
					skyline_set(minArea[0], minArea[2], (ushort)minAreaY[0]);
				}
				else if(minAreaY[0] > minAreaY[1]) {
					skyline_set(minArea[1], minArea[3], (ushort)minAreaY[1]);
				}
				else {
					skyline_set(minArea[0], minArea[3], (ushort)minAreaY[0]);
				}
			}

			public bool skyline_doesFit_sideBounds(in ushort rectX, in int pos) {
				if(s_dir(pos) == DirRight) return pos + (rectX - 1) <= textureSize;
				return pos + (rectX - 1) <= -1;
			}

			public int skyline_getTopBoundForRect(in int minY, in ushort rectY, in int param_skylineMaxSpread) {
				return min(minY + max(param_skylineMaxSpread, rectY), textureSize);
			}

			public bool skyline_doesFit_topBound(in ushort rectY, in int pos, in int topBound) {
				return s_y(pos) + rectY <= topBound;
			}

			public bool skyline_doesOverlap(in ushort rectX, in int pos, out int overhangArea, out int farEndSpanPos) {
				overhangArea = 0;
				farEndSpanPos = pos;
				int rectFarEndPos = s_swapSpan(pos, rectX - 1);
				int testPos = s_swapSpan(pos);

				int _s = 0;
				while(testPos > rectFarEndPos && _s++ < textureSize) { // safety limit
					testPos = skyline_previousSpan(testPos);

					int heightDif = s_y(pos) - s_y(testPos);
					if(heightDif < 0) return true;

					int width = s_spanWidth(testPos);
					overhangArea += heightDif * min(width - (rectFarEndPos - testPos), width);
				}

				farEndSpanPos = testPos;

				return false;
			}

			public int skyline_getNeighborhoodWaste(int pos, in int top) {
				int waste = 0;
				for(; !s_outside(pos); pos = skyline_nextSpan(pos)) {
					int heightDif = top - s_y(pos);
					if(heightDif < 0) break;
					waste += heightDif * s_spanWidth(pos);
				}
				return waste;
			}

			public int skyline_countRectMatchingEdges(in ushort rectX, in ushort rectY, in int pos) {
				int count = 0;

				int baseWidth = s_spanWidth(pos);
				int posY = s_y(pos);
				int side1Y = s_y(skyline_previousSpan(pos));
				int side2Y = s_y(skyline_nextSpan(pos));

				if(baseWidth == rectX) count++;
				if(side1Y - posY == rectY) count++;
				if(side2Y - posY == rectY) count++;
				if(side1Y == textureSize && side2Y == textureSize && posY + rectY == textureSize) count++;

				return count;
			}

			public long skyline_getRectPosScore(in int minYPos, in ushort rectX, in ushort rectY, in int pos) {
				int topBound = skyline_getTopBoundForRect(s_y(minYPos), rectY, skylineMaxSpread);
				if(!skyline_doesFit_topBound(rectY, pos, topBound)) return long.MaxValue;
				if(!skyline_doesFit_sideBounds(rectX, pos)) return long.MaxValue;

				if(skyline_doesOverlap(rectX, pos, out int overhangArea, out int farEndSpanPos)) return long.MaxValue;

				//find wasted space
				int rectTopY = s_y(pos) + rectY;
				int rectFarEndPos = s_swapSpan(pos, rectX - 1);
				int neighborhoodWasteArea = (rectTopY - s_y(farEndSpanPos)) * (farEndSpanPos - rectFarEndPos);
				neighborhoodWasteArea += skyline_getNeighborhoodWaste(s_swapNeighbor(farEndSpanPos), rectTopY);
				neighborhoodWasteArea += skyline_getNeighborhoodWaste(s_swapNeighbor(pos), rectTopY);
				int topWasteArea = (textureSize - rectTopY) * rectX;

				int matchEdgesCount = skyline_countRectMatchingEdges(rectX, rectY, pos);

				long resultScore = 0;
				resultScore += (long)(overhangArea * overhangWeight + neighborhoodWasteArea * neighborhoodWasteWeight + topWasteArea * topWasteWeight);
				resultScore = resultScore * 5 + (4 - matchEdgesCount);
				return resultScore;
			}

			public long skyline_placeRectScored(in int minYPos, in ushort rectX, in ushort rectY, out int bestPos) {
				long bestScore = long.MaxValue;
				bestPos = 0;

				for(int pos = 1; !s_outside(pos); pos = skyline_traverseForward(pos)) {
					if(s_y(s_swapNeighbor(pos)) < s_y(pos)) continue;

					long score = skyline_getRectPosScore(minYPos, rectX, rectY, pos);
					if(score < bestScore) {
						bestScore = score;
						bestPos = pos;
					}
				}

				return bestScore;
			}

			public void skyline_placeBestRect(in int minYPos, out int bestI, out int bestPos, out bool bestRotate) {
				long bestScore = long.MaxValue;
				bestI = -1;
				bestPos = 0;
				bestRotate = false;

				for(int i = 0; i < inputs.Length; i++) {
					if(inputPlaced[i]) continue;

					long score = skyline_placeRectScored(minYPos, (ushort)inputs[i].size.x, (ushort)inputs[i].size.y, out int pos);
					if(score < bestScore) {
						bestScore = score;
						bestI = i;
						bestPos = pos;
						bestRotate = false;
					}

					score = skyline_placeRectScored(minYPos, (ushort)inputs[i].size.y, (ushort)inputs[i].size.x, out pos);
					if(score < bestScore) {
						bestScore = score;
						bestI = i;
						bestPos = pos;
						bestRotate = true;
					}
				}

				if(bestScore == long.MaxValue) bestI = -1;
			}

			public bool pack_step(in int minYPos) {
				skyline_placeBestRect(minYPos, out int bestI, out int bestPos, out bool bestRotate);

				if(bestI == -1) {
					skyline_fillMinY(minYPos);
					return false;
				}

				ushort rectX = (ushort)(bestRotate ? inputs[bestI].size.y : inputs[bestI].size.x);
				ushort rectY = (ushort)(bestRotate ? inputs[bestI].size.x : inputs[bestI].size.y);

				outputs[outputsGI] = new Pack2Merge {
					id = inputs[bestI].id,
					texture = textureId,
					pos = new Vector2Int(
						s_dir(bestPos) == DirRight ? s_posToIndex(bestPos) : s_posToIndex(bestPos) - (rectX - 1),
						s_y(bestPos)
					),
					rotated = bestRotate,
				};
				outputsGI++;

				skyline_addRect(rectX, rectY, bestPos);

				inputPlaced[bestI] = true;

				return true;
			}

			public void Execute() {
				for(int i = 0; i < inputs.Length; i++) inputPlaced[i] = false;
				outputsGI = 0;
				int rectsLeft = inputs.Length;
				skyline_reset();

				for(int i = 0; i < inputs.Length * 15; i++) { // safty limit
					int minYPos = skyline_getMinYPos();
					if(s_y(minYPos) >= textureSize) break;

					bool wasRectPlaced = pack_step(minYPos);
					if(wasRectPlaced) rectsLeft--;
					if(rectsLeft == 0) break;
				}

				failSpot[0] = outputsGI;

				for(int i = 0; i < inputs.Length; i++) {
					if(inputPlaced[i]) continue;

					outputs[outputsGI] = new Pack2Merge { id = inputs[i].id, };
					outputsGI++;
				}
			}
		}
	}
}