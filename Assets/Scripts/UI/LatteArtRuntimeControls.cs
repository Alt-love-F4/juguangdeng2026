using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UIEventSystem = UnityEngine.EventSystems.EventSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OnePersonCompany
{
    /// <summary>Runtime tuning of the latte GPU effect only. No business state and no clock.</summary>
    [RequireComponent(typeof(LatteArtPrototype))]
    public sealed class LatteArtRuntimeControls : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset uiFont;
        private LatteArtPrototype effect;
        private GameObject canvasObject, createdEventSystem;
        private RectTransform body, panel;
        private TMP_Text foldLabel;
        private bool collapsed;
        private readonly List<Action> refresh = new List<Action>();
        private static readonly Color Accent = new Color(0.93f, 0.71f, 0.39f, 1f);
        private static readonly Color TextColor = new Color(0.94f, 0.92f, 0.87f, 1f);
        private static readonly Color Muted = new Color(0.63f, 0.68f, 0.73f, 1f);

        private void Start() { Build(); }
        private void OnEnable() { if (canvasObject != null) canvasObject.SetActive(true); }
        private void OnDisable() { if (canvasObject != null) canvasObject.SetActive(false); }
        private void Build()
        {
            effect = GetComponent<LatteArtPrototype>();
            if (uiFont == null) uiFont = TMP_Settings.defaultFontAsset;
            canvasObject = new GameObject("LatteArt_ControlsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (UIEventSystem.current == null && UnityEngine.Object.FindFirstObjectByType<UIEventSystem>() == null)
            {
                createdEventSystem = new GameObject("LatteArt_EventSystem", typeof(UIEventSystem), typeof(InputSystemUIInputModule));
                createdEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            panel = Rect("LatteArt_ParameterPanel", canvasObject.transform, 18, 18, 300, 684);
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.055f, 0.072f, 0.094f, 0.96f);
            // Background raycast blocks clicks from leaking through into the coffee.
            Label("Title", panel, "咖啡拉花 · NS", 18, 14, 224, 30, 22, TextColor);
            ButtonAt("Fold", panel, "−", 252, 13, 32, 30, TogglePanel, out foldLabel);
            body = Rect("ControlsBody", panel, 0, 51, 300, 625);
            Label("Instructions", body, "左键注奶 · 滚轮调笔刷大小\n半径缩小会同步减弱表面扩张", 18, 0, 268, 43, 12, Muted);
            int row = 0;
            AddSlider("BrushRadius", "笔刷半径", 0.005f, 0.08f, () => effect.BrushRadius, v => effect.BrushRadius = v, "F3", row++);
            AddSlider("PourRate", "注奶速度", 0.1f, 12f, () => effect.PourRate, v => effect.PourRate = v, "F2", row++);
            AddSlider("PushStrength", "表面扩张（联动）", 0f, 2f, () => effect.PushStrength, v => effect.PushStrength = v, "F2", row++);
            AddSlider("DragStrength", "鼠标拖动", 0f, 2f, () => effect.DragStrength, v => effect.DragStrength = v, "F2", row++);
            AddSlider("VelocityDamping", "速度阻尼", 0f, 4f, () => effect.VelocityDamping, v => effect.VelocityDamping = v, "F2", row++);
            AddSlider("Viscosity", "液体黏性", 0f, 0.0005f, () => effect.Viscosity, v => effect.Viscosity = v, "F5", row++);
            AddSlider("Diffusion", "奶泡扩散", 0f, 2f, () => effect.Diffusion, v => effect.Diffusion = v, "F2", row++);
            AddSlider("VorticityStrength", "涡旋增强", 0f, 8f, () => effect.VorticityStrength, v => effect.VorticityStrength = v, "F2", row++);
            AddSlider("PressureIterations", "压力迭代", 12f, 80f, () => effect.PressureIterations, v => effect.PressureIterations = Mathf.RoundToInt(v), "F0", row++, true);
            AddSlider("FoamRelief", "奶泡高光起伏", 0f, 12f, () => effect.FoamRelief, v => effect.FoamRelief = v, "F2", row++);
            ButtonAt("ResetLatte", body, "清空拉花", 18, 545, 126, 34, () => effect.ResetLatte(), out _);
            ButtonAt("RestoreDefaults", body, "恢复参数", 154, 545, 128, 34, () =>
            {
                effect.RestoreAppearanceDefaults();
                foreach (Action callback in refresh) callback();
            }, out _);
            Label("Footer", body, "R 键清空 · 参数仅本次运行有效\n压力迭代越高，GPU 开销越大", 18, 587, 266, 34, 11, Muted);
        }
        private void AddSlider(string name, string caption, float min, float max, Func<float> get, Action<float> set, string format, int index, bool whole = false)
        {
            float y = 52 + index * 48;
            Label(name + "Label", body, caption, 18, y, 190, 21, 13, TextColor);
            TMP_Text number = Label(name + "Value", body, get().ToString(format), 214, y, 68, 21, 12, Accent);
            number.alignment = TextAlignmentOptions.Right;
            RectTransform track = Rect(name, body, 18, y + 23, 264, 19);
            Image hit = track.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = whole;
            RectTransform back = Stretch("Track", track, 7, 7, -7, -7);
            var trackImage = back.gameObject.AddComponent<Image>(); trackImage.color = new Color(0.19f, 0.23f, 0.27f); trackImage.raycastTarget = false;
            RectTransform fillArea = Stretch("FillArea", track, 7, 7, -7, -7);
            RectTransform fill = Stretch("Fill", fillArea, 0, 0, 0, 0);
            var fillImage = fill.gameObject.AddComponent<Image>(); fillImage.color = Accent; fillImage.raycastTarget = false;
            RectTransform handleArea = Stretch("HandleArea", track, 7, 0, -7, 0);
            RectTransform handle = Rect("Handle", handleArea, 0, 2, 12, 15);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(12, 0);
            handle.anchoredPosition = Vector2.zero;
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(1f, 0.90f, 0.72f);
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.SetValueWithoutNotify(get());
            slider.onValueChanged.AddListener(v => { set(v); number.text = get().ToString(format); });
            refresh.Add(() =>
            {
                float value = get();
                if (Mathf.Approximately(slider.value, value)) return;
                slider.SetValueWithoutNotify(value);
                number.text = value.ToString(format);
            });
        }
        // Keep slider values synchronized with wheel input / linked expansion; no timer.
        private void LateUpdate()
        {
            if (effect == null || collapsed) return;
            foreach (Action callback in refresh) callback();
        }
        private TMP_Text Label(string name, Transform parent, string value, float x, float y, float width, float height, float size, Color color)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = uiFont; text.text = value; text.fontSize = size;
            text.color = color; text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
        private void ButtonAt(string name, Transform parent, string caption, float x, float y, float width, float height, Action action, out TMP_Text label)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.20f, 0.25f, 0.29f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            label = Label("Caption", rect, caption, 2, 0, width - 4, height, 13, TextColor);
            label.alignment = TextAlignmentOptions.Center;
        }
        private void TogglePanel()
        {
            collapsed = !collapsed;
            body.gameObject.SetActive(!collapsed);
            panel.sizeDelta = new Vector2(300, collapsed ? 54 : 684);
            foldLabel.text = collapsed ? "+" : "−";
        }
        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        private static RectTransform Stretch(string name, Transform parent, float left, float bottom, float right, float top)
        {
            RectTransform rect = Rect(name, parent, 0, 0, 0, 0);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(right, top); return rect;
        }
        private void OnDestroy()
        {
            if (canvasObject != null) Destroy(canvasObject);
            if (createdEventSystem != null) Destroy(createdEventSystem);
            refresh.Clear();
        }
    }
}


