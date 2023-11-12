#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace codec.PhotoFrame {
	[AddComponentMenu("")]
	public class MarkTypeEditorFramePreview : MonoBehaviour {
		public bool isGenerated;
		public GameObject generateSource;
		public float aspectRatio;
	}

	[CustomEditor(typeof(MarkTypeEditorFramePreview))]
	[CanEditMultipleObjects]
	public class MarkTypeEditorFramePreviewEditor : Editor {
		public override void OnInspectorGUI() {
			GUILayout.Label("If you are seeing this there is a bug. Delete this game object.");
			base.OnInspectorGUI();
		}
	}
}
#endif