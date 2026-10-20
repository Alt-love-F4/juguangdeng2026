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
        private const string DetentionActiveCondition = "church.detention.active";

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
        private bool mapUsageTutorialPending;
        private bool mapUsageTutorialCompleted;
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
        private Action pendingMapAction;
        private Spotlight.FreeCafe.CafeWorld businessWorld;
        private bool businessWorldSubscribed;
        private int presentationVersion;
        private string previousDate = InitialDate;
        private int day = 1;
        private int lastCafeEntryDay;
        private int morningCompletedDay;
        private int detentionAdvanceDay;
        private bool detentionMapSuppressed;
        private string detentionAdvanceError;
        public bool IsDetained
        {
            get
            {
                var controller = NarrativeRuntime.Instance?.Controller;
                return controller != null && controller.State.TryGetCondition(DetentionActiveCondition, out bool active) && active;
            }
        }
        private NarrativePresenter cafeGameplayPresenter;
        private Func<NarrativeEventRequest, NarrativeGameplayPresentationData, Action<NarrativeGameplayResult>, IDisposable> cafeGameplayPlayback;
        private NarrativeCafeGameplayBridgeSession cafeGameplaySession;
        public bool IsNarrativeCafeGameplayActive => cafeGameplaySession != null && cafeGameplaySession.IsReady;

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
        public bool ShouldShowMapUsageTutorial => IsFirstDayMapGuideActive && mapUsageTutorialPending
            && !mapUsageTutorialCompleted && !BlocksGameplayInput;
        public bool CanSave => !IsFirstSessionGuideActive && !awaitingNarrativeEntry && cafeGameplaySession == null
            && deferredNarrativeEntry == null
            // An open business must be owned by the business phase, or loading would restore it on the map.
            && !(PlayerCafeOwner.Current?.State.open == true && State != DayFlowState.FreeCafeBusiness)
            && !(NarrativeRuntime.Instance?.Controller?.BlocksSave ?? false);
        // Phase pages remain blocking while they wait for a player confirmation.
        public bool BlocksGameplayInput => running && (IsDetained || (cafeGameplaySession != null
            ? !IsNarrativeCafeGameplayActive
            : State != DayFlowState.FreeCafeBusiness && State != DayFlowState.CityMapLeisure));
        public bool CanFinishBusiness => running && !IsDetained && !IsCafeTutorialActive && cafeGameplaySession == null
            && !(NarrativeRuntime.Instance?.Controller?.IsBusy ?? false) && State == DayFlowState.FreeCafeBusiness
            && endpointOwner != null && (gameplayEndpoint.DayFlowBusinessActive
                || businessWasActive && gameplayEndpoint.DayFlowBusinessFinished);
        public string BusinessClosingBlockReason
        {
            get
            {
                if (!running || !SessionAllowsPlay) return "游戏日程尚未就绪，请稍后再试。";
                if (IsDetained) return "当前剧情限制营业操作，请先完成剧情。";
                if (IsCafeTutorialActive) return "请先完成咖啡馆新手教程。";
                if (cafeGameplaySession != null) return cafeGameplaySession.GameplayHint ?? "请先完成当前剧情要求的咖啡制作与交付。";
                if (NarrativeRuntime.Instance?.Controller?.IsBusy == true) return "当前剧情尚未结束，请先完成对话或事件后再打烊。";
                if (State != DayFlowState.FreeCafeBusiness)
                {
                    // An open cafe is always settleable: FinishBusiness adopts it, never a dead end.
                    if (State == DayFlowState.CityMapLeisure && CafeSceneActive
                        && FindGameplayEndpoint()?.DayFlowBusinessActive == true)
                        return CafeSceneAdoptionBlocker();
                    return "营业流程正在切换，请稍候再试。";
                }
                if (FindGameplayEndpoint() == null) return "咖啡馆营业系统尚未就绪，请稍后再试。";
                if (!CanFinishBusiness) return "当前没有可结算的营业。";
                return null;
            }
        }

        /// <summary>
        /// Opening uses the same phase as closing, so a business can only start where it can also be
        /// settled. Every reason is transient: a cafe reached by a story page is adopted by Update.
        /// </summary>
        public string BusinessOpeningBlockReason
        {
            get
            {
                if (!running || !SessionAllowsPlay) return "游戏日程尚未就绪，请稍后再试。";
                if (IsDetained) return "当前剧情限制营业操作，请先完成剧情。";
                // The first-day tutorial keeps its own step gate inside the cafe runtime.
                if (IsCafeTutorialActive || State == DayFlowState.FreeCafeBusiness) return null;
                if (State == DayFlowState.CityMapLeisure && CafeSceneActive)
                {
                    // A story's cafe gameplay may run beside ordinary business; adopted afterwards.
                    if (IsNarrativeCafeGameplayActive) return null;
                    return CafeSceneAdoptionBlocker() ?? "正在进入咖啡馆营业流程，请稍候。";
                }
                if (cafeGameplaySession != null)
                    return cafeGameplaySession.GameplayHint ?? "请先完成当前剧情要求的咖啡制作与交付，再开始营业。";
                return "当前日程阶段不能开始营业，请先完成当前剧情或演出。";
            }
        }

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
            EnsureNarrativeCafeGameplayBinding();
        }

        private void OnEnable()
        {
            if (initialized && running) EnsureNarrativeCafeGameplayBinding();
            EnsureBusinessWorldBinding();
        }

        private void OnDisable()
        {
            pendingMapAction = null;
            ReleaseBusinessWorldBinding();
            ReleaseNarrativeCafeGameplayBinding();
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
            pendingMapAction = null;
            ReleaseBusinessWorldBinding();
            businessWorld = null;
            ReleaseNarrativeCafeGameplayBinding();
            CoffeeCupScreenTransition.Clear();
            Spotlight.Audio.GameAudio.StopOwner(this);
            running = false;
            morningCompletedDay = 0;
            detentionAdvanceDay = 0;
            detentionMapSuppressed = false;
            detentionAdvanceError = null;
            businessWasActive = false;
            awaitingNarrativeEntry = false;
            deferredNarrativeEntry = null;
            failedEntryCount = 0;
            ++narrativeEntryVersion;
            SetMapAvailability(false);
            UnsubscribeFirstArrival();
            ClosePresentation();
            endpointOwner = null;
            UnsubscribeGameplayEndpoint();
        }

        /// <summary>Explicit assisted startup: no prologue, creation letter, arrival or tutorials; no fabricated story results.</summary>
        public void BeginFastTestSession()
        {
            if (!SessionAllowsPlay) throw new InvalidOperationException("快速测试需要已就绪的游戏会话。");
            HideForMainMenu();
            day = 1;
            lastCafeEntryDay = 0;
            previousDate = InitialDate;
            newSessionFirstDay = false;
            firstDayIntroductionCompleted = true;
            firstDayCafeTutorialStarted = firstDayCafeTutorialCompleted = true;
            mapUsageTutorialPending = false;
            mapUsageTutorialCompleted = true;
            tutorialTestSkipSubmitted = true;
            if (flow == null || !flow.TrySubmitPlayerCreation(null))
                throw new InvalidOperationException("快速测试的默认角色初始化失败。");
            PlayerRuntime.Current?.SelectAvatar(PlayerRuntime.Current.Catalog.DefaultAvatar);
            running = true;
            EnsureNarrativeCafeGameplayBinding();
            PlayerCafeOwner.Current?.SynchronizeCalendarDay(day);
            var controller = NarrativeRuntime.Instance?.Controller;
            controller?.SetOfficialDay(day);
            if (controller == null) { EnterCityMap(); return; }
            controller.RunAtSafeBoundary(() => {
                if (!SessionAllowsPlay || !running) return;
                controller.ProcessOfficialDay(day);
                controller.RunAtSafeBoundary(() => {
                    if (SessionAllowsPlay && running)
                        RunNarrativeEntry(NarrativeEntryPoint.MorningBeforeMap, EnterCityMap);
                });
            });
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
            mapUsageTutorialPending = false;
            mapUsageTutorialCompleted = false;
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
            mapUsageTutorialPending = false;
            mapUsageTutorialCompleted = false;
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
            pendingMapAction = null;
            if (!initialized) Initialize(flow);
            running = true;
            EnsureNarrativeCafeGameplayBinding();
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
                    panel.UseCoffeeCupEntrance = CoffeeCupScreenTransition.IsPending;
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
            if (initialized && running) EnsureNarrativeCafeGameplayBinding();
            EnsureBusinessWorldBinding();
            ResumeDeferredNarrativeEntry();
            if (SessionAllowsPlay && running)
            {
                if (IsDetained)
                {
                    if (!detentionMapSuppressed)
                    {
                        detentionMapSuppressed = true;
                        SetMapAvailability(false);
                        if (flow != null && flow.IsMapOpen) flow.CloseMapForDayFlow();
                    }
                    // A capture result may commit from a MapAction root, outside ProcessEntry.
                    // Observe it only after that entire root/action has returned to a safe boundary.
                    if (TryCompleteDetainedDay()) return;
                }
                else { detentionMapSuppressed = false; detentionAdvanceError = null; }
            }
            var gameplay = cafeGameplaySession;
            if (gameplay != null && gameplay.IsReady && !ReferenceEquals(gameplay.World, PlayerCafeOwner.Current))
            {
                Debug.LogWarning("[DayFlow] Narrative cafe world was replaced; cancelling pending service.", this);
                gameplay.Cancel();
            }
            // Cheap scene check first: endpoint lookup only happens inside CoffeeShop.
            if (State == DayFlowState.CityMapLeisure && CafeSceneActive) TryAdoptCafeScene(false);
            if (!SessionAllowsPlay || Time.timeScale == 0f || !running || State != DayFlowState.FreeCafeBusiness) return;
            // Do not let an endpoint's old state bypass an unfinished presentation.
            if (endpointOwner == null) FindGameplayEndpoint();
            if (endpointOwner == null) return;
            if (gameplayEndpoint.DayFlowBusinessActive) businessWasActive = true;
            // Night can belong to the previous visit: only settle an observed open session.
            if (businessWasActive && gameplayEndpoint.DayFlowBusinessFinished)
                FinishBusiness();
        }

        private void EnterPreBusiness()
        {
            if (IsDetained) { TryCompleteDetainedDay(); return; }
            EnterCityMap();
        }

        private bool CanCompleteDetainedDay()
        {
            var runtime = NarrativeRuntime.Instance;
            var controller = runtime != null ? runtime.Controller : null;
            return SessionAllowsPlay && running && Time.timeScale > 0f && !PresentationPauseRegistry.IsPaused
                && IsDetained && morningCompletedDay == day
                && detentionAdvanceDay != day && !awaitingNarrativeEntry && cafeGameplaySession == null
                && controller != null && !controller.IsEnded && controller.IsSafeBoundary && !controller.IsBusy
                && !(runtime.Actions?.IsExecuting ?? false)
                && State != DayFlowState.DateTransition && currentPanel == null;
        }

        private bool TryCompleteDetainedDay()
        {
            if (!CanCompleteDetainedDay()) return false;
            SetMapAvailability(false);
            if (!TryEndBoundCafeBusiness(out string error))
            {
                // Do not skip the calendar if an actually open world could not be closed by its
                // real endpoint. Retry only after the scene owner/endpoint is correctly available.
                if (detentionAdvanceError != error)
                {
                    detentionAdvanceError = error;
                    Debug.LogWarning("[DayFlow] Detention day held: " + error, this);
                }
                return false;
            }
            detentionAdvanceError = null;
            businessWasActive = false;
            detentionAdvanceDay = day;
            HideGameplayPanel();
            // Exactly the existing one-day transition. No AddCalendar, StartDay, service orders,
            // money grants or manual loop; next morning MUST run NCS before inspecting the fact.
            CompleteDay();
            return true;
        }

        private bool TryEndBoundCafeBusiness(out string error)
        {
            error = null;
            var world = PlayerCafeOwner.Current;
            if (world == null) { error = "No authoritative cafe world is bound."; return false; }
            if (!world.State.open) return true; // Already closed is a legitimate idempotent success.
            if (!PlayerCafeOwner.HasSceneOwner || FindGameplayEndpoint() == null)
            { error = "Open cafe has no live scene-owned gameplay endpoint."; return false; }
            gameplayEndpoint.EndDayFlowBusiness();
            if (!ReferenceEquals(PlayerCafeOwner.Current, world) || world.State.open)
            { error = "Cafe endpoint did not close the same authoritative world."; return false; }
            businessWasActive = false;
            return true;
        }

        private void EnterBusinessTransition()
        {
            if (IsDetained) { TryCompleteDetainedDay(); return; }
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
            FindGameplayEndpoint();
            SetState(DayFlowState.FreeCafeBusiness);
            ShowGameplayPanel();
        }

        private static bool CafeSceneActive
            => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "CoffeeShop";

        /// <summary>
        /// Why a CoffeeShop scene reached outside DayFlow (NCS scene.cafe pages, narrative gameplay,
        /// HUD/GM transitions) cannot be adopted yet. Every reason is transient; null means adoptable.
        /// </summary>
        private string CafeSceneAdoptionBlocker()
        {
            if (!SessionAllowsPlay || !running) return "游戏日程尚未就绪，请稍后再试。";
            if (State != DayFlowState.CityMapLeisure || !CafeSceneActive) return "当前不在可进入咖啡馆的阶段。";
            if (IsDetained) return "当前剧情限制营业操作，请先完成剧情。";
            if (IsFirstSessionGuideActive) return "请先完成咖啡馆新手教程。";
            if (awaitingNarrativeEntry || cafeGameplaySession != null)
                return "当前剧情尚未结束，请先完成对话或事件。";
            var runtime = NarrativeRuntime.Instance;
            if ((runtime?.Controller?.IsBusy ?? false) || (runtime?.Actions?.IsExecuting ?? false)
                || DialoguePanel.IsAnyPlaying)
                return "当前剧情尚未结束，请先完成对话或事件。";
            if (currentPanel != null) return "请先完成当前演出。";
            if (flow != null && flow.IsMapOpen) return "请先关闭地图返回咖啡馆。";
            if (Time.timeScale == 0f || PresentationPauseRegistry.IsPaused) return "游戏已暂停。";
            if (FindGameplayEndpoint() == null) return "咖啡馆营业系统尚未就绪，请稍后再试。";
            return null;
        }

        /// <summary>
        /// Bring DayFlow back in line with the scene the player is actually in. A cafe entered by a
        /// story page runs the normal entry (opening page / resume) once the story has finished; a
        /// business already opened there is taken over as the current session so it can be settled.
        /// </summary>
        private bool TryAdoptCafeScene(bool openBusinessOnly)
        {
            if (State != DayFlowState.CityMapLeisure || !CafeSceneActive) return false;
            // Remember a business opened while adoption waits (e.g. beside story cafe gameplay):
            // if its timer ends first, it must still reach the settlement page.
            if (endpointOwner != null && gameplayEndpoint.DayFlowBusinessActive) businessWasActive = true;
            if (CafeSceneAdoptionBlocker() != null) return false;
            bool open = gameplayEndpoint.DayFlowBusinessActive;
            bool unsettled = open || businessWasActive && gameplayEndpoint.DayFlowBusinessFinished;
            if (openBusinessOnly && !unsettled) return false;
            Debug.Log("[DayFlow] CoffeeShop was reached outside DayFlow; adopting it as the cafe visit (business open="
                + open + ", unsettled=" + unsettled + ").", this);
            if (!unsettled)
            {
                EnterBusinessTransition();
                return true;
            }
            // Take over the existing session; Update/FinishBusiness then settle it normally.
            lastCafeEntryDay = day;
            ClosePresentation();
            SetState(DayFlowState.FreeCafeBusiness);
            ShowGameplayPanel();
            businessWasActive = true;
            return true;
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
            panel.UseCoffeeCupExit = true;
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
            mapUsageTutorialPending = false;
            mapUsageTutorialCompleted = true;
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
                if (flow != null && flow.IsMapOpen) flow.CloseMapForDayFlow();
                StartFreeCafeBusiness();
            }

            message = "已跳过所有教程，进入首杯黑咖啡交付完成后的准备阶段；可打开地图或手动开始营业，日期不变。";
            return true;
        }

        private void StartFreeCafeBusiness()
        {
            if (!SessionAllowsPlay || !running || IsDetained) return;
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
            businessWasActive |= endpoint.DayFlowBusinessActive;
            if (IsCafeTutorialActive)
            {
                PlayCafeTutorialContent(CafeTutorialSession.WorkstationStep, presentationVersion, null);
            }
        }

        public void FinishBusiness() => FinishBusiness(null);

        /// <summary>Continue a requested map action only after settlement and AfterBusiness finish.</summary>
        public bool TryFinishBusinessForMapAction(Action onMapReady)
            => onMapReady != null && FinishBusiness(onMapReady);

        private bool FinishBusiness(Action onMapReady)
        {
            TryAdoptCafeScene(openBusinessOnly: true);
            IDayFlowGameplayEndpoint endpoint = FindGameplayEndpoint();
            if (!SessionAllowsPlay || endpoint == null || !CanFinishBusiness) return false;
            endpoint.EndDayFlowBusiness();
            // A refusal or a replaced/destroyed endpoint must not consume this session.
            if (endpointOwner == null || !ReferenceEquals(endpoint, gameplayEndpoint)
                || !ReferenceEquals(businessWorld, PlayerCafeOwner.Current) || endpoint.DayFlowBusinessActive) return false;
            // Keep this transient continuation out of saves, and never retain a refused request.
            pendingMapAction = onMapReady;
            businessWasActive = false;
            flow?.CloseMapForDayFlow();
            HideGameplayPanel();
            ShowBusinessEndThen(EnterCityMap);
            return true;
        }

        private void ShowBusinessEndThen(Action next)
        {
            SetState(DayFlowState.PreLeisure);
            ShowPresentation<BusinessEndPanel>(() => RunNarrativeEntry(NarrativeEntryPoint.AfterBusiness, next));
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
            if (!SessionAllowsPlay || !running) { pendingMapAction = null; return; }
            if (IsDetained)
            {
                pendingMapAction = null;
                SetMapAvailability(false);
                TryCompleteDetainedDay();
                return;
            }
            SetState(DayFlowState.CityMapLeisure);
            if (IsFirstDayMapGuideActive && !mapUsageTutorialCompleted) mapUsageTutorialPending = true;
            ClosePresentation();
            if (flow != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Map")
            {
                // Preserve the continuation through the asynchronous recursive arrival callback.
                flow.TransitionToGameplayScene("Map", EnterCityMap, () => pendingMapAction = null);
                return;
            }
            ShowGameplayPanel();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Map") return;
            Action onMapReady = pendingMapAction;
            pendingMapAction = null;
            if (SessionAllowsPlay && running) onMapReady?.Invoke();
        }

        /// <summary>A non-business visit: reach the bed without starting business or ending the day.</summary>
        public bool TryVisitCafeForRest(Action arrived, Action failed = null)
        {
            var runtime = NarrativeRuntime.Instance;
            if (!SessionAllowsPlay || !running || flow == null || IsDetained || IsFirstSessionGuideActive || awaitingNarrativeEntry
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
            if (!SessionAllowsPlay || IsDetained) return;
            if (State == DayFlowState.CityMapLeisure && day == 1 && !firstDayCafeTutorialStarted && !firstDayCafeTutorialCompleted)
            {
                // Entering the cafe also dismisses/skips the map tour; never replay it after closing.
                CompleteMapUsageTutorial();
                firstDayCafeTutorialStarted = true;
            }
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

        public void CompleteMapUsageTutorial()
        {
            if (!ShouldShowMapUsageTutorial) return;
            mapUsageTutorialPending = false;
            mapUsageTutorialCompleted = true;
        }

        public void EndDayFromMap()
        {
            // Compatibility entry for old bindings. Only the real cafe bed may end a day.
            if (State != DayFlowState.CityMapLeisure || !SessionAllowsPlay || IsDetained) return;
            FindFirstObjectByType<MapSceneController>()?.ShowBlockingActionHint("请回咖啡馆三楼宿舍，点击床休息并结束这一天。");
        }

        /// <summary>The bed shares map gates; an active cafe first settles AfterBusiness.</summary>
        public void EndDayFromDormitory()
        {
            if (State != DayFlowState.FreeCafeBusiness && State != DayFlowState.CityMapLeisure
                && State != DayFlowState.PreLeisure)
            {
                if (running && Application.isPlaying) OperationToastPanel.ShowBlocked("请先完成当前剧情或演出，再休息。");
                return;
            }
            // A visible settlement page must be confirmed, not replaced by a second bed click.
            if (State == DayFlowState.PreLeisure && currentPanel != null) return;
            if (!CanRequestDayEnd()) return;
            // An open business must be settled before night, whatever phase DayFlow believed it was in.
            if (State == DayFlowState.CityMapLeisure && PlayerCafeOwner.Current?.State.open == true
                && !TryAdoptCafeScene(openBusinessOnly: true))
            {
                OperationToastPanel.ShowMessage(CafeSceneAdoptionBlocker() ?? "请先结束营业，再休息。");
                return;
            }
            if (State == DayFlowState.CityMapLeisure)
            {
                RunBeforeNightAndCompleteDay();
                return;
            }
            if (State == DayFlowState.FreeCafeBusiness)
            {
                var endpoint = FindGameplayEndpoint();
                bool needsBusinessSettlement = businessWasActive || endpoint?.DayFlowBusinessActive == true;
                if (!needsBusinessSettlement)
                {
                    // A preparation/rest visit (or a return after settled business) is not another
                    // business session. Preserve its existing UI and do not replay AfterBusiness.
                    RunBeforeNightAndCompleteDay();
                    return;
                }
                // Closing an actual business always confirms settlement and returns to Map.
                // Rest can be requested again on a subsequent non-business visit.
                FinishBusiness();
                return;
            }
            RunNarrativeEntry(NarrativeEntryPoint.AfterBusiness, RunBeforeNightAndCompleteDay);
        }

        private bool CanRequestDayEnd()
        {
            if (!SessionAllowsPlay || !running || IsDetained) return false;
            var actions = NarrativeRuntime.Instance?.Actions;
            // Every refusal tells the player why; a silent bed reads as a broken game.
            string busy = IsFirstSessionGuideActive ? "请先按照新手教程完成当前步骤，再休息。"
                : cafeGameplaySession != null ? cafeGameplaySession.GameplayHint ?? "请先完成当前剧情要求的咖啡制作与交付，再休息。"
                : awaitingNarrativeEntry || (NarrativeRuntime.Instance?.Controller?.IsBusy ?? false)
                    || actions != null && actions.IsExecuting ? "当前剧情或行动尚未结束，请完成后再休息。" : null;
            if (busy != null)
            {
                if (Application.isPlaying) OperationToastPanel.ShowBlocked(busy);
                return false;
            }
            if (actions != null && actions.HasBlockingActions())
            {
                // The Map scene stays loaded (hidden) while the player is in the cafe, so a found
                // MapSceneController is not proof the player can see its hint. Use it only when the
                // map is actually on screen; the cafe bed always gets the toast.
                bool mapVisible = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Map"
                    || flow != null && flow.IsMapOpen;
                var map = mapVisible ? FindFirstObjectByType<MapSceneController>() : null;
                map?.FocusFirstBlockingAction();
                string message = BlockingActionsMessage(NarrativeRuntime.Instance);
                if (map != null) map.ShowBlockingActionHint(message);
                else if (Application.isPlaying) OperationToastPanel.ShowMessage(message);
                return false;
            }
            return true;
        }

        /// <summary>Names the mandatory map actions that keep the night locked, so the player knows where to go.</summary>
        private static string BlockingActionsMessage(NarrativeRuntime runtime)
        {
            var names = new System.Collections.Generic.List<string>();
            var actions = runtime?.Actions;
            if (actions != null)
                foreach (var item in GameplayTodoProjection.Build(runtime))
                    if (item != null && item.important && !item.completed && !string.IsNullOrWhiteSpace(item.name)
                        && !names.Contains(item.name)) names.Add(item.name);
            return names.Count == 0
                ? "还有必须完成的事项，请先打开地图或待办事项完成后再休息。"
                : "还有必须完成的事项：" + string.Join("、", names) + "\n请先在地图上完成，再回来休息。";
        }

        private void RunBeforeNightAndCompleteDay()
        {
            // AfterBusiness may unlock new mandatory actions. Recheck before the night boundary.
            if (!CanRequestDayEnd())
            {
                if (SessionAllowsPlay && running && !awaitingNarrativeEntry
                    && !(NarrativeRuntime.Instance?.Controller?.IsBusy ?? false)) EnterCityMap();
                return;
            }
            // Resting without business would silently skip today's AfterBusiness stories, and most of the
            // later main line depends on them. Warn first; if the player still rests, play them before night.
            if (afterBusinessDoneDay != day
                && (NarrativeRuntime.Instance?.Controller?.PendingDateEvents(NarrativeEntryPoint.AfterBusiness).Count ?? 0) > 0)
            {
                PromptMissedAfterBusiness();
                return;
            }
            if (ShouldSkipFailedEntry(NarrativeEntryPoint.BeforeNight))
            {
                SkipFailedNarrativeEntry(NarrativeEntryPoint.BeforeNight, CompleteDay);
                return;
            }
            RunNarrativeEntry(NarrativeEntryPoint.BeforeNight, CompleteDay);
        }

        private void RunNarrativeEntry(NarrativeEntryPoint entry, Action next)
        {
            if (!SessionAllowsPlay || !running || awaitingNarrativeEntry) return;
            ClosePresentation();
            EnsureNarrativeCafeGameplayBinding();
            var controller = NarrativeRuntime.Instance?.Controller;
            if (controller == null) { next(); return; }
            awaitingNarrativeEntry = true;
            int token = ++narrativeEntryVersion;
            SetState(DayFlowState.NarrativeEntry);
            controller.RunAtSafeBoundary(() =>
            {
                if (!running || token != narrativeEntryVersion) return;
                // Paused/loading is not cancellation: resume this same entry once play continues,
                // otherwise awaitingNarrativeEntry would gate input, saving and sleep forever.
                if (!SessionAllowsPlay)
                {
                    DeferNarrativeEntry(token, () => { awaitingNarrativeEntry = false; RunNarrativeEntry(entry, next); });
                    return;
                }
                if (!controller.ProcessEntry(entry, success =>
                {
                    if (!running || token != narrativeEntryVersion) return;
                    if (!SessionAllowsPlay)
                    {
                        // The root already committed; only its DayFlow continuation waits.
                        DeferNarrativeEntry(token, () => CompleteNarrativeEntry(token, entry, next, success));
                        return;
                    }
                    CompleteNarrativeEntry(token, entry, next, success);
                }))
                {
                    awaitingNarrativeEntry = false;
                    RetryNarrativeEntry(entry, next);
                }
            });
        }

        private int deferredNarrativeEntryToken;
        private Action deferredNarrativeEntry;

        private void DeferNarrativeEntry(int token, Action resume)
        {
            deferredNarrativeEntryToken = token;
            deferredNarrativeEntry = resume;
        }

        private void ResumeDeferredNarrativeEntry()
        {
            if (deferredNarrativeEntry == null || !SessionAllowsPlay || !running) return;
            Action resume = deferredNarrativeEntry;
            deferredNarrativeEntry = null;
            if (deferredNarrativeEntryToken == narrativeEntryVersion) resume();
        }

        private void CompleteNarrativeEntry(int token, NarrativeEntryPoint entry, Action next, bool success)
        {
            if (!SessionAllowsPlay || !running || token != narrativeEntryVersion) return;
            awaitingNarrativeEntry = false;
            if (success)
            {
                if (failedEntry == entry) failedEntryCount = 0;
                if (entry == NarrativeEntryPoint.AfterBusiness) afterBusinessDoneDay = day;
                if (entry == NarrativeEntryPoint.MorningBeforeMap) morningCompletedDay = day;
                if (IsDetained) { TryCompleteDetainedDay(); return; }
                next();
            }
            else RetryNarrativeEntry(entry, next);
        }

        private void EnsureNarrativeCafeGameplayBinding()
        {
            var presenter = NarrativeRuntime.Instance?.Presenter;
            if (cafeGameplayPlayback == null) cafeGameplayPlayback = PlayNarrativeCafeGameplay;
            if (ReferenceEquals(presenter, cafeGameplayPresenter)
                && (presenter == null || presenter.GameplayPlayback == cafeGameplayPlayback)) return;
            ReleaseNarrativeCafeGameplayBinding();
            // An explicit/injected host takes precedence. Never overwrite somebody else's adapter.
            if (presenter == null || presenter.GameplayPlayback != null) return;
            cafeGameplayPresenter = presenter;
            presenter.GameplayPlayback = cafeGameplayPlayback;
        }

        private void ReleaseNarrativeCafeGameplayBinding()
        {
            var presenter = cafeGameplayPresenter;
            cafeGameplayPresenter = null;
            if (presenter != null && presenter.GameplayPlayback == cafeGameplayPlayback)
                presenter.GameplayPlayback = null;
            var pending = cafeGameplaySession;
            cafeGameplaySession = null;
            pending?.Cancel();
        }

        private IDisposable PlayNarrativeCafeGameplay(NarrativeEventRequest request,
            NarrativeGameplayPresentationData data, Action<NarrativeGameplayResult> completed)
        {
            // A presenter owns at most one gameplay step. Invalidate the previous owner first.
            cafeGameplaySession?.Dispose();
            var pending = new NarrativeCafeGameplayBridgeSession(completed, released =>
            {
                if (ReferenceEquals(cafeGameplaySession, released)) cafeGameplaySession = null;
            }, "narrative-cafe:" + request.RequestId + ":" + data?.eventId + ":" + data?.pageId + ":" + Guid.NewGuid().ToString("N"));
            cafeGameplaySession = pending;
            void Reject(string reason)
            {
                if (pending.IsDisposed) return;
                Debug.LogWarning("[DayFlow] Narrative cafe gameplay cancelled (" + data?.eventId + "/" + data?.pageId + "): " + reason, this);
                pending.Cancel();
            }
            void Arrived()
            {
                if (pending.IsDisposed || pending.IsReady || !ReferenceEquals(cafeGameplaySession, pending)) return;
                if (!SessionAllowsPlay || !running) { Reject("Game session is no longer playing."); return; }
                if (!PlayerCafeOwner.HasSceneOwner || PlayerCafeOwner.Current == null)
                { Reject("CoffeeShop has no live scene-owned cafe world."); return; }
                if (data.gameplayKey == "host:end_business")
                {
                    if (!TryEndBoundCafeBusiness(out string closeError)) { Reject(closeError); return; }
                    pending.CompleteHostCommand();
                    return;
                }
                if (IsDetained) { Reject("Cafe service is unavailable during church.detention.active."); return; }
                if (FindGameplayEndpoint() == null) { Reject("CoffeeShop has no live cafe gameplay endpoint."); return; }
                if (!pending.Attach(PlayerCafeOwner.Current, data.gameplayKey, out string error))
                { Reject(error); return; }
                // Retain NarrativeEntry and its continuation; only the cafe input gate is opened.
                // The runtime releases its OWN narrative pause. No global Resume/Clear is allowed.
                if (flow != null && flow.IsMapOpen) flow.CloseMapForDayFlow();
                ShowGameplayPanel();
                OperationToastPanel.ShowMessage(pending.GameplayHint, string.Empty);
            }
            if (data == null || completed == null || !SessionAllowsPlay || !running || IsFirstSessionGuideActive)
            { Reject("Missing gameplay request or first-day tutorial/session is not ready."); return pending; }
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "CoffeeShop") Arrived();
            else if (flow != null) flow.TransitionToGameplayScene("CoffeeShop", Arrived,
                () => Reject("Could not transition to the real CoffeeShop scene."));
            else Reject("Cannot load the real CoffeeShop without GameFlowBootstrap.");
            return pending;
        }

        private void RetryNarrativeEntry(NarrativeEntryPoint entry, Action next)
        {
            // A cancelled/failed entry may be retried, but must not replay an old map intent.
            pendingMapAction = null;
            // A deterministic content/resource error fails identically on every retry. After the
            // second consecutive failure of the same entry today, let the player continue the day.
            failedEntryCount = failedEntryDay == day && failedEntry == entry ? failedEntryCount + 1 : 1;
            failedEntryDay = day;
            failedEntry = entry;
            bool offerSkip = failedEntryCount >= 2;
            Debug.LogWarning("[DayFlow] Narrative entry " + entry + " did not complete on day " + day
                + " (attempt " + failedEntryCount + (offerSkip ? "; skip offered" : "") + ").", this);
            if (entry == NarrativeEntryPoint.BeforeNight)
            {
                string hint = offerSkip
                    ? "夜间剧情连续出错，已记录错误。再次点击床休息将跳过这段剧情并进入下一天。"
                    : "请回咖啡馆三楼宿舍，再次点击床休息以重试。";
                // EnterCityMap may first load the Map scene; show the hint once that map is on screen
                // (pendingMapAction runs on arrival), never on the hidden resident map.
                pendingMapAction = () => FindFirstObjectByType<MapSceneController>()?.ShowBlockingActionHint(hint);
                EnterCityMap();
                return;
            }
            SetState(DayFlowState.PreBusiness);
            ShowPresentation<PreBusinessPanel>(() => RunNarrativeEntry(entry, next), panel =>
            {
                panel.ShowNarrativeEntryUnavailable();
                if (offerSkip) panel.OfferEntrySkip(() => SkipFailedNarrativeEntry(entry, next));
            });
        }

        private NarrativeEntryPoint failedEntry;
        private int failedEntryDay, failedEntryCount;

        private bool ShouldSkipFailedEntry(NarrativeEntryPoint entry)
            => failedEntryDay == day && failedEntry == entry && failedEntryCount >= 2;

        /// <summary>Continue the day without the failing root. Its results stay discarded; the error is logged.</summary>
        private void SkipFailedNarrativeEntry(NarrativeEntryPoint entry, Action next)
        {
            if (!SessionAllowsPlay || !running || awaitingNarrativeEntry) return;
            Debug.LogError("[DayFlow] Skipping narrative entry " + entry + " on day " + day + " after "
                + failedEntryCount + " consecutive failures; the failed story was not completed.", this);
            failedEntryCount = 0;
            ClosePresentation();
            if (entry == NarrativeEntryPoint.MorningBeforeMap) morningCompletedDay = day;
            if (entry == NarrativeEntryPoint.AfterBusiness) afterBusinessDoneDay = day;
            if (IsDetained) { TryCompleteDetainedDay(); return; }
            next();
        }

        private int afterBusinessDoneDay;

        private void PromptMissedAfterBusiness()
        {
            var open = UIKit.GetPanel<ConfirmationPanel>();
            if (open != null && open.gameObject.activeInHierarchy) return; // already asking
            if (flow == null) { PlayMissedAfterBusiness(); return; }
            flow.ShowGameplayConfirmation("今天还有重要的事",
                "今天营业结束后会发生重要的事情，错过可能影响之后的剧情。\n建议先回一楼点击「开始营业」挂牌；如果仍然休息，这段剧情会在睡前先播放。",
                "仍然休息", PlayMissedAfterBusiness,
                "先去营业", () => OperationToastPanel.ShowMessage("请到一楼大厅点击「开始营业」挂牌，打烊后再回来休息。", string.Empty));
        }

        /// <summary>The player chose to rest anyway: play today's AfterBusiness stories, then the normal night.</summary>
        private void PlayMissedAfterBusiness()
        {
            if (!SessionAllowsPlay || !running || awaitingNarrativeEntry || !CanRequestDayEnd()) return;
            RunNarrativeEntry(NarrativeEntryPoint.AfterBusiness, RunBeforeNightAndCompleteDay);
        }

        public void RefreshMapAvailability()
            => SetMapAvailability(running && (State == DayFlowState.CityMapLeisure
                || State == DayFlowState.FreeCafeBusiness && flow != null && flow.IsMapOpen && !IsFirstSessionGuideActive));

        private void SetMapAvailability(bool available)
        {
            available &= !IsDetained;
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
            pendingMapAction = null;
            SetState(DayFlowState.DateTransition);
            var closing = FindGameplayEndpoint();
            if (closing != null && closing.DayFlowBusinessActive)
                Debug.LogWarning("[DayFlow] Day ended while cafe business was still open; closing it without the settlement page.", this);
            closing?.EndDayFlowBusiness();
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

        private void EnsureBusinessWorldBinding()
        {
            if (!isActiveAndEnabled || !running || State != DayFlowState.FreeCafeBusiness)
            {
                ReleaseBusinessWorldBinding();
                return;
            }
            var world = PlayerCafeOwner.Current;
            if (!ReferenceEquals(businessWorld, world))
            {
                ReleaseBusinessWorldBinding();
                businessWorld = world;
                businessWasActive = false; // An old world's Night cannot settle a new visit.
            }
            if (businessWorld == null) return;
            if (!businessWorldSubscribed)
            {
                businessWorld.Changed += OnBusinessWorldChanged;
                businessWorldSubscribed = true;
            }
            OnBusinessWorldChanged();
        }

        private void ReleaseBusinessWorldBinding()
        {
            if (businessWorldSubscribed && businessWorld != null)
                businessWorld.Changed -= OnBusinessWorldChanged;
            businessWorldSubscribed = false;
        }

        private void OnBusinessWorldChanged()
        {
            // Record synchronously, including open -> Night within one frame. Never settle
            // inside Changed: narrative/world transactions may still be on the call stack.
            if (isActiveAndEnabled && running && State == DayFlowState.FreeCafeBusiness
                && ReferenceEquals(businessWorld, PlayerCafeOwner.Current) && businessWorld != null
                && businessWorld.State.open)
                businessWasActive = true;
        }

        private IDayFlowGameplayEndpoint FindGameplayEndpoint()
        {
            EnsureBusinessWorldBinding();
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
            EnsureBusinessWorldBinding();
            RefreshMapAvailability();
            ApplyAudioState();
            Debug.Log("[DayFlow] " + state);
        }

        private void ApplyAudioState()
        {
            bool city = State == DayFlowState.CityMapLeisure || State == DayFlowState.PreLeisure;
            bool night = State == DayFlowState.DateTransition || State == DayFlowState.PreNight
                || State == DayFlowState.HomeNight || State == DayFlowState.DayComplete;
            if (night) Spotlight.Audio.GameAudio.Music(Spotlight.Audio.AudioKeys.NightMusic);
            else if (city) Spotlight.Audio.GameAudio.Music(Spotlight.Audio.AudioKeys.CityMusic);
            else Spotlight.Audio.GameAudio.CafeDayMusic(day);
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
            public bool mapUsageTutorialPending;
            public bool mapUsageTutorialCompleted;
            public int lastCafeEntryDay;
        }

        public string CaptureJson() => JsonUtility.ToJson(new SaveState
        {
            day = day, previousDate = previousDate, state = State,
            firstDayIntroductionCompleted = firstDayIntroductionCompleted,
            firstDayCafeTutorialStarted = firstDayCafeTutorialStarted,
            firstDayCafeTutorialCompleted = firstDayCafeTutorialCompleted,
            mapUsageTutorialPending = mapUsageTutorialPending,
            mapUsageTutorialCompleted = mapUsageTutorialCompleted,
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
            mapUsageTutorialPending = saved.mapUsageTutorialPending;
            mapUsageTutorialCompleted = saved.mapUsageTutorialCompleted;
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
                mapUsageTutorialPending = false;
                mapUsageTutorialCompleted = false;
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
            EnsureNarrativeCafeGameplayBinding();
            if (State == DayFlowState.FreeCafeBusiness || State == DayFlowState.CityMapLeisure || State == DayFlowState.PreLeisure)
                morningCompletedDay = day;
            if (IsDetained && State != DayFlowState.DateTransition)
            {
                SetMapAvailability(false);
                if (TryCompleteDetainedDay()) return;
                // Interrupted roots must resume in NCS, never be interpreted as a safe day-end.
                if (State == DayFlowState.NarrativeEntry) return;
            }
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
                    FindGameplayEndpoint();
                    EnsureBusinessWorldBinding();
                    ClosePresentation();
                    ShowGameplayPanel();
                    break;
                case DayFlowState.PreLeisure: ShowBusinessEndThen(EnterCityMap); break;
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
