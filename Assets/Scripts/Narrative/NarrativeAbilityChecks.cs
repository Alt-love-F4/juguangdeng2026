using System;
using System.Collections.Generic;
using System.Linq;

namespace Spotlight.Narrative
{
    [Serializable]
    public sealed class NarrativeAbilityCheckDefinition
    {
        public string checkId;
        public string successEventId;
        public string failureEventId;
        public string successResultOwnerId;
        public string failureResultOwnerId;
    }

    [Serializable]
    public sealed class NarrativeAbilityCheckConditionDefinition
    {
        public string conditionId;
        public string checkId;
        public string attributeId;
        public int threshold;
        public int groupIndex;
        public int seq;
    }

    [Serializable]
    public sealed class NarrativeAbilityCheckValue
    {
        public string attributeId;
        public string displayName;
        public int currentValue;
        public int threshold;
        public int groupIndex;
        public bool passed;
    }

    [Serializable]
    public sealed class NarrativeAbilityCheckSnapshot
    {
        public string checkId;
        public bool passed;
        public bool branchQueued;
        public bool resultApplied;
        public List<NarrativeAbilityCheckValue> values = new List<NarrativeAbilityCheckValue>();
    }

    public static class NarrativeAbilityCheckEvaluator
    {
        public static NarrativeAbilityCheckSnapshot Evaluate(NarrativeAbilityCheckDefinition definition,
            IReadOnlyList<NarrativeAbilityCheckConditionDefinition> conditions, Func<string, (bool ready, int value)> read, Func<string, string> resolveName = null)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.checkId)) throw new ArgumentException("Invalid ability check.");
            if (conditions == null || conditions.Count == 0) throw new ArgumentException("Ability check has no conditions: " + definition.checkId);
            if (read == null) throw new InvalidOperationException("Player attribute reader is unavailable.");
            var snapshot = new NarrativeAbilityCheckSnapshot { checkId = definition.checkId };
            var groups = new Dictionary<int, bool>();
            foreach (var condition in conditions.OrderBy(x => x == null ? int.MaxValue : x.groupIndex).ThenBy(x => x == null ? int.MaxValue : x.seq))
            {
                if (condition == null || condition.groupIndex < 1 || string.IsNullOrWhiteSpace(condition.attributeId))
                    throw new InvalidOperationException("Invalid condition in ability check " + definition.checkId);
                var value = read(condition.attributeId);
                if (!value.ready) throw new InvalidOperationException("Player attribute is unavailable: " + condition.attributeId);
                bool passed = value.value >= condition.threshold;
                snapshot.values.Add(new NarrativeAbilityCheckValue { attributeId = condition.attributeId, displayName = resolveName?.Invoke(condition.attributeId) ?? condition.attributeId, currentValue = value.value,
                    threshold = condition.threshold, groupIndex = condition.groupIndex, passed = passed });
                groups[condition.groupIndex] = !groups.TryGetValue(condition.groupIndex, out bool prior) || prior && passed;
            }
            snapshot.passed = groups.Values.Any(x => x);
            return snapshot;
        }
    }
}
