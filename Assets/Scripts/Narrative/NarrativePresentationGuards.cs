using System;
using System.Globalization;
using System.Linq;

namespace Spotlight.Narrative
{
    /// <summary>The small declarative presentation guard vocabulary, shared by runtime and validators.</summary>
    public static class NarrativePresentationGuards
    {
        public static bool TryParseNumeric(string guard, out string bar, out string op, out int bound)
        {
            bar = op = null; bound = 0;
            var parts = (guard ?? "").Split(':');
            if (parts.Length != 4 || parts[0] != "bar" || string.IsNullOrWhiteSpace(parts[1])
                || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out bound)) return false;
            bar = parts[1]; op = parts[2];
            return op == "ge" || op == "gt" || op == "le" || op == "lt" || op == "eq" || op == "ne";
        }
        public static bool Compare(int value, string op, int bound)
        {
            switch (op)
            {
                case "ge": return value >= bound; case "gt": return value > bound;
                case "le": return value <= bound; case "lt": return value < bound;
                case "eq": return value == bound; case "ne": return value != bound;
                default: throw new InvalidOperationException("Unknown guard operator: " + op);
            }
        }
        public static bool TryParseGameplay(string guard, out string pageId)
        {
            pageId = null;
            if (guard == null || guard.Length <= 16 || !guard.StartsWith("gameplay:", StringComparison.Ordinal) || !guard.EndsWith(":passed", StringComparison.Ordinal)) return false;
            pageId = guard.Substring(9, guard.Length - 9 - 7);
            return !string.IsNullOrWhiteSpace(pageId) && pageId.IndexOf(':') < 0;
        }
        /// <summary>Action eligibility can read current world facts, never another root's transient evidence.</summary>
        public static bool IsReadOnlyWorldGuard(string guard)
        {
            if (string.IsNullOrWhiteSpace(guard)) return false;
            guard = guard.Trim();
            int depth = 0;
            while (guard.StartsWith("not:", StringComparison.Ordinal))
            {
                if (++depth > 64) return false;
                guard = guard.Substring(4).Trim();
            }
            if (string.IsNullOrWhiteSpace(guard)) return false;
            if (guard.StartsWith("bar:", StringComparison.Ordinal)) return TryParseNumeric(guard, out _, out _, out _);
            return !guard.StartsWith("choice:", StringComparison.Ordinal)
                && !guard.StartsWith("anyChoice:", StringComparison.Ordinal)
                && !guard.StartsWith("check:", StringComparison.Ordinal)
                && !guard.StartsWith("gameplay:", StringComparison.Ordinal)
                && !guard.StartsWith("dependency:", StringComparison.Ordinal);
        }
        public static string Validate(NarrativeConfiguration c, string guard, int depth = 0, bool allowTransient = true)
        {
            guard = guard?.Trim();
            if (string.IsNullOrWhiteSpace(guard) || depth > 64) return "empty or over-nested presentation guard.";
            if (guard.StartsWith("not:", StringComparison.Ordinal)) return Validate(c, guard.Substring(4), depth + 1, allowTransient);
            if (guard.StartsWith("bar:", StringComparison.Ordinal))
            {
                if (!TryParseNumeric(guard, out string bar, out _, out _)) return "invalid numeric guard " + guard;
                return c.bars.Any(b => b != null && b.barKey == bar) ? null : "unknown guard bar " + bar;
            }
            if (!allowTransient && !IsReadOnlyWorldGuard(guard)) return "action BOOL_EQ requires a read-only bool/numeric world guard, not " + guard;
            if (guard.StartsWith("gameplay:", StringComparison.Ordinal))
                return TryParseGameplay(guard, out string page) && c.presentations.conversationPages.Any(p => p != null && p.pageId == page && p.stepType == NarrativeConversationStepType.Gameplay)
                    ? null : "unknown or invalid Gameplay guard " + guard;
            if (guard.StartsWith("choice:", StringComparison.Ordinal))
                return c.choices.Any(x => x != null && x.choiceId == guard.Substring(7)) ? null : "unknown guard choice " + guard;
            if (guard.StartsWith("anyChoice:", StringComparison.Ordinal))
                return guard.Substring(10).Split(';').All(id => c.choices.Any(x => x != null && x.choiceId == id)) ? null : "unknown anyChoice guard " + guard;
            if (guard.StartsWith("check:", StringComparison.Ordinal))
                return c.abilityChecks.Any(x => x != null && x.checkId == guard.Substring(6) && x.mode == NarrativeAbilityCheckMode.Inline) ? null : "unknown Inline guard check " + guard;
            if (guard.StartsWith("dependency:", StringComparison.Ordinal) && guard.Length > 11) return null;
            return c.conditionKeys.Any(x => x != null && x.key == guard) ? null : "unknown bool presentation guard " + guard;
        }
    }
}
