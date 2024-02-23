#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace codec.PhotoFrame {
	public enum FrameTypeDimensions {
		LargerSideIsOne, SmallerSideIsOne, UseAspectRatio
	}

	public enum FrameMatching {
		None, ScaleToPhoto, GenerateFrame
	}

	[CreateAssetMenu(fileName = "Photo Frame Type", menuName = "Photo Frame Type")]
	public class PhotoFrameType : ScriptableObject {
		public Material material;
		public string textureSlot;
		public Vector3 photoOffset = Vector3.zero;
		public Vector3 photoRotation = Vector3.zero;
		public FrameTypeDimensions photoDimensions = FrameTypeDimensions.LargerSideIsOne;
		public FrameMatching frameMatching = FrameMatching.None;
		public bool offsetUvs = true;
		public float uvOrientationThreshold = 0.0001f;
		public bool limitAspectRatiosToList = false;
		public GameObject[] photoFrames;
		public Vector2[] aspectRatios;

		public bool haveFrames() => photoFrames?.Length > 0 && aspectRatios?.Length > 0 && photoFrames.Where(a => a != null).Any();
		public GameObject getFrame(int index) => photoFrames?.ElementAtOrDefault(index);

		public GameObject getOrGenerateFrame(int index, float aspectRatio, out bool isGenerated, Vector2 size, bool doGeneration = true) {
			isGenerated = false;
			if(index == -1) return null;
			float sourceAspectRatio = getRatio(index);
			GameObject sourceFrame = getFrame(index);
			if(sourceFrame == null) return null;
			if(frameMatching == FrameMatching.GenerateFrame && sourceAspectRatio == aspectRatio) return sourceFrame;
			else if(frameMatching == FrameMatching.ScaleToPhoto) return sourceFrame;
			else if(frameMatching == FrameMatching.None && sourceAspectRatio == aspectRatio) return sourceFrame;
			else if(frameMatching == FrameMatching.None) return null;

			isGenerated = true;
			if(!doGeneration) return sourceFrame;

			Vector2 sourceFrameSize = Vector2.one;
			if(photoDimensions == FrameTypeDimensions.LargerSideIsOne) sourceFrameSize = Utils.RatioToSize_BigSideOne(sourceAspectRatio);
			else if(photoDimensions == FrameTypeDimensions.SmallerSideIsOne) sourceFrameSize = Utils.RatioToSize_SmallSideOne(sourceAspectRatio);
			else if(photoDimensions == FrameTypeDimensions.UseAspectRatio) {
				sourceFrameSize = aspectRatios[index];
				if(sourceFrameSize.x == 0 || sourceFrameSize.y == 0) sourceFrameSize = Vector2.one;
			}

			var offset = (size - sourceFrameSize) / 2;
			Mesh mesh = Utils.JoinGameobjectMeshes(sourceFrame, out Material[] materials);
			PhotoFrameResize.ResizeFrameMesh(mesh, offset, sourceFrameSize, offsetUvs, uvOrientationThreshold, photoOffset, Quaternion.Euler(photoRotation));
			mesh.RecalculateBounds();
			GameObject frame = new GameObject("GeneratedFrame_" + sourceFrame.name, typeof(MeshFilter), typeof(MeshRenderer));
			frame.GetComponent<MeshFilter>().sharedMesh = mesh;
			frame.GetComponent<MeshRenderer>().sharedMaterials = materials;
			
			return frame;
		}

		public void generateAndSaveMissingFrames() {
			if(photoFrames.Length < aspectRatios.Length) Array.Resize(ref photoFrames, aspectRatios.Length);

			GameObject[] newPhotoFrames = new GameObject[photoFrames.Length];
			Array.Copy(photoFrames, newPhotoFrames, photoFrames.Length);
			for(int i = 0; i < aspectRatios.Length; i++) {
				if(photoFrames[i] != null) continue;
				float aspectRatio = Utils.ConvertRatio(aspectRatios[i]);
				int index = findFrame(aspectRatio, false, out float frameAspectRatio) ?? -1;
				if(index == -1) continue;
				GameObject sourceFrame = getFrame(index);

				Vector2 frameSize = Vector2.one;
				if(photoDimensions == FrameTypeDimensions.LargerSideIsOne) frameSize = Utils.RatioToSize_BigSideOne(frameAspectRatio);
				else if(photoDimensions == FrameTypeDimensions.SmallerSideIsOne) frameSize = Utils.RatioToSize_SmallSideOne(frameAspectRatio);
				else if(photoDimensions == FrameTypeDimensions.UseAspectRatio) frameSize = Utils.RatioToSize_AreaOne(frameAspectRatio);

				string path = AssetDatabase.GetAssetPath(sourceFrame);
				string folder = Path.GetDirectoryName(path);
				string fileName = $"{Path.GetFileNameWithoutExtension(path)}-Generated-{aspectRatios[i].x}-{aspectRatios[i].y}";
				string fullNewPathMesh = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
				string fullNewPathPrefab = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.prefab");

				GameObject prefab = getOrGenerateFrame(index, aspectRatio, out bool _, frameSize);
				prefab.name = fileName;

				AssetDatabase.CreateAsset(prefab.GetComponent<MeshFilter>().sharedMesh, fullNewPathMesh);
				GameObject prefabFile = PrefabUtility.SaveAsPrefabAsset(prefab, fullNewPathPrefab);
				GameObject.DestroyImmediate(prefab);

				newPhotoFrames[i] = prefabFile;
			}

			photoFrames = newPhotoFrames;
		}

		public int? getIndex(Vector2 aspectRatio) => aspectRatio == null ? null : getIndex(Utils.ConvertRatio(aspectRatio));
		public int? getIndex(float aspectRatio) {
			if(aspectRatios == null || aspectRatios.Length == 0) return null;

			for(int i = 0; i < aspectRatios.Length; i++) {
				if(Utils.ConvertRatio(aspectRatios[i]) == aspectRatio) return i;
			}

			return null;
		}

		public float getRatio(int index) {
			if(aspectRatios == null || index == -1 || aspectRatios.Length <= index) return 1;
			return Utils.ConvertRatio(aspectRatios[index]);
		}

		public int? findFrame(float aspectRatio, bool needsExactMatch, out float frameAspectRatio) {
			frameAspectRatio = aspectRatio;
			if(aspectRatios == null || aspectRatios.Length == 0 || !haveFrames()) return null;

			var frames = aspectRatios.Select((ratioVec, i) => {
				float ratio = Utils.ConvertRatio(ratioVec, 0);
				float cutoutPercent = 1.0f - Mathf.Min(ratio, aspectRatio) / Mathf.Max(ratio, aspectRatio);
				return (cutoutPercent, i, ratio);
			});

			var bestFrame = frames.Where(a => getFrame(a.i)).Min();

			if(needsExactMatch && frameMatching == FrameMatching.None && bestFrame.ratio != aspectRatio) return null;

			if(limitAspectRatiosToList && (frameMatching == FrameMatching.ScaleToPhoto || frameMatching == FrameMatching.GenerateFrame)) {
				var bestRatio = frames.Min().ratio;
				if(needsExactMatch && bestRatio != aspectRatio) return null;
				frameAspectRatio = bestRatio;
			}
			else if(frameMatching == FrameMatching.GenerateFrame) frameAspectRatio = aspectRatio;
			else if(frameMatching == FrameMatching.ScaleToPhoto) frameAspectRatio = aspectRatio;
			else frameAspectRatio = bestFrame.ratio;

			return bestFrame.i;
		}

		public IEnumerable<string> getFrameSizeNames() {
			return aspectRatios.Select((ratioVec, i) => {
				double ratio = Utils.ConvertRatio(ratioVec);
				return $"{photoFrames.ElementAtOrDefault(i)?.name ?? "No Frame Set"} -- ({Fraction.ToString(ratio, " \u2215 ")}) = {ratio:0.000}";
			});
		}

		public bool hasDuplicates() {
			if(aspectRatios == null) return false;
			var set = new HashSet<Vector2>(aspectRatios);
			return set.Count != aspectRatios.Length;
		}

		public bool hasAspectRatioList() {
			return haveFrames() && (
				frameMatching == FrameMatching.None
			|| (frameMatching == FrameMatching.ScaleToPhoto && limitAspectRatiosToList)
			|| (frameMatching == FrameMatching.GenerateFrame && limitAspectRatiosToList)
			);
		}
	}
}
#endif