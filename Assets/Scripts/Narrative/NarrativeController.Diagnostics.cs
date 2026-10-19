using System;
using System.Collections.Generic;
using System.Linq;

namespace Spotlight.Narrative
{
    public sealed class NarrativeConditionDiagnostic
    {
        public string ConditionId, Type, Target, Current, Required, Error;
        public int GroupIndex;
        public bool Passed;
        public override string ToString() => $"{ConditionId} [{GroupIndex}] {Target}: {Current} / {Type} {Required} => {Passed} {Error}";
    }

    public sealed class NarrativeEventDiagnostic
    {
        public string EventId, Rejection;
        public bool CanRequest => Rejection == null;
        public IReadOnlyList<NarrativeConditionDiagnostic> Conditions;
        public override string ToString() => EventId + ": " + (Rejection ?? "可直接请求（不代表自然触发已验证）");
    }

    public sealed partial class NarrativeController
    {
        /// <summary>Optional host dependency preflight; called before any result in the batch commits.</summary>
        public Func<NarrativeResultDefinition, string> ResultDependencyValidator { get; set; }
        public string CurrentEventId => activePresentation ? activeRequest.Event.eventId : null;
        public IReadOnlyList<string> PendingEventIds => queue.Select(r => r.Event.eventId).ToArray();
        public bool BlocksSave => activePresentation && activeRequest.Event.blocksSave || queue.Any(r => r.Event.blocksSave) || pendingEffects.Count > 0 && activeRequest.Event != null && activeRequest.Event.blocksSave;

        private string GetEventRejection(string eventId, bool mapAction = false, bool ignoreTransientBusy = false)
        {
            if (string.IsNullOrWhiteSpace(eventId) || !events.TryGetValue(eventId, out var definition)) return "事件不存在";
            if (!string.IsNullOrWhiteSpace(ConstructionBlocker(definition))) return "施工阻塞：" + ConstructionBlocker(definition);
            if (presenterUnavailable) return "演出器不可用";
            if (valueRestore || !valueBaselineReady) return "数值未就绪";
            if (IsEnded) return "会话已结束";
            if (!ignoreTransientBusy && activeEntry.HasValue) return "阶段入口执行中";
            if (definition.entry != NarrativeEntryPoint.Immediate && !(mapAction && definition.entry == NarrativeEntryPoint.MapAction)) return "事件等待声明入口：" + definition.entry;
            var dates = configuration.eventDates.Where(d => d != null && d.eventId == eventId).ToArray();
            if (dates.Length > 0 && !dates.Any(d => DateMatches(d, GetDay()) && DateBoundaryMatches(d))) return "日期或真实阶段不匹配";
            if (!ignoreTransientBusy && (activePresentation || draining)) return "系统忙：活动演出或等待演出结束";
            if (!ignoreTransientBusy && queue.Any(r => r.Event.eventId == eventId)) return "事件已在队列";
            if (state.IsEventLocked(eventId)) return "事件被锁定";
            if (state.IsFlagSet("event.once." + eventId) || definition.onceOnly && state.HasHappenedEvent(eventId)) return "一次性事件已发生";
            return EvaluateConditions("event", string.IsNullOrWhiteSpace(definition.conditionOwnerId) ? eventId : definition.conditionOwnerId, Context(definition)) ? null : "条件不满足";
        }

        public NarrativeEventDiagnostic ExplainEvent(string eventId) => ExplainEventAtEntry(eventId, false);

        private NarrativeEventDiagnostic ExplainEventAtEntry(string eventId, bool mapAction, bool ignoreTransientBusy = false)
        {
            var result = new NarrativeEventDiagnostic { EventId = eventId };
            try { result.Rejection = GetEventRejection(eventId, mapAction, ignoreTransientBusy); }
            catch (Exception e) { result.Rejection = e.Message; }
            events.TryGetValue(eventId ?? string.Empty, out var definition);
            result.Conditions = ExplainConditions("event", string.IsNullOrWhiteSpace(definition?.conditionOwnerId) ? eventId : definition.conditionOwnerId);
            return result;
        }

        public IReadOnlyList<NarrativeConditionDiagnostic> ExplainConditions(string ownerType, string ownerId)
        {
            var rows = new List<NarrativeConditionDiagnostic>();
            if (!conditionsByOwner.TryGetValue(OwnerKey(ownerType, ownerId), out var conditions)) return rows;
            foreach (var condition in conditions)
            {
                var row = new NarrativeConditionDiagnostic { ConditionId = condition.conditionId, Type = condition.condType, Target = condition.target, Required = condition.value, GroupIndex = condition.groupIndex };
                try { row.Passed = EvaluateCondition(condition); row.Current = ReadConditionValue(condition); }
                catch (Exception e) { row.Error = e.Message; row.Current = "不可用"; }
                rows.Add(row);
            }
            return rows;
        }

        private string ReadConditionValue(NarrativeConditionDefinition c)
        {
            string key = StripPrefix(c.target, "bar:", "rel:", "event:", "item:", "action:");
            switch (c.condType?.Trim().ToUpperInvariant())
            {
                case "BOOL_EQ":
                    if (string.Equals(c.ownerType, "result", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(c.ownerType, "action", StringComparison.OrdinalIgnoreCase))
                        return TryReadPresentationCondition(c.target, out bool guardValue) ? guardValue.ToString() : "不可用";
                    return state.GetCondition(c.target).ToString();
                case "ITEM_GTE": return inventory.GetCount(key).ToString();
                case "ACTION_PROGRESS_GTE": return ActionProgressReader?.Invoke(key)?.ToString();
                case "ACTION_COMPLETED": return ActionCompletedReader?.Invoke(key)?.ToString();
                case "EVENT_HAPPENED": case "EVENT_NOT_HAPPENED": return state.HasHappenedEvent(key).ToString();
                default: return state.TryGetBar(key, out int value) ? value.ToString() : "不可用";
            }
        }

        /// <summary>Evaluates the real pure evaluator without caching, queuing, hint selection or random consumption.</summary>
        public NarrativeAbilityCheckSnapshot InspectAbilityCheck(string checkId) => EvaluateAbilityCheck(checkId);
    }
}
