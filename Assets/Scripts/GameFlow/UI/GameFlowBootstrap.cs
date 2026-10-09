using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using YokiFrame;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
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
        private int rootNavigationVersion;
        private int loadingVersion;
        private bool loadingTransitioning;
        private bool gameplay;
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
        private bool loadingSavedSession;
        private DayFlowController dayFlow;
        private DetailsPanel detailsPanel;
        private MapSceneController mapOverlay;
        private bool openingMap;
        public bool IsMapOpen => mapOverlay != null || openingMap;
        internal bool MapControlsAvailable => gameplay && !paused && !loadingTransitioning && detailsPanel == null
            && !DialoguePanel.IsAnyPlaying && (dayFlow == null || !dayFlow.BlocksGameplayInput);
        private int detailsClosedFrame = -1;
        private NarrativeRuntime detailsRuntime;
        private EventSystem persistentEventSystem;

        public GameSettings CurrentSettings => settings.Current;
        internal bool GameplayControlsAvailable => CanUseGameplayControls();
        internal bool CanPreviewDialogue => !paused && !loadingTransitioning
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
            DontDestroyOnLoad(gameObject);
            persistentEventSystem = EventSystem.current;
            if (persistentEventSystem != null)
                DontDestroyOnLoad(persistentEventSystem.gameObject);
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
            if (enableNarrative)
            {
                narrativeIntegration = NarrativeGameFlowIntegration.Instance;
                if (narrativeIntegration == null)
                {
                    GameObject runtimeObject = new GameObject("NarrativeGameFlowRuntime");
                    narrativeIntegration = runtimeObject.AddComponent<NarrativeGameFlowIntegration>();
                }
                narrativeIntegration.EnsureRuntime();
            }
            dayFlow = GetComponent<DayFlowController>();
            if (dayFlow == null)
                dayFlow = gameObject.AddComponent<DayFlowController>();
            dayFlow.Initialize(this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (GetComponent<DialoguePreviewHotkey>() == null) gameObject.AddComponent<DialoguePreviewHotkey>();
#endif
            if (showMainMenuOnStart)
                OpenMainMenu();
            else
                StartGame();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            DialoguePlayback.Stop();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EventSystem[] systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (EventSystem system in systems)
            {
                if (system == persistentEventSystem) continue;
                Destroy(system.gameObject);
            }
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
                if (resolutionConfirmTimer <= 0f) RevertResolution();
            }
            if (narrativeIntegration?.GameplayHUD != null)
                narrativeIntegration.GameplayHUD.CanInteract = CanUseGameplayControls;
            narrativeIntegration?.Runtime?.Presenter?.SetSuspended(!CanUseGameplayControls());
            if (loadingPanel != null && keyboard != null)
            {
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
                    loadingPanel.SetTip(tips.Next());
                else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
                    loadingPanel.SetTip(tips.Previous());
            }
        }

        public void OpenMap()
        {
            if (!CanUseGameplayControls() || SceneManager.GetActiveScene().name != "CoffeeShop") return;
            openingMap = true;
            StartCoroutine(OpenMapRoutine());
        }

        private IEnumerator OpenMapRoutine()
        {
            Scene scene = SceneManager.GetSceneByName("Map");
            if (!scene.isLoaded)
                yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("Map");
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
            if (string.IsNullOrWhiteSpace(sceneName) || sceneName == SceneManager.GetActiveScene().name)
            {
                completed?.Invoke();
                return;
            }
            LoadThen(completed, sceneName, true);
        }

        public void StartGame()
        {
            DialoguePlayback.Stop();
            playtime = 0f;
            autoSaveTimer = 0f;
            pendingNarrativeRestore = null;
            loadingSavedSession = false;
            narrativeIntegration?.ResetSession();
            gameplay = true;
            paused = false;
            // 场景切换开关：留空则留在当前场景，直接打开玩法面板
            if (string.IsNullOrWhiteSpace(config.GameplayScene))
            {
                OpenGameplayPlaceholder();
                dayFlow?.BeginNewSession();
            }
            else
            {
                LoadThen(() =>
                {
                    OpenGameplayPlaceholder();
                    dayFlow?.BeginNewSession();
                }, config.GameplayScene);
            }
        }

        public void OpenPause()
        {
            if (DialoguePanel.IsAnyPlaying || !gameplay || paused || loadingTransitioning
                || (dayFlow != null && dayFlow.BlocksGameplayInput)
                || (narrativeIntegration?.Runtime?.BlocksGameplayInput ?? false)) return;
            paused = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            PauseMenuPanel panel = UIKit.OpenPanel<PauseMenuPanel>(UILevel.Pop, Data(true), cachePolicy: PanelCachePolicy.Reusable);
            StartCoroutine(PushPanelWhenReady(panel, true));
        }

        public void ResumeGame()
        {
            if (!paused || UIKit.PeekPanel(MainStack) is not PauseMenuPanel panel) return;
            panel.CloseAnimated(FinishResume);
        }

        private void FinishResume()
        {
            paused = false;
            Time.timeScale = previousTimeScale;
        }

        public void OpenSaves(bool fromGameplay)
        {
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
            if (UIKit.PeekPanel(MainStack) is SaveManagementPanel || UIKit.PeekPanel(MainStack) is SettingsPanel)
            {
                if (UIKit.PeekPanel(MainStack) is GameFlowPanel panel)
                    panel.CloseAnimated();
            }
        }

        public IReadOnlyList<GameSaveSlotInfo> ListSaveSlots() => saves.List();

        public void ConfirmLoad(int index)
        {
            ShowConfirmation("读取存档", $"读取存档 {index}？当前未保存的进度会丢失。", "读取", () =>
            {
                if (!saves.TryLoad(index, out GameSaveData data, out string error))
                {
                    ShowResult("读取失败", error, () => { });
                    return;
                }
                if (!NarrativeSaveBridge.TryRead(data, out NarrativeSnapshot snapshot, out error))
                {
                    ShowResult("读取失败", error, () => { });
                    return;
                }
                playtime = (float)data.ElapsedPlaytimeSeconds;
                autoSaveTimer = 0f;
                loadingSavedSession = true;
                pendingNarrativeRestore = snapshot;
                gameplay = true;
                GameSaveParticipantRegistry.PendingRestore = data.Participants;
                if (!string.IsNullOrWhiteSpace(data.SceneName)
                    && data.SceneName != SceneManager.GetActiveScene().name)
                    LoadThen(OpenGameplayPlaceholder, data.SceneName);
                else
                    OpenGameplayPlaceholder();
            }, "取消", () => { });
        }

        public void ConfirmSave(int index)
        {
            GameSaveSlotInfo slot = saves.GetSlot(index);
            if (slot.IsOccupied)
            {
                ShowConfirmation("覆盖存档", $"存档 {index} 已存在，确定覆盖？", "覆盖",
                    () => SaveManual(index), "取消", () => { });
            }
            else
            {
                SaveManual(index);
            }
        }

        public void ConfirmDelete(int index)
        {
            ShowConfirmation("删除存档", $"确定永久删除存档 {index}？", "删除", () =>
            {
                if (!saves.TryDeleteManual(index, out string error))
                    ShowResult("删除失败", error, () => { });
                else
                    RefreshSavePanel();
            }, "取消", () => { });
        }

        public void QuickSave()
        {
            if (!TryCaptureSaveData(out GameSaveData data, out string error))
            {
                ShowResult("保存失败", error, () => { });
                return;
            }
            if (saves.TryQuickSave(data, out int slot, out error))
                ShowResult("保存成功", "已保存到存档 " + slot, () => { });
            else if (error != null && error.Contains("occupied", StringComparison.OrdinalIgnoreCase))
                OpenSaves(true);
            else
                ShowResult("保存失败", error ?? "无法保存游戏", () => { });
        }

        public void ConfirmReturnToTitle()
        {
            ShowConfirmation("返回标题界面", "是否保存当前游戏进度并返回标题界面？", "保存并返回标题",
                () => SaveAndReturnToTitle(), "不保存并返回标题",
                () => LoadThen(OpenMainMenu, config.MainMenuScene));
        }

        public void ConfirmExit()
        {
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

        private void SaveAndReturnToTitle()
        {
            if (!TryCaptureSaveData(out GameSaveData data, out string error))
            {
                ShowResult("保存失败", error, () => { });
                return;
            }
            if (saves.TryQuickSave(data, out _, out error))
                LoadThen(OpenMainMenu, config.MainMenuScene);
            else
                ShowResult("保存失败", error ?? "无法保存游戏", () => { });
        }

        public void CloseModalPanel(GameFlowPanel panel, Action afterClose = null)
        {
            if (panel != null)
                panel.CloseAnimated(afterClose);
            else
                afterClose?.Invoke();
        }

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
            }, "恢复", RevertResolution);
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

        private void HandleEscape()
        {
            if (detailsPanel != null || detailsClosedFrame == Time.frameCount || DialoguePanel.BlocksEscapeThisFrame) return;
            if (IsMapOpen) { CloseMap(); return; }
            if (CanUseGameplayControls() && (narrativeIntegration?.Runtime?.BlocksGameplayInput ?? false))
                return;
            if (loadingTransitioning || (dayFlow != null && dayFlow.BlocksGameplayInput))
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
            if (!(IsMapOpen ? MapControlsAvailable : CanUseGameplayControls())) return;
            detailsPanel = UIKit.OpenPanel<DetailsPanel>(UILevel.Pop, DetailsGameDataAdapter.Create(), cachePolicy: PanelCachePolicy.Reusable);
            DetailsPanel opened = detailsPanel;
            detailsRuntime = NarrativeRuntime.Instance;
            if (detailsRuntime != null) detailsRuntime.TurnCompleted += RefreshDetails;
            opened.Preview(() =>
            {
                if (detailsPanel != opened) return;
                if (detailsRuntime != null) detailsRuntime.TurnCompleted -= RefreshDetails;
                detailsRuntime = null;
                detailsPanel = null;
                detailsClosedFrame = Time.frameCount;
            });
            StartCoroutine(PushPanelWhenReady(opened, false));
        }

        public void RefreshDetails()
        {
            if (detailsPanel != null) detailsPanel.Refresh(DetailsGameDataAdapter.Create());
        }

        private void CloseDetails()
        {
            if (detailsRuntime != null) detailsRuntime.TurnCompleted -= RefreshDetails;
            detailsRuntime = null;
            if (detailsPanel != null) UIKit.ClosePanel(detailsPanel);
            detailsPanel = null;
            detailsClosedFrame = Time.frameCount;
        }

        private void OpenMainMenu()
        {
            CloseDetails();
            CloseMap();
            DialoguePlayback.Stop();
            narrativeIntegration?.SetGameplayVisible(false);
            narrativeIntegration?.Runtime?.Presenter?.SetSuspended(true);
            Time.timeScale = 1f;
            paused = gameplay = false;
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
            bool restoreDayFlow = loadingSavedSession;
            if (loadingSavedSession)
            {
                if (pendingNarrativeRestore != null && narrativeIntegration?.Runtime != null) narrativeIntegration.Runtime.RestoreSnapshot(pendingNarrativeRestore);
                else narrativeIntegration?.ResetSession();
                pendingNarrativeRestore = null;
                loadingSavedSession = false;
            }
            narrativeIntegration?.SetGameplayVisible(true);
            narrativeIntegration?.Runtime?.Presenter?.SetSuspended(false);
            Time.timeScale = 1f;
            previousTimeScale = 1f;
            paused = false;
            gameplay = true;
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
            if (restoreDayFlow && dayFlow != null) dayFlow.RestoreSavedDayFlow();
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
            StartCoroutine(PushPanelWhenReady(panel, true));
        }

        private void ShowResult(string title, string message, Action closed)
        {
            var data = new ResultPanelData(this, title, message, closed);
            ResultPanel panel = UIKit.OpenPanel<ResultPanel>(UILevel.Pop, data, cachePolicy: PanelCachePolicy.Reusable);
            StartCoroutine(PushPanelWhenReady(panel, true));
        }

        private void LoadThen(Action completed, string sceneName, bool preserveDayFlow = false)
        {
            CloseDetails();
            CloseMap();
            if (!preserveDayFlow && dayFlow != null) dayFlow.HideForMainMenu();
            int version = ++loadingVersion;
            loadingTransitioning = true;
            narrativeIntegration?.SetGameplayVisible(false);
            if (loadingRoutine != null)
                StopCoroutine(loadingRoutine);
            if (loadingPanel != null)
                UIKit.ClosePanel(loadingPanel);
            loadingPanel = UIKit.OpenPanel<LoadingPanel>(UILevel.AlwayTop, Data(), tag: "game-flow-loading", cachePolicy: PanelCachePolicy.Reusable);
            tips.ResetTimer();
            loadingPanel.SetTip(tips.CurrentTip);
            loadingRoutine = StartCoroutine(LoadRoutine(completed, sceneName, version));
        }

        private IEnumerator LoadRoutine(Action completed, string sceneName, int version)
        {
            float elapsed = 0f;
            float displayedProgress = 0f;
            int buildIndex = FindBuildIndex(sceneName);
            AsyncOperation operation = null;
            if (buildIndex >= 0 && buildIndex != SceneManager.GetActiveScene().buildIndex)
            {
                operation = SceneManager.LoadSceneAsync(buildIndex);
                operation.allowSceneActivation = false;
            }
            else if (buildIndex < 0)
            {
                Debug.LogWarning("Configured scene is not in Build Settings: " + sceneName, this);
            }

            while (elapsed < config.MinimumLoadingSeconds || operation != null && !operation.isDone)
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
                    float progressDuration = Mathf.Max(config.MinimumLoadingSeconds, 3f);
                    targetProgress = Mathf.Min(0.9f, elapsed / progressDuration);
                    if (elapsed >= config.MinimumLoadingSeconds)
                        targetProgress = 1f;
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
                finishedPanel.CloseAnimated(finishTransition);
            else
                finishTransition();
        }

        private void SaveManual(int index)
        {
            if (!TryCaptureSaveData(out GameSaveData data, out string error))
            {
                ShowResult("保存失败", error, RefreshSavePanel);
                return;
            }
            if (!saves.TrySaveManual(index, data, out error))
                ShowResult("保存失败", error, RefreshSavePanel);
            else
                ShowResult("保存成功", "存档 " + index + " 已保存", RefreshSavePanel);
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
            data = new GameSaveData
            {
                SceneName = SceneManager.GetActiveScene().name,
                ElapsedPlaytimeSeconds = playtime,
                CheckpointId = "start",
                State = gameplay ? "gameplay" : "menu"
            };
            data.Participants.AddRange(GameSaveParticipantRegistry.CaptureAll());

            NarrativeRuntime runtime = narrativeIntegration?.Runtime;
            if (!enableNarrative)
            {
                error = null;
                return true;
            }
            if (runtime == null || !runtime.IsReady)
            {
                data = null;
                error = "NCS runtime is not ready; save was not written.";
                return false;
            }
            if (NarrativeSaveBridge.TryCapture(data, runtime.CaptureSnapshot, out error)) return true;
            data = null;
            return false;
        }

        private GameSaveData CaptureSaveData()
        {
            return TryCaptureSaveData(out GameSaveData data, out _) ? data : null;
        }

        public bool TryAutoSave(out string error)
        {
            if (!TryCaptureSaveData(out GameSaveData data, out error)) return false;
            return saves.TryAutoSave(data, out error);
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
