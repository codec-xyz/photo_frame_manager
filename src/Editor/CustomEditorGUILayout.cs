using codec.PhotoFrame;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace codec {
	public static class CustomEditorGUILayout {
		public static Rect handleSliderLayout(bool hasLabel, params GUILayoutOption[] options) {
			if(options == null) options = new GUILayoutOption[] { };

			var method_GetSliderRect = typeof(EditorGUILayout).GetMethod("GetSliderRect", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(bool), typeof(GUILayoutOption[]) }, null);
			var result_GetSliderRect = (Rect)method_GetSliderRect.Invoke(null, new object[] { hasLabel, options });

			var field_TempContent = typeof(EditorGUILayout).GetField("s_LastRect", BindingFlags.NonPublic | BindingFlags.Static);
			field_TempContent.SetValue(null, result_GetSliderRect);

			return result_GetSliderRect;
		}

		public static Rect handleGenericLayout(bool hasLabel, GUIStyle style, params GUILayoutOption[] options) {
			if(options == null) options = new GUILayoutOption[] { };

			var result_GetSliderRect = EditorGUILayout.GetControlRect(hasLabel, 18f, style, options);

			var field_TempContent = typeof(EditorGUILayout).GetField("s_LastRect", BindingFlags.NonPublic | BindingFlags.Static);
			field_TempContent.SetValue(null, result_GetSliderRect);

			return result_GetSliderRect;
		}

		public static Rect get_s_LastRect() {
			var field_TempContent = typeof(EditorGUILayout).GetField("s_LastRect", BindingFlags.NonPublic | BindingFlags.Static);
			return (Rect)field_TempContent.GetValue(null);
		}

		public static float SliderAllOptions(float value, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1, params GUILayoutOption[] options) {
			return CustomEditorGUI.SliderAllOptions(handleSliderLayout(false, options), (GUIContent)null, value, leftValue, rightValue, hardMin, hardMax, power);
		}

		public static float SliderAllOptions(string label, float value, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1, params GUILayoutOption[] options) {
			return CustomEditorGUI.SliderAllOptions(handleSliderLayout(true, options), label, value, leftValue, rightValue, hardMin, hardMax, power);
		}

		public static float SliderAllOptions(GUIContent label, float value, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1, params GUILayoutOption[] options) {
			return CustomEditorGUI.SliderAllOptions(handleSliderLayout(true, options), label, value, leftValue, rightValue, hardMin, hardMax, power);
		}

		public static void SliderAllOptions(SerializedProperty property, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1, params GUILayoutOption[] options) {
			CustomEditorGUI.SliderAllOptions(handleSliderLayout(true, options), property, leftValue, rightValue, hardMin, hardMax, power);
		}

		public static void SliderAllOptions(string label, SerializedProperty property, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1, params GUILayoutOption[] options) {
			CustomEditorGUI.SliderAllOptions(handleSliderLayout(true, options), label, property, leftValue, rightValue, hardMin, hardMax, power);
		}

		public static void SliderAllOptions(GUIContent label, SerializedProperty property, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1, params GUILayoutOption[] options) {
			CustomEditorGUI.SliderAllOptions(handleSliderLayout(true, options), label, property, leftValue, rightValue, hardMin, hardMax, power);
		}

		public static int IntSliderAllOptions(int value, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue, params GUILayoutOption[] options) {
			return CustomEditorGUI.IntSliderAllOptions(handleSliderLayout(true, options), value, leftValue, rightValue, hardMin, hardMax);
		}

		public static int IntSliderAllOptions(string label, int value, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue, params GUILayoutOption[] options) {
			return CustomEditorGUI.IntSliderAllOptions(handleSliderLayout(true, options), label, value, leftValue, rightValue, hardMin, hardMax);
		}

		public static int IntSliderAllOptions(GUIContent label, int value, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue, params GUILayoutOption[] options) {
			return CustomEditorGUI.IntSliderAllOptions(handleSliderLayout(true, options), label, value, leftValue, rightValue, hardMin, hardMax);
		}

		public static void IntSliderAllOptions(SerializedProperty property, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue, params GUILayoutOption[] options) {
			CustomEditorGUI.IntSliderAllOptions(handleSliderLayout(true, options), property, leftValue, rightValue, hardMin, hardMax);
		}

		public static void IntSliderAllOptions(string label, SerializedProperty property, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue, params GUILayoutOption[] options) {
			CustomEditorGUI.IntSliderAllOptions(handleSliderLayout(true, options), label, property, leftValue, rightValue, hardMin, hardMax);
		}

		public static void IntSliderAllOptions(GUIContent label, SerializedProperty property, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue, params GUILayoutOption[] options) {
			CustomEditorGUI.IntSliderAllOptions(handleSliderLayout(true, options), label, property, leftValue, rightValue, hardMin, hardMax);
		}

		public static void MultiSelectPopup(GUIContent label, string value, string[] valueOptions, string[] valueOptionsDisplayName, GUIStyle style, params GUILayoutOption[] options) {
			CustomEditorGUI.MultiSelectPopup(handleGenericLayout(true, style, options), label, value, valueOptions, valueOptionsDisplayName);
		}

		public static void MultiSelectPopup(GUIContent label, string value, string[] valueOptions, string[] valueOptionsDisplayName, string invalidPrepend, GUIStyle style, params GUILayoutOption[] options) {
			CustomEditorGUI.MultiSelectPopup(handleGenericLayout(true, style, options), label, value, valueOptions, valueOptionsDisplayName, invalidPrepend);
		}

		public static void MultiSelectPopup(GUIContent label, SerializedProperty value, string[] valueOptions, string[] valueOptionsDisplayName, GUIStyle style, params GUILayoutOption[] options) {
			CustomEditorGUI.MultiSelectPopup(handleGenericLayout(true, style, options), label, value, valueOptions, valueOptionsDisplayName);
		}

		public static void MultiSelectPopup(GUIContent label, SerializedProperty value, string[] valueOptions, string[] valueOptionsDisplayName, string invalidPrepend, GUIStyle style, params GUILayoutOption[] options) {
			CustomEditorGUI.MultiSelectPopup(handleGenericLayout(true, style, options), label, value, valueOptions, valueOptionsDisplayName, invalidPrepend);
		}
	}
}