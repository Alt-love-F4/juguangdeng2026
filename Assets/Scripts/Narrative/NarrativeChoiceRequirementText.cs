using System;
using System.Collections.Generic;
using System.Globalization;

namespace Spotlight.Narrative
{
    /// <summary>Player-facing requirements, never a dump of runtime condition identifiers.</summary>
    public static class NarrativeChoiceRequirementText
    {
        public const string Unavailable = "条件未满足";

        public static string Format(NarrativeConfiguration configuration, string choiceId, Func<string, string> resolve)
        {
            var groups = new Dictionary<int, List<string>>();
            foreach (var condition in configuration?.conditions ?? new List<NarrativeConditionDefinition>())
            {
                if (condition == null || condition.ownerType != "choice" || condition.ownerId != choiceId) continue;
                // Story flags and event/action guards are internal, not authored player hints.
                // If a group includes one, do not pretend its remaining numeric conditions are sufficient.
                string type = condition.condType?.Trim().ToUpperInvariant();
                if (condition.notFlag || !string.IsNullOrWhiteSpace(condition.op)
                    || (type != "BAR_GTE" && type != "BAR_LTE" && type != "REL_GTE")) return Unavailable;
                string target = condition.target ?? string.Empty;
                if (target.StartsWith("bar:", StringComparison.Ordinal) || target.StartsWith("rel:", StringComparison.Ordinal))
                    target = target.Substring(4);
                var bar = configuration.bars?.Find(b => b != null && b.barKey == target);
                string label = bar == null || string.IsNullOrWhiteSpace(bar.nameText) ? null : resolve?.Invoke(bar.nameText);
                if (string.IsNullOrWhiteSpace(label) || label == bar.nameText || label == target
                    || label.IndexOf('.') >= 0 || label.IndexOf(':') >= 0
                    || !int.TryParse(condition.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bound)) return Unavailable;
                if (!groups.TryGetValue(condition.groupIndex, out var descriptions))
                    groups.Add(condition.groupIndex, descriptions = new List<string>());
                descriptions.Add("需要 " + label + (type == "BAR_LTE" ? " 不高于 " : " 达到 ") + bound.ToString(CultureInfo.InvariantCulture));
            }
            if (groups.Count == 0) return Unavailable;
            var alternatives = new List<string>();
            foreach (var group in groups.Values) alternatives.Add(string.Join("，且", group));
            return string.Join("；或 ", alternatives);
        }
    }
}
