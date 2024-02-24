using codec.PhotoFrame;
using codec;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Burst.CompilerServices;

namespace codec.PhotoFrame {
	[InitializeOnLoad]
	public class ManagerWindow : EditorWindow {
		public int tabSelected;
		public Vector2 scrollPosition;
		public Vector2 debugScrollPosition;
		public bool sceneMainDropdown = true;
		public bool sceneAdvancedDropdown = false;
		public bool sceneTextureDropdown = true;

		static ManagerWindow() {
			EditorApplication.delayCall += () => {
				Menu.SetChecked("Photo Frames/Live Preview", EditorSettings.livePreview);
			};
		}

		[MenuItem("Photo Frames/Window", false, priority = 1)]
		[MenuItem("Window/Photo Frames")]
		public static void OpenWindow() {
			ManagerWindow window = GetWindow<ManagerWindow>();
			window.titleContent = new GUIContent("Photo Frames");
			window.minSize = new Vector2(250, 250);
		}

		[MenuItem("Assets/Add Images As Photo Frames", true)]
		public static bool AddImagesAsPhotoFramesValidate() {
			foreach(var obj in Selection.objects) {
				if(obj is Texture2D) return true;
			}
			return false;
		}

		[MenuItem("Assets/Add Images As Photo Frames")]
		public static void AddImagesAsPhotoFrames() {
			PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
			List<GameObject> createdObjs = new List<GameObject>();

			foreach(var obj in Selection.objects) {
				if(obj is Texture2D photo) {
					PhotoFrame photoFrame = PhotoFrame.Create();
					Undo.RegisterCreatedObjectUndo(photoFrame.gameObject, "Add Images As Photo Frames");
					photoFrame.photo = photo;
					if(prefabStage != null) {
						GameObjectUtility.SetParentAndAlign(photoFrame.gameObject, prefabStage.prefabContentsRoot);
					}
					if(EditorSettings.livePreview) photoFrame.enableAndUpdatePreview();
					createdObjs.Append(photoFrame.gameObject);
				}
			}

			Selection.objects = createdObjs.ToArray();
		}

		[MenuItem("GameObject/Photo Frame", false, 10)]
		public static void AddPhoto(MenuCommand menuCommand) {
			GameObject photoObject = PhotoFrame.Create().gameObject;

			if(menuCommand.context) GameObjectUtility.SetParentAndAlign(photoObject, menuCommand.context as GameObject);
			else {
				PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
				if(prefabStage != null) {
					GameObjectUtility.SetParentAndAlign(photoObject, prefabStage.prefabContentsRoot);
				}
			}
			Undo.RegisterCreatedObjectUndo(photoObject, "Create " + photoObject.name);
			Selection.activeObject = photoObject;
		}

		//[MenuItem("Photo Frames/Test")]
		//public static void Test() {
		//}

		[MenuItem("Photo Frames/Bake Photo Frames", priority = 3)]
		public static void BakeTexturesMenu() => BakeTextures();

		public static void BakeTextures(bool bypassSaveDialog = false) {
			SceneSettings.AssureActiveNotMissing();
			Scene activeScene = EditorSceneManager.GetActiveScene();
			if(!bypassSaveDialog && activeScene.isDirty && !EditorUtility.DisplayDialog("Bake Photo Frames", "Baking will save the scene. Okay?", "Bake and Save", "Cancel")) {
				return;
			}

			EditorUtility.SetDirty(SceneSettings.active);

			PhotoFrame[] photoFrames = Utils.LoadedScenes_FindComponentsOfType<PhotoFrame>(true).Where(pf => pf.photo).ToArray();

			PhotoFrameBaker.Bake(photoFrames, SceneSettings.active, EditorSettings.debug);

			EditorSceneManager.SaveScene(activeScene);
		}

		[MenuItem("Photo Frames/Delete Bake", true)]
		public static bool DeleteTexturesValidate() {
			SceneSettings.AssureActiveNotMissing();
			return SceneSettings.active.hasBake;
		}

		[MenuItem("Photo Frames/Delete Bake", priority = 4)]
		public static void DeleteTextures() {
			SceneSettings.AssureActiveNotMissing();
			Scene activeScene = EditorSceneManager.GetActiveScene();
			if(activeScene.isDirty && !EditorUtility.DisplayDialog("Delete Bake", "Deleting a bake will save the scene. Okay?", "Delete Bake and Save", "Cancel")) {
				return;
			}

			EditorUtility.SetDirty(SceneSettings.active);

			try {
				AssetDatabase.StartAssetEditing();

				SceneSettings.active.deleteBake(EditorSettings.livePreview);
			}
			catch(Exception e) {
				Debug.LogException(e);
			}
			finally {
				AssetDatabase.StopAssetEditing();
				EditorSceneManager.SaveScene(activeScene);
			}
		}

		[MenuItem("Photo Frames/Live Preview", priority = 2)]
		private static void ToggleLivePreview() {
			bool livePreview = EditorSettings.livePreview = !EditorSettings.livePreview;

			Menu.SetChecked("Photo Frames/Live Preview", livePreview);
			Scene activeScene = SceneManager.GetActiveScene();
			foreach(var photoFrame in Utils.LoadedScenes_FindComponentsOfType<PhotoFrame>()) {
				if(livePreview) photoFrame.enableAndUpdatePreview();
				else photoFrame.disablePreview();
			}
		}

		public void bakeAndDeleteButtonsGUI() {
			Rect rect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight * 1.25f, GUILayout.ExpandWidth(true));
			rect.width -= EditorGUIUtility.singleLineHeight * 0.5f;
			rect.x += EditorGUIUtility.singleLineHeight * 0.25f;
			rect.width /= 2;
			if(!SceneSettings.active.hasBake && GUI.Button(rect, "Bake Photo Frames")) BakeTextures();
			if(SceneSettings.active.hasBake && GUI.Button(rect, "Rebake Photo Frames")) BakeTextures();
			rect.x += rect.width;
			GUI.enabled = SceneSettings.active.hasBake;
			if(GUI.Button(rect, "Delete Bake")) DeleteTextures();
			GUI.enabled = true;
		}

		bool isScaleSliderBeingUsed = false;
		float scaleSliderMax = 1;
		public void mainSettingsDropdown() {
			var textureSizes = (new HashSet<int> { 64, 128, 256, 512, 1024, 2048, 4096, 8192, SceneSettings.active.textureSize }).ToArray();
			Array.Sort(textureSizes);

			UtilsGUI.AlignedIntPopup(new GUIContent("Baked Texture Size", "Target size of the baked textures. Some texture might be smaller to reduce size"), SceneSettings.active.s_textureSize, textureSizes.Select(n => new GUIContent(n.ToString())).ToArray(), textureSizes);
			UtilsGUI.AlignedIntSliderAllOptions(new GUIContent("Photo Margin", "Extra colored area around photos to prevent texture bleeding; measured in pixels"), SceneSettings.active.s_margin, 0, (int)(SceneSettings.active.textureSize * 0.125f));
			UtilsGUI.AlignedLeftToggle(new GUIContent("Scale Photo Margin", "Scales the photo margin down when baked textures are below the specified baked texture size"), SceneSettings.active.s_scaleMargin, true);
			UtilsGUI.AlignedLeftToggle(new GUIContent("Join Duplicate Photos", "Joins duplicate photos in baked texture"), SceneSettings.active.s_joinDuplicates, true);

			GUILayout.Space(EditorGUIUtility.singleLineHeight);

			EditorGUI.BeginChangeCheck();
			UtilsGUI.AlignedLeftToggle(new GUIContent("Scale Resolution By Size", "Scales the resolution of photos based on their scale. Photos with resolution full are skipped"), SceneSettings.active.s_scaleResolutionBySize, true);
			if(SceneSettings.active.s_scaleResolutionBySize.boolValue) {
				EditorGUI.indentLevel++;
				if(Event.current.type == EventType.MouseUp) isScaleSliderBeingUsed = false;
				var isMouseDown = Event.current.type == EventType.MouseDown;
				float min = SceneSettings.active.s_scaleResMin.floatValue, max = SceneSettings.active.s_scaleResMax.floatValue;

				if(!isScaleSliderBeingUsed) scaleSliderMax = Mathf.Pow(2, Mathf.Ceil(Mathf.Log(Mathf.Abs(max) + 0.0001f, 2) + 0.0001f));

				UtilsGUI.AlignedMinMaxSlider(new GUIContent("Scale Resolution Values", "Resolution starts being lowered at the maximum value and stops at the minimum value. Photos with resolution full are skipped"), ref min, ref max, 0, scaleSliderMax, true);
				if(isMouseDown && Event.current.type == EventType.Used) isScaleSliderBeingUsed = true;
				SceneSettings.active.s_scaleResMin.floatValue = min;
				SceneSettings.active.s_scaleResMax.floatValue = max;
				EditorGUI.indentLevel--;
			}

			SceneSettings.active.s_resolutionMaxMajorSize.intValue = UtilsGUI.ResolutionPicker(new GUIContent("Resolution Max Major Size", "Photo frames set to \"Use Scene Settings\" will use this resolution value"), SceneSettings.active.s_resolutionMaxMajorSize.intValue, 4096);
			if(EditorGUI.EndChangeCheck()) PhotoFrame.UpdateAll(false);
		}

		public void advancedSettingsDropdown() {
			UtilsGUI.AlignedSliderAllOptions(new GUIContent("Texture Fit",
				"Sorting prioritization (special sorting for better mipmap streaming vs fewer textures)\n"
				+ "0 - sorted for better mipmap streaming\n1 - sorted for fewer textures"), SceneSettings.active.s_textureFit, 0, 1, 0, float.MaxValue);
			UtilsGUI.AlignedSliderAllOptions(new GUIContent("Estimated Pack Efficiency", "Expected packing efficiency used when sorting"), SceneSettings.active.s_estimatedPackEfficiency, 0.1f, 1, 0, 1);
			UtilsGUI.AlignedSliderAllOptions(new GUIContent("Skyline Max Spread", "Parameter for the packing algorithm"), SceneSettings.active.s_skylineMaxSpread, 0, 1, 0, 1);
			UtilsGUI.AlignedSliderAllOptions(new GUIContent("Overhang Weight", "Parameter for the packing algorithm"), SceneSettings.active.s_overhangWeight, 0, 100);
			UtilsGUI.AlignedSliderAllOptions(new GUIContent("Neighborhood Waste Weight", "Parameter for the packing algorithm"), SceneSettings.active.s_neighborhoodWasteWeight, 0, 100);
			UtilsGUI.AlignedSliderAllOptions(new GUIContent("Top Waste Weight", "Parameter for the packing algorithm"), SceneSettings.active.s_topWasteWeight, 0, 100);
		}

		public void texturSettingsDropdown() {
			UtilsGUI.AlignedLeftToggle(new GUIContent("Mipmaps"), SceneSettings.active.s_tex_generateMipmaps, true);
			if(SceneSettings.active.s_tex_generateMipmaps.boolValue) {
				EditorGUI.indentLevel++;
				UtilsGUI.AlignedLeftToggle(new GUIContent("Mipmap Streaming"), SceneSettings.active.s_tex_mipmapStreaming, true);
				if(SceneSettings.active.s_tex_mipmapStreaming.boolValue) {
					EditorGUI.indentLevel++;
					UtilsGUI.AlignedPropertyField(new GUIContent("Priority"), SceneSettings.active.s_tex_mipmapPriority);
					EditorGUI.indentLevel--;
				}
				UtilsGUI.AlignedPropertyField(new GUIContent("Preserve Coverage"), SceneSettings.active.s_tex_preserveCoverage);
				UtilsGUI.AlignedPropertyField(new GUIContent("Filtering"), SceneSettings.active.s_tex_mipmapFiltering);
				EditorGUI.indentLevel--;
				GUILayout.Space(EditorGUIUtility.singleLineHeight);
			}
			UtilsGUI.AlignedPropertyField(new GUIContent("Filter Mode"), SceneSettings.active.s_tex_filterMode);
			UtilsGUI.AlignedIntSliderAllOptions(new GUIContent("Aniso Level"), SceneSettings.active.s_tex_anisoLevel, 0, 16, 0, 16);
			GUILayout.Space(EditorGUIUtility.singleLineHeight);
			SceneSettings.active.s_tex_textureCompression.enumValueIndex = UtilsGUI.AlignedIntPopup(new GUIContent("Compression"), SceneSettings.active.s_tex_textureCompression.enumValueIndex, new GUIContent[] { new GUIContent("None"), new GUIContent("Low Quality"), new GUIContent("Normal Quality"), new GUIContent("High Quality") }, new int[] { (int)TextureImporterCompression.Uncompressed, (int)TextureImporterCompression.CompressedLQ, (int)TextureImporterCompression.Compressed, (int)TextureImporterCompression.CompressedHQ });
			UtilsGUI.AlignedLeftToggle(new GUIContent("Use Crunch Compression"), SceneSettings.active.s_tex_crunchedCompression, true);
			if(SceneSettings.active.s_tex_crunchedCompression.boolValue) UtilsGUI.AlignedIntSliderAllOptions(new GUIContent("Compression Quality"), SceneSettings.active.s_tex_crunchedCompressionQuality, 0, 100, 0, 100);
		}

		public void tab_SceneSettingsGUI() {
			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

			sceneMainDropdown = EditorGUILayout.BeginFoldoutHeaderGroup(sceneMainDropdown, "Bake Settings");
			EditorGUILayout.EndFoldoutHeaderGroup();
			if(sceneMainDropdown) {
				mainSettingsDropdown();
				GUILayout.Space(EditorGUIUtility.singleLineHeight);
			}

			sceneAdvancedDropdown = EditorGUILayout.BeginFoldoutHeaderGroup(sceneAdvancedDropdown, "Advanced Settings");
			EditorGUILayout.EndFoldoutHeaderGroup();
			if(sceneAdvancedDropdown) {
				advancedSettingsDropdown();
				GUILayout.Space(EditorGUIUtility.singleLineHeight);
			}

			sceneTextureDropdown = EditorGUILayout.BeginFoldoutHeaderGroup(sceneTextureDropdown, "Texture Settings", null, (rect) => {
				var menu = new GenericMenu();
				if(!SceneSettings.active.hasBake) menu.AddDisabledItem(new GUIContent("Edit baked texture settings"));
				else menu.AddItem(new GUIContent("Edit baked texture settings"), false, () => {
					Selection.objects = SceneSettings.active.textures;
					EditorWindow.FocusWindowIfItsOpen(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow"));
				});
				menu.DropDown(rect);
			});
			EditorGUILayout.EndFoldoutHeaderGroup();
			if(sceneTextureDropdown) texturSettingsDropdown();

			EditorGUILayout.EndScrollView();
			GUILayout.Space(EditorGUIUtility.singleLineHeight);
			bakeAndDeleteButtonsGUI();
			GUILayout.FlexibleSpace();
			GUILayout.Space(EditorGUIUtility.singleLineHeight);
		}

		public void tab_PhotosGUI() {
			Scene activeScene = SceneManager.GetActiveScene();
			PhotoFrame[] photoFrames = Utils.LoadedScenes_FindComponentsOfType<PhotoFrame>(true).ToArray();

			GUILayout.Label($"{photoFrames.Length} Photos");

			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUI.skin.scrollView);
			scrollPosition.x = 0;
			float screenSize = Screen.width - EditorGUIUtility.singleLineHeight * 2f;
			int count = (int)Math.Max(Math.Floor(screenSize / 150), 1);
			float size = (screenSize - GUI.skin.button.margin.left * (count - 1)) / count;
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(EditorGUIUtility.singleLineHeight);
			GUILayout.BeginVertical();
			for(int i = 0; i < photoFrames.Length;) {
				GUILayout.BeginHorizontal();
				float imageHeight = 0;
				for(int tempI = i, a = 0; a < count && tempI < photoFrames.Length; a++, tempI++) {
					if(photoFrames[tempI].photo == null) continue;
					float aspectRatio = Utils.ConvertRatio(photoFrames[tempI].photoUseSize);
					imageHeight = Mathf.Max(imageHeight, (size - EditorGUIUtility.singleLineHeight * 0.5f) / Math.Max(aspectRatio, 1));
				}
				for(int a = 0; a < count && i < photoFrames.Length; a++, i++) {
					Rect rect = GUILayoutUtility.GetRect(size, imageHeight + EditorGUIUtility.singleLineHeight * 2.75f, GUI.skin.button, GUILayout.ExpandWidth(false));
					if(GUI.Button(rect, "")) {
						Selection.activeGameObject = photoFrames[i].gameObject;
						EditorGUIUtility.PingObject(Selection.activeGameObject);
					}
					rect.x += EditorGUIUtility.singleLineHeight * 0.25f;
					rect.y += EditorGUIUtility.singleLineHeight * 0.25f;
					rect.width -= EditorGUIUtility.singleLineHeight * 0.5f;
					rect.height -= EditorGUIUtility.singleLineHeight * 0.5f;
					float rectHeight = rect.height;
					rect.height = EditorGUIUtility.singleLineHeight;
					GUI.Label(rect, new GUIContent(photoFrames[i].name));
					rect.y += EditorGUIUtility.singleLineHeight;
					GUI.Label(rect, new GUIContent(photoFrames[i].photo == null ? "--" : Path.GetFileName(AssetDatabase.GetAssetPath(photoFrames[i].photo))));
					rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
					rect.height = imageHeight;
					if(photoFrames[i].photo != null) GUI.Box(rect, photoFrames[i].photo, GUIStyle.none);
				}
				GUILayout.EndHorizontal();
			}
			GUILayout.EndVertical();
			GUILayout.Space(EditorGUIUtility.singleLineHeight);
			GUILayout.EndHorizontal();
			EditorGUILayout.EndScrollView();
		}

		public void tab_BakedTexturesGUI() {
			var settings = SceneSettings.active;
			int pfI = 0;
			GUIStyle style = new GUIStyle(GUI.skin.button);
			style.padding.top /= 2;
			style.padding.right /= 2;
			style.padding.bottom /= 2;
			style.padding.left /= 2;

			if(settings.textures.Length > 0) {
				GUILayout.Label($"{settings.textures.Length} Textures");
				scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUI.skin.scrollView);
				scrollPosition.x = 0;
				for(int i = 0; i < settings.textures.Length; i++) {
					if(settings.textures[i] == null) continue;
					GUILayout.BeginHorizontal();
					GUILayout.Space(EditorGUIUtility.singleLineHeight);
					float size = Mathf.Min(500, Screen.width * 0.5f);
					GUILayout.Box(settings.textures[i], GUIStyle.none, GUILayout.Width(size), GUILayout.Height(size));
					GUILayout.Space(EditorGUIUtility.singleLineHeight);
					GUILayout.BeginVertical();
					GUILayout.Label($"{settings.textures[i].width}px x {settings.textures[i].height}px");
					GUILayout.Label($"{settings.pfCounts[i]} photos");
					GUILayout.Space(EditorGUIUtility.singleLineHeight);
					EditorGUILayout.BeginHorizontal();
					GUILayout.FlexibleSpace();
					EditorGUILayout.EndHorizontal();
					float width = EditorGUIUtility.currentViewWidth - size - EditorGUIUtility.singleLineHeight * 3;
					Rect rect = GUILayoutUtility.GetRect(0, width * Mathf.Ceil(settings.pfCounts[i] / 3.0f) / 3.0f, GUILayout.ExpandWidth(true));
					int selection = GUI.SelectionGrid(rect, -1, settings.photoFrames.Skip(pfI).Take(settings.pfCounts[i]).Where(pf => pf?.photo).Select(pf => pf.photo).ToArray(), 3, style);
					if(selection != -1) {
						Selection.activeGameObject = settings.photoFrames[pfI + selection].gameObject;
						EditorGUIUtility.PingObject(Selection.activeGameObject);
					}
					GUILayout.EndVertical();
					GUILayout.EndHorizontal();
					GUILayout.Space(EditorGUIUtility.singleLineHeight);

					pfI += settings.pfCounts[i];
				}
				EditorGUILayout.EndScrollView();
			}
			else {
				GUILayout.Label("No Textures");
			}
		}

		[NonSerialized] public GUIStyle selectableLabelStyle;
		public void sizedSelectableLabel(string text) {
			if(selectableLabelStyle == null) {
				selectableLabelStyle = new GUIStyle(EditorStyles.label);
				selectableLabelStyle.focused.textColor = selectableLabelStyle.normal.textColor;
			}

			var content = new GUIContent(text);
			EditorGUI.SelectableLabel(GUILayoutUtility.GetRect(content, GUI.skin.label), text, selectableLabelStyle);
		}

		public void tab_DebugGUI() {
			bakeAndDeleteButtonsGUI();
			if(GUILayout.Button("Show scene settings")) {
				Selection.activeGameObject = SceneSettings.active.gameObject;
				EditorWindow.FocusWindowIfItsOpen(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow"));
			}
			debugScrollPosition = EditorGUILayout.BeginScrollView(debugScrollPosition);
			if(TextureBaker.d_cycleCount == 0 && TextureBaker.d_log.Count == 0) GUILayout.Label("No Debug Information Saved");
			else {
				GUIStyle horizontalLine;
				horizontalLine = new GUIStyle();
				horizontalLine.normal.background = EditorGUIUtility.whiteTexture;
				horizontalLine.margin = new RectOffset(0, 0, 7, 7);
				horizontalLine.fixedHeight = 1;

				var saveFont = GUI.skin.font;
				Font font = (Font)EditorGUIUtility.Load("RobotoMono-Regular.ttf");
				if(font) GUI.skin.font = font;

				sizedSelectableLabel($"Cycles: {TextureBaker.d_cycleCount}");
				GUILayout.Space(EditorGUIUtility.singleLineHeight);
				for(int i = 0; i < TextureBaker.d_log.Count; i++) {
					GUI.color = new Color(0.5f, 0.5f, 0.5f, 1);
					if(i != 0) GUILayout.Box(GUIContent.none, horizontalLine);
					GUI.color = Color.white;
					sizedSelectableLabel(TextureBaker.d_log[i]);
				}

				GUI.skin.font = saveFont;
			}
			EditorGUILayout.EndScrollView();
		}

		public void OnGUI() {
			CustomEditorGUI.lockValue = false;
			SceneSettings.AssureActiveNotMissing();

			EditorGUIUtility.labelWidth = Screen.width * 0.35f;
			SceneSettings.active.serializedObject.Update();

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if(UtilsGUI.OptionsDropDown(FocusType.Passive)) {
				GenericMenu menu = new GenericMenu();
				menu.AddItem(new GUIContent("Reset"), false, () => {
					Undo.RecordObject(SceneSettings.active, "Reset");
					SceneSettings.active.Reset();
				});
				menu.AddItem(new GUIContent("Right Aligned Fields"), EditorSettings.rightAlignedFields, () => EditorSettings.rightAlignedFields ^= true);
				menu.AddItem(new GUIContent("Debug"), EditorSettings.debug, () => EditorSettings.debug ^= true);
				menu.ShowAsContext();
			}
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			var tabs = new List<string> { " Settings ", " Photos ", " Baked Textures " };
			if(EditorSettings.debug) tabs.Add(" Debug ");
			tabSelected = GUILayout.Toolbar(tabSelected, tabs.ToArray(), GUILayout.Height(EditorGUIUtility.singleLineHeight * 1.25f));
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.Space(EditorGUIUtility.singleLineHeight);

			if(tabSelected == 0) tab_SceneSettingsGUI();
			if(tabSelected == 1) tab_PhotosGUI();
			if(tabSelected == 2) tab_BakedTexturesGUI();
			if(tabSelected == 3) tab_DebugGUI();

			SceneSettings.active.serializedObject.ApplyModifiedProperties();

			if(Event.current.type == EventType.MouseDown
			&& 0 < Event.current.mousePosition.x && Event.current.mousePosition.x < Screen.width
			&& 0 < Event.current.mousePosition.y && Event.current.mousePosition.y < Screen.height) {
				Event.current.Use();
				GUI.FocusControl("");
			}
		}
	}
}
