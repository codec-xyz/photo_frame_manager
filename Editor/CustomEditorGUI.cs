using UnityEditor;
using UnityEngine;

namespace codec {
	public static class CustomEditorGUI {
		private static readonly int s_SliderHash = "EditorSlider".GetHashCode();

		internal static string kFloatFieldFormatString = "g7";

		public static GUIContent TempContent(string text) {
			var method_TempContent = typeof(EditorGUIUtility).GetMethod("TempContent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new[] { typeof(string) }, null);
			return (GUIContent)method_TempContent.Invoke(null, new object[] { text });
		}

		public static float SliderAllOptions(Rect position, float value, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1) {
			var method_DragZoneRect = typeof(EditorGUIUtility).GetMethod("DragZoneRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			var result_DragZoneRect = (Rect)method_DragZoneRect.Invoke(null, new object[] { position, false });

			var field_horizontalSliderThumbExtent = typeof(GUISkin).GetProperty("horizontalSliderThumbExtent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			var value_horizontalSliderThumbExtent = (GUIStyle)field_horizontalSliderThumbExtent.GetGetMethod(true).Invoke(GUI.skin, new object[] { });

			var method_DoSlider = typeof(EditorGUI).GetMethod("DoSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
				new[] { typeof(Rect), typeof(Rect), typeof(int), typeof(float), typeof(float), typeof(float), typeof(string), typeof(float), typeof(float), typeof(float), typeof(GUIStyle), typeof(GUIStyle), typeof(GUIStyle), typeof(Texture2D), typeof(GUIStyle) }, null);

			int controlID = GUIUtility.GetControlID(s_SliderHash, FocusType.Keyboard, position);
			return (float)method_DoSlider.Invoke(null, new object[] { EditorGUI.IndentedRect(position), result_DragZoneRect, controlID, value, leftValue, rightValue, kFloatFieldFormatString, hardMin, hardMax, power, EditorStyles.numberField, GUI.skin.horizontalSlider, GUI.skin.horizontalSliderThumb, null, value_horizontalSliderThumbExtent });
		}

		public static float SliderAllOptions(Rect position, string label, float value, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1) {
			return SliderAllOptions(position, TempContent(label), value, leftValue, rightValue, hardMin, hardMax, power);
		}

		public static float SliderAllOptions(Rect position, GUIContent label, float value, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1) {
			var method_LabelHasContent = typeof(EditorGUI).GetMethod("LabelHasContent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			var result_LabelHasContent = (bool)method_LabelHasContent.Invoke(null, new object[] { label });

			var method_DragZoneRect = typeof(EditorGUIUtility).GetMethod("DragZoneRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			var result_DragZoneRect = (Rect)method_DragZoneRect.Invoke(null, new object[] { position, true });

			var field_horizontalSliderThumbExtent = typeof(GUISkin).GetProperty("horizontalSliderThumbExtent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			var value_horizontalSliderThumbExtent = (GUIStyle)field_horizontalSliderThumbExtent.GetGetMethod(true).Invoke(GUI.skin, new object[] { });

			var method_DoSlider = typeof(EditorGUI).GetMethod("DoSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
				new[] { typeof(Rect), typeof(Rect), typeof(int), typeof(float), typeof(float), typeof(float), typeof(string), typeof(float), typeof(float), typeof(float), typeof(GUIStyle), typeof(GUIStyle), typeof(GUIStyle), typeof(Texture2D), typeof(GUIStyle) }, null);

			int controlID = GUIUtility.GetControlID(s_SliderHash, FocusType.Keyboard, position);
			Rect position2 = EditorGUI.PrefixLabel(position, controlID, label);
			Rect dragZonePosition = (result_LabelHasContent ? result_DragZoneRect : default(Rect));
			return (float)method_DoSlider.Invoke(null, new object[] { position2, dragZonePosition, controlID, value, leftValue, rightValue, kFloatFieldFormatString, hardMin, hardMax, power, EditorStyles.numberField, GUI.skin.horizontalSlider, GUI.skin.horizontalSliderThumb, null, value_horizontalSliderThumbExtent });
		}

		public static void SliderAllOptions(Rect position, SerializedProperty property, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1) {
			SliderAllOptions(position, TempContent(property.displayName), property, leftValue, rightValue, hardMin, hardMax, power);
		}

		public static void SliderAllOptions(Rect position, string label, SerializedProperty property, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1) {
			SliderAllOptions(position, TempContent(label), property, leftValue, rightValue, hardMin, hardMax, power);
		}

		public static void SliderAllOptions(Rect position, GUIContent label, SerializedProperty property, float leftValue, float rightValue, float hardMin = float.MinValue, float hardMax = float.MaxValue, float power = 1) {
			label = EditorGUI.BeginProperty(position, label, property);
			EditorGUI.BeginChangeCheck();
			float floatValue = SliderAllOptions(position, label, property.floatValue, leftValue, rightValue, hardMin, hardMax, power);
			if(EditorGUI.EndChangeCheck()) {
				property.floatValue = floatValue;
			}

			EditorGUI.EndProperty();
		}

		public static int IntSliderAllOptions(Rect position, int value, int leftValue, int rightValue, int hardMin = int.MinValue, int hardMax = int.MaxValue) {
			return Mathf.RoundToInt(SliderAllOptions(position, value, leftValue, rightValue, hardMin, hardMax));
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
			label = EditorGUI.BeginProperty(position, label, property);
			EditorGUI.BeginChangeCheck();
			int intValue = IntSliderAllOptions(position, label, property.intValue, leftValue, rightValue, hardMin, hardMax);
			if(EditorGUI.EndChangeCheck()) {
				property.intValue = intValue;
			}

			EditorGUI.EndProperty();
		}
	}
}