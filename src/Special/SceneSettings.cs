#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace codec.PhotoFrame {
	[InitializeOnLoad]
	[DisallowMultipleComponent]
	[ExecuteInEditMode]
	[AddComponentMenu("")]
	public class SceneSettings : MonoBehaviour {
		public static int default_textureSize = 4096;
		public static int default_margin = 32;
		public static bool default_scaleMargin = true;
		public static bool default_joinDuplicates = true;
		public static float default_textureFit = 0.15f;
		public static float default_skylineMaxSpread = 0.25f;
		public static float default_estimatedPackEfficiency = 0.85f;
		public static float default_overhangWeight = 7f;
		public static float default_neighborhoodWasteWeight = 3f;
		public static float default_topWasteWeight = 1f;
		public static bool default_scaleResolutionBySize = true;
		public static float default_scaleResMax = 1;
		public static float default_scaleResMin = 0.5f;
		public static int default_resolutionMaxMajorSize = 1280;
		//texture importer settings
		public static bool default_tex_generateMipmaps = true;
		public static bool default_tex_mipmapStreaming = true;
		public static sbyte default_tex_mipmapPriority = 0;
		public static bool default_tex_preserveCoverage = false;
		public static TextureImporterMipFilter default_tex_mipmapFiltering = TextureImporterMipFilter.BoxFilter;
		public static FilterMode default_tex_filterMode = FilterMode.Bilinear;
		public static int default_tex_anisoLevel = 1;
		public static TextureImporterCompression default_tex_textureCompression = TextureImporterCompression.Compressed;
		public static bool default_tex_crunchedCompression = false;
		public static int default_tex_crunchedcompressionQuality = 50;

		public int textureSize = default_textureSize;
		public int margin = default_margin;
		public bool scaleMargin = default_scaleMargin;
		public bool joinDuplicates = default_joinDuplicates;
		public float textureFit = default_textureFit;
		public float skylineMaxSpread = default_skylineMaxSpread;
		public float estimatedPackEfficiency = default_estimatedPackEfficiency;
		public float overhangWeight = default_overhangWeight;
		public float neighborhoodWasteWeight = default_neighborhoodWasteWeight;
		public float topWasteWeight = default_topWasteWeight;
		public bool scaleResolutionBySize = default_scaleResolutionBySize;
		public float scaleResMax = default_scaleResMax;
		public float scaleResMin = default_scaleResMin;
		public int resolutionMaxMajorSize = default_resolutionMaxMajorSize;
		//texture importer settings
		public bool tex_generateMipmaps = default_tex_generateMipmaps;
		public bool tex_mipmapStreaming = default_tex_mipmapStreaming;
		public sbyte tex_mipmapPriority = default_tex_mipmapPriority;
		public bool tex_preserveCoverage = default_tex_preserveCoverage;
		public TextureImporterMipFilter tex_mipmapFiltering = default_tex_mipmapFiltering;
		public FilterMode tex_filterMode = default_tex_filterMode;
		public int tex_anisoLevel = default_tex_anisoLevel;
		public TextureImporterCompression tex_textureCompression = default_tex_textureCompression;
		public bool tex_crunchedCompression = default_tex_crunchedCompression;
		public int tex_crunchedcompressionQuality = default_tex_crunchedcompressionQuality;


		public bool hasBake = false;
		public Texture2D[] textures = new Texture2D[0];
		public Material[] materials = new Material[0];
		public int[] pfCounts = new int[0];
		public PhotoFrame[] photoFrames = new PhotoFrame[0]; //flattened array or arrays (pfCounts has array sizes)
		public Mesh[] meshes = new Mesh[0];
		public Mesh[] frameMeshes = new Mesh[0];
		public GameObject[] framePrefabs = new GameObject[0];

		[field: NonSerialized] public SerializedObject serializedObject { get; private set; }
		[field: NonSerialized] public SerializedProperty s_textureSize { get; private set; }
		[field: NonSerialized] public SerializedProperty s_margin { get; private set; }
		[field: NonSerialized] public SerializedProperty s_scaleMargin { get; private set; }
		[field: NonSerialized] public SerializedProperty s_joinDuplicates { get; private set; }
		[field: NonSerialized] public SerializedProperty s_textureFit { get; private set; }
		[field: NonSerialized] public SerializedProperty s_skylineMaxSpread { get; private set; }
		[field: NonSerialized] public SerializedProperty s_estimatedPackEfficiency { get; private set; }
		[field: NonSerialized] public SerializedProperty s_overhangWeight { get; private set; }
		[field: NonSerialized] public SerializedProperty s_neighborhoodWasteWeight { get; private set; }
		[field: NonSerialized] public SerializedProperty s_topWasteWeight { get; private set; }
		[field: NonSerialized] public SerializedProperty s_scaleResolutionBySize { get; private set; }
		[field: NonSerialized] public SerializedProperty s_scaleResMax { get; private set; }
		[field: NonSerialized] public SerializedProperty s_scaleResMin { get; private set; }
		[field: NonSerialized] public SerializedProperty s_resolutionMaxMajorSize { get; private set; }
		//texture importer settings
		[field: NonSerialized] public SerializedProperty s_tex_generateMipmaps { get; private set; }
		[field: NonSerialized] public SerializedProperty s_tex_mipmapStreaming { get; private set; }
		[field: NonSerialized] public SerializedProperty s_tex_mipmapPriority { get; private set; }
		[field: NonSerialized] public SerializedProperty s_tex_preserveCoverage { get; private set; }
		[field: NonSerialized] public SerializedProperty s_tex_mipmapFiltering { get; private set; }
		[field: NonSerialized] public SerializedProperty s_tex_filterMode { get; private set; }
		[field: NonSerialized] public SerializedProperty s_tex_anisoLevel { get; private set; }
		[field: NonSerialized] public SerializedProperty s_tex_textureCompression { get; private set; }
		[field: NonSerialized] public SerializedProperty s_tex_crunchedCompression { get; private set; }
		[field: NonSerialized] public SerializedProperty s_tex_crunchedCompressionQuality { get; private set; }

		public static SceneSettings active { get; private set; }

		public static SceneSettings ofScene(Scene scene) {
			if(scene.name == null) throw new Exception("Not a valid scene");
			var settings = FindSceneSettings(scene);
			if(!settings) active = AddSceneSettings(scene);
			return settings;
		}

		static SceneSettings() {
			EditorSceneManager.activeSceneChangedInEditMode += ActiveSceneChanged;
		}

		public static SceneSettings FindSceneSettings(Scene scene) {
			var settingsList = scene.GetRootGameObjects()
			.Select(obj => obj.GetComponent<SceneSettings>())
			.Where(s => s != null);

			foreach(var s in settingsList.Skip(1)) DestroyImmediate(s);

			return settingsList.FirstOrDefault();
		}

		public static SceneSettings AddSceneSettings(Scene scene) {
			var settingsObj = new GameObject("Photo Frame Manager Settings", typeof(SceneSettings));
			settingsObj.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInBuild;
			settingsObj.tag = "EditorOnly";
			return settingsObj.GetComponent<SceneSettings>();
		}

		public static void ActiveSceneChanged(Scene old, Scene activeScene) {
			active = ofScene(activeScene);
		}

		public static void AssureActiveNotMissing() {
			if(active) return;
			active = ofScene(SceneManager.GetActiveScene());
		}

		public static bool DoesSceneNeedBake(Scene scene) {
			return FindObjectsOfType<PhotoFrame>(true).Where(pf => pf.gameObject.scene == scene && !pf.bakedData).Any();
		}

		public void Reset() {
			textureSize = default_textureSize;
			margin = default_margin;
			scaleMargin = default_scaleMargin;
			joinDuplicates = default_joinDuplicates;
			textureFit = default_textureFit;
			skylineMaxSpread = default_skylineMaxSpread;
			estimatedPackEfficiency = default_estimatedPackEfficiency;
			overhangWeight = default_overhangWeight;
			neighborhoodWasteWeight = default_neighborhoodWasteWeight;
			topWasteWeight = default_topWasteWeight;
			scaleResolutionBySize = default_scaleResolutionBySize;
			scaleResMax = default_scaleResMax;
			scaleResMin = default_scaleResMin;
			resolutionMaxMajorSize = default_resolutionMaxMajorSize;
			//texture importer settings
			tex_generateMipmaps = default_tex_generateMipmaps;
			tex_mipmapStreaming = default_tex_mipmapStreaming;
			tex_mipmapPriority = default_tex_mipmapPriority;
			tex_preserveCoverage = default_tex_preserveCoverage;
			tex_mipmapFiltering = default_tex_mipmapFiltering;
			tex_filterMode = default_tex_filterMode;
			tex_anisoLevel = default_tex_anisoLevel;
			tex_textureCompression = default_tex_textureCompression;
			tex_crunchedCompression = default_tex_crunchedCompression;
			tex_crunchedcompressionQuality = default_tex_crunchedcompressionQuality;
		}

		public void OnEnable() { //runs on script updating
			Scene scene = EditorSceneManager.GetActiveScene();
			if(scene.isLoaded) ActiveSceneChanged(scene, scene);

			if(textures == null) textures = new Texture2D[0];
			if(materials == null) materials = new Material[0];
			if(pfCounts == null) pfCounts = new int[0];
			if(photoFrames == null) photoFrames = new PhotoFrame[0];

			serializedObject = new UnityEditor.SerializedObject(this);
			s_textureSize = serializedObject.FindProperty("textureSize");
			s_margin = serializedObject.FindProperty("margin");
			s_scaleMargin = serializedObject.FindProperty("scaleMargin");
			s_joinDuplicates = serializedObject.FindProperty("joinDuplicates");
			s_textureFit = serializedObject.FindProperty("textureFit");
			s_skylineMaxSpread = serializedObject.FindProperty("skylineMaxSpread");
			s_estimatedPackEfficiency = serializedObject.FindProperty("estimatedPackEfficiency");
			s_overhangWeight = serializedObject.FindProperty("overhangWeight");
			s_neighborhoodWasteWeight = serializedObject.FindProperty("neighborhoodWasteWeight");
			s_topWasteWeight = serializedObject.FindProperty("topWasteWeight");
			s_scaleResolutionBySize = serializedObject.FindProperty("scaleResolutionBySize");
			s_scaleResMax = serializedObject.FindProperty("scaleResMax");
			s_scaleResMin = serializedObject.FindProperty("scaleResMin");
			s_resolutionMaxMajorSize = serializedObject.FindProperty("resolutionMaxMajorSize");
			//texture importer settings
			s_tex_generateMipmaps = serializedObject.FindProperty("tex_generateMipmaps");
			s_tex_mipmapStreaming = serializedObject.FindProperty("tex_mipmapStreaming");
			s_tex_mipmapPriority = serializedObject.FindProperty("tex_mipmapPriority");
			s_tex_preserveCoverage = serializedObject.FindProperty("tex_preserveCoverage");
			s_tex_mipmapFiltering = serializedObject.FindProperty("tex_mipmapFiltering");
			s_tex_filterMode = serializedObject.FindProperty("tex_filterMode");
			s_tex_anisoLevel = serializedObject.FindProperty("tex_anisoLevel");
			s_tex_textureCompression = serializedObject.FindProperty("tex_textureCompression");
			s_tex_crunchedCompression = serializedObject.FindProperty("tex_crunchedCompression");
			s_tex_crunchedCompressionQuality = serializedObject.FindProperty("tex_crunchedcompressionQuality");
		}

		public void deleteBake(bool previewUpdate) {
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

			//foreach(var pf in photoFrames) {
			foreach(var pf in Utils.LoadedScenes_FindComponentsOfType<PhotoFrame>(true)) {
				if(!pf) continue;
				pf.deleteBakedData();
				if(previewUpdate) pf.enableAndUpdatePreview();
			}

			foreach(var mesh in meshes) {
				if(!mesh) continue;
				string path = AssetDatabase.GetAssetPath(mesh);
				if(path != "") AssetDatabase.DeleteAsset(path);
			}

			foreach(var frameMesh in frameMeshes) {
				if(!frameMesh) continue;
				string path = AssetDatabase.GetAssetPath(frameMesh);
				if(path != "") AssetDatabase.DeleteAsset(path);
			}

			foreach(var framePrefab in framePrefabs) {
				if(!framePrefab) continue;
				string path = AssetDatabase.GetAssetPath(framePrefab);
				if(path != "") AssetDatabase.DeleteAsset(path);
			}

			hasBake = false;
			textures = new Texture2D[0];
			materials = new Material[0];
			pfCounts = new int[0];
			photoFrames = new PhotoFrame[0];
			meshes = new Mesh[0];
			frameMeshes = new Mesh[0];
			framePrefabs = new GameObject[0];

			foreach(var lostObj in Utils.LoadedScenes_FindComponentsOfType<MarkTypeBaked>(true)) {
				Transform parent = lostObj.transform.parent;
				DestroyImmediate(lostObj.gameObject);
				PrefabUtility.RecordPrefabInstancePropertyModifications(parent);
			}
		}
	}
}
#endif