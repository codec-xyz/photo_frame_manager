#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace codec.PhotoFrame {
	[InitializeOnLoad]
	[AddComponentMenu("")]
	[ExecuteInEditMode]
	public class SceneSettings : MonoBehaviour {
		public int textureSize = 4096;
		public int margin = 32;
		public float textureFit = 0.15f;
		public float skylineMaxSpread = 0.25f;

		public Texture2D[] textures = new Texture2D[0];
		public Material[] materials = new Material[0];
		public int[] pfCounts = new int[0];
		public PhotoFrame[] photoFrames = new PhotoFrame[0];

		[field: NonSerialized] public SerializedObject serializedObject { get; private set; }
		[field: NonSerialized] public SerializedProperty s_textureSize { get; private set; }
		[field: NonSerialized] public SerializedProperty s_margin { get; private set; }
		[field: NonSerialized] public SerializedProperty s_textureFit { get; private set; }
		[field: NonSerialized] public SerializedProperty s_skylineMaxSpread { get; private set; }

		public static SceneSettings active { get; private set; }

		static SceneSettings() {
			EditorSceneManager.activeSceneChangedInEditMode += ActiveSceneChanged;
		}

		public static void ActiveSceneChanged(Scene old, Scene activeScene) {
			active = null;
			foreach(GameObject rootObject in activeScene.GetRootGameObjects()) {
				var settingsComponent = rootObject.GetComponent<SceneSettings>();
				if(settingsComponent && active) DestroyImmediate(rootObject);
				else if(settingsComponent) active = settingsComponent;
			}

			if(!active) {
				var settingsObj = new GameObject("Photo Frame Manager Settings", typeof(SceneSettings));
				active = settingsObj.GetComponent<SceneSettings>();
			}

			active.gameObject.hideFlags = HideFlags.HideInHierarchy;
			active.gameObject.tag = "EditorOnly";
		}

		public void OnEnable() { // run on script updating
			Scene scene = EditorSceneManager.GetActiveScene();
			if(scene.isLoaded) ActiveSceneChanged(scene, scene);

			if(textures == null) textures = new Texture2D[0];
			if(materials == null) materials = new Material[0];
			if(pfCounts == null) pfCounts = new int[0];
			if(photoFrames == null) photoFrames = new PhotoFrame[0];

			serializedObject = new UnityEditor.SerializedObject(this);
			s_textureSize = serializedObject.FindProperty("textureSize");
			s_margin = serializedObject.FindProperty("margin");
			s_textureFit = serializedObject.FindProperty("textureFit");
			s_skylineMaxSpread = serializedObject.FindProperty("skylineMaxSpread");
		}

		public void DeleteTexturesAndMaterials() {
			foreach(var texture in textures) {
				if(!texture) continue;
				string path = AssetDatabase.GetAssetPath(texture);
				if(path != "") AssetDatabase.DeleteAsset(path);
				DestroyImmediate(texture, true);
			}

			foreach(var material in materials) {
				if(!material) continue;
				string path = AssetDatabase.GetAssetPath(material);
				if(path != "") AssetDatabase.DeleteAsset(path);
				DestroyImmediate(material, true);
			}

			textures = new Texture2D[0];
			materials = new Material[0];
			pfCounts = new int[0];
			photoFrames = new PhotoFrame[0];
		}
	}
}
#endif