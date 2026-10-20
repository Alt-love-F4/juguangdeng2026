using System;
using System.Collections;
using System.Collections.Generic;
using Spotlight.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>拉花工作台：复用现有 Plane 液面，不创建新场景。杯面按镜头居中缩放，左侧为参考图案，右侧为杯型与奶缸信息。</summary>
    public sealed class FreeCafeLatteArtBench : MonoBehaviour, ICafeMiniGamePresenter, ICafeLatteArtResultPresenter, IPointerClickHandler
    {
        public FreeCafeSubSceneDock dock;
        [Tooltip("仅在关闭 useGeneratedTable 时作为背景；默认使用程序生成的胡桃木桌面。")]
        public Sprite woodBackground;
        public bool useGeneratedTable = true;
        [Tooltip("为空时使用内置的四个日常图案与四个势力徽记图案；可在 Inspector 传入自己的预览图和模板类型。")]
        public LatteArtPatternPreset[] patternPresets;
        public string LastPatternId { get; private set; }
        [Header("Latte presentation")]
        [Tooltip("杯碟（含把手）最多占用镜头高度的比例。")]
        [Range(.65f, 1f)] public float layoutHeightFraction = .9f;
        [Min(.05f)] public float transitionDuration = .28f;
        [Min(.05f)] public float recognitionInterval = .15f;
        [Tooltip("标准线宽与流量下每秒消耗的奶泡ml；实际消耗随线宽平方及流量倍率变化。")]
        [Min(.1f)] public float foamMlPerSecond = 6;
        [Tooltip("拉花界面液面底色亮度，不影响杯子颜色或奶泡白色；越小越深。")]
        [Range(.2f, 1f)] public float liquidColorBrightness = 1f;
        [Tooltip("杯面描线参考的不透明度；0 关闭。玩家可在界面中开关。")]
        [Range(0f, .6f)] public float guideOpacity = .26f;
        public bool IsTransitioning => transition != null;
        /// <summary>True when every faction pattern is available because CoffeeShop runs without the formal game flow.</summary>
        public bool UnlocksAllPatterns => SceneManager.GetActiveScene().name == "CoffeeShop" && FindFirstObjectByType<GameFlowBootstrap>() == null;

        // Overlay layout in viewport fractions; the cup is fitted between the two columns.
        private const float LeftColumnMax = .262f, RightColumnMin = .772f, CupCenterY = .53f;
        private static readonly Color Cream = new Color(.98f, .91f, .79f);
        private static readonly Color Espresso = new Color(.12f, .078f, .055f, .97f);
        private static readonly Color Card = new Color(.20f, .13f, .095f);
        private static readonly Color CardRaised = new Color(.26f, .17f, .12f);
        private static readonly Color Caramel = new Color(.86f, .63f, .37f);
        private static readonly Color Muted = new Color(.76f, .67f, .58f);

        private sealed class PatternEntry
        {
            public LatteArtPatternPreset preset;
            public bool unlocked;
            public int fitIndex = -1;
            public Image button;
            public TMP_Text fit;
            public Image fitBar;
        }

        private Coroutine transition;
        private CanvasGroup contentGroup;
        private Image curtain;
        private SpriteRenderer backdrop, cupArt;
        private Sprite backdropSprite;
        private readonly List<PatternEntry> entries = new List<PatternEntry>();
        private readonly List<LatteArtRecognitionResult> patternFits = new List<LatteArtRecognitionResult>();
        private readonly List<LatteArtPatternPreset> activePresets = new List<LatteArtPatternPreset>();
        private string bestFitId;
        private string selectedPreset;
        private float nextRecognition;
        private int lastPointCount = -1;
        private LatteArtRecognitionResult latestRecognition;
        private Button submitButton;
        private TMP_Text authoredCaption;
        private bool captionWasEnabled;

        private string activeToken;
        private Action<string, bool, float> completion;
        private ILatteArtSurface effect;
        private Canvas overlay;
        private GameObject overlayRoot;
        private RectTransform contentRect;
        private TMP_Text status;
        private TMP_Text foamUsage;
        private TMP_Text cupFill;
        private RawImage guide;
        private TMP_Text guideLabel;
        private bool guideVisible = true;
        private FreeCafeRuntime runtime;
        private bool finishing;
        private bool dockWasDocked, layoutDockWasDocked;
        private FreeCafeSubSceneDock layoutDock;
        private Transform layoutRoot;
        private SpriteRenderer background;
        private Transform surfaceTransform;
        private Vector3 authoredLayoutPosition, authoredLayoutScale;
        private Vector3 authoredSurfacePosition, authoredSurfaceScale;
        private bool surfaceFitted;
        private LatteCupStyle cupStyle;
        private int viewWidth, viewHeight;
        private float viewSize, viewAspect;
        private Vector3 viewPosition;
        private Vector3 bowlCenter;
        private float bowlRadius;

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
            CaptureSurface();
        }

        public void Begin(string kind, string cupId, string token, Action<string, bool, float> complete)
        {
            if (transition != null) { StopCoroutine(transition); transition = null; RestoreLayout(); }
            if (!string.IsNullOrEmpty(activeToken)) Cancel(activeToken);
            if (transition != null) { StopCoroutine(transition); transition = null; RestoreLayout(); }
            activeToken = token; completion = complete; LastPatternId = null; finishing = false;
            lastPointCount = -1; nextRecognition = 0; latestRecognition = default;
            effect = FindSurface();
            CaptureSurface();
            var gateway = FindFirstObjectByType<FreeCafeMiniGameGateway>();
            runtime = gateway != null ? gateway.runtime : null;
            var cup = runtime != null && runtime.World != null ? runtime.World.Find(cupId) : null;
            cupStyle = FreeCafeLatteArtStage.StyleFor(cup != null ? cup.specId : null);
            if (effect is ILatteArtFoamSurface foamSurface) foamSurface.SetFoamConsumer(ConsumeFoam, foamMlPerSecond);
            if (effect is ILatteArtFeedbackSurface feedback) feedback.SetResetHandler(ResetDrawing);
            RefreshLiquidColor(cupId);
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
            if (effect is ILatteArtFoamSurface foamSurface) foamSurface.SetFoamConsumer(null, foamMlPerSecond);
            if (effect is ILatteArtFeedbackSurface feedback) feedback.SetResetHandler(null);
            if (effect != null) effect.SetInteractionEnabled(false);
            if (transition != null) { StopCoroutine(transition); transition = null; }
            if (isActiveAndEnabled && overlayRoot != null) transition = StartCoroutine(TransitionRoutine(false));
            else { RestoreLayout(); DestroyOverlay(); }
        }

        private void OnDisable()
        {
            var cancel = completion; var token = activeToken;
            if (!string.IsNullOrEmpty(token))
            {
                if(cancel!=null)cancel(token,false,0);
                if(activeToken==token)Cancel(token);
            }
            if (transition != null) { StopCoroutine(transition); transition = null; }
            RestoreLayout(); DestroyOverlay();
        }

        private void LateUpdate()
        {
            if (string.IsNullOrEmpty(activeToken) || IsTransitioning) return;
            Camera camera = Camera.main;
            if (camera != null && (viewWidth != camera.pixelWidth || viewHeight != camera.pixelHeight || !Mathf.Approximately(viewSize, camera.orthographicSize)
                || !Mathf.Approximately(viewAspect, camera.aspect) || viewPosition != camera.transform.position))
                FitBackgroundToView();
            UpdateGuide(camera);
        }

        private void CaptureSurface()
        {
            if (surfaceFitted || !(effect is Component component)) return;
            surfaceTransform = component.transform;
            authoredSurfacePosition = surfaceTransform.localPosition;
            authoredSurfaceScale = surfaceTransform.localScale;
        }

        /// <summary>Centres the existing liquid Plane between the side columns and sizes it from the camera,
        /// so the whole round surface (and its cup) is always on screen regardless of zoom or aspect.</summary>
        private void FitBackgroundToView()
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic || layoutRoot == null) return;
            layoutRoot.localScale = authoredLayoutScale;
            float height = camera.orthographicSize * 2, width = height * camera.aspect;
            var mesh = surfaceTransform != null ? surfaceTransform.GetComponent<MeshFilter>() : null;
            if (mesh != null && mesh.sharedMesh != null)
            {
                surfaceTransform.localScale = authoredSurfaceScale;
                surfaceTransform.localPosition = authoredSurfacePosition;
                surfaceFitted = true;
                float footprint = Mathf.Min(height * layoutHeightFraction, width * (RightColumnMin - LeftColumnMax) * .95f);
                // Every cup shares one base size so a small cup visibly reads smaller than a mug.
                float baseRadius = footprint * .5f / FreeCafeLatteArtStage.FootprintRadius(LatteCupStyle.Large);
                bowlRadius = baseRadius * FreeCafeLatteArtStage.BowlScale(cupStyle);
                // Plane UVs span the mesh; the drawable bowl is 0.46 of that width (LatteArtPrototype.BowlRadius).
                float current = mesh.sharedMesh.bounds.size.x * Mathf.Abs(surfaceTransform.lossyScale.x) * .46f;
                if (current > .0001f) surfaceTransform.localScale = authoredSurfaceScale * (bowlRadius / current);
                Vector3 target = camera.ViewportToWorldPoint(new Vector3((LeftColumnMax + RightColumnMin) * .5f, CupCenterY, 0));
                target.z = surfaceTransform.position.z;
                surfaceTransform.position = target;
                bowlCenter = target;
                EnsureCupArt();
            }
            EnsureBackdrop(camera, height);
            viewWidth = camera.pixelWidth; viewHeight = camera.pixelHeight; viewSize = camera.orthographicSize; viewAspect = camera.aspect;
            viewPosition = camera.transform.position;
        }

        private void Update()
        {
            UpdateFoamUsage();
            if (string.IsNullOrEmpty(activeToken) || effect == null || status == null || IsTransitioning) return;
            if (Time.unscaledTime < nextRecognition) return;
            nextRecognition = Time.unscaledTime + recognitionInterval;
            if (lastPointCount != effect.StrokePointCount)
            {
                lastPointCount = effect.StrokePointCount;
                latestRecognition = effect.Recognize(activePresets);
                UpdatePatternFits();
            }
            var recognition = latestRecognition;
            string display = "自由绘制";
            foreach (var preset in activePresets) if (preset.id == recognition.patternId) { display = preset.displayName; break; }
            status.text = recognition.HasPattern ? "最像「" + display + "」\n相似度 " + (recognition.confidence * 100f).ToString("0") + "%" : effect.StrokePointCount >= 5 ? "自由创作中 · 尚未匹配参考\n可以继续绘制，也可以直接提交" : "按住左键，慢慢画出图案\n松手可分笔继续";
            LastPatternId = recognition.HasPattern ? recognition.patternId : null;
            if (submitButton != null) submitButton.interactable = effect.StrokePointCount >= 5;
        }

        private float ConsumeFoam(float requested)
        {
            if (runtime == null || runtime.World == null || finishing || IsTransitioning) return 0;
            float accepted = runtime.World.ConsumeLatteArtFoam(activeToken, requested);
            if (accepted > 0) RefreshLiquidColor(runtime.World.State.production.miniGameCupId);
            return accepted;
        }

        private void ResetDrawing()
        {
            if (finishing || IsTransitioning || string.IsNullOrEmpty(activeToken)) return;
            var session = runtime != null && runtime.World != null ? runtime.World.State.production : null;
            if (session != null && session.miniGameToken == activeToken && !string.IsNullOrEmpty(session.miniGamePitcherId)
                && !runtime.World.ResetLatteArtFoam(activeToken)) return;
            effect?.ResetLatte();
            LastPatternId = null; lastPointCount = -1; nextRecognition = 0; latestRecognition = default;
            if (submitButton != null) submitButton.interactable = false;
            UpdatePatternFits();
            if (session != null) RefreshLiquidColor(session.miniGameCupId);
            UpdateFoamUsage();
        }

        private void RefreshLiquidColor(string cupId)
        {
            if (!(effect is ILatteArtFeedbackSurface feedback) || runtime == null || runtime.World == null) return;
            var cup = runtime.World.Find(cupId);
            var view = runtime.View(cupId);
            var liquid = view != null ? view.GetComponentInChildren<OrthographicGlassCupPresenter>(true) : null;
            if (cup == null || liquid == null) return;
            liquid.Present(runtime.World.LiquidSnapshot(cup), 0, false);
            // Match the drink's body color, not the whitened rim highlight.
            Color color = liquid.MixedColor;
            color.r *= liquidColorBrightness;
            color.g *= liquidColorBrightness;
            color.b *= liquidColorBrightness;
            feedback.SetLiquidColor(color);
        }

        private void UpdatePatternFits()
        {
            patternFits.Clear();
            if (effect is ILatteArtFeedbackSurface feedback) feedback.GetPatternFits(activePresets, patternFits);
            bestFitId = null;
            float best = -1;
            foreach (var entry in entries)
            {
                if (!entry.unlocked || entry.fit == null) continue;
                float score = entry.fitIndex >= 0 && entry.fitIndex < patternFits.Count ? patternFits[entry.fitIndex].confidence : 0;
                entry.fit.text = "拟合 " + (score * 100f).ToString("0") + "%";
                if (entry.fitBar != null) entry.fitBar.fillAmount = score;
                if (effect != null && effect.StrokePointCount >= 5 && score > best) { best = score; bestFitId = entry.preset.id; }
            }
            RefreshPresetHighlights();
        }

        private void UpdateFoamUsage()
        {
            if (foamUsage == null || runtime == null || runtime.World == null) return;
            var world = runtime.World; var session = world.State.production;
            if (session == null || session.miniGameToken != activeToken) return;
            var cup = world.Find(session.miniGameCupId);
            if (cupFill != null && cup != null)
                cupFill.text = "杯中 " + world.LiquidVolume(cup).ToString("0") + " / " + world.LiquidCapacity(cup).ToString("0") + " ml";
            if (string.IsNullOrEmpty(session.miniGamePitcherId)) { foamUsage.text = "GM 调试模式 · 不消耗奶泡"; return; }
            var pitcher = world.Find(session.miniGamePitcherId);
            float remaining = world.MilkFoamVolume(pitcher);
            float space = Mathf.Max(0, world.LiquidCapacity(cup) - world.LiquidVolume(cup));
            foamUsage.text = "已用奶泡 <b>" + session.miniGameFoamUsedMl.ToString("0.0") + "</b> ml\n奶缸剩余 <b>" + remaining.ToString("0.0") + "</b> ml"
                + (remaining <= .001f ? "\n<color=#F2B880>奶泡已用完，请提交或返回</color>" : space <= .001f ? "\n<color=#F2B880>杯子已满，已停止加奶泡</color>"
                    : "\n<size=82%><color=#BFAE9C>重画会清空杯中奶泡，不退回奶缸；返回吧台保留当前用量</color></size>");
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
            entries.Clear(); activePresets.Clear();
            var source = new List<LatteArtPatternPreset>();
            if (patternPresets != null) foreach (var preset in patternPresets) if (preset != null && preset.template != LatteArtTemplateKind.None) source.Add(preset);
            if (source.Count == 0) source.AddRange(BuiltinPresets());
            bool unlockAll = UnlocksAllPatterns;
            var state = WorldFactionCatalog.CurrentState;
            foreach (var preset in source)
            {
                var faction = string.IsNullOrEmpty(preset.factionId) ? null : WorldFactionCatalog.Find(preset.factionId);
                var entry = new PatternEntry { preset = preset, unlocked = faction == null || unlockAll || WorldFactionCatalog.IsEncountered(faction, state) };
                if (entry.unlocked) { entry.fitIndex = activePresets.Count; activePresets.Add(preset); }
                entries.Add(entry);
            }
        }

        /// <summary>Four everyday patterns without political meaning, then one emblem per faction.</summary>
        public static LatteArtPatternPreset[] BuiltinPresets() => new[]
        {
            Preset("heart", "爱心", LatteArtTemplateKind.Heart, null,
                "最受欢迎的入门图案。圆润饱满的一颗心，只是单纯地祝客人今天过得愉快。", "从下方尖角起笔，左右各画一道圆弧，在顶部凹口收拢。"),
            Preset("tulip", "郁金香", LatteArtTemplateKind.Tulip, null,
                "层层叠起的花苞，考验注奶时的节奏。一朵花，就只是一朵花。", "先拉出花茎，再画左右两片花瓣，在中央合拢。"),
            Preset("rosetta", "树叶", LatteArtTemplateKind.Rosetta, null,
                "左右摆动拉出叶脉，最后一笔收尖。咖啡馆最常见的招牌图案，不带任何立场。", "自下而上拉出叶柄，再左右来回摆动画出叶片。"),
            Preset("bean", "咖啡豆", LatteArtTemplateKind.Bean, null,
                "一颗饱满的咖啡豆，中间一道浅浅的豆缝。沸点咖啡馆的老规矩：先把咖啡做好。", "先画一个竖长的椭圆，再从上到下画一道弯弯的豆缝。"),
            Preset("sword_crown", "剑与王冠", LatteArtTemplateKind.SwordCrown, WorldFactionCatalog.Court,
                "奥尔狄亚王庭的徽记：一柄长剑贯穿王冠，象征对皇室的效忠与神佑者统治王国的传统。\n端出这样一杯咖啡，几乎等于一句表态——有人会会心一笑，也有人会皱起眉头。",
                "先画出王冠的锯齿轮廓，再从上到下拉出一道笔直的剑身。"),
            Preset("concord_ring", "圣谐之环", LatteArtTemplateKind.ConcordRing, WorldFactionCatalog.Church,
                "圣谐教会的圣环：外环是包容一切的秩序，中央的菱形是信仰的核心。\n对信众而言，它代表庇护与秩序；在另一些人眼里，它也是监察司的影子。",
                "先画一个完整的圆，再在圆心画一个竖立的菱形。"),
            Preset("new_rose", "五瓣玫瑰", LatteArtTemplateKind.NewRose, WorldFactionCatalog.Rose,
                "传说「玫瑰王」挥剑劈开之处，会生出血红色的玫瑰。如今这朵五瓣玫瑰只在知情人之间流传，是心照不宣的暗号。",
                "沿着五片花瓣绕花心画一圈，再从花朵下方拉出花茎。"),
            Preset("broken_chain", "断链", LatteArtTemplateKind.BrokenChain, WorldFactionCatalog.Revolution,
                "挣断的锁链，革命联合的标志。它属于厌倦了贫困与不公的人——锁链既已断开，就不必再回到原来的样子。",
                "画出两节相对的链环，开口朝向彼此，中间留出断口。"),
        };
        private static LatteArtPatternPreset Preset(string id, string label, LatteArtTemplateKind template, string faction, string meaning, string tip)
            => new LatteArtPatternPreset { id = id, displayName = label, template = template, factionId = faction, meaning = meaning, drawingTip = tip };

        private void RestoreLayout()
        {
            if (surfaceTransform != null && surfaceFitted)
            {
                surfaceTransform.localPosition = authoredSurfacePosition;
                surfaceTransform.localScale = authoredSurfaceScale;
                surfaceFitted = false;
            }
            if (layoutRoot == null || overlayRoot == null) return;
            if (dock != null) dock.SetDocked(dockWasDocked, true);
            if (layoutDock != null) layoutDock.SetDocked(layoutDockWasDocked, true);
            layoutRoot.localPosition = authoredLayoutPosition;
            layoutRoot.localScale = authoredLayoutScale;
            if (backdrop != null) backdrop.gameObject.SetActive(false);
            if (cupArt != null) cupArt.gameObject.SetActive(false);
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
                UpdateGuide(Camera.main);
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
        private int BaseSortingOrder => background != null ? background.sortingOrder : 0;
        private int BaseSortingLayer => background != null ? background.sortingLayerID : 0;
        private void EnsureBackdrop(Camera camera, float height)
        {
            if (backdrop == null)
            {
                var go = new GameObject("LatteBackdrop"); go.transform.SetParent(layoutRoot, false);
                backdrop = go.AddComponent<SpriteRenderer>();
                backdrop.sortingLayerID = BaseSortingLayer;
                backdrop.sortingOrder = BaseSortingOrder - 1;
            }
            Sprite sprite = useGeneratedTable || woodBackground == null
                ? FreeCafeLatteArtStage.Table(new Vector2((LeftColumnMax + RightColumnMin) * .5f, CupCenterY)) : woodBackground;
            if (sprite == null)
            {
                if (backdropSprite == null) backdropSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
                sprite = backdropSprite;
            }
            backdrop.sprite = sprite;
            backdrop.color = sprite == backdropSprite ? new Color(.22f, .13f, .08f) : Color.white;
            backdrop.gameObject.SetActive(true);
            Vector3 center = camera.transform.position;
            center.z = surfaceTransform != null ? surfaceTransform.position.z + .05f : layoutRoot.position.z + 1;
            backdrop.transform.position = center;
            var scale = layoutRoot.lossyScale;
            var spriteSize = backdrop.sprite.bounds.size;
            backdrop.transform.localScale = new Vector3(height * camera.aspect / Mathf.Abs(scale.x) / spriteSize.x * 1.01f,
                height / Mathf.Abs(scale.y) / spriteSize.y * 1.01f, 1);
        }
        private void EnsureCupArt()
        {
            if (cupArt == null)
            {
                var go = new GameObject("LatteCupArt"); go.transform.SetParent(layoutRoot, false);
                cupArt = go.AddComponent<SpriteRenderer>();
                cupArt.sortingLayerID = BaseSortingLayer;
                // Above the tabletop, always below the liquid surface (LatteArtPrototype keeps it at >= 1000).
                cupArt.sortingOrder = BaseSortingOrder + 10;
            }
            cupArt.sprite = FreeCafeLatteArtStage.Cup(cupStyle);
            cupArt.gameObject.SetActive(true);
            Vector3 center = bowlCenter; center.z += .02f;
            cupArt.transform.position = center;
            var scale = layoutRoot.lossyScale;
            // Cup sprites use one bowl radius per world unit.
            cupArt.transform.localScale = new Vector3(bowlRadius / Mathf.Abs(scale.x), bowlRadius / Mathf.Abs(scale.y), 1);
        }
        private void OnDestroy()
        {
            if (backdropSprite != null) Destroy(backdropSprite);
        }

        private void UpdateGuide(Camera camera)
        {
            if (guide == null || contentRect == null) return;
            var preset = FindEntry(selectedPreset);
            bool show = guideVisible && guideOpacity > 0 && preset != null && preset.unlocked && camera != null && surfaceFitted;
            guide.gameObject.SetActive(show);
            if (!show) return;
            Vector2 center = camera.WorldToScreenPoint(bowlCenter), edge = camera.WorldToScreenPoint(bowlCenter + Vector3.right * bowlRadius);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRect, center, null, out Vector2 localCenter)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRect, edge, null, out Vector2 localEdge)) return;
            float size = Mathf.Abs(localEdge.x - localCenter.x) * 2 * .78f;
            guide.rectTransform.anchoredPosition = localCenter;
            guide.rectTransform.sizeDelta = new Vector2(size, size);
            guide.color = new Color(1, 1, 1, guideOpacity);
        }
        private void ToggleGuide()
        {
            guideVisible = !guideVisible;
            if (guideLabel != null) guideLabel.text = guideVisible ? "描线参考 · 开" : "描线参考 · 关";
            UpdateGuide(Camera.main);
        }

        private void BuildOverlay()
        {
            DestroyOverlay(); patternFits.Clear(); selectedPreset = bestFitId = null;
            overlayRoot = new GameObject("LatteArtPresetOverlay", typeof(RectTransform));
            overlay = overlayRoot.AddComponent<Canvas>(); overlay.renderMode = RenderMode.ScreenSpaceOverlay; overlay.sortingOrder = 900;
            var scaler = overlayRoot.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            overlayRoot.AddComponent<GraphicRaycaster>();
            var content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
            content.transform.SetParent(overlayRoot.transform, false); contentRect = content.GetComponent<RectTransform>(); Stretch(contentRect);
            contentGroup = content.GetComponent<CanvasGroup>(); contentGroup.alpha = 0;

            // Faint tracing guide drawn over the liquid; never a raycast target so it cannot block pouring.
            var guideObject = new GameObject("ReferenceGuide", typeof(RectTransform), typeof(RawImage));
            guideObject.transform.SetParent(content.transform, false);
            guide = guideObject.GetComponent<RawImage>(); guide.raycastTarget = false;
            guide.rectTransform.anchorMin = guide.rectTransform.anchorMax = new Vector2(.5f, .5f);
            guideObject.SetActive(false);

            BuildPatternPanel(content.transform, out RectTransform hintRect, out TMP_Text hintTitle, out TMP_Text hintBody,
                out TMP_Text hintSubtitle, out TMP_Text hintTip, out Image hintEmblem);
            BuildRightColumn(content.transform);

            var help = MakeText(content.transform, "DrawingHint", "按住左键注奶   ·   松手可分笔   ·   滚轮调整线宽   ·   R 重画", 17, new Color(.96f, .89f, .80f, .85f));
            SetRect(help.rectTransform, new Vector2(LeftColumnMax + .01f, .018f), new Vector2(RightColumnMin - .01f, .062f)); help.alignment = TextAlignmentOptions.Center;

            // Hover card sits last so it renders above both columns.
            hintRect.SetAsLastSibling();
            SelectReference(FirstUnlockedId());
            curtain = MakeImage(overlayRoot.transform, "TransitionCurtain", new Color(.15f, .075f, .045f, 0)); Stretch(curtain.rectTransform);
        }

        private void BuildPatternPanel(Transform content, out RectTransform hintRect, out TMP_Text hintTitle, out TMP_Text hintBody,
            out TMP_Text hintSubtitle, out TMP_Text hintTip, out Image hintEmblem)
        {
            var panel = MakeCard(content, "PresetPanel", Espresso);
            SetRect(panel.rectTransform, new Vector2(.022f, .06f), new Vector2(LeftColumnMax, .94f));
            AddShadow(panel.gameObject, new Color(0, 0, 0, .4f), new Vector2(0, -8));
            var eyebrow = MakeText(panel.transform, "Eyebrow", "LATTE ART  ·  拉花工作台", 13, Caramel);
            SetRect(eyebrow.rectTransform, new Vector2(.08f, .93f), new Vector2(.92f, .968f)); eyebrow.alignment = TextAlignmentOptions.Left;
            eyebrow.characterSpacing = 4;
            var title = MakeText(panel.transform, "Title", "在杯面上作画", 30, Cream);
            SetRect(title.rectTransform, new Vector2(.08f, .868f), new Vector2(.92f, .93f)); title.alignment = TextAlignmentOptions.Left;
            title.fontStyle = FontStyles.Bold;
            var subtitle = MakeText(panel.transform, "Subtitle", "选一个图案作参考，按住左键慢慢注奶", 15, Muted);
            SetRect(subtitle.rectTransform, new Vector2(.08f, .832f), new Vector2(.92f, .868f)); subtitle.alignment = TextAlignmentOptions.Left;
            var divider = MakeImage(panel.transform, "Divider", new Color(Caramel.r, Caramel.g, Caramel.b, .35f)); divider.raycastTarget = false;
            SetRect(divider.rectTransform, new Vector2(.08f, .818f), new Vector2(.92f, .82f));
            var referenceLabel = MakeText(panel.transform, "ReferenceLabel", "参考图案", 15, Caramel);
            SetRect(referenceLabel.rectTransform, new Vector2(.08f, .772f), new Vector2(.6f, .808f)); referenceLabel.alignment = TextAlignmentOptions.Left;
            int unlocked = 0; foreach (var entry in entries) if (entry.unlocked) unlocked++;
            var count = MakeText(panel.transform, "UnlockCount", "已解锁 " + unlocked + " / " + entries.Count, 13, Muted);
            SetRect(count.rectTransform, new Vector2(.5f, .772f), new Vector2(.92f, .808f)); count.alignment = TextAlignmentOptions.Right;

            var listObject = new GameObject("PresetList", typeof(RectTransform)); listObject.transform.SetParent(panel.transform, false);
            SetRect(listObject.GetComponent<RectTransform>(), new Vector2(.06f, .3f), new Vector2(.94f, .765f));
            var list = listObject.AddComponent<VerticalLayoutGroup>(); list.spacing = 6; list.childControlHeight = true;
            list.childControlWidth = true; list.childForceExpandHeight = true;

            var hint = MakeCard(content, "PatternMeaning", new Color(.97f, .91f, .80f));
            hint.raycastTarget = false;
            AddShadow(hint.gameObject, new Color(0, 0, 0, .35f), new Vector2(0, -6));
            var outline = hint.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.55f, .36f, .18f, .6f); outline.effectDistance = new Vector2(1.5f, -1.5f);
            hintRect = hint.rectTransform; hintRect.anchorMin = hintRect.anchorMax = new Vector2(.5f, .5f);
            hintRect.pivot = new Vector2(0, 1); hintRect.sizeDelta = new Vector2(440, 320);
            var ink = new Color(.28f, .16f, .10f);
            hintEmblem = new GameObject("Emblem", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            hintEmblem.transform.SetParent(hint.transform, false); hintEmblem.preserveAspect = true; hintEmblem.raycastTarget = false;
            var emblemRect = hintEmblem.rectTransform; emblemRect.anchorMin = emblemRect.anchorMax = new Vector2(0, 1); emblemRect.pivot = new Vector2(0, 1);
            emblemRect.sizeDelta = new Vector2(60, 60); emblemRect.anchoredPosition = new Vector2(20, -18);
            hintTitle = MakeText(hint.transform, "PatternName", "", 23, ink); hintTitle.fontStyle = FontStyles.Bold;
            Pin(hintTitle.rectTransform, -18, 34); hintTitle.alignment = TextAlignmentOptions.BottomLeft;
            hintSubtitle = MakeText(hint.transform, "Faction", "", 14, new Color(.55f, .33f, .14f));
            Pin(hintSubtitle.rectTransform, -54, 26); hintSubtitle.alignment = TextAlignmentOptions.TopLeft;
            hintBody = MakeText(hint.transform, "Meaning", "", 16, ink); hintBody.lineSpacing = 6;
            SetRect(hintBody.rectTransform, new Vector2(0, .17f), new Vector2(1, 1)); hintBody.rectTransform.offsetMin = new Vector2(22, 0); hintBody.rectTransform.offsetMax = new Vector2(-22, -92);
            hintBody.alignment = TextAlignmentOptions.TopLeft;
            hintTip = MakeText(hint.transform, "Tip", "", 14, new Color(.47f, .29f, .14f));
            SetRect(hintTip.rectTransform, new Vector2(0, 0), new Vector2(1, .17f)); hintTip.rectTransform.offsetMin = new Vector2(22, 12); hintTip.rectTransform.offsetMax = new Vector2(-22, 0);
            hintTip.alignment = TextAlignmentOptions.BottomLeft; hintTip.fontStyle = FontStyles.Italic;
            hint.gameObject.SetActive(false);

            foreach (var entry in entries) BuildPatternRow(list.transform, entry, hintRect, hintTitle, hintBody, hintSubtitle, hintTip, hintEmblem);

            var statusCard = MakeCard(panel.transform, "StatusCard", Card);
            SetRect(statusCard.rectTransform, new Vector2(.06f, .155f), new Vector2(.94f, .275f));
            status = MakeText(statusCard.transform, "Status", "准备好后，在杯面上慢慢画出图案", 16, new Color(.94f, .85f, .75f));
            Stretch(status.rectTransform); status.rectTransform.offsetMin = new Vector2(16, 10); status.rectTransform.offsetMax = new Vector2(-16, -10); status.alignment = TextAlignmentOptions.Center;
            MakeButton(panel.transform, "Cancel", "返回吧台", new Vector2(.06f, .035f), new Vector2(.94f, .11f), () => completion?.Invoke(activeToken, false, 0), false);
        }

        private void BuildPatternRow(Transform list, PatternEntry entry, RectTransform hintRect, TMP_Text hintTitle, TMP_Text hintBody,
            TMP_Text hintSubtitle, TMP_Text hintTip, Image hintEmblem)
        {
            var preset = entry.preset;
            var faction = string.IsNullOrEmpty(preset.factionId) ? null : WorldFactionCatalog.Find(preset.factionId);
            var row = MakeCard(list, preset.id + "Button", CardRaised);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
            entry.button = row;
            var button = row.gameObject.AddComponent<Button>();
            ConfigureButton(button);
            button.interactable = entry.unlocked;
            var cup = MakeImage(row.transform, "PreviewCup", new Color(.31f, .17f, .09f)); cup.sprite = FreeCafeLatteArtStage.Disk(); cup.raycastTarget = false;
            var cupRect = cup.rectTransform; cupRect.anchorMin = cupRect.anchorMax = new Vector2(0, .5f); cupRect.pivot = new Vector2(0, .5f);
            cupRect.sizeDelta = new Vector2(42, 42); cupRect.anchoredPosition = new Vector2(10, 0);
            var ring = cup.gameObject.AddComponent<Outline>(); ring.effectColor = new Color(.93f, .86f, .72f, .55f); ring.effectDistance = new Vector2(1.2f, -1.2f);
            var preview = new GameObject("Preview", typeof(RectTransform), typeof(RawImage)); preview.transform.SetParent(cup.transform, false);
            var raw = preview.GetComponent<RawImage>();
            raw.texture = preset.preview != null ? preset.preview : FreeCafeLatteArtStage.Glyph(preset.template, 96, 7, Color.white);
            raw.color = entry.unlocked ? new Color(1, .97f, .9f, .95f) : new Color(1, 1, 1, .14f);
            raw.raycastTarget = false;
            Stretch((RectTransform)preview.transform); ((RectTransform)preview.transform).offsetMin = Vector2.one * 3; ((RectTransform)preview.transform).offsetMax = -Vector2.one * 3;
            var label = MakeText(row.transform, "Label", entry.unlocked ? preset.displayName : "？？？", 18, entry.unlocked ? Cream : Muted);
            PinLeft(label.rectTransform, 64, 170, .5f, 1f, 1, 4); label.alignment = TextAlignmentOptions.BottomLeft;
            var fit = MakeText(row.transform, "Fit", entry.unlocked ? "拟合 0%" : "未解锁", 13, new Color(.82f, .73f, .65f));
            PinLeft(fit.rectTransform, 64, 70, 0f, .5f, 5, 1); fit.alignment = TextAlignmentOptions.TopLeft;
            entry.fit = fit;
            if (entry.unlocked)
            {
                var track = MakeImage(row.transform, "FitTrack", new Color(0, 0, 0, .28f)); track.raycastTarget = false;
                var trackRect = track.rectTransform; trackRect.anchorMin = trackRect.anchorMax = new Vector2(0, .27f); trackRect.pivot = new Vector2(0, .5f);
                trackRect.sizeDelta = new Vector2(72, 4); trackRect.anchoredPosition = new Vector2(136, 0);
                entry.fitBar = MakeImage(track.transform, "FitBar", Caramel); entry.fitBar.raycastTarget = false;
                entry.fitBar.type = Image.Type.Filled; entry.fitBar.fillMethod = Image.FillMethod.Horizontal; entry.fitBar.fillAmount = 0;
                entry.fitBar.sprite = FreeCafeLatteArtStage.RoundedRect();
                Stretch(entry.fitBar.rectTransform);
            }
            var best = MakeText(row.transform, "Best", "最佳", 13, new Color(1f, .86f, .40f));
            SetRect(best.rectTransform, new Vector2(.62f, .5f), new Vector2(.84f, .96f)); best.alignment = TextAlignmentOptions.Right;
            best.gameObject.SetActive(false);
            var selected = MakeText(row.transform, "Selected", "参考", 13, Cream);
            SetRect(selected.rectTransform, new Vector2(.62f, .04f), new Vector2(.84f, .5f)); selected.alignment = TextAlignmentOptions.Right;
            selected.gameObject.SetActive(false);
            Sprite emblem = faction != null ? WorldFactionCatalog.Emblem(faction) : null;
            if (emblem != null)
            {
                var badge = new GameObject("Emblem", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                badge.transform.SetParent(row.transform, false); badge.sprite = emblem; badge.preserveAspect = true; badge.raycastTarget = false;
                badge.color = entry.unlocked ? Color.white : new Color(0, 0, 0, .35f);
                var rect = badge.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(1, .5f); rect.pivot = new Vector2(1, .5f);
                rect.sizeDelta = new Vector2(34, 34); rect.anchoredPosition = new Vector2(-10, 0);
            }
            string id = preset.id;
            button.onClick.AddListener(() => SelectReference(id));
            var hover = row.gameObject.AddComponent<CafeLattePatternHint>();
            hover.panel = hintRect; hover.heading = hintTitle; hover.body = hintBody;
            hover.subtitleLabel = hintSubtitle; hover.tipLabel = hintTip; hover.emblemImage = hintEmblem;
            if (entry.unlocked)
            {
                hover.patternName = preset.displayName;
                hover.meaning = string.IsNullOrWhiteSpace(preset.meaning) ? "一个自由的图案，可以随意尝试。" : preset.meaning;
                hover.subtitle = faction != null ? faction.Name + " · 徽记图案" : "日常图案 · 没有额外含义";
                hover.tip = string.IsNullOrWhiteSpace(preset.drawingTip) ? string.Empty : "画法：" + preset.drawingTip;
                hover.emblem = emblem;
            }
            else
            {
                hover.patternName = "尚未解锁的图案";
                hover.meaning = "杯面上隐约浮现一个陌生的轮廓，你还说不出它的来历。\n在王都结识更多的人、听闻更多的事之后，也许就能认出这个印记。";
                hover.subtitle = "未知的印记";
                hover.tip = string.Empty;
                hover.emblem = null;
            }
        }

        private void BuildRightColumn(Transform content)
        {
            var cup = runtime != null && runtime.World != null ? runtime.World.Find(runtime.World.State.production.miniGameCupId) : null;
            var cupCard = MakeCard(content, "CupCard", Espresso);
            SetRect(cupCard.rectTransform, new Vector2(RightColumnMin + .006f, .585f), new Vector2(.978f, .94f));
            AddShadow(cupCard.gameObject, new Color(0, 0, 0, .4f), new Vector2(0, -6));
            var header = MakeText(cupCard.transform, "Header", "当前杯型", 14, Caramel);
            SetRect(header.rectTransform, new Vector2(.08f, .86f), new Vector2(.92f, .95f)); header.alignment = TextAlignmentOptions.Left;
            var spec = cup != null ? runtime.World.Spec(cup.specId) : null;
            string cupName = spec != null && !string.IsNullOrEmpty(spec.label) ? spec.label : FreeCafeLatteArtStage.StyleName(cupStyle);
            var icon = new GameObject("CurrentCup", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            icon.transform.SetParent(cupCard.transform, false); icon.raycastTarget = false;
            var cupSprite = FreeCafeLatteArtStage.Cup(cupStyle);
            icon.texture = cupSprite.texture;
            SetRect(icon.rectTransform, new Vector2(.04f, .30f), new Vector2(.5f, .86f));
            icon.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            var name = MakeText(cupCard.transform, "CupName", cupName, 24, Cream); name.fontStyle = FontStyles.Bold;
            SetRect(name.rectTransform, new Vector2(.52f, .58f), new Vector2(.96f, .78f)); name.alignment = TextAlignmentOptions.BottomLeft;
            cupFill = MakeText(cupCard.transform, "CupFill", cup != null ? "" : "调试杯", 14, Muted);
            SetRect(cupFill.rectTransform, new Vector2(.52f, .40f), new Vector2(.98f, .58f)); cupFill.alignment = TextAlignmentOptions.TopLeft;
            // The three overhead vessels on the counter; the current cup and the milk pitcher are highlighted.
            var vessels = new GameObject("OverheadVessels", typeof(RectTransform)).GetComponent<RectTransform>();
            vessels.SetParent(cupCard.transform, false); SetRect(vessels, new Vector2(.06f, .05f), new Vector2(.94f, .28f));
            for (int i = 0; i < 3; i++)
            {
                var vessel = new GameObject("Vessel" + i, typeof(RectTransform), typeof(CafeTopDownVesselGraphic));
                vessel.transform.SetParent(vessels, false);
                var graphic = vessel.GetComponent<CafeTopDownVesselGraphic>(); graphic.vessel = (CafeTopDownVesselGraphic.Vessel)i;
                graphic.selected = i == 2 || cup != null && (i == 0 ? cup.specId == "cup.small" : i == 1 && cup.specId == "cup.large");
                graphic.raycastTarget = false;
                SetRect((RectTransform)vessel.transform, new Vector2(i / 3f, .32f), new Vector2((i + 1) / 3f, 1f));
                var label = MakeText(vessels, "VesselName" + i, i == 0 ? "小杯" : i == 1 ? "大杯" : "奶缸", 13, graphic.selected ? Cream : Muted);
                SetRect(label.rectTransform, new Vector2(i / 3f, 0), new Vector2((i + 1) / 3f, .32f)); label.alignment = TextAlignmentOptions.Center;
            }

            var foamCard = MakeCard(content, "FoamCard", Espresso);
            SetRect(foamCard.rectTransform, new Vector2(RightColumnMin + .006f, .31f), new Vector2(.978f, .565f));
            AddShadow(foamCard.gameObject, new Color(0, 0, 0, .4f), new Vector2(0, -6));
            var foamHeader = MakeText(foamCard.transform, "Header", "奶缸", 14, Caramel);
            SetRect(foamHeader.rectTransform, new Vector2(.08f, .80f), new Vector2(.92f, .93f)); foamHeader.alignment = TextAlignmentOptions.Left;
            foamUsage = MakeText(content, "FoamUsage", "已用奶泡 0.0 ml", 18, Cream);
            SetRect(foamUsage.rectTransform, new Vector2(RightColumnMin + .02f, .325f), new Vector2(.966f, .52f)); foamUsage.alignment = TextAlignmentOptions.TopLeft;
            foamUsage.lineSpacing = 8;
            UpdateFoamUsage();

            var toggle = MakeButton(content, "GuideToggle", "描线参考 · 开", new Vector2(RightColumnMin + .006f, .215f), new Vector2(.978f, .275f), ToggleGuide, false);
            guideLabel = toggle.transform.Find("Label").GetComponent<TMP_Text>();
            guideVisible = guideOpacity > 0;
            guideLabel.text = guideVisible ? "描线参考 · 开" : "描线参考 · 关";
            submitButton = MakeButton(content, "Submit", "提交拉花", new Vector2(RightColumnMin + .006f, .115f), new Vector2(.978f, .195f), Finish, true);
            submitButton.interactable = false;
            MakeButton(content, "Reset", "删除拉花 · 重画", new Vector2(RightColumnMin + .006f, .035f), new Vector2(.978f, .095f), ResetDrawing, false);
        }

        private Button MakeButton(Transform parent, string name, string caption, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action, bool primary)
        {
            var image = MakeCard(parent, name, primary ? new Color(.70f, .40f, .19f) : new Color(.27f, .18f, .13f));
            SetRect(image.rectTransform, min, max);
            AddShadow(image.gameObject, new Color(0, 0, 0, .28f), new Vector2(0, -3));
            var button = image.gameObject.AddComponent<Button>();
            ConfigureButton(button);
            var text = MakeText(image.transform, "Label", caption, primary ? 21 : 16, primary ? Cream : new Color(.90f, .80f, .69f));
            if (primary) text.fontStyle = FontStyles.Bold;
            Stretch(text.rectTransform); text.alignment = TextAlignmentOptions.Center; button.onClick.AddListener(action); return button;
        }
        private PatternEntry FindEntry(string id)
        {
            if (id == null) return null;
            foreach (var entry in entries) if (entry.preset.id == id) return entry;
            return null;
        }
        private string FirstUnlockedId()
        {
            foreach (var entry in entries) if (entry.unlocked) return entry.preset.id;
            return null;
        }
        private void SelectReference(string id)
        {
            var entry = FindEntry(id);
            if (entry == null || !entry.unlocked) return;
            selectedPreset = id;
            if (guide != null)
                guide.texture = entry.preset.preview != null ? entry.preset.preview : FreeCafeLatteArtStage.Glyph(entry.preset.template, 256, 10, Color.white);
            RefreshPresetHighlights();
            UpdateGuide(Camera.main);
        }
        private void RefreshPresetHighlights()
        {
            foreach (var entry in entries)
            {
                if (entry.button == null) continue;
                bool best = entry.preset.id == bestFitId;
                bool selected = entry.preset.id == selectedPreset;
                entry.button.color = !entry.unlocked ? new Color(.17f, .115f, .085f) : best ? new Color(.50f, .37f, .15f) : selected ? new Color(.42f, .26f, .16f) : CardRaised;
                entry.button.transform.Find("Best").gameObject.SetActive(best);
                entry.button.transform.Find("Selected").gameObject.SetActive(selected);
                if (entry.unlocked) entry.fit.color = best ? new Color(1f, .88f, .43f) : new Color(.82f, .73f, .65f);
            }
        }
        private static void ConfigureButton(Button button)
        {
            Spotlight.Audio.UIButtonAudio.Configure(button);
            var colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.15f, 1.1f);
            colors.pressedColor = new Color(.8f, .8f, .8f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.75f, .75f, .75f, .8f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        private static void AddShadow(GameObject gameObject, Color color, Vector2 distance)
        {
            var shadow = gameObject.AddComponent<Shadow>(); shadow.effectColor = color; shadow.effectDistance = distance; shadow.useGraphicAlpha = true;
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
            patternFits.Clear();
            foreach (var entry in entries) { entry.button = null; entry.fit = null; entry.fitBar = null; }
            overlayRoot = null; overlay = null; status = null; foamUsage = null; cupFill = null; contentGroup = null; curtain = null; submitButton = null;
            guide = null; guideLabel = null; contentRect = null;
        }
        public void OnPointerClick(PointerEventData eventData) { /* 背景点击不再取消，玩家可自由在 Plane 上绘制。 */ }
        private static Image MakeImage(Transform parent, string name, Color color) { var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); var image = go.GetComponent<Image>(); image.color = color; return image; }
        private static Image MakeCard(Transform parent, string name, Color color)
        {
            var image = MakeImage(parent, name, color);
            image.sprite = FreeCafeLatteArtStage.RoundedRect(); image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 1.4f;
            return image;
        }
        private TMP_Text MakeText(Transform parent, string name, string value, float size, Color color) { var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); var text = go.GetComponent<TextMeshProUGUI>(); text.text = value; text.font = Resources.Load<TMP_FontAsset>("Fonts/SimHei SDF") ?? TMP_Settings.defaultFontAsset; text.fontSize = size; text.color = color; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; return text; }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        /// <summary>Full-width row pinned to the top edge: <paramref name="top"/> px from the top, <paramref name="height"/> px tall.</summary>
        private static void Pin(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(24, top - height); rect.offsetMax = new Vector2(-24, top);
        }
        /// <summary>Block <paramref name="left"/> px from the left edge, <paramref name="width"/> px wide, inside a vertical anchor band with px padding.</summary>
        private static void PinLeft(RectTransform rect, float left, float width, float minY, float maxY, float bottom, float top)
        {
            rect.anchorMin = new Vector2(0, minY); rect.anchorMax = new Vector2(0, maxY); rect.pivot = new Vector2(0, .5f);
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(left + width, -top);
        }
    }
}
