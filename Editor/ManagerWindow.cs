using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace codec.PhotoFrame {
	public class ManagerWindow : EditorWindow {
		public const string badData = "Misformatted Data";
		public const string debugEditorPref = "wtf.codec.photo-frame-manager.debug";

		public int tabSelected;
		public Vector2 scrollPosition;
		public Vector2 debugScroll;

		[MenuItem("Photo Frames/Window")]
		[MenuItem("Window/Photo Frames")]
		public static void OpenWindow() {
			ManagerWindow wnd = GetWindow<ManagerWindow>();
			wnd.titleContent = new GUIContent("Photo Frames");
			wnd.minSize = new Vector2(250, 250);
		}

		[MenuItem("Assets/Add Images As Photo Frames")]
		public static void AddImagesAsPhotoFrames() {
			PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

			foreach(var obj in Selection.objects) {
				if(!(obj is Texture2D)) continue;

				PhotoFrame photoFrame = AddPhotoFrame().GetComponent<PhotoFrame>();

				Undo.RegisterCreatedObjectUndo(photoFrame.gameObject, "Add Images As Photo Frames");

				photoFrame.photo = obj as Texture2D;

				if(prefabStage != null) {
					GameObjectUtility.SetParentAndAlign(photoFrame.gameObject, prefabStage.prefabContentsRoot);
				}

				photoFrame.updateEditorPreview();
			}
		}

		[MenuItem("Assets/Add Images As Photo Frames", true)]
		public static bool AddImagesAsPhotoFramesValidate() {
			foreach(var obj in Selection.objects) {
				if(obj is Texture2D) return true;
			}
			return false;
		}

		public static GameObject AddPhotoFrame() {
			GameObject photoObject = new GameObject("Photo Frame", typeof(PhotoFrame));
			var iconContent = EditorGUIUtility.IconContent("sv_label_0");
			var setIcon = typeof(EditorGUIUtility).GetMethod("SetIconForObject", BindingFlags.Static | BindingFlags.NonPublic);
			setIcon.Invoke(null, new object[] { photoObject, (Texture2D)iconContent.image });
			return photoObject;
		}

		[MenuItem("GameObject/Photo Frame", false, 10)]
		public static void AddPhoto(MenuCommand menuCommand) {
			GameObject photoObject = AddPhotoFrame();

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

		[MenuItem("Photo Frames/Bake Photo Frames")]
		public static void BakeTextures() {
			var settings = SceneSettings.active;
			if(settings == null) {
				Scene curScene = SceneManager.GetActiveScene();
				SceneSettings.ActiveSceneChanged(curScene, curScene);
				settings = SceneSettings.active;
			}

			EditorUtility.SetDirty(settings);

			try {
				AssetDatabase.StartAssetEditing();

				PhotoFrame[] photoFrames = FindObjectsOfType<PhotoFrame>().Where(pf => pf.photo).ToArray();
				foreach(var pf in photoFrames) pf.unlock();
				var lostObjs = FindObjectsOfType<MarkTypeBaked>();
				foreach(var lostObj in lostObjs) DestroyImmediate(lostObj);

				PhotoFrameBaker.Bake(photoFrames, settings, EditorPrefs.GetBool(debugEditorPref, false), (string[] paths) => {
					AssetDatabase.StopAssetEditing();
					foreach(var path in paths) AssetDatabase.ImportAsset(path);
					AssetDatabase.StartAssetEditing();
				});
			}
			catch(Exception e) {
				Debug.LogException(e);
			}
			finally {
				AssetDatabase.StopAssetEditing();
				EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
			}
		}

		[MenuItem("Photo Frames/Delete Bake")]
		public static void DeleteTextures() {
			var settings = SceneSettings.active;
			if(settings == null) {
				Scene curScene = SceneManager.GetActiveScene();
				SceneSettings.ActiveSceneChanged(curScene, curScene);
				settings = SceneSettings.active;
			}

			EditorUtility.SetDirty(settings);

			try {
				AssetDatabase.StartAssetEditing();

				foreach(var pf in FindObjectsOfType<PhotoFrame>()) {
					pf.unlock();
					pf.updateEditorPreview();
				}

				settings.DeleteTexturesAndMaterials();

				var lostObjs = FindObjectsOfType<MarkTypeBaked>();
				foreach(var lostObj in lostObjs) DestroyImmediate(lostObj);
			}
			catch(Exception e) {
				Debug.LogException(e);
			}
			finally {
				AssetDatabase.StopAssetEditing();
				EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
			}
		}

		public void bakeAndDeleteButtonsGUI() {
			Rect rect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight * 1.25f, GUILayout.ExpandWidth(true));
			rect.width -= EditorGUIUtility.singleLineHeight * 0.5f;
			rect.x += EditorGUIUtility.singleLineHeight * 0.25f;
			rect.width /= 2;
			if(GUI.Button(rect, "Bake Photo Frames")) BakeTextures();
			rect.x += rect.width;
			if(GUI.Button(rect, "Delete Bake")) DeleteTextures();
		}

		public void tab_SceneSettingsGUI() {
			scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
			scrollPosition.x = 0;

			var textureSizes = (new HashSet<int> { 64, 128, 256, 512, 1024, 2048, 4096, 8192, SceneSettings.active.textureSize }).ToArray();
			System.Array.Sort(textureSizes);

			EditorGUILayout.IntPopup(SceneSettings.active.s_textureSize, textureSizes.Select(n => new GUIContent(n.ToString())).ToArray(), textureSizes, new GUIContent("Baked Texture Size", "Target size of the baked textures. Some texture might be smaller to reduce size"));
			EditorGUILayout.IntSlider(SceneSettings.active.s_margin, 0, (int)(SceneSettings.active.textureSize * 0.125f), new GUIContent("Photo Margin", "Extra colored area around photos to prevent texture bleeding; measured in pixels"));

			CustomEditorGUILayout.SliderAllOptions(new GUIContent("Texture Fit",
				"Sorting prioritization (spacial sorting for better mipmap streaming vs fewer textures)\n"
				+ "0 - sorted for better mipmap streaming\n1 - sorted for fewer textures"), SceneSettings.active.s_textureFit, 0, 1, 0, float.MaxValue);

			EditorGUILayout.Slider(SceneSettings.active.s_skylineMaxSpread, 0, 1, new GUIContent("Skyline Max Spread", "Parameter for the packing algorithm"));

			GUILayout.EndScrollView();
			GUILayout.Space(EditorGUIUtility.singleLineHeight);
			bakeAndDeleteButtonsGUI();
			GUILayout.FlexibleSpace();
			GUILayout.Space(EditorGUIUtility.singleLineHeight);
		}

		public void tab_PhotosGUI() {
			PhotoFrame[] photoFrames = FindObjectsOfType<PhotoFrame>();

			GUILayout.Label($"{photoFrames.Length} Photos");

			scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
			scrollPosition.x = 0;
			Rect rect = GUILayoutUtility.GetRect(0, EditorGUIUtility.currentViewWidth * Mathf.Ceil(photoFrames.Length / 3.0f) / 3.0f, GUILayout.ExpandWidth(true));
			int selection = GUI.SelectionGrid(rect, -1, photoFrames.Select(pf => pf.photo).ToArray(), 3);
			if(selection != -1) {
				Selection.activeGameObject = photoFrames[selection].gameObject;
				EditorGUIUtility.PingObject(Selection.activeGameObject);
			}
			GUILayout.EndScrollView();
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
				scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
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
					int selection = GUI.SelectionGrid(rect, -1, settings.photoFrames.Skip(pfI).Take(settings.pfCounts[i]).Select(pf => pf.photo).ToArray(), 3, style);
					if(selection != -1) {
						Selection.activeGameObject = settings.photoFrames[pfI + selection].gameObject;
						EditorGUIUtility.PingObject(Selection.activeGameObject);
					}
					GUILayout.EndVertical();
					GUILayout.EndHorizontal();
					GUILayout.Space(EditorGUIUtility.singleLineHeight);

					pfI += settings.pfCounts[i];
				}
				GUILayout.EndScrollView();
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
			debugScroll = GUILayout.BeginScrollView(debugScroll);
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
			GUILayout.EndScrollView();
		}

		public void OnGUI() {
			if(SceneSettings.active == null) {
				Scene curScene = SceneManager.GetActiveScene();
				SceneSettings.ActiveSceneChanged(curScene, curScene);
			}

			EditorGUIUtility.labelWidth = 200;
			SceneSettings.active.serializedObject.Update();

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUIStyle iconButtonStyle = GUI.skin.FindStyle("IconButton") ?? EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("IconButton");
			GUIContent content = new GUIContent(EditorGUIUtility.Load("icons/d__Popup.png") as Texture2D);
			if(EditorGUILayout.DropdownButton(content, FocusType.Passive, iconButtonStyle)) {
				GenericMenu menu = new GenericMenu();
				menu.AddItem(new GUIContent("Reset"), false, () => {
					SceneSettings.active.serializedObject.Update();
					SceneSettings.active.s_textureSize.intValue = 4096;
					SceneSettings.active.s_margin.intValue = 32;
					SceneSettings.active.s_textureFit.floatValue = 0.15f;
					SceneSettings.active.s_skylineMaxSpread.floatValue = 0.25f;
					SceneSettings.active.serializedObject.ApplyModifiedProperties();
				});
				menu.AddItem(new GUIContent("Debug"), EditorPrefs.GetBool(debugEditorPref, false), () => {
					bool value = EditorPrefs.GetBool(debugEditorPref, false);
					if(value) EditorPrefs.DeleteKey(debugEditorPref);
					else EditorPrefs.SetBool(debugEditorPref, true);
				});
				menu.ShowAsContext();
			}
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			var tabs = new List<string> { " Settings ", " Photos ", " Baked Textures " };
			if(EditorPrefs.GetBool(debugEditorPref, false)) tabs.Add(" Debug ");
			tabSelected = GUILayout.Toolbar(tabSelected, tabs.ToArray(), GUILayout.Height(EditorGUIUtility.singleLineHeight * 1.25f));
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.Space(EditorGUIUtility.singleLineHeight);

			if(tabSelected == 0) tab_SceneSettingsGUI();
			if(tabSelected == 1) tab_PhotosGUI();
			if(tabSelected == 2) tab_BakedTexturesGUI();
			if(tabSelected == 3) tab_DebugGUI();

			SceneSettings.active.serializedObject.ApplyModifiedProperties();
		}
	}
}
