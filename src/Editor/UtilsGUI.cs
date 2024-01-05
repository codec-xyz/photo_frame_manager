using codec.PhotoFrame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace codec {
	public static class UtilsGUI {
		public static readonly int[] ResolutionSizeRanges = new int[] { 0, 341, 533, 747, 1067, 1600, 2240, 3200, 5760, 8192 };
		public static readonly int[] ResolutionMajorSizes = new int[] { 256, 426, 640, 854, 1280, 1920, 2560, 3840, 7680 };
		public static readonly int[] ResolutionMinorSizes = new int[] { 144, 240, 360, 480, 720, 1080, 1440, 2160, 4320 };
		public static readonly string[] ResolutionSizeNames = new string[] { "SD (144p)", "SD (240p)", "SD (360p)", "SD (480p)", "HD (720p)", "FULL HD", "2k", "4k", "8k" };

		public static int ResolutionPicker(GUIContent label, int value, int softMaxSize, SerializedProperty coreProperty = null) {
			return ResolutionPicker(label, value, softMaxSize, new Color(0.55f, 1.25f, 1.85f, 1), coreProperty);
		}

		public static int ResolutionPicker(GUIContent label, int value, int softMaxSize, Color tintColor, SerializedProperty coreProperty = null) {
			return ResolutionPicker(label, value, softMaxSize, tintColor, ResolutionSizeRanges, ResolutionMajorSizes, ResolutionSizeNames, coreProperty);
		}

		public static int ResolutionPicker(GUIContent label, int value, int softMaxSize, Color tintColor, int[] sizeRanges, int[] sizes, string[] sizeNames, SerializedProperty coreProperty = null) {
			value = AlignedIntSliderAllOptions(label, value, 1, softMaxSize, sizeRanges.First() + 1, sizeRanges.Last(), coreProperty);

			Color initialValue = GUI.backgroundColor;
			bool initialEnabled = GUI.enabled;

			if(CustomEditorGUI.lockValue) GUI.enabled = false;
			GUILayout.BeginScrollView(Vector2.zero, false, false, GUIStyle.none, GUIStyle.none, GUIStyle.none);
			GUILayout.BeginHorizontal();
			for(int i = 0; i < sizes.Length; i++) {
				GUI.backgroundColor = (sizeRanges[i] < value && value <= sizeRanges[i + 1]) ? tintColor : initialValue;
				if(GUILayout.Button(sizeNames[i])) { value = sizes[i]; GUI.FocusControl(""); }
			}
			GUILayout.EndHorizontal();
			GUILayout.EndScrollView();

			GUI.backgroundColor = initialValue;
			GUI.enabled = initialEnabled;

			return value;
		}

		public static Rect DoFieldAlignment(Rect rect, float labelSize) {
			EditorGUIUtility.labelWidth += CustomEditorGUI.indent;
			float offset = EditorGUIUtility.labelWidth - (labelSize + CustomEditorGUI.indent);
			offset = Mathf.Max(offset, -CustomEditorGUI.indent);

			EditorGUIUtility.labelWidth -= offset;
			rect.x += offset;
			rect.width -= offset;
			return rect;
		}

		public static float GetPropertyHeight(GUIContent label, SerializedProperty property) {
			var method_GetHandler = typeof(EditorGUILayout).Assembly.GetType("UnityEditor.ScriptAttributeUtility").GetMethod("GetHandler", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(SerializedProperty) }, null);
			var result_GetHandler = method_GetHandler.Invoke(null, new object[] { property });

			var method_GetHeight = typeof(EditorGUILayout).Assembly.GetType("UnityEditor.PropertyHandler").GetMethod("GetHeight", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(SerializedProperty), typeof(GUIContent), typeof(bool) }, null);
			var result_GetHeight = (float)method_GetHeight.Invoke(result_GetHandler, new object[] { property, label, false });

			return result_GetHeight;
		}

		public delegate void AlignedFieldInner(Rect rect, GUIContent label);
		public static void AlignedField(GUIContent label, SerializedProperty property, AlignedFieldInner inner) {
			AlignedField(label, property, property != null ? GetPropertyHeight(label, property) : EditorGUIUtility.singleLineHeight, inner);
		}
		public static void AlignedField(GUIContent label, SerializedProperty property, float height, AlignedFieldInner inner) {
			float savedLabelWidth = EditorGUIUtility.labelWidth;
			Rect rect = GUILayoutUtility.GetRect(0, height, GUI.skin.horizontalSlider, GUILayout.ExpandWidth(true));
			if(property != null) label = EditorGUI.BeginProperty(rect, label, property);
			if(CustomEditorGUI.alignment == GUILabelAlignment.Right) rect = DoFieldAlignment(rect, GUI.skin.label.CalcSize(label).x);
			inner(rect, label);
			if(property != null) EditorGUI.EndProperty();
			EditorGUIUtility.labelWidth = savedLabelWidth;
		}

		public delegate T AlignedFieldInner<T>(Rect rect, GUIContent label);
		public static T AlignedField<T>(GUIContent label, SerializedProperty property, AlignedFieldInner<T> inner) {
			return AlignedField(label, property, property != null ? GetPropertyHeight(label, property) : EditorGUIUtility.singleLineHeight, inner);
		}
		public static T AlignedField<T>(GUIContent label, SerializedProperty property, float height, AlignedFieldInner<T> inner) {
			float savedLabelWidth = EditorGUIUtility.labelWidth;
			Rect rect = GUILayoutUtility.GetRect(0, height, GUI.skin.horizontalSlider, GUILayout.ExpandWidth(true));
			if(property != null) label = EditorGUI.BeginProperty(rect, label, property);
			if(CustomEditorGUI.alignment == GUILabelAlignment.Right) rect = DoFieldAlignment(rect, GUI.skin.label.CalcSize(label).x);
			T value = inner(rect, label);
			if(property != null) EditorGUI.EndProperty();
			EditorGUIUtility.labelWidth = savedLabelWidth;
			return value;
		}

		public static int AlignedIntPopup(GUIContent label, int value, GUIContent[] displayedOptions, int[] optionValues, SerializedProperty coreProperty = null) => AlignedField(label, coreProperty, (rect, labelA) => CustomEditorGUI.IntPopup(rect, labelA, value, displayedOptions, optionValues));
		public static void AlignedIntPopup(GUIContent label, SerializedProperty property, GUIContent[] displayedOptions, int[] optionValues) => AlignedField(label, property, (rect, labelA) => CustomEditorGUI.IntPopup(rect, labelA, property, displayedOptions, optionValues));

		public static int AlignedPopup(GUIContent label, int value, GUIContent[] displayedOptions, SerializedProperty coreProperty = null) => AlignedField(label, coreProperty, (rect, labelA) => CustomEditorGUI.Popup(rect, labelA, value, displayedOptions));

		public static float AlignedSliderAllOptions(GUIContent label, float value, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1, SerializedProperty coreProperty = null) => AlignedField(label, coreProperty, (rect, labelA) => CustomEditorGUI.SliderAllOptions(rect, labelA, value, leftValue, rightValue, hardMin, hardMax, power));
		public static void AlignedSliderAllOptions(GUIContent label, SerializedProperty property, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1) => AlignedField(label, property, (rect, labelA) => CustomEditorGUI.SliderAllOptions(rect, labelA, property, leftValue, rightValue, hardMin, hardMax, power));

		public static int AlignedIntSliderAllOptions(GUIContent label, int value, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue, SerializedProperty coreProperty = null) => AlignedField(label, coreProperty, (rect, labelA) => CustomEditorGUI.IntSliderAllOptions(rect, labelA, value, leftValue, rightValue, hardMin, hardMax));
		public static void AlignedIntSliderAllOptions(GUIContent label, SerializedProperty property, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue) => AlignedField(label, property, (rect, labelA) => CustomEditorGUI.IntSliderAllOptions(rect, labelA, property, leftValue, rightValue, hardMin, hardMax));

		public static void AlignedMinMaxSlider(GUIContent label, ref float min, ref float max, float minLimit, float maxLimit, bool valueEdit = false) {
			float innerMin = min, innerMax = max;
			AlignedField(label, null, (rect, labelA) => EditorGUI.MinMaxSlider(rect, labelA, ref innerMin, ref innerMax, minLimit, maxLimit));
			if(valueEdit) {
				Rect rect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight, GUI.skin.horizontalSlider, GUILayout.ExpandWidth(true));
				rect.x += EditorGUIUtility.labelWidth + 2;
				rect.width -= EditorGUIUtility.labelWidth + 2;
				rect.width /= 2;
				rect.width -= 2;
				innerMin = EditorGUI.FloatField(rect, innerMin);
				rect.x += rect.width + 2;
				innerMax = EditorGUI.FloatField(rect, innerMax);

				if(innerMin > innerMax) innerMax = innerMin;
			}
			min = innerMin;
			max = innerMax;
		}

		public static void AlignedMinMaxSlider(GUIContent label, SerializedProperty property, float minLimit, float maxLimit, bool valueEdit = false) {
			float innerMin = 0, innerMax = 0;
			if(property.vector2Value != null) (innerMin, innerMax) = (property.vector2Value.x, property.vector2Value.y);
			EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			float savedLabelWidth = EditorGUIUtility.labelWidth;
			AlignedField(label, property, (rect, labelA) => {
				EditorGUI.MinMaxSlider(rect, labelA, ref innerMin, ref innerMax, minLimit, maxLimit);
				if(valueEdit) {
					rect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight, GUI.skin.horizontalSlider, GUILayout.ExpandWidth(true));
					rect.x += savedLabelWidth + 2;
					rect.width -= savedLabelWidth + 2;
					rect.width /= 2;
					rect.width -= 2;
					innerMin = EditorGUI.FloatField(rect, innerMin);
					rect.x += rect.width + 2;
					innerMax = EditorGUI.FloatField(rect, innerMax);

					if(innerMin > innerMax) innerMax = innerMin;
				}
			});
			if(EditorGUI.EndChangeCheck()) {
				property.vector2Value = new Vector2(innerMin, innerMax);
			}
		}

		public static void AlignedPropertyField(GUIContent label, SerializedProperty property, bool includeChildren = false) => AlignedField(label, property, (rect, labelA) => EditorGUI.PropertyField(rect, property, labelA, includeChildren));

		public static bool AlignedToggle(GUIContent label, bool value) => AlignedField(label, null, (rect, labelA) => EditorGUI.Toggle(rect, labelA, value));

		public static bool AlignedLeftToggle(GUIContent label, bool value, bool regularOnLeftAligned = false) {
			if(regularOnLeftAligned && CustomEditorGUI.alignment == GUILabelAlignment.Left) {
				return AlignedToggle(label, value);
			}

			Rect rect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight, GUI.skin.horizontalSlider, GUILayout.ExpandWidth(true));

			if(CustomEditorGUI.alignment == GUILabelAlignment.Right) {
				rect.x += EditorGUIUtility.labelWidth - 17;
				rect.width -= EditorGUIUtility.labelWidth - 17;
			}

			return EditorGUI.ToggleLeft(rect, label, value);
		}

		public static void AlignedLeftToggle(GUIContent label, SerializedProperty property, bool regularOnLeftAligned = false) {
			if(regularOnLeftAligned && CustomEditorGUI.alignment == GUILabelAlignment.Left) {
				AlignedPropertyField(label, property);
				return;
			}

			Rect rect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight, GUI.skin.horizontalSlider, GUILayout.ExpandWidth(true));

			if(CustomEditorGUI.alignment == GUILabelAlignment.Right) {
				rect.x += EditorGUIUtility.labelWidth - 17;
				rect.width -= EditorGUIUtility.labelWidth - 17;
			}

			EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			label = EditorGUI.BeginProperty(rect, label, property);
			bool value = EditorGUI.ToggleLeft(rect, label, property.boolValue);
			EditorGUI.EndProperty();
			if(EditorGUI.EndChangeCheck()) property.boolValue = value;
		}

		public static UnityEngine.Object AlignedObjectField(GUIContent label, UnityEngine.Object obj, Type objType, bool allowSceneObjects = true, SerializedProperty coreProperty = null) => AlignedField(label, coreProperty, (rect, labelA) => EditorGUI.ObjectField(rect, labelA, obj, objType, allowSceneObjects));

		public static void AlignedObjectField(GUIContent label, SerializedProperty property, Type objType, bool allowSceneObjects = true) {
			EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			var obj = AlignedField(label, property, (rect, labelA) => EditorGUI.ObjectField(rect, labelA, property.objectReferenceValue, objType, allowSceneObjects));
			if(EditorGUI.EndChangeCheck()) property.objectReferenceValue = obj;
		}

		public static void AlignedVector2Field(GUIContent label, SerializedProperty property) {
			EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			var value = AlignedField(label, property, (rect, labelA) => CustomEditorGUI.Vector2Field(rect, labelA, property.vector2Value));
			if(EditorGUI.EndChangeCheck()) property.vector2Value = value;
		}

		public static void AlignedMultiSelectPopup(GUIContent label, string value, string[] valueOptions, string[] valueOptionsDisplayName) => AlignedField(label, null, (rect, labelA) => CustomEditorGUI.MultiSelectPopup(rect, labelA, value, valueOptions, valueOptionsDisplayName));

		public static void AlignedMultiSelectPopup(GUIContent label, string value, string[] valueOptions, string[] valueOptionsDisplayName, string invalidPrepend) => AlignedField(label, null, (rect, labelA) => CustomEditorGUI.MultiSelectPopup(rect, labelA, value, valueOptions, valueOptionsDisplayName, invalidPrepend));

		public static void AlignedMultiSelectPopup(GUIContent label, SerializedProperty property, string[] valueOptions, string[] valueOptionsDisplayName) => AlignedField(label, property, (rect, labelA) => CustomEditorGUI.MultiSelectPopup(rect, labelA, property, valueOptions, valueOptionsDisplayName));

		public static void AlignedMultiSelectPopup(GUIContent label, SerializedProperty property, string[] valueOptions, string[] valueOptionsDisplayName, string invalidPrepend) => AlignedField(label, property, (rect, labelA) => CustomEditorGUI.MultiSelectPopup(rect, labelA, property, valueOptions, valueOptionsDisplayName, invalidPrepend));

		public static bool AlignedCustomDropdown(GUIContent label, GUIContent dropdown, FocusType focusType, out Rect popupRect) {
			Rect returnRect = new Rect();
			bool returnValue = AlignedField(label, null, (rect, labelA) => {
				EditorGUI.PrefixLabel(rect, labelA);
				rect.x += EditorGUIUtility.labelWidth + 2;
				rect.width -= EditorGUIUtility.labelWidth + 2;
				returnRect = rect;
				return EditorGUI.DropdownButton(rect, dropdown, focusType);
			});

			popupRect = returnRect;
			return returnValue;
		}

		public static string MultiText(int count, bool isSame, string single, string multi, string multiSome) {
			if(count == 1) return single;
			if(isSame) return multi;
			return multiSome;
		}

		public static GUIStyle BeginHelpBoxInnerLabelSyle;
		public static void BeginHelpBox(string text, MessageType type, bool minWidth = false) {
			var icon = typeof(EditorGUIUtility).GetMethod("GetHelpIcon", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { type });
			var tempContent = (GUIContent)typeof(EditorGUIUtility).GetMethod("TempContent", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(string), typeof(Texture) }, null).Invoke(null, new object[] { text, icon });

			if(BeginHelpBoxInnerLabelSyle == null) {
				BeginHelpBoxInnerLabelSyle = new GUIStyle(EditorStyles.helpBox);
				BeginHelpBoxInnerLabelSyle.name = "";
				BeginHelpBoxInnerLabelSyle.margin = new RectOffset();
				BeginHelpBoxInnerLabelSyle.border = new RectOffset();
			}

			GUILayout.BeginHorizontal(EditorStyles.helpBox);
			GUILayout.Label(tempContent, BeginHelpBoxInnerLabelSyle);
			if(minWidth) GUILayout.FlexibleSpace();
			GUILayout.BeginVertical();
		}

		public static void EndHelpBox() {
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
		}

		public static bool DragAndDropHandler<T>(Rect rect, out IEnumerable<T> targets, Func<UnityEngine.Object, bool> check, DragAndDropVisualMode icon = DragAndDropVisualMode.Generic) {
			targets = null;
			if(!rect.Contains(Event.current.mousePosition)) return false;
			if(Event.current.type != EventType.DragUpdated && Event.current.type != EventType.DragPerform) return false;

			targets = DragAndDrop.objectReferences.Where(check).Cast<T>();
			bool dragFinish = Event.current.type == EventType.DragPerform;

			if(targets.Any()) {
				DragAndDrop.visualMode = icon;
				Event.current.Use();
				return dragFinish;
			}

			return false;
		}

		public static Vector2 OptionsDropDownSize() {
			GUIStyle iconButtonStyle = GUI.skin.FindStyle("IconButton") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("IconButton");
			GUIContent content = new GUIContent(EditorGUIUtility.Load("icons/d__Popup.png") as Texture2D);
			return iconButtonStyle.CalcSize(content);
		}

		public static bool OptionsDropDown(FocusType focusType) {
			GUIStyle iconButtonStyle = GUI.skin.FindStyle("IconButton") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("IconButton");
			GUIContent content = new GUIContent(EditorGUIUtility.Load("icons/d__Popup.png") as Texture2D);
			return EditorGUILayout.DropdownButton(content, focusType, iconButtonStyle);
		}

		public static bool OptionsDropDown(Rect rect, FocusType focusType) {
			GUIStyle iconButtonStyle = GUI.skin.FindStyle("IconButton") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("IconButton");
			GUIContent content = new GUIContent(EditorGUIUtility.Load("icons/d__Popup.png") as Texture2D);
			return EditorGUI.DropdownButton(rect, content, focusType, iconButtonStyle);
		}

		public static void DrawTexture(Rect rect, Texture texture, Vector2 cropMin, Vector2 cropMax, float rotationCounterclockwise) {
			Matrix4x4 matrixBackup = GUI.matrix;
			GUI.BeginGroup(rect);
			if(rotationCounterclockwise != 0) GUIUtility.RotateAroundPivot(rotationCounterclockwise, new Vector2(rect.width / 2, rect.height / 2));
			Rect textureRect = new Rect(cropMin.x * rect.width, cropMin.y * rect.height, rect.width / (cropMax.x - cropMin.x), rect.height / (cropMax.y - cropMin.y));
			GUI.DrawTexture(textureRect, texture);
			GUI.matrix = matrixBackup;
			rect.y += rect.height;
			GUI.EndGroup();
		}

		public static void DrawTextureRotateIncr(Rect rect, Texture texture, Vector2 cropMin, Vector2 cropMax, int rotationCounterclockwise90DegIncrs) {
			Matrix4x4 matrixBackup = GUI.matrix;
			Rect clipRect = new Rect(rect);
			Vector2 clipOffset = Vector2.zero;
			if(rotationCounterclockwise90DegIncrs % 2 == 1) {
				clipOffset = new Vector2(
					(rect.width - rect.height) / 2,
					(rect.height - rect.width) / 2
				);
				clipRect.x += clipOffset.x;
				clipRect.y += clipOffset.y;
				clipRect.width = rect.height;
				clipRect.height = rect.width;
			}
			GUI.BeginGroup(clipRect);
			if(rotationCounterclockwise90DegIncrs != 0) GUIUtility.RotateAroundPivot(90 * rotationCounterclockwise90DegIncrs, new Vector2(clipRect.width / 2, clipRect.height / 2));
			float outWidth = rect.width / (cropMax.x - cropMin.x), outHeight = rect.height / (cropMax.y - cropMin.y);
			Rect textureRect = new Rect(-cropMin.x * outWidth - clipOffset.x, -cropMin.y * outHeight - clipOffset.y, outWidth, outHeight);
			GUI.DrawTexture(textureRect, texture);
			GUI.matrix = matrixBackup;
			rect.y += rect.height;
			GUI.EndGroup();
		}
	}
}