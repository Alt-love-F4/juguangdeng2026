using System;
using System.Collections.Generic;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// Item identity until a dedicated item table exists. Configuration references declare
    /// known items even at zero quantity; the current owner's keys preserve saved/runtime items.
    /// Construct once per configuration/session. Never copies inventory quantities.
    /// </summary>
    public sealed class PlayerItemCatalog
    {
        private readonly HashSet<string> configuredIds = new HashSet<string>(StringComparer.Ordinal);

        public PlayerItemCatalog(NarrativeConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            foreach (var action in configuration.actions)
            {
                // Match the action controller: parsed requirements are authoritative when populated.
                var requirements = action.parsedRequirements != null && action.parsedRequirements.Count > 0
                    ? action.parsedRequirements : NarrativeActionRequirementParser.Parse(action.requirements);
                foreach (var requirement in requirements)
                    if (requirement.type == NarrativeActionRequirementType.Item)
                        Add(requirement.key, "action " + action.actionKey);
            }
            foreach (var result in configuration.results)
                if (result.resultType == NarrativeResultType.ItemAdd || result.resultType == NarrativeResultType.ItemRemove)
                    Add(result.target, "result " + result.resultId);
            foreach (var condition in configuration.conditions)
                if (string.Equals(condition.condType, "ITEM_GTE", StringComparison.Ordinal))
                    Add(condition.target, "condition " + condition.conditionId);
        }

        /// <summary>
        /// Pass the currently bound inventory, not a captured prior session. A known key remains
        /// valid after consumption to zero. Missing ownership/readiness is checked by the caller.
        /// </summary>
        public bool IsKnownItem(string key, NarrativeMapInventory inventory = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return configuredIds.Contains(key) || (inventory != null && inventory.Items.ContainsKey(key));
        }

        private void Add(string key, string source)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Empty item ID referenced by " + source + ".");
            configuredIds.Add(key);
        }
    }
}
