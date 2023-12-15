using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static codec.PhotoFrame.PhotoFrame;
using static codec.PhotoFrame.PhotoFrameEditor;
using static UnityEditorInternal.ReorderableList;

namespace codec.PhotoFrame {
	[CustomEditor(typeof(PhotoFrame))]
	[CanEditMultipleObjects]
	public class PhotoFrameEditor : Editor {
		SerializedProperty frameType;
		SerializedProperty photoGUID;
		SerializedProperty autoSelectAspectRatio;
		SerializedProperty aspectRatio;
		SerializedProperty cropScale;
		SerializedProperty cropOffsetX;
		SerializedProperty cropOffsetY;
		SerializedProperty resolutionType;
		SerializedProperty dontBakePhotoUseSource;
		SerializedProperty resolutionValue;

		Texture2D cropOverlayTexture;

		bool guiCustomChanges = false;

		bool isScaleSliderBeingUsed = false;
		float scaleSliderMax = 1;

		void OnEnable() {
			frameType = serializedObject.FindProperty("frameType");
			photoGUID = serializedObject.FindProperty("photoGUID");
			autoSelectAspectRatio = serializedObject.FindProperty("autoSelectAspectRatio");
			aspectRatio = serializedObject.FindProperty("aspectRatio");
			cropScale = serializedObject.FindProperty("cropScale");
			cropOffsetX = serializedObject.FindProperty("cropOffsetX");
			cropOffsetY = serializedObject.FindProperty("cropOffsetY");
			resolutionType = serializedObject.FindProperty("resolutionType");
			dontBakePhotoUseSource = serializedObject.FindProperty("dontBakePhotoUseSource");
			resolutionValue = serializedObject.FindProperty("resolutionValue");
		}

		private void DrawImagePreview(Rect rect, Texture texture, float aspectRatio, Vector2 uvMin, Vector2 uvMax) {
			rect.x += (rect.width - (rect.height * aspectRatio)) * 0.5f;
			rect.width = rect.height * aspectRatio;
			rect.x += EditorGUIUtility.singleLineHeight;
			rect.y += EditorGUIUtility.singleLineHeight * 0.5f;
			rect.width -= EditorGUIUtility.singleLineHeight * 2.0f;
			rect.height -= EditorGUIUtility.singleLineHeight;

			rect.x = Mathf.Round(rect.x);
			rect.y = Mathf.Round(rect.y);
			rect.width = Mathf.Round(rect.width);
			rect.height = Mathf.Round(rect.height);

			if(texture) GUI.DrawTexture(rect, texture);

			if(cropOverlayTexture == null) cropOverlayTexture = Utils.MakeTexture(1, 1, new Color(0.5f, 0.5f, 0.5f, 0.85f));

			GUIStyle style = new GUIStyle(GUI.skin.box);
			style.normal.background = cropOverlayTexture;
			style.border = new RectOffset(0, 0, 0, 0);

			int cRight = (int)Mathf.Floor(rect.width * uvMin.x);
			int cBottom = (int)Mathf.Floor(rect.height * uvMin.y);
			int cLeft = (int)Mathf.Floor(rect.width * (1.0f - uvMax.x));
			int cTop = (int)Mathf.Floor(rect.height * (1.0f - uvMax.y));

			GUI.Box(new Rect(rect.x, rect.y, cRight, rect.height), "", style);
			GUI.Box(new Rect(rect.x + rect.width - cLeft, rect.y, cLeft, rect.height), "", style);
			GUI.Box(new Rect(rect.x + cRight, rect.y, rect.width - cRight - cLeft, cTop), "", style);
			GUI.Box(new Rect(rect.x + cRight, rect.y + rect.height - cBottom, rect.width - cRight - cLeft, cBottom), "", style);

			if(GUI.Button(rect, "", GUIStyle.none)) {
				GUI.FocusControl("");
			}
		}

		public void PhotoPropertyField(out Texture2D singlePhoto) {
			singlePhoto = Utils.Collapse(targets.Cast<PhotoFrame>().Select(pf => pf.photo), out bool isSame, null);

			EditorGUI.showMixedValue = !isSame;
			EditorGUI.BeginChangeCheck();
			singlePhoto = (Texture2D)UtilsGUI.AlignedObjectField(new GUIContent("Photo"), singlePhoto, typeof(Texture2D), false, photoGUID);
			if(EditorGUI.EndChangeCheck()) photoGUID.stringValue = Utils.GetGUID(singlePhoto);

			bool isStretchWarn = Utils.Collapse(targets.Cast<PhotoFrame>().Select(pf => pf.isPhotoStretchedWarn), out isSame, true);
			if(isStretchWarn) {
				UtilsGUI.BeginHelpBox(UtilsGUI.MultiText(targets.Length, isSame, "Photo is", "Photos are", "Some photos are") + "being stretched during import", MessageType.Info);
				GUILayout.FlexibleSpace();
				if(GUILayout.Button("Fix now")) {
					foreach(PhotoFrame pf in targets) pf.fixPhotoStretch();
				}
				UtilsGUI.EndHelpBox();
			}
		}

		public void AspectRatioDropdown(PhotoFrameType pfType) {
			EditorGUI.showMixedValue = aspectRatio.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			int index = pfType.getIndex(aspectRatio.vector2Value) ?? -1;
			var frameSizeNames = pfType.getFrameSizeNames();
			bool notFoundAspectRaio = (index == -1 && aspectRatio.vector2Value != null);
			if(notFoundAspectRaio) {
				frameSizeNames = frameSizeNames.Prepend($"Missing Aspect Ratio ({aspectRatio.vector2Value.x}, {aspectRatio.vector2Value.y})");
				index++;
			}
			int newIndex = UtilsGUI.AlignedPopup(new GUIContent("Frame Size"), index, frameSizeNames.Select(i => new GUIContent(i)).ToArray(), aspectRatio);
			if(EditorGUI.EndChangeCheck()) {
				if(notFoundAspectRaio && index == 0) aspectRatio.vector2Value = aspectRatio.vector2Value;
				else if(notFoundAspectRaio && index > 0) aspectRatio.vector2Value = pfType.aspectRatios[newIndex - 1];
				else aspectRatio.vector2Value = pfType.aspectRatios[newIndex];
			}
		}

		public void AspectRatioField() {
			bool prefabOverridePreview = isPrefabOverrideComapare && aspectRatio.prefabOverride;
			if(autoSelectAspectRatio.boolValue && !prefabOverridePreview) return;
			if(frameType.hasMultipleDifferentValues) return;
			if(prefabOverridePreview) CustomEditorGUI.lockValue = true;

			PhotoFrameType pfType = frameType.objectReferenceValue as PhotoFrameType;

			if(pfType && pfType.haveFrames() && (pfType.frameMatching == FrameMatching.None
			|| pfType.frameMatching == FrameMatching.ScaleToPhoto
			|| (pfType.frameMatching == FrameMatching.GenerateFrame && pfType.limitAspectRatiosToList))) {
				AspectRatioDropdown(pfType);
			}
			else UtilsGUI.AlignedPropertyField(new GUIContent("Aspect Ratio"), aspectRatio);

			CustomEditorGUI.lockValue = false;
		}

		public void ResolutionControl(Texture2D samePhoto, ResolutionType type) {
			if(type == ResolutionType.AbsoluteMajor || (isPrefabOverrideComapare && resolutionValue.prefabOverride && resolutionValue.floatValue > 1)) {
				if(type != ResolutionType.AbsoluteMajor) CustomEditorGUI.lockValue = true;
				int maxSize = 8192;
				if(samePhoto != null) maxSize = Math.Max(samePhoto.width, samePhoto.height);

				EditorGUI.showMixedValue = resolutionValue.hasMultipleDifferentValues;
				EditorGUI.BeginChangeCheck();
				int newValue = UtilsGUI.ResolutionPicker(new GUIContent("Max Major Size"), (int)resolutionValue.floatValue, maxSize, resolutionValue);
				if(EditorGUI.EndChangeCheck()) resolutionValue.floatValue = newValue;
				CustomEditorGUI.lockValue = false;
			}
			else if(type == ResolutionType.Relative || (isPrefabOverrideComapare && resolutionValue.prefabOverride && resolutionValue.floatValue <= 1)) {
				if(type != ResolutionType.Relative) CustomEditorGUI.lockValue = true;
				UtilsGUI.AlignedSliderAllOptions(new GUIContent("Resolution Value"), resolutionValue, 0.0001f, 1, 0.0001f, 1);
				CustomEditorGUI.lockValue = false;
				bool initialEnabled = GUI.enabled;
				if(type != ResolutionType.Relative) GUI.enabled = false;
				GUILayout.BeginHorizontal();
				if(GUILayout.Button("1/12")) { resolutionValue.floatValue = 1.0f / 12.0f; GUI.FocusControl(""); }
				if(GUILayout.Button("1/8")) { resolutionValue.floatValue = 1.0f / 8.0f; GUI.FocusControl(""); }
				if(GUILayout.Button("1/4")) { resolutionValue.floatValue = 1.0f / 4.0f; GUI.FocusControl(""); }
				if(GUILayout.Button("1/3")) { resolutionValue.floatValue = 1.0f / 3.0f; GUI.FocusControl(""); }
				if(GUILayout.Button("1/2")) { resolutionValue.floatValue = 1.0f / 2.0f; GUI.FocusControl(""); }
				if(GUILayout.Button("1")) { resolutionValue.floatValue = 3; GUI.FocusControl(""); }
				GUILayout.EndHorizontal();
				GUI.enabled = initialEnabled;
			}
		}

		public static bool isPrefabOverrideComapare = false;
		public override void OnInspectorGUI() {
			CustomEditorGUI.lockValue = false;
			bool defaultEnabled = GUI.enabled;

			if(!defaultEnabled) {
				isPrefabOverrideComapare = true;
				EditorApplication.delayCall += () => isPrefabOverrideComapare = false;
			}

			serializedObject.Update();
			guiCustomChanges = false;

			if(!EditorSettings.livePreview) EditorGUILayout.HelpBox("To enable live preview toggle: Photo Frames > Live Preview", MessageType.Info);

			bool locked = Utils.Collapse(targets.Cast<PhotoFrame>().Select(pf => pf.bakedData), out _, true);
			bool doUnlock = false;
			if(locked) {
				if(GUILayout.Button("Unlock")) doUnlock = true;
				GUI.enabled = false;
			}

			UtilsGUI.AlignedPropertyField(new GUIContent("Frame"), frameType);
			PhotoPropertyField(out Texture2D samePhoto);
			EditorGUI.BeginChangeCheck();
			UtilsGUI.AlignedLeftToggle(new GUIContent("Auto Select Aspect Ratio"), autoSelectAspectRatio, true);
			bool aspectRatioRealize = (EditorGUI.EndChangeCheck() && autoSelectAspectRatio.boolValue == false);
			if(!autoSelectAspectRatio.boolValue || (isPrefabOverrideComapare && aspectRatio.prefabOverride)) AspectRatioField();

			UtilsGUI.AlignedSliderAllOptions(new GUIContent("Crop Scale"), cropScale, 0.0001f, 1, 0.0001f, 1);
			UtilsGUI.AlignedSliderAllOptions(new GUIContent("Crop Offset X"), cropOffsetX, -1, 1, -1, 1);
			UtilsGUI.AlignedSliderAllOptions(new GUIContent("Crop Offset Y"), cropOffsetY, -1, 1, -1, 1);
			if(GUILayout.Button("Reset Crop")) {
				cropScale.floatValue = 1;
				cropOffsetX.floatValue = 0;
				cropOffsetY.floatValue = 0;
				GUI.FocusControl("");
			}

			UtilsGUI.AlignedLeftToggle(new GUIContent("Don't Bake Photo Use Source"), dontBakePhotoUseSource, true);
			ResolutionType resBefore = (ResolutionType)resolutionType.enumValueIndex;
			ResolutionType resCurrent = (ResolutionType)resolutionType.enumValueIndex;
			if(dontBakePhotoUseSource.boolValue == false) {
				UtilsGUI.AlignedPropertyField(new GUIContent("Resolution Type"), resolutionType);
				resCurrent = (ResolutionType)resolutionType.enumValueIndex;
				ResolutionControl(samePhoto, resCurrent);
			}

			GUI.enabled = defaultEnabled;

			PhotoFrame[] toRealize = null;
			if(aspectRatioRealize) toRealize = targets.Cast<PhotoFrame>().Where(pf => pf.autoSelectAspectRatio).ToArray();

			bool changes = serializedObject.ApplyModifiedProperties() || guiCustomChanges;

			if(toRealize != null) {
				foreach(PhotoFrame pf in toRealize) {
					float photoAspectRatio = 1;
					if(pf.photo != null) photoAspectRatio = pf.photo.width / (float)pf.photo.height;

					float frameAspectRatio = photoAspectRatio;
					int frameIndex = pf.frameType?.findFrame(photoAspectRatio, true, out frameAspectRatio) ?? -1;
					if(frameIndex != -1 && pf.frameType.getRatio(frameIndex) == frameAspectRatio) pf.aspectRatio = pf.frameType.aspectRatios[frameIndex];
					else pf.aspectRatio = new Fraction(frameAspectRatio);
				}
			}

			if(resCurrent != resBefore && resCurrent == ResolutionType.AbsoluteMajor || resCurrent == ResolutionType.Relative) {
				bool targetIsPixelCount = resCurrent != ResolutionType.Relative;
				foreach(PhotoFrame pf in targets) {
					bool currentIsPixelCount = pf.resolutionValue > 1;
					if(currentIsPixelCount == targetIsPixelCount) continue;
					int majorSize = 1024;
					if(pf.photo) majorSize = Math.Max(pf.photo.width, pf.photo.height);
					Undo.RecordObject(pf, "Resolution Value");
					if(targetIsPixelCount) pf.resolutionValue *= majorSize;
					else pf.resolutionValue /= majorSize;
				}
				changes = true;
			}

			if(defaultEnabled == true && changes && EditorSettings.livePreview) {
				foreach(PhotoFrame target in targets) target.enableAndUpdatePreview();
			}

			var photoFrame = (PhotoFrame)target;
			if(EditorSettings.livePreview && targets.Length == 1 && photoFrame.photo != null) {
				if(!photoFrame.preview) photoFrame.enableAndUpdatePreview();
				Texture previewTexture = photoFrame.preview ? (Texture)photoFrame.preview.previewTexture : photoFrame.photo;

				photoFrame.getAspectRatios(out float photoAspectRatio, out float frameAspectRatio, out int frameIndex);
				float cutoutPercent = 1.0f - Mathf.Min(frameAspectRatio, photoAspectRatio) / Mathf.Max(frameAspectRatio, photoAspectRatio);
				var finalRes = photoFrame.getFinalResolution();
				float dataSave = (float)(finalRes.x * finalRes.y) / (float)(photoFrame.photoSourceSize.x * photoFrame.photoSourceSize.y);

				GUILayout.Label($"{photoFrame.photoSourceSize.x}px x {photoFrame.photoSourceSize.y}px - {photoFrame.photo.name}");
				GUILayout.Label($"{finalRes.x}px x {finalRes.y}px final resolution - " + (dataSave * 100f).ToString("0.00") + "% of original - " + (cutoutPercent * 100f).ToString("0.00") + "% cutout");
				if(photoFrame.frameType != null && photoFrame.frameType.photoFrames.Length != 0) {
					string frameInfo = new Fraction(frameAspectRatio).ToString().Replace("/", " \u2215 ") + " = " + frameAspectRatio.ToString("0.000");
					GUILayout.Label($"Frame - {frameInfo}");
				}

				float width = Screen.width;
				float height = Mathf.Min(width / photoAspectRatio, photoFrame.photo.height, width, 500);
				Rect rect = GUILayoutUtility.GetRect(width, height);

				photoFrame.getCropUV(photoAspectRatio, frameAspectRatio, out Vector2 uvMin, out Vector2 uvMax);
				DrawImagePreview(rect, previewTexture, photoAspectRatio, uvMin, uvMax);
			}

			GUILayout.Space(EditorGUIUtility.singleLineHeight * 2);

			EditorGUI.showMixedValue = false;
			EditorGUI.BeginChangeCheck();
			if(Event.current.type == EventType.MouseUp) isScaleSliderBeingUsed = false;
			var isMouseDown = Event.current.type == EventType.MouseDown;

			float scaleValue = targets.Cast<PhotoFrame>().Select(pf =>
				pf.transform.localScale.x
				+ pf.transform.localScale.y
				+ pf.transform.localScale.z).Sum() / (targets.Length * 3);
			if(!isScaleSliderBeingUsed) scaleSliderMax = Mathf.Pow(10, Mathf.Ceil(Mathf.Log10(Mathf.Abs(scaleValue) + 0.0001f) + 0.0001f));

			float newScale = UtilsGUI.AlignedSliderAllOptions(new GUIContent("Object Scale"), scaleValue, 0, scaleSliderMax, float.MinValue, float.MaxValue, 3.321928f);
			if(isMouseDown && Event.current.type == EventType.Used) isScaleSliderBeingUsed = true;

			if(EditorGUI.EndChangeCheck()) {
				foreach(PhotoFrame pf in targets) {
					Undo.RecordObject(pf.transform, "Scale Helper");
					pf.transform.localScale = new Vector3(newScale, newScale, newScale);
				}
			}

			GUILayout.Space(EditorGUIUtility.singleLineHeight);

			if(GUILayout.Button("Snap To Wall")) {
				foreach(PhotoFrame pf in targets) {
					Undo.RecordObject(pf.transform, "Snap To Wall");
					Utils.SnapGameObject(pf.transform.rotation * Vector3.back, pf.gameObject);
				}
			}

			if(GUILayout.Button("Snap Down")) {
				foreach(PhotoFrame pf in targets) {
					Undo.RecordObject(pf.transform, "Snap Down");
					Utils.SnapGameObject(Vector3.down, pf.gameObject);
				}
			}

			if(doUnlock) {
				foreach(PhotoFrame pf in targets) pf.unlock(true);
			}
		}
	}
}