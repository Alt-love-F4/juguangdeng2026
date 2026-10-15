using System;
using UnityEngine;
using YokiFrame;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    public enum DayFlowState
    {
        DateTransition,
        PreBusiness,
        BusinessTransition,
        FreeCafeBusiness,
        PreLeisure,
        CityMapLeisure,
        PreNight,
        HomeNight,
        DayComplete,
        // Append only: retain numeric IDs of existing save phases.
        IntroLetter,
        FirstDayIntroduction,
        AvatarSelection
    }

    /// <summary>
    /// 日程只决定何时打开 XXPanel，以及演出完成后进入哪个阶段。
    /// 面板通过 IDayFlowPresentation 触发；UIKit 管理加载、实例和资源生命周期。
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class DayFlowController : MonoBehaviour
    {
        public const string SaveId = "game.day-flow.v1";
        private const string InitialDate = "王国纪元 670年10月5日";
        private static readonly DateTime FirstDayDate = new DateTime(670, 10, 6);
        private const string PresentationTag = "game-day-presentation";
        private const string FirstDayPerceptionCheckEventId = "EV_FIRST_DAY_PERCEPTION_CHECK";
        private const string FirstDayPerceptionSuccessEventId = "EV_FIRST_DAY_PERCEPTION_SUCCESS";
        private const string FirstDayPerceptionFailureEventId = "EV_FIRST_DAY_PERCEPTION_FAILURE";

        [Header("NCS opening content selection")]
        [SerializeField] private string openingPresentationId = NarrativePresentationDataAdapter.DefaultOpeningPresentationId;
        [SerializeField] private string firstDayLetterBodyKey = NarrativePresentationDataAdapter.DefaultFirstDayLetterBodyKey;
        [SerializeField] private string firstDayLetterSignatureKey = NarrativePresentationDataAdapter.DefaultFirstDayLetterSignatureKey;

        private IDialogueScriptSource introductionSource;
        private DialoguePanel introductionPanel;
        private PlayerAvatarSelection avatarSelection;
        public PlayerAvatarSelection CurrentAvatarSelection => avatarSelection;
        private bool firstDayIntroductionCompleted;
        private bool newSessionFirstDay;
        private bool firstDayCafeTutorialStarted;
        private bool firstDayCafeTutorialCompleted;
        private NarrativeRuntime firstDayCheckRuntime;
        private bool waitingForFirstDayPerceptionBranch;
        private GameFlowBootstrap flow;
        private UIPanel currentPanel;
        private BusinessControlsPanel businessControls;
        private MonoBehaviour endpointOwner;
        private IDayFlowGameplayEndpoint gameplayEndpoint;
        private bool initialized;
        private bool running;
        private int presentationVersion;
        private string previousDate = InitialDate;
        private int day = 1;

        public DayFlowState State { get; private set; } = DayFlowState.DateTransition;
        public int CurrentDay => day;
        private bool SessionAllowsPlay => flow == null || flow.SessionState == GameSessionState.Playing;
        public string CurrentDate => FormatDate(FirstDayDate.AddDays(day - 1));
        public bool IsFirstDayMapGuideActive => running && State == DayFlowState.CityMapLeisure && day == 1
            && !firstDayCafeTutorialStarted && !firstDayCafeTutorialCompleted;
        public bool IsCafeTutorialActive => running && day == 1 && firstDayCafeTutorialStarted && !firstDayCafeTutorialCompleted;
        public bool IsFirstSessionGuideActive => running && day == 1 && !firstDayCafeTutorialCompleted;
        public bool CanSave => !IsFirstSessionGuideActive;
        // Phase pages remain blocking while they wait for a player confirmation.
        public bool BlocksGameplayInput => running && State != DayFlowState.FreeCafeBusiness && State != DayFlowState.CityMapLeisure;
        public IDayFlowPresentation CurrentPresentation => currentPanel as IDayFlowPresentation;

        public DialoguePanel CurrentDialogue => introductionPanel;

        /// <summary>Inject an alternate source for tests or a different opening presentation.</summary>
        public void SetFirstDayIntroductionSource(IDialogueScriptSource source)
            => introductionSource = source;

        public void Initialize(GameFlowBootstrap owner)
        {
            flow = owner;
            if (initialized) return;
            initialized = true;
            PlayerSaveBridge.Register(() => PlayerRuntime.Current);
            GameSaveParticipantRegistry.Capturers[SaveId] = CaptureJson;
            GameSaveParticipantRegistry.Restorers[SaveId] = RestoreJson;
        }

        private void OnDestroy()
        {
            HideForMainMenu();
            UnsubscribeFirstDayPerceptionEvents();
            if (GameSaveParticipantRegistry.Capturers.TryGetValue(SaveId, out var capture) && capture == CaptureJson)
            {
                GameSaveParticipantRegistry.Capturers.Remove(SaveId);
                GameSaveParticipantRegistry.Restorers.Remove(SaveId);
            }
        }

        public void StopForGameOver()
        {
            if (endpointOwner != null) gameplayEndpoint?.EndDayFlowBusiness();
            HideForMainMenu();
        }

        public void HideForMainMenu()
        {
            Spotlight.Audio.GameAudio.StopOwner(this);
            running = false;
            UnsubscribeFirstArrival();
            ClosePresentation();
            HideBusinessControls();
            endpointOwner = null;
            gameplayEndpoint = null;
        }

        public void BeginNewSession(bool playLetter = true, bool playIntroduction = true)
        {
            if (!SessionAllowsPlay) return;
            HideForMainMenu();
            // The GameFlow session owns identity; reopening this flow must not reset it.
            day = 1;
            newSessionFirstDay = true;
            firstDayCafeTutorialStarted = false;
            firstDayCafeTutorialCompleted = false;
            UnsubscribeFirstDayPerceptionEvents();
            firstDayIntroductionCompleted = !playIntroduction;
            previousDate = InitialDate;
            if (!playLetter)
            {
                if (flow == null || flow.TrySubmitPlayerCreation(null)) EnterAvatarSelection();
                return;
            }
            running = true;
            HideBusinessControls();
            ShowGameplayPanel();
            SetState(DayFlowState.IntroLetter);
            ShowPresentation<FirstDayLetterPanel>(EnterAvatarSelection, ConfigureFirstDayLetter);
        }

        private void EnterAvatarSelection()
        {
            // Avatar guidance will be introduced elsewhere. Keep startup unobstructed for now.
            ClosePresentation();
            if (PlayerRuntime.Current != null)
                PlayerRuntime.Current.SelectAvatar(PlayerRuntime.Current.Catalog.DefaultAvatar);
            BeginNewDayFlow();
        }

        public void BeginNewDayFlow()
        {
            if (!SessionAllowsPlay) return;
            if (!initialized) Initialize(flow);
            running = true;
            HideBusinessControls();
            HideGameplayPanel();
            var runtime = NarrativeRuntime.Instance;
            var actions = runtime?.Actions;
            runtime?.Controller?.BeginOperation();
            try
            {
                flow?.NotifyPlayerDayChanged();
                runtime?.Controller?.SetOfficialDay(day);
                if (actions != null && actions.CurrentDay < day) actions.BeginDay(day);
            }
            finally { runtime?.Controller?.EndOperation(); }
            if (!SessionAllowsPlay) return;
            if (newSessionFirstDay && day == 1)
            {
                newSessionFirstDay = false;
                SetState(DayFlowState.DateTransition);
                ShowPresentation<DateChangePanel>(CompleteFirstArrivalDateTransition, panel => panel.SetDates(previousDate, CurrentDate));
                return;
            }
            SetState(DayFlowState.DateTransition);
            newSessionFirstDay = false;
            ShowPresentation<DateChangePanel>(CompleteDateTransition,
                panel => panel.SetDates(previousDate, CurrentDate));
        }

        private void CompleteFirstArrivalDateTransition()
        {
            NarrativeController controller = NarrativeRuntime.Instance?.Controller;
            if (controller == null) { BeginFirstArrival(); return; }
            controller.RunAtSafeBoundary(() =>
            {
                if (!SessionAllowsPlay || !running || day != 1 || State != DayFlowState.DateTransition) return;
                controller.ProcessOfficialDay(day);
                controller.RunAtSafeBoundary(() =>
                {
                    if (SessionAllowsPlay && running && day == 1 && State == DayFlowState.DateTransition) BeginFirstArrival();
                });
            });
        }

        private NarrativeController arrivalController;
        private void BeginFirstArrival() => PlayFirstArrivalEvent("arrival.start");

        private void PlayFirstArrivalEvent(string eventId)
        {
            if (!SessionAllowsPlay || !running) return;
            ClosePresentation();
            UnsubscribeFirstArrival();
            SetState(DayFlowState.FirstDayIntroduction);
            arrivalController = NarrativeRuntime.Instance?.Controller;
            if (arrivalController == null)
            {
                ShowFirstArrivalRetry(eventId);
                return;
            }
            arrivalController.EventCompleted += OnArrivalCompleted;
            arrivalController.EventFailed += OnArrivalFailed;
            if (!arrivalController.QueueEvent(eventId)) ShowFirstArrivalRetry(eventId);
        }

        private void UnsubscribeFirstArrival()
        {
            if (arrivalController == null) return;
            arrivalController.EventCompleted -= OnArrivalCompleted;
            arrivalController.EventFailed -= OnArrivalFailed;
            arrivalController = null;
        }

        private void OnArrivalFailed(string eventId, string reason)
        {
            if (!SessionAllowsPlay || !running || State != DayFlowState.FirstDayIntroduction
                || string.IsNullOrEmpty(eventId) || !eventId.StartsWith("arrival.", StringComparison.Ordinal)) return;
            ShowFirstArrivalRetry(eventId);
        }

        private void ShowFirstArrivalRetry(string eventId)
        {
            UnsubscribeFirstArrival();
            // Retry the failed event only: previous arrival choices/results have already committed.
            ShowPresentation<PreBusinessPanel>(() => PlayFirstArrivalEvent(eventId),
                panel => panel.ShowFirstArrivalUnavailable());
        }

        private void OnArrivalCompleted(string id)
        {
            if (!SessionAllowsPlay || !running || (id != "arrival.complete" && id != "arrival.leave")) return;
            UnsubscribeFirstArrival();
            if (id == "arrival.leave") flow?.ReturnFromFirstArrival();
            else EnterCityMap();
        }

        private void CompleteDateTransition()
        {
            var controller = NarrativeRuntime.Instance?.Controller;
            if (controller == null) { EnterPreBusiness(); return; }
            int version = presentationVersion;
            controller.RunAtSafeBoundary(() =>
            {
                if (!SessionAllowsPlay || !running || version != presentationVersion) return;
                controller.ProcessOfficialDay(day);
                controller.RunAtSafeBoundary(() =>
                {
                    if (SessionAllowsPlay && running && version == presentationVersion) EnterPreBusiness();
                });
            });
        }

        private void Update()
        {
            if (!SessionAllowsPlay || Time.timeScale == 0f || !running || State != DayFlowState.FreeCafeBusiness) return;
            // Do not let an endpoint's old state bypass an unfinished presentation.
            if (endpointOwner != null && gameplayEndpoint.DayFlowBusinessFinished)
                FinishBusiness();
        }

        private void EnterPreBusiness()
        {
            EnterCityMap();
        }

        private void EnterBusinessTransition()
        {
            SetState(DayFlowState.BusinessTransition);
            ShowPresentation<CafeOpeningPanel>(EnterFirstDayIntroductionOrBusiness);
        }

        private void EnterFirstDayIntroductionOrBusiness()
        {
            if (day != 1 || firstDayIntroductionCompleted || FindGameplayEndpoint() == null)
            {
                StartFreeCafeBusiness();
                return;
            }
            PlayFirstDayIntroduction();
        }

        private DialogueScript ResolveIntroductionScript()
        {
            IDialogueScriptSource source = introductionSource
                ?? new LubanOpeningDialogueSource(openingPresentationId);
            return source.CreateScript();
        }

        private void ConfigureFirstDayLetter(FirstDayLetterPanel panel)
        {
            panel.SetContent(null);
            if (PlayerRuntime.Current != null) panel.SetDraft(PlayerRuntime.Current.RestoreDraft());
            panel.DraftChanged = draft => PlayerRuntime.Current?.SaveDraft(draft);
            panel.SubmitDraft = draft => flow != null && flow.TrySubmitPlayerCreation(draft);
            panel.SubmitDefault = () => flow != null && flow.TrySubmitPlayerCreation(null);
        }

        private bool TryResolveFirstDayLetter(out FirstDayLetterContent content)
        {
            NarrativeRuntime runtime = NarrativeRuntime.Instance;
            try
            {
                bool resolved;
                string error;
                if (runtime != null && runtime.IsReady)
                    resolved = NarrativePresentationDataAdapter.TryResolveFirstDayLetter(runtime, firstDayLetterBodyKey, firstDayLetterSignatureKey, null, out content, out error);
                else
                    resolved = NarrativePresentationDataAdapter.TryResolveFirstDayLetter(LubanNarrativeLoader.Load(), firstDayLetterBodyKey, firstDayLetterSignatureKey, null, out content, out error);
                if (!resolved)
                {
                    Debug.LogWarning("[DayFlow] " + error + "; using panel draft fallback.", this);
                    content = null;
                }
                return resolved;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[DayFlow] NCS first-day letter could not be read; using panel draft fallback. " + error.Message, this);
                content = null;
                return false;
            }
        }

        private void PlayFirstDayIntroduction()
        {
            ClosePresentation();
            HideBusinessControls();
            ShowGameplayPanel();
            SetState(DayFlowState.FirstDayIntroduction);
            int version = presentationVersion;
            try
            {
                introductionPanel = DialoguePlayback.Play(ResolveIntroductionScript(), () =>
                {
                    if (!SessionAllowsPlay || !running || version != presentationVersion || State != DayFlowState.FirstDayIntroduction) return;
                    introductionPanel = null;
                    firstDayIntroductionCompleted = true;
                    if (day == 1 && firstDayCafeTutorialStarted)
                    {
                        PlayCafeTutorialContent("customer_arrives", version, () => QueueFirstDayPerceptionCheck(version));
                        return;
                    }
                    StartFreeCafeBusiness();
                }, () =>
                {
                    if (!SessionAllowsPlay || !running || version != presentationVersion || State != DayFlowState.FirstDayIntroduction) return;
                    introductionPanel = null;
                    // Cancel is not completion. The preparation page lets the player retry.
                    EnterPreBusiness();
                });
                if (introductionPanel != null && day == 1 && firstDayCafeTutorialStarted)
                    introductionPanel.AllowCancel = false;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                SetState(DayFlowState.PreBusiness);
                HideGameplayPanel();
                ShowPresentation<PreBusinessPanel>(EnterBusinessTransition,
                    panel => panel.ShowIntroductionUnavailable());
            }
        }

        private string firstDayPassiveHint;
        private void CaptureFirstDayCheck(NarrativeAbilityCheckSnapshot snapshot)
        {
            firstDayPassiveHint = PassiveCheckPresentationConfig.Hint(snapshot);
        }

        private void PlayCafeTutorialContent(string stepId, int version, Action completed)
        {
            if (!SessionAllowsPlay || !running || version != presentationVersion) return;
            try
            {
                var script = new LubanOpeningDialogueSource("cafe_tutorial." + stepId).CreateScript();
                if (stepId == "perception_success") script = script.WithFirstPageHint(firstDayPassiveHint);
                introductionPanel = DialoguePlayback.Play(script, () =>
                {
                    if (!SessionAllowsPlay || !running || version != presentationVersion) return;
                    introductionPanel = null;
                    completed();
                });
                if (introductionPanel != null) introductionPanel.AllowCancel = false;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                // Missing tutorial content must not unlock free production.
            }
        }

        private void QueueFirstDayPerceptionCheck(int version)
        {
            NarrativeRuntime runtime = NarrativeRuntime.Instance;
            if (runtime == null || !runtime.IsReady)
            {
                Debug.LogError("[DayFlow] NCS unavailable; cafe tutorial remains locked.", this);
                return;
            }

            UnsubscribeFirstDayPerceptionEvents();
            firstDayCheckRuntime = runtime;
            firstDayPassiveHint = null;
            runtime.Controller.AbilityCheckEvaluated += CaptureFirstDayCheck;
            waitingForFirstDayPerceptionBranch = true;
            runtime.Controller.EventCompleted += OnFirstDayPerceptionEventCompleted;
            runtime.Controller.EventFailed += OnFirstDayPerceptionEventFailed;
            runtime.Controller.RunAtSafeBoundary(() =>
            {
                if (!SessionAllowsPlay || !running || version != presentationVersion
                    || !waitingForFirstDayPerceptionBranch) return;
                if (runtime.QueueEvent(FirstDayPerceptionCheckEventId)) return;
                Debug.LogError("[DayFlow] Could not queue first-day perception check; cafe tutorial remains locked.", this);
                UnsubscribeFirstDayPerceptionEvents();
            });
        }

        private void OnFirstDayPerceptionEventCompleted(string eventId)
        {
            if (eventId != FirstDayPerceptionSuccessEventId && eventId != FirstDayPerceptionFailureEventId) return;
            int version = presentationVersion;
            if (!waitingForFirstDayPerceptionBranch) return;
            UnsubscribeFirstDayPerceptionEvents();
            PlayCafeTutorialContent(eventId == FirstDayPerceptionSuccessEventId ? "perception_success" : "perception_failure",
                version, () => PlayCafeTutorialContent("order_confirmed", version, StartFreeCafeBusiness));
        }

        private void OnFirstDayPerceptionEventFailed(string eventId, string reason)
        {
            if (eventId != FirstDayPerceptionCheckEventId
                && eventId != FirstDayPerceptionSuccessEventId && eventId != FirstDayPerceptionFailureEventId) return;
            Debug.LogWarning("[DayFlow] First-day perception event failed (" + eventId + "): " + reason, this);
            UnsubscribeFirstDayPerceptionEvents();
        }

        private void FinishFirstDayPerceptionFlow(int version)
        {
            if (!waitingForFirstDayPerceptionBranch || !SessionAllowsPlay || !running
                || version != presentationVersion || day != 1 || !firstDayCafeTutorialStarted) return;
            UnsubscribeFirstDayPerceptionEvents();
            PlayCafeTutorialContent("order_confirmed", version, StartFreeCafeBusiness);
        }

        private void UnsubscribeFirstDayPerceptionEvents()
        {
            if (firstDayCheckRuntime != null && firstDayCheckRuntime.Controller != null)
            {
                firstDayCheckRuntime.Controller.AbilityCheckEvaluated -= CaptureFirstDayCheck;
                firstDayCheckRuntime.Controller.EventCompleted -= OnFirstDayPerceptionEventCompleted;
                firstDayCheckRuntime.Controller.EventFailed -= OnFirstDayPerceptionEventFailed;
            }
            firstDayCheckRuntime = null;
            waitingForFirstDayPerceptionBranch = false;
        }

        private void StartFreeCafeBusiness()
        {
            if (!SessionAllowsPlay || !running) return;
            IDayFlowGameplayEndpoint endpoint = FindGameplayEndpoint();
            if (endpoint == null)
            {
                SetState(DayFlowState.PreBusiness);
                ShowPresentation<PreBusinessPanel>(EnterBusinessTransition,
                    panel => panel.ShowUnavailable());
                return;
            }
            SetState(DayFlowState.FreeCafeBusiness);
            ShowGameplayPanel();
            endpoint.StartDayFlowBusiness();
            if (!IsCafeTutorialActive) ShowBusinessControls();
        }

        public void FinishBusiness()
        {
            if (!SessionAllowsPlay || !running || State != DayFlowState.FreeCafeBusiness) return;
            IDayFlowGameplayEndpoint endpoint = FindGameplayEndpoint();
            endpoint?.EndDayFlowBusiness();
            HideBusinessControls();
            HideGameplayPanel();
            SetState(DayFlowState.PreLeisure);
            ShowPresentation<BusinessEndPanel>(EnterCityMap);
        }

        private void EnterCityMap()
        {
            if (!SessionAllowsPlay) return;
            SetState(DayFlowState.CityMapLeisure);
            ClosePresentation();
            HideBusinessControls();
            if (flow != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Map")
            {
                flow.TransitionToGameplayScene("Map", EnterCityMap);
                return;
            }
            ShowGameplayPanel();
        }

        /// <summary>Called by the map when the player selects the cafe location.</summary>
        public void StartCafeFromMap()
        {
            if (!SessionAllowsPlay) return;
            if (State == DayFlowState.CityMapLeisure && day == 1 && !firstDayCafeTutorialStarted && !firstDayCafeTutorialCompleted)
                firstDayCafeTutorialStarted = true;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "CoffeeShop") return;
            if (!running)
            {
                flow?.TransitionToGameplayScene("CoffeeShop", null);
                return;
            }
            if (State != DayFlowState.CityMapLeisure) return;
            if (flow != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "CoffeeShop")
            {
                flow.TransitionToGameplayScene("CoffeeShop", EnterBusinessTransition);
                return;
            }
            EnterBusinessTransition();
        }

        /// <summary>Called by the map's home/end-day action.</summary>
        public void CompleteFirstDayCafeTutorial()
        {
            if (!IsCafeTutorialActive) return;
            firstDayCafeTutorialCompleted = true;
            Debug.Log("[DayFlow] First-day cafe tutorial completed; free business remains active.", this);
        }

        public void EndDayFromMap()
        {
            if (IsFirstDayMapGuideActive) return;
            if (!SessionAllowsPlay || !running || State != DayFlowState.CityMapLeisure) return;
            var actions = Spotlight.Narrative.NarrativeRuntime.Instance?.Actions;
            if (actions != null && (actions.IsExecuting || actions.HasBlockingActions())) return;
            EnterPreNight();
        }

        private void EnterPreNight()
        {
            SetState(DayFlowState.PreNight);
            ShowPresentation<PreNightPanel>(EnterHomeNight);
        }

        private void EnterHomeNight()
        {
            SetState(DayFlowState.HomeNight);
            ShowPresentation<HomeNightPanel>(CompleteDay);
        }

        private void CompleteDay()
        {
            if (!SessionAllowsPlay || !running) return;
            SetState(DayFlowState.DayComplete);
            previousDate = CurrentDate;
            day++;
            if (flow != null && !flow.TryAutoSave(out string error)) Debug.LogWarning("日结自动存档失败：" + error, this);
            ShowPresentation<DayEndPanel>(BeginNewDayFlow);
        }

        private void ShowPresentation<T>(Action onFinished, Action<T> configure = null)
            where T : UIPanel, IDayFlowPresentation
        {
            ClosePresentation();
            int version = presentationVersion;
            T panel = UIKit.OpenPanel<T>(UILevel.Pop, tag: PresentationTag,
                cachePolicy: PanelCachePolicy.Reusable);
            currentPanel = panel;
            configure?.Invoke(panel);
            IDayFlowPresentation presentation = panel;
            presentation.Play(() =>
            {
                if (!SessionAllowsPlay || !running || version != presentationVersion || currentPanel != panel) return;
                currentPanel = null;
                UIKit.ClosePanel(panel);
                onFinished?.Invoke();
            });
        }

        private void ClosePresentation()
        {
            ++presentationVersion;
            PlayerAvatarSelection selection = avatarSelection;
            avatarSelection = null;
            if (selection != null)
            {
                selection.Stop();
                Destroy(selection.gameObject);
            }
            DialoguePanel dialogue = introductionPanel;
            introductionPanel = null;
            if (dialogue != null)
            {
                dialogue.Stop();
                UIKit.ClosePanel(dialogue);
            }
            UIPanel panel = currentPanel;
            currentPanel = null;
            if (panel == null) return;
            ((IDayFlowPresentation)panel).Stop();
            UIKit.ClosePanel(panel);
        }

        private void ShowBusinessControls()
        {
            HideBusinessControls();
            businessControls = UIKit.OpenPanel<BusinessControlsPanel>(UILevel.Pop,
                tag: PresentationTag, cachePolicy: PanelCachePolicy.Reusable);
            businessControls.Bind(FinishBusiness, flow);
        }

        private void HideBusinessControls()
        {
            if (businessControls == null) return;
            UIKit.ClosePanel(businessControls);
            businessControls = null;
        }

        private IDayFlowGameplayEndpoint FindGameplayEndpoint()
        {
            if (endpointOwner != null) return gameplayEndpoint;
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (!(behaviour is IDayFlowGameplayEndpoint endpoint)) continue;
                endpointOwner = behaviour;
                gameplayEndpoint = endpoint;
                return endpoint;
            }
            return null;
        }

        private void SetState(DayFlowState state)
        {
            State = state;
            ApplyAudioState();
            Debug.Log("[DayFlow] " + state);
        }

        private void ApplyAudioState()
        {
            bool city = State == DayFlowState.CityMapLeisure || State == DayFlowState.PreLeisure;
            bool night = State == DayFlowState.PreNight || State == DayFlowState.HomeNight || State == DayFlowState.DayComplete;
            Spotlight.Audio.GameAudio.Music(night ? Spotlight.Audio.AudioKeys.NightMusic : city ? Spotlight.Audio.AudioKeys.CityMusic : Spotlight.Audio.AudioKeys.CafeMusic);
            Spotlight.Audio.GameAudio.SetLoop(this, "ambience", city ? "ambience.city" : Spotlight.Audio.AudioKeys.CafeAmbience, !night);
        }

        private void HideGameplayPanel()
        {
            if (GameplayPanelHooks.PanelType == null) return;
            var panel = FindFirstObjectByType(GameplayPanelHooks.PanelType, FindObjectsInactive.Include) as GameFlowPanel;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void ShowGameplayPanel()
        {
            if (GameplayPanelHooks.PanelType == null) return;
            var panel = FindFirstObjectByType(GameplayPanelHooks.PanelType, FindObjectsInactive.Include) as GameFlowPanel;
            if (panel != null)
            {
                foreach (var child in panel.GetComponentsInChildren<GameFlowPanel>(true))
                    child.gameObject.SetActive(true);
                panel.gameObject.SetActive(true);
            }
        }

        private static string FormatDate(DateTime date)
            => "王国纪元 " + date.Year + "年" + date.Month + "月" + date.Day + "日";

        [Serializable]
        private sealed class SaveState
        {
            public int day = 1;
            public string previousDate = InitialDate;
            public DayFlowState state = DayFlowState.DateTransition;
            public bool firstDayIntroductionCompleted;
            public bool firstDayCafeTutorialStarted;
            public bool firstDayCafeTutorialCompleted;
        }

        public string CaptureJson() => JsonUtility.ToJson(new SaveState
        {
            day = day, previousDate = previousDate, state = State,
            firstDayIntroductionCompleted = firstDayIntroductionCompleted,
            firstDayCafeTutorialStarted = firstDayCafeTutorialStarted,
            firstDayCafeTutorialCompleted = firstDayCafeTutorialCompleted
        });

        public void RestoreJson(string json)
        {
            SaveState saved = JsonUtility.FromJson<SaveState>(json);
            if (saved == null) return;
            HideForMainMenu();
            day = Mathf.Max(1, saved.day);
            previousDate = string.IsNullOrEmpty(saved.previousDate) ? InitialDate : saved.previousDate;
            State = Enum.IsDefined(typeof(DayFlowState), saved.state) ? saved.state : DayFlowState.PreBusiness;
            newSessionFirstDay = false;
            firstDayCafeTutorialStarted = saved.firstDayCafeTutorialStarted;
            firstDayCafeTutorialCompleted = saved.firstDayCafeTutorialCompleted;
            // Old saves already in/after business must not replay the introduction.
            firstDayIntroductionCompleted = saved.firstDayIntroductionCompleted || day > 1
                || (State >= DayFlowState.FreeCafeBusiness && State <= DayFlowState.DayComplete);
        }

        /// <summary>Restore the saved phase, never restart the new-game intro unconditionally.</summary>
        public void RestoreSavedDayFlow()
        {
            RestoreSavedData();
            ResumeSavedPhase();
        }

        public void RestoreSavedData()
        {
            // PlayerSaveBridge validates and restores identity at the session boundary.
            string json = GameSaveParticipantRegistry.TakePending(SaveId);
            if (!string.IsNullOrEmpty(json)) RestoreJson(json);
            else
            {
                // Missing day-flow data must not inherit the previous session's date.
                day = 1;
                previousDate = InitialDate;
                firstDayIntroductionCompleted = true;
                newSessionFirstDay = false;
                firstDayCafeTutorialStarted = false;
                firstDayCafeTutorialCompleted = false;
                IDayFlowGameplayEndpoint endpoint = FindGameplayEndpoint();
                State = endpoint != null && endpoint.DayFlowBusinessActive
                    ? DayFlowState.FreeCafeBusiness : DayFlowState.PreBusiness;
            }
        }

        public void ResumeSavedPhase()
        {
            if (!SessionAllowsPlay) return;
            ApplyAudioState();
            running = true;
            HideBusinessControls();
            if (State != DayFlowState.FreeCafeBusiness) HideGameplayPanel();
            switch (State)
            {
                case DayFlowState.AvatarSelection: EnterAvatarSelection(); break;
                case DayFlowState.FirstDayIntroduction: PlayFirstDayIntroduction(); break;
                case DayFlowState.IntroLetter: ShowPresentation<FirstDayLetterPanel>(EnterAvatarSelection, ConfigureFirstDayLetter); break;
                case DayFlowState.DateTransition: BeginNewDayFlow(); break;
                case DayFlowState.PreBusiness: EnterCityMap(); break;
                case DayFlowState.BusinessTransition: EnterBusinessTransition(); break;
                case DayFlowState.FreeCafeBusiness:
                    FindGameplayEndpoint();
                    ClosePresentation();
                    ShowGameplayPanel();
                    ShowBusinessControls();
                    break;
                case DayFlowState.PreLeisure: ShowPresentation<BusinessEndPanel>(EnterCityMap); break;
                case DayFlowState.CityMapLeisure: EnterCityMap(); break;
                case DayFlowState.PreNight: EnterPreNight(); break;
                case DayFlowState.HomeNight: EnterHomeNight(); break;
                case DayFlowState.DayComplete: ShowPresentation<DayEndPanel>(BeginNewDayFlow); break;
            }
        }
    }
}
