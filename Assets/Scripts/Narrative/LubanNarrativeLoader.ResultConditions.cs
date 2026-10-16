using System;
using System.Linq;

namespace Spotlight.Narrative
{
    public static partial class LubanNarrativeLoader
    {
        private static bool IsResultPresentationTarget(NarrativeConfiguration c, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            while (key.StartsWith("not:", StringComparison.Ordinal)) key = key.Substring(4);
            if (key.StartsWith("choice:", StringComparison.Ordinal))
                return c.choices.Any(x => x != null && x.choiceId == key.Substring(7));
            if (key.StartsWith("anyChoice:", StringComparison.Ordinal))
                return key.Substring(10).Split(';').All(id => c.choices.Any(x => x != null && x.choiceId == id));
            if (key.StartsWith("check:", StringComparison.Ordinal))
                return c.abilityChecks.Any(x => x != null && x.checkId == key.Substring(6) && x.mode == NarrativeAbilityCheckMode.Inline);
            if (key.StartsWith("checkpointStage:", StringComparison.Ordinal))
                return int.TryParse(key.Substring(16), out int stage) && stage >= 0;
            if (key == "service:passed") return true;
            return c.conditionKeys.Any(x => x != null && x.key == key);
        }
    }
}
