#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace codec.PhotoFrame {
	[DisallowMultipleComponent]
	[ExecuteInEditMode]
	[AddComponentMenu("Scripts/Photo Frame")]
	public class PhotoFrame : MonoBehaviour {
		public enum ResolutionType {
			UseSceneSettings, AbsoluteMajor, Relative, Full
		}

		public static Material defaultMaterial() {
			Material material = new Material(Shader.Find("Unlit/Texture"));
			material.hideFlags = HideFlags.NotEditable;
			return material;
		}

		private static Material _defaultMaterial = null;
		public Material photoMaterial {
			get {
				if(frameType?.material) return frameType?.material;
				if(!_defaultMaterial) _defaultMaterial = defaultMaterial();
				return _defaultMaterial;
			}
		}
		public string photoMaterialTextureSlot => (frameType && frameType.material && frameType.textureSlot != "") ? frameType.textureSlot : "_MainTex";

		[NonSerialized] public SceneSettings sceneSettings;

		public PhotoFrameType frameType;

		public string photoGUID = "";
		private Texture2D _photo;
		private string _photoGUID;
		public Texture2D photo {
			get {
				if(_photoGUID != photoGUID) {
					if(photoGUID == "") _photo = null;
					else _photo = (Texture2D)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(photoGUID), typeof(Texture2D));
					_photoGUID = photoGUID;
					updatePhotoInfo();
				}
				return _photo;
			}
			set {
				if(value == _photo) return;
				string guid = "";
				if(value) {
					bool loaded = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out long _);
					if(!loaded) throw new Exception("Photo does not have GUID (maybe this texture is not an imported asset)");
				}
				photoGUID = _photoGUID = guid;
				_photo = value;
				updatePhotoInfo();
			}
		}
		public Vector2Int photoSourceSize { get; private set; }
		public Vector2Int photoUseSize { get; private set; }
		[NonSerialized] public bool isPhotoStretchedWarn = false;

		public bool autoSelectAspectRatio = true;
		public Vector2 aspectRatio = Vector2.one;
		public float cropScale = 1;
		public float cropOffsetX = 0;
		public float cropOffsetY = 0;

		public bool dontBakePhotoUseSource = false;
		public ResolutionType resolutionType = ResolutionType.UseSceneSettings;
		public float resolutionValue = 1280;

		public bool bakedData;
		public GameObject framePrefab;
		[NonSerialized] public PhotoFramePreview preview;

		public void Awake() {
			hideFlags = HideFlags.DontSaveInBuild;
			if(!bakedData && EditorSettings.livePreview && !preview) {
				EditorApplication.delayCall += () => { //wait for scene to load
					if(preview = GetComponent<PhotoFramePreview>()) GameObject.DestroyImmediate(preview);
					preview = gameObject.AddComponent<PhotoFramePreview>();
					preview.init();
					preview.updatePreview();
				};
			}

		}

		public void Reset() {
			photo = null;
		}

		public static PhotoFrame Create() {
			PhotoFrame photoObject = new GameObject("Photo Frame", typeof(PhotoFrame)).GetComponent<PhotoFrame>();
			var iconContent = EditorGUIUtility.IconContent("sv_label_0");
			var setIcon = typeof(EditorGUIUtility).GetMethod("SetIconForObject", BindingFlags.Static | BindingFlags.NonPublic)
				?? typeof(EditorGUIUtility).GetMethod("SetIconForObject", BindingFlags.Static | BindingFlags.Public);
			if(setIcon != null) setIcon.Invoke(null, new object[] { photoObject.gameObject, (Texture2D)iconContent.image });
			return photoObject;
		}

		public static void UpdateAll(bool assetUpdate) {
			if(!EditorSettings.livePreview) return;

			EditorApplication.delayCall += () => {
				foreach(var photoFrame in Utils.LoadedScenes_FindComponentsOfType<PhotoFrame>()) {
					if(assetUpdate) photoFrame.updatePhotoInfo();
					photoFrame.enableAndUpdatePreview();
				}
			};
		}

		public static void TextureUpdate(Texture2D texture) {
			if(!EditorSettings.livePreview) return;

			EditorApplication.delayCall += () => {
				string guid = "";
				bool loaded = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture, out guid, out long _);
				if(!loaded) return;
				foreach(var photoFrame in Utils.LoadedScenes_FindComponentsOfType<PhotoFrame>()) {
					if(photoFrame.photoGUID != guid) continue;
					photoFrame.updatePhotoInfo();
					photoFrame.enableAndUpdatePreview();
				}
			};
		}

		public static void ModelUpdate(GameObject obj) {
			if(!EditorSettings.livePreview) return;

			EditorApplication.delayCall += () => {
				foreach(var photoFrame in Utils.LoadedScenes_FindComponentsOfType<PhotoFrame>()) {
					if(photoFrame.preview == null || !photoFrame.preview.framePrefab || !photoFrame.preview.framePrefab.isGenerated || photoFrame.preview.framePrefab.generateSource != obj) continue;
					photoFrame.preview.framePrefab.generateSource = null;
					photoFrame.enableAndUpdatePreview();
				}
			};
		}

		public bool assureSceneSettings() {
			if(sceneSettings) return true;
			if(PrefabStageUtility.GetCurrentPrefabStage() != null || gameObject.scene.name == null) return false;
			sceneSettings = SceneSettings.ofScene(gameObject.scene);
			return true;
		}

		public void fixPhotoStretch() {
			if(photo == null || isPhotoStretchedWarn == false) return;

			TextureImporter textureImporter = (TextureImporter)TextureImporter.GetAtPath(AssetDatabase.GetAssetPath(photo));
			textureImporter.npotScale = TextureImporterNPOTScale.None;
			EditorUtility.SetDirty(textureImporter);
			textureImporter.SaveAndReimport();
		}

		public void updatePhotoInfo() {
			isPhotoStretchedWarn = false;
			photoSourceSize = Vector2Int.zero;
			photoUseSize = Vector2Int.zero;
			if(photo == null) return;

			TextureImporter textureImporter = (TextureImporter)TextureImporter.GetAtPath(AssetDatabase.GetAssetPath(photo));
			photoSourceSize = Utils.GetTextureSourceSize(textureImporter);
			bool isPow2 = Mathf.IsPowerOfTwo(photoSourceSize.x) && Mathf.IsPowerOfTwo(photoSourceSize.y);
			isPhotoStretchedWarn = !isPow2 && textureImporter.npotScale != TextureImporterNPOTScale.None;

			float majorSize = Mathf.Max(photoSourceSize.x, photoSourceSize.y);
			if(majorSize > textureImporter.maxTextureSize) {
				Vector2 useSize = ((Vector2)photoSourceSize) * (textureImporter.maxTextureSize / majorSize);
				photoUseSize = new Vector2Int((int)Mathf.Round(useSize.x), (int)Mathf.Round(useSize.y));
			}
			else photoUseSize = photoSourceSize;
		}

		public void getAspectRatios(out float photoAspectRatio, out float frameAspectRatio, out int frameIndex) {
			if(photo == null) photoAspectRatio = 1;
			else photoAspectRatio = photoUseSize.x / (float)photoUseSize.y;

			float targetRatio = autoSelectAspectRatio ? photoAspectRatio : Utils.ConvertRatio(aspectRatio);
			frameAspectRatio = photoAspectRatio;
			frameIndex = frameType?.findFrame(targetRatio, !autoSelectAspectRatio, out frameAspectRatio) ?? -1;
		}

		public Vector2Int getFinalResolution(bool copped = true, bool scaled = true) {
			if(photo == null) return new Vector2Int(0, 0);

			Vector2 res = new Vector2(photoUseSize.x, photoUseSize.y);

			if(copped) {
				getAspectRatios(out float photoAspectRatio, out float frameAspectRatio, out _);

				float percentCutoff = (photoAspectRatio - frameAspectRatio) / Mathf.Max(photoAspectRatio, frameAspectRatio);
				if(percentCutoff > 0) res.x *= 1f - percentCutoff;
				else res.y *= 1f + percentCutoff;

				res *= cropScale;
			}

			if(scaled && !dontBakePhotoUseSource && resolutionType != ResolutionType.Full) {
				bool isInScene = assureSceneSettings();
				if(resolutionType == ResolutionType.UseSceneSettings) {
					int maxMajorSize = SceneSettings.default_resolutionMaxMajorSize;
					if(isInScene) maxMajorSize = sceneSettings.resolutionMaxMajorSize;

					res *= Mathf.Clamp01(Math.Min(1, maxMajorSize / Math.Max(res.x, res.y)));
				}
				else if(resolutionType == ResolutionType.AbsoluteMajor) res *= Mathf.Clamp01(Math.Min(1, Mathf.Round(resolutionValue) / Math.Max(res.x, res.y)));
				else if(resolutionType == ResolutionType.Relative) res *= Mathf.Clamp01(resolutionValue);

				bool scaleResolutionBySize = SceneSettings.default_scaleResolutionBySize;
				float scaleResMin = SceneSettings.default_scaleResMin;
				float scaleResMax = SceneSettings.default_scaleResMax;
				if(isInScene) {
					scaleResolutionBySize = sceneSettings.scaleResolutionBySize;
					scaleResMin = sceneSettings.scaleResMin;
					scaleResMax = sceneSettings.scaleResMax;
				}

				if(scaleResolutionBySize && scaleResMin < scaleResMax && scaleResMax != 0) {
					Vector3 scaleVector = transform.lossyScale;
					float scale = (scaleVector.x + scaleVector.y + scaleVector.z) / 3f;
					scale = Mathf.Max(scale, scaleResMin);
					scale /= scaleResMax;

					res *= scale;
				}
			}

			return new Vector2Int((int)Mathf.Max(1, Mathf.Ceil(res.x)), (int)Mathf.Max(1, Mathf.Ceil(res.y)));
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

			uvMin = (uvMin - new Vector2(0.5f, 0.5f)) * cropScale + new Vector2(0.5f, 0.5f);
			uvMax = (uvMax - new Vector2(0.5f, 0.5f)) * cropScale + new Vector2(0.5f, 0.5f);

			Vector2 uvOff = Vector2.zero;

			if(cropOffsetX > 0) uvOff.x = (1.0f - uvMax.x) * cropOffsetX;
			else uvOff.x = uvMin.x * cropOffsetX;

			if(cropOffsetY > 0) uvOff.y = (1.0f - uvMax.y) * cropOffsetY;
			else uvOff.y = uvMin.y * cropOffsetY;

			uvMin += uvOff;
			uvMax += uvOff;
		}

		public Vector2 getPhotoWorldSize(float frameAspectRatio, int frameIndex, out Vector2 frameScale) {
			Vector2 size;
			frameScale = Vector2.one;

			if(frameType && frameType.photoDimensions == FrameTypeDimensions.UseAspectRatio && frameIndex != -1) {
				size = frameType.aspectRatios[frameIndex];
				if(size.x == 0 || size.y == 0) size = Vector2.one;
			}
			else if(frameType && frameType.photoDimensions == FrameTypeDimensions.SmallerSideIsOne) {
				size = Utils.RatioToSize_SmallSideOne(frameIndex == -1 ? frameAspectRatio : Utils.ConvertRatio(frameType.aspectRatios[frameIndex]));
			}
			else {
				size = Utils.RatioToSize_BigSideOne(frameIndex == -1 ? frameAspectRatio : Utils.ConvertRatio(frameType.aspectRatios[frameIndex]));
			}

			if(frameType && frameType.frameMatching == FrameMatching.ScaleToPhoto && frameIndex != -1) {
				frameScale = Utils.RatioToSize_AreaOne(Utils.Multiplier_fromRatioToRatio(size, frameAspectRatio));
				size *= frameScale;
			}
			else if(frameType && frameType.frameMatching == FrameMatching.GenerateFrame) {
				size *= Utils.RatioToSize_SmallSideOne(Utils.Multiplier_fromRatioToRatio(size, frameAspectRatio));
			}

			return size;
		}

		public static void SetupMesh(Mesh mesh, Vector2 size, Vector2 uvMin, Vector2 uvMax, bool uvRotate, Vector3 offset, Vector3 rotation) {
			Matrix4x4 matrix = Matrix4x4.TRS(Vector3.one, Quaternion.Euler(rotation), Vector3.one);

			mesh.Clear();
			mesh.vertices = new Vector3[] {
				offset + (Quaternion.Euler(rotation) * new Vector3(-0.5f * size.x, -0.5f * size.y, 0)),
				offset + (Quaternion.Euler(rotation) * new Vector3(0.5f * size.x, -0.5f * size.y, 0)),
				offset + (Quaternion.Euler(rotation) * new Vector3(0.5f * size.x, 0.5f * size.y, 0)),
				offset + (Quaternion.Euler(rotation) * new Vector3(-0.5f * size.x, 0.5f * size.y, 0)),
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

		public void enableAndUpdatePreview(bool recordUndo = false) {
			if(bakedData || !gameObject.scene.isLoaded) return;
			if(preview == null) {
				if(preview = GetComponent<PhotoFramePreview>()) Undo.DestroyObjectImmediate(preview);
				if(recordUndo) preview = Undo.AddComponent<PhotoFramePreview>(gameObject);
				else preview = gameObject.AddComponent<PhotoFramePreview>();
				preview.init(recordUndo);
			}
			preview.updatePreview(recordUndo);
		}

		public void disablePreview(bool recordUndo = false) {
			if(bakedData || preview == null) return;
			if(recordUndo) Undo.DestroyObjectImmediate(preview);
			else GameObject.DestroyImmediate(preview);
		}

		public void deleteBakedData(bool recordUndo = false) {
			if(!bakedData) return;

			if(framePrefab) {
				if(recordUndo) {
					Undo.DestroyObjectImmediate(framePrefab);
				}
				else {
					EditorUtility.SetDirty(framePrefab);
					DestroyImmediate(framePrefab);
				}
			}

			var meshFilter = GetComponent<MeshFilter>();
			if(meshFilter) {
				if(recordUndo) Undo.DestroyObjectImmediate(meshFilter);
				else {
					EditorUtility.SetDirty(meshFilter);
					DestroyImmediate(meshFilter);
				}
			}

			var meshRenderer = GetComponent<MeshRenderer>();
			if(meshRenderer) {
				if(recordUndo) Undo.DestroyObjectImmediate(meshRenderer);
				else {
					EditorUtility.SetDirty(meshRenderer);
					DestroyImmediate(meshRenderer);
				}
			}

			var s_obj = new SerializedObject(this);
			s_obj.Update();
			var s_framePrefab = s_obj.FindProperty("framePrefab");
			var s_bakedData = s_obj.FindProperty("bakedData");

			if(PrefabUtility.IsPartOfAnyPrefab(this)) {
				PrefabUtility.RevertPropertyOverride(s_framePrefab, InteractionMode.AutomatedAction);
				PrefabUtility.RevertPropertyOverride(s_bakedData, InteractionMode.AutomatedAction);
			}
			else {
				s_bakedData.boolValue = false;
				if(recordUndo) s_obj.ApplyModifiedProperties();
				else s_obj.ApplyModifiedPropertiesWithoutUndo();
			}

			PrefabUtility.RecordPrefabInstancePropertyModifications(this);
		}

		public delegate GameObject GetSavedFrame(PhotoFrameType type, int index, float ratio, Vector2 size);
		public Mesh setBakedData(string folder, Material material, Vector2 uvMin, Vector2 uvMax, bool uvRotate, GetSavedFrame getSavedFrame) {
			if(preview != null) disablePreview();
			deleteBakedData();

			var meshFilter = GetComponent<MeshFilter>();
			if(meshFilter) DestroyImmediate(meshFilter);

			var meshRenderer = GetComponent<MeshRenderer>();
			if(meshRenderer) DestroyImmediate(meshRenderer);

			meshFilter = gameObject.AddComponent<MeshFilter>();
			meshFilter.sharedMesh = new Mesh();

			meshRenderer = gameObject.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = material;

			getAspectRatios(out float photoAspectRatio, out float frameAspectRatio, out int frameIndex);
			getCropUV(photoAspectRatio, frameAspectRatio, out _, out _);
			var size = getPhotoWorldSize(frameAspectRatio, frameIndex, out Vector2 frameScale);

			SetupMesh(meshFilter.sharedMesh, size, uvMin, uvMax, uvRotate, frameType?.photoOffset ?? Vector3.zero, frameType?.photoRotation ?? Vector3.zero);

			GameObject frameSelected = getSavedFrame(frameType, frameIndex, frameAspectRatio, size);
			if(frameSelected) {
				framePrefab = (GameObject)PrefabUtility.InstantiatePrefab(frameSelected, transform);
				framePrefab.AddComponent<MarkTypeBaked>();

				Vector3 scale = frameSelected.transform.localRotation * new Vector3(frameScale.x, frameScale.y, 1);
				scale.Set(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
				framePrefab.transform.localScale = Vector3.Scale(frameSelected.transform.localScale, scale);

				var staticFlags = GameObjectUtility.GetStaticEditorFlags(gameObject);
				Utils.RunOnObjectAndAllChildren(framePrefab, obj => GameObjectUtility.SetStaticEditorFlags(obj, staticFlags));
				EditorUtility.SetDirty(framePrefab);
			}

			bakedData = true;

			EditorUtility.SetDirty(meshFilter);
			EditorUtility.SetDirty(meshRenderer);
			PrefabUtility.RecordPrefabInstancePropertyModifications(this);

			return meshFilter.sharedMesh;
		}

		public void unlock(bool recordUndo) {
			if(!bakedData) return;
			if(recordUndo) Undo.IncrementCurrentGroup();
			deleteBakedData(recordUndo);
			enableAndUpdatePreview(recordUndo);
			if(recordUndo) Undo.SetCurrentGroupName("Unlock " + gameObject.name);
		}
	}
}
#endif