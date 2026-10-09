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
    /// <summary>将已有 PSD 图层转换为正式主菜单，保留源美术，不引入第二套菜单流程。</summary>
    public static class MainMenuArtworkBuilder
    {
        public const string SourcePath = "Assets/Art/PSD/主界面.prefab";
        public const string PanelPath = "Assets/Resources/Art/UIPrefab/MainMenuPanel.prefab";
        private const string RootPath = "Assets/Resources/Art/UIPrefab/UIRoot.prefab";
        private const string ConfigPath = "Assets/Settings/GameFlowConfig.asset";

        [MenuItem("Tools/Game Flow/Rebuild Main Menu From PSD Artwork")]
        public static void RebuildPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == default) throw new InvalidOperationException("Missing main menu PSD prefab: " + SourcePath);
            SpriteRenderer[] layers = source.GetComponentsInChildren<SpriteRenderer>(true);
            Array.Sort(layers, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            if (layers.Length == 0 || layers[0].sprite == default)
                throw new InvalidOperationException("Main menu PSD has no background sprite.");
            Bounds canvas = LayerBounds(layers[0]);
            var root = new GameObject("MainMenuPanel", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                Stretch((RectTransform)root.transform);
                MainMenuPanel panel = root.AddComponent<MainMenuPanel>();
                var artwork = new GameObject("Artwork", typeof(RectTransform), typeof(AspectRatioFitter));
                artwork.transform.SetParent(root.transform, false);
                Stretch((RectTransform)artwork.transform);
                var fitter = artwork.GetComponent<AspectRatioFitter>();
                // 先设模式：Editor 下 None 模式会把无父 Canvas 的 0/0 尺寸写回 aspectRatio。
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = canvas.size.x / canvas.size.y;
                // PSD 的按钮底板与文字分别导出；合并到同一个可交互区域，不改原来的像素布局。
                string[] labels = { "继续游戏", "开始游戏", "设置", "退出游戏" };
                string[] backgrounds = { "图层 1", "图层 1 副本 2", "图层 1 副本 3", "图层 1 副本" };
                var buttons = new Button[labels.Length];
                for (int i = 0; i < 3; i++) AddLayer(layers[i], artwork.transform, canvas, i == 0);
                for (int i = 0; i < labels.Length; i++)
                {
                    SpriteRenderer background = FindLayer(layers, backgrounds[i]);
                    Bounds row = LayerBounds(background);
                    var go = new GameObject(labels[i], typeof(RectTransform), typeof(Image), typeof(Button));
                    go.transform.SetParent(artwork.transform, false);
                    Place((RectTransform)go.transform, row, canvas);
                    var hitArea = go.GetComponent<Image>();
                    hitArea.color = Color.clear;
                    Button button = go.GetComponent<Button>();
                    Image surface = AddLayer(background, go.transform, row, false);
                    if (i == 0) AddLayer(FindLayer(layers, "图层 1 副本 5"), go.transform, row, false);
                    AddLayer(FindLayer(layers, labels[i]), go.transform, row, false);
                    button.targetGraphic = surface;
                    ColorBlock colors = button.colors;
                    colors.highlightedColor = new Color(1f, .94f, .84f, 1f);
                    colors.pressedColor = new Color(.78f, .72f, .67f, 1f);
                    colors.selectedColor = colors.highlightedColor;
                    colors.fadeDuration = .08f;
                    button.colors = colors;
                    go.AddComponent<ButtonClickFeedback>();
                    buttons[i] = button;
                }
                for (int i = 0; i < buttons.Length; i++)
                {
                    buttons[i].navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length],
                        selectOnDown = buttons[(i + 1) % buttons.Length]
                    };
                }
                var fields = new SerializedObject(panel);
                fields.FindProperty("savesButton").objectReferenceValue = buttons[0];
                fields.FindProperty("startButton").objectReferenceValue = buttons[1];
                fields.FindProperty("settingsButton").objectReferenceValue = buttons[2];
                fields.FindProperty("quitButton").objectReferenceValue = buttons[3];
                // 全屏美术只淡入淡出，不滑动/缩放整个背景以免露出底层场景。
                fields.FindProperty("showOffset").floatValue = 0f;
                fields.FindProperty("hideOffset").floatValue = 0f;
                fields.FindProperty("showStartScale").floatValue = 1f;
                fields.FindProperty("hideEndScale").floatValue = 1f;
                fields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [MenuItem("Tools/Game Flow/Install Main Menu Artwork In Current Scene")]
        public static void InstallInCurrentScene()
        {
            RebuildPrefab();
            Scene scene = SceneManager.GetActiveScene();
            GameObject uiRoot = null;
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == "UIRoot") uiRoot = go;
            if (uiRoot == default) throw new InvalidOperationException("Current scene has no UIRoot.");
            Transform common = uiRoot.transform.Find("Level_Common");
            if (common == default) throw new InvalidOperationException("UIRoot has no Level_Common.");
            // 保存过的 UIKit 调试实例不应该随 Root 模板一起生成。保留 Canvas/层级/Storage 配置。
            GameObject rootAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RootPath);
            if (rootAsset != default)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(RootPath);
                try
                {
                    foreach (MainMenuPanel old in contents.GetComponentsInChildren<MainMenuPanel>(true))
                        Object.DestroyImmediate(old.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(contents, RootPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            for (int i = common.childCount - 1; i >= 0; i--)
            {
                GameObject child = common.GetChild(i).gameObject;
                if (child.GetComponent<MainMenuPanel>() != default || child.name == "主界面")
                    Undo.DestroyObjectImmediate(child);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            var preview = (GameObject)PrefabUtility.InstantiatePrefab(prefab, common);
            Undo.RegisterCreatedObjectUndo(preview, "Install main menu artwork");
            preview.name = "主界面";
            PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
            GameFlowBootstrap bootstrap = null;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                bootstrap = go.GetComponentInChildren<GameFlowBootstrap>(true);
                if (bootstrap != default) break;
            }
            if (bootstrap == default)
            {
                var go = new GameObject("GameFlowBootstrap");
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Install main menu flow");
                bootstrap = Undo.AddComponent<GameFlowBootstrap>(go);
            }
            Undo.RecordObject(bootstrap, "Bind scene main menu preview");
            var flowFields = new SerializedObject(bootstrap);
            flowFields.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameFlowConfig>(ConfigPath);
            flowFields.FindProperty("showMainMenuOnStart").boolValue = true;
            flowFields.FindProperty("mainMenuScenePreview").objectReferenceValue = preview.GetComponent<MainMenuPanel>();
            flowFields.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = preview;
        }

        private static SpriteRenderer FindLayer(SpriteRenderer[] layers, string name)
        {
            foreach (SpriteRenderer layer in layers) if (layer.name == name && layer.sprite != default) return layer;
            throw new InvalidOperationException("Missing PSD layer: " + name);
        }

        private static Image AddLayer(SpriteRenderer source, Transform parent, Bounds parentBounds, bool raycast)
        {
            if (source.sprite == default) throw new InvalidOperationException("Missing sprite: " + source.name);
            var go = new GameObject(source.name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, LayerBounds(source), parentBounds);
            var image = go.GetComponent<Image>();
            image.sprite = source.sprite;
            image.color = source.color;
            image.raycastTarget = raycast;
            return image;
        }

        // Prefab 资产未进入场景时 Renderer.bounds 可能尚未计算，不能用它作为版面尺寸。
        private static Bounds LayerBounds(SpriteRenderer layer)
        {
            Bounds local = layer.sprite.bounds;
            return new Bounds(layer.transform.TransformPoint(local.center),
                Vector3.Scale(local.size, layer.transform.lossyScale));
        }

        private static void Place(RectTransform rect, Bounds bounds, Bounds parent)
        {
            rect.anchorMin = new Vector2((bounds.min.x - parent.min.x) / parent.size.x, (bounds.min.y - parent.min.y) / parent.size.y);
            rect.anchorMax = new Vector2((bounds.max.x - parent.min.x) / parent.size.x, (bounds.max.y - parent.min.y) / parent.size.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
