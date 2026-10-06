using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        DayComplete
    }

    /// <summary>
    /// Owns the day-level loop. FreeCafe remains responsible for the business simulation;
    /// this controller only coordinates phase boundaries and placeholder presentation.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class DayFlowController : MonoBehaviour
    {
        public const string SaveId = "game.day-flow.v1";
        private const string InitialDate = "王国纪元 674年9月19日";

        private GameFlowBootstrap flow;
        private Canvas canvas;
        private Image background;
        private Image shade;
        private TMP_Text phaseText;
        private TMP_Text detailText;
        private Button actionButton;
        private Button endBusinessButton;
        private Coroutine transitionRoutine;
        private bool initialized;
        private bool running;
        private bool waitingForFreeCafe;
        private string previousDate = InitialDate;
        private int day = 1;

        public DayFlowState State { get; private set; } = DayFlowState.DateTransition;
        public string CurrentDate => "王国纪元 674年9月" + (19 + day) + "日";
        public void HideForMainMenu()
        {
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            running = false;
            HideBusinessControls();
            HideOverlay();
        }

        public void Initialize(GameFlowBootstrap owner)
        {
            flow = owner;
            if (initialized) return;
            initialized = true;
            CreateUi();
            GameSaveParticipantRegistry.Capturers[SaveId] = CaptureJson;
            GameSaveParticipantRegistry.Restorers[SaveId] = RestoreJson;
            HideOverlay();
        }

        private void OnDestroy()
        {
            if (GameSaveParticipantRegistry.Capturers.TryGetValue(SaveId, out var capture) && capture == CaptureJson)
            {
                GameSaveParticipantRegistry.Capturers.Remove(SaveId);
                GameSaveParticipantRegistry.Restorers.Remove(SaveId);
            }
        }

        public void BeginNewDayFlow()
        {
            if (!initialized) Initialize(flow);
            running = true;
            waitingForFreeCafe = false;
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(BeginRoutine());
        }

        private IEnumerator BeginRoutine()
        {
            SetState(DayFlowState.DateTransition);
            ShowOverlay("新的一天", previousDate + "  →  " + CurrentDate, "日期变更");
            yield return new WaitForSecondsRealtime(1.2f);
            SetState(DayFlowState.PreBusiness);
            HideGameplayPanel();
            ShowOverlay("经营前阶段", "今天的咖啡馆即将开始营业。这里可以接入开店前事件、昨日结算和准备工作。", "进入准备");
        }

        private void Update()
        {
            if (!running) return;
            IDayFlowGameplayEndpoint endpoint = FindGameplayEndpoint();
            if (endpoint != null && State != DayFlowState.FreeCafeBusiness && endpoint.DayFlowBusinessActive)
            {
                SetState(DayFlowState.FreeCafeBusiness);
                ShowGameplayPanel();
                HideOverlay();
                ShowBusinessControls();
            }
            if (State == DayFlowState.FreeCafeBusiness && endpoint != null && endpoint.DayFlowBusinessFinished)
                FinishBusiness();
        }

        private void Advance()
        {
            switch (State)
            {
                case DayFlowState.PreBusiness:
                    SetState(DayFlowState.BusinessTransition);
                    HideGameplayPanel();
                    ShowOverlay("咖啡馆营业", "店铺点灯、整理台面，今天的客人即将到来。", "开始营业");
                    break;
                case DayFlowState.BusinessTransition:
                    StartFreeCafeBusiness();
                    break;
                case DayFlowState.PreLeisure:
                    SetState(DayFlowState.CityMapLeisure);
                    ShowOverlay("闲暇时段 · 圣诺特城区", "这里是城区地图占位。之后可接入天气、行动力、地点和人物可用性。", "前往城区");
                    break;
                case DayFlowState.CityMapLeisure:
                    SetState(DayFlowState.PreNight);
                    ShowOverlay("夜晚前阶段", "城区活动结束，准备返回住房。这里可以接入错过事件和返程事件。", "返回住房");
                    break;
                case DayFlowState.PreNight:
                    SetState(DayFlowState.HomeNight);
                    ShowOverlay("夜晚 · 住房外景", "这里是住房外景占位。夜晚活动选项将在此处接入。", "结束夜晚");
                    break;
                case DayFlowState.HomeNight:
                    CompleteDay();
                    break;
                case DayFlowState.DayComplete:
                    BeginNewDayFlow();
                    break;
            }
        }

        private void StartFreeCafeBusiness()
        {
            SetState(DayFlowState.FreeCafeBusiness);
            ShowGameplayPanel();
            HideOverlay();
            waitingForFreeCafe = true;
            IDayFlowGameplayEndpoint endpoint = FindGameplayEndpoint();
            if (endpoint == null)
            {
                ShowOverlay("经营阶段不可用", "未找到当前玩法模块。请确认当前运行场景已注册经营入口。", "返回经营前");
                SetState(DayFlowState.PreBusiness);
                return;
            }
            endpoint.StartDayFlowBusiness();
            waitingForFreeCafe = false;
            ShowBusinessControls();
        }

        public void FinishBusiness()
        {
            if (State != DayFlowState.FreeCafeBusiness) return;
            IDayFlowGameplayEndpoint endpoint = FindGameplayEndpoint();
            endpoint?.EndDayFlowBusiness();
            SetState(DayFlowState.PreLeisure);
            HideBusinessControls();
            ShowOverlay("经营结束", "今日营业已结束。可以查看结算，随后前往城区。", "进入闲暇");
        }

        private void CompleteDay()
        {
            SetState(DayFlowState.DayComplete);
            previousDate = CurrentDate;
            day++;
            ShowOverlay("一天结束", "夜晚活动完成。准备进入下一天。", "进入下一天");
        }

        private IDayFlowGameplayEndpoint FindGameplayEndpoint()
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (MonoBehaviour behaviour in behaviours)
                if (behaviour is IDayFlowGameplayEndpoint endpoint)
                    return endpoint;
            return null;
        }

        private void SetState(DayFlowState state)
        {
            State = state;
            Debug.Log("[DayFlow] " + state);
        }

        private void ShowOverlay(string title, string detail, string button)
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(true);
            ApplyBackground();
            shade.color = new Color(.03f, .04f, .06f, .28f);
            shade.raycastTarget = true;
            phaseText.text = title;
            detailText.text = detail;
            actionButton.gameObject.SetActive(true);
            actionButton.GetComponentInChildren<TMP_Text>().text = button;
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(Advance);
        }

        private void ShowBusinessControls()
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(true);
            background.gameObject.SetActive(false);
            shade.color = new Color(0, 0, 0, 0);
            shade.raycastTarget = false;
            phaseText.text = "经营阶段 · FreeCafe";
            detailText.text = "正在营业。完成制作和接客后，可以结束营业进入闲暇。";
            actionButton.gameObject.SetActive(false);
            endBusinessButton.gameObject.SetActive(true);
        }

        private void HideBusinessControls()
        {
            if (endBusinessButton != null) endBusinessButton.gameObject.SetActive(false);
        }

        private void HideOverlay()
        {
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void ApplyBackground()
        {
            if (background == null) return;
            string path = State switch
            {
                DayFlowState.DateTransition => "Art/Gameplay/DayFlow/phase_date_transition",
                DayFlowState.PreBusiness => "Art/Gameplay/DayFlow/phase_pre_business",
                DayFlowState.BusinessTransition => "Art/Gameplay/DayFlow/phase_business_transition",
                DayFlowState.PreLeisure => "Art/Gameplay/DayFlow/phase_pre_leisure",
                DayFlowState.CityMapLeisure => "Art/Gameplay/DayFlow/city_map_placeholder",
                DayFlowState.PreNight => "Art/Gameplay/DayFlow/phase_pre_night",
                DayFlowState.HomeNight => "Art/Gameplay/DayFlow/home_exterior_night",
                DayFlowState.DayComplete => "Art/Gameplay/DayFlow/day_end_transition",
                _ => null
            };
            Texture2D texture = string.IsNullOrEmpty(path) ? null : Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                background.sprite = null;
                background.gameObject.SetActive(false);
                return;
            }
            background.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            background.gameObject.SetActive(true);
        }

        private void CreateUi()
        {
            GameObject root = new GameObject("DayFlowOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            background = CreateFullscreenImage(root.transform, "Background");
            background.preserveAspect = false;
            background.raycastTarget = false;

            shade = CreateFullscreenImage(root.transform, "Shade");
            shade.raycastTarget = true;

            phaseText = CreateText(root.transform, "Phase", new Vector2(0, 180), new Vector2(900, 100), 48);
            detailText = CreateText(root.transform, "Detail", new Vector2(0, 20), new Vector2(900, 180), 26);
            PositionTopLeft(phaseText.rectTransform, new Vector2(64, -58));
            PositionTopLeft(detailText.rectTransform, new Vector2(64, -142));
            phaseText.alignment = TextAlignmentOptions.Left;
            detailText.alignment = TextAlignmentOptions.Left;
            actionButton = CreateButton(root.transform, "Advance", new Vector2(-250, 72), new Vector2(360, 86));
            PositionBottomRight(actionButton.GetComponent<RectTransform>(), new Vector2(64, 64));
            endBusinessButton = CreateButton(root.transform, "EndBusiness", new Vector2(-250, 180), new Vector2(360, 76));
            PositionBottomRight(endBusinessButton.GetComponent<RectTransform>(), new Vector2(64, 170));
            endBusinessButton.transform.SetAsLastSibling();
            endBusinessButton.GetComponentInChildren<TMP_Text>().text = "结束营业并进入闲暇";
            endBusinessButton.onClick.AddListener(FinishBusiness);
            endBusinessButton.gameObject.SetActive(false);
        }

        private void HideGameplayPanel()
        {
            if (GameplayPanelHooks.PanelType == null) return;
            GameFlowPanel panel = FindFirstObjectByType(GameplayPanelHooks.PanelType, FindObjectsInactive.Include) as GameFlowPanel;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void ShowGameplayPanel()
        {
            if (GameplayPanelHooks.PanelType == null) return;
            GameFlowPanel panel = FindFirstObjectByType(GameplayPanelHooks.PanelType, FindObjectsInactive.Include) as GameFlowPanel;
            if (panel != null) panel.gameObject.SetActive(true);
        }

        private Image CreateFullscreenImage(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go.GetComponent<Image>();
        }

        private TMP_Text CreateText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.font = Resources.Load<TMP_FontAsset>("Fonts/SimHei SDF");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            return text;
        }

        private static void PositionTopLeft(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
        }

        private static void PositionBottomRight(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(-position.x, position.y);
        }

        private Button CreateButton(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(.38f, .22f, .1f, .96f);
            Button button = go.GetComponent<Button>();
            TMP_Text text = CreateText(go.transform, "Label", Vector2.zero, size - new Vector2(12, 8), 26);
            text.text = name;
            return button;
        }

        [Serializable]
        private sealed class SaveState
        {
            public int day = 1;
            public string previousDate = InitialDate;
            public DayFlowState state = DayFlowState.DateTransition;
        }

        public string CaptureJson() => JsonUtility.ToJson(new SaveState { day = day, previousDate = previousDate, state = State });

        public void RestoreJson(string json)
        {
            SaveState saved = JsonUtility.FromJson<SaveState>(json);
            if (saved == null) return;
            day = Mathf.Max(1, saved.day);
            previousDate = string.IsNullOrEmpty(saved.previousDate) ? InitialDate : saved.previousDate;
            State = saved.state;
        }
    }
}
