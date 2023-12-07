﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

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

		public override void OnInspectorGUI() {
			CustomEditorGUI.lockValue = false;
			serializedObject.Update();
			UtilsGUI.AlignedPropertyField(new GUIContent("Photo Material"), material);
			UtilsGUI.AlignedPropertyField(new GUIContent("Texture Slot", "Slot to use on the photo material. If left empty _MainTex is used. Ignored if no photo material is set"), textureSlot);
			UtilsGUI.AlignedPropertyField(new GUIContent("Photo Offset"), photoOffset);
			UtilsGUI.AlignedPropertyField(new GUIContent("Photo Rotation"), photoRotation);
			UtilsGUI.AlignedPropertyField(new GUIContent("Photo Dimensions"), photoDimensions);

			bool isGenerateFrame = (FrameMatching)frameMatching.enumValueIndex == FrameMatching.GenerateFrame;
			UtilsGUI.AlignedField(new GUIContent("Frame Matching", "Options to modify frames to match aspect ratio\n- None\n- Scale To Photo - Scales the closest frame to any aspect ratio\n- Generate Frame - Edits the closest frame mesh to match any aspect ratio without streaching the frame"), frameMatching, (rect, labelA) => {
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

			if(isGenerateFrame) {
				EditorGUI.indentLevel++;
				UtilsGUI.AlignedPropertyField(new GUIContent("Offset Frame UVs", "Offset each uv the same as its vertex when editing frame meshes"), offsetUvs);
				UtilsGUI.AlignedPropertyField(new GUIContent("UV Orientation Threshold", "Threshold used to filter out unaligned edges for finding uv island orientation"), uvOrientationThreshold);
				UtilsGUI.AlignedPropertyField(new GUIContent("Limit Aspect Ratios To List"), limitAspectRatiosToList);
				EditorGUI.indentLevel--;
			}

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