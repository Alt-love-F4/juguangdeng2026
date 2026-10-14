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
        [SerializeField] private GameFlowConfig config;
        [SerializeField] private bool showMainMenuOnStart = true;
        [SerializeField] private bool enableNarrative;
        private GameSettingsService settings;
        private GameSaveService saves;
        private LoadingTipSequence tips;
        private Coroutine loadingRoutine;
        private LoadingPanel loadingPanel;
        private ShortLoadingPanel shortLoadingPanel;
        private int rootNavigationVersion;
        private int loadingVersion;
        private bool loadingTransitioning;
        private FSM<GameSessionState> session;
        private int sessionVersion;
        private NarrativeController boundController;
        private NarrativeEndingDefinition selectedEnding;
        public GameSessionState SessionState => session?.CurEnum ?? GameSessionState.Menu;
        public bool CanSaveCurrentSession => dayFlow == null || dayFlow.CanSave;
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
        private DetailsPanel detailsPanel;
        private MapSceneController mapOverlay;
        private bool openingMap;
        public bool IsMapOpen => mapOverlay != null || openingMap;
        internal bool MapControlsAvailable => gameplay && !paused && !loadingTransitioning && detailsPanel == null
            && !DialoguePanel.IsAnyPlaying && (dayFlow == null || !dayFlow.BlocksGameplayInput)
            && (PlayerRuntime.Current?.CreationSubmitted == true || dayFlow != null && dayFlow.State == DayFlowState.CityMapLeisure);
        private int detailsClosedFrame = -1;
        private NarrativeRuntime detailsRuntime;
        private EventSystem persistentEventSystem;
        private bool saving;
        private Action pendingSaveCompletion;
        private Texture2D gameplayThumbnail;
        private bool openingPause;
        public bool HasReadableSave => saves.List().Any(slot => slot.CanLoad);

        public GameSettings CurrentSettings => settings.Current;
        internal bool GameplayControlsAvailable => CanUseGameplayControls();
        internal bool CanPreviewDialogue => SessionState != GameSessionState.GameOver && !paused && !loadingTransitioning
            && !(narrativeIntegration?.Runtime?.BlocksGameplayInput ?? false)
            && (!gameplay || CanUseGameplayControls());
        public IReadOnlyList<ResolutionOption> ResolutionOptions => settings.ResolutionOptions;
        public string WindowModeLabel => settings.Current.WindowMode switch
        {
            FullScreenMode.ExclusiveFullScreen => "全屏",
            FullScreenMode.Windowed => "窗口",
            _ => "无边框"
        };
        public string ResolutionLabel => settings.Current.ResolutionWidth + " x " + settings.Current.ResolutionHeight;

        private void Awake()
        {
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
                narrativeIntegration.Runtime.CharacterResolver = NarrativeCharacterResolver.Resolve;
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
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                HandleEscape();
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            {
                if (IsMapOpen) CloseMap();
                else OpenMap();
            }
            if (gameplay && !paused && !loadingTransitioning)
            {
                narrativeIntegration?.Runtime?.Controller?.FlushValueChanges();
                float deltaTime = Time.unscaledDeltaTime;
                playtime += deltaTime;
                if (!loadingSavedSession)
                {
                    autoSaveTimer += deltaTime;
                    if (config.AutoSaveIntervalSeconds > 0f && autoSaveTimer >= config.AutoSaveIntervalSeconds)
                    {
                        autoSaveTimer %= config.AutoSaveIntervalSeconds;
                        if (!TryAutoSave(out string autoSaveError))
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

        public void OpenMap()
        {
            if (dayFlow != null && dayFlow.IsFirstSessionGuideActive) return;
            if (!CanUseGameplayControls() || SceneManager.GetActiveScene().name != "CoffeeShop") return;
            openingMap = true;
            StartCoroutine(OpenMapRoutine());
        }

        private IEnumerator OpenMapRoutine()
        {
            int version = sessionVersion;
            Scene scene = SceneManager.GetSceneByName("Map");
            if (!scene.isLoaded)
                yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("Map");
            if (!gameplay || version != sessionVersion) { openingMap = false; yield break; }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    camera.gameObject.SetActive(false);
                if (mapOverlay == null) mapOverlay = root.GetComponentInChildren<MapSceneController>(true);
            }
            if (mapOverlay != null) mapOverlay.OpenOverlay(this);
            openingMap = false;
        }

        public void CloseMap()
        {
            if (openingMap || mapOverlay == null || detailsPanel != null) return;
            mapOverlay.gameObject.SetActive(false);
            mapOverlay = null;

        }

        public void TransitionToGameplayScene(string sceneName, Action completed)
        {
            if (!gameplay) return;
            GameSaveParticipantRegistry.PendingRestore = GameSaveParticipantRegistry.CaptureAll();
            if (string.IsNullOrWhiteSpace(sceneName) || sceneName == SceneManager.GetActiveScene().name)
            {
                CompletePendingPlayerCreation();
                completed?.Invoke();
                return;
            }
            LoadThen(() =>
            {
                CompletePendingPlayerCreation();
                completed?.Invoke();
            }, sceneName, true);
        }

        private void CompletePendingPlayerCreation()
        {
            if (PlayerRuntime.Current?.CreationSubmitted == true) return;
            if (!TrySubmitPlayerCreation(pendingDefaultCreation ? null : PlayerRuntime.Current?.RestoreDraft()))
                throw new InvalidOperationException("无法完成角色创建，请检查玩家与咖啡馆运行时初始化状态。");
        }

        public void StartGame()
        {
            ++sessionVersion;
            selectedEnding = null;
            GameSaveParticipantRegistry.PendingRestore = null;
            DialoguePlayback.Stop();
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
            bool tutorialPause = dayFlow != null && dayFlow.IsFirstSessionGuideActive;
            if ((!tutorialPause && DialoguePanel.IsAnyPlaying) || !gameplay || paused || loadingTransitioning
                || (!tutorialPause && dayFlow != null && dayFlow.BlocksGameplayInput)
                || (!tutorialPause && (narrativeIntegration?.Runtime?.BlocksGameplayInput ?? false))) return;
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
            bool tutorialPause = dayFlow != null && dayFlow.IsFirstSessionGuideActive;
            CaptureGameplayThumbnail();
            paused = true;
            if (tutorialPause && dayFlow.CurrentDialogue != null)
                dayFlow.CurrentDialogue.InputSuspended = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            PauseMenuPanel panel = UIKit.OpenPanel<PauseMenuPanel>(UILevel.Pop, Data(true), cachePolicy: PanelCachePolicy.Reusable);
            StartCoroutine(PushPanelWhenReady(panel, false));
        }

        public void ResumeGame()
        {
            if (!gameplay || !paused || UIKit.PeekPanel(MainStack) is not PauseMenuPanel panel) return;
            panel.CloseAnimated(FinishResume);
        }

        private void FinishResume()
        {
            if (!gameplay) return;
            paused = false;
            if (dayFlow != null && dayFlow.CurrentDialogue != null)
                dayFlow.CurrentDialogue.InputSuspended = false;
            Time.timeScale = previousTimeScale;
        }

        public void OpenSaves(bool fromGameplay)
        {
            if (fromGameplay && !CanSaveCurrentSession)
            {
                ShowResult("暂不可存档", "首日咖啡馆教学进行中，完成教学前不能存档。", () => { });
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
                    panel.CloseAnimated();
            }
        }

        public IReadOnlyList<GameSaveSlotInfo> ListSaveSlots() => saves.List();

        public void ConfirmLoad(int index)
        {
            ShowConfirmation("读取存档", $"确认读取槽 {index + 1} 的存档？当前未保存的进度将丢失。", "确认读取", () =>
            {
                if (!saves.TryLoad(index, out GameSaveData data, out string error))
                {
                    ShowResult("读取失败", error, () => { });
                    return;
                }
                if (!PlayerSaveBridge.TryPrepareRestore(data, out PlayerState playerState, out NarrativeSnapshot snapshot, out error))
                {
                    ShowResult("读取失败", error, () => { });
                    return;
                }
                if (data.Participants == null || !data.Participants.Any(item => item != null && item.Id == PlayerCafeOwner.SaveId))
                {
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
                    ShowResult("删除失败", error, () => { });
                else
                    RefreshSavePanel();
            }, "取消", () => { });
        }

        public void QuickSave()
        {
            if (!CanSaveCurrentSession)
            {
                ShowResult("暂不可存档", "首日咖啡馆教学进行中，完成教学前不能存档。", () => { });
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
                ShowConfirmation("退出主菜单", "教学尚未完成，当前进度不会保存。要返回主菜单吗？", "返回主菜单",
                    () => LoadThen(OpenMainMenu, config.MainMenuScene), "继续教学", () => { });
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
                ShowConfirmation("退出游戏", "教学尚未完成，当前进度不会保存。要退出游戏吗？", "退出游戏",
                    QuitGame, "继续教学", () => { });
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
            if (awaitingResolutionConfirmation) RevertResolution();
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
            => gameplay && !paused && !loadingTransitioning && !IsMapOpen && detailsPanel == null && !DialoguePanel.IsAnyPlaying
                && (dayFlow == null || !dayFlow.BlocksGameplayInput)
                && (GameplayPanelHooks.PanelType != null && GameplayPanelHooks.PanelType.IsInstanceOfType(UIKit.PeekPanel(MainStack))
                    || GameplayPanelHooks.IsScenePanel && UIKit.PeekPanel(MainStack) == null);

        private bool CanUseNarrativePresentation()
        {
            if (CanUseGameplayControls()) return true;
            return gameplay && !paused && !loadingTransitioning && !IsMapOpen && detailsPanel == null
                && !DialoguePanel.IsAnyPlaying && dayFlow != null && dayFlow.IsCafeTutorialActive
                && dayFlow.State == DayFlowState.FirstDayIntroduction;
        }

        private void HandleEscape()
        {
            if (SessionState == GameSessionState.GameOver || saving || openingPause) return;
            bool tutorialPause = dayFlow != null && dayFlow.IsFirstSessionGuideActive;
            if (detailsPanel != null || detailsClosedFrame == Time.frameCount || DialoguePanel.BlocksEscapeThisFrame && !tutorialPause) return;
            // The map is an additive gameplay overlay. Escape should open the pause stack over it;
            // closing the map here leaves the player without map controls after the first press.
            if (IsMapOpen && !paused) { OpenPause(); return; }
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
            if (dayFlow != null && dayFlow.IsFirstSessionGuideActive) return;
            if (!(IsMapOpen ? MapControlsAvailable : CanUseGameplayControls())) return;
            detailsPanel = UIKit.OpenPanel<DetailsPanel>(UILevel.Pop, DetailsGameDataAdapter.Create(), cachePolicy: PanelCachePolicy.Reusable);
            DetailsPanel opened = detailsPanel;
            detailsRuntime = NarrativeRuntime.Instance;
            if (detailsRuntime != null) detailsRuntime.TurnCompleted += RefreshDetails;
            if (PlayerRuntime.Current != null) PlayerRuntime.Current.Changed += RefreshDetails;
            opened.Preview(() =>
            {
                if (detailsPanel != opened) return;
                if (detailsRuntime != null) detailsRuntime.TurnCompleted -= RefreshDetails;
                if (PlayerRuntime.Current != null) PlayerRuntime.Current.Changed -= RefreshDetails;
                detailsRuntime = null;
                detailsPanel = null;
                detailsClosedFrame = Time.frameCount;
            });
            StartCoroutine(PushPanelWhenReady(opened, false));
        }

        public bool TrySubmitPlayerCreation(FirstDayLetterDraft draft)
        {
            PlayerRuntime player = PlayerRuntime.Current;
            if (player?.CreationSubmitted == true) return true;
            if (player == null) return false;
            if (PlayerCafeOwner.Current == null)
            {
                pendingDefaultCreation = draft == null;
                if (draft != null) player.SaveDraft(draft);
                return true;
            }
            try
            {
                if (!playerSession.IsBound)
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
            if (detailsPanel != null) detailsPanel.Refresh(DetailsGameDataAdapter.Create());
        }

        private void CloseDetails()
        {
            if (PlayerRuntime.Current != null) PlayerRuntime.Current.Changed -= RefreshDetails;
            if (detailsRuntime != null) detailsRuntime.TurnCompleted -= RefreshDetails;
            detailsRuntime = null;
            if (detailsPanel != null) UIKit.ClosePanel(detailsPanel);
            detailsPanel = null;
            detailsClosedFrame = Time.frameCount;
        }

        private void OpenMainMenu()
        {
            ++sessionVersion;
            boundController?.SetEndingChecksEnabled(false);
            playerSession.Dispose();
            PlayerRuntime.Current = null;
            PlayerCafeOwner.EndSession();
            pendingSaveCompletion = null;
            CloseDetails();
            CloseMap();
            DialoguePlayback.Stop();
            narrativeIntegration?.SetGameplayVisible(false);
            narrativeIntegration?.Runtime?.Presenter?.SetSuspended(true);
            Time.timeScale = 1f;
            paused = false;
            SetSessionState(GameSessionState.Menu);
            dayFlow?.HideForMainMenu();
            UIKit.ClearStack(MainStack, true);
            if (GameplayPanelHooks.IsScenePanel && GameplayPanelHooks.OpenPanel != null)
            {
                // 场景内面板在回到主菜单时隐藏
                var scenePanel = FindFirstObjectByType(GameplayPanelHooks.PanelType, FindObjectsInactive.Include) as GameFlowPanel;
                if (scenePanel != null) scenePanel.gameObject.SetActive(false);
            }
            MainMenuPanel panel = UIKit.OpenPanel<MainMenuPanel>(UILevel.Common, Data(), cachePolicy: PanelCachePolicy.Reusable);
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
            if (!playerSession.IsBound) playerSession.Bind(runtime, PlayerCafeOwner.Current, dayFlow);
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
            CloseMap();
            DialoguePlayback.Stop();
            narrativeIntegration?.Runtime?.StopForGameOver();
            UIKit.ClearStack(MainStack, true);
            Time.timeScale = 0f;
            NarrativeRuntime runtime = narrativeIntegration?.Runtime;
            var data = new ResultPanelData(this, runtime?.ResolveText(selectedEnding?.titleText) ?? "游戏结束",
                runtime?.ResolveText(selectedEnding?.messageText) ?? "", ReturnFromGameOver, "返回主菜单");
            ResultPanel panel = UIKit.OpenPanel<ResultPanel>(UILevel.Pop, data, cachePolicy: PanelCachePolicy.Reusable);
            StartCoroutine(PushPanelWhenReady(panel, true, rootNavigationVersion));
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

        private void LoadThen(Action completed, string sceneName, bool preserveDayFlow = false, bool forceReload = false)
        {
            SetSessionState(GameSessionState.Loading);
            CloseDetails();
            CloseMap();
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
            else ShowResult("保存成功", "已保存到槽 " + (index + 1), RefreshSavePanel);
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
            { error = "首日咖啡馆教学进行中，完成教学前不能存档。"; return false; }
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
            if (!CanSaveCurrentSession) return false;
            if (!gameplay || saving) return false;
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
