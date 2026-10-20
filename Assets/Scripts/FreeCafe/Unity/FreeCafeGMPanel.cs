using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>Runtime GM shell. New systems can add categories and actions without editing the scene.</summary>
    public sealed class FreeCafeGMPanel : MonoBehaviour
    {
        private sealed class Category
        {
            public string Name;
            public Action Build;
        }

        private readonly List<Category> categories = new List<Category>();
        private FreeCafeRuntime runtime;
        private GameObject root;
        private GameObject body;
        private TMP_Text status;
        private TMP_FontAsset font;
        private TMP_FontAsset headingFont;
        private CanvasGroup shellGroup;
        private ScrollRect contentScroll;
        private RectTransform contentViewport;
        private ContentSizeFitter contentFitter;
        private LayoutElement contentMinimum;
        private readonly List<Button> categoryButtons = new List<Button>();

        // Warm paper and coffee ink, matching Map/Details without changing shared assets.
        private static readonly Color Paper = new Color(.98f, .95f, .87f);
        private static readonly Color Parchment = new Color(.93f, .87f, .75f);
        private static readonly Color Ink = new Color(.35f, .19f, .07f);
        private static readonly Color MutedInk = new Color(.46f, .35f, .25f);
        private static readonly Color Burgundy = new Color(.43f, .15f, .16f);
        private static readonly Color Brass = new Color(.65f, .48f, .26f);
        private int activeCategory = -1;
        private string pendingDanger;
        private float pendingUntil;
        private RectTransform windowRect;
        private RectTransform canvasRect;
        private Transform sidebarTransform;
        private string customerSearch = string.Empty;
        private int summonSeatIndex = -1;
        private bool summonAllowDuplicate = true;
        private bool summonReplaceExisting;

        public static FreeCafeGMPanel Instance { get; private set; }
        public bool IsVisible => root!=null && root.activeSelf;
        private EventSystem fallbackEvents;
        private IDisposable dialoguePause;
        private Spotlight.GameFlow.DialoguePanel pausedDialogue;
        private static int lastVisibilityFrame = -1;
        public static bool BlocksDialogueInput => (Instance != null && Instance.IsVisible) || Time.frameCount == lastVisibilityFrame;
        public event Action<bool> VisibilityChanged;
        public Transform ContentRoot => body != null ? body.transform : null;
        public TMP_FontAsset UIFont => font;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Release players must not create the GM shell or its debug/test components.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null) new GameObject("GlobalGM").AddComponent<FreeCafeGMPanel>();
        }
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Spotlight.GameFlow.DialoguePanel.ExternalInputBlocked = () => BlocksDialogueInput;
            Spotlight.Narrative.NarrativePresenter.ExternalInputBlocked = () => BlocksDialogueInput;
            DontDestroyOnLoad(gameObject);
            font = Resources.Load<TMP_FontAsset>("Fonts/Map/SourceHanSansSC-Regular-2 SDF");
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/乐米曲奇方块体 SDF");
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/SimHei SDF");
            if (font == null) font = TMP_Settings.defaultFontAsset;
            headingFont = Resources.Load<TMP_FontAsset>("Fonts/Map/SourceHanSerifCN-Heavy-4 SDF");
            if (headingFont == null) headingFont = font;
            BuildCategories();
            BuildShell();
            SetVisible(false);
            gameObject.AddComponent<Spotlight.GameFlow.NarrativeDebugGM>();
            gameObject.AddComponent<NarrativeAutoGM>();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        public void Initialize(FreeCafeRuntime owner) => runtime = owner;

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            runtime = FindFirstObjectByType<FreeCafeRuntime>();
            pendingDanger = null;
            SetStatus(runtime != null ? "经营系统已连接" : "当前场景未加载经营系统；可使用全局调试");
            UpdateEventSystem();
        }

        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            runtime = FindFirstObjectByType<FreeCafeRuntime>();
            pendingDanger = null;
            UpdateEventSystem();
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
            dialoguePause?.Dispose();
            dialoguePause = null;
            if (Instance == this) Instance = null;
        }

        private void UpdateEventSystem()
        {
            bool sceneHasEvents = false;
            foreach (var events in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (events != fallbackEvents && events.isActiveAndEnabled) { sceneHasEvents = true; break; }
            bool needed = !sceneHasEvents && root != null && root.activeSelf;
            if (needed && fallbackEvents == null)
            {
                var go = new GameObject("GMFallbackEventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                go.transform.SetParent(transform, false);
                fallbackEvents = go.GetComponent<EventSystem>();
            }
            if (fallbackEvents != null) fallbackEvents.gameObject.SetActive(needed);
        }

        private void Update()
        {
            var activeDialogue = Spotlight.GameFlow.DialoguePanel.Active;
            if (IsVisible && activeDialogue != pausedDialogue)
            {
                dialoguePause?.Dispose();
                pausedDialogue = activeDialogue;
                dialoguePause = pausedDialogue != null ? pausedDialogue.AcquireReadingPause(this) : null;
            }
            if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                SetVisible(root != null && !root.activeSelf);
            }

            if (root != null && root.activeSelf && Time.unscaledTime > pendingUntil)
            {
                pendingDanger = null;
            }
        }

        /// <summary>后续系统在初始化后注册自己的 GM 分类；回调内可调用 AddAction。</summary>
        public void RegisterCategory(string name, Action<FreeCafeGMPanel> build)
        {
            if (string.IsNullOrWhiteSpace(name) || build == null) return;
            int existing = categories.FindIndex(item => item.Name == name);
            if (existing >= 0)
            {
                categories[existing].Build = () => build(this);
                if (activeCategory == existing) ShowCategory(existing);
                return;
            }
            categories.Add(new Category { Name = name, Build = () => build(this) });
            if (sidebarTransform != null) AddCategoryButton(categories.Count - 1);
            if (root != null && activeCategory < 0) ShowCategory(categories.Count - 1);
        }

        private void BuildCafeVisits()
        {
            AddAction("一键安排阿黛尔剧情来访（含对话）", () =>
            {
                var result = runtime.DebugArrangeCafeVisit();
                Report(result);
                if (result.success) SetVisible(false);
            }, "走 NCS 预约与到场事件；未营业时先预约。入座后点击交谈，上餐后自动对话并留店继续聊。");
            AddAction("一键安排阿黛尔延迟来访", () =>
            {
                var result = runtime.DebugArrangeCafeVisit(true);
                Report(result);
                if (result.success) SetVisible(false);
            }, "当次营业累计 30 秒后申请入座；此预设仅测试延迟与离店，没有配置阶段对话。");
            foreach (string suffix in new[] { "RESERVE", "DELAYED_RESERVE", "ARRIVE", "CANCEL" })
            {
                string id = "EV_DEMO_CAFE_" + suffix;
                AddAction(id, () =>
                {
                    var ncs = Spotlight.Narrative.NarrativeRuntime.Instance;
                    SetStatus(ncs != null && ncs.IsReady ? (ncs.Controller.QueueEvent(id) ? "示例事件已请求：" : "事件被拒绝：") + id : "NCS 尚未就绪；请进入正式游戏会话。");
                }, "先预约，再到场授权，最后开店；延迟示例单独运行。", false);
            }
            AddAction("查看来访状态", () =>
            {
                if (runtime == null) { SetStatus("请进入 CoffeeShop。"); return; }
                var text = new System.Text.StringBuilder();
                foreach (var v in runtime.World.State.cafeVisits)
                {
                    text.AppendLine($"{v.visitInstanceId} / {v.characterId} default={v.usesDefaultConfiguration} {v.status}");
                    text.AppendLine($"seat={v.preferredSeatId} actual={v.seatId} budget={v.budgetReserved} delay={Mathf.Max(0,v.arrivalDelaySeconds-runtime.World.State.businessElapsed):F1} patience={v.patienceRemaining:F1} reason={v.endReason}");
                    text.AppendLine("diagnostics=" + string.Join(",", v.diagnostics));
                    var config = Spotlight.Narrative.NarrativeRuntime.Instance?.Configuration;
                    if (config != null)
                        text.AppendLine("hooks=" + string.Join(",", config.cafeVisitHooks
                            .FindAll(h => h.visitKey == v.visitKey).ConvertAll(h => h.stage + "/" + h.trigger + " → " + h.eventId)));
                }
                foreach (var f in runtime.World.State.cafeVisitFeedback) text.AppendLine($"pending #{f.sequence} {f.visitInstanceId} {f.stage} click={f.isClick}");
                text.AppendLine("NCS active=" + (Spotlight.Narrative.NarrativeRuntime.Instance?.Controller?.IsBusy ?? false));
                SetStatus(text.Length > 0 ? text.ToString() : "无特殊来访；阶段演出尚未验证。");
            }, "实例、预留、耐心、结束原因与待提交反馈。", true);
        }

        private void BuildCategories()
        {
            categories.Add(new Category { Name = "全局调试", Build = BuildGlobal });
            categories.Add(new Category { Name = "咖啡馆教程", Build = BuildTutorials });
            categories.Add(new Category { Name = "经营流程", Build = BuildBusiness });
            categories.Add(new Category { Name = "资源数值", Build = BuildResources });
            categories.Add(new Category { Name = "时间与顾客", Build = BuildTimeAndCustomers });
            categories.Add(new Category { Name = "状态诊断", Build = BuildDiagnostics });
            categories.Add(new Category { Name = "特殊来访", Build = BuildCafeVisits });
            categories.Add(new Category { Name = "界面与场景", Build = BuildNavigation });
        }

        private void BuildTutorials()
        {
            AddMapTutorialAction();
            AddFirstGuestTutorialAction();
            AddSkipAllTutorialsAction();
            AddAction("黑咖啡操作教程", () => StartTutorial(false), "工作台内启动：拖豆、研磨、加水加热、右键倾倒。退出恢复原物品。", false);
            AddAction("精粹炉操作教程", () => StartTutorial(true), "工作台内启动：装粉、热水、控制注水成分、倒出精粹咖啡液。", false);
            AddAction("拉花杯操作教程", StartMilkPitcherTutorial, "工作台内启动：加牛奶、点击打奶、拖到饮品杯并练习拉花。退出恢复原物品。", false);
            AddAction("结束操作教程", () =>
            {
                if (runtime == null) return;
                if (runtime.CafeTutorialActive) { SetStatus("请完成首位客人的黑咖啡，或使用「跳过所有教程（首杯完成后）」跳过正式教程。"); return; }
                runtime.WorkstationTutorial?.Stop();
                runtime.MilkPitcherTutorial?.Stop();
            }, "恢复练习前工作台，不提交剧情教程进度。", false);
        }

        private void AddMapTutorialAction()
        {
            AddAction("地图使用教程", () =>
            {
                if (MapUsageTutorial.DebugPlay(out string message)) SetVisible(false);
                SetStatus(message);
            }, "介绍拖动/缩放地图、点击点位、查看行动需求及 T 待办；可重复播放，不执行行动或修改日期。", false);
        }

        private void AddFirstGuestTutorialAction()
        {
            AddAction("首位客人剧情与完整教学（沙盒）", () =>
            {
                if (runtime.DebugStartFirstGuestTutorial(out string message)) SetVisible(false);
                SetStatus(message);
            }, "直启 CoffeeShop 后使用：跳过序章/来信/建角，从客人入店对白开始，继续制作与营业教学。", true);
        }

        private void AddSkipAllTutorialsAction()
        {
            AddAction("跳过所有教程（首杯完成后）", () =>
            {
                var dayFlow = FindFirstObjectByType<Spotlight.GameFlow.DayFlowController>();
                if (dayFlow == null) { SetStatus("请先开始新游戏。"); return; }
                if (dayFlow.DebugSkipTutorialToCafe(out string message)) SetVisible(false);
                SetStatus(message);
            }, "跳过开场、首位客人对话和制作教学；停在首杯交付完成后的准备阶段，不跨天、不自动营业。", false);
        }

        private void StartMilkPitcherTutorial()
        {
            if (runtime == null) { SetStatus("请先进入咖啡馆工作台。"); return; }
            SetVisible(false);
            if (!runtime.StartEquipmentTutorial(true))
            {
                SetVisible(true);
                SetStatus("无法启动：请进入工作台、获得拉花套装，并结束当前教程、对话或小游戏。");
            }
        }

        private void StartTutorial(bool extraction)
        {
            if (runtime == null || runtime.WorkstationTutorial == null) { SetStatus("请先进入模拟经营场景的工作台。"); return; }
            SetVisible(false);
            if (!runtime.WorkstationTutorial.Begin(extraction))
            {
                SetVisible(true);
                SetStatus("无法启动：请进入工作台，结束对话/小游戏并确认豆袋、水、杯堆及设备可用。");
            }
        }

        private void BuildShell()
        {
            root = new GameObject("GMPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            shellGroup = root.GetComponent<CanvasGroup>();
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasRect = root.GetComponent<RectTransform>();
            canvas.sortingOrder = 32760;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            var backdrop = CreateImage(root.transform, "Backdrop", new Color(.12f, .08f, .06f, .58f));
            Stretch(backdrop.rectTransform);
            var window = CreateImage(root.transform, "Window", Paper);
            AddRuleBorder(window);
            var shadow = window.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.12f, .07f, .03f, .24f);
            shadow.effectDistance = new Vector2(5, -7);
            windowRect = window.rectTransform;
            windowRect.anchorMin = new Vector2(.5f, .5f);
            windowRect.anchorMax = new Vector2(.5f, .5f);
            windowRect.pivot = new Vector2(.5f, .5f);
            windowRect.sizeDelta = new Vector2(1400f, 800f);
            windowRect.anchoredPosition = Vector2.zero;
            var dragHandle = CreateImage(window.transform, "TitleDragHandle", Color.clear);
            SetRect(dragHandle.rectTransform, Vector2.up, Vector2.one, Vector2.zero);
            dragHandle.rectTransform.offsetMin = new Vector2(0, -94);
            dragHandle.gameObject.AddComponent<GMWindowHandle>().Configure(windowRect, canvasRect, false);
            var title = CreateText(dragHandle.transform, "Title", "咖啡馆 · 管理手册", 30, Burgundy);
            title.font = headingFont;
            SetRect(title.rectTransform, Vector2.zero, Vector2.one, Vector2.zero);
            title.rectTransform.offsetMin = new Vector2(28, 37);
            title.rectTransform.offsetMax = new Vector2(-28, -14);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var subtitle = CreateText(dragHandle.transform, "Subtitle", "GM 调试  /  [~] 开关  ·  拖动标题移动  ·  右下角调整大小", 18, MutedInk);
            Stretch(subtitle.rectTransform);
            subtitle.rectTransform.offsetMin = new Vector2(28, 9);
            subtitle.rectTransform.offsetMax = new Vector2(-28, -56);
            subtitle.alignment = TextAlignmentOptions.MidlineLeft;
            CreateRule(window.transform, "HeaderRule", new Vector2(.02f, 1), new Vector2(.98f, 1), -94);

            var statusPanel = CreateImage(window.transform, "StatusPaper", Parchment);
            SetRect(statusPanel.rectTransform, new Vector2(.02f, 0), new Vector2(.98f, 0), Vector2.zero);
            statusPanel.rectTransform.offsetMin = new Vector2(0, 22);
            statusPanel.rectTransform.offsetMax = new Vector2(0, 100);
            AddRuleBorder(statusPanel);
            var statusView = CreateImage(statusPanel.transform, "StatusViewport", Color.clear);
            Stretch(statusView.rectTransform);
            statusView.rectTransform.offsetMin = new Vector2(14, 6);
            statusView.rectTransform.offsetMax = new Vector2(-14, -6);
            statusView.gameObject.AddComponent<RectMask2D>();
            status = CreateText(statusView.transform, "Status", "准备就绪", 18, Ink);
            status.alignment = TextAlignmentOptions.TopLeft;
            status.rectTransform.anchorMin = Vector2.up;
            status.rectTransform.anchorMax = Vector2.one;
            status.rectTransform.pivot = Vector2.up;
            status.rectTransform.sizeDelta = Vector2.zero;
            status.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ConfigureScroll(statusView.gameObject, statusView.rectTransform, status.rectTransform);

            var sidebar = CreateImage(window.transform, "Sidebar", Parchment);
            SetRect(sidebar.rectTransform, new Vector2(.02f, 0), new Vector2(.25f, 1), Vector2.zero);
            sidebar.rectTransform.offsetMin = new Vector2(0, 114);
            sidebar.rectTransform.offsetMax = new Vector2(0, -108);
            AddRuleBorder(sidebar);
            var sideView = CreateImage(sidebar.transform, "CategoryViewport", Color.clear);
            Stretch(sideView.rectTransform);
            sideView.rectTransform.offsetMin = new Vector2(8, 8);
            sideView.rectTransform.offsetMax = new Vector2(-8, -8);
            sideView.gameObject.AddComponent<RectMask2D>();
            var sideContent = new GameObject("Categories", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            sideContent.transform.SetParent(sideView.transform, false);
            sidebarTransform = sideContent.transform;
            var sideRect = (RectTransform)sidebarTransform;
            sideRect.anchorMin = Vector2.up;
            sideRect.anchorMax = Vector2.one;
            sideRect.pivot = Vector2.up;
            sideRect.sizeDelta = Vector2.zero;
            var sideLayout = sideContent.GetComponent<VerticalLayoutGroup>();
            sideLayout.padding = new RectOffset(4, 4, 4, 4);
            sideLayout.spacing = 8;
            sideLayout.childControlWidth = sideLayout.childControlHeight = true;
            sideLayout.childForceExpandWidth = true;
            sideLayout.childForceExpandHeight = false;
            sideContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ConfigureScroll(sideView.gameObject, sideView.rectTransform, sideRect);
            for (int i = 0; i < categories.Count; i++) AddCategoryButton(i);

            var content = CreateImage(window.transform, "Content", Paper);
            SetRect(content.rectTransform, new Vector2(.27f, 0), new Vector2(.98f, 1), Vector2.zero);
            content.rectTransform.offsetMin = new Vector2(0, 114);
            content.rectTransform.offsetMax = new Vector2(0, -108);
            AddRuleBorder(content);
            var view = CreateImage(content.transform, "ContentViewport", Color.clear);
            Stretch(view.rectTransform);
            view.rectTransform.offsetMin = new Vector2(3, 3);
            view.rectTransform.offsetMax = new Vector2(-3, -3);
            view.gameObject.AddComponent<RectMask2D>();
            contentViewport = view.rectTransform;
            body = new GameObject("ContentRoot", typeof(RectTransform), typeof(LayoutElement), typeof(ContentSizeFitter));
            body.transform.SetParent(view.transform, false);
            Stretch((RectTransform)body.transform);
            contentFitter = body.GetComponent<ContentSizeFitter>();
            contentFitter.enabled = false;
            contentMinimum = body.GetComponent<LayoutElement>();
            contentScroll = ConfigureScroll(view.gameObject, view.rectTransform, (RectTransform)body.transform);
            contentScroll.enabled = false;

            // Keep this last in the window so the resize target always stays above the paper.
            var resizeHandle = CreateImage(window.transform, "ResizeHandle", Parchment);
            resizeHandle.rectTransform.anchorMin = resizeHandle.rectTransform.anchorMax = new Vector2(1, 0);
            resizeHandle.rectTransform.pivot = new Vector2(1, 0);
            resizeHandle.rectTransform.sizeDelta = new Vector2(28, 28);
            resizeHandle.rectTransform.anchoredPosition = new Vector2(-5, 3);
            AddRuleBorder(resizeHandle);
            var grip = CreateText(resizeHandle.transform, "Grip", "//", 18, Burgundy);
            Stretch(grip.rectTransform);
            grip.alignment = TextAlignmentOptions.Center;
            resizeHandle.gameObject.AddComponent<GMWindowHandle>().Configure(windowRect, canvasRect, true);
            ShowCategory(0);
        }

        private void AddCategoryButton(int index)
        {
            if (sidebarTransform == null || index < 0 || index >= categories.Count) return;
            var button = CreateButton(sidebarTransform, categories[index].Name, 20);
            int captured = index;
            button.onClick.AddListener(() => ShowCategory(captured));
            button.GetComponent<LayoutElement>().minHeight = 50;
            categoryButtons.Add(button);
            StyleButton(button, index == activeCategory);
        }

        private void ShowCategory(int index)
        {
            if (index < 0 || index >= categories.Count || body == null) return;
            activeCategory = index;
            for (int i = 0; i < categoryButtons.Count; i++) StyleButton(categoryButtons[i], i == index);
            var oldLayout = body.GetComponent<VerticalLayoutGroup>();
            if (oldLayout != null) oldLayout.enabled = false;
            // External categories own their layout inside the same, full-size ContentRoot.
            contentScroll.StopMovement();
            contentScroll.enabled = false;
            contentFitter.enabled = false;
            contentMinimum.minHeight = 0;
            var bodyRect = (RectTransform)body.transform;
            bodyRect.pivot = Vector2.up;
            Stretch(bodyRect);
            for (int i = body.transform.childCount - 1; i >= 0; i--)
            {
                body.transform.GetChild(i).gameObject.SetActive(false);
                Destroy(body.transform.GetChild(i).gameObject);
            }
            var heading = CreateText(body.transform, "Heading", categories[index].Name, 27, Burgundy);
            heading.font = headingFont;
            SetRect(heading.rectTransform, new Vector2(.04f, .87f), new Vector2(.96f, .99f), Vector2.zero);
            heading.alignment = TextAlignmentOptions.MidlineLeft;
            heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            categories[index].Build();
        }

        private void EnableActionLayout()
        {
            var layout = body.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = body.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 16, 20);
            layout.spacing = 10;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.enabled = true;
            var rect = (RectTransform)body.transform;
            rect.pivot = Vector2.up;
            contentMinimum.minHeight = contentViewport.rect.height;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.enabled = true;
            contentScroll.enabled = true;
        }

        private void LateUpdate()
        {
            // A stretched root is retained for external views; only standard rows are fitted.
            if (IsVisible && contentFitter != null && contentFitter.enabled)
            {
                float height = contentViewport.rect.height;
                if (!Mathf.Approximately(contentMinimum.minHeight, height)) contentMinimum.minHeight = height;
            }
        }

        private void BuildGlobal()
        {
            AddAction("剧情图谱 · 分支 / 状态 / 试播", () => SelectCategory("剧情图谱"),
                "查看故事连线与当前会话诊断；复用正式演出样式，不结算正式进度", false);
            AddMapTutorialAction();
            AddFirstGuestTutorialAction();
            AddSkipAllTutorialsAction();
            AddAction("打开地图（保留当前场景）", () =>
            {
                var flow = FindFirstObjectByType<Spotlight.GameFlow.GameFlowBootstrap>();
                if (flow == null) { SetStatus("请先进入游戏。"); return; }
                if (flow.DebugForceOpenMap(out string message)) SetVisible(false);
                SetStatus(message);
            }, "复用常驻地图面板并置顶；只在地图缺失时补加载，咖啡馆对象和脚本保持运行", false);
            AddAction("跳到教程初次地图", () =>
            {
                var flow = FindFirstObjectByType<Spotlight.GameFlow.GameFlowBootstrap>();
                if (flow == null) { SetStatus("请先开始新游戏。"); return; }
                bool skipped = flow.DebugSkipTutorialToMap(out string message);
                SetStatus(message);
                if (skipped) SetVisible(false);
            }, "跳过地图前的开场；保留咖啡馆教程与地图引导，不自动保存", false);
            AddAction("查看场景与时间状态", () => SetStatus("场景：" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " | 时间倍率：" + Time.timeScale.ToString("0.##") + " | 经营系统：" + (runtime != null && runtime.World != null ? "已连接" : "未加载")), "任意场景都可使用", false);
            AddAction("切换到咖啡馆场景", () =>
            {
                var flow = FindFirstObjectByType<Spotlight.GameFlow.GameFlowBootstrap>();
                if (flow == null) { SetStatus("请先进入游戏。"); return; }
                flow.TransitionToGameplayScene("CoffeeShop", null);
                SetVisible(false);
            }, "调试用：复用常驻 Map，使用短转场进入 CoffeeShop", false);
        }

        private void BuildBusiness()
        {
            AddAction("开始营业", () => runtime.RequestBusinessStart(), "先对齐特殊来访预约，再按正常规则生成当天客流");
            AddAction("结束营业", () => ConfirmDanger("end", () => Report(runtime.GameplayBlocked ? ActionResult.No("当前流程或小游戏禁止结束营业") : runtime.World.EndDay()), "再次点击确认结束营业"), "会结算当前订单，需二次确认");
            AddAction("免费补满库存", () => Report(runtime.World.DebugRefillFree()), "不扣金币");
            AddAction("保存当前存档", () => runtime.Save(), "写入现有快速存档槽");
            AddAction("切换暂停", () => runtime.TogglePause(), "仅影响游戏时间，不影响 GM 面板");
        }

        private void BuildResources()
        {
            AddAction("金币 +100", () => Report(runtime.World.DebugAddMoney(100)), "受整数上限保护");
            AddAction("金币 +1000", () => Report(runtime.World.DebugAddMoney(1000)), "受整数上限保护");
            AddAction("声望 +10", () => Report(runtime.World.DebugAddReputation(10)), "会立即检查解锁内容");
            AddAction("声望 +100", () => Report(runtime.World.DebugAddReputation(100)), "会立即检查解锁内容");
            AddAction("正常购买补货", () => Report(runtime.World.Refill()), "沿用经济规则，会扣金币");
        }

        private void BuildTimeAndCustomers()
        {
            AddAction("NCS 剧情客人来店（含对话）…", () => SelectCategory("特殊来访"), "剧情客人与普通客流分开，走预约、到场和对话事件");
            AddAction("强制召唤指定外观（不触发剧情）…", BuildCustomerSummon, "仅测试人物、座位和订单；不会建立 NCS 来访");
            AddAction("剩余时间设为 60 秒", () => Report(runtime.World.DebugSetTime(60)), "仅修改营业倒计时");
            AddAction("剩余时间设为 10 秒", () => Report(runtime.World.DebugSetTime(10)), "用于测试倒计时结算");
            AddAction("生成 1 位顾客", () => Report(runtime.World.DebugSpawnCustomers(1)), "需要营业且有空卡座");
            AddAction("生成 5 位顾客", () => Report(runtime.World.DebugSpawnCustomers(5)), "需要营业且有空卡座");
        }

        private void BuildCustomerSummon()
        {
            foreach (Transform child in body.transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            if (runtime != null && runtime.World != null && summonSeatIndex >= runtime.World.State.seats) summonSeatIndex = -1;
            AddAction("← 返回时间与顾客", () => ShowCategory(activeCategory), "需已营业；可指定座位、重复召唤和强制挤位", false);
            AddAction("召唤位置：" + SummonSeatLabel() + "（点击切换）", CycleSummonSeat,
                "自动分配或选择已开放的座位；显示桌号、左右侧和占用者");
            AddAction("允许重复召唤：" + (summonAllowDuplicate ? "开启" : "关闭"), () =>
            {
                summonAllowDuplicate = !summonAllowDuplicate;
                BuildCustomerSummon();
            }, "开启后同一角色可以同时出现在多个座位");
            AddAction("挤掉指定位置原客人：" + (summonReplaceExisting ? "开启" : "关闭"), () =>
            {
                summonReplaceExisting = !summonReplaceExisting;
                BuildCustomerSummon();
            }, "仅作用于指定座位；移除旧顾客和订单，剧情来访按离店流程结束");
            var searchBox = CreateImage(body.transform, "CustomerSearch", Paper);
            AddRuleBorder(searchBox);
            searchBox.gameObject.AddComponent<LayoutElement>().minHeight = 52;
            var textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(searchBox.transform, false);
            Stretch((RectTransform)textArea.transform);
            var searchText = CreateText(textArea.transform, "Text", "", 20, Ink);
            Stretch(searchText.rectTransform);
            searchText.margin = new Vector4(12, 6, 12, 6);
            searchText.richText = false;
            var placeholder = CreateText(textArea.transform, "Placeholder", "搜索角色名字 / ID（留空显示全部）", 19, MutedInk);
            Stretch(placeholder.rectTransform);
            placeholder.margin = searchText.margin;
            var input = searchBox.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = searchBox;
            input.colors = PaperStates();
            searchBox.color = Color.white;
            input.customCaretColor = true;
            input.caretColor = Burgundy;
            input.selectionColor = new Color(.65f, .48f, .26f, .28f);
            input.textViewport = (RectTransform)textArea.transform;
            input.textComponent = (TextMeshProUGUI)searchText;
            input.placeholder = placeholder;
            input.fontAsset = font;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.SetTextWithoutNotify(customerSearch);
            var viewport = CreateImage(body.transform, "CustomerList", Parchment);
            AddRuleBorder(viewport);
            viewport.gameObject.AddComponent<LayoutElement>().minHeight = 180;
            viewport.GetComponent<LayoutElement>().flexibleHeight = 1;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Customers", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(.5f, 1);
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36;
            void RefreshList(string query)
            {
                customerSearch = query;
                foreach (Transform child in content.transform)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
                int count = 0;
                string filter = query.Trim();
                void AddCustomer(string id, string name, bool hasArtwork)
                {
                    if (!string.IsNullOrEmpty(filter) && id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) return;
                    count++;
                    var button = CreateButton(content.transform, name + "  [" + id + "]\n<size=17>"
                        + (hasArtwork ? "点击按上方设置召唤到店" : "未配置店内素材，无法召唤") + "</size>", 20);
                    button.GetComponent<LayoutElement>().minHeight = 70;
                    button.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
                    button.interactable = hasArtwork;
                    button.onClick.AddListener(() =>
                    {
                        if (runtime == null || !runtime.isActiveAndEnabled || runtime.World == null)
                        { SetStatus("请先进入 CoffeeShop"); return; }
                        Report(runtime.DebugSummonCustomer(id, summonSeatIndex, summonAllowDuplicate, summonReplaceExisting));
                        BuildCustomerSummon();
                    });
                }
                if (runtime != null)
                {
                    if (runtime.seating != null && runtime.seating.suppliedCustomer != null)
                        AddCustomer("supplied_seated_guest", runtime.seating.suppliedGuestName, true);
                    if (runtime.characterCatalog != null)
                        foreach (var character in runtime.characterCatalog.Characters)
                            if (character != null)
                                AddCustomer(character.characterId, character.displayName,
                                    runtime.seating != null && runtime.seating.CanPresentCustomer(character.characterId) || character.worldSprite != null);
                }
                if (count == 0)
                {
                    var empty = CreateText(content.transform, "Empty", "没有匹配角色；可清空搜索，或进入 CoffeeShop 后重开列表。", 20, MutedInk);
                    empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
                }
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 1;
            }
            input.onValueChanged.AddListener(RefreshList);
            RefreshList(customerSearch);
        }

        private string SummonSeatLabel()
        {
            if (summonSeatIndex < 0) return "自动分配";
            string label = "位置 " + (summonSeatIndex + 1);
            if (runtime == null || runtime.World == null) return label;
            if (runtime.seating != null && runtime.seating.TrySeat("customer_" + summonSeatIndex, out _, out _, out bool right))
            {
                int tableIndex = summonSeatIndex % runtime.seating.tables.Length;
                label += " / 桌 " + (tableIndex + 1) + (right ? " 右侧" : " 左侧");
            }
            var occupant = runtime.World.Find("customer_" + summonSeatIndex);
            return label + (occupant != null ? "（" + runtime.World.PersonName(occupant) + "）" : "（空）");
        }

        private void CycleSummonSeat()
        {
            int seatCount = runtime != null && runtime.World != null ? runtime.World.State.seats : 8;
            summonSeatIndex++;
            if (summonSeatIndex >= seatCount) summonSeatIndex = -1;
            BuildCustomerSummon();
        }

        private void BuildDiagnostics()
        {
            AddAction("拉花快捷入口（GM）", () =>
            {
                if (runtime.GameplayBlocked) { SetStatus("当前流程或小游戏禁止进入拉花"); return; }
                var result = runtime.World.RequestProductionMiniGame();
                Report(result);
                if (result.success) SetVisible(false);
            }, "测试用：绑定出餐杯，不消耗奶泡；正式玩法请拖动拉花杯");
            AddAction("刷新经营视图", () => runtime.DebugRefreshViews(), "重新同步实体显示");
            AddAction("取消当前拖拽", () => runtime.CancelDrag(), "不改变库存");
            AddAction("显示当前状态", ShowState, "在面板底部显示快照摘要");
            AddAction("显示 / 隐藏实体 ID", () => { runtime.ShowAssetIds=!runtime.ShowAssetIds;runtime.DebugRefreshViews();SetStatus("实体 ID："+(runtime.ShowAssetIds ? "显示" : "隐藏")); }, "美术与实体调试，不出现在玩家 HUD");
            AddAction("查看选中实体", () => { var e=runtime.World.Find(runtime.SelectedId);SetStatus(e==null ? "尚未选中实体" : "ID "+e.id+" | 规格 "+e.specId+" | 区域 "+e.area+" | 内容物记录 "+e.contents.Count); }, "诊断实体身份与存储内容");
        }

        public bool SelectCategory(string name)
        {
            for(int i=0;i<categories.Count;i++) if(categories[i].Name==name) { ShowCategory(i);return true; }
            return false;
        }
        private void BuildNavigation()
        {
            AddAction("直达一楼大堂", () => Navigate(AreaId.Cafe), "调试传送；正常游戏使用场景入口");
            AddAction("直达制作界面", () => Navigate(AreaId.Counter), "沿用制作会话，不创建新场景");
            AddAction("直达二楼会客室", () => Navigate(AreaId.TopFloor), "调试传送");
            AddAction("直达三楼宿舍", () => Navigate(AreaId.Basement), "调试传送");
            AddAction("携带选中实体跨区域", () => { runtime.CarrySelection=!runtime.CarrySelection;SetStatus("调试携带："+(runtime.CarrySelection ? "开启" : "关闭")); }, "只影响调试区域切换");
        }
        private void Navigate(AreaId area)
        {
            // GM 关闭后再走原入口，保留既有取消拖拽与返回区域逻辑。
            SetVisible(false);runtime.Enter(area);
        }

        public void AddAction(string label, Action action, string help, bool requiresCafe = true)
        {
            var button = CreateButton(body.transform, label + "\n<size=17>" + help + "</size>", 19);
            var rect = button.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(.06f, 0), new Vector2(.94f, 0), Vector2.zero);
            var layout = button.GetComponent<LayoutElement>();
            layout.minHeight = 72;
            button.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            layout.flexibleWidth = 1;
            button.onClick.AddListener(() =>
            {
                if (requiresCafe && (runtime == null || !runtime.isActiveAndEnabled || runtime.World == null))
                {
                    SetStatus("当前场景未加载经营系统，此操作不可用");
                    return;
                }
                action?.Invoke();
            });
            EnableActionLayout();
            button.transform.SetAsLastSibling();
        }

        private void ConfirmDanger(string key, Action action, string hint)
        {
            if (pendingDanger == key && Time.unscaledTime <= pendingUntil)
            {
                pendingDanger = null;
                action?.Invoke();
                return;
            }
            pendingDanger = key;
            pendingUntil = Time.unscaledTime + 3f;
            SetStatus(hint + "（3 秒内）");
        }

        private void ShowState()
        {
            var s = runtime.World.State;
            SetStatus("第 " + s.day + " 天 | 金币 " + s.money + " | 声望 " + s.reputation + " | 顾客 " + s.requests.Count + " | 阶段 " + s.businessPhase);
        }

        private void Report(ActionResult result) { SetStatus(result.message); }
        private void SetStatus(string message)
        {
            if (status == null) return;
            status.text = message;
            status.rectTransform.anchoredPosition = Vector2.zero;
        }
        public void SetExternalStatus(string message) => SetStatus(message);

        public void AddSlider(string label, float min, float max, Func<float> getter, Action<float> setter, string format = "F2", bool wholeNumbers = false)
        {
            if (body == null || getter == null || setter == null) return;
            var row = new GameObject(label + "Slider", typeof(RectTransform));
            row.transform.SetParent(body.transform, false);
            var layout = row.AddComponent<LayoutElement>(); layout.preferredHeight = 72; layout.flexibleWidth = 1;
            var title = CreateText(row.transform, "Label", label, 20, Ink);
            title.rectTransform.anchorMin = new Vector2(0, .52f); title.rectTransform.anchorMax = new Vector2(.72f, 1); title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            var value = CreateText(row.transform, "Value", getter().ToString(format), 20, Burgundy);
            value.rectTransform.anchorMin = new Vector2(.72f, .52f); value.rectTransform.anchorMax = new Vector2(1, 1); value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero; value.alignment = TextAlignmentOptions.MidlineRight;
            var track = CreateImage(row.transform, "Track", Parchment); track.rectTransform.anchorMin = new Vector2(0, 0); track.rectTransform.anchorMax = new Vector2(1, .42f); track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;
            var slider = track.gameObject.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = wholeNumbers; slider.navigation = new Navigation { mode = Navigation.Mode.None };
            var fill = CreateImage(track.transform, "Fill", Brass); fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one; fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero; fill.raycastTarget = false;
            var handle = CreateImage(track.transform, "Handle", Color.white); handle.rectTransform.anchorMin = new Vector2(0,0); handle.rectTransform.anchorMax = new Vector2(0,1); handle.rectTransform.sizeDelta = new Vector2(18,0); handle.raycastTarget = false;
            var handleStates = ColorBlock.defaultColorBlock;
            handleStates.normalColor = Ink;
            handleStates.highlightedColor = Burgundy;
            handleStates.pressedColor = new Color(.28f, .08f, .09f);
            handleStates.selectedColor = Burgundy;
            handleStates.disabledColor = MutedInk;
            handleStates.fadeDuration = .1f;
            slider.colors = handleStates;
            AddRuleBorder(track);
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle; slider.SetValueWithoutNotify(Mathf.Clamp(getter(), min, max));
            slider.onValueChanged.AddListener(v => { setter(v); value.text = getter().ToString(format); });
            EnableActionLayout();
        }
        public void SetVisible(bool visible)
        {
            if (root == null) return;
            if (visible) { runtime = FindFirstObjectByType<FreeCafeRuntime>();if(runtime!=null) runtime.CancelDrag(); }
            pendingDanger = null;
            if (!visible) SetExternalOverlay(false);
            root.SetActive(visible);
            lastVisibilityFrame = Time.frameCount;
            dialoguePause?.Dispose();
            pausedDialogue = visible ? Spotlight.GameFlow.DialoguePanel.Active : null;
            dialoguePause = pausedDialogue != null ? pausedDialogue.AcquireReadingPause(this) : null;
            VisibilityChanged?.Invoke(visible);
            UpdateEventSystem();
        }

        private TMP_Text CreateText(Transform parent, string name, string value, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value; text.font = font; text.fontSize = size; text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(Transform parent, string label, float size)
        {
            var image = CreateImage(parent, "Button", Color.white);
            AddRuleBorder(image);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            image.gameObject.AddComponent<LayoutElement>().minHeight = 50;
            // The button grows with wrapped Chinese text instead of clipping its help line.
            var layout = image.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            var text = CreateText(image.transform, "Label", label, size, Ink);
            text.alignment = TextAlignmentOptions.Center;
            StyleButton(button, false);
            return button;
        }

        private static ColorBlock PaperStates()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(.95f, .90f, .80f);
            colors.highlightedColor = new Color(1f, .97f, .90f);
            colors.pressedColor = new Color(.80f, .69f, .54f);
            colors.selectedColor = new Color(.98f, .93f, .84f);
            colors.disabledColor = new Color(.82f, .79f, .73f);
            colors.fadeDuration = .1f;
            return colors;
        }

        private static void StyleButton(Button button, bool active)
        {
            var colors = PaperStates();
            if (active)
            {
                colors.normalColor = Burgundy;
                colors.highlightedColor = new Color(.52f, .21f, .21f);
                colors.pressedColor = new Color(.32f, .10f, .11f);
                colors.selectedColor = new Color(.48f, .18f, .19f);
                colors.disabledColor = new Color(.42f, .33f, .30f);
            }
            button.colors = colors;
            button.GetComponentInChildren<TMP_Text>().color = active ? Paper : Ink;
        }

        private static ScrollRect ConfigureScroll(GameObject owner, RectTransform viewport, RectTransform content)
        {
            var scroll = owner.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 36;
            return scroll;
        }

        private static void AddRuleBorder(Image image)
        {
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Brass.r, Brass.g, Brass.b, .65f);
            outline.effectDistance = new Vector2(1, -1);
            outline.useGraphicAlpha = false;
        }

        private void CreateRule(Transform parent, string name, Vector2 min, Vector2 max, float y)
        {
            var line = CreateImage(parent, name, Brass);
            SetRect(line.rectTransform, min, max, Vector2.zero);
            line.rectTransform.offsetMin = new Vector2(0, y);
            line.rectTransform.offsetMax = new Vector2(0, y + 1);
            line.raycastTarget = false;
        }

        /// <summary>Hide the UGUI shell under an external overlay without releasing its input shield.</summary>
        public void SetExternalOverlay(bool active)
        {
            if (shellGroup == null) return;
            shellGroup.alpha = active ? 0f : 1f;
            shellGroup.interactable = !active;
            shellGroup.blocksRaycasts = true;
        }

        private Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.color = color; return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 size)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = size; rect.offsetMax = -size;
        }
    }

    public sealed class GMWindowHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private RectTransform window;
        private RectTransform canvas;
        private bool resize;
        private Vector2 startPointer;
        private Vector2 startPosition;
        private Vector2 startSize;
        private const float MinWidth = 720f;
        private const float MinHeight = 480f;
        private const float MaxWidth = 1800f;
        private const float MaxHeight = 1000f;

        public void Configure(RectTransform target, RectTransform canvasRect, bool resizeHandle)
        {
            window = target;
            canvas = canvasRect;
            resize = resizeHandle;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (window == null || canvas == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, eventData.position, eventData.pressEventCamera, out startPointer);
            startPosition = window.anchoredPosition;
            startSize = window.sizeDelta;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (window == null || canvas == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, eventData.position, eventData.pressEventCamera, out var pointer)) return;
            var delta = pointer - startPointer;
            if (resize)
            {
                var size = new Vector2(Mathf.Clamp(startSize.x + delta.x, MinWidth, MaxWidth), Mathf.Clamp(startSize.y - delta.y, MinHeight, MaxHeight));
                window.sizeDelta = size;
                window.anchoredPosition = ClampPosition(window.anchoredPosition, size, canvas.rect.size);
            }
            else
            {
                window.anchoredPosition = ClampPosition(startPosition + delta, window.sizeDelta, canvas.rect.size);
            }
        }

        private static Vector2 ClampPosition(Vector2 position, Vector2 size, Vector2 canvasSize)
        {
            var halfCanvas = canvasSize * .5f;
            var halfWindow = size * .5f;
            return new Vector2(
                Mathf.Clamp(position.x, -halfCanvas.x + halfWindow.x, halfCanvas.x - halfWindow.x),
                Mathf.Clamp(position.y, -halfCanvas.y + halfWindow.y, halfCanvas.y - halfWindow.y));
        }
    }
}
