using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;

namespace Spotlight.Narrative.Editor
{
    public static class NarrativeConfigurationValidator
    {
        public static List<string> Validate(NarrativeConfiguration c)
        {
            var e = new List<string>();
            Unique(c.objects.Select(x => x.objId), "Object", e); Unique(c.bars.Select(x => x.barKey), "Bar", e);
            Unique(c.triggers.Select(x => x.triggerId), "Trigger", e); Unique(c.events.Select(x => x.eventId), "Event", e);
            Unique(c.results.Select(x => x.resultId), "Result", e); Unique(c.conditions.Select(x => x.conditionId), "Condition", e);
            Unique(c.eventDates.Select(x => x.eventId + "@" + x.day + "@" + x.timeSlot), "EventDate", e);
            Unique(c.templates.Select(x => x.tplName), "Template", e); Unique(c.setups.Select(x => x.setupKey), "SetupParams", e);
            Unique(c.choices.Select(x => x.choiceId), "Choice", e); Unique(c.texts.Select(x => x.key), "Text", e);
            var ids = new HashSet<string>(c.objects.Select(x => x.objId).Where(x => !string.IsNullOrWhiteSpace(x)));
            var bars = new Dictionary<string, NarrativeBarDefinition>(StringComparer.Ordinal);
            foreach (var b in c.bars)
            {
                if (string.IsNullOrWhiteSpace(b.barKey)) { e.Add("Bar: missing barKey"); continue; }
                if (!ids.Contains(b.ownerId)) e.Add($"Bar {b.barKey}: unknown owner {b.ownerId}.");
                if (b.barType == NarrativeBarType.Relation && !ids.Contains(b.targetId)) e.Add($"Bar {b.barKey}: relation target {b.targetId} is unknown.");
                bool calendar = b.barKey == "world.calendar";
                bool allowed = calendar || (b.min == 0 && b.max == 20) || (b.min == 0 && b.max == 10) || (b.min == -2 && b.max == 5);
                if (!allowed) e.Add($"Bar {b.barKey}: range must be 0..20, 0..10, -2..5; only world.calendar is exempt.");
                if (b.min >= b.max) e.Add($"Bar {b.barKey}: min must be less than max.");
                if (b.initValue < b.min || b.initValue > b.max) e.Add($"Bar {b.barKey}: initValue is outside its range.");
                if (b.advanceMode == NarrativeAdvanceMode.Derived && b.derivedSources.Count == 0) e.Add($"Bar {b.barKey}: derived bar requires derivedSources.");
                bars[b.barKey] = b;
            }
            foreach (var t in c.triggers)
            {
                if (!bars.ContainsKey(t.barKey)) e.Add($"Trigger {t.triggerId}: unknown bar {t.barKey}.");
                else { var b = bars[t.barKey]; if (t.threshold < b.min || t.threshold > b.max) e.Add($"Trigger {t.triggerId}: threshold outside {b.min}..{b.max}."); }
                if (!c.events.Any(x => x.eventId == t.eventId)) e.Add($"Trigger {t.triggerId}: unknown event {t.eventId}.");
                if (!string.Equals(t.reachCount, "all", StringComparison.OrdinalIgnoreCase) && (!int.TryParse(t.reachCount, out int reach) || reach < 1)) e.Add($"Trigger {t.triggerId}: reachCount must be all or a positive integer.");
            }
            foreach (var group in c.triggers.GroupBy(t => new { t.barKey, t.threshold, t.direction }))
                if (group.Select(t => t.resetOnExit).Distinct().Count() > 1)
                    e.Add($"Trigger {group.Key.barKey}@{group.Key.threshold}@{group.Key.direction}: resetOnExit must be identical for the shared counter group.");
            foreach (var x in c.results)
            {
                if (!c.events.Any(v => v.eventId == x.eventId) && !c.choices.Any(v => (string.IsNullOrWhiteSpace(v.resultOwnerId) ? v.choiceId : v.resultOwnerId) == x.eventId)) e.Add($"Result {x.resultId}: unknown event/choice result owner {x.eventId}.");
                if ((x.resultType == NarrativeResultType.BarAdd || x.resultType == NarrativeResultType.BarSet || x.resultType == NarrativeResultType.RelAdd) && !int.TryParse(x.value, out _)) e.Add($"Result {x.resultId}: numeric value required.");
            }
            foreach (var x in c.conditions)
            {
                if (string.Equals(x.ownerType, "event", StringComparison.OrdinalIgnoreCase)) { if (!c.events.Any(v => (string.IsNullOrWhiteSpace(v.conditionOwnerId) ? v.eventId : v.conditionOwnerId) == x.ownerId)) e.Add($"Condition {x.conditionId}: unknown event owner {x.ownerId}."); }
                else if (string.Equals(x.ownerType, "choice", StringComparison.OrdinalIgnoreCase)) { if (!c.choices.Any(v => v.choiceId == x.ownerId)) e.Add($"Condition {x.conditionId}: unknown choice owner {x.ownerId}."); }
                else e.Add($"Condition {x.conditionId}: ownerType must be event or choice.");
                string type = (x.condType ?? "").ToUpperInvariant();
                string target = x.target ?? "";
                string key = target.Contains(":") ? target.Substring(target.IndexOf(':') + 1) : target;
                var types = new[] { "BAR_GTE", "BAR_LTE", "BAR_BETWEEN", "REL_GTE", "FLAG_SET", "FLAG_CLEAR", "SWITCH_ON", "EVENT_HAPPENED", "EVENT_NOT_HAPPENED", "ACTOR_IS", "TARGET_IS", "LOCATION_EQ", "TIMESLOT_EQ", "REACH_COUNT_EQ", "CHANCE_PCT" };
                if (!types.Contains(type)) e.Add($"Condition {x.conditionId}: unsupported condType {x.condType}.");
                if (type.StartsWith("BAR_") && !bars.ContainsKey(key)) e.Add($"Condition {x.conditionId}: unknown bar {target}.");
                if ((type == "BAR_GTE" || type == "BAR_LTE" || type == "REL_GTE" || type == "REACH_COUNT_EQ") && !int.TryParse(x.value, out _)) e.Add($"Condition {x.conditionId}: integer value required.");
                if (type == "BAR_BETWEEN")
                {
                    var range = (x.value ?? "").Split(new[] { '.', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (range.Length != 2 || !int.TryParse(range[0], out int low) || !int.TryParse(range[1], out int high) || low > high) e.Add($"Condition {x.conditionId}: expected inclusive min..max.");
                }
                if (type == "CHANCE_PCT" && (!double.TryParse(x.value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double chance) || double.IsNaN(chance) || chance < 0 || chance > 1)) e.Add($"Condition {x.conditionId}: probability must be 0..1.");
                if (type == "TIMESLOT_EQ" && !Enum.TryParse<NarrativeTimeSlot>(x.value, true, out _)) e.Add($"Condition {x.conditionId}: unsupported timeslot.");
                if ((type == "EVENT_HAPPENED" || type == "EVENT_NOT_HAPPENED") && !c.events.Any(ev => ev.eventId == key)) e.Add($"Condition {x.conditionId}: unknown event {target}.");
            }
            foreach (var x in c.events)
            {
                if (x.eventKind == NarrativeEventKind.Actor && !ids.Contains(x.actorId)) e.Add($"Event {x.eventId}: actorId must reference an Object.");
                if (!string.IsNullOrWhiteSpace(x.targetId) && !ids.Contains(x.targetId)) e.Add($"Event {x.eventId}: unknown targetId {x.targetId}.");
            }
            foreach (var x in c.eventDates)
            {
                if (!c.events.Any(v => v.eventId == x.eventId)) e.Add($"EventDate: unknown event {x.eventId}.");
                if (x.day < 0 || x.repeatRule == NarrativeRepeatRule.Yearly && (x.day < 1 || x.day > 365)) e.Add($"EventDate {x.eventId}: day must be non-negative; yearly day must be 1..365.");
            }
            foreach (var x in c.setups)
            {
                var template = c.templates.FirstOrDefault(v => v.tplName == x.tplName);
                if (template == null) { e.Add($"SetupParams {x.setupKey}: unknown template {x.tplName}."); continue; }
                var slots = new HashSet<string>(Split(template.slotDef).Select(v => v.Split(':')[0]));
                var assigned = new HashSet<string>();
                foreach (var p in x.parameters)
                {
                    if (!slots.Contains(p.slot)) e.Add($"SetupParams {x.setupKey}: undeclared slot {p.slot}.");
                    if (!assigned.Add(p.slot)) e.Add($"SetupParams {x.setupKey}: duplicate slot {p.slot}.");
                    if (string.IsNullOrWhiteSpace(p.value) && !IsOptionalSlot(p.slot)) e.Add($"SetupParams {x.setupKey}: empty value for slot {p.slot}.");
                }
                foreach (var slot in slots) if (!assigned.Contains(slot)) e.Add($"SetupParams {x.setupKey}: missing slot {slot}.");
            }
            foreach (var b in c.bars.Where(x => x.advanceMode == NarrativeAdvanceMode.Derived))
            {
                foreach (var source in b.derivedSources) if (!bars.ContainsKey(source)) e.Add($"Bar {b.barKey}: unknown derived source {source}.");
                if (b.derivedWeights.Count != 0 && b.derivedWeights.Count != b.derivedSources.Count) e.Add($"Bar {b.barKey}: derivedWeights count must match derivedSources.");
                if (HasCycle(b.barKey, bars, new HashSet<string>())) e.Add($"Bar {b.barKey}: derived source cycle detected.");
            }
            foreach (var x in c.events)
            {
                string setupKey = string.IsNullOrWhiteSpace(x.setupKey) ? x.presentationKey : x.setupKey;
                if (!string.IsNullOrWhiteSpace(setupKey) && !c.setups.Any(v => v.setupKey == setupKey)) e.Add($"Event {x.eventId}: unknown setup {setupKey}.");
                if (string.IsNullOrWhiteSpace(x.setupType) && string.IsNullOrWhiteSpace(x.presentationClass)) e.Add($"Event {x.eventId}: missing presentation type.");
                if (!string.IsNullOrWhiteSpace(x.presentationClass) && !new[] { "Conversation", "Reading", "Notice", "CutScene" }.Contains(x.presentationClass, StringComparer.OrdinalIgnoreCase)) e.Add($"Event {x.eventId}: unsupported presentationClass {x.presentationClass}.");
            }
            foreach (var x in c.choices)
            {
                if (string.IsNullOrWhiteSpace(x.groupKey)) e.Add($"Choice {x.choiceId}: missing groupKey.");
                if (!c.texts.Any(v => v.key == x.textKey)) e.Add($"Choice {x.choiceId}: unknown text key {x.textKey}.");
            }
            foreach (var x in c.results)
            {
                if (x.resultType == NarrativeResultType.PlaySetup)
                {
                    string setupKey = x.target ?? string.Empty;
                    if (setupKey.StartsWith("setup:", StringComparison.OrdinalIgnoreCase)) setupKey = setupKey.Substring(6);
                    var setup = c.setups.FirstOrDefault(s => s.setupKey == setupKey);
                    var template = setup == null ? null : c.templates.FirstOrDefault(t => t.tplName == setup.tplName);
                    bool choiceAtom = template != null && (template.atomSeq ?? string.Empty).Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries).Any(a => string.Equals(a.Trim(), "A_CHOICE", StringComparison.OrdinalIgnoreCase) || string.Equals(a.Trim(), "CHOICE", StringComparison.OrdinalIgnoreCase));
                    bool hasOptions = setup != null && (setup.parameters.Any(p => string.Equals(p.slot, "opts", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(p.value)) || Split(setup.slotValues).Any(a => a.StartsWith("opts=", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(a.Substring(5))));
                    if (setup == null) e.Add($"Result {x.resultId}: PLAY_SETUP references unknown setup {setupKey}.");
                    else if (choiceAtom && hasOptions) e.Add($"Result {x.resultId}: PLAY_SETUP cannot invoke choice setup {setup.setupKey}; choices require a narrative event transaction.");
                }
                if (x.resultType == NarrativeResultType.BarAdd || x.resultType == NarrativeResultType.BarSet || x.resultType == NarrativeResultType.RelAdd || x.resultType == NarrativeResultType.BarHint)
                {
                    string rawTarget = x.target ?? string.Empty;
                    string key = rawTarget.IndexOf(':') >= 0 ? rawTarget.Substring(rawTarget.IndexOf(':') + 1) : rawTarget;
                    if (x.resultType == NarrativeResultType.RelAdd && rawTarget.StartsWith("rel:", StringComparison.OrdinalIgnoreCase))
                    {
                        var relation = rawTarget.Substring(4).Split('>');
                        var candidates = bars.Values.Where(b => relation.Length == 2 && b.barType == NarrativeBarType.Relation &&
                            (b.ownerId == relation[0] && b.targetId == relation[1] || b.ownerId == relation[1] && b.targetId == relation[0])).ToList();
                        key = bars.ContainsKey(key) ? key : candidates.Count == 1 ? candidates[0].barKey : null;
                    }
                    if (string.IsNullOrWhiteSpace(key) || !bars.ContainsKey(key)) e.Add($"Result {x.resultId}: unknown bar target {x.target}.");
                    if ((x.resultType == NarrativeResultType.BarAdd || x.resultType == NarrativeResultType.RelAdd || x.resultType == NarrativeResultType.BarSet) && !int.TryParse(x.value, out _)) e.Add($"Result {x.resultId}: numeric value required.");
                }
            }
            return e;
        }
        static bool IsOptionalSlot(string slot) => string.Equals(slot, "opts", StringComparison.OrdinalIgnoreCase) || string.Equals(slot, "optionList", StringComparison.OrdinalIgnoreCase) || string.Equals(slot, "bg", StringComparison.OrdinalIgnoreCase) || string.Equals(slot, "a", StringComparison.OrdinalIgnoreCase) || string.Equals(slot, "b", StringComparison.OrdinalIgnoreCase) || string.Equals(slot, "portrait", StringComparison.OrdinalIgnoreCase);
        static IEnumerable<string> Split(string text) => (text ?? string.Empty).Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
        static bool HasCycle(string key, Dictionary<string, NarrativeBarDefinition> bars, HashSet<string> visiting)
        {
            if (!bars.TryGetValue(key, out var b) || b.advanceMode != NarrativeAdvanceMode.Derived) return false;
            if (!visiting.Add(key)) return true;
            bool result = b.derivedSources.Any(source => HasCycle(source, bars, visiting));
            visiting.Remove(key);
            return result;
        }
        static void Unique(IEnumerable<string> values, string kind, List<string> errors)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values) if (string.IsNullOrWhiteSpace(value)) errors.Add(kind + ": missing ID"); else if (!seen.Add(value)) errors.Add(kind + ": duplicate ID " + value);
        }
    }
}
