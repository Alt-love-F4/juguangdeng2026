using System;
using Spotlight.GameFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    public static class GravityLiquidImageEditor
    {
        public const string PrefabPath = "Assets/Prefabs/UI/GravityLiquidImage.prefab";

        [MenuItem("Tools/UI Effects/Create or Update Gravity Liquid Prefab")]
        public static void BuildPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before building assets.");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI")) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            GameObject root = new GameObject("GravityLiquidImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.SetActive(false);
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 360f);
                Image image = root.GetComponent<Image>();
                image.raycastTarget = false;
                image.type = Image.Type.Simple;
                image.color = Color.white;
                root.AddComponent<GravityLiquidImage>();
                // Save active by default. This enables its private material instance only on this temporary prefab root.
                root.SetActive(true);
                image.material = null; // Never serialize the transient, per-instance material into the prefab.
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Gravity liquid prefab saved: " + PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [MenuItem("GameObject/UI/Gravity Liquid Image", false, 2050)]
        public static void AddToSelectedCanvas(MenuCommand command)
        {
            Transform parent = Selection.activeTransform;
            if (parent == null || parent.GetComponentInParent<Canvas>() == null)
            {
                Debug.LogWarning("Select an existing Canvas or one of its children first. No new Canvas/input system is created.");
                return;
            }
            Add(parent);
        }

        [MenuItem("Tools/UI Effects/Preview Gravity Liquid in Shared Debug Scene")]
        public static void PreviewShared()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            Scene scene = SceneManager.GetSceneByPath(PresentationDebugSceneController.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(PresentationDebugSceneController.ScenePath);
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "DebugRoomCanvas") continue;
                Transform existing = root.transform.Find("GravityLiquidImage");
                if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
                Add(root.transform);
                return;
            }
            throw new InvalidOperationException("Shared scene is missing DebugRoomCanvas. Use the existing scene builder to repair it.");
        }

        private static void Add(Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { BuildPrefab(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Create Gravity Liquid Image");
            instance.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            Selection.activeGameObject = instance;
        }
    }
}
