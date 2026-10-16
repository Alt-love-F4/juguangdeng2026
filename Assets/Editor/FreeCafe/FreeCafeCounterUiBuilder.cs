using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Converts the supplied counter PSD layers into screen-filling Canvas artwork.</summary>
    public static class FreeCafeCounterUiBuilder
    {
        private const string PrefabPath = "Assets/Art/Gameplay/咖啡馆/台面ui.prefab";
        // The PSD was exported at 4096 x 2304, with 100 pixels per world unit.
        private static readonly Vector2 ArtworkSize = new Vector2(40.96f, 23.04f);

        [MenuItem("Tools/Free Cafe/Fix Current Counter UI")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before fixing the counter UI.");

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var instance = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "台面ui" && item.GetComponentInParent<Canvas>() != null);
            if (instance == null)
                throw new InvalidOperationException("The current scene has no 台面ui under a Canvas.");

            string backup = "output/counter-ui-backup/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            if (File.Exists(scene.path)) File.Copy(scene.path, backup + "/CoffeeShop.unity");
            File.Copy(PrefabPath, backup + "/台面ui.prefab");

            var parent = instance.parent;
            int sibling = instance.GetSiblingIndex();
            bool active = instance.gameObject.activeSelf;
            // Preserve scene-authored children separately while the imported Transform is replaced.
            var additions = instance.Cast<Transform>()
                .Where(child => PrefabUtility.IsAddedGameObjectOverride(child.gameObject)).ToArray();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Fix counter UI layout");
            foreach (var child in additions) Undo.SetTransformParent(child, parent, "Preserve counter UI child");
            Undo.DestroyObjectImmediate(instance.gameObject);

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            var uiRoot = new GameObject("台面ui", typeof(RectTransform));
            try
            {
                Stretch((RectTransform)uiRoot.transform);
                uiRoot.layer = LayerMask.NameToLayer("UI");
                var renderers = contents.GetComponentsInChildren<SpriteRenderer>(true)
                    .OrderBy(renderer => SortingLayer.GetLayerValueFromID(renderer.sortingLayerID))
                    .ThenBy(renderer => renderer.sortingOrder)
                    .ThenByDescending(renderer => renderer.transform.localPosition.z).ToArray();
                if (renderers.Length == 0)
                {
                    UnityEngine.Object.DestroyImmediate(uiRoot);
                    uiRoot = UnityEngine.Object.Instantiate(contents);
                    uiRoot.name = "台面ui";
                    Stretch((RectTransform)uiRoot.transform);
                }
                foreach (var renderer in renderers)
                {
                    var sprite = renderer.sprite;
                    if (sprite == null) continue;
                    var position = (Vector2)renderer.transform.localPosition;
                    var size = sprite.rect.size / sprite.pixelsPerUnit;
                    var pivot = sprite.pivot / sprite.rect.size;
                    var minimum = position - Vector2.Scale(pivot, size);
                    var layer = new GameObject(renderer.name, typeof(RectTransform), typeof(Image));
                    layer.transform.SetParent(uiRoot.transform, false);
                    var rect = (RectTransform)layer.transform;
                    rect.localScale = new Vector3(renderer.flipX ? -1 : 1, renderer.flipY ? -1 : 1, 1);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchorMin = minimum / ArtworkSize;
                    rect.anchorMax = (minimum + size) / ArtworkSize;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    rect.anchoredPosition3D = Vector3.zero;
                    layer.layer = LayerMask.NameToLayer("UI");
                    layer.SetActive(renderer.gameObject.activeSelf);
                    var image = layer.GetComponent<Image>();
                    image.sprite = sprite;
                    image.color = renderer.color;
                    image.enabled = renderer.enabled;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = false;
                    image.raycastTarget = false;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            try { PrefabUtility.SaveAsPrefabAsset(uiRoot, PrefabPath); }
            finally { UnityEngine.Object.DestroyImmediate(uiRoot); }

            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), parent);
            Undo.RegisterCreatedObjectUndo(replacement, "Create Canvas counter UI");
            replacement.transform.SetSiblingIndex(sibling);
            replacement.SetActive(active);
            Stretch((RectTransform)replacement.transform);
            foreach (var child in additions)
            {
                Undo.SetTransformParent(child, replacement.transform, "Restore counter UI child");
                var image = child.GetComponent<Image>();
                // An unassigned Image renders a white square over the supplied art.
                if (image != null && image.sprite == null && child.name == "Image")
                    image.enabled = false;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = replacement;
            Canvas.ForceUpdateCanvases();
            Debug.Log("Counter UI converted to Canvas Images and stretched to the current screen. Backup: " + backup);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.anchoredPosition3D = Vector3.zero;
        }
    }
}
