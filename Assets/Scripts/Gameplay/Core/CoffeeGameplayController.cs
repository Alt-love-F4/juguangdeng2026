using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.GameFlow;
using Spotlight.Gameplay.Social;
using Spotlight.Interaction;
using UnityEngine;

namespace Spotlight.Gameplay
{
    public sealed class CoffeeGameplayController : MonoBehaviour
    {
        public static CoffeeGameplayController Instance { get; private set; }
        public static CoffeeGameplayController EnsureExists()
        {
            if (Instance == null) new GameObject(nameof(CoffeeGameplayController)).AddComponent<CoffeeGameplayController>();
            return Instance;
        }
        public PlayerProfile Profile { get; private set; }
        public OrderManager Orders { get; private set; }
        public DayCycleController Day { get; private set; }
        public CraftingSession Session { get; private set; }
        public CustomerOrder CurrentOrder { get; private set; }
        public string CurrentLatteArtShape { get; private set; }
        public float TimedStepProgress { get; private set; } = -1;
        public float TimedStepDuration { get; private set; }
        public string TimedDeviceId { get; private set; }
        public bool IsProcessing => TimedStepProgress >= 0;
        public event Action StateChanged;
        public event Action<string> Toast;
        public event Action<RecipeStep> StepChanged;
        public event Action DayEnded;
        /// <summary>Future narrative/topic acquisition listens here; the NPC entity owns no story rules.</summary>
        public event Action<string> NpcInteracted;
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; Profile = PlayerProfile.CreateDefault(); Orders = new OrderManager(Profile); Day = new DayCycleController(Profile, Orders);
            Orders.OnOrdersChanged += NotifyState; Day.OnDayStarted += NotifyState; Day.OnTimeTick += OnTick; Day.OnDayEnded += HandleDayEnded;
            GameSaveParticipantRegistry.Capturers[GameplaySaveParticipant.ParticipantId] = CaptureProfileJson;
            GameSaveParticipantRegistry.Restorers[GameplaySaveParticipant.ParticipantId] = RestoreProfileJson;
        }
        private void Start()
        {
            var pending = GameSaveParticipantRegistry.TakePending(GameplaySaveParticipant.ParticipantId);
            if (!string.IsNullOrEmpty(pending)) RestoreProfileJson(pending);
            NotifyState();
        }
        private void OnDestroy()
        {
            if (Instance != this) return;
            CleanupSession(); Orders.OnOrdersChanged -= NotifyState; Day.OnDayStarted -= NotifyState; Day.OnTimeTick -= OnTick; Day.OnDayEnded -= HandleDayEnded;
            GameSaveParticipantRegistry.Capturers.Remove(GameplaySaveParticipant.ParticipantId); GameSaveParticipantRegistry.Restorers.Remove(GameplaySaveParticipant.ParticipantId); Instance = null;
        }
        private void Update() => Tick(Time.deltaTime);
        public void Tick(float delta)
        {
            Day.Tick(delta);
            if (!IsProcessing || Session == null || Day.Phase != DayPhase.Running) return;
            TimedStepProgress += Mathf.Max(0, delta) / Mathf.Max(.01f, TimedStepDuration);
            if (TimedStepProgress >= 1) { TimedStepProgress = -1; TimedDeviceId = null; Session.CompleteTimerStep(); }
            NotifyState();
        }
        private void OnTick(float _) => NotifyState();
        public void NotifyState() => StateChanged?.Invoke();
        public void ShowToast(string text) { if (string.IsNullOrEmpty(text)) return; Debug.Log("[Gameplay] " + text); Toast?.Invoke(text); }
        public void StartDay()
        {
            if (Day.Phase == DayPhase.Running) return;
            CleanupSession(); CurrentOrder = null; Day.StartDay(); ShowToast("开始营业：点击顾客接单，或使用下方辅助按钮");
        }
        public void EndDay() => Day.EndDay();
        private void HandleDayEnded()
        {
            if (CurrentOrder != null) { Orders.MarkFailed(CurrentOrder); CleanupSession(); CurrentOrder = null; }
            foreach (var order in Orders.Orders) if (order.State == OrderState.Pending) order.State = OrderState.Failed;
            DayEnded?.Invoke(); ShowToast(Profile.LastDaySummary); NarrativeServiceRegistry.Current.TriggerEvent(NarrativeEvents.DayEnded); NotifyState();
        }
        public void TalkCustomer()
        {
            if (Day.Phase != DayPhase.Running) { ShowToast("请先开始营业"); return; }
            var visitor = CurrentOrder ?? Orders.NextPending;
            if (visitor == null) { ShowToast("今日顾客已全部接待，可提前打烊"); return; }
            var npc = GameplayConfigService.GetNpc(visitor.NpcId);
            ShowToast(visitor.CustomerName + "：" + (npc?.dialogue ?? "请帮我做一杯咖啡。"));
            NpcInteracted?.Invoke(visitor.NpcId);
            if (Session == null) TakeNextOrder();
        }
        public void TakeNextOrder()
        {
            if (Session != null || Day.Phase != DayPhase.Running) return;
            CurrentOrder = Orders.TakeNextPending();
            if (CurrentOrder == null) { ShowToast("没有待处理订单"); return; }
            Session = new CraftingSession(CurrentOrder, Profile); HookSession(); CurrentLatteArtShape = null; Session.Start(); NotifyState();
        }
        private void HookSession()
        {
            Session.OnStepChanged += OnSessionStepChanged; Session.OnSessionFinished += OnSessionFinished;
            Session.OnGrindProgress += OnProgress; Session.OnCocoaMistake += OnCocoaMistake;
        }
        private void OnProgress(int _) => NotifyState();
        private void OnCocoaMistake(int count) { ShowToast("可可失误，剩余容错 " + Mathf.Max(0, Session.MaxCocoaMistakes - count)); NotifyState(); }
        public InteractionResult TrySelectCup(string size)
        {
            if (Day.Phase != DayPhase.Running || Session == null) return InteractionResult.Reject("请先接单");
            var result = Session.CanSelectCup(size); if (!result.Success) return result;
            Session.SelectCup(); NotifyState(); return InteractionResult.Accept("杯子已放好");
        }
        public void SelectCup() => FindFirstObjectByType<CoffeeWorldRuntime>()?.SelectCupFromButton();
        public InteractionResult TryAddIngredient(string id, bool prepaid = false)
        {
            if (Day.Phase != DayPhase.Running || Session == null || IsProcessing) return InteractionResult.Reject("当前不能加料");
            var oldStep = Session.CurrentStep; var result = Session.AddIngredient(id, prepaid);
            if (!result.Success) return result;
            if (oldStep.Kind == RecipeStepKind.AddMilk) BeginTimedStep(oldStep);
            NotifyState(); return InteractionResult.Accept("已加入：" + GameplayConfigService.GetIngredient(id).comment);
        }
        public void AddIngredient(string id) => FindFirstObjectByType<CoffeeWorldRuntime>()?.AddFromButton(id);
        public InteractionResult TryUseDevice(string deviceId)
        {
            if (Session == null || Day.Phase != DayPhase.Running) return InteractionResult.Reject("请先接单");
            if (IsProcessing) return InteractionResult.Reject("设备正在加工，请稍候");
            var step = Session.CurrentStep;
            if (step == null || step.DeviceId != deviceId) return InteractionResult.Reject("当前步骤不使用这台设备");
            if (step.Kind == RecipeStepKind.GrindClick) Session.GrindClick();
            else if (step.Kind is RecipeStepKind.Process or RecipeStepKind.MokaPot or RecipeStepKind.MatchaGrind) BeginTimedStep(step);
            else if (step.Kind == RecipeStepKind.CocoaMinigame) { FindFirstObjectByType<CoffeeGameplayPanel>()?.OpenStepMinigame(); return InteractionResult.Accept("开始可可搅拌"); }
            else return InteractionResult.Reject("这台设备不支持当前操作");
            NotifyState(); return InteractionResult.Accept("正在使用：" + GameplayConfigService.GetDevice(deviceId)?.comment);
        }
        public void ClickGrinder() => FindFirstObjectByType<CoffeeWorldRuntime>()?.UseCurrentDeviceFromButton();
        private void BeginTimedStep(RecipeStep step)
        {
            TimedDeviceId = step.DeviceId; TimedStepDuration = GetDeviceTime(step.DeviceId, step.DurationSec > 0 ? step.DurationSec : 5); TimedStepProgress = 0;
        }
        public void CompleteBeanSelection(bool success, int score) { Session?.CompleteBeanSelection(success, score); NotifyState(); }
        public void CompleteCocoaQTE(bool success) { if (Session?.CurrentStep?.Kind != RecipeStepKind.CocoaMinigame) return; if (success) Session.CompleteCocoaMinigame(); else Session.CocoaMistake(); NotifyState(); }
        public void CompleteLatteArt(float score, string shape) { if (Session?.CurrentStep?.Kind != RecipeStepKind.LatteArt) return; CurrentLatteArtShape = shape; Session.SetLatteArtQuality(score); ShowToast($"拉花评分 {score:0}（{shape}）"); NotifyState(); }
        public void Serve() => FindFirstObjectByType<CoffeeWorldRuntime>()?.ServeFromButton();
        public void CompleteServe() { if (Session?.CurrentStep?.Kind == RecipeStepKind.Serve && Day.Phase == DayPhase.Running) Session.Serve(); }
        public void DiscardCurrentOrder()
        {
            if (CurrentOrder == null) return;
            Orders.MarkFailed(CurrentOrder); CleanupSession(); CurrentOrder = null; ShowToast("订单已放弃"); NotifyState();
        }
        public void RefillIngredient(string id)
        {
            var entry = GameplayConfigService.GetIngredient(id);
            if (entry == null) return;
            if (Profile.Reputation < entry.unlock_rep) { ShowToast("原料尚未解锁"); return; }
            if (Profile.GetStock(id) >= entry.max_stock) { ShowToast("库存已满"); return; }
            if (!Profile.CanAfford(entry.refill_cost)) { ShowToast("金币不足"); return; }
            Profile.Spend(entry.refill_cost); Profile.Refill(id, entry.max_stock); ShowToast("已补货：" + entry.comment); NotifyState();
        }
        public void UpgradeDevice(string id)
        {
            var entry = GameplayConfigService.GetDevice(id); if (entry == null) return;
            if (Profile.UpgradedDevices.ContainsKey(id)) { ShowToast("设备已升级"); return; }
            if (Profile.Reputation < entry.unlock_rep || Profile.SkillPoints < entry.upgrade_cost) { ShowToast("声望或技能点不足"); return; }
            Profile.SkillPoints -= entry.upgrade_cost; Profile.UpgradedDevices[id] = true; ShowToast("设备已升级：" + entry.comment);
            NarrativeServiceRegistry.Current.TriggerEvent(NarrativeEvents.DeviceUpgraded, new Dictionary<string, object> { { "deviceId", id } }); NotifyState();
        }
        public float GetDeviceTime(string id, float fallback) { var entry = string.IsNullOrEmpty(id) ? null : GameplayConfigService.GetDevice(id); return entry == null ? fallback : Profile.UpgradedDevices.ContainsKey(id) ? entry.upgraded_time : entry.base_time; }
        private void OnSessionStepChanged(RecipeStep step) { StepChanged?.Invoke(step); NotifyState(); }
        private void OnSessionFinished(CraftingStepResult result)
        {
            var order = CurrentOrder; var session = Session; CleanupSession();
            if (result == CraftingStepResult.Success && order != null)
            {
                int gold = Mathf.Max(1, Mathf.RoundToInt(order.Recipe.price * session.QualityMultiplier * SocialServiceRegistry.Current.OnOrderServed(order.CustomerId, session.QualityMultiplier, CurrentLatteArtShape ?? "", order.Recipe.price) * NarrativeServiceRegistry.Current.NarrativePriceModifier));
                Profile.Earn(gold); Profile.TodayRevenue += gold; Orders.MarkReady(order); Orders.MarkServed(order); ShowToast("完成订单 +" + gold + " 金币");
                var args = new Dictionary<string, object> { { "orderId", order.OrderId }, { "npcId", order.NpcId }, { "recipeId", order.RecipeId }, { "quality", session.QualityMultiplier }, { "goldEarned", gold } };
                NarrativeServiceRegistry.Current.TriggerEvent(NarrativeEvents.OrderCompleted, args);
                NarrativeServiceRegistry.Current.TriggerEvent(NarrativeEvents.CustomerServed, args);
            }
            else if (order != null) { Orders.MarkFailed(order); ShowToast("订单失败"); }
            CurrentOrder = null; NotifyState();
        }
        private void CleanupSession()
        {
            if (Session != null) { Session.OnStepChanged -= OnSessionStepChanged; Session.OnSessionFinished -= OnSessionFinished; Session.OnGrindProgress -= OnProgress; Session.OnCocoaMistake -= OnCocoaMistake; }
            Session = null; TimedStepProgress = -1; TimedDeviceId = null;
        }
        [Serializable] private sealed class ProfileSave
        {
            public int gold, reputation, skillPoints, totalOrders, todayOrders, todayReputation, todayRevenue, dayNumber, currentOrderId;
            public string[] stockIds, upgradedIds; public int[] stockCounts;
            public DayPhase phase; public float timeRemaining, timedProgress = -1, timedDuration;
            public string timedDevice, latteShape, summary;
            public CustomerOrder[] orders; public CraftingSnapshot session;
        }
        public string CaptureProfileJson()
        {
            var save = new ProfileSave { gold = Profile.Gold, reputation = Profile.Reputation, skillPoints = Profile.SkillPoints, totalOrders = Profile.TotalOrdersCompleted,
                todayOrders = Profile.TodayOrdersCompleted, todayReputation = Profile.TodayReputationDelta, todayRevenue = Profile.TodayRevenue, dayNumber = Day.DayNumber,
                phase = Day.Phase, timeRemaining = Day.TimeRemaining, orders = Orders.Orders.ToArray(), currentOrderId = CurrentOrder?.OrderId ?? 0,
                session = Session?.Capture(), timedProgress = TimedStepProgress, timedDuration = TimedStepDuration, timedDevice = TimedDeviceId, latteShape = CurrentLatteArtShape,
                summary = Profile.LastDaySummary, stockIds = Profile.IngredientStock.Keys.ToArray(), stockCounts = Profile.IngredientStock.Values.ToArray(), upgradedIds = Profile.UpgradedDevices.Keys.ToArray() };
            return JsonUtility.ToJson(save);
        }
        public void RestoreProfileJson(string json)
        {
            var save = JsonUtility.FromJson<ProfileSave>(json);
            if (save == null || save.stockIds != null && (save.stockCounts == null || save.stockIds.Length != save.stockCounts.Length)) throw new FormatException("无效经营存档");
            CleanupSession(); Profile.Gold = Mathf.Max(0, save.gold); Profile.Reputation = Mathf.Max(0, save.reputation); Profile.SkillPoints = Mathf.Max(0, save.skillPoints);
            Profile.TotalOrdersCompleted = save.totalOrders; Profile.TodayOrdersCompleted = save.todayOrders; Profile.TodayReputationDelta = save.todayReputation; Profile.TodayRevenue = save.todayRevenue; Profile.LastDaySummary = save.summary ?? "";
            Profile.IngredientStock.Clear(); foreach (var entry in GameplayConfigService.AllIngredients) Profile.IngredientStock[entry.id] = entry.max_stock;
            if (save.stockIds != null) for (int i = 0; i < save.stockIds.Length; i++) if (GameplayConfigService.GetIngredient(save.stockIds[i]) is { } entry) Profile.IngredientStock[entry.id] = Mathf.Clamp(save.stockCounts[i], 0, entry.max_stock);
            Profile.UpgradedDevices.Clear(); if (save.upgradedIds != null) foreach (var id in save.upgradedIds) if (GameplayConfigService.GetDevice(id) != null) Profile.UpgradedDevices[id] = true;
            Orders.Restore(save.orders); Day.Restore(save.phase, save.timeRemaining, save.dayNumber); CurrentOrder = Orders.Orders.FirstOrDefault(o => o.OrderId == save.currentOrderId);
            if (CurrentOrder != null && save.session != null && save.phase == DayPhase.Running) { Session = new CraftingSession(CurrentOrder, Profile); Session.Restore(save.session); HookSession(); }
            TimedStepProgress = Session != null ? save.timedProgress : -1; TimedStepDuration = save.timedDuration; TimedDeviceId = save.timedDevice; CurrentLatteArtShape = save.latteShape; NotifyState();
        }
    }
}
