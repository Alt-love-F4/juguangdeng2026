using System;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativeController
    {
        private readonly List<KeyValuePair<INarrativeValueProvider, Action>> valueSubscriptions = new List<KeyValuePair<INarrativeValueProvider, Action>>();
        private readonly Queue<List<NarrativeBarChange>> settledValueChanges = new Queue<List<NarrativeBarChange>>();
        private Dictionary<string, int> observedValues, operationValues;
        private readonly Dictionary<string, NarrativeBarChange> operationDirectionalChanges = new Dictionary<string, NarrativeBarChange>(StringComparer.Ordinal);
        private bool valueRestore, valueBaselineReady = true, flushingValues;
        private int valueBindingEpoch;
        private bool restoredValueChanges;
        public INarrativeMoneyAccount MoneyAccount { get; set; }
        public bool ValuesReady => !valueRestore && valueBaselineReady;

        public void BindValueProvider(INarrativeValueProvider provider)
        {
            state.RegisterProvider(provider);
            int epoch = valueBindingEpoch;
            Action handler = () =>
            {
                if (epoch != valueBindingEpoch || valueRestore || IsEnded) return;
                if (!valueBaselineReady) throw new InvalidOperationException("Establish the narrative value baseline before gameplay.");
                if (operationDepth > 0) return;
                CommitValueComparison(observedValues);
                // Do not enter the presenter recursively from an owner callback.
            };
            provider.Changed += handler;
            valueSubscriptions.Add(new KeyValuePair<INarrativeValueProvider, Action>(provider, handler));
            valueBaselineReady = false;
        }

        public void UnbindValueProviders()
        {
            valueBindingEpoch++;
            foreach (var entry in valueSubscriptions) entry.Key.Changed -= entry.Value;
            valueSubscriptions.Clear();
            state.ClearProviders();
            observedValues = null;
            valueBaselineReady = false;
        }

        public void BeginValueRestore()
        {
            if (operationDepth != 0 || settling) throw new InvalidOperationException("Cannot restore during a narrative operation.");
            if (valueRestore) return;
            valueRestore = true;
            valueBaselineReady = false;
            changes.Clear();
            settledValueChanges.Clear();
        }

        public void CompleteValueRestore(bool evaluateInitial = true)
        {
            observedValues = ReadAllValues();
            if (!restoredValueChanges) changes.Clear();
            restoredValueChanges = false;
            settledValueChanges.Clear();
            valueRestore = false;
            valueBaselineReady = true;
            endingDirty = true;
            if (evaluateInitial)
            {
                EvaluateBoundary(0, true);
                ProcessQueue();
                CheckEndingsAtSafeBoundary();
            }
        }

        private Dictionary<string, int> ReadAllValues()
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var bar in configuration.bars)
            {
                if (!state.TryGetBar(bar.barKey, out int value))
                {
                    valueBaselineReady = false;
                    throw new InvalidOperationException("Narrative value unavailable: " + bar.barKey + " (provider=" + bar.providerId + ", key=" + bar.sourceKey + ")");
                }
                result.Add(bar.barKey, value);
            }
            return result;
        }

        private void CommitValueComparison(Dictionary<string, int> before)
        {
            var after = ReadAllValues();
            var batch = new List<NarrativeBarChange>();
            if (before != null) foreach (var pair in before)
                if (after.TryGetValue(pair.Key, out int next) && next != pair.Value)
                    batch.Add(new NarrativeBarChange { key = pair.Key, oldValue = pair.Value, newValue = next, depth = transactionDepth });
            foreach (var change in operationDirectionalChanges.Values)
                if (change.oldValue != change.newValue) batch.Add(change);
            operationDirectionalChanges.Clear();
            observedValues = after;
            if (batch.Count > 0) { settledValueChanges.Enqueue(batch); endingDirty = true; }
        }

        private void DrainSettledValueChanges()
        {
            if (valueRestore || !valueBaselineReady || operationDepth > 0 || activePresentation || draining || queue.Count > 0) return;
            if (settledValueChanges.Count > 0)
            {
                if (!turnEnding) pendingEvents.Clear();
                var batch = settledValueChanges.Dequeue();
                int depth = 0;
                foreach (var change in batch)
                {
                    changes.Add(change);
                    depth = Math.Max(depth, change.depth);
                    NotifyConditionsChanged();
                    try { BarChanged?.Invoke(change.key, change.oldValue, change.newValue); }
                    catch (Exception error) { UnityEngine.Debug.LogException(error); }
                }
                EvaluateBoundary(depth, false);
            }
        }

        /// <summary>Call at a safe owner-operation boundary / existing session Update; never scans values.</summary>
        public void FlushValueChanges()
        {
            if (flushingValues || valueRestore || operationDepth > 0 || settling || activePresentation || draining) return;
            flushingValues = true;
            try { DrainSettledValueChanges(); ProcessQueue(); }
            finally { flushingValues = false; }
        }
    }
}
