using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Explicit, reviewable test inputs. No preset grants a terminal/ending fact.</summary>
    [Serializable]
    public sealed class NarrativeAutoPreset
    {
        public string id, targetEndingId, route;
        public bool historicalScenario;
        public int startDay = 1, maxDay = 65, attribute = 6, language = 6, courage = 6, variant;
        public string startEvent;
        public string[] history = Array.Empty<string>();
        public string[] preferredChoices = Array.Empty<string>();
    }

    public static class NarrativeAutoPresets
    {
        public static List<NarrativeAutoPreset> Create(NarrativeConfiguration config, int variants = 3, bool includeHistoricalScenarios = true)
        {
            var result = new List<NarrativeAutoPreset>();
            foreach (var ending in config.endings.Where(e => e.enabled).OrderBy(e => e.endingId, StringComparer.Ordinal))
            {
                for (int variant = 0; variant < Math.Max(1, Math.Min(8, variants)); variant++)
                    result.Add(Make(ending.endingId, variant, false));
            }
            if (includeHistoricalScenarios)
                foreach (var ending in config.endings.Where(e => e.enabled).OrderBy(e => e.endingId, StringComparer.Ordinal))
                    if (ending.endingId != "arrival.ending.if0") result.Add(Make(ending.endingId, 0, true));
            return result;
        }

        public static NarrativeAutoPreset Make(string ending, int variant, bool historical)
        {
            string route = ending.StartsWith("neutral.", StringComparison.Ordinal) ? "neutral"
                : ending.StartsWith("rose.", StringComparison.Ordinal) ? "rose"
                : ending.StartsWith("church.", StringComparison.Ordinal) ? "church"
                : ending.StartsWith("revolution.", StringComparison.Ordinal) ? "revolution" : "";
            var p = new NarrativeAutoPreset { id = ending + (historical ? "/historical" : "/campaign-" + variant), targetEndingId = ending,
                route = route, historicalScenario = historical, variant = variant,
                attribute = variant == 1 ? 3 : variant == 2 ? 18 : 6, language = variant == 1 ? 3 : 6, courage = variant == 1 ? 3 : 18 };
            var choices = new List<string> { "MA70.10.06-S1-2.2" };
            switch (ending)
            {
                case "neutral.ending.a": Set(p, "neutral.route.1111.s1", 37, "route.neutral"); break;
                case "neutral.ending.b": Set(p, "neutral.route.1122.s1", 48, "route.neutral", "story.neutral.route.1118.s1.completed", "neutral.investigation.shelter"); choices.Add("neutral.route.1122.s1.choice.future.b"); break;
                case "neutral.ending.c": Set(p, "neutral.route.1130.s2", 56, "route.neutral", "story.neutral.route.1122.s1.completed", "neutral.stayed"); break;
                case "neutral.ending.d": Set(p, "neutral.route.1130.s1", 56, "route.neutral", "story.neutral.route.1129.s1.completed", "neutral.association.committee", "neutral.stayed"); break;
                case "rose.ending.trust_limit.ending": Set(p, "rose.ending.trust_limit", 21, "story.rose.ending.trust_limit.pending"); break;
                case "rose.ending.job_requirements.ending": Set(p, "rose.ending.job_requirements", 27, "story.rose.ending.job_requirements.pending"); break;
                case "rose.route.1115.s1.ending.ideal_world": Set(p, "rose.route.1115.s1", 41, "route.rose", "story.rose.route.1111.s2.completed"); break;
                case "rose.route.ending.unforgiven.ending": Set(p, "rose.route.ending.unforgiven", 48, "route.rose", "story.rose.route.ending.unforgiven.pending"); break;
                case "rose.route.ending.bloom.ending": Set(p, "rose.route.ending.bloom", 48, "route.rose", "story.rose.route.ending.bloom.pending"); break;
                case "church.ending.if2": Set(p, "church.1023.s2", 18, "story.church.1023.s1.completed", "church.detention.release_failed", "church.detention.active"); break;
                case "church.ending.if4": Set(p, "church.1111.s1", 37, "story.revolution.1107.s1.completed"); choices.AddRange(new[] { "church.1111.s1.choice.s1.2", "church.1111.s1.choice.s2.2" }); p.language = 3; break;
                case "church.ending.ry1": Set(p, "church.route.1123.s1", 49, "route.church", "story.church.route.1118.s1.completed", "church.thibaut.followed"); break;
                case "church.ending.ry2": Set(p, "church.route.1123.s1", 49, "route.church", "story.church.route.1118.s1.completed"); break;
                case "revolution.end01": Set(p, "revolution.route.1124.s1", 50, "route.revolution", "story.revolution.route.1123.s1.completed"); choices.Add("revolution.route.1124.s1.c0.1"); break;
                case "revolution.end02": Set(p, "revolution.route.1124.s1", 50, "route.revolution", "story.revolution.route.1123.s1.completed"); choices.AddRange(new[] { "revolution.route.1124.s1.c0.2", "revolution.route.1124.s1.c1.1", "revolution.route.1124.s1.c2.1" }); break;
            }
            if (!historical) { p.startDay = 1; p.history = Array.Empty<string>(); p.startEvent = "arrival.start"; }
            p.preferredChoices = Preferred(ending);
            // Explicit test seeds, not claims about legal character creation or player balance.
            p.attribute = variant == 1 ? 6 : 20;
            p.courage = ending == "church.ending.if2" ? 0 : 20;
            p.language = ending == "church.ending.if4" ? 0 : variant == 2 ? 4 : 20;
            return p;
        }
        private static string[] Preferred(string ending)
        {
            if (ending == "arrival.ending.if0") return new[] { "MA70.10.06-S1-2.1", "MA70.10.06-S1-3.2" };
            var c = new List<string> { "MA70.10.06-S1-2.2", "rose.1011.s1.choice.p062", "rose.1011.s1.choice.p080",
                "rose.1016.s1.choice.p047", "rose.1016.s1.choice.p064", "rose.1016.s1.choice.p065.topics.64",
                "rose.1016.s1.choice.p066.topics.64_65", "rose.1016.s1.choice.p091", "church.1020.s1.choice.search.1",
                "church.1020.s1.choice.explain.2", "revolution.1014.s1.c4.3", "revolution.1014.s1.c5.1", "revolution.1107.s1.c2.3" };
            if (ending == "church.ending.if2") { c.Add("church.1023.s1.choice.release.3"); return c.ToArray(); }
            c.Add(ending == "rose.ending.trust_limit.ending" ? "rose.1024.s1.choice.p128" : "rose.1024.s1.choice.p127");
            c.Add(ending == "rose.ending.job_requirements.ending" ? "revolution.1029.s1.c3.2" : "revolution.1029.s1.c3.1");
            c.Add("rose.1103.s1.choice.p051");
            if (ending == "neutral.ending.a") c.Add("church.1111.s1.choice.s1.1");
            else
            {
                c.AddRange(new[] { "church.1111.s1.choice.s1.2", "church.1111.s1.choice.s2.2" });
                bool neutral = ending.StartsWith("neutral.", StringComparison.Ordinal);
                c.Add(neutral ? "church.1111.s1.choice.s3.2" : "church.1111.s1.choice.s3.1");
                if (!neutral) c.Add(ending.StartsWith("rose.", StringComparison.Ordinal) ? "church.1111.s1.choice.s4.2"
                    : ending.StartsWith("revolution.", StringComparison.Ordinal) ? "church.1111.s1.choice.s4.3" : "church.1111.s1.choice.s4.1");
            }
            c.AddRange(new[] { "neutral.route.1118.s1.choice.investigation.b",
                ending == "neutral.ending.d" ? "neutral.route.1112.s1.choice.membership.a" : "neutral.route.1112.s1.choice.membership.b",
                ending == "neutral.ending.b" ? "neutral.route.1122.s1.choice.future.b" : "neutral.route.1122.s1.choice.future.a",
                ending == "rose.route.ending.unforgiven.ending" ? "rose.route.1122.s1.choice.p063" : "rose.route.1122.s1.choice.p064",
                ending == "church.ending.ry1" ? "church.route.1115.s1.choice.follow_thibaut.1" : "church.route.1115.s1.choice.follow_thibaut.2",
                "revolution.route.1123.s1.shoot.1", ending == "revolution.end01" ? "revolution.route.1124.s1.c0.1" : "revolution.route.1124.s1.c0.2",
                "revolution.route.1124.s1.c1.1", "revolution.route.1124.s1.c2.1" });
            return c.ToArray();
        }
        private static void Set(NarrativeAutoPreset p, string root, int day, params string[] history)
        { p.startEvent = root; p.startDay = day; p.history = history; }
    }

    [Serializable]
    public sealed class NarrativeAutoRunResult
    {
        public string presetId, targetEndingId, actualEndingId, status, reason, evidence, startedUtc, finishedUtc;
        public int day, steps, cupsDelivered, ordinaryOrders, narrativeDeliveries;
        public bool historicalScenario;
        public string screenshot, screenshotError, review = "pending", configurationHash;
        public List<string> log = new List<string>();
        public List<NarrativeStringValue> finalValues = new List<NarrativeStringValue>();
        public List<string> events = new List<string>();
        public List<string> trueConditions = new List<string>();
        public List<string> blockers = new List<string>();
    }

    /// <summary>
    /// An isolated real Controller + CafeWorld, never NarrativeRuntime.Instance/PlayerCafeOwner.
    /// The host replaces presentation waits with budgeted ticks, not production conditions/results.
    /// </summary>
    public sealed class NarrativeAutoSession : IDisposable
    {
        public NarrativeConfiguration Configuration { get; }
        public NarrativeAutoPreset Preset { get; }
        public NarrativeController Controller { get; }
        public NarrativeActionController Actions { get; }
        public NarrativeMapInventory Inventory { get; }
        public CafeWorld World { get; }
        public NarrativeAutomationPresenter Presenter { get; }
        public NarrativeAutoRunResult Result { get; }
        public int Day { get; private set; }
        public string Phase => phase.ToString();
        public bool Finished { get; private set; }
        private enum Stage { Opening, Morning, Business, AfterBusiness, Actions, Night, Advance }
        private Stage phase;
        private readonly CafeNarrativeValueProvider money;
        private readonly DelegateNarrativeValueProvider calendar, points;
        private readonly HashSet<string> actionsToday = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> clicked = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> knownConditions = new Dictionary<string, bool>(StringComparer.Ordinal);
        private NarrativeCafeGameplayBridge service;
        private bool startedStage, disposed;
        private int businessTicks, actionsTaken;
        private const int MaxSteps = 100000;
        public event Action<string> Logged;

        public NarrativeAutoSession(NarrativeConfiguration configuration, CafeCatalog catalog, NarrativeAutoPreset preset)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Preset = preset ?? throw new ArgumentNullException(nameof(preset));
            Day = preset.startDay;
            Result = new NarrativeAutoRunResult { presetId = preset.id, targetEndingId = preset.targetEndingId,
                historicalScenario = preset.historicalScenario, startedUtc = DateTime.UtcNow.ToString("O"), status = "running",
                evidence = preset.historicalScenario ? "assisted-historical-scenario" : "assisted-campaign-from-day1" };
            World = new CafeWorld(catalog); World.EnableProduction(); World.SynchronizeCalendarDay(Day);
            World.AutomationUnlockAndRefill();
            var plan = NarrativeAutomationPlan.ForEnding(configuration, preset.targetEndingId);
            Presenter = new NarrativeAutomationPresenter(configuration, plan);
            Controller = new NarrativeController(configuration, Presenter); Presenter.Bind(Controller);
            Inventory = new NarrativeMapInventory(); Controller.Inventory = Inventory;
            Actions = new NarrativeActionController(Controller, configuration.actions, 6, Inventory);
            foreach (var position in configuration.mapPositions.Where(p => p.enabled))
            {
                if (!string.IsNullOrEmpty(position.defaultAction)) Actions.Mount(position.positionKey, position.defaultAction);
                foreach (string action in position.extendedActions) Actions.Mount(position.positionKey, action);
            }
            money = new CafeNarrativeValueProvider(World);
            calendar = new DelegateNarrativeValueProvider("dayflow", key => key == "day" ? Day : (int?)null);
            points = new DelegateNarrativeValueProvider("actions", key => key == "current" ? Actions.ActionPoints.Current : key == "max" ? Actions.ActionPoints.Max : (int?)null);
            Controller.BeginValueRestore();
            Controller.BindValueProvider(money); Controller.BindValueProvider(calendar); Controller.BindValueProvider(points);
            Controller.MoneyAccount = money;
            Actions.ActionPoints.Changed += OnPoints;
            Controller.AbilityReader = key => Controller.State.TryGetBar("player." + key, out int n) ? (true, n) : (false, 0);
            Controller.TextResolver = ResolveText;
            Controller.SetOfficialDay(Day);
            foreach (string key in new[] { "language", "courage", "wisdom", "perception" })
                if (Controller.State.IsWritableBar("player." + key)) Controller.SetBar("player." + key, key == "language" ? preset.language : key == "courage" ? preset.courage : preset.attribute);
            foreach (var seed in new[] { (key: "alienor.affection", value: 20), (key: "faction.revolution", value: preset.targetEndingId == "church.ending.if2" ? -1 : 0) })
                if (Controller.State.IsWritableBar(seed.key)) Controller.SetBar(seed.key, seed.value);
            if (!World.TryInitializeMoney(10000, out string error)) throw new InvalidOperationException(error);
            foreach (string key in preset.history)
            {
                if (key.StartsWith("construction.", StringComparison.Ordinal) || key.StartsWith("blocked.", StringComparison.Ordinal) || key.StartsWith("integration.", StringComparison.Ordinal))
                    throw new InvalidOperationException("Preset cannot unlock author gates: " + key);
                Controller.State.SetCondition(key, true);
            }
            foreach (var pair in Controller.State.Conditions) knownConditions[pair.Key] = pair.Value;
            Controller.CompleteValueRestore(false);
            Actions.RefreshForDay(Day);
            Controller.SetEndingChecksEnabled(true);
            Controller.EventCompleted += OnEventCompleted;
            Controller.EventFailed += OnEventFailed;
            Controller.BarChanged += OnBarChanged;
            Controller.AbilityCheckEvaluated += check => Log("检定 " + check.checkId + "=" + check.passed);
            Controller.State.ConditionsChanged += OnConditions;
            Presenter.GameplayRequested += OnGameplay;
            Presenter.LogAdded += Log;
            Presenter.ChoiceSelector = SelectChoice;
            Log("后台辅助：初始资金10000、属性预设、6AP/日、设备与配方解锁、免费原料、按领域评价交付；不验证手动制作或数值平衡。");
            if (preset.historicalScenario) Log("历史场景注入（非开局可达证据）：" + string.Join(",", preset.history));
            phase = Stage.Opening;
        }

        public string ResolveText(string key) => Configuration.texts.Find(t => t.key == key)?.content ?? key ?? "";
        private void OnPoints(int before, int after) => points.NotifyChanged();
        private void OnEventCompleted(string id) { Result.events.Add(id); Log("事件完成 " + id); }
        private void OnEventFailed(string id, string error) => Stop("blocked", id + ": " + error
            + (string.IsNullOrEmpty(Presenter.LastError) ? "" : " | " + Presenter.LastError));
        private void OnBarChanged(string key, int before, int after) => Log(key + " " + before + " → " + after);
        private void OnConditions()
        {
            foreach (var pair in Controller.State.Conditions)
                if (!knownConditions.TryGetValue(pair.Key, out bool old) || old != pair.Value)
                { knownConditions[pair.Key] = pair.Value; Log("条件 " + pair.Key + "=" + pair.Value); }
        }
        private void Log(string line)
        {
            if (Result.log.Count >= 30000) return;
            string entry = "D" + Day + "/" + phase + " " + line;
            Result.log.Add(entry); Logged?.Invoke(entry);
        }

        // Explicit overrides win; the shared plan's semantic scoring handles the remaining groups.
        private string SelectChoice(NarrativeAutomationChoiceContext context)
        {
            var available = context.Choices;
            foreach (string id in Preset.preferredChoices) if (available.Any(c => c.choiceId == id)) return id;
            return null; // Presenter falls back to the target-aware plan.
        }

        public void Tick(int budget = 32)
        {
            if (Finished || disposed) return;
            try
            {
                for (int i = 0; i < Math.Max(1, Math.Min(budget, 256)) && !Finished; i++)
                {
                    if (++Result.steps > MaxSteps) { Stop("budget-exhausted", "运行步数达到上限，不据此判定不可达。"); break; }
                    Presenter.Tick(8);
                    if (Controller.IsEnded) { Stop("ending", Controller.SelectedEndingId == Preset.targetEndingId ? "已达目标结局" : "实际抵达其他结局"); break; }
                    if (Finished) break;
                    if (Presenter.IsAwaitingGameplay)
                    {
                        var delivery = World.AutomationServePendingOrders(16);
                        RecordDelivery(delivery);
                        if (!string.IsNullOrEmpty(delivery.StopReason) || delivery.Delivered == 0 && !World.HasNarrativeService)
                            Stop("host-blocked", delivery.StopReason ?? "咖啡宿主未产生可消费的交付回执。");
                        continue;
                    }
                    if (PumpVisits()) continue;
                    Controller.FlushValueChanges();
                    Controller.CheckEndingsAtSafeBoundary();
                    if (Controller.IsEnded || Finished) continue;
                    if (Controller.IsBusy || Presenter.IsPresenting || Actions.IsExecuting) continue;
                    if (phase == Stage.Opening)
                    {
                        phase = Preset.historicalScenario ? Stage.Advance : Stage.Morning;
                        if (!string.IsNullOrEmpty(Preset.startEvent))
                        {
                            var root = Configuration.events.Single(e => e.eventId == Preset.startEvent);
                            string blocker = Controller.ConstructionBlocker(root);
                            if (!string.IsNullOrEmpty(blocker)) { Stop("content-blocked", root.eventId + ": " + blocker); continue; }
                            var date = Configuration.eventDates.FirstOrDefault(d => d.eventId == root.eventId);
                            if (date != null)
                            {
                                var entry = date.timeSlot == NarrativeTimeSlot.Morning ? NarrativeEntryPoint.MorningBeforeMap
                                    : date.timeSlot == NarrativeTimeSlot.Noon ? NarrativeEntryPoint.AfterBusiness : NarrativeEntryPoint.BeforeNight;
                                if (!Controller.ProcessEntry(entry)) Stop("blocked", "历史场景的正式日期阶段入口拒绝：" + entry);
                            }
                            else if (root.entry == NarrativeEntryPoint.Immediate)
                            { if (!Controller.QueueEvent(root.eventId)) Stop("blocked", Controller.ExplainEvent(root.eventId).ToString()); }
                            else if (root.entry == NarrativeEntryPoint.MapAction)
                            { if (!Controller.QueueMapActionEvent(root.eventId)) Stop("blocked", Controller.ExplainMapActionEvent(root.eventId).ToString()); }
                            else Controller.ProcessEntry(root.entry);
                        }
                    }
                    else if (Preset.historicalScenario && phase == Stage.Advance)
                        Stop("no-ending", "历史起点的实际剧情结算未产生结局；未授予任何结局事实。");
                    else StepDay();
                }
            }
            catch (Exception error) { Stop("error", error.GetType().Name + ": " + error.Message); }
        }

        private void StepDay()
        {
            switch (phase)
            {
                case Stage.Morning:
                    World.SynchronizeCalendarDay(Day);
                    Controller.SetOfficialDay(Day);
                    if (!startedStage) { startedStage = true; Controller.ProcessOfficialDay(Day); return; }
                    Next(Stage.Business); Controller.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap); break;
                case Stage.Business:
                    if (IsDetained) { Next(Stage.AfterBusiness); break; }
                    if (!startedStage)
                    {
                        startedStage = true; World.AutomationUnlockAndRefill();
                        if (Finished || Controller.IsEnded) break;
                        var result = World.StartDay(false); Log("营业 " + result.message);
                        if (!result.success) { Stop("blocked", result.message); break; }
                    }
                    if (PumpVisits()) break;
                    var delivered = World.AutomationServePendingOrders(16);
                    RecordDelivery(delivered);
                    if (!string.IsNullOrEmpty(delivered.StopReason)) { Stop("host-blocked", delivered.StopReason); break; }
                    // Let served/leave hooks settle through the controller before closing business.
                    if (delivered.Delivered > 0 || PumpVisits()) break;
                    bool delayedVisit = World.State.cafeVisits.Any(v => v.arrivalAuthorized && v.status != CafeVisitStatus.Ended
                        && v.status <= CafeVisitStatus.WaitingForSeat);
                    if (++businessTicks <= 120 && (World.State.todayArrivals < World.State.todayCustomerBudget || delayedVisit))
                    { World.Tick(Math.Min(World.Catalog.customerInterval, 5)); break; }
                    if (delayedVisit) { Stop("host-blocked", "剧情顾客在有界营业等待内未能入座/到店，未自动跳过。"); break; }
                    if (World.State.requests.Count > 0) { Stop("host-blocked", "仍有订单无法后台交付。"); break; }
                    if (World.State.open) World.EndDay();
                    Log("打烊 收入=" + World.State.todayRevenue + " 成功=" + World.State.todayServed);
                    if (Finished || Controller.IsEnded) break;
                    Next(Stage.AfterBusiness); break;
                case Stage.AfterBusiness:
                    SetMapActive(true); Next(Stage.Actions); Controller.ProcessEntry(NarrativeEntryPoint.AfterBusiness); break;
                case Stage.Actions:
                    if (!IsDetained && TryAction()) break;
                    if (!IsDetained && Actions.HasBlockingActions())
                    { Stop("blocked", "地图必须行动无法完成：" + string.Join(";", Actions.GetBlockingPositions())); break; }
                    SetMapActive(false); Next(Stage.Night); break;
                case Stage.Night:
                    Next(Stage.Advance); Controller.ProcessEntry(NarrativeEntryPoint.BeforeNight); break;
                case Stage.Advance:
                    if (Day >= Preset.maxDay) { Stop("no-ending", "已到预设最后一天；未找到目标路线，不等于证明不可达。"); break; }
                    Day++; calendar.NotifyChanged(); Controller.SetOfficialDay(Day); Actions.BeginDay(Day, 6);
                    actionsToday.Clear(); actionsTaken = businessTicks = 0; Next(Stage.Morning); Log("进入新一天"); break;
            }
        }
        private bool IsDetained => Controller.State.TryGetCondition("church.detention.active", out bool value) && value;
        private void SetMapActive(bool active)
        { if (Controller.State.TryGetCondition("world.map.active", out _)) Controller.State.SetCondition("world.map.active", active && !IsDetained); }
        private void Next(Stage stage) { phase = stage; startedStage = false; }
        private bool TryAction()
        {
            if (actionsTaken >= 16) return false;
            var candidates = Configuration.mapPositions.Where(p => p.enabled)
                .SelectMany(p => Actions.GetDisplayableActions(p.positionKey).Select(a => (position: p.positionKey, action: a)))
                .Where(x => !x.action.endDay && !x.action.actionKey.StartsWith("act_test_", StringComparison.Ordinal)
                    && (!actionsToday.Contains(x.action.actionKey) || Actions.IsDailyPoolAction(x.action.actionKey)))
                .GroupBy(x => x.action.actionKey).Select(g => g.First())
                .OrderByDescending(x => x.action.mustExecute).ThenByDescending(x => x.action.actionKey.StartsWith(Preset.route + ".", StringComparison.Ordinal))
                .ThenBy(x => x.action.actionKey, StringComparer.Ordinal).ToArray();
            foreach (var candidate in candidates)
            {
                Controller.Location = candidate.position;
                if (!Actions.IsAvailable(candidate.action.actionKey, out _)) continue;
                actionsTaken++; actionsToday.Add(candidate.action.actionKey);
                Log("地图行动 " + candidate.position + "/" + candidate.action.actionKey);
                return Actions.Execute(candidate.action.actionKey);
            }
            return false;
        }
        private void RecordDelivery(CafeAutomationResult result)
        {
            Result.cupsDelivered += result.Delivered; Result.ordinaryOrders += result.OrdinaryOrdersServed; Result.narrativeDeliveries += result.NarrativeDeliveries;
            if (result.Delivered > 0) Log("后台制作并交付 " + result.Delivered + " 杯（普通=" + result.OrdinaryOrdersServed + ",剧情=" + result.NarrativeDeliveries + "）");
            if (!string.IsNullOrEmpty(result.StopReason)) Log("制作提示 " + result.StopReason);
        }
        private void OnGameplay(NarrativeAutomationGameplayRequest request)
        {
            service?.Dispose(); service = null;
            if (request.Data.gameplayKey == "host:end_business")
            {
                if (World.State.open) World.EndDay();
                Log("宿主命令：真实营业关闭"); request.Complete(new NarrativeGameplayResult { passed = true }); return;
            }
            if (!NarrativeCafeGameplayBridge.TryStart(World, "auto:" + Preset.id + ":" + Result.steps, request.Data.gameplayKey,
                result => { Log("咖啡交付回执 passed=" + result.passed + " cancelled=" + result.cancelled); request.Complete(result); }, out service, out string error, NarrativeCafeCustomers.Resolve(request.Data.pageId, LubanNarrativeLoader.Load())))
                throw new InvalidOperationException("后台咖啡宿主拒绝 " + request.Data.gameplayKey + ": " + error);
        }
        private bool PumpVisits()
        {
            var snapshot = Controller.GetCommittedCafeVisits();
            if (snapshot == null || !snapshot.IsReady) return false;
            var sync = World.SyncCafeVisits(snapshot.Visits.Select(v => new CafeVisitIntent { visitInstanceId = v.VisitInstanceId,
                characterId = v.CharacterId, visitKey = v.VisitKey, preferredSeatId = v.PreferredSeatId, seatedVisualPath = v.SeatedVisualPath,
                arrivalAuthorized = v.ArrivalAuthorized, isEnded = v.IsEnded, endReason = v.EndReason, arrivalDelaySeconds = v.ArrivalDelaySeconds,
                hasPatienceOverride = v.PatienceSeconds.HasValue, patienceSeconds = v.PatienceSeconds ?? 0,
                leaveAfterServed = v.LeaveAfterServed, usesDefaultConfiguration = v.UsesDefaultConfiguration,
                identity = new CafePersonIdentity { personId = v.CharacterId, characterId = v.CharacterId, narrativeCharacterId = v.CharacterId,
                    displayName = v.CharacterId, visualId = "customer", role = CafePersonRole.Customer } }).ToList());
            if (!sync.success) { Stop("host-blocked", sync.message); return true; }
            foreach (var fact in World.State.cafeVisitFeedback.ToArray())
            {
                var dto = new NarrativeCafeVisitFeedback { visitInstanceId = fact.visitInstanceId, characterId = fact.characterId,
                    stage = fact.stage, reason = fact.reason, sequence = fact.sequence, isClick = fact.isClick };
                Controller.SubmitCafeVisitFeedback(dto);
                if (Controller.IsCafeVisitFeedbackComplete(dto)) World.AcknowledgeCafeVisitFeedback(fact.sequence);
            }
            // Background interaction request, not a made-up fact; the domain validates the stage.
            if (!Controller.IsBusy)
                foreach (var visit in World.State.cafeVisits.ToArray())
                {
                    string key = visit.visitInstanceId + "/" + visit.status;
                    if ((visit.status == CafeVisitStatus.SeatedBeforeServe || visit.status == CafeVisitStatus.Served) && !clicked.Contains(key))
                    {
                        var requested = World.RequestCafeVisitClick(visit.visitInstanceId);
                        if (requested.success) { clicked.Add(key); return true; }
                    }
                }
            return Controller.IsBusy || World.State.cafeVisitFeedback.Count > 0;
        }
        public void Stop(string status, string reason)
        {
            if (Finished) return;
            Finished = true; Result.status = status; Result.reason = reason; Result.actualEndingId = Controller.SelectedEndingId;
            Result.day = Day; Result.finishedUtc = DateTime.UtcNow.ToString("O");
            foreach (var p in Controller.State.Bars) Result.finalValues.Add(new NarrativeStringValue { key = p.Key, value = p.Value.ToString() });
            Result.trueConditions = Controller.State.Conditions.Where(p => p.Value).Select(p => p.Key).OrderBy(p => p, StringComparer.Ordinal).ToList();
            foreach (var e in Configuration.events)
                if (!string.IsNullOrEmpty(Controller.ConstructionBlocker(e))) Result.blockers.Add(e.eventId + ": " + Controller.ConstructionBlocker(e));
            foreach (var key in new[] { "route.eligible.church", "route.eligible.rose", "route.eligible.revolution" })
                if (Controller.State.TryGetCondition(key, out bool ready) && !ready
                    && !Configuration.results.Any(r => r.resultType == NarrativeResultType.SetFlag && r.target == key && r.value == "true"))
                    Result.blockers.Add(key + ": 初始false且没有结果生产者；本工具未自动授予阵营资格。");
            Log("停止 " + status + ": " + reason);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            service?.Dispose(); Presenter.LogAdded -= Log; Presenter.Dispose(); Actions.ActionPoints.Changed -= OnPoints;
            Controller.EventCompleted -= OnEventCompleted; Controller.EventFailed -= OnEventFailed; Controller.BarChanged -= OnBarChanged;
            Controller.State.ConditionsChanged -= OnConditions; Controller.UnbindValueProviders(); money.Dispose();
        }
    }
}
