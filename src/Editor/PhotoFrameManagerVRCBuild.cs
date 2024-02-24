#if VRC
using UnityEditor;
using static UnityEngine.Object;
using VRC.SDKBase;
using VRC.SDKBase.Editor.BuildPipeline;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace codec.PhotoFrame {
	public class PhotoFrameManagerVRCBuild : IVRCSDKBuildRequestedCallback {
		public int callbackOrder => 0;
		public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType) {
			if(requestedBuildType == VRCSDKRequestedBuildType.Avatar) return true;
			var descriptors = FindObjectsOfType<VRC_SceneDescriptor>(true);
			if(descriptors.Length == 0) return true;
			Scene buildScene = descriptors[0].gameObject.scene;
			if(!SceneSettings.DoesSceneNeedBake(buildScene)) return true;

			int todo = EditorUtility.DisplayDialogComplex("Bake Photo Frames", "Photo frames in the scene are not fully backed. Do you want to bake them now?", "Bake", "Abort Build", "Continue Without Baking");

			if(todo == 1) return false;
			if(todo == 2) return true;

			Scene activeScene = EditorSceneManager.GetActiveScene();
			if(activeScene != buildScene) EditorSceneManager.SetActiveScene(buildScene);

			//VRChat saves during build
			ManagerWindow.BakeTextures(true);
			return true;
		}
	}
}
#endif