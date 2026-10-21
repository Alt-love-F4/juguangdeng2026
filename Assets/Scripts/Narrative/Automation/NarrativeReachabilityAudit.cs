using System;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    /// <summary>
    /// Configuration evidence only, not a reachability solver. Runtime/host writes, alternative
    /// condition groups and producer execution order are deliberately not inferred.
    /// </summary>
    public static class NarrativeReachabilityAudit
    {
        [Serializable]
        public sealed class Finding
        {
            public string owner;
            public string key;
            public string reason;
        }

        /// <summary>
        /// Returns findings in configuration order without modifying configuration or runtime state.
        /// Condition findings identify the referenced fact; their reason includes the condition ID.
        /// Event metadata findings use key="blockedBy" and retain the authored value in reason.
        /// A producer's presence suppresses a missing-producer hint, not proof it can execute.
        /// </summary>
        public static List<Finding> Inspect(NarrativeConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            var findings = new List<Finding>();
            var initiallyFalse = new HashSet<string>(StringComparer.Ordinal);
            var initiallyTrue = new HashSet<string>(StringComparer.Ordinal);
            var producers = new HashSet<string>(StringComparer.Ordinal);

            if (configuration.conditionKeys != null)
                foreach (var definition in configuration.conditionKeys)
                {
                    if (definition == null || string.IsNullOrWhiteSpace(definition.key)) continue;
                    (definition.initialValue ? initiallyTrue : initiallyFalse).Add(definition.key);
                }
            // Conflicting duplicate declarations are validation concerns, not evidence of false.
            initiallyFalse.ExceptWith(initiallyTrue);

            if (configuration.results != null)
                foreach (var result in configuration.results)
                {
                    if (result == null || (result.resultType != NarrativeResultType.SetFlag
                        && result.resultType != NarrativeResultType.SetSwitch)
                        || !bool.TryParse(result.value, out bool value) || !value) continue;
                    string target = result.target;
                    // Authored targets may use the runtime's flag:/switch: qualifiers.
                    string prefix = result.resultType == NarrativeResultType.SetFlag ? "flag:" : "switch:";
                    if (target != null && target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        target = target.Substring(prefix.Length);
                    if (!string.IsNullOrWhiteSpace(target)) producers.Add(target);
                }

            if (configuration.conditions != null)
                foreach (var condition in configuration.conditions)
                {
                    if (condition == null || condition.notFlag || !string.IsNullOrWhiteSpace(condition.op)
                        || !string.Equals(condition.condType?.Trim(), "BOOL_EQ", StringComparison.OrdinalIgnoreCase)
                        || !bool.TryParse(condition.value, out bool expected) || !expected
                        || !IsAuditedKey(condition.target) || !initiallyFalse.Contains(condition.target)
                        || producers.Contains(condition.target)) continue;
                    findings.Add(new Finding
                    {
                        owner = condition.ownerId,
                        key = condition.target,
                        reason = "Condition " + condition.conditionId + " (" + condition.ownerType
                            + ", group " + condition.groupIndex + ") requires BOOL_EQ true; the key is initially false"
                            + " and no SET_FLAG/SET_SWITCH true producer exists in configuration."
                            + " Evidence only; runtime writes and alternative groups are not evaluated."
                    });
                }

            if (configuration.events != null)
                foreach (var definition in configuration.events)
                {
                    if (definition?.eventProperties == null) continue;
                    foreach (var property in definition.eventProperties)
                    {
                        if (property == null || !string.Equals(property.property, "blockedBy", StringComparison.OrdinalIgnoreCase)
                            || string.IsNullOrWhiteSpace(property.value)) continue;
                        findings.Add(new Finding
                        {
                            owner = definition.eventId,
                            key = "blockedBy",
                            reason = "Authored eventProperties blockedBy: " + property.value
                                + ". Metadata evidence only; not an ending reachability verdict."
                        });
                    }
                }
            return findings;
        }

        private static bool IsAuditedKey(string key)
        {
            if (key == null) return false;
            return InFamily(key, "route.eligible") || InFamily(key, "construction")
                || InFamily(key, "integration") || InFamily(key, "blocked");
        }

        private static bool InFamily(string key, string family)
            => key == family || key.StartsWith(family + ".", StringComparison.Ordinal);
    }
}
