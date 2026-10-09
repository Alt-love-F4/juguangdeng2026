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

        [Header("NCS opening content selection")]
        [SerializeField] private string openingPresentationId = NarrativePresentationDataAdapter.DefaultOpeningPresentationId;
        [SerializeField] private string firstDayLetterBodyKey = NarrativePresentationDataAdapter.DefaultFirstDayLetterBodyKey;
        [SerializeField] private string firstDayLetterSignatureKey = NarrativePresentationDataAdapter.DefaultFirstDayLetterSignatureKey;

        private IDialogueScriptSource introductionSource;
        private DialoguePanel introductionPanel;
        private PlayerAvatarSelection avatarSelection;
        public PlayerAvatarSelection CurrentAvatarSelection => avatarSelection;
        private bool firstDayIntroductionCompleted;
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
        public string CurrentDate => FormatDate(FirstDayDate.AddDays(day - 1));
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
            PlayerDetailsProfile.EnsureRegistered();
            GameSaveParticipantRegistry.Capturers[SaveId] = CaptureJson;
            GameSaveParticipantRegistry.Restorers[SaveId] = RestoreJson;
        }

        private void OnDestroy()
        {
            HideForMainMenu();
            if (GameSaveParticipantRegistry.Capturers.TryGetValue(SaveId, out var capture) && capture == CaptureJson)
            {
                GameSaveParticipantRegistry.Capturers.Remove(SaveId);
                GameSaveParticipantRegistry.Restorers.Remove(SaveId);
            }
        }

        public void HideForMainMenu()
        {
            running = false;
            ClosePresentation();
            HideBusinessControls();
            endpointOwner = null;
            gameplayEndpoint = null;
        }

        public void BeginNewSession(bool playLetter = true, bool playIntroduction = true)
        {
            HideForMainMenu();
            PlayerDetailsProfile.ResetForNewSession();
            day = 1;
            firstDayIntroductionCompleted = !playIntroduction;
            previousDate = InitialDate;
            if (!playLetter)
            {
                EnterAvatarSelection();
                return;
            }
            running = true;
            HideBusinessControls();
            HideGameplayPanel();
            SetState(DayFlowState.IntroLetter);
            ShowPresentation<FirstDayLetterPanel>(EnterAvatarSelection, ConfigureFirstDayLetter);
        }

        private void EnterAvatarSelection()
        {
            // Avatar guidance will be introduced elsewhere. Keep startup unobstructed for now.
            ClosePresentation();
            PlayerDetailsProfile.Current.SelectAvatar(AvatarKind.Male);
            BeginNewDayFlow();
        }

        public void BeginNewDayFlow()
        {
            if (!initialized) Initialize(flow);
            running = true;
            HideBusinessControls();
            HideGameplayPanel();
            var actions = Spotlight.Narrative.NarrativeRuntime.Instance?.Actions;
            if (actions != null && actions.CurrentDay < day) actions.BeginDay(day);
            SetState(DayFlowState.DateTransition);
            ShowPresentation<DateChangePanel>(EnterPreBusiness,
                panel => panel.SetDates(previousDate, CurrentDate));
        }

        private void Update()
        {
            if (Time.timeScale == 0f || !running || State != DayFlowState.FreeCafeBusiness) return;
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
            if (TryResolveFirstDayLetter(out FirstDayLetterContent content)) panel.SetContent(content);
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
                    if (!running || version != presentationVersion || State != DayFlowState.FirstDayIntroduction) return;
                    introductionPanel = null;
                    firstDayIntroductionCompleted = true;
                    StartFreeCafeBusiness();
                }, () =>
                {
                    if (!running || version != presentationVersion || State != DayFlowState.FirstDayIntroduction) return;
                    introductionPanel = null;
                    // Cancel is not completion. The preparation page lets the player retry.
                    EnterPreBusiness();
                });
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

        private void StartFreeCafeBusiness()
        {
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
            ShowBusinessControls();
        }

        public void FinishBusiness()
        {
            if (!running || State != DayFlowState.FreeCafeBusiness) return;
            IDayFlowGameplayEndpoint endpoint = FindGameplayEndpoint();
            endpoint?.EndDayFlowBusiness();
            HideBusinessControls();
            HideGameplayPanel();
            SetState(DayFlowState.PreLeisure);
            ShowPresentation<BusinessEndPanel>(EnterCityMap);
        }

        private void EnterCityMap()
        {
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
        public void EndDayFromMap()
        {
            if (!running || State != DayFlowState.CityMapLeisure) return;
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
                if (!running || version != presentationVersion || currentPanel != panel) return;
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
            Debug.Log("[DayFlow] " + state);
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
            if (panel != null) panel.gameObject.SetActive(true);
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
        }

        public string CaptureJson() => JsonUtility.ToJson(new SaveState
        {
            day = day, previousDate = previousDate, state = State,
            firstDayIntroductionCompleted = firstDayIntroductionCompleted
        });

        public void RestoreJson(string json)
        {
            SaveState saved = JsonUtility.FromJson<SaveState>(json);
            if (saved == null) return;
            HideForMainMenu();
            day = Mathf.Max(1, saved.day);
            previousDate = string.IsNullOrEmpty(saved.previousDate) ? InitialDate : saved.previousDate;
            State = Enum.IsDefined(typeof(DayFlowState), saved.state) ? saved.state : DayFlowState.PreBusiness;
            // Old saves already in/after business must not replay the introduction.
            firstDayIntroductionCompleted = saved.firstDayIntroductionCompleted || day > 1
                || (State >= DayFlowState.FreeCafeBusiness && State <= DayFlowState.DayComplete);
        }

        /// <summary>Restore the saved phase, never restart the new-game intro unconditionally.</summary>
        public void RestoreSavedDayFlow()
        {
            PlayerDetailsProfile.RestorePending();
            string json = GameSaveParticipantRegistry.TakePending(SaveId);
            if (!string.IsNullOrEmpty(json)) RestoreJson(json);
            else
            {
                // Missing day-flow data belongs to a legacy loaded session, not a new game.
                firstDayIntroductionCompleted = true;
                IDayFlowGameplayEndpoint endpoint = FindGameplayEndpoint();
                State = endpoint != null && endpoint.DayFlowBusinessActive
                    ? DayFlowState.FreeCafeBusiness : DayFlowState.PreBusiness;
            }
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
