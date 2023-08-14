#if UNITY_EDITOR
using UnityEngine;

namespace codec.PhotoFrame {
	[CreateAssetMenu(fileName = "Photo Frame Type", menuName = "Photo Frame Type")]
	public class PhotoFrameType : ScriptableObject {
		public Material material;
		public string textureSlot;
		public bool isSmallerDimensionAlwaysOne = false;
		public GameObject[] photoFrames;
		public Vector2[] aspectRatios;
	}
}
#endif