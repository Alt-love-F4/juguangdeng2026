using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Narrative
{
    public sealed class NarrativeState
    {
        private readonly Dictionary<string, NarrativeBarDefinition> definitions;
        private readonly Dictionary<string, INarrativeValueProvider> providers = new Dictionary<string, INarrativeValueProvider>(StringComparer.Ordinal);
        internal void RegisterProvider(INarrativeValueProvider provider)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.ProviderId)) throw new ArgumentException("Invalid value provider.");
            if (providers.ContainsKey(provider.ProviderId)) throw new InvalidOperationException("Duplicate provider: " + provider.ProviderId);
            providers.Add(provider.ProviderId, provider);
        }
        internal void ClearProviders() => providers.Clear();
        public bool IsWritableBar(string key) => definitions.TryGetValue(ResolveRelation(key) ?? key ?? string.Empty, out var d)
            && d.sourceMode == NarrativeSourceMode.Ncs && d.advanceMode != NarrativeAdvanceMode.Derived;
        private readonly Dictionary<string, int> bars = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> reverseRelationBars = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> counters = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> firedTriggers = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> happenedEvents = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> flags = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> switches = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> objectStates = new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly Dictionary<string, NarrativeConditionKeyDefinition> conditionDefinitions = new Dictionary<string, NarrativeConditionKeyDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> conditions = new Dictionary<string, bool>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, bool> Conditions => conditions;
        public event Action ConditionsChanged;

        public bool TryGetCondition(string key, out bool value)
        {
            value = false;
            return key != null && conditions.TryGetValue(key, out value);
        }

        public bool GetCondition(string key)
        {
            if (!TryGetCondition(key, out bool value)) throw new ArgumentException("Unknown condition key: " + key);
            return value;
        }

        public void ValidateConditionWrite(string key, bool value)
        {
            if (key == null || !conditionDefinitions.TryGetValue(key, out var definition)) throw new ArgumentException("Unknown condition key: " + key);
            if (definition.onlySetTrue && !value) throw new InvalidOperationException("Condition only permits true: " + key);
        }

        public bool SetCondition(string key, bool value)
        {
            ValidateConditionWrite(key, value);
            if (conditions[key] == value) return false;
            conditions[key] = value;
            ConditionsChanged?.Invoke();
            return true;
        }

        internal void CommitConditions(IReadOnlyDictionary<string, bool> values)
        {
            bool changed = false;
            foreach (var pair in values)
            {
                if (conditions[pair.Key] == pair.Value) continue;
                conditions[pair.Key] = pair.Value;
                changed = true;
            }
            if (changed) ConditionsChanged?.Invoke();
        }

        public IReadOnlyDictionary<string, int> Bars
        {
            get
            {
                var values = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string key in definitions.Keys) if (TryGetBar(key, out int value)) values[key] = value;
                return values;
            }
        }
        public IReadOnlyCollection<string> HappenedEvents => happenedEvents;

        public NarrativeState(IReadOnlyList<NarrativeBarDefinition> definitions, IReadOnlyList<NarrativeConditionKeyDefinition> conditionKeys = null)
        {
            if (conditionKeys != null) foreach (var definition in conditionKeys)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.key) || conditionDefinitions.ContainsKey(definition.key)
                    || IsInternalFlag(definition.key)) throw new ArgumentException("Invalid or duplicate condition key: " + definition?.key);
                conditionDefinitions.Add(definition.key, definition);
                conditions.Add(definition.key, definition.initialValue);
            }
            this.definitions = new Dictionary<string, NarrativeBarDefinition>(StringComparer.Ordinal);
            if (definitions == null) return;
            for (int i = 0; i < definitions.Count; i++)
            {
                NarrativeBarDefinition definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.barKey)) continue;
                this.definitions.Add(definition.barKey, definition);
                if (definition.sourceMode == NarrativeSourceMode.External || definition.advanceMode == NarrativeAdvanceMode.Derived) continue;
                bars[definition.barKey] = definition.barKey == "world.calendar" ? definition.Clamp(Math.Max(1, definition.initValue)) : definition.Clamp(definition.initValue);
                if (definition.barType == NarrativeBarType.Relation)
                    reverseRelationBars[definition.barKey] = definition.Clamp(definition.reverseInitValue);
            }
        }

        public bool TryGetBar(string key, out int value)
        {
            if (TryResolveRelationDirection(key, out string relationKey, out bool reverse))
            {
                if (!reverse) return TryReadBar(relationKey, new HashSet<string>(StringComparer.Ordinal), out value);
                return reverseRelationBars.TryGetValue(relationKey, out value);
            }
            return TryReadBar(key, new HashSet<string>(StringComparer.Ordinal), out value);
        }
        private bool TryReadBar(string key, HashSet<string> visiting, out int value)
        {
            value = 0;
            if (key == null || !definitions.TryGetValue(key, out NarrativeBarDefinition definition)) return false;
            if (definition.sourceMode == NarrativeSourceMode.External)
                return providers.TryGetValue(definition.providerId ?? string.Empty, out var provider) && provider.IsReady && provider.TryRead(definition.sourceKey, out value);
            if (definition.advanceMode != NarrativeAdvanceMode.Derived) return bars.TryGetValue(key, out value);
            if (!visiting.Add(key)) return false;
            var sources = definition.derivedSources;
            if ((sources == null || sources.Count == 0) && !string.IsNullOrWhiteSpace(definition.derivedFormula))
                sources = new List<string>(definition.derivedFormula.Replace("sum:", "").Split(new[] { ',', '+' }, StringSplitOptions.RemoveEmptyEntries));
            if (sources == null || sources.Count == 0) { visiting.Remove(key); return false; }
            double sum = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(sources[i]) || !TryReadBar(sources[i].Trim(), visiting, out int source)) { visiting.Remove(key); return false; }
                sum += (double)source * (definition.derivedWeights != null && i < definition.derivedWeights.Count ? definition.derivedWeights[i] : 1);
            }
            visiting.Remove(key);
            value = definition.Clamp((int)Math.Max(int.MinValue, Math.Min(int.MaxValue, Math.Round(sum))));
            return true;
        }
        public IReadOnlyDictionary<string, string> NpcLocations => npcLocations;
        private readonly Dictionary<string, string> npcLocations = new Dictionary<string, string>(StringComparer.Ordinal);
        public bool IsEventLocked(string key) => IsFlagSet("event.locked." + key);
        public void SetEventLocked(string key, bool locked) => SetFlag("event.locked." + key, locked);
        public void SetNpcLocation(string key, string location) => npcLocations[key] = location ?? string.Empty;
        public string GetNpcLocation(string key) => npcLocations.TryGetValue(key, out string location) ? location : string.Empty;
        public string ResolveRelation(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (definitions.TryGetValue(key, out var exact)) return exact.barType == NarrativeBarType.Relation ? key : null;
            string[] pair = (key ?? string.Empty).Split('>');
            if (pair.Length != 2) return null;
            foreach (NarrativeBarDefinition definition in definitions.Values)
                if (definition.barType == NarrativeBarType.Relation &&
                    ((definition.ownerId == pair[0] && definition.targetId == pair[1]) ||
                     (definition.ownerId == pair[1] && definition.targetId == pair[0])))
                    return definition.barKey;
            return null;
        }

        private bool TryResolveRelationDirection(string key, out string relationKey, out bool reverse)
        {
            relationKey = null; reverse = false;
            if (string.IsNullOrWhiteSpace(key)) return false;
            string[] pair = key.Split('>');
            if (pair.Length != 2) return false;
            foreach (NarrativeBarDefinition definition in definitions.Values)
            {
                if (definition.barType != NarrativeBarType.Relation) continue;
                if (definition.ownerId == pair[0] && definition.targetId == pair[1]) { relationKey = definition.barKey; return true; }
                if (definition.ownerId == pair[1] && definition.targetId == pair[0]) { relationKey = definition.barKey; reverse = true; return true; }
            }
            return false;
        }
        public bool IsFlagSet(string key) => key != null && flags.TryGetValue(key, out bool value) && value;
        public bool IsSwitchOn(string key) => key != null && switches.TryGetValue(key, out bool value) && value;
        public bool HasFiredTrigger(string key) => firedTriggers.Contains(key);
        public bool HasHappenedEvent(string key) => happenedEvents.Contains(key);
        public int GetCounter(string key) => key != null && counters.TryGetValue(key, out int value) ? value : 0;
        public string GetObjectState(string key) => objectStates.TryGetValue(key, out string value) ? value : string.Empty;

        public bool TrySetBar(string key, int value, out int oldValue, out int newValue)
        {
            oldValue = 0;
            newValue = value;
            if (key == null) return false;
            bool relation = TryResolveRelationDirection(key, out string relationKey, out bool reverse);
            string storageKey = relation ? relationKey : key;
            if (!definitions.TryGetValue(storageKey, out NarrativeBarDefinition definition) || definition.sourceMode == NarrativeSourceMode.External || definition.advanceMode == NarrativeAdvanceMode.Derived) return false;
            if (reverse) reverseRelationBars.TryGetValue(storageKey, out oldValue); else bars.TryGetValue(storageKey, out oldValue);
            newValue = storageKey == "world.calendar" ? Math.Max(oldValue, definition.Clamp(Math.Max(1, value))) : definition.Clamp(value);
            if (definition.advanceMode == NarrativeAdvanceMode.Monotonic) newValue = Math.Max(oldValue, newValue);
            if (reverse) reverseRelationBars[storageKey] = newValue; else bars[storageKey] = newValue;
            return oldValue != newValue;
        }

        internal bool SynchronizeCalendar(int day, out int oldValue, out int newValue)
        {
            oldValue = newValue = 0;
            if (!definitions.TryGetValue("world.calendar", out var definition) || definition.sourceMode == NarrativeSourceMode.External) return false;
            oldValue = bars["world.calendar"];
            newValue = definition.Clamp(day);
            bars["world.calendar"] = newValue;
            return oldValue != newValue;
        }

        public bool TryAddBar(string key, int delta, out int oldValue, out int newValue)
        {
            oldValue = 0; newValue = 0;
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (!TryGetBar(key, out oldValue)) return false;
            long sum = (long)oldValue + delta;
            return TrySetBar(key, (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, sum)), out oldValue, out newValue);
        }

        public NarrativeBarDefinition GetDefinition(string key) => key != null && definitions.TryGetValue(key, out NarrativeBarDefinition definition) ? definition : null;
        public void SetCounter(string key, int value) => counters[key] = Mathf.Max(0, value);
        public void IncrementCounter(string key) => SetCounter(key, GetCounter(key) + 1);
        public void ResetCounter(string key) => counters.Remove(key);
        public void MarkTriggerFired(string key) => firedTriggers.Add(key);
        public void MarkEventHappened(string key) { if (happenedEvents.Add(key)) ConditionsChanged?.Invoke(); }
        // Internal scheduling/display bookkeeping only. Public story facts use SetCondition.
        public void SetFlag(string key, bool value)
        {
            if (string.IsNullOrWhiteSpace(key) || !IsInternalFlag(key)) throw new ArgumentException("Use SetCondition for public story facts: " + key);
            flags[key] = value;
        }
        private static bool IsInternalFlag(string key) => key.StartsWith("event.locked.", StringComparison.Ordinal)
            || key.StartsWith("event.once.", StringComparison.Ordinal) || key.StartsWith("trigger.once.", StringComparison.Ordinal)
            || key.StartsWith("date.once.", StringComparison.Ordinal) || key.StartsWith("achievement.", StringComparison.Ordinal);
        public void SetSwitch(string key, bool value) => switches[key] = value;
        public void SetObjectState(string key, string value) => objectStates[key] = value;

        public NarrativeSnapshot Capture()
        {
            var snapshot = new NarrativeSnapshot { conditionValues = new List<NarrativeBoolValue>() };
            foreach (var pair in conditions) snapshot.conditionValues.Add(new NarrativeBoolValue { key = pair.Key, value = pair.Value });
            foreach (KeyValuePair<string, int> pair in bars)
                if (definitions[pair.Key].advanceMode != NarrativeAdvanceMode.Derived) snapshot.bars.Add(new NarrativeBarValue { key = pair.Key, value = pair.Value, reverse = false });
            foreach (KeyValuePair<string, int> pair in reverseRelationBars)
                snapshot.bars.Add(new NarrativeBarValue { key = pair.Key, value = pair.Value, reverse = true });
            foreach (KeyValuePair<string, string> pair in npcLocations) snapshot.npcLocations.Add(new NarrativeStringValue { key = pair.Key, value = pair.Value });
            foreach (KeyValuePair<string, int> pair in counters) snapshot.counters.Add(new NarrativeCounterValue { key = pair.Key, value = pair.Value });
            foreach (string value in firedTriggers) snapshot.firedTriggers.Add(value);
            foreach (string value in happenedEvents) snapshot.happenedEvents.Add(value);
            foreach (KeyValuePair<string, bool> pair in flags) snapshot.flags.Add(new NarrativeBoolValue { key = pair.Key, value = pair.Value });
            foreach (KeyValuePair<string, bool> pair in switches) snapshot.switches.Add(new NarrativeBoolValue { key = pair.Key, value = pair.Value });
            foreach (KeyValuePair<string, string> pair in objectStates) snapshot.objectStates.Add(new NarrativeStringValue { key = pair.Key, value = pair.Value });
            return snapshot;
        }

        public void Restore(NarrativeSnapshot snapshot)
        {
            if (snapshot == null) return;
            bars.Clear(); reverseRelationBars.Clear(); counters.Clear(); firedTriggers.Clear(); happenedEvents.Clear(); flags.Clear(); switches.Clear(); objectStates.Clear(); npcLocations.Clear();
            foreach (NarrativeBarDefinition definition in definitions.Values)
            {
                if (definition.sourceMode == NarrativeSourceMode.External || definition.advanceMode == NarrativeAdvanceMode.Derived) continue;
                bars[definition.barKey] = definition.barKey == "world.calendar" ? definition.Clamp(Math.Max(1, definition.initValue)) : definition.Clamp(definition.initValue);
                if (definition.barType == NarrativeBarType.Relation)
                    reverseRelationBars[definition.barKey] = definition.Clamp(definition.reverseInitValue);
            }
            if (snapshot.bars != null) foreach (NarrativeBarValue value in snapshot.bars)
                if (value != null && value.key != null && definitions.ContainsKey(value.key) && definitions[value.key].sourceMode == NarrativeSourceMode.Ncs && definitions[value.key].advanceMode != NarrativeAdvanceMode.Derived)
                    if (value.reverse && definitions[value.key].barType == NarrativeBarType.Relation)
                        reverseRelationBars[value.key] = definitions[value.key].Clamp(value.value);
                    else
                        bars[value.key] = value.key == "world.calendar" ? definitions[value.key].Clamp(Math.Max(1, value.value)) : definitions[value.key].Clamp(value.value);
            if (snapshot.npcLocations != null) foreach (NarrativeStringValue value in snapshot.npcLocations) if (value != null && value.key != null) npcLocations[value.key] = value.value ?? string.Empty;
            if (snapshot.counters != null) foreach (NarrativeCounterValue value in snapshot.counters) if (value != null && !string.IsNullOrWhiteSpace(value.key)) counters[value.key] = Mathf.Max(0, value.value);
            if (snapshot.firedTriggers != null) foreach (string value in snapshot.firedTriggers) if (!string.IsNullOrWhiteSpace(value)) firedTriggers.Add(value);
            if (snapshot.happenedEvents != null) foreach (string value in snapshot.happenedEvents) if (!string.IsNullOrWhiteSpace(value)) happenedEvents.Add(value);
            conditions.Clear();
            foreach (var definition in conditionDefinitions.Values) conditions[definition.key] = definition.initialValue;
            if (snapshot.conditionValues != null)
            {
                foreach (var value in snapshot.conditionValues)
                    if (value != null && value.key != null && conditionDefinitions.ContainsKey(value.key)) conditions[value.key] = value.value;
            }
            if (snapshot.flags != null) foreach (NarrativeBoolValue value in snapshot.flags)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.key)) continue;
                if (IsInternalFlag(value.key)) flags[value.key] = value.value;
                else if (snapshot.conditionValues == null)
                {
                    string key = value.key.StartsWith("details.specialization.", StringComparison.Ordinal)
                        ? "specialization.player." + value.key.Substring("details.specialization.".Length) : value.key;
                    if (conditionDefinitions.ContainsKey(key)) conditions[key] = value.value;
                    else Debug.LogWarning("[NCS] Ignored undefined legacy condition: " + value.key);
                }
            }
            if (snapshot.switches != null) foreach (NarrativeBoolValue value in snapshot.switches) if (value != null && !string.IsNullOrWhiteSpace(value.key)) switches[value.key] = value.value;
            if (snapshot.objectStates != null) foreach (NarrativeStringValue value in snapshot.objectStates) if (value != null && !string.IsNullOrWhiteSpace(value.key)) objectStates[value.key] = value.value;
        }
    }
}
