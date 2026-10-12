using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spotlight.Narrative
{
    public sealed class NarrativeController
    {
        private readonly NarrativeConfiguration configuration;
        private readonly NarrativeState state;
        private readonly Dictionary<string, List<NarrativeTriggerDefinition>> triggersByBar = new Dictionary<string, List<NarrativeTriggerDefinition>>(StringComparer.Ordinal);
        private readonly Dictionary<string, NarrativeEventDefinition> events = new Dictionary<string, NarrativeEventDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<NarrativeResultDefinition>> resultsByEvent = new Dictionary<string, List<NarrativeResultDefinition>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<NarrativeConditionDefinition>> conditionsByOwner = new Dictionary<string, List<NarrativeConditionDefinition>>(StringComparer.Ordinal);
        private readonly Queue<NarrativeEventRequest> queue = new Queue<NarrativeEventRequest>();
        private readonly HashSet<string> pendingEvents = new HashSet<string>(StringComparer.Ordinal);
        private readonly INarrativePresenter presenter;
        private bool processing;
        private bool activePresentation;
        private int transactionDepth;
        private bool turnOpen, turnEnding, pumping, settling, resumeBoundary;
        private long presentationEpoch;
        private bool draining;
        private readonly List<NarrativeResultDefinition> pendingEffects = new List<NarrativeResultDefinition>();
        private NarrativeEventRequest activeRequest;
        private readonly List<NarrativeBarChange> changes = new List<NarrativeBarChange>();
        private readonly List<string> selectedChoices = new List<string>();
        private readonly Dictionary<NarrativeTriggerDefinition, int> triggerRows = new Dictionary<NarrativeTriggerDefinition, int>();
        private uint randomSeed = 0x6D2B79F5;
        public string Location { get; set; } = string.Empty;
        public NarrativeTimeSlot TimeSlot { get; set; }
        public IReadOnlyList<string> SelectedChoices => selectedChoices;
        public event Action<NarrativeEffectNotification> EffectRequested;
        public event Action TurnCompleted;

        public NarrativeState State => state;
        public bool IsBusy => processing || activePresentation || draining || queue.Count > 0 || turnEnding;
        public event Action<NarrativeEventRequest> EventQueued;
        public event Action<string, int, int> BarChanged;
        public event Action<string> EventCompleted;

        public NarrativeController(NarrativeConfiguration configuration, INarrativePresenter presenter = null)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            state = new NarrativeState(configuration.bars);
            this.presenter = presenter ?? new ImmediateNarrativePresenter();
            BuildIndexes();
        }

        public bool QueueEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId) || !events.TryGetValue(eventId, out NarrativeEventDefinition definition)) return false;
            if (activePresentation || draining || pendingEvents.Contains(eventId) || state.IsEventLocked(eventId) || state.IsFlagSet("event.once." + eventId) || definition.onceOnly && state.HasHappenedEvent(eventId)) return false;
            if (!EvaluateConditions("event", string.IsNullOrWhiteSpace(definition.conditionOwnerId) ? eventId : definition.conditionOwnerId, Context(definition))) return false;
            if (!turnOpen) BeginTurn();
            pendingEvents.Add(eventId);
            var request = new NarrativeEventRequest(definition, null, 0, null, Context(definition));
            queue.Enqueue(request);
            Debug.Log($"[NCS][ManualEvent] event={eventId} tag={definition.logTag}");
            try { EventQueued?.Invoke(request); }
            catch (Exception error) { Debug.LogException(error); }
            ProcessQueue();
            return true;
        }

        public void BeginTurn()
        {
            if (turnOpen || IsBusy) return;
            turnOpen = true;
            transactionDepth = 0;
            pendingEvents.Clear();
        }

        public void EndTurn()
        {
            if (turnEnding) return;
            if (!turnOpen) BeginTurn();
            if (!turnOpen) return;
            turnEnding = true;
            if (!resumeBoundary) EvaluateBoundary(0, true);
            resumeBoundary = false;
            ProcessQueue();
        }

        public bool AddBar(string barKey, int delta)
        {
            if ((activePresentation || draining) && !settling) return false;
            var derived = ReadDerived();
            if (!state.TryAddBar(barKey, delta, out int oldValue, out int newValue)) return false;
            OnBarChanged(barKey, oldValue, newValue);
            RecordDerived(derived);
            return true;
        }

        public bool SetBar(string barKey, int value)
        {
            if ((activePresentation || draining) && !settling) return false;
            var derived = ReadDerived();
            if (!state.TrySetBar(barKey, value, out int oldValue, out int newValue)) return false;
            OnBarChanged(barKey, oldValue, newValue);
            RecordDerived(derived);
            return true;
        }

        private Dictionary<string, int> ReadDerived()
        {
            var values = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (NarrativeBarDefinition bar in configuration.bars)
                if (bar != null && bar.advanceMode == NarrativeAdvanceMode.Derived && state.TryGetBar(bar.barKey, out int value)) values[bar.barKey] = value;
            return values;
        }
        private void RecordDerived(Dictionary<string, int> before)
        {
            foreach (KeyValuePair<string, int> pair in before)
                if (state.TryGetBar(pair.Key, out int current) && current != pair.Value) OnBarChanged(pair.Key, pair.Value, current);
        }

        public bool EvaluateCondition(NarrativeConditionDefinition condition) => EvaluateCondition(condition, null);

        public bool EvaluateCondition(NarrativeConditionDefinition condition, NarrativeConditionContext context)
        {
            if (condition == null) return false;
            string type = condition.condType?.Trim().ToUpperInvariant();
            string target = StripPrefix(condition.target, "bar:", "flag:", "switch:", "event:", "obj:", "scene:", "reach:");
            bool value;
            string[] supportedTypes = { "BAR_GTE", "REL_GTE", "BAR_LTE", "BAR_BETWEEN", "FLAG_SET", "FLAG_CLEAR", "SWITCH_ON", "EVENT_HAPPENED", "EVENT_NOT_HAPPENED", "ACTOR_IS", "TARGET_IS", "LOCATION_EQ", "TIMESLOT_EQ", "REACH_COUNT_EQ", "CHANCE_PCT" };
            if (!supportedTypes.Contains(type)) { Debug.LogWarning("[NCS] Unknown condition: " + type); return false; }
            if (type != "CHANCE_PCT" && type != "ACTOR_IS" && type != "TARGET_IS" && type != "LOCATION_EQ" && type != "TIMESLOT_EQ" && string.IsNullOrWhiteSpace(target)) return false;
            switch (type)
            {
                case "BAR_GTE":
                    if (!state.TryGetBar(target, out _) || !int.TryParse(condition.value, out _)) return false;
                    value = CompareBar(target, condition.value, 1); break;
                case "REL_GTE":
                    string relationTarget = StripPrefix(condition.target, "rel:", "bar:");
                    if (state.ResolveRelation(relationTarget) == null || !int.TryParse(condition.value, out _)) return false;
                    value = CompareBar(relationTarget, condition.value, 1); break;
                case "BAR_LTE":
                    if (!state.TryGetBar(target, out _) || !int.TryParse(condition.value, out _)) return false;
                    value = CompareBar(target, condition.value, -1); break;
                case "BAR_BETWEEN":
                    string[] range = (condition.value ?? string.Empty).Split(new[] { '.', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (!state.TryGetBar(target, out int current) || range.Length != 2 || !int.TryParse(range[0], out int min) || !int.TryParse(range[1], out int max) || min > max) return false;
                    value = current >= min && current <= max;
                    break;
                case "FLAG_SET": value = state.IsFlagSet(target); break;
                case "FLAG_CLEAR": value = !state.IsFlagSet(target); break;
                case "SWITCH_ON": value = state.IsSwitchOn(target); break;
                case "EVENT_HAPPENED": value = state.HasHappenedEvent(target); break;
                case "EVENT_NOT_HAPPENED": value = !state.HasHappenedEvent(target); break;
                case "ACTOR_IS": value = context != null && context.ActorId == StripPrefix(string.IsNullOrWhiteSpace(condition.value) ? target : condition.value, "obj:"); break;
                case "TARGET_IS": value = context != null && context.TargetId == StripPrefix(string.IsNullOrWhiteSpace(condition.value) ? target : condition.value, "obj:"); break;
                case "LOCATION_EQ": value = (context?.Location ?? Location) == StripPrefix(string.IsNullOrWhiteSpace(condition.value) ? target : condition.value, "scene:"); break;
                case "TIMESLOT_EQ": value = string.Equals((context?.TimeSlot ?? TimeSlot).ToString(), string.IsNullOrWhiteSpace(condition.value) ? target : condition.value, StringComparison.OrdinalIgnoreCase); break;
                case "REACH_COUNT_EQ":
                    int count = state.GetCounter(target);
                    if (target.Split('@').Length == 2)
                    {
                        var keys = configuration.triggers.Where(t => t != null && (t.barKey + "@" + t.threshold) == target).Select(CounterKey).Distinct().ToList();
                        if (keys.Count != 1) return false;
                        count = state.GetCounter(keys[0]);
                    }
                    value = int.TryParse(condition.value, out int expected) && count == expected; break;
                case "CHANCE_PCT":
                    if (!double.TryParse(condition.value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double probability) || probability < 0 || probability > 1) return false;
                    randomSeed ^= randomSeed << 13; randomSeed ^= randomSeed >> 17; randomSeed ^= randomSeed << 5;
                    value = randomSeed / ((double)uint.MaxValue + 1) < probability; break;
                default: Debug.LogWarning("[NCS] Unknown condition: " + type); return false;
            }
            return condition.notFlag ? !value : value;
        }

        public bool EvaluateConditions(string ownerType, string ownerId, NarrativeConditionContext context = null)
        {
            if (!conditionsByOwner.TryGetValue(OwnerKey(ownerType, ownerId), out List<NarrativeConditionDefinition> conditions)) return true;
            var groups = new Dictionary<int, bool>();
            foreach (NarrativeConditionDefinition condition in conditions)
            {
                bool value = EvaluateCondition(condition, context);
                groups[condition.groupIndex] = groups.TryGetValue(condition.groupIndex, out bool previous) ? previous && value : value;
            }
            return groups.Values.Any(value => value);
        }

        public bool SelectChoice(string choiceId)
        {
            if (!activePresentation || string.IsNullOrWhiteSpace(choiceId)) return false;
            NarrativeChoiceDefinition choice = configuration.choices.Find(c => c != null && c.choiceId == choiceId);
            if (choice == null || !ChoiceBelongsToActiveSetup(choice)) return false;
            if (selectedChoices.Contains(choiceId)) return true;
            if (selectedChoices.Any(id => configuration.choices.Exists(c => c != null && c.choiceId == id && c.groupKey == choice.groupKey))) return false;
            if (!EvaluateConditions("choice", choiceId, activeRequest.Context ?? Context(activeRequest.Event))) return false;
            selectedChoices.Add(choiceId);
            return true;
        }

        private bool ChoiceBelongsToActiveSetup(NarrativeChoiceDefinition choice)
        {
            NarrativeSetupParamsDefinition setup = configuration.setups.Find(s => s != null && s.setupKey == activeRequest.Event.setupKey);
            if (setup == null) return true;
            bool hasOptions = false;
            foreach (string assignment in (setup.slotValues ?? string.Empty).Split(','))
            {
                int separator = assignment.IndexOf('=');
                if (separator < 0) continue;
                string slot = assignment.Substring(0, separator).Trim();
                if (slot != "opts" && slot != "optionList") continue;
                hasOptions = true;
                if (assignment.Substring(separator + 1).Trim() == choice.groupKey) return true;
            }
            if (setup.parameters != null) foreach (NarrativeAtomParameter parameter in setup.parameters)
            {
                if (parameter == null || parameter.slot != "opts" && parameter.slot != "optionList") continue;
                hasOptions = true;
                if (parameter.value == choice.groupKey) return true;
            }
            return !hasOptions;
        }

        public NarrativeSnapshot CaptureSnapshot()
        {
            if (settling) throw new InvalidOperationException("NCS snapshots cannot be captured during an atomic result transaction; save after EventCompleted.");
            NarrativeSnapshot snapshot = state.Capture();
            snapshot.location = Location; snapshot.timeSlot = TimeSlot; snapshot.randomSeed = randomSeed;
            snapshot.turnOpen = turnOpen; snapshot.turnEnding = turnEnding;
            snapshot.turnEvents.AddRange(pendingEvents);
            foreach (NarrativeResultDefinition effect in pendingEffects) snapshot.pendingEffects.Add(CloneEffect(effect));
            foreach (NarrativeEventRequest request in queue) snapshot.pending.Add(ToSaved(request));
            if (activePresentation) { snapshot.active = ToSaved(activeRequest); snapshot.active.selectedChoices.Clear(); snapshot.active.selectedChoices.AddRange(selectedChoices); }
            foreach (NarrativeBarChange change in changes) snapshot.changes.Add(new NarrativeBarChange { key = change.key, oldValue = change.oldValue, newValue = change.newValue, depth = change.depth });
            return snapshot;
        }

        public void RestoreSnapshot(NarrativeSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (settling) throw new InvalidOperationException("Restore cannot interrupt an NCS result transaction.");
            presentationEpoch++;
            queue.Clear(); pendingEvents.Clear(); changes.Clear(); selectedChoices.Clear(); pendingEffects.Clear(); draining = false;
            processing = false; activePresentation = false; pumping = false; transactionDepth = 0;
            resumeBoundary = snapshot.turnEnding;
            state.Restore(snapshot);
            Location = snapshot.location ?? string.Empty; TimeSlot = snapshot.timeSlot;
            randomSeed = snapshot.randomSeed == 0 ? 0x6D2B79F5 : snapshot.randomSeed;
            turnOpen = snapshot.turnOpen; turnEnding = snapshot.turnEnding;
            if (snapshot.turnEvents != null) foreach (string id in snapshot.turnEvents) if (!string.IsNullOrEmpty(id)) pendingEvents.Add(id);
            if (snapshot.active != null) RestoreRequest(snapshot.active, true);
            if (snapshot.pending != null) foreach (NarrativePendingValue saved in snapshot.pending) RestoreRequest(saved, false);
            if (snapshot.changes != null) foreach (NarrativeBarChange change in snapshot.changes) if (change != null && state.GetDefinition(change.key) != null) changes.Add(change);
            if (snapshot.pendingEffects != null) foreach (NarrativeResultDefinition effect in snapshot.pendingEffects)
                if (effect != null && effect.resultType == NarrativeResultType.PlaySetup) { pendingEffects.Add(CloneEffect(effect)); NotifyEffect(effect); }
            // Resume only after the caller explicitly pumps EndTurn; old presenter callbacks are invalidated.
            if (queue.Count > 0 || pendingEffects.Count > 0 || turnEnding) { resumeBoundary = true; turnOpen = true; turnEnding = false; }
        }

        private NarrativePendingValue ToSaved(NarrativeEventRequest request)
        {
            var saved = new NarrativePendingValue { eventId = request.Event.eventId, triggerId = request.Trigger?.triggerId, requestId = request.RequestId, depth = request.Depth, timeSlot = request.Context?.TimeSlot ?? TimeSlot, location = request.Context?.Location ?? Location };
            if (request.SelectedChoices != null) saved.selectedChoices.AddRange(request.SelectedChoices);
            return saved;
        }
        private void RestoreRequest(NarrativePendingValue saved, bool wasActive)
        {
            if (saved == null || saved.depth < 0 || saved.depth > 3 || !events.TryGetValue(saved.eventId ?? string.Empty, out NarrativeEventDefinition definition)) return;
            NarrativeTriggerDefinition trigger = configuration.triggers.Find(t => t != null && t.triggerId == saved.triggerId);
            var context = Context(definition); context.TimeSlot = saved.timeSlot; context.Location = saved.location ?? Location;
            queue.Enqueue(new NarrativeEventRequest(definition, trigger, saved.depth, saved.selectedChoices == null ? null : saved.selectedChoices.ToArray(), context, saved.requestId));
            pendingEvents.Add(definition.eventId);
        }
        private static string OwnerKey(string type, string id) => (string.IsNullOrWhiteSpace(type) ? "event" : type.Trim().ToLowerInvariant()) + ":" + id;
        private NarrativeConditionContext Context(NarrativeEventDefinition definition) => new NarrativeConditionContext { ActorId = definition.actorId, TargetId = definition.targetId, Location = Location, TimeSlot = TimeSlot };

        private void BuildIndexes()
        {
            foreach (NarrativeEventDefinition definition in configuration.events)
                if (definition != null && !string.IsNullOrWhiteSpace(definition.eventId)) events[definition.eventId] = definition;
            foreach (NarrativeTriggerDefinition trigger in configuration.triggers)
            {
                if (trigger == null || string.IsNullOrWhiteSpace(trigger.barKey)) continue;
                if (!triggersByBar.TryGetValue(trigger.barKey, out List<NarrativeTriggerDefinition> list))
                    triggersByBar[trigger.barKey] = list = new List<NarrativeTriggerDefinition>();
                list.Add(trigger);
                triggerRows[trigger] = configuration.triggers.IndexOf(trigger);
            }
            foreach (NarrativeConditionDefinition condition in configuration.conditions)
            {
                if (condition == null || string.IsNullOrWhiteSpace(condition.ownerId)) continue;
                string ownerKey = OwnerKey(condition.ownerType, condition.ownerId);
                if (!conditionsByOwner.TryGetValue(ownerKey, out List<NarrativeConditionDefinition> list))
                    conditionsByOwner[ownerKey] = list = new List<NarrativeConditionDefinition>();
                list.Add(condition);
            }
            foreach (NarrativeResultDefinition result in configuration.results)
            {
                if (result == null || string.IsNullOrWhiteSpace(result.eventId)) continue;
                if (!resultsByEvent.TryGetValue(result.eventId, out List<NarrativeResultDefinition> list))
                    resultsByEvent[result.eventId] = list = new List<NarrativeResultDefinition>();
                list.Add(result);
            }
            foreach (List<NarrativeResultDefinition> list in resultsByEvent.Values)
                list.Sort((a, b) => a.seq != b.seq ? a.seq.CompareTo(b.seq) : configuration.results.IndexOf(a).CompareTo(configuration.results.IndexOf(b)));
        }

        private void OnBarChanged(string barKey, int oldValue, int newValue)
        {
            if (!turnOpen) BeginTurn();
            changes.Add(new NarrativeBarChange { key = barKey, oldValue = oldValue, newValue = newValue, depth = transactionDepth });
            Debug.Log($"[NCS][Bar] key={barKey} old={oldValue} new={newValue} depth={transactionDepth}");
            try { BarChanged?.Invoke(barKey, oldValue, newValue); }
            catch (Exception error) { Debug.LogException(error); }
        }

        private sealed class Hit
        {
            public NarrativeTriggerDefinition Trigger;
            public NarrativeEventDefinition Event;
            public int Priority, Row, Count;
            public bool OnceDate;
            public NarrativeEventDateDefinition Date;
            public NarrativeConditionContext Context;
        }

        private void EvaluateBoundary(int depth, bool includeHoldAndDates)
        {
            var hits = new List<Hit>();
            var boundaryChanges = changes.ToArray();
            changes.Clear();
            foreach (NarrativeBarChange change in boundaryChanges)
            {
                if (!triggersByBar.TryGetValue(change.key, out List<NarrativeTriggerDefinition> triggers)) continue;
                var incremented = new HashSet<string>(StringComparer.Ordinal);
                foreach (NarrativeTriggerDefinition trigger in triggers)
                {
                    if (!trigger.enabled) continue;
                    string key = CounterKey(trigger);
                    if (trigger.resetOnExit && IsInside(trigger, change.oldValue) && !IsInside(trigger, change.newValue)) state.ResetCounter(key);
                    bool crossed = Crossed(trigger, change.oldValue, change.newValue);
                    if (crossed && incremented.Add(key)) state.IncrementCounter(key);
                }
                foreach (NarrativeTriggerDefinition trigger in triggers)
                    if (trigger.enabled && trigger.crossType == NarrativeCrossType.Cross && Crossed(trigger, change.oldValue, change.newValue) && events.TryGetValue(trigger.eventId ?? string.Empty, out NarrativeEventDefinition definition))
                        hits.Add(new Hit { Trigger = trigger, Event = definition, Priority = trigger.priority, Row = triggerRows[trigger], Count = state.GetCounter(CounterKey(trigger)) });
            }
            if (includeHoldAndDates)
            {
                foreach (NarrativeTriggerDefinition trigger in configuration.triggers)
                {
                    if (trigger == null || !trigger.enabled || trigger.crossType != NarrativeCrossType.Hold || !state.TryGetBar(trigger.barKey, out int current) || !IsInside(trigger, current)) continue;
                    if (state.GetCounter(CounterKey(trigger)) == 0) state.IncrementCounter(CounterKey(trigger));
                    if (events.TryGetValue(trigger.eventId ?? string.Empty, out NarrativeEventDefinition definition))
                        hits.Add(new Hit { Trigger = trigger, Event = definition, Priority = trigger.priority, Row = triggerRows[trigger], Count = state.GetCounter(CounterKey(trigger)) });
                }
                if (state.TryGetBar("world.calendar", out int day))
                    for (int i = 0; i < configuration.eventDates.Count; i++)
                    {
                        NarrativeEventDateDefinition date = configuration.eventDates[i];
                        if (date == null || !DateMatches(date, day) || !events.TryGetValue(date.eventId ?? string.Empty, out NarrativeEventDefinition definition)) continue;
                        if (date.repeatRule == NarrativeRepeatRule.Once && state.IsFlagSet("date.once." + date.eventId + "." + date.day + "." + date.timeSlot)) continue;
                        var context = Context(definition); context.TimeSlot = date.timeSlot;
                        hits.Add(new Hit { Event = definition, Priority = date.priority, Row = configuration.triggers.Count + i, OnceDate = date.repeatRule == NarrativeRepeatRule.Once, Date = date, Context = context });
                    }
            }
            hits.Sort((a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : a.Row.CompareTo(b.Row));
            foreach (Hit hit in hits)
            {
                NarrativeTriggerDefinition trigger = hit.Trigger;
                if (trigger != null)
                {
                    if (state.IsFlagSet("trigger.once." + trigger.triggerId)) continue;
                    if (!trigger.MatchesReach(hit.Count)) continue;
                    if (string.Equals(trigger.reachCount, "all", StringComparison.OrdinalIgnoreCase) && hits.Any(x => x.Trigger != null && CounterKey(x.Trigger) == CounterKey(trigger) && !string.Equals(x.Trigger.reachCount, "all", StringComparison.OrdinalIgnoreCase) && x.Trigger.MatchesReach(hit.Count))) continue;
                    if (trigger.onceType == NarrativeOnceType.Once && state.HasFiredTrigger(trigger.triggerId)) continue;
                }
                if (depth > 3) { Debug.LogWarning("[NCS] Trigger depth exceeds 3: " + hit.Event.eventId); continue; }
                if (state.IsEventLocked(hit.Event.eventId) || state.IsFlagSet("event.once." + hit.Event.eventId) || hit.Event.onceOnly && state.HasHappenedEvent(hit.Event.eventId) || pendingEvents.Contains(hit.Event.eventId)) continue;
                NarrativeConditionContext frozenContext = hit.Context ?? Context(hit.Event);
                if (!EvaluateConditions("event", string.IsNullOrWhiteSpace(hit.Event.conditionOwnerId) ? hit.Event.eventId : hit.Event.conditionOwnerId, frozenContext)) continue;
                pendingEvents.Add(hit.Event.eventId);
                if (trigger != null && trigger.onceType == NarrativeOnceType.Once)
                {
                    state.MarkTriggerFired(trigger.triggerId);
                    state.SetFlag("trigger.once." + trigger.triggerId, true);
                }
                if (hit.Event.onceOnly) state.SetFlag("event.once." + hit.Event.eventId, true);
                if (hit.OnceDate) state.SetFlag("date.once." + hit.Date.eventId + "." + hit.Date.day + "." + hit.Date.timeSlot, true);
                var request = new NarrativeEventRequest(hit.Event, trigger, depth, null, frozenContext);
                queue.Enqueue(request);
                Debug.Log($"[NCS][Trigger] bar={trigger?.barKey ?? "date"} trigger={trigger?.triggerId ?? "EventDate"} threshold={trigger?.threshold} count={hit.Count} depth={depth} event={hit.Event.eventId} tag={hit.Event.logTag}");
                try { EventQueued?.Invoke(request); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }

        private int GetDay() => state.TryGetBar("world.calendar", out int day) ? day : 1;
        private static bool DateMatches(NarrativeEventDateDefinition date, int day)
        {
            int start = date.day > 0 ? date.day : 1;
            if (day < start) return false;
            switch (date.repeatRule)
            {
                case NarrativeRepeatRule.Once: return date.day > 0 && day == start;
                case NarrativeRepeatRule.Daily: return true;
                case NarrativeRepeatRule.Weekly: return (day - start) % 7 == 0;
                case NarrativeRepeatRule.Yearly: return (day - start) % 365 == 0;
                default: return false;
            }
        }

        private void ProcessQueue()
        {
            if (pumping || draining) return;
            if (pendingEffects.Count > 0 && presenter is INarrativePresentationDrain drain)
            {
                draining = true;
                long epoch = ++presentationEpoch;
                try
                {
                    drain.Drain(() =>
                    {
                        if (!draining || presentationEpoch != epoch) return;
                        presentationEpoch++;
                        draining = false;
                        pendingEffects.Clear();
                        ProcessQueue();
                    });
                }
                catch (Exception error) { Debug.LogException(error); }
                return;
            }
            pendingEffects.Clear();
            pumping = true;
            processing = true;
            try
            {
                while (!activePresentation && queue.Count > 0 && pendingEffects.Count == 0)
                {
                    activeRequest = queue.Dequeue();
                    selectedChoices.Clear();
                    if (activeRequest.SelectedChoices != null) selectedChoices.AddRange(activeRequest.SelectedChoices);
                    activePresentation = true;
                    long epoch = ++presentationEpoch;
                    NarrativeEventRequest request = new NarrativeEventRequest(activeRequest.Event, activeRequest.Trigger, activeRequest.Depth, selectedChoices.ToArray(), activeRequest.Context, activeRequest.RequestId);
                    // A broken presenter is not treated as successful: its active request remains saveable.
                    try { presenter.Play(request, () => { if (epoch == presentationEpoch && activePresentation) CompleteEvent(request); }); }
                    catch (Exception error) { Debug.LogException(error); break; }
                }
                if (!activePresentation && queue.Count == 0 && pendingEffects.Count == 0)
                {
                    processing = false;
                    if (turnEnding) FinishTurn();
                }
            }
            finally { pumping = false; }
            if (!activePresentation && pendingEffects.Count > 0) ProcessQueue();
        }

        private void CompleteEvent(NarrativeEventRequest request)
        {
            presentationEpoch++;
            settling = true;
            transactionDepth = request.Depth + 1;
            // Retain active until the synchronous transaction has finished; stale callbacks cannot settle twice.
            ApplyResults(request.Event.eventId);
            foreach (string id in selectedChoices)
            {
                NarrativeChoiceDefinition choice = configuration.choices.Find(c => c != null && c.choiceId == id);
                if (choice != null) ApplyResults(string.IsNullOrWhiteSpace(choice.resultOwnerId) ? choice.choiceId : choice.resultOwnerId);
            }
            state.MarkEventHappened(request.Event.eventId);
            activePresentation = false;
            selectedChoices.Clear();
            EvaluateBoundary(transactionDepth, false);
            transactionDepth = 0;
            settling = false;
            try { EventCompleted?.Invoke(request.Event.eventId); }
            catch (Exception error) { Debug.LogException(error); }
            ProcessQueue();
        }

        private void ApplyResults(string ownerId)
        {
            if (resultsByEvent.TryGetValue(ownerId, out List<NarrativeResultDefinition> results))
                foreach (NarrativeResultDefinition result in results) ApplyResult(result);
        }

        private void FinishTurn()
        {
            // The new date is evaluated at the next boundary, never inside the just-finished day.
            var derived = ReadDerived();
            foreach (NarrativeBarDefinition bar in configuration.bars)
            {
                if (bar == null || bar.advanceMode != NarrativeAdvanceMode.Monotonic && bar.barKey != "world.calendar") continue;
                int delta = bar.barKey == "world.calendar" ? 1 : Math.Max(0, bar.autoStep);
                if (state.TryAddBar(bar.barKey, delta, out int oldValue, out int newValue))
                {
                    changes.Add(new NarrativeBarChange { key = bar.barKey, oldValue = oldValue, newValue = newValue });
                    try { BarChanged?.Invoke(bar.barKey, oldValue, newValue); }
                    catch (Exception error) { Debug.LogException(error); }
                }
            }
            RecordDerived(derived);
            turnOpen = false; turnEnding = false;
            try { TurnCompleted?.Invoke(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        private void ApplyResult(NarrativeResultDefinition result)
        {
            if (result == null) return;
            string target = StripPrefix(result.target, "bar:", "rel:", "flag:", "switch:", "obj:", "event:", "ach:", "setup:");
            bool positionEffect = result.resultType == NarrativeResultType.ShowPosition || result.resultType == NarrativeResultType.HidePosition
                || result.resultType == NarrativeResultType.AddTempPosition || result.resultType == NarrativeResultType.RemoveTempPosition;
            if (result.resultType != NarrativeResultType.AdvanceDay && !positionEffect && string.IsNullOrWhiteSpace(target)) return;
            switch (result.resultType)
            {
                case NarrativeResultType.BarAdd:
                    if (int.TryParse(result.value, out int delta)) AddBar(target, delta); else return;
                    break;
                case NarrativeResultType.RelAdd:
                    if (state.ResolveRelation(target) != null && int.TryParse(result.value, out int relationDelta)) AddBar(target, relationDelta); else return;
                    break;
                case NarrativeResultType.BarSet:
                    if (int.TryParse(result.value, out int value)) SetBar(target, value); else return;
                    break;
                case NarrativeResultType.SetFlag:
                    if (!bool.TryParse(result.value, out bool flag)) return;
                    state.SetFlag(target, flag); break;
                case NarrativeResultType.ClearFlag: state.SetFlag(target, false); break;
                case NarrativeResultType.SetSwitch:
                    if (!bool.TryParse(result.value, out bool on)) return;
                    state.SetSwitch(target, on); break;
                case NarrativeResultType.ObjectState: state.SetObjectState(target, result.value); break;
                case NarrativeResultType.UnlockEvent: state.SetEventLocked(target, false); break;
                case NarrativeResultType.LockEvent: state.SetEventLocked(target, true); break;
                case NarrativeResultType.SpawnNpc: state.SetNpcLocation(target, StripPrefix(result.value, "scene:")); break;
                case NarrativeResultType.DespawnNpc: state.SetNpcLocation(target, string.Empty); break;
                case NarrativeResultType.ShowPosition:
                case NarrativeResultType.HidePosition:
                case NarrativeResultType.AddTempPosition:
                case NarrativeResultType.RemoveTempPosition:
                    break;
                case NarrativeResultType.AdvanceDay:
                    if (int.TryParse(result.value, out int days) && days > 0) AddBar("world.calendar", days); else return;
                    break;
                case NarrativeResultType.AwardAchievement: state.SetFlag("achievement." + target, true); break;
                case NarrativeResultType.PlaySetup:
                    if (presenter is INarrativePresentationDrain) pendingEffects.Add(CloneEffect(result));
                    break;
                case NarrativeResultType.BarHint: break;
                default: Debug.LogWarning("[NCS] Unknown result: " + result.resultType); return;
            }
            NotifyEffect(result);
        }

        private static NarrativeResultDefinition CloneEffect(NarrativeResultDefinition result) => new NarrativeResultDefinition { resultId = result.resultId, eventId = result.eventId, seq = result.seq, resultType = result.resultType, target = result.target, value = result.value, note = result.note };
        private void NotifyEffect(NarrativeResultDefinition result)
        {
            string target = StripPrefix(result.target, "bar:", "rel:", "flag:", "switch:", "obj:", "event:", "ach:", "setup:");
            try { EffectRequested?.Invoke(new NarrativeEffectNotification { Result = result, Target = target, Value = result.value }); }
            catch (Exception error) { Debug.LogException(error); }
        }

        private bool CompareBar(string key, string expected, int direction)
        {
            return state.TryGetBar(key, out int current) && int.TryParse(expected, out int value) && (direction > 0 ? current >= value : current <= value);
        }
        private static bool IsInside(NarrativeTriggerDefinition trigger, int value) => trigger.direction == NarrativeTriggerDirection.Down ? value <= trigger.threshold : trigger.direction == NarrativeTriggerDirection.Up ? value >= trigger.threshold : value == trigger.threshold;
        private static bool Crossed(NarrativeTriggerDefinition trigger, int oldValue, int newValue) => (trigger.direction == NarrativeTriggerDirection.Up || trigger.direction == NarrativeTriggerDirection.Both) && oldValue < trigger.threshold && newValue >= trigger.threshold || (trigger.direction == NarrativeTriggerDirection.Down || trigger.direction == NarrativeTriggerDirection.Both) && oldValue > trigger.threshold && newValue <= trigger.threshold;
        private static string CounterKey(NarrativeTriggerDefinition trigger) => trigger.barKey + "@" + trigger.threshold + "@" + trigger.direction;
        private static string StripPrefix(string value, params string[] prefixes)
        {
            string result = value ?? string.Empty;
            for (int i = 0; i < prefixes.Length; i++) if (result.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase)) return result.Substring(prefixes[i].Length);
            return result;
        }
    }
}
