using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.Gameplay
{
    public sealed class CoffeeGameplayPanel : GameFlowPanel
    {
        public TMP_Text goldText, repText, skillText, timerText, stepText, orderListText, pendingCountText, toastText, hintText;
        public Button startDayButton, takeOrderButton, selectCupButton, grindButton, serveButton, discardButton, pauseButton, quickSaveButton, endDayButton, clearItemsButton, resumeButton;
        public Button addCoffeeButton, addMilkButton, addMatchaButton, addChocoButton, addHoneyButton;
        public Slider timerSlider;
        public Transform inventoryRows, deviceRows;
        public GameObject lattePopup, beanPopup, cocoaPopup, pausePopup;
        public LatteArtMinigame latteArtPanel;
        public BeanSelectionMinigame beanSelectionPanel;
        public BeanSelectionButton beanSelectionButton;
        public CocoaQTEMinigame cocoaQTEPanel;
        public static bool IsMinigameOpen { get; private set; }
        private CoffeeGameplayController ctrl;
        private CoffeeWorldRuntime world;
        private readonly Dictionary<string, TMP_Text> stockLabels = new(), deviceLabels = new();
        private readonly Dictionary<string, Button> refillButtons = new(), upgradeButtons = new();
        private float toastUntil;
        protected override void Awake()
        {
            base.Awake(); IsMinigameOpen = false;
            startDayButton?.onClick.AddListener(() => ctrl.StartDay()); takeOrderButton?.onClick.AddListener(() => ctrl.TalkCustomer());
            selectCupButton?.onClick.AddListener(() => world?.SelectCupFromButton());
            if (beanSelectionButton != null)
            {
                if (grindButton == null) grindButton = beanSelectionButton.Button;
                beanSelectionButton.AddListener(PerformCurrentAction);
            }
            else
            {
                grindButton?.onClick.AddListener(PerformCurrentAction);
            }
            serveButton?.onClick.AddListener(() => world?.ServeFromButton()); discardButton?.onClick.AddListener(() => ctrl.DiscardCurrentOrder());
            endDayButton?.onClick.AddListener(() => ctrl.EndDay()); clearItemsButton?.onClick.AddListener(() => world?.ClearLooseItems());
            pauseButton?.onClick.AddListener(Pause); resumeButton?.onClick.AddListener(Resume); quickSaveButton?.onClick.AddListener(QuickSave);
            BindIngredientButton(addCoffeeButton, "coffee_bean"); BindIngredientButton(addMilkButton, "milk"); BindIngredientButton(addMatchaButton, "matcha"); BindIngredientButton(addChocoButton, "chocolate"); BindIngredientButton(addHoneyButton, "honey");
            if (inventoryRows != null) foreach (var ingredient in GameplayConfigService.AllIngredients)
            {
                var row = inventoryRows.Find(ingredient.id); if (row == null) continue;
                stockLabels[ingredient.id] = row.GetComponentInChildren<TMP_Text>();
                var button = row.GetComponentInChildren<Button>(); refillButtons[ingredient.id] = button;
                var id = ingredient.id; button.onClick.AddListener(() => ctrl.RefillIngredient(id));
            }
            if (deviceRows != null) foreach (var device in GameplayConfigService.AllDevices)
            {
                var row = deviceRows.Find(device.id); if (row == null) continue;
                deviceLabels[device.id] = row.GetComponentInChildren<TMP_Text>();
                var button = row.GetComponentInChildren<Button>(); upgradeButtons[device.id] = button;
                var id = device.id; button.onClick.AddListener(() => ctrl.UpgradeDevice(id));
            }
        }
        private void PerformCurrentAction() => world?.PerformCurrentStep();
        private void BindIngredientButton(Button button, string id) => button?.onClick.AddListener(() => world?.AddFromButton(id));
        private void OnEnable() => Bind();
        private void OnDisable() { Detach(); CloseMinigames(); }
        public void OpenFromFlow(GameFlowBootstrap flow) { if (flow != null) SetFlow(flow); Bind(); }
        protected override void OnOpen(IUIData data = null) { base.OnOpen(data); Bind(); }
        protected override void OnClose() { Detach(); CloseMinigames(); base.OnClose(); }
        private void Bind()
        {
            Detach(); ctrl = CoffeeGameplayController.EnsureExists(); world = FindFirstObjectByType<CoffeeWorldRuntime>();
            ctrl.StateChanged += Refresh; ctrl.Toast += OnToast; ctrl.DayEnded += CloseMinigames; Refresh();
        }
        private void Detach() { if (ctrl != null) { ctrl.StateChanged -= Refresh; ctrl.Toast -= OnToast; ctrl.DayEnded -= CloseMinigames; } }
        private void Update()
        {
            if (hintText != null && world != null && Time.unscaledTime >= toastUntil) hintText.text = world.Hint;
            if (Flow == null && Keyboard.current?.escapeKey.wasPressedThisFrame == true) { if (Time.timeScale <= 0) Resume(); else Pause(); }
            if (IsMinigameOpen && ctrl.Session == null) CloseMinigames();
        }
        private void OnToast(string message) { toastUntil = Time.unscaledTime + 3f; if (hintText != null) hintText.text = message; }
        private static void Label(Button button, string text) { var label = button?.GetComponentInChildren<TMP_Text>(); if (label != null) label.text = text; }
        private static void Enable(Button button, bool value) { if (button != null) button.interactable = value; }
        private void Refresh()
        {
            if (ctrl?.Profile == null) return;
            goldText.text = "金币 " + ctrl.Profile.Gold; repText.text = "声望 " + ctrl.Profile.Reputation; skillText.text = "技能点 " + ctrl.Profile.SkillPoints;
            bool running = ctrl.Day.Phase == DayPhase.Running;
            timerText.text = running ? $"第 {ctrl.Day.DayNumber} 天 · {Mathf.CeilToInt(ctrl.Day.TimeRemaining / 60):0}:{ctrl.Day.TimeRemaining % 60:00}" : ctrl.Day.Phase == DayPhase.Ended ? ctrl.Profile.LastDaySummary : "准备营业 · 从杯子堆取杯，可拖动到工作台";
            pendingCountText.text = "待接待 " + ctrl.Orders.PendingCount;
            Label(startDayButton, ctrl.Day.Phase == DayPhase.Ended ? "下一天" : "开始营业"); Enable(startDayButton, !running); Enable(endDayButton, running);
            Enable(takeOrderButton, running && ctrl.Session == null && ctrl.Orders.PendingCount > 0);
            var visitor = ctrl.CurrentOrder ?? ctrl.Orders.NextPending;
            var recent = ctrl.Orders.Orders.Where(o => o.State == OrderState.Served || o.State == OrderState.Failed).TakeLast(4);
            orderListText.text = visitor == null ? "今日暂无待接待顾客" : $"{visitor.CustomerName}\n{visitor.Recipe.comment} · {visitor.Recipe.price} 金币\n杯型：{(visitor.Recipe.cup_size == "small" ? "小杯" : "大杯")}\n";
            orderListText.text += "\n已完成 " + ctrl.Orders.CompletedToday + " 单\n今日收入 " + ctrl.Profile.TodayRevenue + "\n\n" + string.Join("\n", recent.Select(o => (o.State == OrderState.Served ? "✓ " : "× ") + o.Recipe.comment));
            var step = ctrl.Session?.CurrentStep;
            stepText.text = step == null ? "点击顾客交流并接单" : $"{ctrl.Session.CurrentStepIndex + 1}/{ctrl.Session.StepCount} · " + StepDescription(step);
            if (timerSlider != null) { timerSlider.gameObject.SetActive(ctrl.IsProcessing); timerSlider.value = Mathf.Max(0, ctrl.TimedStepProgress); }
            Enable(selectCupButton, step?.Kind == RecipeStepKind.SelectCup); Enable(discardButton, ctrl.Session != null); Enable(serveButton, step?.Kind == RecipeStepKind.Serve);
            bool hasActiveCup = world?.ActiveCup != null;
            bool isActionStep = step != null && step.Kind is RecipeStepKind.GrindClick or RecipeStepKind.Process or RecipeStepKind.BeanSelection or RecipeStepKind.CocoaMinigame or RecipeStepKind.LatteArt;
            var actionLabel = step?.Kind == RecipeStepKind.BeanSelection ? "筛选咖啡豆" : step?.Kind == RecipeStepKind.LatteArt ? "开始拉花" : step?.Kind == RecipeStepKind.CocoaMinigame ? "可可搅拌" : step?.Kind == RecipeStepKind.GrindClick ? "研磨" : "使用设备";
            Enable(grindButton, isActionStep && hasActiveCup && !ctrl.IsProcessing);
            if (beanSelectionButton != null) beanSelectionButton.SetState(grindButton != null && grindButton.interactable, actionLabel);
            else Label(grindButton, actionLabel);
            IngredientState(addCoffeeButton, "coffee_bean", step); IngredientState(addMilkButton, "milk", step); IngredientState(addMatchaButton, "matcha", step); IngredientState(addChocoButton, "chocolate", step); IngredientState(addHoneyButton, "honey", step);
            foreach (var entry in GameplayConfigService.AllIngredients)
            {
                if (stockLabels.TryGetValue(entry.id, out var label)) label.text = $"{entry.comment} {ctrl.Profile.GetStock(entry.id)}/{entry.max_stock}" + (ctrl.Profile.Reputation < entry.unlock_rep ? $" · 声望{entry.unlock_rep}解锁" : $" · 补满 {entry.refill_cost}金");
                if (refillButtons.TryGetValue(entry.id, out var button)) button.interactable = ctrl.Profile.Reputation >= entry.unlock_rep && ctrl.Profile.GetStock(entry.id) < entry.max_stock && ctrl.Profile.CanAfford(entry.refill_cost);
            }
            foreach (var entry in GameplayConfigService.AllDevices)
            {
                bool upgraded = ctrl.Profile.UpgradedDevices.ContainsKey(entry.id);
                if (deviceLabels.TryGetValue(entry.id, out var label)) label.text = $"{entry.comment} · {ctrl.GetDeviceTime(entry.id, entry.base_time):0.#}秒" + (upgraded ? " · 已升级" : $" · 升级 {entry.upgrade_cost}技能点");
                if (upgradeButtons.TryGetValue(entry.id, out var button)) button.interactable = !upgraded && ctrl.Profile.Reputation >= entry.unlock_rep && ctrl.Profile.SkillPoints >= entry.upgrade_cost;
            }
        }
        private void IngredientState(Button button, string id, RecipeStep step) => Enable(button, step?.Kind == RecipeStepKind.AddIngredient && step.IngredientId == id && ctrl.Profile.GetStock(id) >= step.Amount);
        private string StepDescription(RecipeStep step) => step.Kind switch
        {
            RecipeStepKind.SelectCup => "从杯子堆取出正确杯型，放到任一工作台",
            RecipeStepKind.AddIngredient => "把「" + GameplayConfigService.GetIngredient(step.IngredientId)?.comment + "」拖入当前杯子",
            RecipeStepKind.BeanSelection => "点击杯子 / 筛豆按钮，挑出好豆",
            RecipeStepKind.GrindClick => $"将杯子拖到研磨器，再点击研磨器 · {ctrl.Session.Clicks}/{step.ClickCount}",
            RecipeStepKind.Process => ctrl.IsProcessing ? "加工中：" + GameplayConfigService.GetDevice(step.DeviceId)?.comment : "将杯子拖到：" + GameplayConfigService.GetDevice(step.DeviceId)?.comment,
            RecipeStepKind.CocoaMinigame => "把杯子移到可可壶，完成搅拌小游戏",
            RecipeStepKind.LatteArt => "点击杯子 / 拉花按钮，在咖啡表面绘制图案",
            RecipeStepKind.Serve => "将完成的杯子拖到右侧出餐口",
            _ => step.Kind.ToString()
        };
        public void OpenStepMinigame()
        {
            if (IsMinigameOpen) return;
            if (ctrl?.Session == null) { OnToast("当前没有制作中的订单"); return; }
            if (ctrl.Day.Phase != DayPhase.Running) { OnToast("当前不在营业状态"); return; }
            if (world?.ActiveCup == null) { OnToast("请先把当前订单的杯子放到工作台"); return; }

            var session = ctrl.Session; var kind = session.CurrentStep.Kind;
            if (kind == RecipeStepKind.CocoaMinigame && world.Ledger.Definition(world.ActiveCup.locationId)?.behavior_id != session.CurrentStep.DeviceId) { world.UseCurrentDeviceFromButton(); return; }
            world.CancelDrag();
            if (kind == RecipeStepKind.BeanSelection && beanSelectionPanel != null && beanPopup != null)
            {
                IsMinigameOpen = true; beanPopup.SetActive(true); beanSelectionPanel.Open((success, score) => { CloseMinigames(); if (ctrl.Session == session) ctrl.CompleteBeanSelection(success, score); });
            }
            else if (kind == RecipeStepKind.LatteArt && latteArtPanel != null && lattePopup != null)
            {
                IsMinigameOpen = true; lattePopup.SetActive(true); latteArtPanel.Open((score, shape) => { CloseMinigames(); if (ctrl.Session == session) ctrl.CompleteLatteArt(score, shape); });
            }
            else if (kind == RecipeStepKind.CocoaMinigame && cocoaQTEPanel != null && cocoaPopup != null)
            {
                IsMinigameOpen = true; cocoaPopup.SetActive(true); cocoaQTEPanel.Open(success => { CloseMinigames(); if (ctrl.Session == session) ctrl.CompleteCocoaQTE(success); });
            }
            else
            {
                OnToast("当前小游戏组件未绑定");
            }
        }
        private void CloseMinigames()
        {
            IsMinigameOpen = false; latteArtPanel?.Cancel(); cocoaQTEPanel?.Cancel(); beanSelectionPanel?.Cancel();
            if (lattePopup != null) lattePopup.SetActive(false); if (beanPopup != null) beanPopup.SetActive(false); if (cocoaPopup != null) cocoaPopup.SetActive(false);
        }
        private void Pause()
        {
            world?.CancelDrag();
            if (Flow != null) { Flow.OpenPause(); return; }
            Time.timeScale = 0; if (pausePopup != null) pausePopup.SetActive(true);
        }
        private void Resume() { Time.timeScale = 1; if (pausePopup != null) pausePopup.SetActive(false); }
        private void QuickSave()
        {
            world?.CancelDrag();
            if (Flow != null) { Flow.QuickSave(); return; }
            var data = new GameSaveData { SceneName = SceneManager.GetActiveScene().name, CheckpointId = "coffee.day." + ctrl.Day.DayNumber, State = ctrl.Day.Phase.ToString(), Participants = GameSaveParticipantRegistry.CaptureAll() };
            OnToast(new GameSaveService().TryQuickSave(data, out int slot, out string error) ? "已保存到存档 " + slot : error);
        }
    }
}
