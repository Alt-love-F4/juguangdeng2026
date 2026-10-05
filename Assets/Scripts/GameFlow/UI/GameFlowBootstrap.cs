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
        private NarrativeGameFlowIntegration narrativeIntegration;
        private NarrativeSnapshot pendingNarrativeRestore;
        private bool loadingSavedSession;

        public GameSettings CurrentSettings => settings.Current;
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
            if (EventSystem.current != null)
                DontDestroyOnLoad(EventSystem.current.gameObject);
            if (config == null)
                config = Resources.Load<GameFlowConfig>("GameFlowConfig");
            if (config == null)
            {
                Debug.LogError("GameFlowBootstrap requires GameFlowConfig.", this);
                enabled = false;
                return;
            }

            settings = new GameSettingsService();
            settings.Apply();
            saves = new GameSaveService();
            tips = new LoadingTipSequence(config.LoadingTips);
            narrativeIntegration = NarrativeGameFlowIntegration.Instance;
            if (narrativeIntegration == null)
            {
                GameObject runtimeObject = new GameObject("NarrativeGameFlowRuntime");
                narrativeIntegration = runtimeObject.AddComponent<NarrativeGameFlowIntegration>();
            }
            narrativeIntegration.EnsureRuntime();
            if (showMainMenuOnStart)
                OpenMainMenu();
            else
                OpenGameplayPlaceholder();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                HandleEscape();
            if (gameplay && !paused)
                playtime += Time.unscaledDeltaTime;
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

        public void StartGame()
        {
            playtime = 0f;
            pendingNarrativeRestore = null;
            loadingSavedSession = false;
            narrativeIntegration.ResetSession();
            gameplay = true;
            paused = false;
            // 场景切换开关：留空则留在当前场景，直接打开玩法面板
            if (string.IsNullOrWhiteSpace(config.GameplayScene))
            {
                OpenGameplayPlaceholder();
            }
            else
            {
                LoadThen(OpenGameplayPlaceholder, config.GameplayScene);
            }
        }

        public void OpenPause()
        {
            if (!gameplay || paused || (narrativeIntegration?.Runtime?.BlocksGameplayInput ?? false)) return;
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
                pendingNarrativeRestore = snapshot;
                loadingSavedSession = true;
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
        public void SetResolution(ResolutionOption option) => settings.SetResolution(option);

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private bool CanUseGameplayControls()
            => gameplay && !paused && !loadingTransitioning && UIKit.PeekPanel(MainStack) is GameplayPlaceholderPanel;

        private void HandleEscape()
        {
            if (CanUseGameplayControls() && (narrativeIntegration?.Runtime?.BlocksGameplayInput ?? false))
                return;
            if (loadingTransitioning)
                return;
            Type topType = UIKit.PeekPanel(MainStack)?.GetType();
            if (topType == typeof(ConfirmationPanel))
                UIKit.GetPanel<ConfirmationPanel>()?.CancelFromFlow();
            else if (topType == typeof(ResultPanel))
                UIKit.GetPanel<ResultPanel>()?.CloseFromFlow();
            else if (topType == typeof(SettingsPanel) || topType == typeof(SaveManagementPanel))
                BackToPrevious();
            else if (gameplay && !paused && (topType == typeof(GameplayPlaceholderPanel) || (GameplayPanelHooks.PanelType != null && topType == GameplayPanelHooks.PanelType)))
                OpenPause();
            else if (gameplay && !paused && GameplayPanelHooks.IsScenePanel && topType == null)
                OpenPause();
            else if (paused && topType == typeof(PauseMenuPanel))
                ResumeGame();
        }

        private void OpenMainMenu()
        {
            narrativeIntegration?.SetGameplayVisible(false);
            narrativeIntegration?.Runtime?.Presenter?.SetSuspended(true);
            Time.timeScale = 1f;
            paused = gameplay = false;
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
            if (loadingSavedSession)
            {
                if (pendingNarrativeRestore != null) narrativeIntegration.Runtime.RestoreSnapshot(pendingNarrativeRestore);
                else narrativeIntegration.ResetSession();
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
                GameplayPlaceholderPanel placeholder = UIKit.OpenPanel<GameplayPlaceholderPanel>(UILevel.Hud, Data(true), cachePolicy: PanelCachePolicy.Reusable);
                placeholder.Refresh(SceneManager.GetActiveScene().name, FormatTime(playtime));
                panel = placeholder;
            }
            StartCoroutine(PushPanelWhenReady(panel, false, ++rootNavigationVersion));
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

        private void LoadThen(Action completed, string sceneName)
        {
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

<<<<<<< HEAD
        private GameSaveData CaptureSaveData()
        {
            var data = new GameSaveData
=======
        public bool TryCaptureSaveData(out GameSaveData data, out string error)
        {
            data = new GameSaveData
>>>>>>> origin/main
            {
                SceneName = SceneManager.GetActiveScene().name,
                ElapsedPlaytimeSeconds = playtime,
                CheckpointId = "start",
                State = gameplay ? "gameplay" : "menu"
            };
<<<<<<< HEAD
            data.Participants.AddRange(GameSaveParticipantRegistry.CaptureAll());
            return data;
=======
            NarrativeRuntime runtime = narrativeIntegration?.Runtime;
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

        public bool TryAutoSave(out string error)
        {
            if (!TryCaptureSaveData(out GameSaveData data, out error)) return false;
            return saves.TryAutoSave(data, out error);
>>>>>>> origin/main
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
