using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.Narrative
{
    /// <summary>
    /// Daily loop scaffold. Each phase is a replaceable UI entry point for a future module.
    /// NarrativeRuntime remains the owner of calendar advancement.
    /// </summary>
    public sealed class NarrativeGameplayHUD : MonoBehaviour
    {
        private enum DailyPhase { DateChange, PreBusiness, Business, PreLeisure, Leisure, PreNight, Night }
        private enum BusinessView { CoffeeMaking, LatteArt, CafeScene }

        [SerializeField] private int kingdomYear = 674;
        [SerializeField] private int startMonth = 9;
        [SerializeField] private int startDay = 19;

        private NarrativeRuntime runtime;
        private GameObject root;
        private TMP_Text dateText, phaseText, descriptionText;
        private Button primaryButton, secondaryButton, tertiaryButton;
        private bool visible, advancingDay;
        private DailyPhase phase;
        private BusinessView businessView;
        private int displayedCalendarDay = 1;

        public Func<bool> CanInteract { get; set; }
        private bool AllowsAction => visible && runtime != null && runtime.IsReady && !runtime.BlocksGameplayInput
            && Time.timeScale > 0 && !advancingDay && (CanInteract?.Invoke() ?? true);

        public static NarrativeGameplayHUD Create(NarrativeRuntime owner)
        {
            GameObject go = new GameObject("NarrativeGameplayHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(NarrativeGameplayHUD));
            DontDestroyOnLoad(go);
            NarrativeGameplayHUD hud = go.GetComponent<NarrativeGameplayHUD>();
            hud.runtime = owner;
            hud.Build();
            owner.BarChanged += hud.OnBarChanged;
            owner.TurnCompleted += hud.OnTurnCompleted;
            hud.SetVisible(false);
            return hud;
        }

        private void Build()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            root = new GameObject("DailyFlowControls", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.12f, .06f); rect.anchorMax = new Vector2(.88f, .32f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            dateText = AddText(root.transform, "Date", new Vector2(0, .76f), new Vector2(1, 1), 28);
            phaseText = AddText(root.transform, "Phase", new Vector2(0, .56f), new Vector2(1, .75f), 24);
            descriptionText = AddText(root.transform, "Description", new Vector2(0, .26f), new Vector2(1, .54f), 20);
            descriptionText.alignment = TextAlignmentOptions.Center;
            primaryButton = AddButton(root.transform, "Primary", new Vector2(0, 0), new Vector2(.32f, .21f));
            secondaryButton = AddButton(root.transform, "Secondary", new Vector2(.34f, 0), new Vector2(.66f, .21f));
            tertiaryButton = AddButton(root.transform, "Tertiary", new Vector2(.68f, 0), new Vector2(1, .21f));
            Refresh();
        }

        private static TMP_Text AddText(Transform parent, string name, Vector2 min, Vector2 max, float size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            TMP_Text text = go.GetComponent<TMP_Text>(); text.font = Resources.Load<TMP_FontAsset>("Fonts/乐米曲奇方块体 SDF"); text.fontSize = size; text.color = Color.white; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text;
        }

        private static Button AddButton(Transform parent, string label, Vector2 min, Vector2 max)
        {
            GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(.08f, .12f, .18f, .96f);
            Button button = go.GetComponent<Button>();
            TMP_Text text = AddText(go.transform, "Label", Vector2.zero, Vector2.one, 20); text.text = label; return button;
        }

        public void Refresh()
        {
            if (dateText == null) return;
            ReadCalendarDay(); dateText.text = FormatDate(displayedCalendarDay); phaseText.text = PhaseTitle(phase); descriptionText.text = PhaseDescription(phase, businessView);
            ClearButtons();
            switch (phase)
            {
                case DailyPhase.DateChange: AddAction(primaryButton, "开始新的一天", () => { phase = DailyPhase.PreBusiness; Refresh(); }); break;
                case DailyPhase.PreBusiness: AddAction(primaryButton, "进入咖啡馆", () => { phase = DailyPhase.Business; businessView = BusinessView.CoffeeMaking; Refresh(); }); break;
                case DailyPhase.Business:
                    AddAction(primaryButton, businessView == BusinessView.CafeScene ? "结束营业" : "制作咖啡", () =>
                    {
                        if (businessView == BusinessView.CafeScene) phase = DailyPhase.PreLeisure;
                        else businessView = BusinessView.CoffeeMaking;
                        Refresh();
                    });
                    AddAction(secondaryButton, "拉花出品", () => { businessView = BusinessView.LatteArt; Refresh(); });
                    AddAction(tertiaryButton, "查看场景", () => { businessView = BusinessView.CafeScene; Refresh(); }); break;
                case DailyPhase.PreLeisure: AddAction(primaryButton, "前往圣诺特城区", () => { phase = DailyPhase.Leisure; Refresh(); }); break;
                case DailyPhase.Leisure: AddAction(primaryButton, "选择地点（占位）", () => { phase = DailyPhase.PreNight; Refresh(); }); break;
                case DailyPhase.PreNight: AddAction(primaryButton, "回到住所", () => { phase = DailyPhase.Night; Refresh(); }); break;
                case DailyPhase.Night: AddAction(primaryButton, "结束夜晚活动", FinishDay); break;
            }
        }

        private void ClearButtons()
        {
            primaryButton.onClick.RemoveAllListeners(); secondaryButton.onClick.RemoveAllListeners(); tertiaryButton.onClick.RemoveAllListeners();
            SetButton(primaryButton, "", false); SetButton(secondaryButton, "", false); SetButton(tertiaryButton, "", false);
        }

        private void AddAction(Button button, string label, Action action)
        {
            SetButton(button, label, true); button.onClick.AddListener(() => { if (AllowsAction) action(); });
        }

        private static void SetButton(Button button, string label, bool active)
        {
            button.gameObject.SetActive(active); button.interactable = active;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(); if (text != null) text.text = label;
        }

        private void FinishDay()
        {
            if (!AllowsAction || runtime == null) return;
            advancingDay = true; runtime.BeginTurn(); runtime.EndTurn();
        }

        private void OnTurnCompleted() { advancingDay = false; phase = DailyPhase.DateChange; Refresh(); }

        private void ReadCalendarDay()
        {
            displayedCalendarDay = 1;
            if (runtime?.Controller != null) runtime.Controller.State.TryGetBar("world.calendar", out displayedCalendarDay);
            displayedCalendarDay = Mathf.Max(1, displayedCalendarDay);
        }

        private string FormatDate(int calendarDay)
        {
            DateTime date = new DateTime(Mathf.Clamp(kingdomYear, 1, 9999), Mathf.Clamp(startMonth, 1, 12), Mathf.Clamp(startDay, 1, 28)).AddDays(calendarDay - 1);
            return $"王国纪元{date.Year}年{date.Month}月{date.Day}日";
        }

        private static string PhaseTitle(DailyPhase value)
        {
            switch (value)
            {
                case DailyPhase.DateChange: return "日期变更";
                case DailyPhase.PreBusiness: return "经营前阶段";
                case DailyPhase.Business: return "经营阶段 · 制作 / 场景";
                case DailyPhase.PreLeisure: return "闲暇前阶段";
                case DailyPhase.Leisure: return "闲暇时段";
                case DailyPhase.PreNight: return "夜晚前阶段";
                default: return "夜晚时段";
            }
        }

        private static string PhaseDescription(DailyPhase value, BusinessView view)
        {
            switch (value)
            {
                case DailyPhase.DateChange: return "日历翻动与日期展示。";
                case DailyPhase.PreBusiness: return "事件插入点：经营前事件将在这里接入。";
                case DailyPhase.Business: return view == BusinessView.CoffeeMaking ? "制作界面占位：接入咖啡制作玩法。" : view == BusinessView.LatteArt ? "拉花出品界面占位：接入出品判定。" : "场景界面占位：接入楼层、客人和房间互动。";
                case DailyPhase.PreLeisure: return "事件插入点：闲暇前事件将在这里接入。";
                case DailyPhase.Leisure: return "圣诺特城区地图占位：地点和可访问人物将在这里接入。";
                case DailyPhase.PreNight: return "事件插入点：夜晚前事件将在这里接入。";
                default: return "住所外景占位：接入选项式夜晚活动。";
            }
        }

        public void SetVisible(bool value) { visible = value; if (root != null) root.SetActive(value); if (value && !advancingDay) Refresh(); }
        private void OnBarChanged(string key, int oldValue, int newValue) { if (key == "world.calendar") Refresh(); }
        private void Update()
        {
            bool canInteract = AllowsAction;
            if (primaryButton != null) primaryButton.interactable = canInteract && primaryButton.gameObject.activeSelf;
            if (secondaryButton != null) secondaryButton.interactable = canInteract && secondaryButton.gameObject.activeSelf;
            if (tertiaryButton != null) tertiaryButton.interactable = canInteract && tertiaryButton.gameObject.activeSelf;
        }

        private void OnDestroy()
        {
            if (runtime == null) return;
            runtime.BarChanged -= OnBarChanged; runtime.TurnCompleted -= OnTurnCompleted;
        }
    }
}
