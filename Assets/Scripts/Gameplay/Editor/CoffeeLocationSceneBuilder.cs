#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Spotlight.Gameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.Gameplay.EditorTools
{
    /// <summary>把咖啡店场景整理为楼层/区域结构，并生成地点切换 UI。</summary>
    public static class CoffeeLocationSceneBuilder
    {
        private const string FontPath = "Assets/Resources/Fonts/SimHei SDF.asset";
        private const string BackgroundDir = "Assets/Art/Background";
        private const string MainScene = "Assets/Scenes/CoffeeShop.unity";
        private const string SandboxScene = "Assets/Scenes/CoffeeUIEntitySandbox.unity";

        [MenuItem("Tools/Spotlight/Capture Cafe Preview")]
        public static void CapturePreview()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("请在 Play Mode 中截取预览");
            var controller = Object.FindFirstObjectByType<CoffeeLocationController>();
            if (controller == null) throw new InvalidOperationException("当前不是咖啡店区域场景");
            Directory.CreateDirectory(".unity/capture");
            string path = $".unity/capture/location-{controller.CurrentLocation}.png";
            // CaptureScreenshot captures the Game display including ScreenSpaceOverlay canvases.
            foreach (var graphic in Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None)) graphic.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[CoffeeLocation] 帧末保存完整 UI 预览：" + Path.GetFullPath(path));
        }

        [MenuItem("Tools/Spotlight/Build Cafe Locations")]
        public static void BuildActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出 Play Mode");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MainScene && scene.path != SandboxScene)
                throw new InvalidOperationException("请打开 CoffeeShop 或 CoffeeUIEntitySandbox 场景");
            Build(scene);
        }

        public static void Build(Scene scene)
        {
            // Preflight before changing the hierarchy; original sketches are never rewritten.
            foreach (var file in new[] { "World/一楼背景.png", "出餐台背景.png", "World/二楼背景.png", "World/地下室背景.png" })
                if (LoadSprite($"{BackgroundDir}/{file}") == null) throw new InvalidOperationException("背景缺失：" + file);
            RemoveGenerated(scene, "CoffeeLocationRoot", "LocationNavigationUI", "LetterboxUI", "BuildingRoot");
            var legacy = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "CoffeeWorldRoot");
            if (legacy != null) legacy.SetActive(false);

            var building = NewRoot(scene, "BuildingRoot");
            var firstFloor = Child(building.transform, "Floor_First");
            var cafe = Child(firstFloor.transform, "CafeArea");
            var counter = Child(firstFloor.transform, "ServingCounterArea");
            var second = Child(building.transform, "Floor_Second");
            var basement = Child(building.transform, "Floor_Basement");

            CreateBackground(cafe.transform, "FirstFloorBackground", "World/一楼背景.png");
            CreateBackground(counter.transform, "ServingCounterBackground", "出餐台背景.png");
            CreateBackground(second.transform, "SecondFloorBackground", "World/二楼背景.png");
            CreateBackground(basement.transform, "BasementBackground", "World/地下室背景.png");

            var runtimeComponent = Object.FindObjectsByType<CoffeeWorldRuntime>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => x.gameObject.scene == scene);
            var runtime = runtimeComponent == null ? null : runtimeComponent.gameObject;
            GameObject cafeEntities = null;
            GameObject counterEntities = null;
            if (runtime != null)
            {
                // 分组在 CoffeeWorkspace prefab 内完成，这里只移动 prefab 根节点，保留 prefab 引用。
                var interactionRuntime = runtime.transform.parent == null
                    ? NewRoot(scene, "InteractionRuntime")
                    : runtime.transform.parent.gameObject;
                runtime.transform.SetParent(interactionRuntime.transform, false);
                cafeEntities = runtime.transform.Find("CafeEntities")?.gameObject;
                counterEntities = runtime.transform.Find("ServingCounterEntities")?.gameObject;
                if (runtimeComponent != null)
                    runtimeComponent.carryableRoot = counterEntities == null ? runtime.transform : counterEntities.transform.Find("CarryableItems");
            }

            var locationRoot = NewRoot(scene, "CoffeeLocationRoot");
            var controller = locationRoot.AddComponent<CoffeeLocationController>();
            controller.interactionWorld = runtimeComponent;
            if (runtimeComponent != null) runtimeComponent.locationController = controller;
            var portal = new GameObject("ServingCounterPortal", typeof(BoxCollider2D), typeof(CoffeeLocationPortal));
            portal.transform.SetParent(cafe.transform, false);
            portal.transform.localPosition = new Vector3(8.0f, -0.25f, 0);
            var collider = portal.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(2.3f, 3.2f);
            var portalComponent = portal.GetComponent<CoffeeLocationPortal>();
            portalComponent.controller = controller;
            portalComponent.targetLocation = CoffeeLocationId.FirstFloorServingCounter;
            var marker = new GameObject("PortalHighlight", typeof(SpriteRenderer)); marker.transform.SetParent(portal.transform, false);
            var markerRenderer = marker.GetComponent<SpriteRenderer>();
            markerRenderer.sprite = LoadSprite("Assets/Art/Gameplay/workbench_slot.png"); markerRenderer.sortingOrder = 1;
            markerRenderer.color = new Color(1, .78f, .4f, .14f);
            if (markerRenderer.sprite != null) marker.transform.localScale = new Vector3(2.3f / markerRenderer.sprite.bounds.size.x, 3.2f / markerRenderer.sprite.bounds.size.y, 1);
            portalComponent.highlightRenderer = markerRenderer;
            var label = new GameObject("PortalLabel", typeof(TextMeshPro)); label.transform.SetParent(portal.transform, false);
            label.transform.localPosition = new Vector3(0, 1.8f, -.1f);
            var labelText = label.GetComponent<TextMeshPro>(); labelText.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            labelText.text = "出餐台 →"; labelText.fontSize = 3; labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = new Color(1, .9f, .65f); labelText.GetComponent<MeshRenderer>().sortingOrder = 10;

            var cafeHud = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "CafeHUD");
            if (cafeHud != null)
            {
                cafeHud.SetActive(true);
                var hudContext = cafeHud.GetComponentInChildren<CoffeeLocationHudUI>(true);
                if (hudContext != null) hudContext.controller = controller;
            }
            var legacyHud = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "GameplayCanvas");
            if (legacyHud != null) legacyHud.SetActive(false);
            var legacyBridge = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "UnityCliBridge_Sample_Temp");
            if (legacyBridge != null) legacyBridge.SetActive(false);
            controller.locations = new[]
            {
                Binding(CoffeeLocationId.FirstFloorCafe, cafe, cafeEntities),
                Binding(CoffeeLocationId.FirstFloorServingCounter, counter, counterEntities),
                Binding(CoffeeLocationId.SecondFloor, second),
                Binding(CoffeeLocationId.Basement, basement)
            };
            controller.initialLocation = CoffeeLocationId.FirstFloorCafe;

            var camera = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Camera>(true)).FirstOrDefault(x => x.CompareTag("MainCamera"));
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .08f, .065f);
                var viewport = camera.GetComponent<CoffeeWorldViewport>(); if (viewport == null) viewport = camera.gameObject.AddComponent<CoffeeWorldViewport>();
                viewport.backgrounds = building.GetComponentsInChildren<SpriteRenderer>(true).Where(x => x.sortingOrder == -100).ToArray();
                viewport.Apply();
            }
            BuildLetterbox(scene);
            BuildNavigation(scene, controller);
            SetInitialActiveState(controller.locations, CoffeeLocationId.FirstFloorCafe);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CoffeeLocation] 已重构 {scene.path}：一楼营业区、出餐台、二楼、地下室与右下角导航已生成。");
        }

        private static CoffeeLocationController.LocationBinding Binding(CoffeeLocationId id, GameObject root, params GameObject[] extra)
        {
            return new CoffeeLocationController.LocationBinding
            {
                id = id,
                root = root,
                visibleRoots = extra == null ? Array.Empty<GameObject>() : extra.Where(x => x != null).ToArray()
            };
        }

        private static void SetInitialActiveState(CoffeeLocationController.LocationBinding[] bindings, CoffeeLocationId active)
        {
            CoffeeLocationController.ApplyVisibility(bindings, active);
        }

        private static GameObject NewRoot(Scene scene, string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static GameObject Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void CreateBackground(Transform parent, string name, string file)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0, 5);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = LoadSprite($"{BackgroundDir}/{file}");
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = -100;
            if (renderer.sprite != null)
            {
                var height = renderer.sprite.bounds.size.y;
                go.transform.localScale = Vector3.one * (10.8f / height);
            }
        }

        private static Sprite LoadSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ??
                   AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }

        private static void BuildLetterbox(Scene scene)
        {
            var canvasGo = NewRoot(scene, "LetterboxUI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 450;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.GetComponent<RectTransform>();
            Stretch(root);
            var top = ImageBox(root, "TopBlackBar", Color.black);
            top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one; top.pivot = new Vector2(.5f, 1);
            top.offsetMin = new Vector2(0, -160); top.offsetMax = Vector2.zero;
            var bottom = ImageBox(root, "BottomBlackBar", Color.black);
            bottom.anchorMin = Vector2.zero; bottom.anchorMax = new Vector2(1, 0); bottom.pivot = new Vector2(.5f, 0);
            bottom.offsetMin = Vector2.zero; bottom.offsetMax = new Vector2(0, 160);
        }

        private static void BuildNavigation(Scene scene, CoffeeLocationController controller)
        {
            var canvasGo = NewRoot(scene, "LocationNavigationUI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 600;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var nav = canvasGo.AddComponent<CoffeeLocationNavigationUI>();
            nav.controller = controller;
            var root = canvasGo.GetComponent<RectTransform>(); Stretch(root);
            var panel = ImageBox(root, "BottomRightNavigation", new Color(.035f, .04f, .055f, .94f));
            panel.anchorMin = new Vector2(1, 0); panel.anchorMax = new Vector2(1, 0); panel.pivot = new Vector2(1, 0);
            panel.anchoredPosition = new Vector2(-16, 10); panel.sizeDelta = new Vector2(400, 140);
            nav.currentLocationText = Text(panel, "CurrentLocation", "当前地点", new Vector2(0, -22), new Vector2(370, 28), 18, TextAlignmentOptions.Center);
            nav.cafeButton = Button(panel, "FirstFloorButton", "一楼 · 营业区", new Vector2(-94, -57));
            nav.counterButton = Button(panel, "ServingCounterButton", "一楼 · 出餐台", new Vector2(94, -57));
            nav.secondFloorButton = Button(panel, "SecondFloorButton", "顶楼", new Vector2(-94, -106));
            nav.basementButton = Button(panel, "BasementButton", "地下室", new Vector2(94, -106));
        }

        private static RectTransform ImageBox(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        private static TMP_Text Text(Transform parent, string name, string value, Vector2 pos, Vector2 size, int fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = pos; rect.sizeDelta = size;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            text.text = value; text.fontSize = fontSize; text.alignment = alignment; text.color = new Color(1, .9f, .65f); text.raycastTarget = false;
            return text;
        }

        private static Button Button(Transform parent, string name, string value, Vector2 pos)
        {
            var rect = ImageBox(parent, name, new Color(.27f, .18f, .12f, 1));
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = pos; rect.sizeDelta = new Vector2(178, 40);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            Text(rect, "Label", value, new Vector2(0, -5), new Vector2(142, 36), 16, TextAlignmentOptions.Center);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void RemoveGenerated(Scene scene, params string[] names)
        {
            foreach (var root in scene.GetRootGameObjects().Where(x => names.Contains(x.name)).ToArray())
                Object.DestroyImmediate(root);
        }
    }
}
#endif

