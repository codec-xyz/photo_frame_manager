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
	public static class PhotoFrameResize {
		public static List<Vector2[]> GetMeshUvs(Mesh mesh) {
			List<Vector2[]> uvs = new List<Vector2[]>();
			for(int i = 0; i < 8; i++) {
				var uvList = new List<Vector2>();
				mesh.GetUVs(i, uvList);
				if(uvList.Count() > 0) uvs.Add(uvList.ToArray());
				else break;
			}
			return uvs;
		}

		public static List<(int p0, int p1)> GetMeshEdges(List<int> triangles, int pCount) {
			var edges = new List<(int p0, int p1)>();
			for(int i = 0; i < triangles.Count(); i += 3) {
				edges.Add((triangles[i + 0], triangles[i + 1]));
				edges.Add((triangles[i + 1], triangles[i + 2]));
				edges.Add((triangles[i + 2], triangles[i + 0]));
			}
			return edges;
		}

		public static List<(int p0, int p1)>[] GetMeshEdgesPerPoints(List<(int p0, int p1)> edges, int pCount) {
			var pointEdges = new List<(int p0, int p1)>[pCount];
			for(int i = 0; i < pCount; i++) pointEdges[i] = new List<(int p0, int p1)>();
			foreach(var edge in edges) {
				pointEdges[edge.p0].Add(edge);
				pointEdges[edge.p1].Add(edge);
			}
			return pointEdges;
		}

		public static int[][] Edges_To_Islands(List<(int p0, int p1)> edges, int pCount) {
			var tree = new int[pCount];
			for(int i = 0; i < tree.Length; i++) tree[i] = i;

			int getTopNode(int treeIndex) {
				for(int _s = 0; _s < pCount; _s++) {
					if(tree[treeIndex] == treeIndex) return treeIndex;
					treeIndex = tree[treeIndex];
				}
				throw new Exception("Tree misformatted");
			}

			foreach(var edge in edges) {
				tree[getTopNode(edge.p0)] = getTopNode(edge.p1);
			}

			var topNodes = new HashSet<int>();
			for(int i = 0; i < tree.Length; i++) {
				tree[i] = getTopNode(i);
				topNodes.Add(tree[i]);
			}

			var topNodesList = topNodes.ToList();

			var islandPoints = new List<int>[topNodes.Count()];
			for(int i = 0; i < islandPoints.Length; i++) islandPoints[i] = new List<int>();
			for(int i = 0; i < tree.Length; i++) {
				islandPoints[topNodesList.IndexOf(tree[i])].Add(i);
			}
			return islandPoints.Select(i => i.ToArray()).ToArray();
		}

		public static (float scale, float rotation)[] GetFrameEdgeInfo((int p0, int p1) edge, Vector3[] points, List<Vector2[]> uvs, float uvOrientationThreshold) {
			(float scale, float rotation)[] info = new (float scale, float rotation)[uvs.Count()];

			Vector2 p0 = new Vector2(points[edge.p0].x, points[edge.p0].y);
			Vector2 p1 = new Vector2(points[edge.p1].x, points[edge.p1].y);
			Vector2 from = p0 - p1;

			for(int uvI = 0; uvI < uvs.Count(); uvI++) {
				Vector2 p0Uv = uvs[uvI][edge.p0];
				Vector2 p1Uv = uvs[uvI][edge.p1];
				Vector2 to = p0Uv - p1Uv;

				(float scale, float rotation) = Utils.GetVectorToVectorScaleRotation(from, to);

				info[uvI].scale = scale;
				info[uvI].rotation = rotation;

				if(Math.Abs(from.x) > uvOrientationThreshold && Math.Abs(from.y) > uvOrientationThreshold) info[uvI].scale = 0;
			}

			return info;
		}

		public static (float scale, float rotation)[] GetFrameIslandInfo(int[] island, Vector3[] points, List<(int p0, int p1)>[] pointEdges, List<Vector2[]> uvs, float uvOrientationThreshold) {
			(float scale, float rotation)[] total = new (float scale, float rotation)[uvs.Count()];
			int[] count = new int[uvs.Count()];
			foreach(int pI in island) {
				foreach(var edge in pointEdges[pI]) {
					(float scale, float rotation)[] edgeInfo = GetFrameEdgeInfo(edge, points, uvs, uvOrientationThreshold);
					for(int uvI = 0; uvI < uvs.Count(); uvI++) {
						if(edgeInfo[uvI].scale == 0) continue;
						total[uvI].scale += edgeInfo[uvI].scale;
						total[uvI].rotation += edgeInfo[uvI].rotation;
						count[uvI]++;
					}
				}
			}

			for(int uvI = 0; uvI < uvs.Count(); uvI++) {
				total[uvI].scale /= count[uvI];
				total[uvI].rotation /= count[uvI];
			}

			return total;
		}

		public static void OffsetFramePointUvs(int index, List<Vector2[]> uvs, (float scale, float rotation)[] info, Vector3 offset3d) {
			Vector2 offset2d = new Vector2(offset3d.x, offset3d.y);

			for(int uvI = 0; uvI < uvs.Count(); uvI++) {
				Vector2 uvOffset = Utils.RotateVector(offset2d, -info[uvI].rotation) * info[uvI].scale;
				uvs[uvI][index] += uvOffset;
			}
		}

		public static void TriListOtherPoints(int pIndex, out int p0Index, out int p1Index) {
			int part = pIndex % 3;
			int location = pIndex - part;
			p0Index = location + ((part + 1) % 3);
			p1Index = location + ((part + 2) % 3);
		}

		public static Vector3 FramePointOffset(Vector3 point, Vector2 offset, Vector2 frameSize) {
			if(point.x < -frameSize.x * 0.5f + 0.00025f) offset.x = -offset.x;
			else if(point.x < frameSize.x * 0.5f - 0.00025f) offset.x = 0;

			if(point.y < -frameSize.y * 0.5f + 0.00025f) offset.y = -offset.y;
			else if(point.y < frameSize.y * 0.5f - 0.00025f) offset.y = 0;

			return new Vector3(offset.x, offset.y, 0);
		}

		public static void ResizeFrameMesh(Mesh mesh, Vector2 offset, Vector2 frameSize, bool offsetUvs, float uvOrientationThreshold, Vector3 photoOffset, Quaternion photoRotation) {
			Quaternion inversePhotoRotation = Quaternion.Inverse(photoRotation);
			Vector3[] points = mesh.vertices;
			Vector3[] alignedPoints = points.Select(p => inversePhotoRotation * (p - photoOffset)).ToArray();
			bool[] pMoved = new bool[points.Length];
			List<int> triangles = new List<int>(mesh.triangles);
			List<Vector2[]> uvs = GetMeshUvs(mesh);
			List<(int p0, int p1)> edges = GetMeshEdges(triangles, points.Length);
			List<(int p0, int p1)>[] pointEdges = GetMeshEdgesPerPoints(edges, points.Length);

			int[][] islands_pointIndex = Edges_To_Islands(edges, points.Length);
			var islands_info = islands_pointIndex.Select(island => GetFrameIslandInfo(island, alignedPoints, pointEdges, uvs, uvOrientationThreshold)).ToList();

			for(int islandI = 0; islandI < islands_pointIndex.Count(); islandI++) {
				var islandInfo = islands_info[islandI];
				foreach(int pI in islands_pointIndex[islandI]) {
					Vector3 offset3d = FramePointOffset(alignedPoints[pI], offset, frameSize);
					if(offsetUvs) OffsetFramePointUvs(pI, uvs, islandInfo, offset3d);

					points[pI] = photoOffset + (photoRotation * (alignedPoints[pI] + offset3d));
				}
			}

			//Utils.MeshVisualizeUvs(points, triangles, uvs[0]);
			//int triIndex = 0;
			//for(int i = 0; i < mesh.subMeshCount; i++) {
			//	int submeshLength = mesh.GetTriangles(i).Length;
			//	mesh.SetTriangles(triangles.GetRange(triIndex, submeshLength).ToArray(), i);
			//	triIndex += submeshLength;
			//}

			mesh.vertices = points;
			for(int i = 0; i < uvs.Count(); i++) mesh.SetUVs(i, uvs[i]);
		}
	}
}
#endif