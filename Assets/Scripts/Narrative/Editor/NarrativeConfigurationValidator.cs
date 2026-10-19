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
                bool training = b.barKey.StartsWith("training.", StringComparison.Ordinal) && new[] { "language", "courage", "wisdom", "perception" }.Contains(b.barKey.Substring(9))
                    && b.sourceMode == NarrativeSourceMode.Ncs && b.advanceMode == NarrativeAdvanceMode.EventDriven && b.min == 0 && b.max == 9;
                bool social = (b.barKey.StartsWith("faction.", StringComparison.Ordinal) || b.barKey.EndsWith(".affection", StringComparison.Ordinal))
                    && b.sourceMode == NarrativeSourceMode.Ncs && b.advanceMode == NarrativeAdvanceMode.EventDriven && b.min == -20 && b.max == 20;
                bool allowed = b.sourceMode == NarrativeSourceMode.External || calendar || training || social || (b.min == 0 && b.max == 20) || (b.min == 0 && b.max == 10) || (b.min == -2 && b.max == 5);
                if (!allowed) e.Add($"Bar {b.barKey}: range must be 0..20, 0..10, -2..5; training attributes use 0..9, faction/affection bars use -20..20; only world.calendar/external bars are exempt.");
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
            e.AddRange(LubanNarrativeLoader.ValidateEntryConfiguration(c));
            e.AddRange(LubanNarrativeLoader.ValidateConversationSteps(c));
            e.AddRange(LubanNarrativeLoader.ValidateConditionConfiguration(c));
            e.AddRange(LubanNarrativeLoader.ValidateValueConfiguration(c));
            var checkIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var check in c.abilityChecks)
            {
                if (check == null || string.IsNullOrWhiteSpace(check.checkId) || !checkIds.Add(check.checkId)) e.Add("AbilityCheck: missing or duplicate checkId " + check?.checkId);
                else e.AddRange(LubanNarrativeLoader.ValidateAbilityCheckMode(c, check));
                if (check != null && !c.abilityCheckConditions.Any(v => v.checkId == check.checkId)) e.Add($"AbilityCheck {check.checkId}: requires conditions.");
            }
            foreach (var condition in c.abilityCheckConditions)
            {
                if (!checkIds.Contains(condition.checkId)) e.Add($"AbilityCheckCondition {condition.conditionId}: unknown check {condition.checkId}.");
                if (!c.objects.Any(o => o.objId == "player") || !c.bars.Any(b => b.barKey == "player." + condition.attributeId)) e.Add($"AbilityCheckCondition {condition.conditionId}: unknown player attribute {condition.attributeId}.");
                if (condition.groupIndex < 1 || condition.threshold < 0) e.Add($"AbilityCheckCondition {condition.conditionId}: invalid group or threshold.");
            }
            foreach (var choice in c.choices) if (!string.IsNullOrWhiteSpace(choice.checkId) && !checkIds.Contains(choice.checkId)) e.Add($"Choice {choice.choiceId}: unknown check {choice.checkId}.");
            foreach (var setup in c.setups) if (!string.IsNullOrWhiteSpace(setup.checkId) && !checkIds.Contains(setup.checkId)) e.Add($"SetupParams {setup.setupKey}: unknown check {setup.checkId}.");
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
                foreach (var slot in slots) if (!assigned.Contains(slot) && !IsOptionalSlot(slot)) e.Add($"SetupParams {x.setupKey}: missing slot {slot}.");
            }
            foreach (var b in c.bars.Where(x => x.advanceMode == NarrativeAdvanceMode.Derived))
            {
                foreach (var source in b.derivedSources) if (!bars.ContainsKey(source)) e.Add($"Bar {b.barKey}: unknown derived source {source}.");
                if (b.derivedWeights.Count != 0 && b.derivedWeights.Count != b.derivedSources.Count) e.Add($"Bar {b.barKey}: derivedWeights count must match derivedSources.");
                if (HasCycle(b.barKey, bars, new HashSet<string>())) e.Add($"Bar {b.barKey}: derived source cycle detected.");
            }
            foreach (var x in c.events)
            {
                string setupKey = x.setupKey;
                if (!string.IsNullOrWhiteSpace(setupKey) && !c.setups.Any(v => v.setupKey == setupKey)) e.Add($"Event {x.eventId}: unknown setup {setupKey}.");
                bool hasPresentation = !string.IsNullOrWhiteSpace(x.presentationClass)
                    || (!string.IsNullOrWhiteSpace(x.presentationKey)
                        && c.presentations.index.Any(index => index.presentationKey == x.presentationKey));
                if (string.IsNullOrWhiteSpace(x.setupType) && !hasPresentation && !LubanNarrativeLoader.IsResultOnlyEvent(c, x)) e.Add($"Event {x.eventId}: missing presentation type.");
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
        public static List<string> Warnings(NarrativeConfiguration c)
        {
            var warnings = new List<string>();
            if (c.cafeVisits.Count == 0) warnings.Add("CafeVisit: 特殊来访配置为空，使用默认来访行为。");
            if (c.cafeVisitHooks.Count == 0) warnings.Add("CafeVisitHook: 阶段剧情未配置，演出未验证。");
            foreach (var hook in c.cafeVisitHooks)
            {
                if (!c.events.Any(x => x.eventId == hook.eventId)) warnings.Add($"CafeVisitHook {hook.visitKey}/{hook.stage}: missing event {hook.eventId}; runtime skips.");
                if (hook.stage == "before_served" && hook.trigger == "auto" || hook.trigger == "click" && hook.stage != "before_served" && hook.stage != "served")
                    warnings.Add($"CafeVisitHook {hook.visitKey}/{hook.stage}/{hook.trigger}: unsupported stage trigger; use arrived auto or before_served/served click.");
            }
            foreach (var action in c.actions.Where(x => x.deliveryMode != NarrativeDeliveryMode.None))
            {
                List<NarrativeActionRequirement> costs;
                try { costs = NarrativeActionRequirementParser.Parse(action.requirements); }
                catch (FormatException) { continue; }
                foreach (var result in c.results.Where(x => x.resultType == NarrativeResultType.ItemRemove && action.resultEvents.Contains(x.eventId)))
                    if (costs.Any(x => x.type == NarrativeActionRequirementType.Item && x.key == result.target))
                        warnings.Add($"Action {action.actionKey}, Result {result.resultId}: completion removes delivery cost item {result.target} again.");
            }
            return warnings;
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
