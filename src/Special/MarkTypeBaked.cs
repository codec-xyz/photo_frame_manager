#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace codec.PhotoFrame {
	[AddComponentMenu("")]
	public class MarkTypeBaked : MonoBehaviour {
	}

	[CustomEditor(typeof(MarkTypeBaked))]
	[CanEditMultipleObjects]
	public class MarkTypeBakedEditor : Editor {
		public override void OnInspectorGUI() {
			GUILayout.Label("Internal use, don't touch");
			GUILayout.Label("Used for cleanup incase object gets lost");
			base.OnInspectorGUI();
		}
	}
}
#endif