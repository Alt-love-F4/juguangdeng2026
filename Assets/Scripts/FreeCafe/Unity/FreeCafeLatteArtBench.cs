using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>拉花工作台：复用现有 Plane 与背景，不创建新场景；左侧面板展示预设和实时识别。</summary>
    public sealed class FreeCafeLatteArtBench : MonoBehaviour, ICafeMiniGamePresenter, ICafeLatteArtResultPresenter, IPointerClickHandler
    {
        public FreeCafeSubSceneDock dock;
        [Tooltip("为空时使用爱心、X、A、郁金香、树叶五个内置模板；可在 Inspector 传入自己的预览图和模板类型。")]
        public LatteArtPatternPreset[] patternPresets;
        public string LastPatternId { get; private set; }

        private string activeToken;
        private Action<string, bool, float> completion;
        private ILatteArtSurface effect;
        private Canvas overlay;
        private GameObject overlayRoot;
        private TMP_Text status;
        private readonly List<LatteArtPatternPreset> activePresets = new List<LatteArtPatternPreset>();
        private bool finishing;
        private bool dockWasDocked, layoutDockWasDocked;
        private FreeCafeSubSceneDock layoutDock;
        private Transform layoutRoot;
        private SpriteRenderer background;
        private Vector3 authoredLayoutPosition, authoredLayoutScale;
        private int viewWidth, viewHeight;
        private float viewSize, viewAspect;

        private void Awake()
        {
            layoutRoot = transform.parent != null ? transform.parent : transform;
            authoredLayoutPosition = layoutRoot.localPosition;
            authoredLayoutScale = layoutRoot.localScale;
            layoutDock = layoutRoot.GetComponent<FreeCafeSubSceneDock>();
            background = GetComponent<SpriteRenderer>();
        }

        public void Begin(string kind, string cupId, string token, Action<string, bool, float> complete)
        {
            if (!string.IsNullOrEmpty(activeToken)) Cancel(activeToken);
            activeToken = token; completion = complete; LastPatternId = null; finishing = false;
            effect = FindSurface();
            dockWasDocked = dock != null && dock.IsDocked;
            layoutDockWasDocked = layoutDock != null && layoutDock.IsDocked;
            // Dock both the Counter parent and the authored LatteArtBench child. The
            // camera remains unchanged; the child dock is what removes its idle x=60 offset.
            if (dock != null) dock.SetDocked(true);
            if (layoutDock != null) layoutDock.SetDocked(true);
            layoutRoot.localPosition = Vector3.zero;
            FitBackgroundToView();
            if (effect != null) { effect.SetInteractionEnabled(true); effect.ResetLatte(); }
            BuildPresetList(); BuildOverlay();
        }

        public void Cancel(string token)
        {
            if (!string.IsNullOrEmpty(activeToken) && token != activeToken) return;
            activeToken = null; completion = null; finishing = false;
            if (effect != null) effect.SetInteractionEnabled(false);
            DestroyOverlay();
            if (layoutRoot != null)
            {
                layoutRoot.localPosition = authoredLayoutPosition;
                layoutRoot.localScale = authoredLayoutScale;
            }
            if (dock != null) dock.SetDocked(dockWasDocked);
            if (layoutDock != null) layoutDock.SetDocked(layoutDockWasDocked);
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(activeToken)) Cancel(activeToken);
        }

        private void LateUpdate()
        {
            if (string.IsNullOrEmpty(activeToken)) return;
            Camera camera = Camera.main;
            if (camera != null && (viewWidth != camera.pixelWidth || viewHeight != camera.pixelHeight || !Mathf.Approximately(viewSize, camera.orthographicSize) || !Mathf.Approximately(viewAspect, camera.aspect)))
                FitBackgroundToView();
        }

        private void FitBackgroundToView()
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic || background == null || layoutRoot == null) return;
            // Cover, never shrink or stretch. Scale the whole authored layout so the cup,
            // liquid and background stay aligned; leave the gameplay camera untouched.
            layoutRoot.localScale = authoredLayoutScale;
            Vector3 size = background.bounds.size;
            if (size.x <= 0 || size.y <= 0) return;
            float height = camera.orthographicSize * 2;
            float factor = Mathf.Max(1f, height / size.y * 1.0001f, height * camera.aspect / size.x * 1.0001f);
            layoutRoot.localScale = new Vector3(authoredLayoutScale.x * factor, authoredLayoutScale.y * factor, authoredLayoutScale.z);
            viewWidth = camera.pixelWidth; viewHeight = camera.pixelHeight; viewSize = camera.orthographicSize; viewAspect = camera.aspect;
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(activeToken) || effect == null || status == null) return;
            var recognition = effect.Recognize(activePresets);
            status.text = "实时拟合：" + (recognition.HasPattern ? recognition.patternId + "  " + (recognition.confidence * 100f).ToString("0") + "%" : "绘制中") +
                          "\n轨迹点：" + recognition.pointCount + "\n完成后点击提交拉花";
            LastPatternId = recognition.HasPattern ? recognition.patternId : null;
        }

        private ILatteArtSurface FindSurface()
        {
            Transform root = transform.parent != null ? transform.parent : transform;
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is ILatteArtSurface surface) return surface;
            return null;
        }

        private void BuildPresetList()
        {
            activePresets.Clear();
            if (patternPresets != null) foreach (var preset in patternPresets) if (preset != null && preset.template != LatteArtTemplateKind.None) activePresets.Add(preset);
            if (activePresets.Count > 0) return;
            AddBuiltin("heart", "爱心", LatteArtTemplateKind.Heart);
            AddBuiltin("x", "X 形", LatteArtTemplateKind.X);
            AddBuiltin("a", "A 形", LatteArtTemplateKind.A);
            AddBuiltin("tulip", "郁金香", LatteArtTemplateKind.Tulip);
            AddBuiltin("rosetta", "树叶", LatteArtTemplateKind.Rosetta);
        }
        private void AddBuiltin(string id, string label, LatteArtTemplateKind template) => activePresets.Add(new LatteArtPatternPreset { id = id, displayName = label, template = template });

        private void BuildOverlay()
        {
            DestroyOverlay();
            overlayRoot = new GameObject("LatteArtPresetOverlay", typeof(RectTransform));
            overlay = overlayRoot.AddComponent<Canvas>(); overlay.renderMode = RenderMode.ScreenSpaceOverlay; overlay.sortingOrder = 900;
            overlayRoot.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            overlayRoot.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            overlayRoot.AddComponent<GraphicRaycaster>();
            var panel = MakeImage(overlayRoot.transform, "PresetPanel", new Color(.08f, .06f, .045f, .94f)); SetRect(panel.rectTransform, new Vector2(0, .12f), new Vector2(.22f, .88f));
            var title = MakeText(panel.transform, "Title", "拉花图案", 25, Color.white); SetRect(title.rectTransform, new Vector2(.08f, .9f), new Vector2(.92f, .98f)); title.alignment = TextAlignmentOptions.Center;
            var listObject = new GameObject("PresetList", typeof(RectTransform)); listObject.transform.SetParent(panel.transform, false); SetRect(listObject.GetComponent<RectTransform>(), new Vector2(.08f, .36f), new Vector2(.92f, .88f)); var list = listObject.AddComponent<VerticalLayoutGroup>(); list.padding = new RectOffset(0, 0, 0, 0); list.spacing = 8; list.childForceExpandHeight = false;
            foreach (var preset in activePresets)
            {
                var buttonImage = MakeImage(list.transform, preset.id + "Button", new Color(.2f, .15f, .11f, 1)); buttonImage.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
                var button = buttonImage.gameObject.AddComponent<Button>(); var label = MakeText(buttonImage.transform, "Label", preset.displayName, 18, Color.white); Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center; button.onClick.AddListener(() => { LastPatternId = preset.id; });
                if (preset.preview != null) { var preview = new GameObject("Preview", typeof(RectTransform), typeof(RawImage)); preview.transform.SetParent(buttonImage.transform, false); var raw = preview.GetComponent<RawImage>(); raw.texture = preset.preview; raw.raycastTarget = false; preview.GetComponent<RectTransform>().anchorMin = new Vector2(0,0); preview.GetComponent<RectTransform>().anchorMax = new Vector2(.3f,1); preview.GetComponent<RectTransform>().offsetMin = preview.GetComponent<RectTransform>().offsetMax = Vector2.zero; label.rectTransform.anchorMin = new Vector2(.28f,0); label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero; }
            }
            status = MakeText(panel.transform, "Status", "绘制轨迹后这里会实时显示拟合图案", 16, new Color(.94f,.78f,.48f)); SetRect(status.rectTransform, new Vector2(.08f, .17f), new Vector2(.92f, .34f)); status.alignment = TextAlignmentOptions.Center;
            var submit = MakeImage(panel.transform, "Submit", new Color(.54f,.28f,.12f,1)); SetRect(submit.rectTransform, new Vector2(.08f,.05f), new Vector2(.92f,.13f)); var submitButton = submit.gameObject.AddComponent<Button>(); var submitLabel = MakeText(submit.transform, "Label", "提交拉花", 18, Color.white); Stretch(submitLabel.rectTransform); submitLabel.alignment = TextAlignmentOptions.Center; submitButton.onClick.AddListener(Finish);
            var cancel = MakeImage(panel.transform, "Cancel", new Color(.2f,.2f,.2f,1)); SetRect(cancel.rectTransform, new Vector2(.08f,.0f), new Vector2(.92f,.045f)); var cancelButton = cancel.gameObject.AddComponent<Button>(); var cancelLabel = MakeText(cancel.transform, "Label", "取消", 15, Color.white); Stretch(cancelLabel.rectTransform); cancelLabel.alignment = TextAlignmentOptions.Center; cancelButton.onClick.AddListener(() => completion?.Invoke(activeToken, false, 0));
        }

        private void Finish()
        {
            if (finishing || string.IsNullOrEmpty(activeToken) || effect == null) return;
            var result = effect.Recognize(activePresets); LastPatternId = result.HasPattern ? result.patternId : "unknown";
            float score = result.HasPattern ? result.confidence * 100f : 0f;
            finishing = true; completion?.Invoke(activeToken, true, score);
        }
        private void DestroyOverlay() { if (overlayRoot != null) Destroy(overlayRoot); overlayRoot = null; overlay = null; status = null; }
        public void OnPointerClick(PointerEventData eventData) { /* 背景点击不再取消，玩家可自由在 Plane 上绘制。 */ }
        private static Image MakeImage(Transform parent, string name, Color color) { var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); var image = go.GetComponent<Image>(); image.color = color; return image; }
        private TMP_Text MakeText(Transform parent, string name, string value, float size, Color color) { var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); var text = go.GetComponent<TextMeshProUGUI>(); text.text = value; text.font = TMP_Settings.defaultFontAsset; text.fontSize = size; text.color = color; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; return text; }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
