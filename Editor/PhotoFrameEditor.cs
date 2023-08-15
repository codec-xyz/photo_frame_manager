using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace codec.PhotoFrame {
	[CustomEditor(typeof(PhotoFrame))]
	[CanEditMultipleObjects]
	public class PhotoFrameEditor : Editor {
		SerializedProperty frameType;
		SerializedProperty frameSize;
		SerializedProperty noFrameAspectRatio;
		SerializedProperty photo;
		SerializedProperty autoSelectFrameSize;
		SerializedProperty cropScalePercent;
		SerializedProperty cropOffset;
		SerializedProperty isAbsolsuteRes;
		SerializedProperty resolutionScale;
		SerializedProperty resolutionMaxMajorSize;

		Texture2D cropOverlayTexture;

		bool isScaleSliderBeingUsed = false;
		float scaleSliderMax = 1;

		private Texture2D MakeTex(int width, int height, Color col) {
			Color[] pix = new Color[width * height];
			for(int i = 0; i < pix.Length; ++i) {
				pix[i] = col;
			}
			Texture2D result = new Texture2D(width, height);
			result.SetPixels(pix);
			result.Apply();
			return result;
		}

		void OnEnable() {
			frameType = serializedObject.FindProperty("frameType");
			frameSize = serializedObject.FindProperty("frameSize");
			noFrameAspectRatio = serializedObject.FindProperty("noFrameAspectRatio");
			photo = serializedObject.FindProperty("photo");
			autoSelectFrameSize = serializedObject.FindProperty("autoSelectFrameSize");
			cropScalePercent = serializedObject.FindProperty("cropScalePercent");
			cropOffset = serializedObject.FindProperty("cropOffset");
			isAbsolsuteRes = serializedObject.FindProperty("isAbsolsuteRes");
			resolutionScale = serializedObject.FindProperty("resolutionScale");
			resolutionMaxMajorSize = serializedObject.FindProperty("resolutionMaxMajorSize");
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

			GUI.DrawTexture(rect, texture);

			if(cropOverlayTexture == null) cropOverlayTexture = MakeTex(1, 1, new Color(0.5f, 0.5f, 0.5f, 0.85f));

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

		private string[] getFrameSizeArray() {
			if(frameType.objectReferenceValue == null) return new string[] { "--" };
			PhotoFrameType group = (PhotoFrameType)frameType.objectReferenceValue;

			string[] frameSizeArray = new string[group.aspectRatios.Length];
			for(int i = 0; i < group.aspectRatios.Length; i++) {
				double ratio = 1;
				if(group.aspectRatios[i].x != 0 && group.aspectRatios[i].y != 0) ratio = (double)group.aspectRatios[i].x / (double)group.aspectRatios[i].y;
				frameSizeArray[i] = group.photoFrames[i].name + " -- (" + new Fraction(ratio).ToString().Replace("/", " \u2215 ") + " = " + ratio.ToString("0.000") + ")";
			}

			return frameSizeArray;
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();

			bool livePreview = EditorPrefs.GetBool(ManagerWindow.livePreviewEditorPref, true);

			if(!livePreview) EditorGUILayout.HelpBox("To enable live preview toggle: Photo Frames > Live Preview", MessageType.Info);

			bool anyLocked = false, doUnlock = false;
			foreach(PhotoFrame pf in targets) {
				if(pf.savedData) anyLocked = true;
			}

			if(anyLocked) {
				if(GUILayout.Button("Unlock")) doUnlock = true;

				GUI.enabled = false;
			}

			EditorGUILayout.PropertyField(frameType);
			EditorGUILayout.PropertyField(photo);
			EditorGUILayout.PropertyField(autoSelectFrameSize);
			if(!autoSelectFrameSize.boolValue) {
				bool allNoFrame = true;
				foreach(PhotoFrame pf in targets) {
					if(pf.frameType?.aspectRatios != null && pf.frameType?.aspectRatios.Length > 0) allNoFrame = false;
				}

				if(frameType.objectReferenceValue != null) frameSize.intValue = EditorGUILayout.Popup("Frame Size", frameSize.intValue, getFrameSizeArray());
				if(allNoFrame) EditorGUILayout.PropertyField(noFrameAspectRatio, new GUIContent("Aspect Ratio"));
			}
			EditorGUILayout.Slider(cropScalePercent, 0.0001f, 1);
			Vector2 cropOffsetVec = cropOffset.vector2Value;
			cropOffsetVec.x = EditorGUILayout.Slider("Crop Offset X", cropOffsetVec.x, -1, 1);
			cropOffsetVec.y = EditorGUILayout.Slider("Crop Offset Y", cropOffsetVec.y, -1, 1);
			EditorGUI.Slider(new Rect(), 0, 0, 0);
			if(GUILayout.Button("Reset Crop")) {
				cropScalePercent.floatValue = 1;
				cropOffsetVec.Set(0, 0);
				GUI.FocusControl("");
			}
			cropOffset.vector2Value = cropOffsetVec;

			EditorGUILayout.PropertyField(isAbsolsuteRes, new GUIContent("Use Absolute Resolution"));
			if(isAbsolsuteRes.boolValue) {
				var photoTexture = (Texture)photo.objectReferenceValue;
				int maxSize = 8192;
				if(photoTexture != null) maxSize = Math.Max(photoTexture.width, photoTexture.height);

				CustomEditorGUILayout.IntSliderAllOptions(resolutionMaxMajorSize, 1, maxSize, 0, 8192);

				Color initialValue = GUI.backgroundColor;
				Color tinted = new Color(0.55f, 1.25f, 1.85f, 1);//new Color(0.62f, 1.34f, 2, 1);
				int size = resolutionMaxMajorSize.intValue;

				GUILayout.BeginHorizontal();
				GUI.backgroundColor = size <= 341 ? tinted : initialValue;
				if(GUILayout.Button("SD (144p)")) { resolutionMaxMajorSize.intValue = 256; GUI.FocusControl(""); }
				GUI.backgroundColor = 341 < size && size <= 533 ? tinted : initialValue;
				if(GUILayout.Button("SD (240p)")) { resolutionMaxMajorSize.intValue = 426; GUI.FocusControl(""); }
				GUI.backgroundColor = 533 < size && size <= 747 ? tinted : initialValue;
				if(GUILayout.Button("SD (360p)")) { resolutionMaxMajorSize.intValue = 640; GUI.FocusControl(""); }
				GUI.backgroundColor = 747 < size && size <= 1067 ? tinted : initialValue;
				if(GUILayout.Button("SD (480p)")) { resolutionMaxMajorSize.intValue = 854; GUI.FocusControl(""); }
				GUI.backgroundColor = 1067 < size && size <= 1600 ? tinted : initialValue;
				if(GUILayout.Button("HD (720p)")) { resolutionMaxMajorSize.intValue = 1280; GUI.FocusControl(""); }
				GUI.backgroundColor = 1600 < size && size <= 2240 ? tinted : initialValue;
				if(GUILayout.Button("FULL HD")) { resolutionMaxMajorSize.intValue = 1920; GUI.FocusControl(""); }
				GUI.backgroundColor = 2240 < size && size <= 3200 ? tinted : initialValue;
				if(GUILayout.Button("2k")) { resolutionMaxMajorSize.intValue = 2560; GUI.FocusControl(""); }
				GUI.backgroundColor = 3200 < size && size <= 5760 ? tinted : initialValue;
				if(GUILayout.Button("4k")) { resolutionMaxMajorSize.intValue = 3840; GUI.FocusControl(""); }
				GUI.backgroundColor = 5760 < size ? tinted : initialValue;
				if(GUILayout.Button("8k")) { resolutionMaxMajorSize.intValue = 7680; GUI.FocusControl(""); }
				GUI.backgroundColor = initialValue;
				GUILayout.EndHorizontal();
			}
			else {
				EditorGUILayout.Slider(resolutionScale, 0.0001f, 1);
				GUILayout.BeginHorizontal();
				if(GUILayout.Button("1/12")) { resolutionScale.floatValue = 1.0f / 12.0f; GUI.FocusControl(""); }
				if(GUILayout.Button("1/8")) { resolutionScale.floatValue = 1.0f / 8.0f; GUI.FocusControl(""); }
				if(GUILayout.Button("1/4")) { resolutionScale.floatValue = 1.0f / 4.0f; GUI.FocusControl(""); }
				if(GUILayout.Button("1/3")) { resolutionScale.floatValue = 1.0f / 3.0f; GUI.FocusControl(""); }
				if(GUILayout.Button("1/2")) { resolutionScale.floatValue = 1.0f / 2.0f; GUI.FocusControl(""); }
				if(GUILayout.Button("1")) { resolutionScale.floatValue = 3; GUI.FocusControl(""); }
				GUILayout.EndHorizontal();
			}

			GUI.enabled = true;

			var photoFrame = (PhotoFrame)target;
			if(livePreview && targets.Length == 1 && photoFrame.photo != null) {
				photoFrame.updateResizeTexture();

				photoFrame.getAspectRatios(out float photoAspectRatio, out float frameAspectRatio);
				float cutoutPercent = 1.0f - Mathf.Min(frameAspectRatio, photoAspectRatio) / Mathf.Max(frameAspectRatio, photoAspectRatio);
				var finalRes = photoFrame.getFinalResolution();
				float dataSave = (float)(finalRes.x * finalRes.y) / (float)(photoFrame.photo.width * photoFrame.photo.height);

				GUILayout.Label($"{photoFrame.photo.width}px x {photoFrame.photo.height}px - {photoFrame.photo.name}");
				GUILayout.Label($"{finalRes.x}px x {finalRes.y}px final resolution - " + (dataSave * 100f).ToString("0.00") + "% of original - " + (cutoutPercent * 100f).ToString("0.00") + "% cutout");
				if(photoFrame.frameType != null && photoFrame.frameType.photoFrames.Length != 0) {
					string frameInfo = new Fraction(frameAspectRatio).ToString().Replace("/", " \u2215 ") + " = " + frameAspectRatio.ToString("0.000");
					GUILayout.Label($"Frame - {frameInfo}");
				}

				float width = Screen.width;
				float height = Mathf.Min(width / photoAspectRatio, photoFrame.photo.height, width, 500);
				Rect rect = GUILayoutUtility.GetRect(width, height);

				photoFrame.getCropUV(photoAspectRatio, frameAspectRatio, out Vector2 uvMin, out Vector2 uvMax);
				DrawImagePreview(rect, photoFrame.resizeTexture, photoAspectRatio, uvMin, uvMax);
			}

			bool changes = serializedObject.ApplyModifiedProperties();
			if(changes) {
				foreach(PhotoFrame target in targets) target.updateEditorPreview();
			}

			EditorGUI.BeginChangeCheck();
			if(Event.current.type == EventType.MouseUp) isScaleSliderBeingUsed = false;
			var isMouseDown = Event.current.type == EventType.MouseDown;

			float scaleValue = targets.Cast<PhotoFrame>().Select(pf =>
				pf.transform.localScale.x
				+ pf.transform.localScale.y
				+ pf.transform.localScale.z).Sum() / (targets.Length * 3);
			if(!isScaleSliderBeingUsed) scaleSliderMax = Mathf.Pow(10, Mathf.Ceil(Mathf.Log10(Mathf.Abs(scaleValue) + 0.0001f) + 0.0001f));

			float newScale = CustomEditorGUILayout.SliderAllOptions("Object Scale", scaleValue, 0, scaleSliderMax, float.MinValue, float.MaxValue, 3.321928f);
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
					pf.snap(pf.transform.rotation * Vector3.back);
				}
			}

			if(GUILayout.Button("Snap Down")) {
				foreach(PhotoFrame pf in targets) {
					Undo.RecordObject(pf.transform, "Snap Down");
					pf.snap(Vector3.down);
				}
			}

			if(doUnlock) {
				foreach(PhotoFrame pf in targets) {
					pf.unlock();
					pf.updateEditorPreview();
				}
			}
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

			////The lower fraction is 0/1
			//int lower_n = 0;
			//int lower_d = 1;
			////The upper fraction is 1/1
			//int upper_n = 1;
			//int upper_d = 1;

			//while(true) {
			//	//The middle fraction is (lower_n + upper_n) / (lower_d + upper_d)
			//	int middle_n = lower_n + upper_n;
			//	int middle_d = lower_d + upper_d;
			//	if(middle_d * (value + maxError) < middle_n) { //If x + error < middle
			//		//middle is our new upper
			//		upper_n = middle_n;
			//		upper_d = middle_d;
			//	}
			//	else if(middle_n < (value - maxError) * middle_d) { //Else If middle < x - error
			//		//middle is our new lower
			//		lower_n = middle_n;
			//		lower_d = middle_d;
			//	}
			//	//Else middle is our best fraction
			//	else {
			//		n = baseN * middle_d + middle_n;
			//		d = middle_d;
			//		return;
			//	}
			//}
		}

		public override string ToString() {
			return $"{n}/{d}";
		}
	}
}