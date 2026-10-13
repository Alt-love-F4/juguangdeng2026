using System;
using System.Collections;
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
        [Header("Latte presentation")]
        [Range(.65f, 1f)] public float layoutHeightFraction = .9f;
        [Min(.05f)] public float transitionDuration = .28f;
        [Min(.05f)] public float recognitionInterval = .15f;
        public bool IsTransitioning => transition != null;
        private Coroutine transition;
        private CanvasGroup contentGroup;
        private Image curtain;
        private SpriteRenderer backdrop;
        private Sprite backdropSprite;
        private readonly List<Texture2D> generatedPreviews = new List<Texture2D>();
        private readonly List<Image> presetButtons = new List<Image>();
        private string selectedPreset;
        private float nextRecognition;
        private int lastPointCount = -1;
        private LatteArtRecognitionResult latestRecognition;
        private Button submitButton;
        private static readonly Color Cream = new Color(.98f, .91f, .79f);
        private TMP_Text authoredCaption;
        private bool captionWasEnabled;

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
            Transform caption = layoutRoot.Find("Caption");
            if (caption != null) authoredCaption = caption.GetComponent<TMP_Text>();
            effect = FindSurface();
            if (effect != null) effect.SetInteractionEnabled(false);
        }

        public void Begin(string kind, string cupId, string token, Action<string, bool, float> complete)
        {
            if (transition != null) { StopCoroutine(transition); transition = null; RestoreLayout(); }
            if (!string.IsNullOrEmpty(activeToken)) Cancel(activeToken);
            if (transition != null) { StopCoroutine(transition); transition = null; RestoreLayout(); }
            activeToken = token; completion = complete; LastPatternId = null; finishing = false;
            lastPointCount = -1; nextRecognition = 0; latestRecognition = default;
            effect = FindSurface();
            dockWasDocked = dock != null && dock.IsDocked;
            layoutDockWasDocked = layoutDock != null && layoutDock.IsDocked;
            if (authoredCaption != null) { captionWasEnabled = authoredCaption.enabled; authoredCaption.enabled = false; }
            if (effect != null) { effect.SetInteractionEnabled(false); effect.ResetLatte(); }
            BuildPresetList(); BuildOverlay();
            transition = StartCoroutine(TransitionRoutine(true));
        }

        public void Cancel(string token)
        {
            if (!string.IsNullOrEmpty(activeToken) && token != activeToken) return;
            activeToken = null; completion = null; finishing = false;
            if (effect != null) effect.SetInteractionEnabled(false);
            if (transition != null) { StopCoroutine(transition); transition = null; }
            if (isActiveAndEnabled && overlayRoot != null) transition = StartCoroutine(TransitionRoutine(false));
            else { RestoreLayout(); DestroyOverlay(); }
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(activeToken)) Cancel(activeToken);
            if (transition != null) { StopCoroutine(transition); transition = null; }
            RestoreLayout(); DestroyOverlay();
        }

        private void LateUpdate()
        {
            if (string.IsNullOrEmpty(activeToken) || IsTransitioning) return;
            Camera camera = Camera.main;
            if (camera != null && (viewWidth != camera.pixelWidth || viewHeight != camera.pixelHeight || !Mathf.Approximately(viewSize, camera.orthographicSize) || !Mathf.Approximately(viewAspect, camera.aspect)))
                FitBackgroundToView();
        }

        private void FitBackgroundToView()
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic || background == null || layoutRoot == null) return;
            // Fit the authored composition inside the view; never enlarge the cup for a wide screen.
            layoutRoot.localScale = authoredLayoutScale;
            Vector3 size = background.bounds.size;
            if (size.x <= 0 || size.y <= 0) return;
            float height = camera.orthographicSize * 2;
            float factor = Mathf.Min(1f, height * layoutHeightFraction / size.y, height * camera.aspect / size.x);
            layoutRoot.localScale = new Vector3(authoredLayoutScale.x * factor, authoredLayoutScale.y * factor, authoredLayoutScale.z);
            EnsureBackdrop(camera, height);
            viewWidth = camera.pixelWidth; viewHeight = camera.pixelHeight; viewSize = camera.orthographicSize; viewAspect = camera.aspect;
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(activeToken) || effect == null || status == null || IsTransitioning) return;
            if (Time.unscaledTime < nextRecognition) return;
            nextRecognition = Time.unscaledTime + recognitionInterval;
            if (lastPointCount != effect.StrokePointCount)
            {
                lastPointCount = effect.StrokePointCount;
                latestRecognition = effect.Recognize(activePresets);
            }
            var recognition = latestRecognition;
            string display = "自由绘制";
            foreach (var preset in activePresets) if (preset.id == recognition.patternId) { display = preset.displayName; break; }
            status.text = recognition.HasPattern ? "最像「" + display + "」\n相似度 " + (recognition.confidence * 100f).ToString("0") + "%" : effect.StrokePointCount >= 5 ? "自由创作中 · 尚未匹配参考\n可以继续绘制，也可以直接提交" : "按住左键，慢慢画出图案\n松手可分笔继续";
            LastPatternId = recognition.HasPattern ? recognition.patternId : null;
            if (submitButton != null) submitButton.interactable = effect.StrokePointCount >= 5;
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

        private void RestoreLayout()
        {
            if (layoutRoot == null || overlayRoot == null) return;
            if (dock != null) dock.SetDocked(dockWasDocked, true);
            if (layoutDock != null) layoutDock.SetDocked(layoutDockWasDocked, true);
            layoutRoot.localPosition = authoredLayoutPosition;
            layoutRoot.localScale = authoredLayoutScale;
            if (backdrop != null) backdrop.gameObject.SetActive(false);
            if (authoredCaption != null) authoredCaption.enabled = captionWasEnabled;
        }

        private IEnumerator TransitionRoutine(bool entering)
        {
            contentGroup.interactable = contentGroup.blocksRaycasts = false;
            curtain.gameObject.SetActive(true);
            float half = Mathf.Max(.025f, transitionDuration * .5f);
            float start = curtain.color.a;
            for (float elapsed = 0; elapsed < half; elapsed += Time.unscaledDeltaTime)
            {
                SetCurtain(Mathf.Lerp(start, 1, Mathf.SmoothStep(0, 1, elapsed / half)));
                if (contentGroup != null) contentGroup.alpha = entering ? 0 : 1 - elapsed / half;
                yield return null;
            }
            SetCurtain(1);
            if (entering)
            {
                if (dock != null) dock.SetDocked(true, true);
                if (layoutDock != null) layoutDock.SetDocked(true, true);
                FitBackgroundToView();
            }
            else RestoreLayout();
            yield return null;
            for (float elapsed = 0; elapsed < half; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / half);
                SetCurtain(1 - t);
                if (entering && contentGroup != null) contentGroup.alpha = t;
                yield return null;
            }
            SetCurtain(0); transition = null;
            if (entering && !string.IsNullOrEmpty(activeToken))
            {
                curtain.gameObject.SetActive(false);
                contentGroup.alpha = 1; contentGroup.interactable = contentGroup.blocksRaycasts = true;
                if (effect != null) effect.SetInteractionEnabled(true);
            }
            else DestroyOverlay();
        }
        private void SetCurtain(float alpha)
        {
            if (curtain != null) curtain.color = new Color(.15f, .075f, .045f, Mathf.Clamp01(alpha));
        }
        private void EnsureBackdrop(Camera camera, float height)
        {
            if (backdrop == null)
            {
                var go = new GameObject("LatteBackdrop"); go.transform.SetParent(layoutRoot, false);
                go.transform.localPosition = new Vector3(0, 0, 1);
                backdrop = go.AddComponent<SpriteRenderer>();
                backdropSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
                backdrop.sprite = backdropSprite;
                backdrop.color = new Color(.46f, .29f, .24f);
                backdrop.sortingLayerID = background.sortingLayerID;
                backdrop.sortingOrder = background.sortingOrder - 1;
            }
            backdrop.gameObject.SetActive(true);
            var scale = layoutRoot.lossyScale;
            backdrop.transform.localScale = new Vector3(height * camera.aspect / Mathf.Abs(scale.x) * 1.01f,
                height / Mathf.Abs(scale.y) * 1.01f, 1);
        }
        private void OnDestroy()
        {
            if (backdropSprite != null) Destroy(backdropSprite);
        }

        private void BuildOverlay()
        {
            DestroyOverlay(); presetButtons.Clear(); selectedPreset = null;
            overlayRoot = new GameObject("LatteArtPresetOverlay", typeof(RectTransform));
            overlay = overlayRoot.AddComponent<Canvas>(); overlay.renderMode = RenderMode.ScreenSpaceOverlay; overlay.sortingOrder = 900;
            var scaler = overlayRoot.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            overlayRoot.AddComponent<GraphicRaycaster>();
            var content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
            content.transform.SetParent(overlayRoot.transform, false); Stretch(content.GetComponent<RectTransform>());
            contentGroup = content.GetComponent<CanvasGroup>(); contentGroup.alpha = 0;
            var panel = MakeImage(content.transform, "PresetPanel", new Color(.16f, .10f, .08f));
            SetRect(panel.rectTransform, new Vector2(.035f, .075f), new Vector2(.285f, .925f));
            AddShadow(panel.gameObject, new Color(0, 0, 0, .35f), new Vector2(0, -8));
            var eyebrow = MakeText(panel.transform, "Eyebrow", "LATTE ART  /  拉花工作台", 13, new Color(.82f, .63f, .43f));
            SetRect(eyebrow.rectTransform, new Vector2(.09f, .92f), new Vector2(.91f, .965f)); eyebrow.alignment = TextAlignmentOptions.Left;
            var title = MakeText(panel.transform, "Title", "画一朵好看的花", 29, Cream);
            SetRect(title.rectTransform, new Vector2(.09f, .855f), new Vector2(.91f, .93f)); title.alignment = TextAlignmentOptions.Left;
            var subtitle = MakeText(panel.transform, "Subtitle", "选参考 · 慢慢画 · 完成后提交", 15, new Color(.78f, .69f, .62f));
            SetRect(subtitle.rectTransform, new Vector2(.09f, .815f), new Vector2(.91f, .855f)); subtitle.alignment = TextAlignmentOptions.Left;
            var referenceLabel = MakeText(panel.transform, "ReferenceLabel", "选择参考图案", 14, new Color(.82f, .63f, .43f));
            SetRect(referenceLabel.rectTransform, new Vector2(.09f, .765f), new Vector2(.91f, .805f)); referenceLabel.alignment = TextAlignmentOptions.Left;
            var listObject = new GameObject("PresetList", typeof(RectTransform)); listObject.transform.SetParent(panel.transform, false);
            SetRect(listObject.GetComponent<RectTransform>(), new Vector2(.09f, .345f), new Vector2(.91f, .76f));
            var list = listObject.AddComponent<VerticalLayoutGroup>(); list.spacing = 8; list.childControlHeight = true;
            list.childControlWidth = true; list.childForceExpandHeight = true;
            foreach (var preset in activePresets)
            {
                var buttonImage = MakeImage(list.transform, preset.id + "Button", new Color(.24f, .16f, .13f));
                buttonImage.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
                presetButtons.Add(buttonImage);
                var button = buttonImage.gameObject.AddComponent<Button>();
                ConfigureButton(button);
                var label = MakeText(buttonImage.transform, "Label", preset.displayName, 19, Cream);
                SetRect(label.rectTransform, new Vector2(.32f, 0), new Vector2(.77f, 1)); label.alignment = TextAlignmentOptions.Left;
                var selectedLabel = MakeText(buttonImage.transform, "Selected", "参考", 13, Cream);
                SetRect(selectedLabel.rectTransform, new Vector2(.77f, 0), new Vector2(.95f, 1)); selectedLabel.alignment = TextAlignmentOptions.Right;
                selectedLabel.gameObject.SetActive(false);
                var preview = new GameObject("Preview", typeof(RectTransform), typeof(RawImage)); preview.transform.SetParent(buttonImage.transform, false);
                var raw = preview.GetComponent<RawImage>(); raw.texture = preset.preview != null ? preset.preview : CreatePreview(preset.template);
                raw.color = new Color(1, 1, 1, .9f);
                raw.raycastTarget = false;
                SetRect(preview.GetComponent<RectTransform>(), new Vector2(.025f, .08f), new Vector2(.27f, .92f));
                var fitter = preview.AddComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 1;
                button.onClick.AddListener(() => SelectReference(preset.id));
            }
            var statusCard = MakeImage(panel.transform, "StatusCard", new Color(.22f, .14f, .11f));
            SetRect(statusCard.rectTransform, new Vector2(.09f, .205f), new Vector2(.91f, .325f));
            status = MakeText(statusCard.transform, "Status", "准备好后，在杯面上慢慢画出图案", 16, new Color(.92f, .82f, .72f));
            Stretch(status.rectTransform); status.rectTransform.offsetMin = new Vector2(16, 10); status.rectTransform.offsetMax = new Vector2(-16, -10); status.alignment = TextAlignmentOptions.Center;
            submitButton = MakeButton(panel.transform, "Submit", "完成拉花  ·  提交作品", new Vector2(.09f, .105f), new Vector2(.91f, .185f), Finish, true);
            submitButton.interactable = false;
            MakeButton(panel.transform, "Reset", "重新绘制", new Vector2(.09f, .035f), new Vector2(.48f, .085f), () =>
            { effect?.ResetLatte(); LastPatternId = null; lastPointCount = -1; nextRecognition = 0; submitButton.interactable = false; }, false);
            MakeButton(panel.transform, "Cancel", "返回吧台", new Vector2(.52f, .035f), new Vector2(.91f, .085f), () => completion?.Invoke(activeToken, false, 0), false);
            var help = MakeText(content.transform, "DrawingHint", "按住左键绘制   ·   松手可分笔   ·   滚轮调整线宽", 18, new Color(.96f, .89f, .80f));
            SetRect(help.rectTransform, new Vector2(.32f, .035f), new Vector2(.94f, .09f)); help.alignment = TextAlignmentOptions.Center;
            var badge = MakeText(content.transform, "CanvasBadge", "LATTE ART", 14, new Color(1, 1, 1, .65f));
            SetRect(badge.rectTransform, new Vector2(.82f, .91f), new Vector2(.95f, .95f)); badge.alignment = TextAlignmentOptions.Right;
            SelectReference(activePresets.Count > 0 ? activePresets[0].id : null);
            curtain = MakeImage(overlayRoot.transform, "TransitionCurtain", new Color(.15f, .075f, .045f, 0)); Stretch(curtain.rectTransform);
        }
        private Button MakeButton(Transform parent, string name, string caption, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action, bool primary)
        {
            var image = MakeImage(parent, name, primary ? new Color(.64f, .36f, .18f) : new Color(.29f, .20f, .16f));
            SetRect(image.rectTransform, min, max);
            AddShadow(image.gameObject, new Color(0, 0, 0, .22f), new Vector2(0, -3));
            var button = image.gameObject.AddComponent<Button>();
            ConfigureButton(button);
            var text = MakeText(image.transform, "Label", caption, primary ? 19 : 15, primary ? Cream : new Color(.88f, .77f, .66f));
            Stretch(text.rectTransform); text.alignment = TextAlignmentOptions.Center; button.onClick.AddListener(action); return button;
        }
        private void SelectReference(string id)
        {
            selectedPreset = id;
            for (int i = 0; i < presetButtons.Count; i++)
            {
                presetButtons[i].color = activePresets[i].id == selectedPreset ? new Color(.56f, .32f, .19f) : new Color(.24f, .16f, .13f);
                presetButtons[i].transform.Find("Selected").gameObject.SetActive(activePresets[i].id == selectedPreset);
            }
        }
        private static void ConfigureButton(Button button)
        {
            var colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.15f, 1.1f);
            colors.pressedColor = new Color(.8f, .8f, .8f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.6f, .6f, .6f, .75f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        private static void AddShadow(GameObject gameObject, Color color, Vector2 distance)
        {
            var shadow = gameObject.AddComponent<Shadow>(); shadow.effectColor = color; shadow.effectDistance = distance; shadow.useGraphicAlpha = true;
        }
        private Texture2D CreatePreview(LatteArtTemplateKind kind)
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "LatteReference_" + kind; texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            var points = LatteArtPatternRecognizer.PreviewPath(kind);
            Color32 line = Cream;
            for (int i = 1; i < points.Length; i++)
            {
                Vector2 a = (points[i - 1] * .76f + Vector2.one * .12f) * (size - 1);
                Vector2 b = (points[i] * .76f + Vector2.one * .12f) * (size - 1);
                if (float.IsNaN(a.x) || float.IsNaN(b.x)) continue;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) * 2));
                for (int step = 0; step <= steps; step++)
                {
                    Vector2 p = Vector2.Lerp(a, b, step / (float)steps);
                    for (int y = -2; y <= 2; y++) for (int x = -2; x <= 2; x++)
                    {
                        int px = Mathf.RoundToInt(p.x) + x, py = Mathf.RoundToInt(p.y) + y;
                        if (px >= 0 && px < size && py >= 0 && py < size && x*x+y*y <= 4) pixels[py * size + px] = line;
                    }
                }
            }
            texture.SetPixels32(pixels); texture.Apply(); generatedPreviews.Add(texture); return texture;
        }

        private void Finish()
        {
            if (finishing || IsTransitioning || string.IsNullOrEmpty(activeToken) || effect == null || effect.StrokePointCount < 5) return;
            var result = effect.Recognize(activePresets); LastPatternId = result.HasPattern ? result.patternId : "unknown";
            float score = result.HasPattern ? result.confidence * 100f : 0f;
            finishing = true; completion?.Invoke(activeToken, true, score);
        }
        private void DestroyOverlay()
        {
            if (overlayRoot != null) { overlayRoot.SetActive(false); Destroy(overlayRoot); }
            foreach (var texture in generatedPreviews) if (texture != null) Destroy(texture);
            generatedPreviews.Clear(); presetButtons.Clear();
            overlayRoot = null; overlay = null; status = null; contentGroup = null; curtain = null; submitButton = null;
        }
        public void OnPointerClick(PointerEventData eventData) { /* 背景点击不再取消，玩家可自由在 Plane 上绘制。 */ }
        private static Image MakeImage(Transform parent, string name, Color color) { var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); var image = go.GetComponent<Image>(); image.color = color; return image; }
        private TMP_Text MakeText(Transform parent, string name, string value, float size, Color color) { var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); var text = go.GetComponent<TextMeshProUGUI>(); text.text = value; text.font = TMP_Settings.defaultFontAsset; text.fontSize = size; text.color = color; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; return text; }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
