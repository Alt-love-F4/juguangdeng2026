using System;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    /// <summary>Persistent map inventory shared by requirement checks, UI and rewards.</summary>
    public sealed class NarrativeMapInventory : INarrativeActionInventory
    {
        private readonly Dictionary<string, int> items = new Dictionary<string, int>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, int> Items => items;
        public event Action Changed;
        private int notificationDepth;
        private bool changed;

        public IDisposable DeferNotifications()
        {
            notificationDepth++;
            return new NotificationScope(this);
        }

        private sealed class NotificationScope : IDisposable
        {
            private NarrativeMapInventory owner;
            public NotificationScope(NarrativeMapInventory owner) { this.owner = owner; }
            public void Dispose()
            {
                if (owner == null) return;
                var target = owner; owner = null;
                if (--target.notificationDepth == 0 && target.changed)
                {
                    target.changed = false;
                    target.Changed?.Invoke();
                }
            }
        }

        private void NotifyChanged()
        {
            if (notificationDepth > 0) changed = true;
            else Changed?.Invoke();
        }

        /// <summary>Atomically sets the supplied final quantities, preserving other keys.</summary>
        public bool TryApplyCounts(IReadOnlyDictionary<string, int> counts)
        {
            if (counts == null) return false;
            foreach (var pair in counts)
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0) return false;
            bool any = false;
            foreach (var pair in counts)
            {
                if (GetCount(pair.Key) == pair.Value) continue;
                items[pair.Key] = pair.Value;
                any = true;
            }
            if (any) NotifyChanged();
            return true;
        }

        public bool TryConsume(IReadOnlyDictionary<string, int> costs)
        {
            if (costs == null) return false;
            var next = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in costs)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0 || GetCount(pair.Key) < pair.Value) return false;
                next[pair.Key] = GetCount(pair.Key) - pair.Value;
            }
            return TryApplyCounts(next);
        }
        public int GetCount(string key) => key != null && items.TryGetValue(key, out int count) ? count : 0;
        public bool TryConsume(string key, int count)
        {
            if (string.IsNullOrWhiteSpace(key) || count <= 0 || GetCount(key) < count) return false;
            items[key] -= count;
            NotifyChanged();
            return true;
        }
        public bool TryAdd(string key, int count)
        {
            if (string.IsNullOrWhiteSpace(key) || count <= 0 || (long)GetCount(key) + count > int.MaxValue) return false;
            items[key] = GetCount(key) + count;
            NotifyChanged();
            return true;
        }
        public List<NarrativeActionCounterValue> Capture()
        {
            var result = new List<NarrativeActionCounterValue>();
            foreach (var pair in items) result.Add(new NarrativeActionCounterValue { key = pair.Key, value = pair.Value });
            return result;
        }
        public void Restore(List<NarrativeActionCounterValue> values)
        {
            items.Clear();
            if (values != null) foreach (var value in values)
                if (value != null && !string.IsNullOrWhiteSpace(value.key)) items[value.key] = Math.Max(0, value.value);
            NotifyChanged();
        }
    }
}
