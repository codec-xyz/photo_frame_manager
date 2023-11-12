using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace codec.PhotoFrame {
	public static class Utils {
		public static IEnumerable<T> LoadedScenes_FindComponentsOfType<T>(bool activeSceneOnly = false) where T : Component {
			Scene activeScene = SceneManager.GetActiveScene();
			return Resources.FindObjectsOfTypeAll<T>().Where(pf => {
				if(activeSceneOnly) return pf.gameObject.scene == activeScene;
				return pf.gameObject.scene.name != null;
			});
		}

		public static string GetGUID(UnityEngine.Object obj) {
			if(obj == null) return "";
			bool loaded = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long _);
			if(!loaded) return "";
			return guid;
		}

		public static Vector2Int GetTextureSourceSize(TextureImporter importer) {
			object[] args = new object[2] { 0, 0 };
			MethodInfo m = typeof(TextureImporter).GetMethod("GetWidthAndHeight", BindingFlags.NonPublic | BindingFlags.Instance);
			m.Invoke(importer, args);

			return new Vector2Int((int)args[0], (int)args[1]);
		}

		public static Texture2D MakeTexture(int width, int height, Color col) {
			Color[] pix = new Color[width * height];
			for(int i = 0; i < pix.Length; ++i) {
				pix[i] = col;
			}
			Texture2D result = new Texture2D(width, height);
			result.SetPixels(pix);
			result.Apply();
			return result;
		}

		public delegate void GameObjectFunction(GameObject obj);
		public static void RunOnAllChildren(GameObject obj, GameObjectFunction func) {
			foreach(Transform child in obj.transform) func(child.gameObject);
		}
		public static void RunOnObjectAndAllChildren(GameObject obj, GameObjectFunction func) {
			func(obj);
			foreach(Transform child in obj.transform) func(child.gameObject);
		}

		public static List<List<Vector2>> GetMeshUVs(Mesh mesh) {
			var uvs = new List<List<Vector2>>();

			for(int i = 0; i < 8; i++) {
				var uvList = new List<Vector2>();
				mesh.GetUVs(i, uvList);
				if(uvList.Count() > 0) uvs.Add(uvList);
				else break;
			}

			return uvs;
		}

		public static Mesh JoinGameobjectMeshes(GameObject obj, out Material[] materials) {
			var materialsList = new List<Material>();
			var vertices = new List<Vector3>();
			var uvs = new List<List<Vector2>>();
			var normals = new List<Vector3>();
			var tangents = new List<Vector4>();
			var colors = new List<Color32>();
			var triangles = new List<List<int>>();

			foreach(var renderer in obj.GetComponentsInChildren<MeshRenderer>()) {
				MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
				if(!meshFilter || !meshFilter.sharedMesh) continue;
				Mesh mesh = meshFilter.sharedMesh;

				int vertexIndexOffset = vertices.Count();

				Vector3[] curVertices = mesh.vertices;
				Matrix4x4 matrix = renderer.transform.localToWorldMatrix;
				for(int i = 0; i < curVertices.Length; i++) curVertices[i] = matrix.MultiplyPoint(curVertices[i]);
				vertices.AddRange(curVertices);

				var addUvs = GetMeshUVs(mesh);
				while(uvs.Count() < addUvs.Count()) uvs.Add(new List<Vector2>());
				foreach(var uvsChannel in uvs) PadList(uvsChannel, Vector2.zero, vertexIndexOffset);
				for(int i = 0; i < addUvs.Count(); i++) uvs[i].AddRange(addUvs[i]);
				foreach(var uvsChannel in uvs) PadList(uvsChannel, Vector2.zero, vertexIndexOffset + mesh.vertexCount);

				Vector3[] curNormals = mesh.normals;
				for(int i = 0; i < curNormals.Length; i++) curNormals[i] = matrix.MultiplyPoint(curNormals[i]);
				normals.AddRange(curNormals);
				if(normals.Count() > 0) PadList(normals, Vector3.zero, vertexIndexOffset + mesh.vertexCount);

				tangents.AddRange(mesh.tangents);
				if(tangents.Count() > 0) PadList(tangents, Vector4.zero, vertexIndexOffset + mesh.vertexCount);

				colors.AddRange(mesh.colors32);
				if(colors.Count() > 0) PadList(colors, default, vertexIndexOffset + mesh.vertexCount);

				for(int i = 0; i < mesh.subMeshCount; i++) {
					Material material = renderer.sharedMaterials[Mathf.Min(renderer.sharedMaterials.Length - 1, i)];

					int submeshI = materialsList.IndexOf(material);
					if(submeshI == -1) {
						submeshI = materialsList.Count();
						materialsList.Add(material);
						triangles.Add(new List<int>());
					}

					int[] tris = mesh.GetTriangles(i);
					for(int tI = 0; tI < tris.Length; tI++) tris[tI] += vertexIndexOffset;
					triangles[submeshI].AddRange(tris);
				}
			}

			Mesh finalMesh = new Mesh();
			finalMesh.vertices = vertices.ToArray();
			for(int i = 0; i < uvs.Count(); i++) finalMesh.SetUVs(i, uvs[i]);
			finalMesh.normals = normals.ToArray();
			finalMesh.tangents = tangents.ToArray();
			finalMesh.colors32 = colors.ToArray();
			finalMesh.subMeshCount = triangles.Count();
			for(int i = 0; i < triangles.Count(); i++) finalMesh.SetTriangles(triangles[i], i);

			materials = materialsList.ToArray();
			return finalMesh;
		}

		public static void MeshVisualizeUvs(Vector3[] points, List<int> triangles, Vector2[] uvs) {
			for(int i = 0; i < points.Length; i++) {
				points[i] = new Vector3(uvs[i].x, uvs[i].y, 0);
			}

			for(int i = 0; i < triangles.Count(); i += 3) {
				Vector3 u = points[triangles[i + 1]] - points[triangles[i + 0]];
				Vector3 v = points[triangles[i + 2]] - points[triangles[i + 0]];

				Vector3 normalish = Vector3.Cross(u, v);

				if(normalish.z < 0) (triangles[i], triangles[i + 1]) = (triangles[i + 1], triangles[i]);
			}
		}

		public static bool FindCircleCircleIntersection(double2 cP0, double2 cP1, double cR0, double cR1, out double2 p0, out double2 p1) {
			p0 = double2.zero;
			p1 = double2.zero;

			double2 offset = cP1 - cP0;
			double distance = math.sqrt(offset.x * offset.x + offset.y * offset.y);

			if(distance == 0
			|| cR0 + cR1 < distance
			|| math.abs(cR0 - cR1) > distance) return false;

			double cR0_Sqr = cR0 * cR0, cR1_Sqr = cR1 * cR1;

			double a = (cR0_Sqr - cR1_Sqr + distance * distance) / (2 * distance);
			double h = math.sqrt(cR0_Sqr - a * a);

			offset /= distance;

			double2 center = cP0 + a * offset;

			offset = new double2(offset.y, -offset.x);

			p0 = center + h * offset;
			p1 = center - h * offset;
			return true;
		}

		public static (float scale, float rotation) GetVectorToVectorScaleRotation(Vector2 vecFrom, Vector2 vecTo) {
			if(vecFrom == Vector2.zero || vecTo == Vector2.zero) return (0, 0);
			Vector2 p = new Vector2(-vecTo.y, vecTo.x);
			float rotation = Mathf.Atan2(Vector2.Dot(vecFrom, p), Vector2.Dot(vecFrom, vecTo));
			return (Mathf.Sqrt(vecTo.sqrMagnitude / vecFrom.sqrMagnitude), rotation);
		}

		public static Vector2 RotateVector(Vector2 vec, float rotation) {
			float cosRot = Mathf.Cos(rotation);
			float sinRot = Mathf.Sin(rotation);

			return new Vector2(
				cosRot * vec.x - sinRot * vec.y,
				sinRot * vec.x + cosRot * vec.y
			);
		}

		public static T Collapse<T>(IEnumerable<T> targets, out bool isSame, T defaultValue = default) {
			isSame = false;
			bool first = false;
			T value = defaultValue;
			foreach(T target in targets) {
				if(!first) {
					value = target;
					first = true;
				}
				else if(!EqualityComparer<T>.Default.Equals(value, target)) return defaultValue;
			}
			isSame = true;
			return value;
		}

		public static void PadList<T>(List<T> list, T value, int count) {
			if(list.Count() >= count) return;
			int toGo = count - list.Count();
			list.AddRange(Enumerable.Repeat(value, toGo).ToList());
		}

		public static float ConvertRatio(Vector2 ratio, float defaultRatio = 1) => ratio.x != 0 && ratio.y != 0 ? ratio.x / ratio.y : defaultRatio;
		public static Vector2 ConvertRatio(float ratio) => new Vector2(ratio, 1);
		public static Vector2Int ConvertRatioInt(float ratio) => new Fraction(ratio);

		public static float Multiplier_fromRatioToRatio(float fromRatio, float toRatio) => toRatio / fromRatio;
		public static float Multiplier_fromRatioToRatio(Vector2 fromRatio, float toRatio) => toRatio / ConvertRatio(fromRatio);
		public static float Multiplier_fromRatioToRatio(float fromRatio, Vector2 toRatio) => ConvertRatio(toRatio) / fromRatio;
		public static float Multiplier_fromRatioToRatio(Vector2 fromRatio, Vector2 toRatio) => ConvertRatio(toRatio) / ConvertRatio(fromRatio);

		public static Vector2 RatioToSize_AreaOne(float ratio) {
			Vector2 scale = new Vector2(Mathf.Sqrt(ratio), 1);
			scale.y /= scale.x;
			return scale;
		}

		public static Vector2 RatioToSize_SmallSideOne(float ratio) {
			if(ratio > 1) return new Vector2(ratio, 1);
			return new Vector2(1, 1 / ratio);
		}

		public static Vector2 RatioToSize_BigSideOne(float ratio) {
			if(ratio > 1) return new Vector2(1, 1 / ratio);
			return new Vector2(ratio, 1);
		}

		public static void SnapGameObject(Vector3 dir, GameObject obj, float maxDistance = float.MaxValue) {
			dir = dir.normalized;

			Vector3[] vari = new Vector3[] {
				new Vector3(1, 1, 1),
				new Vector3(-1, 1, 1),
				new Vector3(1, -1, 1),
				new Vector3(-1, -1, 1),
				new Vector3(1, 1, -1),
				new Vector3(-1, 1, -1),
				new Vector3(1, -1, -1),
				new Vector3(-1, -1, -1),
			};

			var meshFilters = obj.GetComponentsInChildren<MeshFilter>();
			bool notSet = true;
			Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
			foreach(var meshFilter in meshFilters) {
				Bounds local = meshFilter.sharedMesh.bounds;
				Matrix4x4 transform = obj.transform.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
				if(notSet) {
					combinedBounds = new Bounds(transform * local.center, Vector3.zero);
					notSet = false;
				}

				for(int i = 0; i < vari.Length; i++) {
					combinedBounds.Encapsulate(transform * (local.center + Vector3.Scale(vari[i], local.extents)));
				}
			}

			combinedBounds.center = Vector3.Scale(combinedBounds.center, obj.transform.lossyScale);
			combinedBounds.size = Vector3.Scale(combinedBounds.size, obj.transform.lossyScale);
			bool isHit = Physics.BoxCast(obj.transform.position + combinedBounds.center, combinedBounds.extents, dir, out RaycastHit hitInfo, obj.transform.rotation, maxDistance);
			if(!isHit) return;

			obj.transform.position += hitInfo.distance * dir;
		}

		public static int Map(int value, int inMin, int inMax, int outMin, int outMax) {
			return (value - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
		}

		public static long Map(long value, long inMin, long inMax, long outMin, long outMax) {
			return (value - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
		}

		public static float Map(float value, float inMin, float inMax, float outMin, float outMax) {
			return (value - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
		}

		public static double Map(double value, double inMin, double inMax, double outMin, double outMax) {
			return (value - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
		}
	}

	public class Fraction {
		public int n;
		public int d;

		public Fraction(int _n, int _d) {
			n = _n;
			d = _d;
		}

		public Fraction(double value, double maxError = 0.000001) {
			int sign = Math.Sign(value);
			value = Math.Abs(value);

			int baseN = (int)Math.Floor(value);
			value -= baseN;

			if(value < maxError) {
				n = sign * baseN;
				d = 1;
				return;
			}
			else if(1 - maxError < value) {
				n = sign * (n + 1);
				d = 1;
				return;
			}

			double z = value;
			int previousDenominator = 0;
			int denominator = 1;
			int numerator;

			do {
				z = 1.0 / (z - (int)z);
				int temp = denominator;
				denominator = denominator * (int)z + previousDenominator;
				previousDenominator = temp;
				numerator = Convert.ToInt32(value * denominator);
			}
			while(Math.Abs(value - (double)numerator / denominator) > maxError && z != (int)z);

			n = sign * (baseN * denominator + numerator);
			d = denominator;
		}

		public override string ToString() {
			return $"{n}/{d}";
		}

		public string ToString(string divide = "/", string formating = "") {
			return n.ToString(formating) + divide + d.ToString(formating);
		}

		public static string ToString(double ratio, string divide = "/", string formating = "") {
			return new Fraction(ratio).ToString(divide, formating);
		}

		public static implicit operator Vector2Int(Fraction fraction) => new Vector2Int(fraction.n, fraction.d);
		public static implicit operator Vector2(Fraction fraction) => new Vector2(fraction.n, fraction.d);
	}
}