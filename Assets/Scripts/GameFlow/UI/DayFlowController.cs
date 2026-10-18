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
        AvatarSelection,
        TutorialBusinessHandoff,
        NarrativeEntry
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
        private readonly object firstDayTutorialContinuityOwner = new object();
        private PlayerAvatarSelection avatarSelection;
        public PlayerAvatarSelection CurrentAvatarSelection => avatarSelection;
        private bool firstDayIntroductionCompleted;
        private bool newSessionFirstDay;
        private bool firstDayCafeTutorialStarted;
        private bool firstDayCafeTutorialCompleted;
        private bool tutorialTestSkipSubmitted;
        private bool awaitingNarrativeEntry;
        private int narrativeEntryVersion;
        public bool IsNarrativeEntryActive => awaitingNarrativeEntry;
        private NarrativeRuntime firstDayCheckRuntime;
        private bool waitingForFirstDayPerceptionBranch;
        private GameFlowBootstrap flow;
        private UIPanel currentPanel;
        private MonoBehaviour endpointOwner;
        private IDayFlowGameplayEndpoint gameplayEndpoint;
        private ICafeTutorialGameplayEndpoint cafeTutorialEndpoint;
        private bool initialized;
        private bool running;
        private bool businessWasActive;
        private int presentationVersion;
        private string previousDate = InitialDate;
        private int day = 1;
        private int lastCafeEntryDay;

        public DayFlowState State { get; private set; } = DayFlowState.DateTransition;
        public int CurrentDay => day;
        private bool SessionAllowsPlay => flow == null || flow.SessionState == GameSessionState.Playing;
        public string CurrentDate => FormatDate(FirstDayDate.AddDays(day - 1));
        public bool IsFirstDayMapGuideActive => running && State == DayFlowState.CityMapLeisure && day == 1
            && !firstDayCafeTutorialStarted && !firstDayCafeTutorialCompleted;
        public bool IsCafeTutorialActive => running && day == 1 && firstDayCafeTutorialStarted && !firstDayCafeTutorialCompleted;
        public bool IsFirstDayCafeMapGuideActive => running && day == 1
            && !firstDayCafeTutorialCompleted
            && (State == DayFlowState.CityMapLeisure || IsCafeTutorialActive);
        public bool IsFirstSessionGuideActive => running && day == 1 && !firstDayCafeTutorialCompleted;
        public bool CanSave => !IsFirstSessionGuideActive && !awaitingNarrativeEntry
            && !(NarrativeRuntime.Instance?.Controller?.BlocksSave ?? false);
        // Phase pages remain blocking while they wait for a player confirmation.
        public bool BlocksGameplayInput => running && State != DayFlowState.FreeCafeBusiness && State != DayFlowState.CityMapLeisure;
        public bool CanFinishBusiness => running && !IsCafeTutorialActive && State == DayFlowState.FreeCafeBusiness
            && endpointOwner != null && (gameplayEndpoint.DayFlowBusinessActive
                || businessWasActive && gameplayEndpoint.DayFlowBusinessFinished);
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
            CoffeeCupScreenTransition.Clear();
            Spotlight.Audio.GameAudio.StopOwner(this);
            running = false;
            businessWasActive = false;
            awaitingNarrativeEntry = false;
            ++narrativeEntryVersion;
            SetMapAvailability(false);
            UnsubscribeFirstArrival();
            ClosePresentation();
            endpointOwner = null;
            UnsubscribeGameplayEndpoint();
        }

        public void BeginNewSession(bool playLetter = true, bool playIntroduction = true)
        {
            if (!SessionAllowsPlay) return;
            HideForMainMenu();
            // The GameFlow session owns identity; reopening this flow must not reset it.
            day = 1;
            lastCafeEntryDay = 0;
            newSessionFirstDay = true;
            firstDayCafeTutorialStarted = false;
            firstDayCafeTutorialCompleted = false;
            tutorialTestSkipSubmitted = false;
            UnsubscribeFirstDayPerceptionEvents();
            firstDayIntroductionCompleted = !playIntroduction;
            previousDate = InitialDate;
            if (!playLetter)
            {
                if (flow == null || flow.TrySubmitPlayerCreation(null)) EnterAvatarSelection();
                return;
            }
            running = true;
            ShowGameplayPanel();
            SetState(DayFlowState.IntroLetter);
            ShowPresentation<FirstDayLetterPanel>(EnterAvatarSelection, ConfigureFirstDayLetter);
        }

        /// <summary>Explicit GM entry: reuse the cafe story/tutorial without the separate prologue or map arrival.</summary>
        internal void BeginStandaloneCafeTutorial()
        {
            day = 1;
            lastCafeEntryDay = 1;
            previousDate = InitialDate;
            newSessionFirstDay = false;
            firstDayIntroductionCompleted = false;
            firstDayCafeTutorialStarted = true;
            firstDayCafeTutorialCompleted = false;
            tutorialTestSkipSubmitted = false;
            running = true;
            FindGameplayEndpoint();
            // No ProcessOfficialDay: it would schedule the independent first-arrival story.
            PlayFirstDayIntroduction();
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
            PlayerCafeOwner.Current?.SynchronizeCalendarDay(day);
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
                ShowPresentation<DateChangePanel>(CompleteFirstArrivalDateTransition, panel =>
                {
                    panel.SetDates(previousDate, CurrentDate);
                    panel.UseCoffeeCupExit = true;
                });
                return;
            }
            SetState(DayFlowState.DateTransition);
            newSessionFirstDay = false;
            ShowPresentation<DateChangePanel>(CompleteDateTransition,
                panel =>
                {
                    panel.SetDates(previousDate, CurrentDate);
                    panel.UseCoffeeCupExit = day == 1 && !firstDayIntroductionCompleted;
                });
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
            CoffeeCupScreenTransition.Clear();
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
                    if (SessionAllowsPlay && running && version == presentationVersion)
                        RunNarrativeEntry(NarrativeEntryPoint.MorningBeforeMap, EnterPreBusiness);
                });
            });
        }

        private void Update()
        {
            if (!SessionAllowsPlay || Time.timeScale == 0f || !running || State != DayFlowState.FreeCafeBusiness) return;
            // Do not let an endpoint's old state bypass an unfinished presentation.
            if (endpointOwner == null) return;
            if (gameplayEndpoint.DayFlowBusinessActive) businessWasActive = true;
            // Night can belong to the previous visit: only settle an observed open session.
            if (businessWasActive && gameplayEndpoint.DayFlowBusinessFinished)
                FinishBusiness();
        }

        private void EnterPreBusiness()
        {
            EnterCityMap();
        }

        private void EnterBusinessTransition()
        {
            var cafe = PlayerCafeOwner.Current;
            bool alreadyClosedToday = cafe != null && cafe.State.day == day
                && cafe.State.businessPhase == Spotlight.FreeCafe.BusinessPhase.Night;
            if (lastCafeEntryDay == day || alreadyClosedToday)
            {
                // A cancelled/failed first-day introduction is not a completed handoff.
                if (day == 1 && !firstDayIntroductionCompleted) EnterFirstDayIntroductionOrBusiness();
                else ResumeCafeVisit();
                return;
            }
            ShowCafeOpening();
        }

        private void ShowCafeOpening()
        {
            lastCafeEntryDay = day;
            SetState(DayFlowState.BusinessTransition);
            ShowPresentation<CafeOpeningPanel>(EnterFirstDayIntroductionOrBusiness);
        }

        private void ResumeCafeVisit()
        {
            lastCafeEntryDay = day;
            ClosePresentation();
            businessWasActive = FindGameplayEndpoint()?.DayFlowBusinessActive == true;
            SetState(DayFlowState.FreeCafeBusiness);
            ShowGameplayPanel();
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
            ShowGameplayPanel();
            SetState(DayFlowState.FirstDayIntroduction);
            int version = presentationVersion;
            try
            {
                DialogueScript script = ResolveIntroductionScript();
                if (day == 1 && firstDayCafeTutorialStarted)
                    script = script.WithContinuity(firstDayTutorialContinuityOwner, continues: true);
                introductionPanel = DialoguePlayback.Play(script, () =>
                {
                    if (!SessionAllowsPlay || !running || version != presentationVersion || State != DayFlowState.FirstDayIntroduction) return;
                    if (!script.Continues) introductionPanel = null;
                    firstDayIntroductionCompleted = true;
                    if (day == 1 && firstDayCafeTutorialStarted)
                    {
                        cafeTutorialEndpoint?.PrepareCafeTutorial();
                        PlayCafeTutorialContent("customer_arrives", version, () =>
                        {
                            // The perception check uses its own modal presenter. Release the
                            // seamless dialogue stage before handing input to that presenter.
                            if (introductionPanel != null && introductionPanel.SeamlessChoicePending)
                            {
                                DialoguePlayback.Stop();
                                introductionPanel = null;
                            }
                            QueueFirstDayPerceptionCheck(version);
                        });
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
                ShowPresentation<PreBusinessPanel>(ShowCafeOpening,
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
                var configuration = NarrativeRuntime.Instance?.Configuration ?? LubanNarrativeLoader.Load();
                if (!configuration.TryGetCafeTutorialStep(CafeTutorialSession.TutorialId, stepId, out var step)
                    || string.IsNullOrEmpty(step.presentationId))
                    throw new InvalidOperationException("Missing cafe tutorial presentation: " + stepId);
                var script = new LubanOpeningDialogueSource(step.presentationId).CreateScript();
                if (stepId == "perception_success") script = script.WithFirstPageHint(firstDayPassiveHint);
                if (stepId == "customer_arrives" || stepId == "perception_success"
                    || stepId == "perception_failure" || stepId == "order_confirmed")
                {
                    // The ability check owns a separate modal UI; keep opening -> arrival and
                    // each check result -> order follow-up free of an extra dialogue transition.
                    bool continues = stepId == "perception_success" || stepId == "perception_failure";
                    script = script.WithContinuity(firstDayTutorialContinuityOwner, continues);
                }
                introductionPanel = DialoguePlayback.Play(script, () =>
                {
                    if (!SessionAllowsPlay || !running || version != presentationVersion) return;
                    if (!script.Continues) introductionPanel = null;
                    completed?.Invoke();
                });
                if (introductionPanel != null) introductionPanel.AllowCancel = false;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                // Missing tutorial content must not unlock free production or strand a continued panel.
                if (introductionPanel != null && introductionPanel.SeamlessChoicePending)
                {
                    DialoguePlayback.Stop();
                    introductionPanel = null;
                }
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
                version, () => PlayCafeTutorialContent("order_confirmed", version, EnterFirstDayBusinessHandoff));
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
            PlayCafeTutorialContent("order_confirmed", version, EnterFirstDayBusinessHandoff);
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

        private void EnterFirstDayBusinessHandoff()
        {
            if (FindGameplayEndpoint() is ICafeTutorialGameplayEndpoint)
            {
                StartFreeCafeBusiness();
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!SessionAllowsPlay || !running || day != 1 || tutorialTestSkipSubmitted) return;
            SetState(DayFlowState.TutorialBusinessHandoff);
            ShowPresentation<PreBusinessPanel>(StartFreeCafeBusiness,
                panel => panel.ShowTutorialHandoff(TrySkipTutorialToOctober07));
#else
            StartFreeCafeBusiness();
#endif
        }

        public bool CanSkipTutorialToOctober07
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var controller = NarrativeRuntime.Instance?.Controller;
                return SessionAllowsPlay && (flow == null || flow.CanUseTutorialTestSkip)
                    && running && day == 1 && firstDayCafeTutorialStarted
                    && !firstDayCafeTutorialCompleted && !tutorialTestSkipSubmitted
                    && introductionPanel == null && !waitingForFirstDayPerceptionBranch
                    && (State == DayFlowState.TutorialBusinessHandoff || State == DayFlowState.FreeCafeBusiness)
                    && controller != null && controller.IsSafeBoundary && !controller.IsBusy;
#else
                return false;
#endif
            }
        }

        public bool TrySkipTutorialToOctober07()
        {
            if (!CanSkipTutorialToOctober07) return false;
            tutorialTestSkipSubmitted = true;
            // Invalidate only this tutorial's callbacks and UI. Never reset NCS/player data.
            UnsubscribeFirstArrival();
            UnsubscribeFirstDayPerceptionEvents();
            ClosePresentation();
            cafeTutorialEndpoint?.FinishCafeTutorial();
            if (endpointOwner != null && gameplayEndpoint.DayFlowBusinessActive)
                gameplayEndpoint.EndDayFlowBusiness();
            endpointOwner = null;
            UnsubscribeGameplayEndpoint();
            firstDayIntroductionCompleted = firstDayCafeTutorialCompleted = true;
            previousDate = CurrentDate;
            day = 2;
            newSessionFirstDay = false;
            // BeginNewDayFlow owns calendar/AP/day-reset synchronization and date presentation.
            // This bypass is not EndDay: no settlement, reward or automatic save is performed.
            BeginNewDayFlow();
            return true;
        }

        public bool DebugSkipTutorialToCafe(out string message)
        {
            message = "请先开始游戏并进入新手流程。";
            if (!SessionAllowsPlay || !running)
                return false;
            if (flow == null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "CoffeeShop")
            {
                message = "请在 CoffeeShop 中使用此操作。";
                return false;
            }
            if (!IsFirstSessionGuideActive && !(State == DayFlowState.FreeCafeBusiness && IsCafeTutorialActive))
            {
                message = "当前不在新手教程阶段。";
                return false;
            }
            if (State == DayFlowState.FreeCafeBusiness && !IsCafeTutorialActive)
            {
                message = "当前已经在咖啡店经营环节。";
                return true;
            }

            PlayerRuntime player = PlayerRuntime.Current;
            if (player != null)
            {
                if (flow != null && !flow.TrySubmitPlayerCreation(null))
                {
                    message = "角色初始化失败，未跳过教程。";
                    return false;
                }
                if (!player.HasSelectedAvatar) player.SelectAvatar(player.Catalog.DefaultAvatar);
            }

            NarrativeRuntime narrative = NarrativeRuntime.Instance;
            if (narrative?.Controller != null)
            {
                if (!narrative.Controller.DebugCancelPendingPresentations())
                {
                    message = "当前剧情事件正在结算，请稍后重试。";
                    return false;
                }
            }
            awaitingNarrativeEntry = false;
            ++narrativeEntryVersion;
            UnsubscribeFirstArrival();
            UnsubscribeFirstDayPerceptionEvents();
            narrative?.Presenter?.DebugStopPresentation();
            ClosePresentation();
            DialoguePlayback.Stop();
            FindGameplayEndpoint();
            cafeTutorialEndpoint?.FinishCafeTutorial();
            tutorialTestSkipSubmitted = true;
            newSessionFirstDay = false;
            firstDayIntroductionCompleted = true;
            firstDayCafeTutorialStarted = true;
            firstDayCafeTutorialCompleted = true;
            waitingForFirstDayPerceptionBranch = false;

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "CoffeeShop")
            {
                flow.TransitionToGameplayScene("CoffeeShop", () =>
                {
                    if (SessionAllowsPlay && running) StartFreeCafeBusiness();
                });
            }
            else
            {
                if (flow != null && flow.IsMapOpen) flow.CloseMap();
                StartFreeCafeBusiness();
            }

            message = "已跳过所有教程，进入首杯黑咖啡交付完成后的准备阶段；可打开地图或手动开始营业，日期不变。";
            return true;
        }

        private void StartFreeCafeBusiness()
        {
            if (!SessionAllowsPlay || !running) return;
            IDayFlowGameplayEndpoint endpoint = FindGameplayEndpoint();
            if (endpoint == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (day == 1 && firstDayCafeTutorialStarted && !firstDayCafeTutorialCompleted)
                {
                    EnterFirstDayBusinessHandoff();
                    return;
                }
#endif
                SetState(DayFlowState.PreBusiness);
                ShowPresentation<PreBusinessPanel>(ShowCafeOpening,
                    panel => panel.ShowUnavailable());
                return;
            }
            lastCafeEntryDay = day;
            SetState(DayFlowState.FreeCafeBusiness);
            ShowGameplayPanel();
            endpoint.StartDayFlowBusiness();
            businessWasActive = endpoint.DayFlowBusinessActive;
            if (IsCafeTutorialActive)
            {
                PlayCafeTutorialContent(CafeTutorialSession.WorkstationStep, presentationVersion, null);
            }
        }

        public void FinishBusiness()
        {
            if (!SessionAllowsPlay || !CanFinishBusiness) return;
            IDayFlowGameplayEndpoint endpoint = FindGameplayEndpoint();
            endpoint?.EndDayFlowBusiness();
            businessWasActive = false;
            HideGameplayPanel();
            SetState(DayFlowState.PreLeisure);
            ShowPresentation<BusinessEndPanel>(() => RunNarrativeEntry(NarrativeEntryPoint.AfterBusiness, EnterCityMap));
        }

        public bool DebugSkipTutorialToMap(out string message)
        {
            message = "请先开始新游戏，并在新手教程期间使用。";
            if (flow == null || !SessionAllowsPlay || !IsFirstSessionGuideActive) return false;
            var narrative = NarrativeRuntime.Instance;
            if (narrative == null || !narrative.IsReady
                || narrative.Actions != null && narrative.Actions.IsExecuting)
            {
                message = "当前事件正在结算，请稍后重试。";
                return false;
            }
            if (!flow.TrySubmitPlayerCreation(null))
            {
                message = "角色初始化失败，未跳过教程。";
                return false;
            }
            if (!narrative.Controller.DebugCancelPendingPresentations())
            {
                message = "当前事件正在结算，请稍后重试。";
                return false;
            }
            HideForMainMenu();
            UnsubscribeFirstDayPerceptionEvents();
            narrative.Presenter?.DebugStopPresentation();
            DialoguePlayback.Stop();
            newSessionFirstDay = false;
            firstDayIntroductionCompleted = false;
            firstDayCafeTutorialStarted = false;
            firstDayCafeTutorialCompleted = false;
            running = true;
            EnterCityMap();
            message = "已跳到新手教程首次地图阶段，请进入咖啡馆继续教程；未自动保存。";
            return true;
        }

        private void EnterCityMap()
        {
            if (!SessionAllowsPlay) return;
            SetState(DayFlowState.CityMapLeisure);
            ClosePresentation();
            if (flow != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Map")
            {
                flow.TransitionToGameplayScene("Map", EnterCityMap);
                return;
            }
            ShowGameplayPanel();
        }

        /// <summary>A non-business visit: reach the bed without starting business or ending the day.</summary>
        public bool TryVisitCafeForRest(Action arrived, Action failed = null)
        {
            var runtime = NarrativeRuntime.Instance;
            if (!SessionAllowsPlay || !running || flow == null || IsFirstSessionGuideActive || awaitingNarrativeEntry
                || (runtime?.Controller?.IsBusy ?? false) || (runtime?.Actions?.IsExecuting ?? false)
                || (runtime?.Actions?.HasBlockingActions() ?? false)
                || PlayerCafeOwner.Current?.State.open == true
                || (State != DayFlowState.CityMapLeisure && State != DayFlowState.FreeCafeBusiness)) return false;
            flow.TransitionToGameplayScene("CoffeeShop", () =>
            {
                ResumeCafeVisit();
                arrived?.Invoke();
            }, failed);
            return true;
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
            cafeTutorialEndpoint?.FinishCafeTutorial();
            ShowGameplayPanel();
            Debug.Log("[DayFlow] First-day cafe tutorial completed; free business remains active.", this);
        }

        public void EndDayFromMap()
        {
            if (IsFirstDayCafeMapGuideActive) return;
            if (!SessionAllowsPlay || !running || State != DayFlowState.CityMapLeisure) return;
            var actions = Spotlight.Narrative.NarrativeRuntime.Instance?.Actions;
            if (awaitingNarrativeEntry || (NarrativeRuntime.Instance?.Controller?.IsBusy ?? false)) return;
            if (actions != null && actions.IsExecuting) return;
            if (actions != null && actions.HasBlockingActions())
            {
                var map = FindFirstObjectByType<MapSceneController>();
                map?.FocusFirstBlockingAction();
                map?.ShowBlockingActionHint();
                return;
            }
            RunNarrativeEntry(NarrativeEntryPoint.BeforeNight, CompleteDay);
        }

        /// <summary>
        /// Temporary direct day-end shortcut from the third-floor dormitory.
        /// Mandatory-story checks will be added here when the night gate is finalized.
        /// </summary>
        public void EndDayFromDormitory()
        {
            if (!SessionAllowsPlay || !running || awaitingNarrativeEntry) return;
            if (NarrativeRuntime.Instance?.Controller?.IsBusy ?? false) return;
            var actions = NarrativeRuntime.Instance?.Actions;
            if (actions != null && actions.IsExecuting) return;
            if (State != DayFlowState.FreeCafeBusiness && State != DayFlowState.CityMapLeisure
                && State != DayFlowState.PreLeisure) return;

            if (State == DayFlowState.FreeCafeBusiness)
            {
                if (IsCafeTutorialActive)
                {
                    firstDayCafeTutorialStarted = true;
                    firstDayCafeTutorialCompleted = true;
                    cafeTutorialEndpoint?.FinishCafeTutorial();
                }
                FindGameplayEndpoint()?.EndDayFlowBusiness();
            }
            HideGameplayPanel();
            // Temporary dormitory shortcut: deliberately skips the current mandatory-story gate.
            // Add the future night eligibility check here before calling CompleteDay.
            CompleteDay();
        }

        private void RunNarrativeEntry(NarrativeEntryPoint entry, Action next)
        {
            if (!SessionAllowsPlay || !running || awaitingNarrativeEntry) return;
            ClosePresentation();
            var controller = NarrativeRuntime.Instance?.Controller;
            if (controller == null) { next(); return; }
            awaitingNarrativeEntry = true;
            int token = ++narrativeEntryVersion;
            SetState(DayFlowState.NarrativeEntry);
            controller.RunAtSafeBoundary(() =>
            {
                if (!SessionAllowsPlay || !running || token != narrativeEntryVersion) return;
                if (!controller.ProcessEntry(entry, success =>
                {
                    if (!SessionAllowsPlay || !running || token != narrativeEntryVersion) return;
                    awaitingNarrativeEntry = false;
                    if (success) next();
                    else RetryNarrativeEntry(entry, next);
                }))
                {
                    awaitingNarrativeEntry = false;
                    RetryNarrativeEntry(entry, next);
                }
            });
        }

        private void RetryNarrativeEntry(NarrativeEntryPoint entry, Action next)
        {
            if (entry == NarrativeEntryPoint.BeforeNight)
            {
                EnterCityMap();
                FindFirstObjectByType<MapSceneController>()?.ShowBlockingActionHint("剧情已中断，请再次结束当天重试。");
                return;
            }
            SetState(DayFlowState.PreBusiness);
            ShowPresentation<PreBusinessPanel>(() => RunNarrativeEntry(entry, next),
                panel => panel.ShowNarrativeEntryUnavailable());
        }

        private void SetMapAvailability(bool available)
        {
            var runtime = NarrativeRuntime.Instance;
            var state = runtime?.Controller?.State;
            if (state != null && state.TryGetCondition("world.map.active", out bool old) && old != available)
                state.SetCondition("world.map.active", available);
            runtime?.Actions?.RefreshMapContent();
        }

        /// <summary>
        /// Completes the current day and immediately starts the night-to-morning calendar transition.
        /// The old leisure confirmation panels are intentionally no longer part of gameplay.
        /// </summary>
        private void CompleteDay()
        {
            if (!SessionAllowsPlay || !running) return;
            SetState(DayFlowState.DateTransition);
            FindGameplayEndpoint()?.EndDayFlowBusiness();
            previousDate = CurrentDate;
            day++;
            PlayerCafeOwner.Current?.SynchronizeCalendarDay(day);
            if (flow != null && !flow.TryAutoSave(out string error)) Debug.LogWarning("日结自动存档失败：" + error, this);
            BeginNewDayFlow();
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

        private IDayFlowGameplayEndpoint FindGameplayEndpoint()
        {
            if (endpointOwner != null) return gameplayEndpoint;
            UnsubscribeGameplayEndpoint();
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (!(behaviour is IDayFlowGameplayEndpoint endpoint)) continue;
                endpointOwner = behaviour;
                gameplayEndpoint = endpoint;
                cafeTutorialEndpoint = behaviour as ICafeTutorialGameplayEndpoint;
                if (cafeTutorialEndpoint != null)
                    cafeTutorialEndpoint.CafeTutorialStepCompleted += OnCafeTutorialStepCompleted;
                return endpoint;
            }
            return null;
        }

        private void UnsubscribeGameplayEndpoint()
        {
            if (cafeTutorialEndpoint != null)
                cafeTutorialEndpoint.CafeTutorialStepCompleted -= OnCafeTutorialStepCompleted;
            cafeTutorialEndpoint = null;
            gameplayEndpoint = null;
        }

        private void OnCafeTutorialStepCompleted(string tutorialId, string stepId)
        {
            if (!IsCafeTutorialActive || tutorialId != CafeTutorialSession.TutorialId) return;
            int version = presentationVersion;
            if (stepId == CafeTutorialSession.WorkstationSignal)
            {
                PlayCafeTutorialContent(CafeTutorialSession.CoffeeStep, version, () =>
                {
                    if (cafeTutorialEndpoint == null || !cafeTutorialEndpoint.BeginCafeTutorialCoffee())
                        Debug.LogError("[DayFlow] 无法启动黑咖啡工作台教程，教程保持锁定。", this);
                });
            }
            else if (stepId == CafeTutorialSession.PostCoffeeSignal)
            {
                CompleteFirstDayCafeTutorial();
            }
        }

        private void SetState(DayFlowState state)
        {
            State = state;
            SetMapAvailability(running && state == DayFlowState.CityMapLeisure);
            ApplyAudioState();
            Debug.Log("[DayFlow] " + state);
        }

        private void ApplyAudioState()
        {
            bool city = State == DayFlowState.CityMapLeisure || State == DayFlowState.PreLeisure;
            bool night = State == DayFlowState.DateTransition || State == DayFlowState.PreNight
                || State == DayFlowState.HomeNight || State == DayFlowState.DayComplete;
            Spotlight.Audio.GameAudio.Music(night ? Spotlight.Audio.AudioKeys.NightMusic : city ? Spotlight.Audio.AudioKeys.CityMusic : Spotlight.Audio.AudioKeys.CafeMusic);
            Spotlight.Audio.GameAudio.SetLoop(this, "ambience", city ? "ambience.city" : Spotlight.Audio.AudioKeys.CafeAmbience, !night);
        }

        private void HideBusinessControls()
        {
            var panel = FindFirstObjectByType<BusinessControlsPanel>(FindObjectsInactive.Include);
            if (panel != null) panel.gameObject.SetActive(false);
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
            public int lastCafeEntryDay;
        }

        public string CaptureJson() => JsonUtility.ToJson(new SaveState
        {
            day = day, previousDate = previousDate, state = State,
            firstDayIntroductionCompleted = firstDayIntroductionCompleted,
            firstDayCafeTutorialStarted = firstDayCafeTutorialStarted,
            firstDayCafeTutorialCompleted = firstDayCafeTutorialCompleted,
            lastCafeEntryDay = lastCafeEntryDay
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
            lastCafeEntryDay = Mathf.Clamp(saved.lastCafeEntryDay, 0, day);
            // Legacy saves in/after business have already entered the cafe that day.
            if (State == DayFlowState.FreeCafeBusiness || State == DayFlowState.PreLeisure
                || day == 1 && firstDayCafeTutorialCompleted)
                lastCafeEntryDay = day;
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
                lastCafeEntryDay = 0;
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
            if (State != DayFlowState.FreeCafeBusiness) HideGameplayPanel();
            switch (State)
            {
                case DayFlowState.NarrativeEntry: EnterCityMap(); break;
                case DayFlowState.TutorialBusinessHandoff: EnterFirstDayBusinessHandoff(); break;
                case DayFlowState.AvatarSelection: EnterAvatarSelection(); break;
                case DayFlowState.FirstDayIntroduction: PlayFirstDayIntroduction(); break;
                case DayFlowState.IntroLetter: ShowPresentation<FirstDayLetterPanel>(EnterAvatarSelection, ConfigureFirstDayLetter); break;
                case DayFlowState.DateTransition: BeginNewDayFlow(); break;
                case DayFlowState.PreBusiness: EnterCityMap(); break;
                // Resume an unconfirmed opening page even though this visit is already recorded.
                case DayFlowState.BusinessTransition: ShowCafeOpening(); break;
                case DayFlowState.FreeCafeBusiness:
                    businessWasActive = FindGameplayEndpoint()?.DayFlowBusinessActive == true;
                    ClosePresentation();
                    ShowGameplayPanel();
                    break;
                case DayFlowState.PreLeisure: ShowPresentation<BusinessEndPanel>(() => RunNarrativeEntry(NarrativeEntryPoint.AfterBusiness, EnterCityMap)); break;
                case DayFlowState.CityMapLeisure: EnterCityMap(); break;
                // Legacy saves may still contain the removed leisure phases. Finish them
                // immediately and continue with the current night-to-calendar flow.
                case DayFlowState.PreNight:
                case DayFlowState.HomeNight: CompleteDay(); break;
                case DayFlowState.DayComplete: BeginNewDayFlow(); break;
            }
        }
    }
}
