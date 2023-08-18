#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace codec.PhotoFrame {
	[DisallowMultipleComponent]
	[ExecuteInEditMode]
	public class PhotoFrame : MonoBehaviour {
		public PhotoFrameType frameType;
		public int frameSize;
		public Vector2 noFrameAspectRatio;
		[NonSerialized] public Texture2D photo;
		[NonSerialized] public string currentPhotoGUID = "";
		public string photoGUID;
		public bool autoSelectFrameSize = true;
		public float cropScalePercent = 1;
		public Vector2 cropOffset = Vector2.zero;
		public bool isAbsolsuteRes = true;
		public float resolutionScale = 1;
		public int resolutionMaxMajorSize = 1280;

		public bool savedData;
		public GameObject framePrefab;
		public MeshFilter meshFilter;
		public MeshRenderer meshRenderer;

		[NonSerialized] public RenderTexture resizeTexture;
		[NonSerialized] public Texture photoInResizeTexture;
		[NonSerialized] public MaterialPropertyBlock textureOverride;
		[NonSerialized] public Material defaultMaterial;

		public void Awake() {
			CheckPhotoIsSet();
			//fixes duplication
			if(!savedData && framePrefab) DestroyImmediate(framePrefab);
			if(!savedData && meshFilter) DestroyImmediate(meshFilter);
			if(!savedData && meshRenderer) DestroyImmediate(meshRenderer);
			if(!savedData) updateEditorPreview();
		}

		public void snap(Vector3 dir) {
			dir = dir.normalized;

			Bounds combinedBounds;
			if(meshFilter != null) combinedBounds = meshFilter.sharedMesh.bounds;
			else combinedBounds = new Bounds();

			if(frameType != null && frameType.photoFrames.Length > 0) {
				GameObject frame = frameType.photoFrames[Math.Min(frameSize, frameType.aspectRatios.Length - 1)];
				var renderers = frame.GetComponentsInChildren<Renderer>();
				foreach(var renderer in renderers) combinedBounds.Encapsulate(renderer.bounds);
			}

			combinedBounds.center = Vector3.Scale(combinedBounds.center, transform.lossyScale);
			combinedBounds.size = Vector3.Scale(combinedBounds.size, transform.lossyScale);


			bool isHit = Physics.BoxCast(transform.position + combinedBounds.center, combinedBounds.size * 0.5f, dir, out RaycastHit hitInfo, transform.rotation);
			if(!isHit) return;

			transform.position += hitInfo.distance * dir;
		}

		public void getAspectRatios(out float photoAspectRatio, out float frameAspectRatio) {
			CheckPhotoIsSet();
			if(photo == null) photoAspectRatio = 1;
			else photoAspectRatio = (float)photo.width / (float)photo.height;
			if(frameType?.aspectRatios != null && frameType.aspectRatios.Length > 0) {
				Vector2 frameAspectRatioVector = frameType.aspectRatios[Math.Min(frameSize, frameType.aspectRatios.Length - 1)];
				frameAspectRatio = 1;
				if(frameAspectRatioVector.x != 0 && frameAspectRatioVector.y != 0) frameAspectRatio = frameAspectRatioVector.x / frameAspectRatioVector.y;
			}
			else {
				frameAspectRatio = 1;
				if(noFrameAspectRatio.x != 0 && noFrameAspectRatio.y != 0) frameAspectRatio = noFrameAspectRatio.x / noFrameAspectRatio.y;
			}
		}

		public Vector2Int getFinalResolution(bool copped = true, bool scaled = true) {
			CheckPhotoIsSet();
			if(photo == null) return new Vector2Int(0, 0);

			Vector2 res = new Vector2(photo.width, photo.height);

			if(copped) {
				getAspectRatios(out float photoAspectRatio, out float frameAspectRatio);

				float percentCutoff = (photoAspectRatio - frameAspectRatio) / Mathf.Max(photoAspectRatio, frameAspectRatio);
				if(percentCutoff > 0) res.x *= 1f - percentCutoff;
				else res.y *= 1f + percentCutoff;

				res *= cropScalePercent;
			}

			if(scaled) {
				if(isAbsolsuteRes) res *= Math.Min(1, resolutionMaxMajorSize / Math.Max(res.x, res.y));
				else res *= resolutionScale;
			}

			return new Vector2Int((int)Mathf.Ceil(res.x), (int)Mathf.Ceil(res.y));
		}

		public void getCropUV(float photoAspectRatio, float frameAspectRatio, out Vector2 uvMin, out Vector2 uvMax) {
			float percentCutoff = (photoAspectRatio - frameAspectRatio) / Mathf.Max(photoAspectRatio, frameAspectRatio);

			uvMin = new Vector2(0, 0);
			uvMax = new Vector2(1, 1);

			if(percentCutoff > 0) {
				uvMin.x += percentCutoff * 0.5f;
				uvMax.x -= percentCutoff * 0.5f;
			}
			else {
				uvMin.y -= percentCutoff * 0.5f;
				uvMax.y += percentCutoff * 0.5f;
			}

			uvMin = (uvMin - new Vector2(0.5f, 0.5f)) * cropScalePercent + new Vector2(0.5f, 0.5f);
			uvMax = (uvMax - new Vector2(0.5f, 0.5f)) * cropScalePercent + new Vector2(0.5f, 0.5f);

			Vector2 uvOff = Vector2.zero;

			if(cropOffset.x > 0) uvOff.x = (1.0f - uvMax.x) * cropOffset.x;
			else uvOff.x = uvMin.x * cropOffset.x;

			if(cropOffset.y > 0) uvOff.y = (1.0f - uvMax.y) * cropOffset.y;
			else uvOff.y = uvMin.y * cropOffset.y;

			uvMin += uvOff;
			uvMax += uvOff;
		}

		public Vector2 getPhotoRealSize(float frameAspectRatio) {
			Vector2 size = new Vector2(1, 1);
			
			if(frameType != null && frameType.isSmallerDimensionAlwaysOne) {
				if(frameAspectRatio > 1) size.x *= frameAspectRatio;
				else size.y /= frameAspectRatio;
			}
			else {
				if(frameAspectRatio > 1) size.y /= frameAspectRatio;
				else size.x *= frameAspectRatio;
			}

			return size;
		}

		public static void SetupMesh(Mesh mesh, Vector2 size, Vector2 uvMin, Vector2 uvMax, bool uvRotate) {
			mesh.Clear();
			mesh.vertices = new Vector3[] {
				new Vector3(-0.5f * size.x, -0.5f * size.y, 0),
				new Vector3(0.5f * size.x, -0.5f * size.y, 0),
				new Vector3(0.5f * size.x, 0.5f * size.y, 0),
				new Vector3(-0.5f * size.x, 0.5f * size.y, 0),
			};
			if(uvRotate) mesh.uv = new Vector2[] {
				new Vector2(uvMax.x, uvMax.y),
				new Vector2(uvMax.x, uvMin.y),
				new Vector2(uvMin.x, uvMin.y),
				new Vector2(uvMin.x, uvMax.y),
			};
			else mesh.uv = new Vector2[] {
				new Vector2(uvMax.x, uvMin.y),
				new Vector2(uvMin.x, uvMin.y),
				new Vector2(uvMin.x, uvMax.y),
				new Vector2(uvMax.x, uvMax.y),
			};
			mesh.SetTriangles(new int[] { 0, 1, 2, 2, 3, 0 }, 0);
			mesh.RecalculateBounds();
		}

		public void doAutoSelectFrameSize() {
			frameSize = 0;
			if(photo == null) return;

			float photoAspectRatio = (float)photo.width / (float)photo.height, bestCutoutPercent = 1;

			if(frameType?.aspectRatios == null || frameType.aspectRatios.Length == 0) {
				noFrameAspectRatio = new Vector2(photoAspectRatio, 1);
				return;
			}

			for(int i = 0; i < frameType.aspectRatios.Length; i++) {
				float frameAspectRatio = 1;
				if(frameType.aspectRatios[i].x != 0 && frameType.aspectRatios[i].y != 0) frameAspectRatio = frameType.aspectRatios[i].x / frameType.aspectRatios[i].y;
				float cutoutPercent = 1.0f - Mathf.Min(frameAspectRatio, photoAspectRatio) / Mathf.Max(frameAspectRatio, photoAspectRatio);

				if(bestCutoutPercent > cutoutPercent) {
					frameSize = i;
					bestCutoutPercent = cutoutPercent;
				}
			}
		}

		public GameObject getSelectedFrame() {
			if(frameType == null || frameType.photoFrames == null || frameType.photoFrames.Length == 0) return null;
			return frameType.photoFrames[System.Math.Min(frameSize, frameType.photoFrames.Length - 1)];
		}

		public GameObject getActiveFrame() {
			if(framePrefab == null) return null;
			return (GameObject)PrefabUtility.GetCorrespondingObjectFromSource(framePrefab);
		}

		//public void trueTextureSize() {
		//	string assetPath = AssetDatabase.GetAssetPath(photo);
		//	TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

		//	object[] args = new object[2] { 0, 0 };
		//	MethodInfo mi = typeof(TextureImporter).GetMethod("GetWidthAndHeight", BindingFlags.NonPublic | BindingFlags.Instance);
		//	mi.Invoke(importer, args);

		//	truePhotoRes = new Vector2Int((int)args[0], (int)args[1]);
		//}

		public void CheckPhotoIsSet() {
			if(photoGUID == null || photoGUID == "") {
				photo = null;
				return;
			}

			if(photoGUID == currentPhotoGUID) return;

			photo = (Texture2D)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(photoGUID), typeof(Texture2D));
			currentPhotoGUID = photoGUID;
		}

		public void updateResizeTexture() {
			CheckPhotoIsSet();
			var res = getFinalResolution(false);
			if(resizeTexture == null || res.x != resizeTexture.width || res.y != resizeTexture.height) {
				if(resizeTexture != null) resizeTexture.Release();

				resizeTexture = new RenderTexture(res.x, res.y, 0);
				photoInResizeTexture = null;
			}

			if(photoInResizeTexture != photo) {
				RenderTexture.active = resizeTexture;
				Graphics.Blit(photo, resizeTexture);
				RenderTexture.active = null;
				photoInResizeTexture = photo;
			}
		}

		public void turnOffEditorPreview() {
			if(savedData) return;
			if(framePrefab) DestroyImmediate(framePrefab);
			if(meshFilter) DestroyImmediate(meshFilter);
			if(meshRenderer) DestroyImmediate(meshRenderer);
			if(resizeTexture != null) resizeTexture.Release();
			photoInResizeTexture = null;
		}

		public void updateEditorPreview() {
			if(savedData) return;

			bool livePreview = EditorPrefs.GetBool("wtf.codec.photo-frame-manager.livePreview", true);
			if(!livePreview) return;

			if(!defaultMaterial) {
				defaultMaterial = new Material(Shader.Find("Unlit/Texture"));
				defaultMaterial.hideFlags = HideFlags.NotEditable;
			}

			if(autoSelectFrameSize) doAutoSelectFrameSize();

			GameObject frameSelected = getSelectedFrame();
			GameObject frameActive = getActiveFrame();
			if(frameSelected != frameActive) {
				if(framePrefab) DestroyImmediate(framePrefab);
				if(frameSelected) {
					framePrefab = (GameObject)PrefabUtility.InstantiatePrefab(frameSelected, transform);
					framePrefab.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInHierarchy | HideFlags.NotEditable;
				}
			}

			if(!meshFilter) {
				if(meshFilter = gameObject.GetComponent<MeshFilter>()) {
					EditorUtility.SetDirty(meshFilter);
					DestroyImmediate(meshFilter);
				}

				meshFilter = gameObject.AddComponent<MeshFilter>();
				meshFilter.sharedMesh = new Mesh();
				meshFilter.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInInspector | HideFlags.NotEditable;
			}

			if(textureOverride == null) {
				textureOverride = new MaterialPropertyBlock();
			}

			if(!meshRenderer) {
				if(meshRenderer = gameObject.GetComponent<MeshRenderer>()) {
					EditorUtility.SetDirty(meshRenderer);
					DestroyImmediate(meshRenderer);
				}

				meshRenderer = gameObject.AddComponent<MeshRenderer>();
				meshRenderer.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInInspector | HideFlags.NotEditable;
			}

			getAspectRatios(out float photoAspectRatio, out float frameAspectRatio);
			getCropUV(photoAspectRatio, frameAspectRatio, out Vector2 uvMin, out Vector2 uvMax);
			var size = getPhotoRealSize(frameAspectRatio);
			SetupMesh(meshFilter.sharedMesh, size, uvMin, uvMax, false);

			if(frameType && frameType.material) meshRenderer.sharedMaterial = frameType.material;
			else meshRenderer.sharedMaterial = defaultMaterial;
			updateResizeTexture();
			textureOverride.Clear();
			if(frameType && frameType.material && frameType.textureSlot != "") textureOverride.SetTexture(frameType.textureSlot, resizeTexture, UnityEngine.Rendering.RenderTextureSubElement.Default);
			else textureOverride.SetTexture("_MainTex", resizeTexture, UnityEngine.Rendering.RenderTextureSubElement.Default);
			meshRenderer.SetPropertyBlock(textureOverride);
		}

		public void setSavedData(string folder, Material material, Vector2 uvMin, Vector2 uvMax, bool uvRotate) {
			unlock();
			savedData = true;

			if(framePrefab) DestroyImmediate(framePrefab);
			meshFilter = GetComponent<MeshFilter>();
			if(meshFilter) {
				if(meshFilter.sharedMesh) DestroyImmediate(meshFilter.sharedMesh);
				DestroyImmediate(meshFilter);
			}

			meshRenderer = GetComponent<MeshRenderer>();
			if(meshRenderer) {
				DestroyImmediate(meshRenderer);
			}

			GameObject frameSelected = getSelectedFrame();
			if(frameSelected) {
				framePrefab = (GameObject)PrefabUtility.InstantiatePrefab(frameSelected, transform);
				framePrefab.AddComponent<MarkTypeBaked>();
				GameObjectUtility.SetStaticEditorFlags(framePrefab, GameObjectUtility.GetStaticEditorFlags(gameObject));
				EditorUtility.SetDirty(framePrefab);
			}

			meshFilter = gameObject.AddComponent<MeshFilter>();
			meshFilter.sharedMesh = new Mesh();

			meshRenderer = gameObject.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = material;

			getAspectRatios(out float photoAspectRatio, out float frameAspectRatio);
			getCropUV(photoAspectRatio, frameAspectRatio, out _, out _);
			var size = getPhotoRealSize(frameAspectRatio);
			SetupMesh(meshFilter.sharedMesh, size, uvMin, uvMax, uvRotate);

			AssetDatabase.CreateAsset(meshFilter.sharedMesh, $"{folder}/Photo-Mesh-{System.Guid.NewGuid()}.asset");

			EditorUtility.SetDirty(meshFilter);
			EditorUtility.SetDirty(meshRenderer);
		}

		public void unlock() {
			if(savedData == false) return;
			savedData = false;

			if(framePrefab) {
				EditorUtility.SetDirty(framePrefab);
				DestroyImmediate(framePrefab);
			}

			meshFilter = GetComponent<MeshFilter>();
			if(meshFilter) {
				var meshAsset = meshFilter.sharedMesh;
				if(meshAsset) {
					string meshAssetPath = AssetDatabase.GetAssetPath(meshAsset);
					if(meshAssetPath != "") AssetDatabase.DeleteAsset(meshAssetPath);
				}
				EditorUtility.SetDirty(meshFilter);
				DestroyImmediate(meshFilter);
			}

			meshRenderer = GetComponent<MeshRenderer>();
			if(meshRenderer) {
				EditorUtility.SetDirty(meshRenderer);
				DestroyImmediate(meshRenderer);
			}
		}
	}
}
#endif