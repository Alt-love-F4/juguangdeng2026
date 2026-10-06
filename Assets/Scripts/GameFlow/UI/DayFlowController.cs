using System;
using UnityEngine;
using YokiFrame;

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
        IntroLetter
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
        public bool BlocksGameplayInput => running && State != DayFlowState.FreeCafeBusiness;
        public IDayFlowPresentation CurrentPresentation => currentPanel as IDayFlowPresentation;

        public void Initialize(GameFlowBootstrap owner)
        {
            flow = owner;
            if (initialized) return;
            initialized = true;
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

        public void BeginNewSession(bool playLetter = true)
        {
            HideForMainMenu();
            day = 1;
            previousDate = InitialDate;
            if (!playLetter)
            {
                BeginNewDayFlow();
                return;
            }
            running = true;
            HideBusinessControls();
            HideGameplayPanel();
            SetState(DayFlowState.IntroLetter);
            ShowPresentation<FirstDayLetterPanel>(BeginNewDayFlow);
        }

        public void BeginNewDayFlow()
        {
            if (!initialized) Initialize(flow);
            running = true;
            HideBusinessControls();
            HideGameplayPanel();
            SetState(DayFlowState.DateTransition);
            ShowPresentation<DateChangePanel>(EnterPreBusiness,
                panel => panel.SetDates(previousDate, CurrentDate));
        }

        private void Update()
        {
            if (!running || State != DayFlowState.FreeCafeBusiness) return;
            // Do not let an endpoint's old state bypass an unfinished presentation.
            if (endpointOwner != null && gameplayEndpoint.DayFlowBusinessFinished)
                FinishBusiness();
        }

        private void EnterPreBusiness()
        {
            SetState(DayFlowState.PreBusiness);
            HideGameplayPanel();
            ShowPresentation<PreBusinessPanel>(EnterBusinessTransition);
        }

        private void EnterBusinessTransition()
        {
            SetState(DayFlowState.BusinessTransition);
            ShowPresentation<CafeOpeningPanel>(StartFreeCafeBusiness);
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
            ShowPresentation<CityMapPanel>(EnterPreNight);
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
        }

        public string CaptureJson() => JsonUtility.ToJson(new SaveState
        {
            day = day, previousDate = previousDate, state = State
        });

        public void RestoreJson(string json)
        {
            SaveState saved = JsonUtility.FromJson<SaveState>(json);
            if (saved == null) return;
            HideForMainMenu();
            day = Mathf.Max(1, saved.day);
            previousDate = string.IsNullOrEmpty(saved.previousDate) ? InitialDate : saved.previousDate;
            State = Enum.IsDefined(typeof(DayFlowState), saved.state) ? saved.state : DayFlowState.PreBusiness;
        }

        /// <summary>Restore the saved phase, never restart the new-game intro unconditionally.</summary>
        public void RestoreSavedDayFlow()
        {
            string json = GameSaveParticipantRegistry.TakePending(SaveId);
            if (!string.IsNullOrEmpty(json)) RestoreJson(json);
            else
            {
                IDayFlowGameplayEndpoint endpoint = FindGameplayEndpoint();
                State = endpoint != null && endpoint.DayFlowBusinessActive
                    ? DayFlowState.FreeCafeBusiness : DayFlowState.PreBusiness;
            }
            running = true;
            HideBusinessControls();
            if (State != DayFlowState.FreeCafeBusiness) HideGameplayPanel();
            switch (State)
            {
                case DayFlowState.IntroLetter: ShowPresentation<FirstDayLetterPanel>(BeginNewDayFlow); break;
                case DayFlowState.DateTransition: BeginNewDayFlow(); break;
                case DayFlowState.PreBusiness: EnterPreBusiness(); break;
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
