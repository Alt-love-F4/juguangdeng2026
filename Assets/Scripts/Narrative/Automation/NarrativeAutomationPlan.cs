using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Spotlight.Narrative
{
    /// <summary>
    /// A preference, never an authority: overrides still have to be visible and unlocked. Scoring
    /// inspects authored results/guards and ending prerequisites; it never edits world or story facts.
    /// A target ending need not be reachable (construction gates and competing early endings remain).
    /// </summary>
    public sealed class NarrativeAutomationPlan
    {
        public string TargetEndingId { get; set; }
        /// <summary>Accepts neutral/church/rose/revolution, or the full route.* condition key.</summary>
        public string TargetRoute { get; set; }
        public Dictionary<string, string> ChoiceOverrides { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, double> ChoiceWeights { get; } = new Dictionary<string, double>(StringComparer.Ordinal);
        private NarrativeConfiguration indexedConfiguration;
        private Dictionary<string, List<NarrativeConditionDefinition>> conditionIndex;
        private Dictionary<string, List<NarrativeResultDefinition>> producerIndex;
        private Dictionary<string, List<Goal>> cachedGoals;
        private string cachedEnding, cachedRoute;
        private void EnsureIndex(NarrativeConfiguration configuration)
        {
            if (ReferenceEquals(indexedConfiguration, configuration)) return;
            indexedConfiguration = configuration;
            conditionIndex = configuration.conditions.Where(c => c != null).GroupBy(c => c.ownerType + "\n" + c.ownerId)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            producerIndex = configuration.results.Where(r => r != null).GroupBy(r => r.target ?? "")
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            cachedGoals = null;
        }
        private List<NarrativeConditionDefinition> Conditions(string type, string id)
            => conditionIndex.TryGetValue(type + "\n" + id, out var rows) ? rows : EmptyConditions;
        private static readonly List<NarrativeConditionDefinition> EmptyConditions = new List<NarrativeConditionDefinition>();

        /// <summary>Explicit authored choices for all formally enabled ending targets. No fixture history.</summary>
        public static NarrativeAutomationPlan ForEnding(NarrativeConfiguration configuration, string endingId)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (!configuration.endings.Any(e => e != null && e.enabled && e.endingId == endingId))
                throw new ArgumentException("Unknown or disabled ending: " + endingId, nameof(endingId));
            string route = endingId.StartsWith("neutral.", StringComparison.Ordinal) ? "neutral"
                : endingId.StartsWith("church.", StringComparison.Ordinal) ? "church"
                : endingId.StartsWith("rose.", StringComparison.Ordinal) ? "rose"
                : endingId.StartsWith("revolution.", StringComparison.Ordinal) ? "revolution" : null;
            var plan = ForRoute(configuration, route);
            plan.TargetEndingId = endingId;
            if (endingId == "arrival.ending.if0")
                plan.AddChoices(configuration, "MA70.10.06-S1-2.1", "MA70.10.06-S1-3.2");
            else if (endingId == "neutral.ending.a")
                plan.AddChoices(configuration, "church.1111.s1.choice.s1.1");
            else if (endingId.StartsWith("neutral.ending.", StringComparison.Ordinal))
            {
                plan.AddChoices(configuration, "neutral.route.1118.s1.choice.investigation.b");
                plan.AddChoices(configuration, endingId == "neutral.ending.b"
                    ? "neutral.route.1122.s1.choice.future.b" : "neutral.route.1122.s1.choice.future.a");
                plan.AddChoices(configuration, endingId == "neutral.ending.d"
                    ? "neutral.route.1112.s1.choice.membership.a" : "neutral.route.1112.s1.choice.membership.b");
            }
            else if (endingId == "church.ending.if4")
                plan.AddChoices(configuration, "church.1111.s1.choice.s1.2", "church.1111.s1.choice.s2.2");
            else if (endingId == "church.ending.ry1" || endingId == "church.ending.ry2")
                plan.AddChoices(configuration, endingId == "church.ending.ry1"
                    ? "church.route.1115.s1.choice.follow_thibaut.1" : "church.route.1115.s1.choice.follow_thibaut.2");
            else if (endingId == "revolution.end01")
                plan.AddChoices(configuration, "revolution.route.1124.s1.c0.1");
            else if (endingId == "revolution.end02")
                plan.AddChoices(configuration, "revolution.route.1124.s1.c0.2", "revolution.route.1124.s1.c1.1", "revolution.route.1124.s1.c2.1");
            // Rose/check-owned endings derive preference from actual terminal-result guards below;
            // a plan does not manipulate ability values to force their success/failure.
            return plan;
        }

        public static NarrativeAutomationPlan ForRoute(NarrativeConfiguration configuration, string route)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            route = RouteKey(route);
            var plan = new NarrativeAutomationPlan { TargetRoute = route };
            plan.AddChoices(configuration, "MA70.10.06-S1-2.2", "MA70.10.06-S1-3.1");
            // Explicit legal branch coverage: these pages evaluate checks consumed by later guards.
            // Host still has to supply faction.revolution >= -1 for 1014's language check page.
            plan.AddChoices(configuration, "revolution.1014.s1.c4.3", "revolution.1014.s1.c5.1", "revolution.1107.s1.c2.3");
            plan.AddChoices(configuration, "church.1111.s1.choice.s1.2", "church.1111.s1.choice.s2.2");
            if (route == "route.neutral") plan.AddChoices(configuration, "church.1111.s1.choice.s3.2");
            else
            {
                plan.AddChoices(configuration, "church.1111.s1.choice.s3.1");
                if (route == "route.church") plan.AddChoices(configuration, "church.1111.s1.choice.s4.1");
                if (route == "route.rose") plan.AddChoices(configuration, "church.1111.s1.choice.s4.2");
                if (route == "route.revolution") plan.AddChoices(configuration, "church.1111.s1.choice.s4.3");
            }
            return plan;
        }

        private void AddChoices(NarrativeConfiguration configuration, params string[] ids)
        {
            foreach (string id in ids)
            {
                var choice = configuration.choices.Find(c => c != null && c.choiceId == id);
                if (choice != null) ChoiceOverrides[choice.groupKey] = id;
            }
        }

        public string Select(NarrativeAutomationChoiceContext context)
        {
            if (ChoiceOverrides.TryGetValue(context.GroupKey, out string explicitId))
            {
                if (!context.Choices.Any(c => c.choiceId == explicitId))
                    throw new InvalidOperationException("Planned choice is locked or absent: " + explicitId);
                return explicitId;
            }
            return context.Scores.OrderByDescending(s => s.Score).ThenBy(s =>
                context.Choices.ToList().FindIndex(c => c.choiceId == s.ChoiceId)).First().ChoiceId;
        }

        public IReadOnlyList<NarrativeAutomationChoiceScore> ScoreChoices(NarrativeConfiguration configuration,
            NarrativeController controller, IReadOnlyList<NarrativeChoiceDefinition> choices)
        {
            EnsureIndex(configuration);
            var goals = BuildGoals(configuration);
            var scores = new List<NarrativeAutomationChoiceScore>();
            foreach (var choice in choices)
            {
                double score = ChoiceWeights.TryGetValue(choice.choiceId, out double custom) ? custom : 0;
                var reasons = new List<string>();
                if (custom != 0) reasons.Add("explicit weight " + custom);
                if (ChoiceOverrides.TryGetValue(choice.groupKey, out string planned) && planned == choice.choiceId)
                { score += 1000000; reasons.Add("explicit group override"); }
                string owner = string.IsNullOrWhiteSpace(choice.resultOwnerId) ? choice.choiceId : choice.resultOwnerId;
                var inspected = new HashSet<string>(StringComparer.Ordinal);
                foreach (var result in configuration.results)
                {
                    if (result == null) continue;
                    bool owned = result.eventId == owner;
                    var guards = Conditions("result", result.resultId);
                    bool linked = guards.Any(c => GuardMentions(c.target, choice.choiceId));
                    if (!owned && !linked) continue;
                    if (!CouldApply(guards, choice, controller)) continue;
                    ScoreResult(result, goals, ref score, reasons);
                    inspected.Add(result.resultId ?? "");
                }
                if (!string.IsNullOrWhiteSpace(choice.checkId))
                {
                    var check = configuration.abilityChecks.Find(x => x != null && x.checkId == choice.checkId);
                    if (check != null)
                    {
                        var snapshot = controller.InspectAbilityCheck(choice.checkId);
                        string checkOwner = snapshot.passed ? check.successResultOwnerId : check.failureResultOwnerId;
                        foreach (var result in configuration.results.Where(r => r != null && r.eventId == checkOwner && !inspected.Contains(r.resultId ?? "")))
                            if (CouldApply(Conditions("result", result.resultId), choice, controller))
                                ScoreResult(result, goals, ref score, reasons);
                        reasons.Add("check preview " + choice.checkId + " passed=" + snapshot.passed + " (no commit)");
                    }
                }
                scores.Add(new NarrativeAutomationChoiceScore { ChoiceId = choice.choiceId, Score = score, Reasons = reasons });
            }
            return scores;
        }

        // Back-propagate only declarative producer prerequisites, with decay and a hard depth cap.
        // Conflicting OR paths are advisory evidence, not a synthesized proof of reachability.
        private Dictionary<string, List<Goal>> BuildGoals(NarrativeConfiguration configuration)
        {
            if (cachedGoals != null && cachedEnding == TargetEndingId && cachedRoute == TargetRoute) return cachedGoals;
            var goals = new Dictionary<string, List<Goal>>(StringComparer.Ordinal);
            var frontier = new Queue<Goal>();
            void Add(string target, bool desired, double weight, int depth)
            {
                if (string.IsNullOrWhiteSpace(target)) return;
                if (!goals.TryGetValue(target, out var list)) goals[target] = list = new List<Goal>();
                if (list.Any(x => x.Desired == desired && x.Weight >= weight)) return;
                var goal = new Goal { Target = target, Desired = desired, Weight = weight, Depth = depth };
                list.Add(goal); frontier.Enqueue(goal);
            }
            foreach (var gate in Conditions("ending", TargetEndingId))
                if (gate.condType == "BOOL_EQ" && bool.TryParse(gate.value, out bool expected)) Add(gate.target, expected, 100, 0);
            string route = RouteKey(TargetRoute);
            if (route != null)
                foreach (string name in new[] { "route.neutral", "route.rose", "route.church", "route.revolution" }) Add(name, name == route, 80, 0);
            int expanded = 0;
            while (frontier.Count > 0 && expanded++ < 2048)
            {
                var goal = frontier.Dequeue();
                if (goal.Depth >= 5) continue;
                if (!producerIndex.TryGetValue(goal.Target, out var producers)) continue;
                foreach (var result in producers)
                {
                    if (!TryBoolResult(result, out bool produced) || produced != goal.Desired) continue;
                    foreach (var gate in Conditions("result", result.resultId).Concat(Conditions("event", result.eventId)))
                        if (gate.condType == "BOOL_EQ" && bool.TryParse(gate.value, out bool desired)
                            && !gate.target.StartsWith("choice:", StringComparison.Ordinal) && !gate.target.StartsWith("anyChoice:", StringComparison.Ordinal)
                            && !gate.target.StartsWith("not:", StringComparison.Ordinal) && !gate.target.StartsWith("bar:", StringComparison.Ordinal))
                            Add(gate.target, desired, goal.Weight * .4, goal.Depth + 1);
                }
            }
            cachedEnding = TargetEndingId; cachedRoute = TargetRoute; cachedGoals = goals;
            return goals;
        }
        private sealed class Goal { public string Target; public bool Desired; public double Weight; public int Depth; }
        private static void ScoreResult(NarrativeResultDefinition result, Dictionary<string, List<Goal>> goals, ref double score, List<string> reasons)
        {
            if (!goals.TryGetValue(result.target ?? "", out var matches) || !TryBoolResult(result, out bool value)) return;
            foreach (var goal in matches)
            {
                double delta = value == goal.Desired ? goal.Weight : -goal.Weight;
                score += delta;
                reasons.Add(result.resultId + ": " + result.target + "=" + value + " relevance=" + delta.ToString(CultureInfo.InvariantCulture));
            }
        }
        private static bool TryBoolResult(NarrativeResultDefinition result, out bool value)
        {
            value = false;
            if (result.resultType == NarrativeResultType.ClearFlag) return true;
            return (result.resultType == NarrativeResultType.SetFlag || result.resultType == NarrativeResultType.SetSwitch)
                && bool.TryParse(result.value, out value);
        }
        private static bool GuardMentions(string guard, string choice)
        {
            if (guard == null) return false;
            while (guard.StartsWith("not:", StringComparison.Ordinal)) guard = guard.Substring(4);
            return guard == "choice:" + choice || guard.StartsWith("anyChoice:", StringComparison.Ordinal) && guard.Substring(10).Split(';').Contains(choice);
        }
        private static bool CouldApply(List<NarrativeConditionDefinition> guards, NarrativeChoiceDefinition choice, NarrativeController controller)
        {
            if (guards.Count == 0) return true;
            return guards.GroupBy(c => c.groupIndex).Any(group => group.All(c =>
            {
                if (c.condType != "BOOL_EQ" || !bool.TryParse(c.value, out bool expected)) return false;
                return HypotheticalGuard(c.target, choice, controller) == expected;
            }));
        }
        private static bool? HypotheticalGuard(string guard, NarrativeChoiceDefinition choice, NarrativeController controller, int depth = 0)
        {
            if (guard == null || depth > 64) return null;
            if (guard.StartsWith("not:", StringComparison.Ordinal)) return !HypotheticalGuard(guard.Substring(4), choice, controller, depth + 1);
            if (guard.StartsWith("choice:", StringComparison.Ordinal))
                return guard.Substring(7) == choice.choiceId || controller.SelectedChoices.Contains(guard.Substring(7));
            if (guard.StartsWith("anyChoice:", StringComparison.Ordinal))
                return guard.Substring(10).Split(';').Any(id => id == choice.choiceId || controller.SelectedChoices.Contains(id));
            if (guard == "check:" + choice.checkId && !string.IsNullOrWhiteSpace(choice.checkId)) return controller.InspectAbilityCheck(choice.checkId).passed;
            try { return controller.ReadPresentationCondition(guard); }
            catch (InvalidOperationException) { return null; } // Unknown future evidence stays unknown even under not:.
        }
        private static string RouteKey(string value) => string.IsNullOrWhiteSpace(value) ? null : value.StartsWith("route.", StringComparison.Ordinal) ? value : "route." + value;

        public NarrativeAutomationPlanStatus Evaluate(NarrativeController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            var rows = controller.ExplainConditions("ending", TargetEndingId ?? "");
            double progress = rows.Count == 0 ? 0 : rows.GroupBy(c => c.GroupIndex).Max(g => (double)g.Count(c => c.Passed) / g.Count());
            return new NarrativeAutomationPlanStatus { TargetEndingId = TargetEndingId, SelectedEndingId = controller.SelectedEndingId,
                TargetReached = !string.IsNullOrWhiteSpace(TargetEndingId) && controller.SelectedEndingId == TargetEndingId,
                ConditionProgress = progress, Conditions = rows };
        }
    }
}
