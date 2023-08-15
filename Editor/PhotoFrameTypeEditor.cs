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
		private SerializedProperty isSmallerDimensionAlwaysOne;
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
				Debug.Log(new string(chars.ToArray()));
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
			isSmallerDimensionAlwaysOne = serializedObject.FindProperty("isSmallerDimensionAlwaysOne");
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
			serializedObject.Update();
			EditorGUILayout.PropertyField(material, new GUIContent("Photo Material"));
			EditorGUILayout.PropertyField(textureSlot);
			EditorGUILayout.PropertyField(isSmallerDimensionAlwaysOne);
			aspectRatios.arraySize = photoFrames.arraySize;
			list.DoLayoutList();
			if(removeIndex != -1) {
				doRemoveIndex(removeIndex);
				removeIndex = -1;
			}

			GUIStyle boxStyle = new GUIStyle(GUI.skin.textField);
			boxStyle.alignment = TextAnchor.MiddleCenter;
			boxStyle.fontStyle = FontStyle.Italic;
			boxStyle.fontSize = 12;

			Rect myRect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight * 4, GUILayout.ExpandWidth(true));
			GUI.Box(myRect, "Drag and Drop GameObjects Here\n(Prefabs/FBXs/etc.)", boxStyle);
			if(myRect.Contains(Event.current.mousePosition)) {
				if(Event.current.type == EventType.DragUpdated) {
					bool goodToUse = DragAndDrop.objectReferences.Length > 0;
					foreach(var obj in DragAndDrop.objectReferences) {
						if(!(obj is GameObject)) goodToUse = false;
					}

					if(goodToUse) {
						DragAndDrop.visualMode = DragAndDropVisualMode.Link;
						Event.current.Use();
					}
				}
				else if(Event.current.type == EventType.DragPerform) {
					bool goodToUse = DragAndDrop.objectReferences.Length > 0;
					foreach(var obj in DragAndDrop.objectReferences) {
						if(!(obj is GameObject)) goodToUse = false;
					}

					if(goodToUse) {
						photoFrames.arraySize += DragAndDrop.objectReferences.Length;
						aspectRatios.arraySize += DragAndDrop.objectReferences.Length;
						for(int i = 0; i < DragAndDrop.objectReferences.Length; i++) {
							int arrayIndex = photoFrames.arraySize - DragAndDrop.objectReferences.Length + i;
							Object obj = DragAndDrop.objectReferences[i];
							photoFrames.GetArrayElementAtIndex(arrayIndex).objectReferenceValue = obj;
							string fileName = AssetDatabase.GetAssetPath(obj).Split('/').Last();
							aspectRatios.GetArrayElementAtIndex(arrayIndex).vector2Value = ParseForAspectRatios(fileName);
						}
					}
				}
			}

			bool changes = serializedObject.ApplyModifiedProperties();
			if(changes) {
				Scene activeScene = SceneManager.GetActiveScene();
				var photoFrames = Resources.FindObjectsOfTypeAll<PhotoFrame>().Where(pf => pf.gameObject.scene == activeScene);
				foreach(var photoFrame in photoFrames) photoFrame.updateEditorPreview();
			}
		}

		public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height) {
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