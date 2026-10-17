using System;
using Spotlight.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>One UIKit-owned HUD shared by gameplay scenes, including standalone editor entry.</summary>
    [DefaultExecutionOrder(-600)]
    public sealed class GameplayHudPanel : GameFlowPanel
    {
        public const int SortingOrder = 660;
        public const float HeaderHeight = 210f;
        public static GameplayHudPanel Instance { get; private set; }
        public static bool BlocksWorldInput => Instance != null && Instance.IsDetailsOpen;
        public static bool IsHandlingEscape => DismissedFrame == Time.frameCount || Instance != null
            && (Instance.IsDetailsOpen || Instance.inventory.IsVisible || Instance.tasks.IsVisible);
        internal DetailsPanel OpenDetailsPanel => details;
        public static int DismissedFrame { get; private set; } = -1;
        [SerializeField] private RectTransform artwork;
        [SerializeField] private TextMeshProUGUI header, dateLabel;
        [SerializeField] private Image pointsFill;
        [SerializeField] private Image[] pointsSegments;
        [SerializeField] private MapActionInventoryView inventory;
        [SerializeField] private MapTaskPanel tasks;
        [SerializeField] private Button detailsButton, inventoryButton, tasksButton;
        [SerializeField] private Button endDayButton;
        private MapSceneController map;
        private CanvasGroup visibility;
        private GameFlowBootstrap flow;
        private DetailsPanel details;
        private NarrativeRuntime observedRuntime;
        private PlayerRuntime observedPlayer;
        private StandaloneGameplaySession standalone;
        private IDisposable detailsPause;
        private bool visible;
        private Vector2 authoredArtworkPosition;
        public MapActionInventoryView Inventory => inventory;
        public MapTaskPanel Tasks => tasks;
        public Button EndDayButton => endDayButton;
        public void BindMap(MapSceneController owner) => map = owner;
        public RectTransform Artwork => artwork;
        public bool IsDetailsOpen => details != null;
        public bool IsVisible => visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            DismissedFrame = -1;
            SceneManager.sceneLoaded -= SceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterScenes() => SceneManager.sceneLoaded += SceneLoaded;

        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsGameplayScene(scene)) Ensure();
        }

        private static bool IsGameplayScene(Scene scene)
        {
            if (scene.name == "Map" || scene.name == "CoffeeShop") return true;
            var config = Resources.Load<GameFlowConfig>("GameFlowConfig");
            return config != null && config.GameplayScene != config.MainMenuScene && scene.name == config.GameplayScene;
        }

        public static GameplayHudPanel Ensure()
        {
            if (Instance == null || !Instance.gameObject.activeInHierarchy)
                UIKit.OpenPanel<GameplayHudPanel>(UILevel.Common, tag: "gameplay-hud", cachePolicy: PanelCachePolicy.Reusable);
            return Instance;
        }

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            visibility = GetComponent<CanvasGroup>();
            authoredArtworkPosition = artwork.anchoredPosition;
            inventory.PointsFill = pointsFill;
            inventory.DateLabel = dateLabel;
            inventory.PointsSegments = pointsSegments;
            inventory.Initialize(header);
            artwork.GetComponent<MapActionPointsView>().Initialize(header, inventory.PointsFill, inventory.DateLabel, inventory.PointsSegments);
            tasks.Initialize();
            detailsButton.onClick.AddListener(ToggleDetails);
            inventoryButton.onClick.AddListener(ToggleInventory);
            tasksButton.onClick.AddListener(ToggleTasks);
            if (endDayButton != null) endDayButton.onClick.AddListener(() => { if (map != null && map.isActiveAndEnabled) map.FinishDayFromHud(); });
            // Keep one input owner as the map and cafe coexist additively.
            EventSystem input = UIKit.EnsureEventSystem();
            DontDestroyOnLoad(input.transform.root.gameObject);
            SceneManager.activeSceneChanged += ActiveSceneChanged;
        }

        protected override void OnShow()
        {
            base.OnShow();
            var canvas = GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
        }

        private void ActiveSceneChanged(Scene before, Scene after) => CloseWindows();

        private void Update()
        {
            if (flow == null) flow = FindFirstObjectByType<GameFlowBootstrap>();
            Scene active = SceneManager.GetActiveScene();
            bool gameplayScene = IsGameplayScene(active)
                || flow != null && flow.SessionState == GameSessionState.Playing
                || map != null && map.isActiveAndEnabled && map.gameObject.scene == active;
            if (gameplayScene && flow == null
                && (NarrativeRuntime.Instance == null || NarrativeRuntime.Instance.isActiveAndEnabled))
            {
                if (standalone == null) standalone = new StandaloneGameplaySession();
                standalone.Ensure();
            }
            else if (standalone != null) ReleaseStandaloneSession();
            BindSources();
            var cafe=PlayerCafeOwner.Current;
            bool workstation=active.name=="CoffeeShop" && cafe!=null && cafe.ProductionEnabled
                && cafe.State.currentArea==Spotlight.FreeCafe.AreaId.Counter && !(map!=null && map.isActiveAndEnabled);
            bool show = gameplayScene && !workstation && (flow == null || flow.SessionState == GameSessionState.Playing);
            if (visible != show)
            {
                visible = show;
                if (!show) CloseWindows();
            }
            if (endDayButton != null) endDayButton.gameObject.SetActive(map != null && map.isActiveAndEnabled);
            bool canInteract = CanInteract;
            visibility.alpha = show ? 1f : 0f;
            visibility.interactable = visibility.blocksRaycasts = show && canInteract;
            if (artwork != null && transform is RectTransform root)
            {
                bool cafeLayout = active.name == "CoffeeShop" && !(map != null && map.isActiveAndEnabled);
                float cafeFit = Mathf.Min(1f, root.rect.width / 1920f);
                artwork.localScale = Vector3.one * (cafeLayout ? .75f * cafeFit
                    : Mathf.Min(1f, Mathf.Max(.1f, (root.rect.width - 64f) / 1380f)));
                artwork.anchoredPosition = cafeLayout ? new Vector2(-6f, -85f * cafeFit) : authoredArtworkPosition;
            }
            if (!show || !canInteract || IsTyping()) return;
            Keyboard key = Keyboard.current;
            if (key == null) return;
            // DetailsPanel owns its close keys; do not reopen it on the closing frame.
            if (key.tabKey.wasPressedThisFrame && details == null && DismissedFrame != Time.frameCount) ToggleDetails();
            if (key.bKey.wasPressedThisFrame) ToggleInventory();
            if (key.tKey.wasPressedThisFrame) ToggleTasks();
            if (key.escapeKey.wasPressedThisFrame && details == null && (inventory.IsVisible || tasks.IsVisible))
            {
                CloseSidebars();
                DismissedFrame = Time.frameCount;
            }
        }

        private bool CanInteract => !DialoguePanel.IsAnyPlaying
            && !(NarrativeRuntime.Instance?.BlocksGameplayInput ?? false)
            && !(NarrativeRuntime.Instance?.Actions?.IsExecuting ?? false)
            && (flow == null || flow.SharedHudControlsAvailable)
            && !LoadingVisible();

        private static bool LoadingVisible()
        {
            var panel = UIKit.GetPanel<ShortLoadingPanel>();
            return panel != null && panel.gameObject.activeInHierarchy;
        }

        private static bool IsTyping()
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null) return false;
            var selected = EventSystem.current.currentSelectedGameObject;
            return selected.GetComponent<TMP_InputField>() != null || selected.GetComponent<InputField>() != null;
        }

        public void ToggleInventory()
        {
            if (!CanInteract || IsDetailsOpen) return;
            if (!inventory.IsVisible) tasks.SetVisible(false);
            inventory.Toggle();
        }

        public void ToggleTasks()
        {
            if (!CanInteract || IsDetailsOpen) return;
            if (!tasks.IsVisible) inventory.SetVisible(false);
            tasks.Toggle();
        }

        public void ToggleDetails()
        {
            if (details != null) { details.RequestClose(); return; }
            OpenDetails();
        }

        public void OpenDetails()
        {
            if (!CanInteract || details != null || PlayerRuntime.Current == null) return;
            CloseSidebars();
            details = UIKit.OpenPanel<DetailsPanel>(UILevel.Pop, DetailsGameDataAdapter.Create(), cachePolicy: PanelCachePolicy.Reusable);
            detailsPause = PresentationPauseRegistry.Acquire("gameplay-details");
            details.Closed += DetailsClosed;
        }

        private void DetailsClosed()
        {
            details = null;
            detailsPause?.Dispose();
            detailsPause = null;
            DismissedFrame = Time.frameCount;
        }

        private void BindSources()
        {
            var runtime = NarrativeRuntime.Instance;
            var player = PlayerRuntime.Current;
            if (observedRuntime == runtime && observedPlayer == player) return;
            UnbindSources();
            observedRuntime = runtime;
            observedPlayer = player;
            if (runtime != null) runtime.TurnCompleted += RefreshDetails;
            if (player != null) player.Changed += RefreshDetails;
            RefreshDetails();
            tasks.RefreshSource();
        }

        private void UnbindSources()
        {
            if (observedRuntime != null) observedRuntime.TurnCompleted -= RefreshDetails;
            if (observedPlayer != null) observedPlayer.Changed -= RefreshDetails;
            observedRuntime = null;
            observedPlayer = null;
        }

        public void RefreshDetails()
        {
            if (details != null) details.Refresh(DetailsGameDataAdapter.Create());
        }

        public void CloseSidebars()
        {
            if (inventory != null) inventory.SetVisible(false);
            if (tasks != null) tasks.SetVisible(false);
        }

        public void CloseWindows()
        {
            CloseSidebars();
            if (details != null) UIKit.ClosePanel(details);
            detailsPause?.Dispose();
            detailsPause = null;
            details = null;
        }

        public static void ReleaseStandaloneSession()
        {
            if (Instance == null || Instance.standalone == null) return;
            Instance.standalone.Dispose();
            Instance.standalone = null;
        }

        protected override void OnBeforeDestroy()
        {
            enabled = false; // UIKit releases ownership before Unity destroys the object at frame end.
            SceneManager.activeSceneChanged -= ActiveSceneChanged;
            CloseWindows();
            UnbindSources();
            standalone?.Dispose();
            standalone = null;
            if (Instance == this) Instance = null;
            base.OnBeforeDestroy();
        }
    }
}
