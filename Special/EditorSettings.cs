#if UNITY_EDITOR
using UnityEditor;

namespace codec.PhotoFrame {
	public static class EditorSettings {
		public const string debugEditorPref = "wtf.codec.photo-frame-manager.debug";
		public const string livePreviewEditorPref = "wtf.codec.photo-frame-manager.livePreview";
		public const string rightAlignedFieldsEditorPref = "wtf.codec.photo-frame-manager.rightAlignedFields";

		public static bool debug {
			get => EditorPrefs.GetBool(debugEditorPref, false);
			set => EditorPrefs.SetBool(debugEditorPref, value);
		}

		public static bool livePreview {
			get => EditorPrefs.GetBool(livePreviewEditorPref, true);
			set => EditorPrefs.SetBool(livePreviewEditorPref, value);
		}

		public static bool rightAlignedFields {
			get => EditorPrefs.GetBool(rightAlignedFieldsEditorPref, true);
			set => EditorPrefs.SetBool(rightAlignedFieldsEditorPref, value);
		}
	}
}
#endif