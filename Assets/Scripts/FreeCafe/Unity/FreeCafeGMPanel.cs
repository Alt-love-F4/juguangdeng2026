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
        private int activeCategory = -1;
        private string pendingDanger;
        private float pendingUntil;
        private RectTransform windowRect;
        private RectTransform canvasRect;

        public static FreeCafeGMPanel Instance { get; private set; }
        private EventSystem fallbackEvents;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null) new GameObject("GlobalGM").AddComponent<FreeCafeGMPanel>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            font = Resources.Load<TMP_FontAsset>("Fonts/SimHei SDF");
            BuildCategories();
            BuildShell();
            SetVisible(false);
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
            categories.Add(new Category { Name = name, Build = () => build(this) });
            if (root != null && activeCategory < 0) ShowCategory(categories.Count - 1);
        }

        private void BuildCategories()
        {
            categories.Add(new Category { Name = "全局调试", Build = BuildGlobal });
            categories.Add(new Category { Name = "经营流程", Build = BuildBusiness });
            categories.Add(new Category { Name = "资源数值", Build = BuildResources });
            categories.Add(new Category { Name = "时间与顾客", Build = BuildTimeAndCustomers });
            categories.Add(new Category { Name = "状态诊断", Build = BuildDiagnostics });
        }

        private void BuildShell()
        {
            root = new GameObject("GMPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasRect = root.GetComponent<RectTransform>();
            canvas.sortingOrder = 32760;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            var backdrop = CreateImage(root.transform, "Backdrop", new Color(0, 0, 0, .58f));
            Stretch(backdrop.rectTransform);
            var window = CreateImage(root.transform, "Window", new Color(.08f, .09f, .11f, .98f));
            windowRect = window.rectTransform;
            windowRect.anchorMin = new Vector2(.5f, .5f);
            windowRect.anchorMax = new Vector2(.5f, .5f);
            windowRect.pivot = new Vector2(.5f, .5f);
            windowRect.sizeDelta = new Vector2(1400f, 800f);
            windowRect.anchoredPosition = Vector2.zero;
            var dragHandle = CreateImage(window.transform, "TitleDragHandle", new Color(0, 0, 0, 0));
            SetRect(dragHandle.rectTransform, new Vector2(0, .91f), new Vector2(1, 1), Vector2.zero);
            dragHandle.gameObject.AddComponent<GMWindowHandle>().Configure(windowRect, canvasRect, false);
            var resizeHandle = CreateImage(window.transform, "ResizeHandle", new Color(.35f, .4f, .45f, .9f));
            resizeHandle.rectTransform.anchorMin = new Vector2(1, 0);
            resizeHandle.rectTransform.anchorMax = new Vector2(1, 0);
            resizeHandle.rectTransform.pivot = new Vector2(1, 0);
            resizeHandle.rectTransform.sizeDelta = new Vector2(28, 28);
            resizeHandle.rectTransform.anchoredPosition = new Vector2(-8, 8);
            resizeHandle.gameObject.AddComponent<GMWindowHandle>().Configure(windowRect, canvasRect, true);

            var title = CreateText(window.transform, "Title", "GM 调试面板   [~] 开关   · 拖动标题栏移动 · 拖动右下角调整大小", 25, Color.white);
            SetRect(title.rectTransform, new Vector2(.035f, .91f), new Vector2(.965f, .99f), Vector2.zero);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            status = CreateText(window.transform, "Status", "准备就绪", 18, new Color(.75f, .82f, .85f));
            SetRect(status.rectTransform, new Vector2(.035f, .02f), new Vector2(.965f, .09f), Vector2.zero);
            status.alignment = TextAlignmentOptions.MidlineLeft;

            var sidebar = CreateImage(window.transform, "Sidebar", new Color(.12f, .14f, .17f, 1));
            SetRect(sidebar.rectTransform, new Vector2(.02f, .12f), new Vector2(.25f, .9f), Vector2.zero);
            var sideLayout = sidebar.gameObject.AddComponent<VerticalLayoutGroup>();
            sideLayout.padding = new RectOffset(14, 14, 14, 14);
            sideLayout.spacing = 10;
            sideLayout.childForceExpandHeight = false;
            for (int i = 0; i < categories.Count; i++)
            {
                var index = i;
                var button = CreateButton(sidebar.transform, categories[i].Name, 20);
                button.onClick.AddListener(() => ShowCategory(index));
                button.GetComponent<LayoutElement>().preferredHeight = 58;
            }

            var content = CreateImage(window.transform, "Content", new Color(.1f, .11f, .13f, 1));
            SetRect(content.rectTransform, new Vector2(.27f, .12f), new Vector2(.98f, .9f), Vector2.zero);
            body = content.gameObject;
            ShowCategory(0);
        }

        private void ShowCategory(int index)
        {
            if (index < 0 || index >= categories.Count || body == null) return;
            activeCategory = index;
            for (int i = body.transform.childCount - 1; i >= 0; i--) Destroy(body.transform.GetChild(i).gameObject);
            var heading = CreateText(body.transform, "Heading", categories[index].Name, 27, Color.white);
            SetRect(heading.rectTransform, new Vector2(.04f, .87f), new Vector2(.96f, .99f), Vector2.zero);
            heading.alignment = TextAlignmentOptions.MidlineLeft;
            categories[index].Build();
        }

        private void BuildGlobal()
        {
            AddAction("查看场景与时间状态", () => SetStatus("场景：" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " | 时间倍率：" + Time.timeScale.ToString("0.##") + " | 经营系统：" + (runtime != null && runtime.World != null ? "已连接" : "未加载")), "任意场景都可使用", false);
        }

        private void BuildBusiness()
        {
            AddAction("开始营业", () => runtime.StartBusiness(), "会按正常规则生成当天客流");
            AddAction("结束营业", () => ConfirmDanger("end", () => runtime.EndBusiness(), "再次点击确认结束营业"), "会结算当前订单，需二次确认");
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
            AddAction("正常购买补货", () => runtime.RefillSources(), "沿用经济规则，会扣金币");
        }

        private void BuildTimeAndCustomers()
        {
            AddAction("剩余时间设为 60 秒", () => Report(runtime.World.DebugSetTime(60)), "仅修改营业倒计时");
            AddAction("剩余时间设为 10 秒", () => Report(runtime.World.DebugSetTime(10)), "用于测试倒计时结算");
            AddAction("生成 1 位顾客", () => Report(runtime.World.DebugSpawnCustomers(1)), "需要营业且有空卡座");
            AddAction("生成 5 位顾客", () => Report(runtime.World.DebugSpawnCustomers(5)), "需要营业且有空卡座");
        }

        private void BuildDiagnostics()
        {
            AddAction("刷新经营视图", () => runtime.SendMessage("SyncViews", SendMessageOptions.DontRequireReceiver), "重新同步实体显示");
            AddAction("取消当前拖拽", () => runtime.CancelDrag(), "不改变库存");
            AddAction("显示当前状态", ShowState, "在面板底部显示快照摘要");
        }

        public void AddAction(string label, Action action, string help, bool requiresCafe = true)
        {
            var button = CreateButton(body.transform, label + "\n<size=14>" + help + "</size>", 19);
            var rect = button.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(.06f, 0), new Vector2(.94f, 0), Vector2.zero);
            var layout = button.GetComponent<LayoutElement>();
            layout.preferredHeight = 62;
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
            var layoutGroup = body.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = body.AddComponent<VerticalLayoutGroup>();
                layoutGroup.padding = new RectOffset(30, 30, 90, 30);
                layoutGroup.spacing = 12;
                layoutGroup.childForceExpandWidth = true;
                layoutGroup.childForceExpandHeight = false;
            }
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
        private void SetStatus(string message) { if (status != null) status.text = message; }
        public void SetVisible(bool visible)
        {
            if (root == null) return;
            if (visible) runtime = FindFirstObjectByType<FreeCafeRuntime>();
            pendingDanger = null;
            root.SetActive(visible);
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
            var image = CreateImage(parent, "Button", new Color(.2f, .25f, .29f, 1));
            var button = image.gameObject.AddComponent<Button>();
            var layout = image.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 50;
            var text = CreateText(image.transform, "Label", label, size, Color.white);
            Stretch(text.rectTransform); text.alignment = TextAlignmentOptions.Center;
            text.margin = new Vector4(12, 6, 12, 6);
            return button;
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
