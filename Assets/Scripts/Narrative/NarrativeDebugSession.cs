using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Spotlight.Narrative
{
    /// <summary>Explicitly seeded test values. Unknown keys remain unavailable, never an implicit success.</summary>
    public sealed class NarrativeDebugValueProvider : INarrativeValueProvider
    {
        private readonly Dictionary<string, int> values = new Dictionary<string, int>(StringComparer.Ordinal);
        public string ProviderId { get; }
        public bool IsReady { get; set; } = true;
        public event Action Changed;
        public NarrativeDebugValueProvider(string id) { ProviderId = id; }
        public bool TryRead(string key, out int value) { value = 0; return IsReady && key != null && values.TryGetValue(key, out value); }
        public void Set(string key, int value, bool notify = false) { values[key] = value; if (notify) Changed?.Invoke(); }
        internal Dictionary<string, int> Capture() => new Dictionary<string, int>(values, StringComparer.Ordinal);
        internal void Restore(Dictionary<string, int> copy) { values.Clear(); foreach (var p in copy) values.Add(p.Key, p.Value); }
    }

    /// <summary>Owns only independent memory. Never accesses or replaces NarrativeRuntime.Instance.</summary>
    public sealed class NarrativeDebugSession : IDisposable, INarrativeMoneyAccount
    {
        private sealed class Presenter : INarrativePresenter, INarrativePresentationCheckpoints, INarrativePresentationCancellation
        {
            public NarrativeDebugSession Owner;
            public INarrativePresenter Inner;
            public event Action<NarrativeEventRequest, string, NarrativeCheckpointWait> CheckpointReached;
            public event Action<string> Cancelled;
            public void ForwardCancellation(string reason)
            {
                if (Owner.disposed) return;
                Owner.epoch++; Owner.completion = null; Owner.CurrentRequest = default;
                Cancelled?.Invoke(reason);
            }
            public void ForwardCheckpoint(NarrativeEventRequest request, string page, NarrativeCheckpointWait wait)
            {
                if (Owner.disposed || Owner.CurrentRequest.RequestId != request.RequestId) return;
                Owner.epoch++; Owner.completion = null; Owner.CurrentRequest = default;
                CheckpointReached?.Invoke(request, page, wait);
            }
            public void Play(NarrativeEventRequest request, Action completed)
            {
                Owner.CurrentRequest = request;
                long token = Owner.epoch;
                bool consumed = false;
                Action guarded = () => { if (consumed || Owner.disposed || token != Owner.epoch) return; consumed = true; Owner.completion = null; Owner.CurrentRequest = default; completed(); };
                Owner.completion = guarded;
                Owner.log.Add("演出 " + request.Event.eventId);
                Inner?.Play(request, guarded);
            }
        }
        private readonly Dictionary<string, NarrativeDebugValueProvider> providers = new Dictionary<string, NarrativeDebugValueProvider>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> abilities = new Dictionary<string, int>(StringComparer.Ordinal);
        private Dictionary<string, Dictionary<string, int>> baselineProviders;
        private Dictionary<string, int> baselineAbilities;
        private string baselineJson;
        private bool disposed, editing = true;
        private long epoch;
        private Action completion;
        private readonly Presenter bridge;
        private readonly List<string> log = new List<string>();
        public NarrativeConfiguration Configuration { get; }
        public NarrativeController Controller { get; }
        public NarrativeActionController Actions { get; }
        public NarrativeMapInventory Inventory { get; }
        public NarrativeEventRequest CurrentRequest { get; private set; }
        public IReadOnlyList<string> Log => log;
        public bool HasBaseline => baselineJson != null;
        public string PreviewName { get; set; } = "测试玩家";

        public NarrativeDebugSession(NarrativeConfiguration configuration = null, INarrativePresenter presenter = null)
        {
            Configuration = configuration ?? LubanNarrativeLoader.Load();
            bridge = new Presenter { Owner = this, Inner = presenter };
            if (presenter is INarrativePresentationCheckpoints checkpoints) checkpoints.CheckpointReached += bridge.ForwardCheckpoint;
            if (presenter is INarrativePresentationCancellation cancellation) cancellation.Cancelled += bridge.ForwardCancellation;
            Controller = new NarrativeController(Configuration, bridge);
            Inventory = new NarrativeMapInventory();
            Controller.Inventory = Inventory;
            Actions = new NarrativeActionController(Controller, Configuration.actions, 5, Inventory);
            Controller.BeginValueRestore();
            foreach (var bar in Configuration.bars.Where(b => b.sourceMode == NarrativeSourceMode.External))
            {
                if (!providers.TryGetValue(bar.providerId, out var provider))
                {
                    provider = new NarrativeDebugValueProvider(bar.providerId); providers.Add(bar.providerId, provider); Controller.BindValueProvider(provider);
                }
                // Generated defaults are explicit test inputs, not values borrowed from the formal host.
                provider.Set(bar.sourceKey, bar.providerId == "dayflow" && bar.sourceKey == "day" ? Math.Max(1, bar.initValue) : bar.initValue);
            }
            foreach (string id in new[] { "language", "courage", "wisdom", "perception" }) abilities[id] = 0;
            Controller.AbilityReader = id => abilities.TryGetValue(id, out int value) ? (true, value) : (false, 0);
            Controller.TextResolver = ResolveText;
            Controller.MoneyAccount = this;
            Controller.ResultDependencyValidator = ValidateResult;
            Controller.EventFailed += (id, error) => log.Add("未验证/失败 " + id + ": " + error);
            Controller.EventCompleted += id => log.Add("已验证NCS结算 " + id);
            Controller.AbilityCheckEvaluated += check => log.Add("检定 " + check.checkId + " = " + check.passed + ": " + string.Join(", ", check.values.Select(v => v.attributeId + " " + v.currentValue + "/" + v.threshold)));
            Controller.BarChanged += (key, before, after) => log.Add("数值 " + key + " " + before + " → " + after);
            Controller.EffectRequested += e => log.Add((IsHostResult(e.Result.resultType) ? "仅记录宿主请求 " : "已验证结果 ") + e.Result.resultId + " " + e.Result.resultType + " " + e.Target + "=" + e.Value);
            Controller.EndingSelected += ending => log.Add("仅记录结局请求（未调用正式宿主）");
            SyncActionValues();
            Actions.StateChanged += SyncActionValues;
            log.Add("独立分支测试；外部值使用生成配置默认值；请显式建立测试起点。");
        }

        public void SetPresenter(INarrativePresenter presenter)
        {
            EnsureAlive();
            if (CurrentRequest.Event != null) throw new InvalidOperationException("先恢复测试起点再替换演出器。");
            if (bridge.Inner is INarrativePresentationCheckpoints old) old.CheckpointReached -= bridge.ForwardCheckpoint;
            if (bridge.Inner is INarrativePresentationCancellation cancelled) cancelled.Cancelled -= bridge.ForwardCancellation;
            bridge.Inner = presenter;
            if (presenter is INarrativePresentationCheckpoints next) next.CheckpointReached += bridge.ForwardCheckpoint;
            if (presenter is INarrativePresentationCancellation cancellation) cancellation.Cancelled += bridge.ForwardCancellation;
        }
        private void EnsureAlive() { if (disposed) throw new ObjectDisposedException(nameof(NarrativeDebugSession)); }
        private void Edit()
        {
            EnsureAlive();
            // First edit of another trial always discards the preceding trial's rewards and facts.
            if (!editing && HasBaseline) RestoreBaseline();
            if (!editing) { Controller.BeginValueRestore(); editing = true; }
        }
        public void SetAbility(string id, int value) { Edit(); if (!abilities.ContainsKey(id)) throw new ArgumentException("Unknown attribute: " + id); abilities[id] = value; }
        public void SetExternalValue(string provider, string key, int value)
        {
            Edit();
            if (!Configuration.bars.Any(b => b.sourceMode == NarrativeSourceMode.External && b.providerId == provider && b.sourceKey == key)) throw new ArgumentException("Unknown external value: " + provider + "/" + key);
            providers[provider].Set(key, value);
        }
        public void SetBar(string key, int value)
        {
            Edit(); var definition = Controller.State.GetDefinition(key) ?? throw new ArgumentException("Unknown bar: " + key);
            if (definition.sourceMode == NarrativeSourceMode.External) SetExternalValue(definition.providerId, definition.sourceKey, value);
            else if (definition.advanceMode == NarrativeAdvanceMode.Derived) throw new InvalidOperationException("Derived bar: set its sources.");
            else Controller.State.TrySetBar(key, value, out _, out _);
        }
        public void SetCondition(string key, bool value) { Edit(); Controller.State.SetCondition(key, value); }
        public void SetDate(int day) { Edit(); if (day < 1) throw new ArgumentOutOfRangeException(nameof(day)); if (providers.TryGetValue("dayflow", out var p)) p.Set("day", day); Controller.SetOfficialDay(day); }
        public void SetLocation(string location) { Edit(); Controller.Location = location ?? string.Empty; }
        public void PrepareEventFact(string id)
        {
            Edit(); if (!Configuration.events.Any(e => e.eventId == id)) throw new ArgumentException("Unknown event fact: " + id);
            Controller.State.MarkEventHappened(id);
        }
        public void EstablishBaseline()
        {
            EnsureAlive();
            if (!editing) throw new InvalidOperationException("先恢复起点并修改条件，不能将上一轮奖励建立为新起点。");
            if (Controller.IsBusy || Actions.IsExecuting) throw new InvalidOperationException("测试起点必须无活动事件/动作。");
            Controller.CompleteValueRestore(false);
            var snapshot = Controller.CaptureSnapshot(); snapshot.actionState = new NarrativeActionStateSnapshot(); Actions.CaptureInto(snapshot.actionState);
            baselineJson = JsonUtility.ToJson(snapshot);
            baselineProviders = providers.ToDictionary(p => p.Key, p => p.Value.Capture());
            baselineAbilities = new Dictionary<string, int>(abilities);
            editing = false; log.Clear(); log.Add("已建立测试起点");
        }
        public void RestoreBaseline()
        {
            EnsureAlive(); if (!HasBaseline) throw new InvalidOperationException("尚未建立测试起点");
            if (Actions.IsExecuting) throw new InvalidOperationException("动作仍在执行，请结束动作后恢复起点。");
            epoch++; completion = null; CurrentRequest = default;
            Controller.BeginValueRestore();
            Controller.RestoreSnapshot(JsonUtility.FromJson<NarrativeSnapshot>(baselineJson));
            abilities.Clear(); foreach (var p in baselineAbilities) abilities.Add(p.Key, p.Value);
            editing = true;
            Actions.RestoreFrom(JsonUtility.FromJson<NarrativeSnapshot>(baselineJson).actionState);
            foreach (var p in baselineProviders) providers[p.Key].Restore(p.Value);
            Controller.CompleteValueRestore(false);
            editing = false; log.Clear(); log.Add("已深复制恢复测试起点");
        }
        private void RequireBaseline() { EnsureAlive(); if (!HasBaseline || editing) throw new InvalidOperationException("请先建立测试起点"); }
        public bool RequestEvent(string id) { RequireBaseline(); log.Add("直接请求 " + Controller.ExplainEvent(id)); return Controller.QueueEvent(id); }
        public bool SelectChoice(string id) { RequireBaseline(); bool accepted = Controller.SelectChoice(id); log.Add("选择 " + id + " accepted=" + accepted); return accepted; }
        public void CompletePresentation() { RequireBaseline(); var callback = completion; callback?.Invoke(); }
        public NarrativeAbilityCheckSnapshot EvaluateAbilityCheck(string id) { RequireBaseline(); return Controller.EvaluateAbilityCheckForPresentation(id); }
        public NarrativeAbilityCheckSnapshot EvaluateInlineAbilityCheck(string id) { RequireBaseline(); return Controller.EvaluateInlineAbilityCheckForPresentation(id); }
        public void SubmitDate(int day)
        {
            RequireBaseline(); if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            log.Add("自然日期提交 " + (providers.TryGetValue("dayflow", out var p) && p.TryRead("day", out int old) ? old.ToString() : "NCS") + " → " + day + " / ProcessOfficialDay");
            if (p != null) p.Set("day", day, true);
            Controller.SetOfficialDay(day); Controller.ProcessOfficialDay(day); Controller.FlushValueChanges();
        }
        public void SubmitBar(string key, int value)
        {
            RequireBaseline(); Controller.State.TryGetBar(key, out int before);
            var bar = Controller.State.GetDefinition(key) ?? throw new ArgumentException("Unknown bar: " + key);
            log.Add("自然阈值提交 " + key + " " + before + " → " + value + " / FlushValueChanges");
            Controller.BeginOperation();
            try
            {
                if (bar.sourceMode == NarrativeSourceMode.External) providers[bar.providerId].Set(bar.sourceKey, value, true);
                else if (bar.advanceMode == NarrativeAdvanceMode.Derived) throw new InvalidOperationException("Derived bar: set its sources.");
                else Controller.SetBar(key, value);
            }
            finally { Controller.EndOperation(); }
            Controller.FlushValueChanges();
        }
        public void EndTurn() { RequireBaseline(); log.Add("提交测试 EndTurn（独立内存）"); Controller.EndTurn(); }
        public string ResolveText(string key)
        {
            string text = Configuration.texts.Find(t => t.key == key)?.content;
            return text == null ? "[缺失文本:" + key + "]" : NarrativeTextSubstitution.PlayerName(text, PreviewName);
        }
        private void SyncActionValues()
        {
            if (!providers.TryGetValue("actions", out var p)) return;
            p.Set("current", Actions.ActionPoints.Current, !editing); p.Set("max", Actions.ActionPoints.Max, !editing);
        }
        public bool TryRead(out int balance) { balance = 0; return providers.TryGetValue("cafe", out var p) && p.TryRead("money", out balance); }
        public bool TryCommit(int expectedBalance, int finalBalance, out string error)
        {
            error = null;
            if (!TryRead(out int actual) || actual != expectedBalance || finalBalance < 0) { error = "Test money account unavailable or stale."; return false; }
            providers["cafe"].Set("money", finalBalance, true); log.Add("已验证资金 " + actual + " → " + finalBalance); return true;
        }
        private static bool IsHostResult(NarrativeResultType type) => type == NarrativeResultType.AwardAchievement || type == NarrativeResultType.SpawnNpc || type == NarrativeResultType.DespawnNpc || type == NarrativeResultType.PlaySetup || type == NarrativeResultType.ShowPosition || type == NarrativeResultType.HidePosition || type == NarrativeResultType.AddTempPosition || type == NarrativeResultType.RemoveTempPosition;
        private string ValidateResult(NarrativeResultDefinition result)
        {
            if (result.resultType == NarrativeResultType.PlaySetup)
            {
                string key = (result.target ?? string.Empty).Replace("setup:", string.Empty);
                if (!Configuration.setups.Any(s => s.setupKey == key)) return "缺少演出setup，未验证: " + key;
            }
            if (result.resultType == NarrativeResultType.ReserveCafeVisit) log.Add("仅记录宿主依赖：咖啡馆预约，顾客到店/反馈未验证 " + result.resultId);
            return null;
        }
        public string GetReport()
        {
            var text = new StringBuilder("模式=分支测试（独立内存）\n起点=" + HasBaseline + " 编辑中=" + editing + "\neventId=" + Controller.CurrentEventId + "\n");
            text.AppendLine("待播=" + string.Join(",", Controller.PendingEventIds));
            text.AppendLine("选择=" + string.Join(",", Controller.SelectedChoices));
            text.AppendLine("属性=" + string.Join(",", abilities.Select(p => p.Key + "=" + p.Value)));
            text.AppendLine("事件事实=" + string.Join(",", Controller.State.HappenedEvents));
            if (HasBaseline)
            {
                var start = JsonUtility.FromJson<NarrativeSnapshot>(baselineJson);
                foreach (var p in Controller.State.Bars)
                {
                    var original = start.bars.Find(b => b.key == p.Key && !b.reverse);
                    if (original != null && original.value != p.Value) text.AppendLine(p.Key + ": " + original.value + " → " + p.Value);
                }
                foreach (var value in Controller.State.Conditions)
                {
                    var old = start.conditionValues?.Find(v => v.key == value.Key);
                    if (old != null && old.value != value.Value) text.AppendLine("条件 " + value.Key + ": " + old.value + " → " + value.Value);
                }
                foreach (var value in Inventory.Items)
                {
                    int old = start.actionState?.items?.Find(v => v.key == value.Key)?.value ?? 0;
                    if (old != value.Value) text.AppendLine("物品 " + value.Key + ": " + old + " → " + value.Value);
                }
                foreach (var p in providers) foreach (var value in p.Value.Capture())
                    if (baselineProviders[p.Key].TryGetValue(value.Key, out int old) && old != value.Value) text.AppendLine(p.Key + "/" + value.Key + ": " + old + " → " + value.Value);
            }
            foreach (string entry in log) text.AppendLine(entry);
            return text.ToString();
        }
        public void Dispose() { if (disposed) return; disposed = true; epoch++; completion = null; CurrentRequest = default; if (bridge.Inner is INarrativePresentationCheckpoints checkpoints) checkpoints.CheckpointReached -= bridge.ForwardCheckpoint; if (bridge.Inner is INarrativePresentationCancellation cancellation) cancellation.Cancelled -= bridge.ForwardCancellation; Controller.UnbindValueProviders(); Actions.StateChanged -= SyncActionValues; }
    }
}
