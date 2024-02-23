using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace codec.PhotoFrame {
	[CustomEditor(typeof(PhotoFrameType))]
	[CanEditMultipleObjects]
	public class PhotoFrameTypeEditor : Editor {
		private SerializedProperty material;
		private SerializedProperty textureSlot;
		private SerializedProperty photoOffset;
		private SerializedProperty photoRotation;
		private SerializedProperty photoDimensions;
		private SerializedProperty frameMatching;
		private SerializedProperty offsetUvs;
		private SerializedProperty uvOrientationThreshold;
		private SerializedProperty limitAspectRatiosToList;
		private SerializedProperty photoFrames;
		private SerializedProperty aspectRatios;
		private ReorderableList list;
		private int removeIndex = -1;

		public static Vector2 ParseForAspectRatios(string str) {
			var foundChars = new List<List<char>>();
			bool seperate = true, foundDot = false;
			foreach(var character in str) {
				if(char.IsDigit(character) || (character == '.' && !foundDot)) {
					if(character == '.') foundDot = true;
					if(seperate) foundChars.Add(new List<char>());
					foundChars.Last().Add(character);
					seperate = false;
				}
				else {
					seperate = true;
					foundDot = false;
				}
			}

			var foundFloats = foundChars.Select(chars => {
				bool isGood = float.TryParse(new string(chars.ToArray()), out float num);
				return isGood ? num : float.NaN;
			}).Where(num => !float.IsNaN(num)).Take(2).ToArray();

			float num1 = 1, num2 = 1;
			if(foundFloats.Length >= 1) num1 = foundFloats[0];
			if(foundFloats.Length >= 2) num2 = foundFloats[1];

			return new Vector2(num1, num2);
		}

		private void doRemoveIndex(int i) {
			SerializedProperty photoFrame = photoFrames.GetArrayElementAtIndex(i);
			if(photoFrame.objectReferenceValue != null) photoFrames.DeleteArrayElementAtIndex(i);
			photoFrames.DeleteArrayElementAtIndex(i);
			aspectRatios.DeleteArrayElementAtIndex(i);
		}

		void OnEnable() {
			material = serializedObject.FindProperty("material");
			textureSlot = serializedObject.FindProperty("textureSlot");
			photoOffset = serializedObject.FindProperty("photoOffset");
			photoRotation = serializedObject.FindProperty("photoRotation");
			photoDimensions = serializedObject.FindProperty("photoDimensions");
			frameMatching = serializedObject.FindProperty("frameMatching");
			offsetUvs = serializedObject.FindProperty("offsetUvs");
			uvOrientationThreshold = serializedObject.FindProperty("uvOrientationThreshold");
			limitAspectRatiosToList = serializedObject.FindProperty("limitAspectRatiosToList");
			photoFrames = serializedObject.FindProperty("photoFrames");
			aspectRatios = serializedObject.FindProperty("aspectRatios");

			list = new ReorderableList(serializedObject, photoFrames, true, true, true, true);

			list.drawHeaderCallback = rect => {
				float margin = EditorGUIUtility.singleLineHeight * 0.125f;
				rect.width /= 2;
				rect.width -= margin * 0.5f;
				EditorGUI.LabelField(rect, "Photo Frame");
				rect.x += rect.width + margin;
				EditorGUI.LabelField(rect, "Aspect Ratio");
			};

			list.drawElementCallback = (rect, index, active, focused) => {
				SerializedProperty photoFrame = photoFrames.GetArrayElementAtIndex(index);
				SerializedProperty aspectRatio = aspectRatios.GetArrayElementAtIndex(index);

				float margin = EditorGUIUtility.singleLineHeight * 0.125f;
				rect.y += EditorGUIUtility.singleLineHeight * 0.125f;
				rect.height = EditorGUIUtility.singleLineHeight;
				float buttonWidth = EditorGUIUtility.singleLineHeight * 2f;

				rect.width -= 2 * margin + buttonWidth;
				rect.width /= 2;
				EditorGUI.PropertyField(rect, photoFrame, GUIContent.none);
				rect.x += margin + rect.width;
				EditorGUI.PropertyField(rect, aspectRatio, GUIContent.none);
				rect.x += margin + rect.width;
				rect.width = buttonWidth;
				if(GUI.Button(rect, "✕")) removeIndex = index;
			};

			list.elementHeightCallback = (index) => EditorGUIUtility.singleLineHeight * 1.25f;

			list.onAddCallback = (ReorderableList l) => {
				photoFrames.arraySize++;
				aspectRatios.arraySize++;
			};

			list.onRemoveCallback = (ReorderableList l) => {
				doRemoveIndex(l.index);
			};

			list.onReorderCallbackWithDetails = (ReorderableList list, int oldIndex, int newIndex) => {
				aspectRatios.MoveArrayElement(oldIndex, newIndex);
			};
		}

		public class MaterialSlotPopup : PopupWindowContent {
			int extraOptions = -1;
			string[] optionValues = new string[0];
			string[] optionDisplayNames = new string[0];

			private SerializedObject obj;
			private SerializedProperty material;
			private SerializedProperty textureSlot;

			public MaterialSlotPopup(SerializedObject obj, SerializedProperty material, SerializedProperty textureSlot) {
				this.obj = obj;
				this.material = material;
				this.textureSlot = textureSlot;
			}

			public override Vector2 GetWindowSize() {
				return new Vector2(250, 2 + (EditorGUIUtility.singleLineHeight + 2) * Mathf.Max(1, optionValues.Length + (extraOptions == -1 ? 0 : 0.5f)));
			}

			public override void OnGUI(Rect rect) {
				if(optionValues.Length == 0) {
					GUILayout.Label("Material has no texture slots");
					return;
				}

				obj.Update();
				Color initialValue = GUI.backgroundColor;

				Dictionary<string, int> values = new Dictionary<string, int>();
				foreach(PhotoFrameType pFT in obj.targetObjects) {
					foreach(string textureSlot in new HashSet<string>(pFT.textureSlot.Split(',').Select(v => v.Trim()).Where(v => v != ""))) {
						values[textureSlot] = values.GetValueOrDefault(textureSlot, 0) + 1;
					}
				}

				bool changes = false;

				for(int i = 0; i < optionValues.Length; i++) {
					if(extraOptions == i) EditorGUILayout.Space(EditorGUIUtility.singleLineHeight * 0.5f);

					EditorGUI.BeginChangeCheck();
					EditorGUI.showMixedValue = 0 < values.GetValueOrDefault(optionValues[i]) && values.GetValueOrDefault(optionValues[i]) < obj.targetObjects.Length;
					bool isOn = EditorGUILayout.ToggleLeft(new GUIContent(optionDisplayNames[i], optionValues[i]), values.GetValueOrDefault(optionValues[i], 0) != 0);
					EditorGUI.showMixedValue = false;
					if(EditorGUI.EndChangeCheck()) {
						foreach(PhotoFrameType pFT in obj.targetObjects) {
							var set = new HashSet<string>(pFT.textureSlot.Split(',').Select(v => v.Trim()).Where(v => v != ""));
							if(isOn) set.Add(optionValues[i]);
							else set.Remove(optionValues[i]);
							var list = set.ToList();
							list.Sort();
							pFT.textureSlot = String.Join(",", list);
							string action = isOn ? "added" : "removed";
							Undo.RecordObject(pFT, $"PhotoFrameType.textureSlot {action} {optionValues[i]}");
							EditorUtility.SetDirty(pFT);
							changes = true;
						}
					}
				}

				if(changes) PhotoFrame.UpdateAll(true);

				if(Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) {
					Event.current.Use();
					GUI.FocusControl("");
					editorWindow.Close();
				}
			}

			public override void OnOpen() {
				Material singleMaterial = (Material)(material.objectReferenceValue);
				if(singleMaterial == null) {
					extraOptions = -1;
					optionValues = new string[0];
					optionDisplayNames = new string[0];
					return;
				}

				List<string> list_optionValues = singleMaterial.GetTexturePropertyNames().ToList();
				List<string> list_optionDisplayNames = singleMaterial.GetTexturePropertyNames().Select(a => ObjectNames.NicifyVariableName(a)).ToList();
				string strValue = textureSlot.stringValue;
				HashSet<string> values = new HashSet<string>(strValue.Split(',').Select(v => v.Trim()).Where(v => v != ""));
				extraOptions = -1;

				foreach(string value in values) {
					if(!list_optionValues.Contains(value)) {
						if(extraOptions == -1) extraOptions = list_optionValues.Count();
						list_optionValues.Add(value);
						list_optionDisplayNames.Add("(Invalid) " + ObjectNames.NicifyVariableName(value));
					}
				}

				if(extraOptions == 0) extraOptions = -1;

				optionValues = list_optionValues.ToArray();
				optionDisplayNames = list_optionDisplayNames.ToArray();
			}
		}

		public override void OnInspectorGUI() {
			CustomEditorGUI.lockValue = false;
			serializedObject.Update();
			EditorGUI.BeginChangeCheck();
			EditorGUI.showMixedValue = material.hasMultipleDifferentValues;
			Material newMaterial = (Material)UtilsGUI.AlignedObjectField(new GUIContent("Photo Material"), material.objectReferenceValue, typeof(Material));
			EditorGUI.showMixedValue = false;
			bool materialUpdate = false;
			if(EditorGUI.EndChangeCheck()) {
				material.objectReferenceValue = newMaterial;
				materialUpdate = true;
			}

			if(material.objectReferenceValue != null || material.hasMultipleDifferentValues)
			{
				var label = new GUIContent("Texture Slot", "List of slots to use on the photo's material (In preview mode some textures like normal textures do not get enabled by Unity  unless that texture is set in the source material. This does not effect baked photos)");
				string value = "None";
				if(textureSlot.hasMultipleDifferentValues) value = "Mixed...";
				else if(textureSlot.stringValue != "") value = String.Join(", ", new HashSet<string>(textureSlot.stringValue.Split(',').Select(v => v.Trim()).Where(v => v != "")).Select(v => ObjectNames.NicifyVariableName(v)));

				if(material.hasMultipleDifferentValues) GUI.enabled = false;
				if(UtilsGUI.AlignedCustomDropdown(label, new GUIContent(value), FocusType.Keyboard, out Rect rect)) {
					UnityEditor.PopupWindow.Show(rect, new MaterialSlotPopup(serializedObject, material, textureSlot));
				}
				GUI.enabled = true;
			}

			UtilsGUI.AlignedPropertyField(new GUIContent("Photo Offset"), photoOffset);
			UtilsGUI.AlignedPropertyField(new GUIContent("Photo Rotation"), photoRotation);
			UtilsGUI.AlignedPropertyField(new GUIContent("Photo Dimensions", "Setting for sizing the photo to match the frame\n - Larger Side Is One\n - Smaller Side Is One\n - Use Aspect Ratio - uses the Aspect Ratio value in the frame model list as the size"), photoDimensions);

			bool isScaleFrame = (FrameMatching)frameMatching.enumValueIndex == FrameMatching.ScaleToPhoto;
			bool isGenerateFrame = (FrameMatching)frameMatching.enumValueIndex == FrameMatching.GenerateFrame;
			UtilsGUI.AlignedField(new GUIContent("Frame Matching", "Settings for how the frame is resized to match the photo (The closest aspect ratio frame model is picked and modified)\n- None - the frame is not resized. The photo will be cropped to the frame's aspect ratio\n- Scale To Photo - the frame is scaled to the photo's aspect ratio\n- Generate Frame -  algorithm to resize frame meshes to different aspect ratios. Preserves frame border sizes and offsets texture UVs to avoid texture stretching"), frameMatching, (rect, labelA) => {
				Vector2 buttonRect = UtilsGUI.OptionsDropDownSize();
				if(isGenerateFrame) rect.width -= buttonRect.x;
				EditorGUI.PropertyField(rect, frameMatching, labelA, false);
				if(isGenerateFrame) {
					rect.x += rect.width;
					rect.width = buttonRect.x;
					if(UtilsGUI.OptionsDropDown(rect, FocusType.Passive)) {
						GenericMenu menu = new GenericMenu();
						menu.AddItem(new GUIContent("Generate And Save Missing Frames"), false, () => {
							foreach(PhotoFrameType frame in targets) {
								EditorUtility.SetDirty(frame);
								frame.generateAndSaveMissingFrames();
							}
						});
						menu.ShowAsContext();
					}
				}
			});

			EditorGUI.indentLevel++;

			if(isGenerateFrame) {
				UtilsGUI.AlignedPropertyField(new GUIContent("Offset Frame UVs", "If UVs should offset to avoid texture stretching"), offsetUvs);
				UtilsGUI.AlignedPropertyField(new GUIContent("UV Orientation Threshold", "Threshold used to filter aligned edges to determine UV island orientation"), uvOrientationThreshold);
			}

			if(isScaleFrame || isGenerateFrame) {
				UtilsGUI.AlignedPropertyField(new GUIContent("Limit Aspect Ratios To List", "Limit available aspect ratios to the model list. This is lets you define a list of aspect ratio by adding other entries to the model list with no model set. The frame matching functionally only uses the closest aspect ratio with a model set"), limitAspectRatiosToList);
			}

			EditorGUI.indentLevel--;

			GUILayout.Space(EditorGUIUtility.singleLineHeight);

			if(aspectRatios.arraySize != photoFrames.arraySize) aspectRatios.arraySize = photoFrames.arraySize;
			list.DoLayoutList();
			if(removeIndex != -1) {
				doRemoveIndex(removeIndex);
				removeIndex = -1;
			}

			if(targets.Cast<PhotoFrameType>().Any(t => t.hasDuplicates())) {
				EditorGUILayout.HelpBox("Multiple frames have the same aspect ratio", MessageType.Warning);
			}

			GUILayout.Space(EditorGUIUtility.singleLineHeight);

			GUIStyle boxStyle = new GUIStyle(GUI.skin.textField);
			boxStyle.alignment = TextAnchor.MiddleCenter;
			boxStyle.fontStyle = FontStyle.Italic;
			boxStyle.fontSize = 12;

			Rect myRect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight * 4, GUILayout.ExpandWidth(true));
			GUI.Box(myRect, "Drag and Drop GameObjects Here\n(Prefabs/FBXs/etc.)", boxStyle);
			if(UtilsGUI.DragAndDropHandler<GameObject>(myRect, out var drop, o => o is GameObject go && go.scene.name == null, DragAndDropVisualMode.Link)) {
				GameObject[] dropArray = drop.ToArray();
				photoFrames.arraySize += dropArray.Length;
				aspectRatios.arraySize += dropArray.Length;
				for(int i = 0; i < dropArray.Length; i++) {
					int arrayIndex = photoFrames.arraySize - dropArray.Length + i;
					UnityEngine.Object obj = dropArray[i];
					photoFrames.GetArrayElementAtIndex(arrayIndex).objectReferenceValue = obj;
					string fileName = AssetDatabase.GetAssetPath(obj).Split('/').Last();
					aspectRatios.GetArrayElementAtIndex(arrayIndex).vector2Value = ParseForAspectRatios(fileName);
				}
			}

			bool changes = serializedObject.ApplyModifiedProperties();

			if(materialUpdate && newMaterial != null) {
				string[] newMaterialSlots = newMaterial.GetTexturePropertyNames();

				foreach(PhotoFrameType pfT in serializedObject.targetObjects.Cast<PhotoFrameType>()) {
					pfT.textureSlot = String.Join(",", new HashSet<string>(pfT.textureSlot.Split(',').Select(v => v.Trim()).Where(v => v != "")).Where(v => newMaterialSlots.Contains(v)));
					if(pfT.textureSlot == "" && newMaterialSlots.Length > 0) pfT.textureSlot = newMaterialSlots[0];
				}
			}

			if(changes) PhotoFrame.UpdateAll(true);
		}

		public override Texture2D RenderStaticPreview(string assetPath, UnityEngine.Object[] subAssets, int width, int height) {
			PhotoFrameType photoFrameFileGroup = (PhotoFrameType)serializedObject.targetObject;
			if(photoFrameFileGroup == null || photoFrameFileGroup.photoFrames == null || photoFrameFileGroup.photoFrames.Length == 0) return null;

			var gameObject = photoFrameFileGroup.photoFrames.FirstOrDefault(f => f != null);
			if(gameObject == null) return null;
			var editor = UnityEditor.Editor.CreateEditor(gameObject);
			Texture2D texture = editor.RenderStaticPreview(AssetDatabase.GetAssetPath(gameObject), null, width, height);
			EditorWindow.DestroyImmediate(editor);
			return texture;
		}
	}
}