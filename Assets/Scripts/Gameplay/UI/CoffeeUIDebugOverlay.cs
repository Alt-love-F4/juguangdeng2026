using System;
using TMPro;
using Spotlight.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spotlight.Gameplay
{
    /// <summary>
    /// CoffeeUIEntitySandbox 的运行时诊断面板。
    /// 它故意不替代正式玩法 UI，而是提供一组可重复点击的入口，验证：
    /// 1) UGUI 控件是否可见、可交互；2) CoffeeGameplayPanel 的按钮绑定；
    /// 3) CoffeeWorldRuntime 中的实体数量、物品状态和当前订单状态。
    /// </summary>
    public sealed class CoffeeUIDebugOverlay : MonoBehaviour
    {
        public TMP_Text statusText;
        public TMP_Text logText;
        public Button startDayButton;
        public Button talkButton;
        public Button selectCupButton;
        public Button actionButton;
        public Button clearItemsButton;
        public Button addCoffeeButton;
        public Button addMilkButton;
        public Button dumpButton;
        public Toggle autoRefreshToggle;
        public Slider refreshRateSlider;

        private const int MaxLogLength = 512;
        private CoffeeGameplayController controller;
        private CoffeeWorldRuntime world;
        private CoffeeGameplayPanel gameplayPanel;
        private Canvas diagnosticCanvas;
        private float nextRefresh;
        private float nextReferenceRefresh;
        private bool refreshRequested = true;
        private string lastLog = "等待操作…";

        // 诊断读取没有创建业务对象的副作用，且不包含上一次 JSON 快照。
        public string DebugSummary => BuildReport(false);
        public string DebugJson => BuildReport(true);

        private void Awake() => diagnosticCanvas = GetComponentInParent<Canvas>();
        private void OnEnable()
        {
            BindButtons(true);
            nextReferenceRefresh = nextRefresh = 0;
            refreshRequested = true;
        }
        private void Start() { RefreshReferences(); RefreshView(); }
        private void OnDisable() { BindButtons(false); DetachController(); }

        private void BindButtons(bool bind)
        {
            Bind(startDayButton, StartDay, bind);
            Bind(talkButton, TalkCustomer, bind);
            Bind(selectCupButton, SelectCup, bind);
            Bind(actionButton, InvokeActionButton, bind);
            Bind(clearItemsButton, ClearLooseItems, bind);
            Bind(addCoffeeButton, AddCoffeeBean, bind);
            Bind(addMilkButton, AddMilk, bind);
            Bind(dumpButton, DumpState, bind);
            if (autoRefreshToggle != null)
            {
                if (bind) autoRefreshToggle.onValueChanged.AddListener(OnAutoRefreshChanged);
                else autoRefreshToggle.onValueChanged.RemoveListener(OnAutoRefreshChanged);
            }
            if (refreshRateSlider != null)
            {
                if (bind) refreshRateSlider.onValueChanged.AddListener(OnRefreshRateChanged);
                else refreshRateSlider.onValueChanged.RemoveListener(OnRefreshRateChanged);
            }
        }
        private static void Bind(Button button, UnityEngine.Events.UnityAction handler, bool bind)
        {
            if (button == null) return;
            if (bind) button.onClick.AddListener(handler);
            else button.onClick.RemoveListener(handler);
        }
        private void OnAutoRefreshChanged(bool _) => refreshRequested = true;
        private void OnRefreshRateChanged(float _) => refreshRequested = true;
        private void RefreshReferences()
        {
            var current = CoffeeGameplayController.Instance;
            if (controller != current)
            {
                DetachController();
                controller = current;
                if (controller != null) controller.Toast += OnToast;
            }
            if (world == null) world = FindFirstObjectByType<CoffeeWorldRuntime>();
            if (gameplayPanel == null)
                gameplayPanel = FindFirstObjectByType<CoffeeGameplayPanel>(FindObjectsInactive.Include);
        }
        private void DetachController()
        {
            if (controller != null) controller.Toast -= OnToast;
            controller = null;
        }
        private void Update()
        {
            var now = Time.unscaledTime;
            if (now >= nextReferenceRefresh)
            {
                nextReferenceRefresh = now + 1f;
                RefreshReferences();
            }
            var auto = autoRefreshToggle == null || autoRefreshToggle.isOn;
            if (now < nextRefresh || (!auto && !refreshRequested)) return;
            // 不订阅每帧 StateChanged，限制字符串分配和 TMP 网格重建频率。
            nextRefresh = now + (refreshRateSlider == null
                ? 0.25f : Mathf.Lerp(0.1f, 1f, refreshRateSlider.value));
            refreshRequested = false;
            RefreshView();
        }
        private bool ControllerReady()
        {
            RefreshReferences();
            if (controller != null && controller.Day != null) return true;
            WriteLog("失败：控制器尚未初始化（诊断面板不会创建控制器）");
            return false;
        }
        private bool WorldReady()
        {
            RefreshReferences();
            if (world != null && world.Ledger != null) return true;
            WriteLog("失败：实体世界尚未初始化");
            return false;
        }

        public void StartDay()
        {
            if (!ControllerReady()) return;
            controller.StartDay();
            WriteLog("调用 CoffeeGameplayController.StartDay()");
        }

        public void TalkCustomer()
        {
            if (!ControllerReady()) return;
            controller.TalkCustomer();
            WriteLog("调用 CoffeeGameplayController.TalkCustomer()");
        }

        public void SelectCup()
        {
            if (!WorldReady()) return;
            world?.SelectCupFromButton();
            WriteLog("调用 CoffeeWorldRuntime.SelectCupFromButton()");
        }

        public void InvokeActionButton()
        {
            RefreshReferences();
            if (gameplayPanel?.grindButton == null)
            {
                WriteLog("失败：CoffeeGameplayPanel.grindButton 为空");
                return;
            }

            var button = gameplayPanel.grindButton;
            if (!button.isActiveAndEnabled || !button.IsInteractable())
            {
                WriteLog($"动作未执行：active={button.gameObject.activeInHierarchy}, " +
                         $"enabled={button.enabled}, interactable={button.IsInteractable()}");
                return;
            }
            button.onClick.Invoke();
            WriteLog("已调用正式动作按钮（程序调用，不代表鼠标命中测试）");
        }

        public void AddCoffeeBean()
        {
            if (!WorldReady()) return;
            world?.AddFromButton("coffee_bean");
            WriteLog("调用 CoffeeWorldRuntime.AddFromButton(coffee_bean)");
        }

        public void AddMilk()
        {
            if (!WorldReady()) return;
            world?.AddFromButton("milk");
            WriteLog("调用 CoffeeWorldRuntime.AddFromButton(milk)");
        }
        public void ClearLooseItems()
        {
            if (!WorldReady()) return;
            world?.ClearLooseItems();
            WriteLog("调用 CoffeeWorldRuntime.ClearLooseItems()");
        }

        public void DumpState()
        {
            RefreshReferences();
            WriteLog("已输出 Debug JSON 到 Console");
            Debug.Log("[CoffeeUI.Debug] " + BuildReport(true));
        }

        private void OnToast(string message) => WriteLog("Toast: " + message);

        private void WriteLog(string message)
        {
            message ??= string.Empty;
            if (message.Length > MaxLogLength) message = message.Substring(0, MaxLogLength) + "…";
            lastLog = $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (logText != null) logText.text = lastLog;
            Debug.Log("[CoffeeUI.Debug] " + message);
            refreshRequested = true;
        }

        private void RefreshView()
        {
            if (statusText != null) statusText.text = BuildReport(false);
            if (logText != null && !string.IsNullOrEmpty(lastLog)) logText.text = lastLog;
        }

        private string BuildReport(bool json)
        {
            var button = gameplayPanel?.grindButton;
            var session = controller?.Session;
            var canvas = diagnosticCanvas;
            var eventSystem = EventSystem.current;
            var entityCount = world == null ? 0 : world.GetComponentsInChildren<InteractiveEntity>(true).Length;
            var itemCount = world?.Ledger?.Items?.Count ?? 0;
            var step = session?.CurrentStep;
            var panelState = gameplayPanel == null ? "missing" : gameplayPanel.State.ToString();
            var popupActive = gameplayPanel?.beanPopup != null && gameplayPanel.beanPopup.activeInHierarchy;

            if (json)
            {
                var snapshot = new DebugSnapshot
                {
                    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    panelState = panelState,
                    panelActive = gameplayPanel != null && gameplayPanel.gameObject.activeInHierarchy,
                    canvasActive = canvas != null && canvas.gameObject.activeInHierarchy,
                    canvasEnabled = canvas != null && canvas.enabled,
                    eventSystemPresent = eventSystem != null,
                    entityCount = entityCount,
                    itemCount = itemCount,
                    phase = controller?.Day?.Phase.ToString() ?? "missing",
                    hasOrder = controller?.CurrentOrder != null,
                    hasSession = session != null,
                    step = step?.Kind.ToString() ?? "none",
                    actionButtonActive = button != null && button.gameObject.activeInHierarchy,
                    actionButtonEnabled = button != null && button.enabled,
                    actionButtonInteractable = button != null && button.IsInteractable(),
                    beanPopupActive = popupActive,
                    hasActiveCup = world != null && world.Ledger != null && world.ActiveCup != null,
                    lastLog = lastLog
                };
                return JsonUtility.ToJson(snapshot);
            }

            return $"[UI/实体诊断]\n" +
                   $"场景: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\n" +
                   $"Panel: {panelState} active={gameplayPanel != null && gameplayPanel.gameObject.activeInHierarchy}\n" +
                   $"Canvas: active={canvas != null && canvas.gameObject.activeInHierarchy} enabled={canvas != null && canvas.enabled}\n" +
                   $"EventSystem: {(eventSystem == null ? "缺失" : eventSystem.currentInputModule?.GetType().Name ?? "无输入模块")}\n" +
                   $"实体: {entityCount} · 运行时物品: {itemCount}\n" +
                   $"营业阶段: {controller?.Day?.Phase.ToString() ?? "missing"} · 订单: {(controller?.CurrentOrder == null ? "无" : "有")}\n" +
                   $"步骤: {step?.Kind.ToString() ?? "none"}\n" +
                   $"ActionButton: active={button != null && button.gameObject.activeInHierarchy}, enabled={button != null && button.enabled}, interactable={button != null && button.IsInteractable()}\n" +
                   $"筛豆弹窗: active={popupActive} · 当前杯: {world != null && world.Ledger != null && world.ActiveCup != null}\n" +
                   $"最后日志: {lastLog}";
        }

        [Serializable]
        private sealed class DebugSnapshot
        {
            public string scene, panelState, phase, step, lastLog;
            public bool panelActive, canvasActive, canvasEnabled, eventSystemPresent, hasOrder, hasSession;
            public bool actionButtonActive, actionButtonEnabled, actionButtonInteractable, beanPopupActive, hasActiveCup;
            public int entityCount, itemCount;
        }
    }
}
