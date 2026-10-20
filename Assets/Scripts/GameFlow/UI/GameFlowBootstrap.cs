using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using YokiFrame;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    public enum GameSessionState { Menu, Loading, Playing, GameOver }

    public sealed class GameFlowBootstrap : MonoBehaviour
    {
        private const string MainStack = "game-flow-main";
        private const string MapSceneName = "Map";
        [SerializeField] private GameFlowConfig config;
        [SerializeField] private bool showMainMenuOnStart = true;
        [SerializeField] private bool enableNarrative;
        private GameSettingsService settings;
        private GameSaveService saves;
        private LoadingTipSequence tips;
        private Coroutine loadingRoutine;
        private LoadingPanel loadingPanel;
        private ShortLoadingPanel shortLoadingPanel;
        private OpeningProloguePanel openingProloguePanel;
        private int rootNavigationVersion;
        private int loadingVersion;
        private bool loadingTransitioning;
        private FSM<GameSessionState> session;
        private int sessionVersion;
        private NarrativeController boundController;
        private NarrativeEndingDefinition selectedEnding;
        public GameSessionState SessionState => session?.CurEnum ?? GameSessionState.Menu;
        public bool CanSaveCurrentSession => !NarrativeAutoRunner.IsActive && !NarrativeAutoRunner.IsWalkthroughActive && (dayFlow == null || dayFlow.CanSave);
        private bool gameplay => SessionState == GameSessionState.Playing;

        private sealed class SessionPhase : AbstractState<GameSessionState, GameFlowBootstrap>
        {
            private readonly GameSessionState phase;
            public SessionPhase(FSM<GameSessionState> fsm, GameFlowBootstrap owner, GameSessionState phase) : base(fsm, owner) { this.phase = phase; }
            protected override void OnEnter()
            {
                if (phase == GameSessionState.GameOver) mBlack.EnterGameOver();
            }
        }

        private void SetSessionState(GameSessionState value)
        {
            if (session == null)
            {
                session = new FSM<GameSessionState>("Spotlight Session");
                foreach (GameSessionState state in Enum.GetValues(typeof(GameSessionState)))
                    session.Add(state, new SessionPhase(session, this, state));
                session.Start(value);
            }
            else if (session.CurEnum != value) session.Change(value);
        }
        private bool paused;
        private float playtime;
        private float previousTimeScale = 1f;
        private float autoSaveTimer;
        private float resolutionConfirmTimer;
        private int previousResolutionWidth;
        private int previousResolutionHeight;
        private bool awaitingResolutionConfirmation;
        private NarrativeGameFlowIntegration narrativeIntegration;
        private NarrativeSnapshot pendingNarrativeRestore;
        private PlayerState pendingPlayerRestore;
        private readonly PlayerSessionCoordinator playerSession = new PlayerSessionCoordinator();
        private bool loadingSavedSession;
        private bool pendingDefaultCreation;
        private DayFlowController dayFlow;
        private DetailsPanel detailsPanel => GameplayHudPanel.Instance != null ? GameplayHudPanel.Instance.OpenDetailsPanel : null;
        private MapSceneController mapOverlay;
        private bool openingMap;
        private int mapNavigationVersion;
        private GameFlowPanel gameplayScenePanel;
        public bool IsMapOpen => mapOverlay != null || openingMap;
        public bool IsDetailsOpen => detailsPanel != null;
        internal bool SharedHudControlsAvailable => gameplay && !paused && !loadingTransitioning && !openingMap
            && !saving && (dayFlow == null || !dayFlow.BlocksGameplayInput);
        public bool CanOpenMap => CanOpenMapFromGameplayScene() && !IsMapOpen
            && !paused && !saving && !openingPause && Time.timeScale > 0f && !PresentationPauseRegistry.IsPaused
            && !DialoguePanel.IsAnyPlaying && !CafeMiniGameActive
            && !(narrativeIntegration?.Runtime?.BlocksGameplayInput ?? false)
            && !(narrativeIntegration?.Runtime?.Actions?.IsExecuting ?? false)
            && (dayFlow == null || !dayFlow.IsFirstSessionGuideActive && !dayFlow.BlocksGameplayInput)
            && SceneManager.GetActiveScene().name != MapSceneName;
        private static bool CafeMiniGameActive
            => !string.IsNullOrEmpty(PlayerCafeOwner.Current?.State.production?.miniGameToken);
        internal bool MapControlsAvailable => gameplay && !paused && !saving && !openingPause && !loadingTransitioning
            && Time.timeScale > 0f && detailsPanel == null
            && !(narrativeIntegration?.Runtime?.Actions?.IsExecuting ?? false)
            && (dayFlow != null && dayFlow.IsFirstDayCafeMapGuideActive
                || !DialoguePanel.IsAnyPlaying && !(narrativeIntegration?.Runtime?.BlocksGameplayInput ?? false)
                    && (dayFlow == null || !dayFlow.BlocksGameplayInput))
            && (PlayerRuntime.Current?.CreationSubmitted == true || dayFlow != null && dayFlow.State == DayFlowState.CityMapLeisure);
        private int detailsClosedFrame = -1;
        private EventSystem persistentEventSystem;
        private bool saving;
        private Action pendingSaveCompletion;
        private Texture2D gameplayThumbnail;
        private bool openingPause;
        public bool HasReadableSave => saves.List().Any(slot => slot.CanLoad);

        public GameSettings CurrentSettings => settings.Current;
        internal bool GameplayControlsAvailable => CanUseGameplayControls();
        internal bool CanUseTutorialTestSkip => gameplay && !paused && !saving && !loadingTransitioning
            && !IsMapOpen && detailsPanel == null;
        internal bool CanPreviewDialogue => SessionState != GameSessionState.GameOver && !paused && !loadingTransitioning
            && !(narrativeIntegration?.Runtime?.BlocksGameplayInput ?? false)
            && (!gameplay || CanUseGameplayControls());
        public IReadOnlyList<ResolutionOption> ResolutionOptions => settings.ResolutionOptions;
        public string WindowModeLabel => settings.Current.WindowMode == FullScreenMode.Windowed
            ? "窗口"
            : "无边框全屏";
        public string ResolutionLabel => settings.Current.ResolutionWidth + " x " + settings.Current.ResolutionHeight;

        private static GameFlowBootstrap instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }
            instance = this;
            GameplayHudPanel.ReleaseStandaloneSession();
            SetSessionState(GameSessionState.Menu);
            DontDestroyOnLoad(transform.root.gameObject);
            persistentEventSystem = EventSystem.current;
            if (persistentEventSystem == null)
                persistentEventSystem = CreateEventSystem();
            DontDestroyOnLoad(persistentEventSystem.transform.root.gameObject);
            RemoveDuplicateEventSystems(persistentEventSystem);
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (config == null)
                config = Resources.Load<GameFlowConfig>("GameFlowConfig");
            if (config == null)
            {
                Debug.LogError("GameFlowBootstrap requires GameFlowConfig.", this);
                enabled = false;
                return;
            }

            GameSettingsServiceAdapter settingsAdapter = SpotlightArchitecture.Interface.GetService<GameSettingsServiceAdapter>();
            GameSaveServiceAdapter saveAdapter = SpotlightArchitecture.Interface.GetService<GameSaveServiceAdapter>();
            settings = settingsAdapter.Service;
            settings.Apply();
            saves = saveAdapter.Service;
            tips = new LoadingTipSequence(config.LoadingTips);
            // The formal session always owns the sole narrative/condition runtime.
            enableNarrative = true;
            {
                narrativeIntegration = NarrativeGameFlowIntegration.Instance;
                if (narrativeIntegration == null)
                {
                    GameObject runtimeObject = new GameObject("NarrativeGameFlowRuntime");
                    narrativeIntegration = runtimeObject.AddComponent<NarrativeGameFlowIntegration>();
                }
                narrativeIntegration.EnsureRuntime();
                narrativeIntegration.Runtime.ReadingPlayback = ReadingEventPlayback.Play;
                narrativeIntegration.Runtime.NoticePlayback = NarrativeNoticeEventPlayback.Play;
                narrativeIntegration.Runtime.ConversationPlayback = NarrativeConversationEventPlayback.Play;
                narrativeIntegration.Runtime.LetterPlayback = LetterEventPlayback.Play;
                narrativeIntegration.Runtime.CharacterResolver = NarrativeCharacterResolver.Resolve;
                narrativeIntegration.Runtime.PlayerNameProvider = () => PlayerRuntime.Current?.PlayerName ?? string.Empty;
            }
            dayFlow = GetComponent<DayFlowController>();
            if (dayFlow == null)
                dayFlow = gameObject.AddComponent<DayFlowController>();
            dayFlow.Initialize(this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (GetComponent<DialoguePreviewHotkey>() == null) gameObject.AddComponent<DialoguePreviewHotkey>();
#endif
            if (showMainMenuOnStart)
                StartCoroutine(StartupRoutine());
            else
                StartGame();
        }

        private IEnumerator StartupRoutine()
        {
            // The startup scene already contains the title entry point. Do not run
            // the splash or the timed loading transition just to open its menu.
            int buildIndex = FindBuildIndex(config.MainMenuScene);
            if (buildIndex >= 0 && buildIndex != SceneManager.GetActiveScene().buildIndex)
            {
                loadingTransitioning = true;
                yield return SceneManager.LoadSceneAsync(buildIndex);
            }
            else if (buildIndex < 0)
            {
                Debug.LogWarning("Configured scene is not in Build Settings: " + config.MainMenuScene, this);
            }

            loadingTransitioning = false;
            OpenMainMenu();
        }

        private void OnDestroy()
        {
            // A rejected scene duplicate never owned the shared player/narrative session.
            if (instance != this) return;
            instance = null;
            openingProloguePanel?.Stop();
            playerSession.Dispose();
            PlayerRuntime.Current = null;
            PlayerCafeOwner.EndSession();
            if (gameplayThumbnail != null) Destroy(gameplayThumbnail);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (boundController != null) boundController.EndingSelected -= OnEndingSelected;
            session?.Dispose();
            DialoguePlayback.Stop();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EventSystem[] systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventSystem preferred = persistentEventSystem != null && persistentEventSystem ? persistentEventSystem : systems.FirstOrDefault();
            if (preferred == null)
                preferred = CreateEventSystem();
            persistentEventSystem = preferred;
            DontDestroyOnLoad(persistentEventSystem.transform.root.gameObject);
            RemoveDuplicateEventSystems(preferred);
        }

        private static EventSystem CreateEventSystem()
        {
            GameObject root = new GameObject("PersistentEventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            return root.GetComponent<EventSystem>();
        }

        private static void RemoveDuplicateEventSystems(EventSystem keep)
        {
            foreach (EventSystem system in FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (system != keep) Destroy(system.gameObject);
        }

        private void Update()
        {
            // The background verifier owns no formal state. Freeze host commands and all periodic
            // save/flush work until it releases its own lease, including already-open menus.
            if (NarrativeAutoRunner.IsActive) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                HandleEscape();
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame && !IsTypingInInputField())
            {
                if (IsMapOpen) CloseMap();
                else OpenMap();
            }
            if (gameplay && !paused && !loadingTransitioning)
            {
                narrativeIntegration?.Runtime?.Controller?.FlushValueChanges();
                float deltaTime = Time.unscaledDeltaTime;
                playtime += deltaTime;
                // Test time is not ordinary play time for autosave scheduling. Without this reset,
                // stopping a walkthrough can immediately save the assisted test session.
                if (NarrativeAutoRunner.IsWalkthroughActive) autoSaveTimer = 0f;
                if (!loadingSavedSession && !NarrativeAutoRunner.IsWalkthroughActive)
                {
                    autoSaveTimer += deltaTime;
                    if (config.AutoSaveIntervalSeconds > 0f && autoSaveTimer >= config.AutoSaveIntervalSeconds)
                    {
                        autoSaveTimer %= config.AutoSaveIntervalSeconds;
                        if (CanSaveCurrentSession && !saving && !TryAutoSave(out string autoSaveError))
                            Debug.LogWarning("Autosave failed: " + autoSaveError, this);
                    }
                }
            }
            if (awaitingResolutionConfirmation)
            {
                resolutionConfirmTimer -= Time.unscaledDeltaTime;
                UIKit.GetPanel<ConfirmationPanel>()?.SetMessage($"保持此分辨率？将在 {Mathf.CeilToInt(Mathf.Max(0f, resolutionConfirmTimer))} 秒后恢复原设置。");
                if (resolutionConfirmTimer <= 0f)
                {
                    RevertResolution();
                    UIKit.GetPanel<ConfirmationPanel>()?.DismissFromFlow();
                }
            }
            if (narrativeIntegration?.GameplayHUD != null)
                narrativeIntegration.GameplayHUD.CanInteract = CanUseGameplayControls;
            narrativeIntegration?.Runtime?.Presenter?.SetSuspended(!CanUseNarrativePresentation());
            if (loadingPanel != null && keyboard != null)
            {
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                    loadingPanel.SetTip(tips.Next());
                else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                    loadingPanel.SetTip(tips.Previous());
            }
        }

        private static bool IsTypingInInputField()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && (selected.GetComponentInParent<TMPro.TMP_InputField>() != null
                || selected.GetComponentInParent<UnityEngine.UI.InputField>() != null);
        }

        public void OpenMap()
        {
            OpenMapInternal(false);
        }

        /// <summary>True means the caller must stop, whether settlement was accepted or refused.</summary>
        public bool TryCloseCafeForMapAction(string position, string actionKey)
        {
            if (PlayerCafeOwner.Current?.State.open != true) return false;
            // Only a formal session owns DayFlow. Never fall through to an action with an open
            // formal cafe when closing is currently unsafe or the endpoint refused the request.
            int version = sessionVersion;
            var runtime = narrativeIntegration?.Runtime;
            var actions = runtime?.Actions;
            var world = PlayerCafeOwner.Current;
            int day = dayFlow != null ? dayFlow.CurrentDay : 0;
            string instanceKey = !string.IsNullOrWhiteSpace(position) && !string.IsNullOrWhiteSpace(actionKey)
                ? actions?.GetActionInstanceKey(position, actionKey) : null;
            if (!isActiveAndEnabled || !gameplay || paused || saving || openingPause || loadingTransitioning
                || Time.timeScale <= 0f || PresentationPauseRegistry.IsPaused || CafeMiniGameActive
                || DialoguePanel.IsAnyPlaying || dayFlow == null || dayFlow.IsFirstSessionGuideActive
                || (runtime?.BlocksGameplayInput ?? false) || (actions?.IsExecuting ?? false)
                || string.IsNullOrEmpty(instanceKey)
                || !dayFlow.TryFinishBusinessForMapAction(() =>
                {
                    if (!isActiveAndEnabled || !gameplay || version != sessionVersion) return;
                    if (dayFlow == null || dayFlow.CurrentDay != day || !ReferenceEquals(world, PlayerCafeOwner.Current)
                        || !ReferenceEquals(runtime, narrativeIntegration?.Runtime) || !ReferenceEquals(actions, runtime?.Actions)
                        || instanceKey != actions.GetActionInstanceKey(position, actionKey))
                    {
                        OperationToastPanel.ShowMessage("所选行动或游戏状态已变化，已取消自动继续，请重新选择行动。");
                        return;
                    }
                    FindMapPanel(SceneManager.GetSceneByName(MapSceneName))?.ResumeRequestedAction(position, actionKey);
                }))
            {
                OperationToastPanel.ShowMessage("当前无法自动打烊，请先完成当前操作后重试所选行动。");
                return true;
            }
            OperationToastPanel.ShowMessage("咖啡店已自动打烊，营业结算后继续所选行动。未完成订单按打烊规则结算。", string.Empty);
            return true;
        }

        public bool DebugForceOpenMap(out string message)
        {
            message = "无法打开地图：当前还未进入游戏。";
            if (!CanOpenMapFromGameplayScene()) return false;
            if (IsMapOpen || SceneManager.GetActiveScene().name == MapSceneName) { message = "地图已经打开。"; return true; }
            CloseDetails();
            openingMap = true;
            StartCoroutine(OpenMapRoutine(true));
            message = "已打开最高层地图，当前玩法场景保持加载。";
            return true;
        }

        private void OpenMapInternal(bool ignoreTutorial)
        {
            if (!CanOpenMap)
            {
                if (IsMapOpen || gameplay && SceneManager.GetActiveScene().name == MapSceneName) return;
                OperationToastPanel.ShowBlocked(InteractionBlockedMessage("打开地图"));
                return;
            }
            CloseDetails();
            openingMap = true;
            StartCoroutine(OpenMapRoutine(ignoreTutorial));
        }

        public string InteractionBlockedMessage(string operation)
        {
            if (!gameplay) return "请先进入游戏，再" + operation + "。";
            if (loadingTransitioning || openingMap) return "正在切换场景或打开地图，请稍候。";
            if (saving) return "正在保存，请稍后再" + operation + "。";
            if (paused || openingPause || Time.timeScale <= 0f) return "请先恢复游戏，再" + operation + "。";
            if (dayFlow?.IsFirstSessionGuideActive == true) return "请先按照新手教程完成当前步骤，再" + operation + "。";
            if (CafeMiniGameActive) return "请先完成或退出当前制作小游戏，再" + operation + "。";
            if (narrativeIntegration?.Runtime?.Actions?.IsExecuting == true) return "当前行动正在执行，请完成后再" + operation + "。";
            if (DialoguePanel.IsAnyPlaying || narrativeIntegration?.Runtime?.BlocksGameplayInput == true)
                return "当前剧情尚未结束，请完成对话或事件后再" + operation + "。";
            return "当前阶段暂不能" + operation + "，请先完成当前操作。";
        }

        private bool CanOpenMapFromGameplayScene()
            => gameplay && !loadingTransitioning
                && SceneManager.GetActiveScene().name != config.MainMenuScene;

        private IEnumerator OpenMapRoutine(bool ignoreTutorial = false)
        {
            int version = sessionVersion;
            int mapVersion = mapNavigationVersion;
            // Admission was checked before openingMap became true. Rechecking gameplay
            // controls here would reject our own IsMapOpen loading guard.
            Scene scene = SceneManager.GetSceneByName(MapSceneName);
            if (!scene.isLoaded)
            {
                ShortLoadingPanel transition = UIKit.OpenPanel<ShortLoadingPanel>(UILevel.AlwayTop,
                    tag: "game-flow-short-loading", cachePolicy: PanelCachePolicy.Reusable);
                while (transition != null && !transition.IsCovered) yield return null;
                yield return SceneManager.LoadSceneAsync(MapSceneName, LoadSceneMode.Additive);
                if (transition != null) transition.EndLoading();
            }
            scene = SceneManager.GetSceneByName(MapSceneName);
            if (mapVersion != mapNavigationVersion) yield break;
            if (!gameplay || version != sessionVersion) { openingMap = false; yield break; }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    camera.gameObject.SetActive(false);
                if (mapOverlay == null)
                    mapOverlay = root.GetComponentsInChildren<MapSceneController>(true)
                        .FirstOrDefault(panel => panel.transform is RectTransform);
            }
            if (mapOverlay == null)
            {
                openingMap = false;
                Debug.LogError("Map 场景未找到地图面板，保留咖啡馆界面。");
                yield break;
            }
            mapOverlay.OpenOverlay(this);
            openingMap = false;
            dayFlow?.RefreshMapAvailability();
        }

        public void CloseMap()
        {
            if (!gameplay || paused || saving || openingPause || loadingTransitioning
                || (narrativeIntegration?.Runtime?.Actions?.IsExecuting ?? false)
                || (narrativeIntegration?.Runtime?.BlocksGameplayInput ?? false)
                || DialoguePanel.IsAnyPlaying || (dayFlow != null && dayFlow.BlocksGameplayInput)
                || mapOverlay != null && mapOverlay.GetComponentInChildren<MapActionDetailsView>(true)?.IsBusy == true)
            {
                OperationToastPanel.ShowBlocked(InteractionBlockedMessage("关闭地图或返回咖啡馆"));
                return;
            }
            ForceCloseMap();
        }

        // Host-owned transitions must still release the overlay while ordinary user dismissal
        // is locked. In particular AfterBusiness cannot run behind a suspended map presenter.
        internal void CloseMapForDayFlow() => ForceCloseMap();

        private void ForceCloseMap()
        {
            // Invalidate an additive open still waiting on its cover/load as well as a visible
            // overlay, so a timer-driven settlement cannot reopen the map over its confirmation.
            ++mapNavigationVersion;
            openingMap = false;
            if (mapOverlay != null) mapOverlay.gameObject.SetActive(false);
            mapOverlay = null;
            dayFlow?.RefreshMapAvailability();
        }

        public void TransitionToGameplayScene(string sceneName, Action completed, Action failed = null)
        {
            if (!gameplay)
            {
                failed?.Invoke();
                return;
            }
            if (string.IsNullOrWhiteSpace(sceneName) || sceneName == SceneManager.GetActiveScene().name)
            {
                try
                {
                    CompletePendingPlayerCreation();
                    completed?.Invoke();
                }
                catch (Exception error)
                {
                    Debug.LogException(error, this);
                    failed?.Invoke();
                }
                return;
            }
            // Resident scene adapters already own live data: Start will not run on return.
            // Carry a cafe snapshot only while there is no loaded adapter to preserve it.
            var handoff = GameSaveParticipantRegistry.CaptureAll();
            handoff.RemoveAll(item => item != null && (item.Id == DayFlowController.SaveId
                || item.Id == PlayerRuntime.SaveId
                || item.Id == PlayerCafeOwner.SaveId && PlayerCafeOwner.HasSceneOwner));
            GameSaveParticipantRegistry.PendingRestore = handoff;
            StartCoroutine(TransitionGameplaySceneRoutine(sceneName, completed, failed));
        }

        private IEnumerator TransitionGameplaySceneRoutine(string sceneName, Action completed, Action failed)
        {
            int version = ++loadingVersion;
            Scene previousActive = SceneManager.GetActiveScene();
            GameFlowPanel previousGameplayPanel = gameplayScenePanel;
            bool previousGameplayPanelActive = previousGameplayPanel != null && previousGameplayPanel.gameObject.activeSelf;
            MapSceneController previousMapOverlay = mapOverlay;
            bool previousMapOverlayActive = previousMapOverlay != null && previousMapOverlay.gameObject.activeSelf;
            List<GameObject> previousMapCameras = CaptureActiveMapCameras(previousActive);
            SetSessionState(GameSessionState.Loading);
            CloseDetails();
            ForceCloseMap();
            loadingTransitioning = true;
            narrativeIntegration?.SetGameplayVisible(false);

            try
            {
                if (shortLoadingPanel != null) UIKit.ClosePanel(shortLoadingPanel);
                shortLoadingPanel = UIKit.OpenPanel<ShortLoadingPanel>(UILevel.AlwayTop,
                    tag: "game-flow-short-loading", cachePolicy: PanelCachePolicy.Reusable);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                FailGameplaySceneTransition(version, shortLoadingPanel, previousActive, previousGameplayPanel,
                    previousGameplayPanelActive, previousMapOverlay, previousMapOverlayActive, previousMapCameras, failed,
                    error.Message);
                yield break;
            }
            while (shortLoadingPanel != null && !shortLoadingPanel.IsCovered) yield return null;
            if (version != loadingVersion) yield break;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var transferWatch = System.Diagnostics.Stopwatch.StartNew();
#endif

            if (sceneName != MapSceneName) HideResidentMap();

            Scene destination = SceneManager.GetSceneByName(sceneName);
            if (!destination.IsValid() || !destination.isLoaded)
            {
                AsyncOperation load;
                try { load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive); }
                catch (Exception error)
                {
                    Debug.LogException(error, this);
                    FailGameplaySceneTransition(version, shortLoadingPanel, previousActive, previousGameplayPanel,
                        previousGameplayPanelActive, previousMapOverlay, previousMapOverlayActive, previousMapCameras, failed,
                        error.Message);
                    yield break;
                }
                if (load == null)
                {
                    Debug.LogError("无法加载游戏场景：" + sceneName, this);
                    FailGameplaySceneTransition(version, shortLoadingPanel, previousActive, previousGameplayPanel,
                        previousGameplayPanelActive, previousMapOverlay, previousMapOverlayActive, previousMapCameras, failed,
                        "Gameplay scene transition failed.");
                    yield break;
                }
                yield return load;
                destination = SceneManager.GetSceneByName(sceneName);
            }

            if (version != loadingVersion) yield break;
            if (!destination.IsValid() || !destination.isLoaded)
            {
                FailGameplaySceneTransition(version, shortLoadingPanel, previousActive, previousGameplayPanel,
                    previousGameplayPanelActive, previousMapOverlay, previousMapOverlayActive, previousMapCameras, failed,
                    "Destination scene was not ready after loading.");
                yield break;
            }

            Scene previous = SceneManager.GetActiveScene();
            if (destination != previous)
                SceneManager.SetActiveScene(destination);

            if (sceneName == MapSceneName)
            {
                MapSceneController map = FindMapPanel(destination);
                if (map == null)
                {
                    Debug.LogError("Map 场景未找到地图面板。", this);
                    FailGameplaySceneTransition(version, shortLoadingPanel, previousActive, previousGameplayPanel,
                        previousGameplayPanelActive, previousMapOverlay, previousMapOverlayActive, previousMapCameras, failed,
                        "Gameplay scene transition failed.");
                    yield break;
                }
                map.RegisterAsGameplayPanel();
                mapOverlay = null;
                gameplayScenePanel = GameplayPanelHooks.OpenPanel(this);
                if (gameplayScenePanel == null)
                {
                    FailGameplaySceneTransition(version, shortLoadingPanel, previousActive, previousGameplayPanel,
                        previousGameplayPanelActive, previousMapOverlay, previousMapOverlayActive, previousMapCameras, failed,
                        "Map scene has no registered gameplay panel.");
                    yield break;
                }
            }
            else
            {
                mapOverlay = null;
                RegisterGameplayPanel(destination);
                GameFlowPanel panel = GameplayPanelHooks.OpenPanel != null
                    ? GameplayPanelHooks.OpenPanel(this)
                    : null;
                if (panel == null)
                {
                    Debug.LogError("场景未注册玩法面板：" + sceneName, this);
                    FailGameplaySceneTransition(version, shortLoadingPanel, previousActive, previousGameplayPanel,
                        previousGameplayPanelActive, previousMapOverlay, previousMapOverlayActive, previousMapCameras, failed,
                        "Gameplay scene transition failed.");
                    yield break;
                }
                gameplayScenePanel = panel;
            }

            try { CompletePendingPlayerCreation(); }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                FailGameplaySceneTransition(version, shortLoadingPanel, previousActive, previousGameplayPanel,
                    previousGameplayPanelActive, previousMapOverlay, previousMapOverlayActive, previousMapCameras, failed,
                    error.Message);
                yield break;
            }
            if (sceneName != MapSceneName)
                yield return UnloadGameplaySceneExcept(MapSceneName, sceneName);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long sceneReadyMs = transferWatch.ElapsedMilliseconds;
#endif
            // 目标场景的 Awake/Start（Luban 全量解析、角色立绘、经营实体视图）和紧接着的
            // 玩法面板/演出预载是整次转场里最重的几帧。必须让它们全部落在遮罩仍然全不透明时，
            // 否则淡出动画会从一帧卡顿上起跑，玩家看到的就是"进店后卡一下"。
            // 卸载旧场景没有内容可卸时上面不会 yield，这里补一帧，保证 Start 已经执行完。
            yield return null;

            SetSessionState(GameSessionState.Playing);
            narrativeIntegration?.SetGameplayVisible(true);
            // 后续演出（开店、地图）也在遮罩下打开：它的 Prefab 加载和贴图上传属于隐藏段，
            // 不属于淡出后的第一帧可见画面。
            try { completed?.Invoke(); }
            catch (Exception error) { Debug.LogException(error, this); }
            // 让演出刚打开的 UI 完成第一次 Awake/OnInit 布局与首帧渲染，再开始淡出。
            yield return null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long preFadeMs = transferWatch.ElapsedMilliseconds;
#endif
            // 引擎在进场后会自己跑一次 Resources.UnloadUnusedAssets（实测释放 0.92GB / 3908 个资源，
            // 约 380ms）。它原本落在淡出第一帧上，把淡出冻住再跳一下，这正是"进店卡一下"的最后一环。
            // 这段回收本身不可省，但趁遮罩还全不透明时主动做掉，就等于把它移进隐藏段。
            yield return Resources.UnloadUnusedAssets();
            yield return null;
            FinishGameplaySceneTransition(version, shortLoadingPanel);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float worstMs = 0f;
            int worstAt = -1;
            for (int frame = 0; frame < 45; frame++)
            {
                yield return null;
                float ms = Time.unscaledDeltaTime * 1000f;
                if (ms > worstMs) { worstMs = ms; worstAt = frame; }
            }
            Debug.Log("[GameFlow] " + sceneName + " 转场：就绪 " + sceneReadyMs
                + "ms / 预热 " + (preFadeMs - sceneReadyMs)
                + "ms / 遮罩内资源回收 " + (transferWatch.ElapsedMilliseconds - preFadeMs)
                + "ms；淡出后 45 帧最差 " + worstMs.ToString("F1") + "ms（第 " + worstAt + " 帧）", this);
#endif
        }

        private static MapSceneController FindMapPanel(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MapSceneController panel in root.GetComponentsInChildren<MapSceneController>(true))
                    if (panel.transform is RectTransform) return panel;
            }
            return null;
        }

        private static List<GameObject> CaptureActiveMapCameras(Scene scene)
        {
            var active = new List<GameObject>();
            if (!scene.IsValid() || !scene.isLoaded) return active;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera.gameObject.activeSelf) active.Add(camera.gameObject);
            return active;
        }

        private static void RegisterGameplayPanel(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component is IGameplayScenePanelRegistration registration)
                    {
                        registration.RegisterAsGameplayPanel();
                        return;
                    }
        }

        private void HideResidentMap()
        {
            Scene scene = SceneManager.GetSceneByName(MapSceneName);
            if (!scene.IsValid() || !scene.isLoaded) return;
            MapSceneController panel = FindMapPanel(scene);
            if (panel != null) panel.gameObject.SetActive(false);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    camera.gameObject.SetActive(false);
            mapOverlay = null;
        }

        private IEnumerator UnloadGameplaySceneExcept(string preservedSceneName, string destinationName)
        {
            for (int index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded || scene.name == preservedSceneName || scene.name == destinationName
                    || scene == gameObject.scene) continue;
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        private void FinishGameplaySceneTransition(int version, ShortLoadingPanel transition)
        {
            if (version != loadingVersion) return;
            loadingTransitioning = false;
            shortLoadingPanel = null;
            if (transition != null) transition.EndLoading();
        }

        private void FailGameplaySceneTransition(int version, ShortLoadingPanel transition, Scene previousActive,
            GameFlowPanel previousGameplayPanel, bool previousGameplayPanelActive, MapSceneController previousMapOverlay,
            bool previousMapOverlayActive, List<GameObject> previousMapCameras, Action failed, string reason)
        {
            if (version != loadingVersion) return;
            loadingTransitioning = false;
            shortLoadingPanel = null;
            if (transition != null) transition.EndLoading();

            if (previousActive.IsValid() && previousActive.isLoaded)
                SceneManager.SetActiveScene(previousActive);

            if (gameplayScenePanel != null && gameplayScenePanel != previousGameplayPanel)
                gameplayScenePanel.gameObject.SetActive(false);
            gameplayScenePanel = previousGameplayPanel;
            if (previousGameplayPanel != null) previousGameplayPanel.gameObject.SetActive(previousGameplayPanelActive);
            if (previousMapOverlay != null)
            {
                previousMapOverlay.gameObject.SetActive(previousMapOverlayActive);
                mapOverlay = previousMapOverlay;
            }
            else
            {
                mapOverlay = null;
                if (previousActive.name == MapSceneName)
                {
                    MapSceneController map = FindMapPanel(previousActive);
                    if (map != null)
                    {
                        map.gameObject.SetActive(previousGameplayPanelActive);
                        gameplayScenePanel = map;
                    }
                }
            }
            if (previousMapCameras != null)
                foreach (GameObject camera in previousMapCameras)
                    if (camera != null) camera.SetActive(true);

            SetSessionState(GameSessionState.Playing);
            narrativeIntegration?.SetGameplayVisible(true);
            Debug.LogError("Gameplay scene transition failed and was restored: " + reason, this);
            try { failed?.Invoke(); }
            catch (Exception error) { Debug.LogException(error, this); }
        }

        private void CompletePendingPlayerCreation()
        {
            if (PlayerRuntime.Current?.CreationSubmitted == true)
            {
                var runtime = narrativeIntegration?.Runtime;
                if (!playerSession.IsBoundTo(runtime, PlayerCafeOwner.Current))
                {
                    playerSession.Bind(runtime, PlayerCafeOwner.Current, dayFlow);
                    runtime.Controller.CompleteValueRestore(evaluateInitial: false);
                    PlayerRuntime.Current.NotifyOwnersChanged();
                }
                return;
            }
            if (!TrySubmitPlayerCreation(pendingDefaultCreation ? null : PlayerRuntime.Current?.RestoreDraft()))
                throw new InvalidOperationException("无法完成角色创建，请检查玩家与咖啡馆运行时初始化状态。");
        }

        public bool DebugSkipTutorialToMap(out string message)
        {
            message = "场景正在加载，请稍后重试。";
            if (loadingTransitioning) return false;
            if (openingProloguePanel != null)
            {
                var panel = openingProloguePanel;
                panel.Stop();
                FinishOpeningPrologue(panel);
            }
            if (dayFlow == null)
            {
                message = "请先开始新游戏。";
                return false;
            }
            return dayFlow.DebugSkipTutorialToMap(out message);
        }

        private bool fastTestStartup;
        public void StartGame() => StartGameInternal(false);
        public void StartFastTestGame() => StartGameInternal(true);
        private void StartGameInternal(bool fastStartup)
        {
            fastTestStartup = fastStartup;
            if (openingProloguePanel != null)
            {
                openingProloguePanel.Stop();
                UIKit.ClosePanel(openingProloguePanel);
                openingProloguePanel = null;
            }
            ++sessionVersion;
            // The prologue has its own Canvas sorting order. Do not leave the opaque
            // topmost menu behind it while a new session is loading/starting.
            ++rootNavigationVersion;
            UIKit.ClearStack(MainStack, true);
            MainMenuPanel mainMenu = UIKit.GetPanel<MainMenuPanel>();
            if (mainMenu != null) UIKit.ClosePanel(mainMenu);
            selectedEnding = null;
            GameSaveParticipantRegistry.PendingRestore = null;
            DialoguePlayback.Stop();
            NarrativeEndingEcho.Clear();
            DialogueHistoryService.ClearFormal();
            playtime = 0f;
            autoSaveTimer = 0f;
            pendingNarrativeRestore = null;
            pendingDefaultCreation = false;
            loadingSavedSession = false;
            playerSession.Dispose();
            PlayerCafeOwner.EndSession();
            PlayerRuntime.Current = new PlayerRuntime(PlayerCreationCatalog.Load());
            PlayerSaveBridge.Register(() => PlayerRuntime.Current);
            narrativeIntegration?.ResetSession();
            BindNarrativeSession();
            SetSessionState(GameSessionState.Loading);
            paused = false;
            // 场景切换开关：留空则留在当前场景，直接打开玩法面板
            if (string.IsNullOrWhiteSpace(config.GameplayScene))
            {
                InitializeNewGameplay();
            }
            else
            {
                LoadThen(() =>
                {
                    InitializeNewGameplay();
                }, config.GameplayScene, forceReload: true);
            }
        }

        private void InitializeNewGameplay()
        {
            try
            {
                if (fastTestStartup)
                {
                    fastTestStartup = false;
                    OpenGameplayPlaceholder();
                    dayFlow.BeginFastTestSession();
                    CompleteNarrativeRestore(false);
                    return;
                }
                // New games alone get the standalone prologue. Gameplay/DayFlow are not opened until it completes.
                if (openingProloguePanel == null)
                {
                    openingProloguePanel = UIKit.OpenPanel<OpeningProloguePanel>(UILevel.AlwayTop,
                        tag: "game-opening-prologue", cachePolicy: PanelCachePolicy.Transient);
                    OpeningProloguePanel panel = openingProloguePanel;
                    panel.Play(() => FinishOpeningPrologue(panel));
                    return;
                }
                FinishOpeningPrologue(openingProloguePanel);
            }
            catch (Exception exception)
            {
                openingProloguePanel = null;
                OpenMainMenu();
                ShowResult("初始化失败", exception.Message, () => { });
            }
        }

        private void FinishOpeningPrologue(OpeningProloguePanel panel)
        {
            if (panel == null || panel != openingProloguePanel) return;
            openingProloguePanel = null;
            UIKit.ClosePanel(panel);
            try
            {
                OpenGameplayPlaceholder();
                dayFlow?.BeginNewSession();
                CompleteNarrativeRestore(false);
            }
            catch (Exception exception)
            {
                OpenMainMenu();
                ShowResult("初始化失败", exception.Message, () => { });
            }
        }

        public void OpenPause()
        {
            bool tutorialPause = dayFlow != null && (dayFlow.IsFirstSessionGuideActive || dayFlow.IsNarrativeEntryActive)
                || (narrativeIntegration?.Runtime?.Controller?.BlocksSave ?? false);
            if ((!tutorialPause && DialoguePanel.IsAnyPlaying) || !gameplay || paused || loadingTransitioning
                || (!tutorialPause && dayFlow != null && dayFlow.BlocksGameplayInput)
                || (!tutorialPause && (narrativeIntegration?.Runtime?.BlocksGameplayInput ?? false))) return;
            if (IsMapOpen) CloseMap();
            if (openingPause) return;
            openingPause = true;
            StartCoroutine(OpenPauseAfterRender());
        }

        private IEnumerator OpenPauseAfterRender()
        {
            int version = sessionVersion;
            yield return new WaitForEndOfFrame();
            openingPause = false;
            if (!gameplay || version != sessionVersion) yield break;
            bool tutorialPause = dayFlow != null && (dayFlow.IsFirstSessionGuideActive || dayFlow.IsNarrativeEntryActive)
                || (narrativeIntegration?.Runtime?.Controller?.BlocksSave ?? false);
            CaptureGameplayThumbnail();
            paused = true;
            if (tutorialPause)
            {
                var dialogue = UIKit.GetPanel<DialoguePanel>();
                if (dialogue != null) dialogue.InputSuspended = true;
            }
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            PauseMenuPanel panel = UIKit.OpenPanel<PauseMenuPanel>(UILevel.Pop, Data(true), cachePolicy: PanelCachePolicy.Reusable);
            StartCoroutine(PushPanelWhenReady(panel, false));
        }

        public void ResumeGame()
        {
            if (!gameplay || !paused || UIKit.PeekPanel(MainStack) is not PauseMenuPanel panel) return;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Cancel);
            panel.CloseAnimated(FinishResume);
        }

        private void FinishResume()
        {
            if (!gameplay) return;
            paused = false;
            var dialogue = UIKit.GetPanel<DialoguePanel>();
            if (dialogue != null) dialogue.InputSuspended = false;
            Time.timeScale = previousTimeScale;
        }

        public void OpenSaves(bool fromGameplay)
        {
            if (fromGameplay && !CanSaveCurrentSession)
            {
                ShowResult("暂不可存档", "当前教学或剧情正在播放，请在结束后的安全节点存档。", () => { });
                return;
            }
            SaveManagementPanel panel = UIKit.OpenPanel<SaveManagementPanel>(UILevel.Pop, Data(fromGameplay), cachePolicy: PanelCachePolicy.Reusable);
            StartCoroutine(PushPanelWhenReady(panel, true));
        }

        public void OpenSettings(bool fromGameplay)
        {
            SettingsPanel panel = UIKit.OpenPanel<SettingsPanel>(UILevel.Pop, Data(fromGameplay), cachePolicy: PanelCachePolicy.Reusable);
            StartCoroutine(PushPanelWhenReady(panel, true));
        }

        public void BackToPrevious()
        {
            if (UIKit.GetStackDepth(MainStack) <= 1) return;
            if (UIKit.PeekPanel(MainStack) is SaveManagementPanel) pendingSaveCompletion = null;
            if (UIKit.PeekPanel(MainStack) is SaveManagementPanel || UIKit.PeekPanel(MainStack) is SettingsPanel)
            {
                if (UIKit.PeekPanel(MainStack) is GameFlowPanel panel)
                {
                    Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Cancel);
                    panel.CloseAnimated();
                }
            }
        }

        public IReadOnlyList<GameSaveSlotInfo> ListSaveSlots() => saves.List();

        public void ConfirmLoad(int index)
        {
            ShowConfirmation("读取存档", $"确认读取槽 {index + 1} 的存档？当前未保存的进度将丢失。", "确认读取", () =>
            {
                if (!saves.TryLoad(index, out GameSaveData data, out string error))
                {
                    Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Error);
                    ShowResult("读取失败", error, () => { });
                    return;
                }
                if (!PlayerSaveBridge.TryPrepareRestore(data, out PlayerState playerState, out NarrativeSnapshot snapshot, out error))
                {
                    Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Error);
                    ShowResult("读取失败", error, () => { });
                    return;
                }
                if (data.Participants == null || !data.Participants.Any(item => item != null && item.Id == PlayerCafeOwner.SaveId))
                {
                    Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Error);
                    ShowResult("读取失败", "该旧档缺少经营资金与世界快照，无法安全恢复共用余额。原档已保留，请使用其他完整存档或开始新游戏。", () => { });
                    return;
                }
                ++sessionVersion;
                selectedEnding = null;
                playtime = (float)data.ElapsedPlaytimeSeconds;
                autoSaveTimer = 0f;
                loadingSavedSession = true;
                pendingNarrativeRestore = snapshot;
                pendingPlayerRestore = playerState;
                playerSession.Dispose();
                PlayerCafeOwner.EndSession();
                narrativeIntegration?.Runtime?.Controller?.BeginValueRestore();
                SetSessionState(GameSessionState.Loading);
                GameSaveParticipantRegistry.PendingRestore = data.Participants;
                LoadThen(() =>
                {
                    try { OpenGameplayPlaceholder(); }
                    catch (Exception exception)
                    {
                        loadingSavedSession = false;
                        GameSaveParticipantRegistry.PendingRestore = null;
                        OpenMainMenu();
                        Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Error);
                        ShowResult("读取失败", exception.Message, () => { });
                    }
                }, "CoffeeShop", forceReload: true);
            }, "取消", () => { });
        }

        public void ConfirmSave(int index)
        {
            if (!CanSaveCurrentSession) return;
            GameSaveSlotInfo slot = saves.GetSlot(index);
            if (slot.IsOccupied)
            {
                ShowConfirmation("覆盖存档", $"槽 {index + 1} 已有存档，确认覆盖？原存档将被替换且无法恢复。", "确认覆盖",
                    () => SaveManual(index), "取消", () => { pendingSaveCompletion = null; });
            }
            else
            {
                SaveManual(index);
            }
        }

        public void ConfirmDelete(int index)
        {
            ShowConfirmation("删除存档", $"确认删除槽 {index + 1} 的存档？删除后无法恢复。", "确认删除", () =>
            {
                if (!saves.TryDeleteManual(index, out string error))
                {
                    Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Error);
                    ShowResult("删除失败", error, () => { });
                }
                else
                    RefreshSavePanel();
            }, "取消", () => { });
        }

        public void QuickSave()
        {
            if (!CanSaveCurrentSession)
            {
                ShowResult("暂不可存档", "当前教学或剧情正在播放，请在结束后的安全节点存档。", () => { });
                return;
            }
            BeginQuickSave(null);
        }

        private void BeginQuickSave(Action completed)
        {
            if (!gameplay || saving || !CanSaveCurrentSession) return;
            int slot = saves.FindFirstEmptyManualSlot();
            if (slot < GameSaveService.FirstManualSlot)
            {
                pendingSaveCompletion = completed;
                OpenSaves(true);
                return;
            }
            StartCoroutine(SaveRoutine(slot, completed));
        }

        public void ConfirmReturnToTitle()
        {
            if (SessionState == GameSessionState.GameOver) { ReturnFromGameOver(); return; }
            if (!CanSaveCurrentSession)
            {
                ShowConfirmation("退出主菜单", "当前教学或剧情尚未完成，本段进度不会保存。要返回主菜单吗？", "返回主菜单",
                    () => LoadThen(OpenMainMenu, config.MainMenuScene), "继续游戏", () => { });
                return;
            }
            ShowConfirmation("退出主菜单", "是否保存当前游戏进度？不保存退出将丢失当前未保存的进度。", "保存游戏并退出",
                () => SaveAndReturnToTitle(), "不保存游戏并退出",
                () => LoadThen(OpenMainMenu, config.MainMenuScene));
        }

        public void ConfirmExit()
        {
            if (!CanSaveCurrentSession)
            {
                ShowConfirmation("退出游戏", "当前教学或剧情尚未完成，本段进度不会保存。要退出游戏吗？", "退出游戏",
                    QuitGame, "继续游戏", () => { });
                return;
            }
            ShowConfirmation("退出游戏", "是否保存游戏并退出？", "保存并退出", () =>
            {
                if (!TryCaptureSaveData(out GameSaveData data, out string error))
                {
                    ShowResult("保存失败", error, () => { });
                    return;
                }
                if (saves.TryQuickSave(data, out _, out error))
                    QuitGame();
                else
                    ShowResult("保存失败", error ?? "无法保存游戏", () => { });
            }, "不保存并退出", QuitGame);
        }

        public void ConfirmQuitFromMenu() => ShowConfirmation("退出游戏", "确认退出游戏？", "确认退出", QuitGame, "取消", () => { });

        private void SaveAndReturnToTitle() => BeginQuickSave(() => LoadThen(OpenMainMenu, config.MainMenuScene));

        public void CloseModalPanel(GameFlowPanel panel, Action afterClose = null)
        {
            if (panel != null)
                panel.CloseAnimated(afterClose);
            else
                afterClose?.Invoke();
        }

        public void SaveSettings() => settings.Save();
        public string GetThumbnail(int index) => saves.TryLoad(index, out GameSaveData data, out _) ? data.ThumbnailPng : null;

        public void SetMasterVolume(float value) => settings.SetMasterVolume(value);
        public void SetMusicVolume(float value) => settings.SetMusicVolume(value);
        public void SetSfxVolume(float value) => settings.SetSfxVolume(value);
        public void SetWindowMode(FullScreenMode mode) => settings.SetWindowMode(mode);
        public void SetResolution(ResolutionOption option)
        {
            if (awaitingResolutionConfirmation)
            {
                // The pending display mode is already active. Replace its confirmation
                // instead of restoring it first, which would enqueue two transitions.
                awaitingResolutionConfirmation = false;
                UIKit.GetPanel<ConfirmationPanel>()?.DismissFromFlow();
            }
            previousResolutionWidth = settings.Current.ResolutionWidth;
            previousResolutionHeight = settings.Current.ResolutionHeight;
            settings.SetResolution(option, false);
            awaitingResolutionConfirmation = true;
            resolutionConfirmTimer = 10f;
            ShowConfirmation("确认分辨率", "保持此分辨率？将在10秒后恢复原设置。", "保持", () =>
            {
                awaitingResolutionConfirmation = false;
                settings.CommitCurrentDisplaySettings();
            }, "恢复原设置", RevertResolution);
        }

        private void RevertResolution()
        {
            if (!awaitingResolutionConfirmation) return;
            awaitingResolutionConfirmation = false;
            settings.RestoreResolution(previousResolutionWidth, previousResolutionHeight);
            SettingsPanel panel = UIKit.GetPanel<SettingsPanel>();
            panel?.RefreshDisplaySettings();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private bool CanUseGameplayControls()
            => gameplay && !paused && !saving && !openingPause && !loadingTransitioning
                && Time.timeScale > 0f && !IsMapOpen && detailsPanel == null && !DialoguePanel.IsAnyPlaying
                && !CafeMiniGameActive
                && !(narrativeIntegration?.Runtime?.Actions?.IsExecuting ?? false)
                && (dayFlow == null || !dayFlow.BlocksGameplayInput)
                && (GameplayPanelHooks.PanelType != null && GameplayPanelHooks.PanelType.IsInstanceOfType(UIKit.PeekPanel(MainStack))
                    || GameplayPanelHooks.IsScenePanel && UIKit.PeekPanel(MainStack) == null);

        private bool CanUseNarrativePresentation()
        {
            if (CanUseGameplayControls()) return true;
            bool executingAction = narrativeIntegration?.Runtime?.Actions?.IsExecuting ?? false;
            // An action keeps the map open while its result events play, including the fallback narrative UI.
            return gameplay && !paused && !loadingTransitioning && (!IsMapOpen || executingAction) && detailsPanel == null
                && (executingAction || dayFlow != null && (dayFlow.IsNarrativeEntryActive
                    || dayFlow.IsFirstSessionGuideActive && dayFlow.State == DayFlowState.FirstDayIntroduction
                    || (narrativeIntegration?.Runtime?.Controller?.BlocksSave ?? false)));
        }

        private void HandleEscape()
        {
            if (openingProloguePanel != null && openingProloguePanel.IsPlaying) return;
            if (SessionState == GameSessionState.GameOver || saving || openingPause) return;
            bool tutorialPause = dayFlow != null && (dayFlow.IsFirstSessionGuideActive || dayFlow.IsNarrativeEntryActive)
                || (narrativeIntegration?.Runtime?.Controller?.BlocksSave ?? false);
            if (detailsPanel != null || GameplayHudPanel.IsHandlingEscape || detailsClosedFrame == Time.frameCount || DialoguePanel.BlocksEscapeThisFrame && !tutorialPause) return;
            if (IsMapOpen && !paused) { CloseMap(); return; }
            if (tutorialPause && gameplay && !paused)
            {
                OpenPause();
                return;
            }
            if (CanUseGameplayControls() && (narrativeIntegration?.Runtime?.BlocksGameplayInput ?? false))
                return;
            if (loadingTransitioning || (dayFlow != null && dayFlow.BlocksGameplayInput && !tutorialPause))
                return;
            Type topType = UIKit.PeekPanel(MainStack)?.GetType();
            if (topType == typeof(ConfirmationPanel))
                UIKit.GetPanel<ConfirmationPanel>()?.CancelFromFlow();
            else if (topType == typeof(ResultPanel))
                UIKit.GetPanel<ResultPanel>()?.CloseFromFlow();
            else if (topType == typeof(SettingsPanel) || topType == typeof(SaveManagementPanel))
                BackToPrevious();
            else if (gameplay && !paused && GameplayPanelHooks.PanelType != null && topType == GameplayPanelHooks.PanelType)
                OpenPause();
            else if (gameplay && !paused && GameplayPanelHooks.IsScenePanel && topType == null)
                OpenPause();
            else if (paused && topType == typeof(PauseMenuPanel))
                ResumeGame();
        }

        public void OpenDetails()
        {
            if (!SharedHudControlsAvailable) return;
            GameplayHudPanel.Ensure().OpenDetails();
        }

        public bool TrySubmitPlayerCreation(FirstDayLetterDraft draft)
        {
            PlayerRuntime player = PlayerRuntime.Current;
            if (player?.CreationSubmitted == true) return true;
            if (player == null) return false;
            try
            {
                // Arrival runs on the map before the cafe scene exists. Bind the session
                // economy now; entering the cafe transfers it through the existing save bridge.
                if (PlayerCafeOwner.Current == null)
                {
                    var catalogAsset = Resources.Load<TextAsset>("FreeCafe/catalog");
                    if (catalogAsset == null) throw new InvalidOperationException("Missing FreeCafe/catalog for player creation.");
                    PlayerCafeOwner.Bind(new Spotlight.FreeCafe.CafeWorld(
                        JsonUtility.FromJson<Spotlight.FreeCafe.CafeCatalog>(catalogAsset.text)));
                }
                if (!playerSession.IsBoundTo(narrativeIntegration?.Runtime, PlayerCafeOwner.Current))
                    playerSession.Bind(narrativeIntegration?.Runtime, PlayerCafeOwner.Current, dayFlow);
                if (!playerSession.Submit(draft)) return false;
                CompleteNarrativeRestore(false);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Player creation failed: " + exception.Message, this);
                return false;
            }
        }

        internal void NotifyPlayerDayChanged() => playerSession.NotifyDayChanged();

        public void RefreshDetails()
        {
            if (GameplayHudPanel.Instance != null) GameplayHudPanel.Instance.RefreshDetails();
        }

        private void CloseDetails()
        {
            if (GameplayHudPanel.Instance != null) GameplayHudPanel.Instance.CloseWindows();
            detailsClosedFrame = Time.frameCount;
        }

        private void OpenMainMenu()
        {
            // Persistent NCS UI is not unloaded with the gameplay scene. Invalidate
            // DayFlow callbacks first, then release the entire composite stage.
            SetSessionState(GameSessionState.Menu);
            dayFlow?.HideForMainMenu();
            narrativeIntegration?.Runtime?.Presenter?.InterruptForHost();
            DialogueHistoryService.ClearFormal();
            NarrativeEndingEcho.Clear();
            Spotlight.Audio.GameAudio.Music(Spotlight.Audio.AudioKeys.MenuMusic);
            openingProloguePanel?.Stop();
            if (openingProloguePanel != null) UIKit.ClosePanel(openingProloguePanel);
            openingProloguePanel = null;
            ++sessionVersion;
            boundController?.SetEndingChecksEnabled(false);
            playerSession.Dispose();
            PlayerRuntime.Current = null;
            PlayerCafeOwner.EndSession();
            pendingSaveCompletion = null;
            CloseDetails();
            ForceCloseMap();
            DialoguePlayback.Stop();
            narrativeIntegration?.SetGameplayVisible(false);
            narrativeIntegration?.Runtime?.Presenter?.SetSuspended(true);
            Time.timeScale = 1f;
            paused = false;
            UIKit.ClearStack(MainStack, true);
            if (GameplayPanelHooks.IsScenePanel && GameplayPanelHooks.OpenPanel != null)
            {
                // 场景内面板在回到主菜单时隐藏
                var scenePanel = FindFirstObjectByType(GameplayPanelHooks.PanelType, FindObjectsInactive.Include) as GameFlowPanel;
                if (scenePanel != null) scenePanel.gameObject.SetActive(false);
            }
            MainMenuPanel panel = UIKit.OpenPanel<MainMenuPanel>(UILevel.Pop, Data(), cachePolicy: PanelCachePolicy.Reusable);
            StartCoroutine(PushPanelWhenReady(panel, false, ++rootNavigationVersion));
        }

        private void OpenGameplayPlaceholder()
        {
            if (!string.IsNullOrEmpty(PlayerCafeOwner.RestoreError))
                throw new InvalidOperationException("经营存档恢复失败：" + PlayerCafeOwner.RestoreError);
            bool restoreDayFlow = loadingSavedSession;
            bool importLegacyProfile = pendingNarrativeRestore?.conditionValues == null;
            if (loadingSavedSession)
            {
                DialogueHistoryService.ClearFormal();
                if (pendingNarrativeRestore != null && narrativeIntegration?.Runtime != null) narrativeIntegration.Runtime.RestoreSnapshot(pendingNarrativeRestore);
                else narrativeIntegration?.ResetSession();
                pendingNarrativeRestore = null;
                PlayerRuntime.Current = new PlayerRuntime(PlayerCreationCatalog.Load(), pendingPlayerRestore);
                pendingPlayerRestore = null;
                PlayerSaveBridge.Register(() => PlayerRuntime.Current);
                loadingSavedSession = false;
                dayFlow?.RestoreSavedData();
                // When loading into the already active scene, its Start() does not run again.
                foreach (var entry in GameSaveParticipantRegistry.Restorers.ToArray())
                {
                    if (entry.Key == DayFlowController.SaveId || entry.Key == PlayerSaveBridge.LegacyParticipantId || entry.Key == PlayerRuntime.SaveId) continue;
                    string json = GameSaveParticipantRegistry.TakePending(entry.Key);
                    if (!string.IsNullOrEmpty(json)) entry.Value(json);
                }
                BindNarrativeSession();
            }
            if (restoreDayFlow)
            {
                CompleteNarrativeRestore(importLegacyProfile);
                if (SessionState == GameSessionState.GameOver) return;
            }
            narrativeIntegration?.SetGameplayVisible(true);
            narrativeIntegration?.Runtime?.Presenter?.SetSuspended(false);
            Time.timeScale = 1f;
            previousTimeScale = 1f;
            paused = false;
            SetSessionState(GameSessionState.Playing);
            UIKit.ClearStack(MainStack, true);
            GameFlowPanel panel;
            if (GameplayPanelHooks.OpenPanel != null)
            {
                panel = GameplayPanelHooks.OpenPanel(this);
                gameplayScenePanel = panel;
            }
            else
            {
                Debug.LogError("No gameplay panel is registered for scene " + SceneManager.GetActiveScene().name + ".", this);
                return;
            }
            StartCoroutine(PushPanelWhenReady(panel, false, ++rootNavigationVersion));
            if (restoreDayFlow && gameplay) dayFlow?.ResumeSavedPhase();
        }

        private void BindNarrativeSession()
        {
            if (boundController != null) boundController.EndingSelected -= OnEndingSelected;
            boundController = narrativeIntegration?.Runtime?.Controller;
            if (boundController != null)
            {
                boundController.SetEndingChecksEnabled(false);
                boundController.BeginValueRestore();
                boundController.EndingSelected += OnEndingSelected;
            }
        }

        private void CompleteNarrativeRestore(bool importLegacyProfile)
        {
            NarrativeRuntime runtime = narrativeIntegration?.Runtime;
            if (runtime?.Controller == null) throw new InvalidOperationException("Narrative session failed to initialize.");
            if (PlayerRuntime.Current?.CreationSubmitted != true) return;
            if (PlayerCafeOwner.Current == null) return;
            if (!playerSession.IsBoundTo(runtime, PlayerCafeOwner.Current)) playerSession.Bind(runtime, PlayerCafeOwner.Current, dayFlow);
            runtime.Actions.RefreshForDay(dayFlow?.CurrentDay ?? 1);
            runtime.Controller.SetOfficialDay(dayFlow?.CurrentDay ?? 1);
            if (runtime.Controller.IsEnded)
            {
                var ending = runtime.Configuration.endings.Find(item => item.endingId == runtime.Controller.SelectedEndingId);
                OnEndingSelected(ending ?? new NarrativeEndingDefinition { endingId = runtime.Controller.SelectedEndingId, titleText = "游戏结束", messageText = runtime.Controller.SelectedEndingId });
                return;
            }
            runtime.Controller.CompleteValueRestore();
            runtime.Controller.SetEndingChecksEnabled(true);
            if (!runtime.Controller.IsEnded) runtime.ResumeRestoredSession();
        }

        private void OnEndingSelected(NarrativeEndingDefinition ending)
        {
            if (SessionState == GameSessionState.GameOver) return;
            selectedEnding = ending;
            SetSessionState(GameSessionState.GameOver);
        }

        private void EnterGameOver()
        {
            ++sessionVersion;
            ++loadingVersion;
            ++rootNavigationVersion;
            autoSaveTimer = 0f;
            pendingSaveCompletion = null;
            openingPause = false;
            paused = false;
            dayFlow?.StopForGameOver();
            CloseDetails();
            ForceCloseMap();
            DialoguePlayback.Stop();
            narrativeIntegration?.Runtime?.StopForGameOver();
            UIKit.ClearStack(MainStack, true);
            Time.timeScale = 0f;
            NarrativeRuntime runtime = narrativeIntegration?.Runtime;
            // A real accepted ending and the restored-ending path share exactly the same panel snapshot.
            // Do not show raw text keys or manufacture completion facts to produce this screen.
            NarrativeEndingEcho.Clear();
            DialoguePlayback.CaptureLastRevealedLines(1);
            var endingEcho = NarrativeEndingEcho.Text;
            var accepted = selectedEnding ?? new NarrativeEndingDefinition { endingId = runtime?.Controller?.SelectedEndingId ?? "ending.unknown" };
            var data = NarrativeEndingPresentation.Create(this, accepted,
                key => runtime?.ResolveText(key) ?? key, ReturnFromGameOver,
                artwork: NarrativeEndingEcho.ResolveArtwork(accepted.endingId), endingEcho: endingEcho);
            NarrativeEndingEcho.Clear();
            ResultPanel panel = UIKit.OpenPanel<ResultPanel>(UILevel.Pop, data, cachePolicy: PanelCachePolicy.Reusable);
            StartCoroutine(PushPanelWhenReady(panel, true, rootNavigationVersion));
        }

        public void ReturnFromFirstArrival()
        {
            if (!gameplay || dayFlow == null || !dayFlow.IsFirstSessionGuideActive) return;
            // Explicit opening IF0 belongs to the same ending pipeline as the later stories.
            // Do not skip its panel by jumping to the menu from an EventCompleted listener.
            var core = narrativeIntegration?.Runtime?.Controller;
            if (core != null && core.State.HasHappenedEvent("arrival.leave"))
            {
                // EventCompleted listeners are inside an atomic notification boundary. Wait
                // for that boundary instead of falsely reporting the completed IF0 as unready.
                core.RunAtSafeBoundary(() =>
                {
                    if (SessionState != GameSessionState.Playing) return;
                    core.SetEndingChecksEnabled(true);
                    var ending = core.CheckEndingsAtSafeBoundary();
                    if (ending != null) OnEndingSelected(ending);
                    else Debug.LogWarning("[DayFlow] Opening departure has no valid formal ending; keeping the session rather than silently skipping its result panel.", this);
                });
                return;
            }
            dayFlow.HideForMainMenu();
            Time.timeScale = 1f;
            LoadThen(OpenMainMenu, config.MainMenuScene);
        }

        private void ReturnFromGameOver()
        {
            if (SessionState != GameSessionState.GameOver) return;
            Time.timeScale = 1f;
            LoadThen(OpenMainMenu, config.MainMenuScene);
        }

        private IEnumerator PushPanelWhenReady(GameFlowPanel panel, bool hidePrevious, int navigationVersion = -1)
        {
            while (panel != null
                && (navigationVersion < 0 || navigationVersion == rootNavigationVersion)
                && panel.State == PanelState.Opening)
                yield return null;
            if (panel != null
                && (navigationVersion < 0 || navigationVersion == rootNavigationVersion)
                && (panel.State == PanelState.Open || panel.State == PanelState.Hide)
                && !UIKit.IsInStack(panel))
                UIKit.PushPanel(panel, MainStack, hidePrevious);
        }

        private void ShowConfirmation(string title, string message, string accept, Action accepted, string cancel, Action cancelled)
        {
            var data = new ConfirmationPanelData(this, title, message, accept, accepted, cancel, cancelled);
            ConfirmationPanel panel = UIKit.OpenPanel<ConfirmationPanel>(UILevel.Pop, data, cachePolicy: PanelCachePolicy.Reusable);
            StartCoroutine(PushPanelWhenReady(panel, false));
        }

        private void ShowResult(string title, string message, Action closed)
        {
            var data = new ResultPanelData(this, title, message, closed);
            ResultPanel panel = UIKit.OpenPanel<ResultPanel>(UILevel.Pop, data, cachePolicy: PanelCachePolicy.Reusable);
            StartCoroutine(PushPanelWhenReady(panel, true));
        }

        private void ShowSaveSuccess(int index, Action closed)
        {
            var data = new ResultPanelData(this, "保存成功", "已保存到槽 " + (index + 1), closed,
                "继续游戏", isSaveSuccess: true);
            ResultPanel panel = UIKit.OpenPanel<ResultPanel>(UILevel.Pop, data, cachePolicy: PanelCachePolicy.Reusable);
            StartCoroutine(PushPanelWhenReady(panel, true));
        }


        private void LoadThen(Action completed, string sceneName, bool preserveDayFlow = false, bool forceReload = false)
        {
            SetSessionState(GameSessionState.Loading);
            CloseDetails();
            ForceCloseMap();
            if (!preserveDayFlow && dayFlow != null) dayFlow.HideForMainMenu();
            int version = ++loadingVersion;
            loadingTransitioning = true;
            narrativeIntegration?.SetGameplayVisible(false);
            if (loadingRoutine != null)
                StopCoroutine(loadingRoutine);
            if (shortLoadingPanel != null)
            {
                UIKit.ClosePanel(shortLoadingPanel);
                shortLoadingPanel = null;
            }
            if (loadingPanel != null)
            {
                UIKit.ClosePanel(loadingPanel);
                loadingPanel = null;
            }
            if (preserveDayFlow)
            {
                shortLoadingPanel = UIKit.OpenPanel<ShortLoadingPanel>(UILevel.AlwayTop, Data(), tag: "game-flow-short-loading", cachePolicy: PanelCachePolicy.Reusable);
                loadingRoutine = StartCoroutine(ShortLoadRoutine(completed, sceneName, version));
                return;
            }
            loadingPanel = UIKit.OpenPanel<LoadingPanel>(UILevel.AlwayTop, Data(), tag: "game-flow-loading", cachePolicy: PanelCachePolicy.Reusable);
            tips.ResetTimer();
            loadingPanel.BeginLoading();
            loadingPanel.SetTip(tips.CurrentTip);
            loadingRoutine = StartCoroutine(LoadRoutine(completed, sceneName, version, forceReload));
        }

        private IEnumerator ShortLoadRoutine(Action completed, string sceneName, int version)
        {
            // Do not replace the scene until the diagonal dissolve fully covers it.
            while (shortLoadingPanel != null && !shortLoadingPanel.IsCovered)
                yield return null;
            yield return null;
            if (version != loadingVersion) yield break;
            int buildIndex = FindBuildIndex(sceneName);
            if (buildIndex >= 0 && buildIndex != SceneManager.GetActiveScene().buildIndex)
                yield return SceneManager.LoadSceneAsync(buildIndex);
            else if (buildIndex < 0)
                Debug.LogWarning("Configured scene is not in Build Settings: " + sceneName, this);
            if (version != loadingVersion) yield break;
            // Prepare the destination UI while it is still hidden under the opaque cover.
            SetSessionState(GameSessionState.Playing);
            completed?.Invoke();
            yield return null;
            if (version != loadingVersion) yield break;
            loadingRoutine = null;
            ShortLoadingPanel finishedPanel = shortLoadingPanel;
            Action finishTransition = () =>
            {
                if (version != loadingVersion) return;
                shortLoadingPanel = null;
                loadingTransitioning = false;
            };
            if (finishedPanel != null) finishedPanel.EndLoading(finishTransition);
            else finishTransition();
        }

        private IEnumerator LoadRoutine(Action completed, string sceneName, int version, bool forceReload = false)
        {
            // Let the panel open and render before scene loading can stall the main thread.
            while (loadingPanel != null && loadingPanel.State == PanelState.Opening)
                yield return null;
            yield return null;
            if (version != loadingVersion) yield break;
            float elapsed = 0f;
            float displayedProgress = 0f;
            int buildIndex = FindBuildIndex(sceneName);
            AsyncOperation operation = null;
            ResourceRequest resourceOperation = null;
            if (buildIndex >= 0 && (forceReload || buildIndex != SceneManager.GetActiveScene().buildIndex))
            {
                operation = SceneManager.LoadSceneAsync(buildIndex);
                operation.allowSceneActivation = false;
            }
            else if (buildIndex < 0)
            {
                Debug.LogWarning("Configured scene is not in Build Settings: " + sceneName, this);
            }
            if (operation == null)
                resourceOperation = Resources.LoadAsync<GameObject>("Art/UIPrefab/MainMenuPanel");

            while (elapsed < config.MinimumLoadingSeconds || operation != null && !operation.isDone || resourceOperation != null && !resourceOperation.isDone)
            {
                float deltaTime = Time.unscaledDeltaTime;
                elapsed += deltaTime;
                if (tips.Tick(deltaTime) && loadingPanel != null)
                    loadingPanel.SetTip(tips.CurrentTip);

                float targetProgress;
                if (operation != null)
                {
                    targetProgress = operation.isDone ? 1f : Mathf.Clamp01(operation.progress / 0.9f) * 0.9f;
                    if (operation.progress >= 0.9f && elapsed >= config.MinimumLoadingSeconds
                        && displayedProgress >= 0.9f)
                        operation.allowSceneActivation = true;
                }
                else
                {
                    targetProgress = resourceOperation != null ? resourceOperation.progress : 1f;
                }

                displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, deltaTime * 0.8f);
                if (loadingPanel != null)
                    loadingPanel.SetProgress(displayedProgress);
                yield return null;
            }

            while (displayedProgress < 1f)
            {
                displayedProgress = Mathf.MoveTowards(displayedProgress, 1f, Time.unscaledDeltaTime * 0.8f);
                if (loadingPanel != null)
                    loadingPanel.SetProgress(displayedProgress);
                yield return null;
            }

            if (version != loadingVersion)
                yield break;
            loadingRoutine = null;
            LoadingPanel finishedPanel = loadingPanel;
            loadingPanel = null;
            Action finishTransition = () =>
            {
                if (version != loadingVersion) return;
                loadingTransitioning = false;
                completed?.Invoke();
            };
            if (finishedPanel != null)
                finishedPanel.EndLoading(finishTransition);
            else
                finishTransition();
        }

        private void SaveManual(int index)
        {
            if (!CanSaveCurrentSession) return;
            if (!saving) StartCoroutine(SaveRoutine(index, pendingSaveCompletion));
        }

        private IEnumerator SaveRoutine(int index, Action completed)
        {
            int version = sessionVersion;
            if (!gameplay) yield break;
            saving = true;
            PauseMenuPanel pause = UIKit.GetPanel<PauseMenuPanel>();
            pause?.SetSaveStatus("保存中");
            yield return null;
            if (!gameplay || version != sessionVersion) { saving = false; yield break; }
            bool success = TryCaptureSaveData(out GameSaveData data, out string error)
                && saves.TrySaveManual(index, data, out error);
            Spotlight.Audio.GameAudio.Play(success ? Spotlight.Audio.AudioKeys.Save : Spotlight.Audio.AudioKeys.Error);
            saving = false;
            pause?.SetSaveStatus(success ? "保存成功" : "保存失败");
            if (!success) { ShowResult("保存失败", error, RefreshSavePanel); yield break; }
            pendingSaveCompletion = null;
            RefreshSavePanel();
            if (completed != null)
            {
                yield return new WaitForSecondsRealtime(.25f);
                if (gameplay && version == sessionVersion) completed();
            }
            else ShowSaveSuccess(index, RefreshSavePanel);
        }

        private void CaptureGameplayThumbnail()
        {
            if (gameplayThumbnail != null) Destroy(gameplayThumbnail);
            gameplayThumbnail = null;
            Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();
            if (screen == null) return;
            RenderTexture target = RenderTexture.GetTemporary(320, 180, 0);
            RenderTexture active = RenderTexture.active;
            try
            {
                Graphics.Blit(screen, target);
                RenderTexture.active = target;
                gameplayThumbnail = new Texture2D(320, 180, TextureFormat.RGB24, false);
                gameplayThumbnail.ReadPixels(new Rect(0, 0, 320, 180), 0, 0);
                gameplayThumbnail.Apply();
            }
            finally { RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); Destroy(screen); }
        }

        private void RefreshSavePanel()
        {
            SaveManagementPanel panel = UIKit.GetPanel<SaveManagementPanel>();
            if (panel != null && panel.State == PanelState.Open)
                panel.RefreshSlots();
        }

        private GameFlowPanelData Data(bool fromGameplay = false) => new GameFlowPanelData(this, fromGameplay);

        public bool TryCaptureSaveData(out GameSaveData data, out string error)
        {
            data = null;
            if (!CanSaveCurrentSession)
            { error = "当前教学或剧情正在播放，请在结束后的安全节点存档。"; return false; }
            if (!gameplay || loadingSavedSession || loadingTransitioning)
            { error = "当前会话状态不允许保存。"; return false; }
            data = new GameSaveData
            {
                SceneName = SceneManager.GetActiveScene().name,
                ElapsedPlaytimeSeconds = playtime,
                CheckpointId = "start",
                State = gameplay ? "gameplay" : "menu",
                ThumbnailPng = gameplayThumbnail != null ? Convert.ToBase64String(gameplayThumbnail.EncodeToPNG()) : null
            };
            try
            {
                if (dayFlow?.CurrentPresentation is FirstDayLetterPanel letter && PlayerRuntime.Current != null)
                    PlayerRuntime.Current.SaveDraft(letter.Draft);
                data.Participants.AddRange(GameSaveParticipantRegistry.CaptureAll());
            }
            catch (Exception exception) { data = null; error = "保存未完成：" + exception.Message; return false; }

            NarrativeRuntime runtime = narrativeIntegration?.Runtime;
                        if (runtime == null || !runtime.IsReady)
            {
                data = null;
                error = "NCS runtime is not ready; save was not written.";
                return false;
            }
            if (NarrativeSaveBridge.TryCapture(data, () => runtime.CaptureSnapshot(PlayerRuntime.Current?.CreationSubmitted == false), out error)) return true;
            data = null;
            return false;
        }

        private GameSaveData CaptureSaveData()
        {
            return TryCaptureSaveData(out GameSaveData data, out _) ? data : null;
        }

        public bool TryAutoSave(out string error)
        {
            error = null;
            if (!CanSaveCurrentSession) { error = "当前教学或会话阶段暂不允许存档。"; return false; }
            if (!gameplay) { error = "当前没有正在进行的游戏。"; return false; }
            if (saving) { error = "正在保存，请稍后重试。"; return false; }
            StartCoroutine(AutoSaveAfterRender());
            return true;
        }

        private IEnumerator AutoSaveAfterRender()
        {
            int version = sessionVersion;
            yield return new WaitForEndOfFrame();
            if (!gameplay || version != sessionVersion || paused || loadingTransitioning) yield break;
            CaptureGameplayThumbnail();
            if (!TryCaptureSaveData(out GameSaveData data, out string error) || !saves.TryAutoSave(data, out error))
                Debug.LogWarning("自动存档失败：" + error, this);
        }

        private static int FindBuildIndex(string scene)
        {
            if (string.IsNullOrWhiteSpace(scene)) return -1;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                if (string.Equals(Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)), scene, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static string FormatTime(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return string.Format("{0:00}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds);
        }
    }
}
