#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.Gameplay.EditorTools
{
    /// <summary>生成独立的 UI + 交互实体诊断场景，不修改正式 CoffeeShop 场景。</summary>
    public static class CoffeeUIEntitySandboxBuilder
    {
        private const string SourceScene = "Assets/Scenes/CoffeeShop.unity";
        private const string DebugScene = "Assets/Scenes/CoffeeUIEntitySandbox.unity";
        private const string FontPath = "Assets/Resources/Fonts/SimHei SDF.asset";

        [MenuItem("Tools/Spotlight/Build Coffee UI Entity Sandbox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("请先退出 Play Mode");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
                throw new System.InvalidOperationException("源场景不存在：" + SourceScene);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // 卸载旧沙盒后再删除旧资产。不改写正式场景或它的预制体。
            EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DebugScene) != null &&
                !AssetDatabase.DeleteAsset(DebugScene))
                throw new System.InvalidOperationException("无法删除旧诊断场景：" + DebugScene);
            if (!AssetDatabase.CopyAsset(SourceScene, DebugScene))
                throw new System.InvalidOperationException("无法复制诊断场景：" + DebugScene);
            var scene = EditorSceneManager.OpenScene(DebugScene, OpenSceneMode.Single);
            foreach (var old in Object.FindObjectsByType<CoffeeUIDebugOverlay>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (old.gameObject.scene == scene) Object.DestroyImmediate(old.gameObject);
            var overlay = BuildOverlay();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new System.InvalidOperationException("无法保存诊断场景：" + DebugScene);
            Selection.activeGameObject = overlay;
            Debug.Log("[CoffeeUI.Debug] 已删除旧场景，从 CoffeeShop 复制并重建：" + DebugScene);
        }

        private static GameObject BuildOverlay()
        {
            var canvasGo = new GameObject("CoffeeUIDebugOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CoffeeUIDebugOverlay));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 700;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            var root = canvasGo.transform as RectTransform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var panel = CreateImage(root, "DebugPanel", new Vector2(-24, -24), new Vector2(560, 1030), new Color(.035f, .045f, .07f, .94f));
            panel.anchorMin = new Vector2(1, 1);
            panel.anchorMax = new Vector2(1, 1);
            panel.pivot = new Vector2(1, 1);

            var title = CreateText(panel, "Title", "Coffee UI + Entity Sandbox", 28, new Vector2(0, -28), new Vector2(500, 54), TextAlignmentOptions.Left);
            title.color = new Color(1f, .84f, .42f);
            CreateText(panel, "Hint", "测试 UI 控件、按钮绑定、事件系统与可交互实体。\n左侧仍可拖动杯子/原料到工作台或设备。", 17, new Vector2(0, -84), new Vector2(500, 70), TextAlignmentOptions.Left);

            var status = CreateText(panel, "Status", "正在读取运行态…", 16, new Vector2(0, -190), new Vector2(500, 275), TextAlignmentOptions.TopLeft);
            status.textWrappingMode = TextWrappingModes.Normal;
            status.color = new Color(.82f, .9f, 1f);

            var buttons = CreateImage(panel, "Buttons", new Vector2(0, -480), new Vector2(500, 270), new Color(.08f, .1f, .15f, 1f));
            var start = CreateButton(buttons, "StartDayButton", "1. 开始营业", new Vector2(-120, -36));
            var talk = CreateButton(buttons, "TalkButton", "2. 交流 / 接单", new Vector2(120, -36));
            var select = CreateButton(buttons, "SelectCupButton", "3. 自动取杯", new Vector2(-120, -94));
            var action = CreateButton(buttons, "ActionButton", "4. 调用当前动作", new Vector2(120, -94));
            var clear = CreateButton(buttons, "ClearItemsButton", "清理散落物", new Vector2(-120, -152));
            var dump = CreateButton(buttons, "DumpButton", "Dump Debug JSON", new Vector2(120, -152));
            var addCoffee = CreateButton(buttons, "AddCoffeeBeanButton", "添加咖啡豆", new Vector2(-120, -210));
            var addMilk = CreateButton(buttons, "AddMilkButton", "添加牛奶", new Vector2(120, -210));

            var auto = CreateToggle(panel, "AutoRefresh", "自动刷新诊断", new Vector2(-130, -776));
            auto.isOn = true;
            var slider = CreateSlider(panel, "RefreshRate", new Vector2(125, -818));
            CreateText(panel, "SliderLabel", "刷新频率", 15, new Vector2(-95, -818), new Vector2(90, 32), TextAlignmentOptions.Left);

            var log = CreateText(panel, "Log", "等待操作…", 15, new Vector2(0, -874), new Vector2(500, 125), TextAlignmentOptions.TopLeft);
            log.textWrappingMode = TextWrappingModes.Normal;
            log.color = new Color(1f, .82f, .55f);

            var debug = canvasGo.GetComponent<CoffeeUIDebugOverlay>();
            debug.debugPanel = panel.gameObject;
            var toggle = CreateButton(root, "DebugToggleButton", "调试面板 / Debug", new Vector2(-18, -20));
            var toggleRect = (RectTransform)toggle.transform;
            toggleRect.anchorMin = toggleRect.anchorMax = Vector2.one; toggleRect.pivot = new Vector2(1, 1);
            debug.debugToggleButton = toggle;
            // 默认收起，避免诊断浮层抢走营业区右侧入口的射线。
            panel.gameObject.SetActive(false);
            debug.statusText = status;
            debug.logText = log;
            debug.startDayButton = start;
            debug.talkButton = talk;
            debug.selectCupButton = select;
            debug.actionButton = action;
            debug.clearItemsButton = clear;
            debug.addCoffeeButton = addCoffee;
            debug.addMilkButton = addMilk;
            debug.dumpButton = dump;
            debug.autoRefreshToggle = auto;
            debug.refreshRateSlider = slider;
            return canvasGo;
        }

        private static RectTransform CreateImage(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.transform as RectTransform;
            rect.anchorMin = new Vector2(.5f, 1);
            rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rect;
        }

        private static TMP_Text CreateText(Transform parent, string name, string text, float size, Vector2 anchoredPosition, Vector2 dimensions, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.transform as RectTransform;
            rect.anchorMin = new Vector2(.5f, 1);
            rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = dimensions;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            tmp.fontSize = size;
            tmp.text = text;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, string text, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.transform as RectTransform;
            rect.anchorMin = new Vector2(.5f, 1);
            rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(210, 45);
            var image = go.GetComponent<Image>();
            image.color = new Color(.34f, .21f, .1f, 1f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var label = CreateText(go.transform, "Label", text, 17, Vector2.zero, new Vector2(200, 42), TextAlignmentOptions.Center);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static Toggle CreateToggle(Transform parent, string name, string text, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);
            var rect = go.transform as RectTransform;
            rect.anchorMin = new Vector2(.5f, 1);
            rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(190, 34);
            var bg = CreateImage(rect, "Background", new Vector2(0, -3), new Vector2(28, 28), new Color(.2f, .2f, .24f, 1));
            bg.anchorMin = new Vector2(0, 1); bg.anchorMax = new Vector2(0, 1); bg.pivot = new Vector2(0, 1);
            var check = CreateImage(bg, "Checkmark", Vector2.zero, new Vector2(20, 20), new Color(1f, .75f, .2f, 1));
            check.anchorMin = new Vector2(.5f, .5f); check.anchorMax = new Vector2(.5f, .5f); check.pivot = new Vector2(.5f, .5f);
            var label = CreateText(rect, "Label", text, 15, new Vector2(32, -1), new Vector2(145, 30), TextAlignmentOptions.Left);
            label.rectTransform.anchorMin = new Vector2(0, 1);
            label.rectTransform.anchorMax = new Vector2(0, 1);
            label.rectTransform.pivot = new Vector2(0, 1);
            var toggle = go.GetComponent<Toggle>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.targetGraphic = bg.GetComponent<Image>();
            return toggle;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rect = go.transform as RectTransform;
            rect.anchorMin = new Vector2(.5f, 1);
            rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(200, 26);
            var bg = CreateImage(rect, "Background", Vector2.zero, new Vector2(200, 8), new Color(.25f, .25f, .3f, 1));
            bg.anchorMin = new Vector2(.5f, .5f); bg.anchorMax = new Vector2(.5f, .5f); bg.pivot = new Vector2(.5f, .5f);
            var fill = CreateImage(rect, "Fill", Vector2.zero, new Vector2(100, 8), new Color(.95f, .62f, .18f, 1));
            fill.anchorMin = new Vector2(0, .5f); fill.anchorMax = new Vector2(0, .5f); fill.pivot = new Vector2(0, .5f);
            var handle = CreateImage(rect, "Handle", Vector2.zero, new Vector2(22, 22), new Color(1f, .82f, .36f, 1));
            handle.anchorMin = new Vector2(0, .5f); handle.anchorMax = new Vector2(0, .5f); handle.pivot = new Vector2(.5f, .5f);
            var slider = go.GetComponent<Slider>();
            slider.minValue = 0; slider.maxValue = 1; slider.value = .25f;
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
            return slider;
        }
    }
}
#endif
