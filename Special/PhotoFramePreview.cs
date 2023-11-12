#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace codec.PhotoFrame {
	[DisallowMultipleComponent]
	[ExecuteInEditMode]
	[AddComponentMenu("")]
	public class PhotoFramePreview : MonoBehaviour {
		public static HideFlags hideFlagsSelf = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInHierarchy | HideFlags.NotEditable | HideFlags.HideInInspector;
		public static HideFlags hideFlagsFrame = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInHierarchy | HideFlags.NotEditable | HideFlags.HideInInspector;
		public static HideFlags hideFlagsPhoto = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.NotEditable | HideFlags.HideInInspector;

		public PhotoFrame photoFrame;
		public MarkTypeEditorFramePreview framePrefab;
		public MeshFilter meshFilter;
		public MeshRenderer meshRenderer;
		[NonSerialized] public RenderTexture previewTexture;
		[NonSerialized] public Texture photoInPreviewTexture;
		[NonSerialized] public MaterialPropertyBlock textureOverride;

		public void Awake() {
			hideFlags = hideFlagsSelf;
			Undo.undoRedoPerformed += OnUndoRedoEvent;
		}

		public void OnUndoRedoEvent() {
			updatePreview();
		}

		public void init(bool recordUndo = false) {
			photoFrame = GetComponent<PhotoFrame>();

			if(meshFilter = photoFrame.GetComponent<MeshFilter>()) {
				if(recordUndo) Undo.DestroyObjectImmediate(meshFilter);
				else {
					EditorUtility.SetDirty(meshFilter);
					GameObject.DestroyImmediate(meshFilter);
				}
			}

			if(meshRenderer = photoFrame.GetComponent<MeshRenderer>()) {
				if(recordUndo) Undo.DestroyObjectImmediate(meshRenderer);
				else {
					EditorUtility.SetDirty(meshRenderer);
					GameObject.DestroyImmediate(meshRenderer);
				}
			}

			foreach(Transform child in photoFrame.transform) {
				if(child.gameObject.GetComponent<MarkTypeEditorFramePreview>() == null) continue;
				if(recordUndo) Undo.DestroyObjectImmediate(child.gameObject);
				else {
					EditorUtility.SetDirty(child.gameObject);
					GameObject.DestroyImmediate(child.gameObject);
				}
			}

			if(recordUndo) meshFilter = Undo.AddComponent<MeshFilter>(photoFrame.gameObject);
			else meshFilter = photoFrame.gameObject.AddComponent<MeshFilter>();
			meshFilter.sharedMesh = new Mesh();
			meshFilter.hideFlags = hideFlagsPhoto;

			if(recordUndo) meshRenderer = Undo.AddComponent<MeshRenderer>(photoFrame.gameObject);
			else meshRenderer = photoFrame.gameObject.AddComponent<MeshRenderer>();
			meshRenderer.hideFlags = hideFlagsPhoto;

			textureOverride = new MaterialPropertyBlock();
		}

		public GameObject getActiveFrameSource() {
			if(framePrefab == null || framePrefab.isGenerated) return null;
			return PrefabUtility.GetCorrespondingObjectFromSource(framePrefab.gameObject);
		}

		public static void UpdatePreviewTexture(ref PhotoFrame photoFrame, ref RenderTexture previewTexture, ref Texture photoInPreviewTexture) {
			var res = photoFrame.getFinalResolution(false);
			if(previewTexture == null || res.x != previewTexture.width || res.y != previewTexture.height) {
				if(previewTexture != null) previewTexture.Release();

				previewTexture = new RenderTexture(Math.Max(1, res.x), Math.Max(1, res.y), 0);
				photoInPreviewTexture = null;
			}

			if(photoInPreviewTexture != photoFrame.photo) {
				RenderTexture.active = previewTexture;
				Graphics.Blit(photoFrame.photo, previewTexture);
				RenderTexture.active = null;
				photoInPreviewTexture = photoFrame.photo;
			}
		}

		public void deleteFrame(bool recordUndo) {
			if(framePrefab && framePrefab.isGenerated) {
				MeshFilter meshFilter = framePrefab.GetComponent<MeshFilter>();
				if(meshFilter && meshFilter.sharedMesh) {
					if(recordUndo) Undo.DestroyObjectImmediate(meshFilter.sharedMesh);
					else GameObject.DestroyImmediate(meshFilter.gameObject);
				}
			}
			if(framePrefab) {
				if(recordUndo) Undo.DestroyObjectImmediate(framePrefab.gameObject);
				else GameObject.DestroyImmediate(framePrefab.gameObject);
			}
		}

		public void setFrame(bool recordUndo, GameObject newFrame, bool isGenerated, GameObject generateSource, float aspectRatio) {
			if(newFrame == null) return;

			GameObject framePrefabGameObject;

			if(isGenerated) {
				if(recordUndo) {
					Undo.RegisterCreatedObjectUndo(newFrame, "Create Photo Preview Frame Object");
					Undo.RecordObject(newFrame, "Update Photo Preview Frame");
				}
				newFrame.transform.SetParent(photoFrame.transform, false);
				framePrefabGameObject = newFrame;
			}
			else {
				framePrefabGameObject = (GameObject)PrefabUtility.InstantiatePrefab(newFrame, photoFrame.transform);
				if(recordUndo) {
					Undo.RegisterCreatedObjectUndo(framePrefabGameObject, "Create Photo Preview Frame Object");
					Undo.RecordObject(framePrefabGameObject, "Update Photo Preview Frame");
				}
			}

			framePrefabGameObject.hideFlags = hideFlagsFrame;
			if(recordUndo) framePrefab = Undo.AddComponent<MarkTypeEditorFramePreview>(framePrefabGameObject);
			else framePrefab = framePrefabGameObject.AddComponent<MarkTypeEditorFramePreview>();
			framePrefab.isGenerated = isGenerated;
			framePrefab.generateSource = generateSource;
			framePrefab.aspectRatio = aspectRatio;
		}

		public void updatePreview(bool recordUndo = false) {
			UpdatePreviewTexture(ref photoFrame, ref previewTexture, ref photoInPreviewTexture);

			photoFrame.getAspectRatios(out float photoAspectRatio, out float frameAspectRatio, out int frameIndex);
			photoFrame.getCropUV(photoAspectRatio, frameAspectRatio, out Vector2 uvMin, out Vector2 uvMax);
			var size = photoFrame.getPhotoWorldSize(frameAspectRatio, frameIndex, out Vector2 frameScale);

			PhotoFrame.SetupMesh(meshFilter.sharedMesh, size, uvMin, uvMax, false, photoFrame.frameType?.photoOffset ?? Vector3.zero, photoFrame.frameType?.photoRotation ?? Vector3.zero);

			meshRenderer.sharedMaterial = photoFrame.photoMaterial;
			textureOverride.Clear();
			textureOverride.SetTexture(photoFrame.photoMaterialTextureSlot, previewTexture);
			meshRenderer.SetPropertyBlock(textureOverride);

			bool isGenerated = false;
			GameObject frameSelected = photoFrame.frameType?.getOrGenerateFrame(frameIndex, frameAspectRatio, out isGenerated, size, false);
			if((frameSelected == null) != (framePrefab == null)
			|| (framePrefab && (
				framePrefab.isGenerated != isGenerated
				|| framePrefab.generateSource != frameSelected
				|| framePrefab.aspectRatio != frameAspectRatio
				|| (!isGenerated && frameSelected != getActiveFrameSource())
			))) {
				frameSelected = photoFrame.frameType?.getOrGenerateFrame(frameIndex, frameAspectRatio, out isGenerated, size);

				deleteFrame(recordUndo);
				if(frameSelected) setFrame(recordUndo, frameSelected, isGenerated, frameSelected, frameAspectRatio);
			}

			if(frameSelected) {
				Vector3 scale = frameSelected.transform.localRotation * new Vector3(frameScale.x, frameScale.y, 1);
				scale.Set(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
				Vector3 frameSourceScale = framePrefab.isGenerated ? Vector3.one : frameSelected.transform.localScale;
				Vector3 finalFrameScale = Vector3.Scale(frameSourceScale, scale);
				if(framePrefab.transform.localScale != finalFrameScale) {
					if(recordUndo) Undo.RecordObject(framePrefab.transform, "Update Photo Preview Frame Scale");
					framePrefab.transform.localScale = finalFrameScale;
				}
			}
		}

		public void OnDestroy() {
			Undo.undoRedoPerformed -= OnUndoRedoEvent;
			if(previewTexture != null) previewTexture.Release();

			EditorApplication.delayCall += () => { //workaround for no errors during scene/prefab unloading
				if(meshFilter) GameObject.DestroyImmediate(meshFilter);
				if(meshRenderer) GameObject.DestroyImmediate(meshRenderer);
				if(framePrefab) GameObject.DestroyImmediate(framePrefab.gameObject);
			};
		}
	}
}
#endif