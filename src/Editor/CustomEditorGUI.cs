using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace codec {
	public enum GUILabelAlignment {
		Left, Right
	}

	public static class CustomEditorGUI {
		public static readonly int s_SliderHash = "EditorSlider".GetHashCode();
		public static readonly int s_PopupHash = "EditorPopup".GetHashCode();
		public static readonly int s_FoldoutHash = "Foldout".GetHashCode();
		public static string kFloatFieldFormatString = "g7";

		/*
		 * Similar to GUI.enable but does not disable prefab revert options
		 * Only works on some gui functions
		 */
		public static bool lockValue = false;
		public static GUILabelAlignment alignment {
			get {
				if(PhotoFrame.EditorSettings.rightAlignedFields) return GUILabelAlignment.Right;
				else return GUILabelAlignment.Left;
			}
			set {
				PhotoFrame.EditorSettings.rightAlignedFields = value == GUILabelAlignment.Right;
			}
		}

		public static float indent => (float)typeof(EditorGUI).GetProperty("indent", BindingFlags.NonPublic | BindingFlags.Static).GetGetMethod(true).Invoke(null, new object[] { });

		public static GUIContent TempContent(string text) {
			var method_TempContent = typeof(EditorGUIUtility).GetMethod("TempContent", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string) }, null);
			return (GUIContent)method_TempContent.Invoke(null, new object[] { text });
		}

		public static float SliderAllOptions(Rect position, string label, float value, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1) {
			return SliderAllOptions(position, TempContent(label), value, leftValue, rightValue, hardMin, hardMax, power);
		}

		public static float SliderAllOptions(Rect position, GUIContent label, float value, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1) {
			var result_DragZoneRect = (Rect)typeof(EditorGUIUtility).GetMethod("DragZoneRect", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { position, true });

			var value_horizontalSliderThumbExtent = (GUIStyle)typeof(GUISkin).GetProperty("horizontalSliderThumbExtent", BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod(true).Invoke(GUI.skin, new object[] { });

			var method_DoSlider = typeof(EditorGUI).GetMethod("DoSlider", BindingFlags.NonPublic | BindingFlags.Static, null,
				new[] { typeof(Rect), typeof(Rect), typeof(int), typeof(float), typeof(float), typeof(float), typeof(string), typeof(float), typeof(float), typeof(float), typeof(GUIStyle), typeof(GUIStyle), typeof(GUIStyle), typeof(Texture2D), typeof(GUIStyle) }, null);

			int controlID = GUIUtility.GetControlID(s_SliderHash, FocusType.Keyboard, position);
			if(label != null) position = EditorGUI.PrefixLabel(position, controlID, label);
			bool initialEnabled = GUI.enabled;
			if(lockValue) GUI.enabled = false;
			value = (float)method_DoSlider.Invoke(null, new object[] { position, result_DragZoneRect, controlID, value, leftValue, rightValue, kFloatFieldFormatString, hardMin, hardMax, power, EditorStyles.numberField, GUI.skin.horizontalSlider, GUI.skin.horizontalSliderThumb, null, value_horizontalSliderThumbExtent });
			GUI.enabled = initialEnabled;
			return value;
		}

		public static void SliderAllOptions(Rect position, SerializedProperty property, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1) {
			SliderAllOptions(position, TempContent(property.displayName), property, leftValue, rightValue, hardMin, hardMax, power);
		}

		public static void SliderAllOptions(Rect position, string label, SerializedProperty property, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1) {
			SliderAllOptions(position, TempContent(label), property, leftValue, rightValue, hardMin, hardMax, power);
		}

		public static void SliderAllOptions(Rect position, GUIContent label, SerializedProperty property, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1) {
			EditorGUI.BeginChangeCheck();
			float floatValue = SliderAllOptions(position, label, property.floatValue, leftValue, rightValue, hardMin, hardMax, power);
			if(EditorGUI.EndChangeCheck()) property.floatValue = floatValue;
		}

		public static int IntSliderAllOptions(Rect position, int value, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue) {
			return Mathf.RoundToInt(SliderAllOptions(position, (GUIContent)null, value, leftValue, rightValue, hardMin, hardMax));
		}

		public static int IntSliderAllOptions(Rect position, string label, int value, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue) {
			return Mathf.RoundToInt(SliderAllOptions(position, label, value, leftValue, rightValue, hardMin, hardMax));
		}

		public static int IntSliderAllOptions(Rect position, GUIContent label, int value, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue) {
			return Mathf.RoundToInt(SliderAllOptions(position, label, value, leftValue, rightValue, hardMin, hardMax));
		}

		public static void IntSliderAllOptions(Rect position, SerializedProperty property, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue) {
			IntSliderAllOptions(position, TempContent(property.displayName), property, leftValue, rightValue, hardMin, hardMax);
		}

		public static void IntSliderAllOptions(Rect position, string label, SerializedProperty property, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue) {
			IntSliderAllOptions(position, TempContent(label), property, leftValue, rightValue, hardMin, hardMax);
		}

		public static void IntSliderAllOptions(Rect position, GUIContent label, SerializedProperty property, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue) {
			EditorGUI.BeginChangeCheck();
			int intValue = IntSliderAllOptions(position, label, property.intValue, leftValue, rightValue, hardMin, hardMax);
			if(EditorGUI.EndChangeCheck()) property.intValue = intValue;
		}

		public static int Popup(Rect position, GUIContent label, int selectedIndex, GUIContent[] displayedOptions) {
			var method_DoPopup = typeof(EditorGUI).GetMethod("DoPopup", BindingFlags.NonPublic | BindingFlags.Static, null,
				new[] { typeof(Rect), typeof(int), typeof(int), typeof(GUIContent[]), typeof(Func<int, bool>), typeof(GUIStyle) }, null);

			int controlID = GUIUtility.GetControlID(s_PopupHash, FocusType.Keyboard, position);
			if(label != null) position = EditorGUI.PrefixLabel(position, controlID, label);
			bool initialEnabled = GUI.enabled;
			if(lockValue) GUI.enabled = false;
			selectedIndex = (int)method_DoPopup.Invoke(null, new object[] { position, controlID, selectedIndex, displayedOptions, null, EditorStyles.popup });
			GUI.enabled = initialEnabled;
			return selectedIndex;
		}

		public static int IntPopup(Rect position, GUIContent label, int selectedValue, GUIContent[] displayedOptions, int[] optionValues) {
			int i = Array.IndexOf(optionValues, selectedValue);
			i = Popup(position, label, i, displayedOptions);
			if(i < 0 || i >= optionValues.Length) return selectedValue;
			return optionValues[i];
		}

		public static void IntPopup(Rect position, GUIContent label, SerializedProperty property, GUIContent[] displayedOptions, int[] optionValues) {
			EditorGUI.BeginChangeCheck();
			int intValue = IntPopup(position, label, property.intValue, displayedOptions, optionValues);
			if(EditorGUI.EndChangeCheck()) property.intValue = intValue;
		}

		public static Vector2 Vector2Field(Rect position, GUIContent label, Vector2 value) {
			int controlID = GUIUtility.GetControlID(s_FoldoutHash, FocusType.Keyboard, position);
			position = (Rect)typeof(EditorGUI).GetMethod("MultiFieldPrefixLabel", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { position, controlID, label, 2 });
			position.height = 18f;
			bool initialEnabled = GUI.enabled;
			if(lockValue) GUI.enabled = false;
			value = (Vector2)typeof(EditorGUI).GetMethod("Vector2Field", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Rect), typeof(Vector2) }, null).Invoke(null, new object[] { position, value });
			GUI.enabled = initialEnabled;
			return value;
		}

		public static Vector2 Vector3Field(Rect position, GUIContent label, Vector3 value) {
			int controlID = GUIUtility.GetControlID(s_FoldoutHash, FocusType.Keyboard, position);
			position = (Rect)typeof(EditorGUI).GetMethod("MultiFieldPrefixLabel", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { position, controlID, label, 3 });
			position.height = 18f;
			bool initialEnabled = GUI.enabled;
			if(lockValue) GUI.enabled = false;
			value = (Vector2)typeof(EditorGUI).GetMethod("Vector3Field", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Rect), typeof(Vector3) }, null).Invoke(null, new object[] { position, value });
			GUI.enabled = initialEnabled;
			return value;
		}

		public static string MultiSelectPopup(Rect position, GUIContent label, string value, string[] valueOptions, string[] valueOptionsDisplayName, string invalidPrepend = "(Invalid) ") {
			List<string> optionsList = new List<string>(valueOptions);
			List<string> optionsDisplayNameList = new List<string>(valueOptionsDisplayName);
			IEnumerable<string> values = value.Split(',').Select(v => v.Trim()).Where(v => v != "");
			int mask = 0;
			foreach(string v in values) {
				int index = optionsList.IndexOf(v);
				if(index == -1) {
					optionsList.Add(v);
					optionsDisplayNameList.Add(invalidPrepend + v);
					index = optionsList.Count - 1;
				}

				mask |= 1 << index;
			}

			mask = EditorGUI.MaskField(position, label, mask, optionsDisplayNameList.ToArray());

			value = "";
			for(int i = 0; i < 31 && mask > 0; i++, mask >>= 1) {
				if((mask & 1) != 0) value += optionsList[i] + ",";
			}

			return value;
		}

		public static void MultiSelectPopup(Rect position, GUIContent label, SerializedProperty property, string[] valueOptions, string[] valueOptionsDisplayName, string invalidPrepend = "(Invalid) ") {
			EditorGUI.BeginChangeCheck();
			string stringValue = MultiSelectPopup(position, label, property.stringValue, valueOptions, valueOptionsDisplayName, invalidPrepend);
			if(EditorGUI.EndChangeCheck()) property.stringValue = stringValue;
		}
	}
}