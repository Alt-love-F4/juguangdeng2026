using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spotlight.Narrative
{
    /// <summary>
    /// Main-thread, read-only view of an explicitly supplied session. The owner (e.g. GM) should
    /// Refresh(existingRuntime.Controller) every 0.5 seconds, including null on session removal.
    /// Diagnostics are lazy and cached until Refresh; OnGUI must not refresh for each node/frame.
    /// Never discovers/creates a runtime, captures a controller snapshot, pumps, or binds providers.
    /// </summary>
    public sealed class NarrativeGraphRuntimeStatus
    {
        private readonly NarrativeConfiguration configuration;
        private readonly Dictionary<StoryGraphNode, Status> nodes = new Dictionary<StoryGraphNode, Status>();
        private readonly Dictionary<string, string> pages = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, NarrativeEventDefinition> events;
        private NarrativeController controller;

        private sealed class Status
        {
            public string Label, Details;
        }

        public NarrativeGraphRuntimeStatus(NarrativeConfiguration configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            events = configuration.events.Where(e => e != null && e.eventId != null)
                .GroupBy(e => e.eventId).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        }

        public void Refresh(NarrativeController controller)
        {
            this.controller = controller;
            nodes.Clear();
            pages.Clear();
        }

        public string Label(StoryGraphNode node) => Read(node).Label;
        public string Details(StoryGraphNode node) => Read(node).Details;

        private Status Read(StoryGraphNode node)
        {
            if (node == null) return new Status { Label = "未知节点", Details = "未提供节点；没有推断任何运行状态。" };
            if (nodes.TryGetValue(node, out var cached)) return cached;
            var text = new StringBuilder();
            string label;
            try { label = Describe(node, text); }
            catch (Exception error)
            {
                label = "未知（读取不可用）";
                text.AppendLine("只读诊断不可用：" + error.Message);
            }
            text.Insert(0, label + "\n");
            text.AppendLine("只读观察；条件满足不等于已播放，也不代表自然触发已验证。");
            var status = new Status { Label = label, Details = text.ToString() };
            nodes.Add(node, status);
            return status;
        }

        private string Describe(StoryGraphNode node, StringBuilder text)
        {
            events.TryGetValue(node.Key ?? "", out var definition);
            var ending = node.Kind == StoryNodeKind.Ending
                ? configuration.endings.Find(e => e != null && e.endingId == node.Key) : null;
            string blocker = node.Blocker;
            if (node.Kind == StoryNodeKind.Event && string.IsNullOrWhiteSpace(blocker))
                blocker = definition?.eventProperties?.FirstOrDefault(p => p != null && p.property == "blockedBy")?.value;
            string priority = !string.IsNullOrWhiteSpace(blocker) ? "建设阻塞：" + blocker
                : node.Disabled || ending != null && !ending.enabled ? "禁用" : null;

            if (controller == null)
            {
                text.AppendLine("无会话：未注入 controller；当前值、入口可请求性与历史均不可用。");
                if (node.Kind == StoryNodeKind.Event || node.Kind == StoryNodeKind.Ending)
                {
                    string ownerType = node.Kind == StoryNodeKind.Event ? "event" : "ending";
                    string ownerId = node.Kind == StoryNodeKind.Event && !string.IsNullOrWhiteSpace(definition?.conditionOwnerId)
                        ? definition.conditionOwnerId : node.Key;
                    AppendConditions(text, configuration.conditions.Where(c => c != null && c.ownerId == ownerId
                        && string.Equals(string.IsNullOrWhiteSpace(c.ownerType) ? "event" : c.ownerType.Trim(), ownerType, StringComparison.OrdinalIgnoreCase))
                        .Select(c => new NarrativeConditionDiagnostic { ConditionId = c.conditionId, GroupIndex = c.groupIndex,
                            Target = c.target, Type = c.condType, Required = c.value, Current = "不可用", Error = "无会话" }).ToArray());
                }
                if (node.Kind == StoryNodeKind.Opening) text.AppendLine("Opening 无独立播放历史，不能由同名 Event 推断。");
                if (node.Kind == StoryNodeKind.Fact) text.AppendLine(FactProducer(node.Key));
                return priority ?? "无会话";
            }

            switch (node.Kind)
            {
                case StoryNodeKind.Event:
                    text.AppendLine("声明入口：" + (definition == null ? "未知" : definition.entry.ToString()));
                    if (definition == null) return priority ?? "未知（事件不存在）";
                    var diagnostic = definition.entry == NarrativeEntryPoint.MapAction
                        ? controller.ExplainMapActionEvent(node.Key) : controller.ExplainEvent(node.Key);
                    text.AppendLine("当前入口诊断：" + (diagnostic.Rejection ?? "可请求（不代表已播放）"));
                    AppendConditions(text, diagnostic.Conditions);
                    bool happened = controller.State.HasHappenedEvent(node.Key);
                    bool once = definition.onceOnly || controller.State.IsFlagSet("event.once." + node.Key);
                    text.AppendLine("根事件已发生记录：" + happened + "；一次性限制：" + once);
                    if (priority != null) return priority;
                    if (controller.CurrentEventId == node.Key) return "当前播放";
                    if (controller.PendingEventIds.Contains(node.Key)) return "排队中";
                    if (happened) return once ? "已发生（一次性）"
                        : "已发生（可重复）；" + RequestLabel(diagnostic);
                    return RequestLabel(diagnostic);
                case StoryNodeKind.Opening:
                    text.AppendLine("Opening 无独立播放历史；Event 完成记录、条件或同名 ID 均不能证明此独立演出已播放。");
                    return priority ?? "未知（Opening 无独立历史）";
                case StoryNodeKind.Ending:
                    text.AppendLine("已选择结局事实：" + (controller.SelectedEndingId ?? "无"));
                    var conditions = controller.ExplainConditions("ending", node.Key);
                    AppendConditions(text, conditions);
                    text.AppendLine("不调用结局检查，不选择结局，不以当前条件反推已达成。");
                    if (priority != null) return priority;
                    if (ending == null) return "未知（结局不存在）";
                    if (controller.SelectedEndingId == node.Key) return "已达成（已选择结局）";
                    if (controller.IsEnded) return "未选中（会话已有其他结局）";
                    bool? eligible = GroupedConditions(conditions);
                    return !eligible.HasValue ? "未知（结局条件不可用或未配置）"
                        : eligible.Value ? "结局条件满足（尚未选中）" : "结局条件不满足（尚未选中）";
                case StoryNodeKind.Fact:
                    text.AppendLine(FactProducer(node.Key));
                    bool known = controller.State.TryGetCondition(node.Key, out bool value);
                    text.AppendLine("当前布尔值：" + (known ? value.ToString().ToLowerInvariant() : "未知／不可用（会话未注册）"));
                    return priority ?? (known ? "事实 = " + value.ToString().ToLowerInvariant() : "未知（事实未注册）");
                default: return priority ?? "未知（节点类型）";
            }
        }

        private static string RequestLabel(NarrativeEventDiagnostic diagnostic)
        {
            if (diagnostic.CanRequest) return "当前入口可请求（未证明已播放）";
            bool unavailable = diagnostic.Conditions != null && diagnostic.Conditions.Any(c => !string.IsNullOrEmpty(c.Error));
            return (unavailable ? "未知／不可请求：" : "不可请求：") + diagnostic.Rejection;
        }

        private string FactProducer(string key)
        {
            bool produced = configuration.results.Any(r => r != null && r.resultType == NarrativeResultType.SetFlag
                && (r.target == key || r.target == "cond:" + key || r.target == "flag:" + key)
                && bool.TryParse(r.value, out bool value) && value);
            return produced ? "存在表内置真结果；是否执行仅以会话事实为准。"
                : "未找到表内置真生产者；不等于缺少宿主实现，不据此猜测 bool。";
        }

        private static void AppendConditions(StringBuilder text, IReadOnlyList<NarrativeConditionDiagnostic> rows)
        {
            text.AppendLine("当前条件（组内 AND／组间 OR；不是播放历史）：");
            if (rows == null || rows.Count == 0) { text.AppendLine("  无条件行"); return; }
            foreach (var row in rows)
                text.AppendLine($"  {row.ConditionId} [组 {row.GroupIndex}] {row.Target}: 当前={Available(row.Current)}；{row.Type} 要求={Available(row.Required)}；"
                    + (string.IsNullOrEmpty(row.Error) ? "判定=" + row.Passed : "判定未知／不可用：" + row.Error));
        }

        private static string Available(string value) => string.IsNullOrWhiteSpace(value) ? "不可用／未提供" : value;

        private static bool? GroupedConditions(IReadOnlyList<NarrativeConditionDiagnostic> rows)
        {
            if (rows == null || rows.Count == 0) return null;
            bool unknown = false;
            foreach (var group in rows.GroupBy(c => c.GroupIndex))
            {
                // Three-valued AND/OR: an unknown alternative must not hide a certain true group.
                if (group.Any(c => string.IsNullOrEmpty(c.Error) && !c.Passed)) continue;
                if (group.All(c => string.IsNullOrEmpty(c.Error))) return true;
                unknown = true;
            }
            return unknown ? (bool?)null : false;
        }

        /// <summary>No per-page ledger or public active-page cursor exists in the controller.
        /// Only evaluate current guards; root completion is deliberately never propagated to pages.</summary>
        public string PageStatus(string eventId, string pageId)
        {
            string key = (eventId ?? "") + "\n" + (pageId ?? "");
            if (pages.TryGetValue(key, out string cached)) return cached;
            const string boundary = "系统无逐页历史；根事件已完成不代表每个互斥页均看过。活动当前页不可得，不能标记本页正在播放。";
            var text = new StringBuilder(boundary).AppendLine();
            try
            {
                if (!events.TryGetValue(eventId ?? "", out var definition)) text.AppendLine("未知：事件不存在。");
                else
                {
                    var page = configuration.presentations.conversationPages.Find(p => p != null && p.pageId == pageId && p.presentationKey == definition.presentationKey);
                    if (page == null) text.AppendLine("未知：未找到属于该根事件的 ConversationPage。");
                    else if (controller == null) text.AppendLine("无会话：条件当前值不可用；只能浏览配置。");
                    else
                    {
                        bool failed = false, unknown = false;
                        if (!string.IsNullOrWhiteSpace(page.pageBarConds))
                        {
                            unknown = true;
                            text.AppendLine("pageBarConds 不受现有演出支持，条件预判未知：" + page.pageBarConds);
                        }
                        foreach (string guard in (page.pageCondKeys ?? "").Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            bool? result;
                            string error = null;
                            try { result = ReadGuard(eventId, guard.Trim()); }
                            catch (Exception e) { result = null; error = e.Message; }
                            if (!result.HasValue) unknown = true;
                            else if (!result.Value) failed = true;
                            text.AppendLine(guard.Trim() + " = " + (result.HasValue ? result.Value.ToString().ToLowerInvariant() : "未知／不可用")
                                + (error == null ? "" : "：" + error));
                        }
                        text.AppendLine("仅条件预判：" + (failed ? "不满足" : unknown ? "未知／不可用" : "满足（不代表已播放或必将到达）"));
                    }
                }
            }
            catch (Exception error) { text.AppendLine("条件预判未知／不可用：" + error.Message); }
            return pages[key] = text.ToString();
        }

        private bool? ReadGuard(string eventId, string guard, int depth = 0)
        {
            if (string.IsNullOrWhiteSpace(guard) || depth > 64) return null;
            guard = guard.Trim();
            if (guard.StartsWith("not:", StringComparison.Ordinal))
            {
                bool? inner = ReadGuard(eventId, guard.Substring(4), depth + 1);
                return inner.HasValue ? !inner.Value : (bool?)null;
            }
            if (guard.StartsWith("bar:", StringComparison.Ordinal))
            {
                if (!NarrativePresentationGuards.TryParseNumeric(guard, out string bar, out string op, out int bound)
                    || !controller.State.TryGetBar(bar, out int value)) return null;
                return NarrativePresentationGuards.Compare(value, op, bound);
            }
            if (guard.StartsWith("choice:", StringComparison.Ordinal) || guard.StartsWith("anyChoice:", StringComparison.Ordinal)
                || guard.StartsWith("check:", StringComparison.Ordinal))
            {
                if (controller.CurrentEventId != eventId) return null; // Never reuse another/finished root's local evidence.
                // These paths are pure. A not-yet-evaluated check throws; the caller renders unknown.
                return controller.ReadPresentationCondition(guard);
            }
            // These runtime readers can mutate warning-dedup state on missing evidence. Do not invoke them.
            if (guard.StartsWith("gameplay:", StringComparison.Ordinal) || guard.StartsWith("dependency:", StringComparison.Ordinal)) return null;
            return controller.State.TryGetCondition(guard, out bool fact) ? fact : (bool?)null;
        }
    }
}
