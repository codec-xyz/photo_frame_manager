using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static UnityEngine.Object;
using Scene = UnityEngine.SceneManagement.Scene;

namespace codec.PhotoFrame {
	[InitializeOnLoad]
	public class HideInEditor {
		const HideFlags flags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
		const bool SavingMode = false;
		const bool EditingMode = true;

		static HideInEditor() {
			UpdateHiding(EditingMode);

			EditorSceneManager.sceneSaving += (scene, _) => UpdateHiding(SavingMode, scene);
			EditorSceneManager.sceneSaved += (scene) => UpdateHiding(EditingMode, scene);
			EditorSceneManager.sceneOpened += (scene, mode) => {
				if(mode != OpenSceneMode.AdditiveWithoutLoading) UpdateHiding(EditingMode, scene);
			};
			AssemblyReloadEvents.beforeAssemblyReload += () => UpdateHiding(SavingMode);
			AssemblyReloadEvents.afterAssemblyReload += () => UpdateHiding(EditingMode);
		}

		public static void SetHidingFlags<T>(bool setHidden, Scene? scene) where T : Component {
			IEnumerable<T> objs = FindObjectsOfType<T>(true);
			if(scene != null) objs = objs.Where(o => o.gameObject.scene == scene);

			if(setHidden) foreach(var o in objs) o.gameObject.hideFlags |= flags;
			else foreach(var o in objs) o.gameObject.hideFlags &= ~flags;
		}

		public static void UpdateHiding(bool editingMode, Scene? scene = null) {
			SetHidingFlags<SceneSettings>(editingMode, scene);
		}
	}
}