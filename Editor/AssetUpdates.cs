using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace codec.PhotoFrame {
	public class AssetUpdates : AssetPostprocessor {
		//public void OnPostprocessTexture(Texture2D texture) {
		//	PhotoFrame.TextureUpdate(texture);
		//}

		//public void OnPostprocessPrefab(GameObject gameObject) {
		//	PhotoFrame.ModelUpdate(gameObject);
		//}

		//public void OnPostprocessModel(GameObject gameObject) {
		//	PhotoFrame.ModelUpdate(gameObject);
		//}

		private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
			foreach(string assetPath in importedAssets) {
				System.Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);

				if(type == typeof(GameObject)) PhotoFrame.ModelUpdate(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));
				else if(type == typeof(Texture2D)) PhotoFrame.TextureUpdate(AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));
			}
		}
	}
}
