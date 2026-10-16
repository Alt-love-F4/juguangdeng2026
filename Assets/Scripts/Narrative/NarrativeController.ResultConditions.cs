using System;
using System.Collections.Generic;
using System.Linq;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativeController
    {
        private bool EvaluateResultConditions(List<NarrativeConditionDefinition> conditions, NarrativeConditionContext context)
        {
            // Keep the existing OR-of-AND groups, but reject an unchosen branch before reading its other guards.
            foreach (var group in conditions.GroupBy(c => c.groupIndex))
            {
                bool passed = true;
                foreach (var condition in group.OrderBy(c => IsResultChoiceGuard(c) ? 0 : 1))
                    if (!EvaluateCondition(condition, context)) { passed = false; break; }
                if (passed) return true;
            }
            return false;
        }

        private static bool IsResultChoiceGuard(NarrativeConditionDefinition condition)
        {
            if (!string.Equals(condition.condType?.Trim(), "PRESENTATION", StringComparison.OrdinalIgnoreCase)) return false;
            string key = condition.target ?? string.Empty;
            while (key.StartsWith("not:", StringComparison.Ordinal)) key = key.Substring(4);
            return key.StartsWith("choice:", StringComparison.Ordinal) || key.StartsWith("anyChoice:", StringComparison.Ordinal);
        }

        private bool EvaluateResultPresentationCondition(NarrativeConditionDefinition condition)
        {
            if (!string.Equals(condition.ownerType, "result", StringComparison.OrdinalIgnoreCase)
                || !bool.TryParse(condition.value, out bool expected))
                throw new InvalidOperationException("PRESENTATION requires a result owner and true/false value: " + condition.conditionId);
            // An unknown/unvisited check remains unknown through not: and value=false; neither can award a failure result.
            return activePresentation && TryReadResultPresentationCondition(condition.target, out bool actual) && actual == expected;
        }

        private bool TryReadResultPresentationCondition(string key, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (key.StartsWith("not:", StringComparison.Ordinal))
            {
                if (!TryReadResultPresentationCondition(key.Substring(4), out bool inner)) return false;
                value = !inner;
                return true;
            }
            if (key.StartsWith("choice:", StringComparison.Ordinal))
            {
                string id = key.Substring(7);
                if (!configuration.choices.Exists(c => c != null && c.choiceId == id)) return false;
                value = selectedChoices.Contains(id);
                return true;
            }
            if (key.StartsWith("anyChoice:", StringComparison.Ordinal))
            {
                string[] ids = key.Substring(10).Split(';');
                if (ids.Any(id => !configuration.choices.Exists(c => c != null && c.choiceId == id))) return false;
                value = ids.Any(selectedChoices.Contains);
                return true;
            }
            if (key.StartsWith("check:", StringComparison.Ordinal))
            {
                string id = key.Substring(6);
                if (!configuration.abilityChecks.Exists(c => c != null && c.checkId == id && c.mode == NarrativeAbilityCheckMode.Inline)
                    || !inlineAbilityChecks.TryGetValue(id, out var snapshot) || snapshot == null) return false;
                value = snapshot.passed;
                return true;
            }
            if (key.StartsWith("checkpointStage:", StringComparison.Ordinal))
            {
                if (!int.TryParse(key.Substring(16), out int stage) || stage < 0) return false;
                value = CurrentCheckpointStage == stage;
                return true;
            }
            if (key == "service:passed") return TryReadServiceResultGuard(key, out value);
            return state.TryGetCondition(key, out value);
        }
    }
}
