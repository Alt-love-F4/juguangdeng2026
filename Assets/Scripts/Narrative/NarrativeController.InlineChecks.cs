using System;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativeController
    {
        private readonly Dictionary<string, NarrativeAbilityCheckSnapshot> inlineAbilityChecks =
            new Dictionary<string, NarrativeAbilityCheckSnapshot>(StringComparer.Ordinal);

        public NarrativeAbilityCheckSnapshot EvaluateInlineAbilityCheckForPresentation(string checkId)
        {
            if (!activePresentation) throw new InvalidOperationException("Inline ability checks require an active narrative presentation.");
            var definition = configuration.abilityChecks.Find(x => x != null && x.checkId == checkId);
            if (definition == null || definition.mode != NarrativeAbilityCheckMode.Inline)
                throw new InvalidOperationException("Inline step requires an Inline ability check: " + checkId);
            if (!string.IsNullOrWhiteSpace(definition.successEventId) || !string.IsNullOrWhiteSpace(definition.failureEventId)
                || !string.IsNullOrWhiteSpace(definition.successResultOwnerId) || !string.IsNullOrWhiteSpace(definition.failureResultOwnerId))
                throw new InvalidOperationException("Inline check cannot own branch events/results: " + checkId);
            if (!inlineAbilityChecks.TryGetValue(checkId, out var snapshot))
            {
                snapshot = EvaluateAbilityCheck(checkId);
                if (snapshot.passed) PassiveCheckPresentationConfig.Hint(snapshot);
                inlineAbilityChecks.Add(checkId, snapshot);
                AbilityCheckEvaluated?.Invoke(CloneAbilityCheck(snapshot));
            }
            return CloneAbilityCheck(snapshot);
        }
    }
}
