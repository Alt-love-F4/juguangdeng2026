using System;
using System.Collections.Generic;
using System.Linq;

namespace Spotlight.Narrative
{
    internal sealed class NarrativeActionRootCommit
    {
        public IReadOnlyList<NarrativeActionRequirement> costs;
        public NarrativeActionPoints points;
        public Action committed;
        public Func<bool> canCommit;
        public string resultOwner;
    }

    public sealed partial class NarrativeController
    {
        internal Func<string, NarrativeActionRootCommit> ActionRootCommit;

        private static List<NarrativeResultDefinition> ActionCostResults(IReadOnlyList<NarrativeActionRequirement> costs)
        {
            var results = new List<NarrativeResultDefinition>();
            foreach (var cost in costs ?? Array.Empty<NarrativeActionRequirement>())
            {
                if (cost.type == NarrativeActionRequirementType.ActionPoint) continue;
                results.Add(new NarrativeResultDefinition { resultId = "action.cost", eventId = "action.cost",
                    resultType = cost.type == NarrativeActionRequirementType.Item ? NarrativeResultType.ItemRemove : NarrativeResultType.MoneyRemove,
                    target = cost.type == NarrativeActionRequirementType.Item ? cost.key : "cafe", value = cost.value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            return results;
        }

        /// <summary>Uses the same Result preflight as event roots. Standalone trades support only
        /// inventory and the authoritative wallet; no presentation or world effects may follow payment.</summary>
        internal bool TryCommitActionBatch(string resultOwner, IReadOnlyList<NarrativeActionRequirement> costs,
            NarrativeActionPoints points, Action committed, out string error)
        {
            error = null;
            if (IsEnded || !ValuesReady || settling) { error = "Action settlement is unavailable."; return false; }
            var batch = new List<NarrativeResultDefinition>();
            long pointCost = 0;
            foreach (var cost in costs ?? Array.Empty<NarrativeActionRequirement>())
            {
                if (cost == null || cost.value <= 0) { error = "Invalid action cost."; return false; }
                if (cost.type == NarrativeActionRequirementType.ActionPoint) { pointCost += cost.value; continue; }
                if (cost.type != NarrativeActionRequirementType.Item && cost.type != NarrativeActionRequirementType.Money)
                { error = "Unsupported action cost."; return false; }
                batch.Add(new NarrativeResultDefinition { resultId = "action.cost", eventId = resultOwner,
                    resultType = cost.type == NarrativeActionRequirementType.Item ? NarrativeResultType.ItemRemove : NarrativeResultType.MoneyRemove,
                    target = cost.type == NarrativeActionRequirementType.Item ? cost.key : "cafe", value = cost.value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            int pointBalanceBefore = points?.Current ?? 0;
            if (points == null || pointCost > points.Current) { error = "Insufficient action points."; return false; }
            if (!string.IsNullOrWhiteSpace(resultOwner))
            {
                if (!resultsByEvent.TryGetValue(resultOwner, out var results) || results.Count == 0)
                { error = "Unknown action result owner: " + resultOwner; return false; }
                foreach (var result in results)
                {
                    if (result.resultType != NarrativeResultType.ItemAdd && result.resultType != NarrativeResultType.ItemRemove
                        && result.resultType != NarrativeResultType.MoneyAdd && result.resultType != NarrativeResultType.MoneyRemove)
                    { error = "Exchange requires inventory or money results: " + result.resultId; return false; }
                    if (EvaluateConditions("result", result.resultId)) batch.Add(result);
                }
            }
            bool committedBatch = false;
            try
            {
                // Costs precede output, so an incoming item cannot finance its own input.
                PreflightResults(batch, out _, out var counts, out int? moneyBefore, out int? moneyAfter);
                if (moneyBefore.HasValue && !(MoneyAccount is INarrativeMoneyNotificationScope))
                    throw new InvalidOperationException("Money provider does not support a transaction notification scope.");
                if (counts.Count > 0 && inventory == null) throw new InvalidOperationException("Inventory is unavailable.");
                BeginOperation();
                try
                {
                    // The wallet compare-and-commit is the sole fallible write. Final map counts
                    // are validated first, then applied by the sealed inventory in one batch.
                    using ((MoneyAccount as INarrativeMoneyNotificationScope)?.DeferNotifications())
                    using (inventory?.DeferNotifications())
                    using (points.DeferNotifications())
                    {
                        if (moneyBefore.HasValue && !MoneyAccount.TryCommit(moneyBefore.Value, moneyAfter.Value, out string failure))
                            throw new InvalidOperationException("Money batch rejected: " + failure);
                        int pointsBefore = points.Current;
                        if (pointsBefore < pointCost)
                        {
                            if (moneyBefore.HasValue && !MoneyAccount.TryCommit(moneyAfter.Value, moneyBefore.Value, out string restoreError))
                                throw new InvalidOperationException("Cannot restore rejected action wallet: " + restoreError);
                            points.Set(pointBalanceBefore);
                            throw new InvalidOperationException("Action points changed during wallet commit.");
                        }
                        if (counts.Count > 0 && !inventory.TryApplyCounts(counts))
                            throw new InvalidOperationException("Inventory rejected validated counts.");
                        points.TryConsume((int)pointCost);
                        committed?.Invoke();
                        NotifyConditionsChanged();
                        committedBatch = true;
                    }
                }
                finally { EndOperation(); }
                return true;
            }
            catch (Exception failure)
            {
                if (committedBatch)
                {
                    UnityEngine.Debug.LogException(failure);
                    return true; // Notification failures cannot turn an already committed receipt into rejection.
                }
                error = failure.Message; return false;
            }
        }

        public IReadOnlyList<NarrativeResultDefinition> GetActionResults(string owner)
            => !string.IsNullOrWhiteSpace(owner) && resultsByEvent.TryGetValue(owner, out var results)
                ? results.ToArray() : Array.Empty<NarrativeResultDefinition>();
    }
}
