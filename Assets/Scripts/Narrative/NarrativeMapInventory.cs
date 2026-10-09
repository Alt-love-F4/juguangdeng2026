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
        public int GetCount(string key) => key != null && items.TryGetValue(key, out int count) ? count : 0;
        public bool TryConsume(string key, int count)
        {
            if (string.IsNullOrWhiteSpace(key) || count <= 0 || GetCount(key) < count) return false;
            items[key] -= count;
            Changed?.Invoke();
            return true;
        }
        public bool TryAdd(string key, int count)
        {
            if (string.IsNullOrWhiteSpace(key) || count <= 0 || (long)GetCount(key) + count > int.MaxValue) return false;
            items[key] = GetCount(key) + count;
            Changed?.Invoke();
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
            Changed?.Invoke();
        }
    }
}
