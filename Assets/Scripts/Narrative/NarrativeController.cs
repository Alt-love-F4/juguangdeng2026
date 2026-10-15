using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativeController
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
        private bool presenterUnavailable;
        private bool activePresentation;
        private int transactionDepth;
        private bool turnOpen, turnEnding, pumping, settling, resumeBoundary;
        private long presentationEpoch;
        private bool draining;
        private readonly List<NarrativeResultDefinition> pendingEffects = new List<NarrativeResultDefinition>();
        private NarrativeEventRequest activeRequest;
        private readonly List<NarrativeBarChange> changes = new List<NarrativeBarChange>();
        private readonly List<string> selectedChoices = new List<string>();
        private NarrativeAbilityCheckSnapshot activeAbilityCheck;
        private string activeAbilityCheckId;
        private bool resumeActiveAbilityCheck;
        private readonly Dictionary<NarrativeTriggerDefinition, int> triggerRows = new Dictionary<NarrativeTriggerDefinition, int>();
        private uint randomSeed = 0x6D2B79F5;
        public string Location { get; set; } = string.Empty;
        public NarrativeTimeSlot TimeSlot { get; set; }
        public IReadOnlyList<string> SelectedChoices => selectedChoices;
        public NarrativeAbilityCheckSnapshot ActiveAbilityCheck => activeAbilityCheck;
        public Func<string, (bool ready, int value)> AbilityReader { get; set; }
        public Func<string, string> TextResolver { get; set; }
        public Func<string, string> AbilityNameResolver { get; set; }
        public event Action<NarrativeAbilityCheckSnapshot> AbilityCheckEvaluated;
        public event Action<NarrativeEffectNotification> EffectRequested;
        public event Action TurnCompleted;

        private NarrativeMapInventory inventory;
        private int operationDepth;
        private readonly Queue<Action> safeCallbacks = new Queue<Action>();
        private bool dispatchingSafeCallbacks;
        public bool IsSafeBoundary => !valueRestore && valueBaselineReady && settledValueChanges.Count == 0 && operationDepth == 0 && !settling && !activePresentation && !draining && !pumping && queue.Count == 0 && pendingEffects.Count == 0;
        public void RunAtSafeBoundary(Action callback)
        {
            if (callback == null) return;
            if (IsEnded) { callback(); return; }
            safeCallbacks.Enqueue(callback);
            CheckEndingsAtSafeBoundary();
        }
        private void DispatchSafeCallbacks()
        {
            PumpCafeBoundary();
            if (dispatchingSafeCallbacks) return;
            dispatchingSafeCallbacks = true;
            try
            {
                while (IsSafeBoundary && safeCallbacks.Count > 0)
                {
                    var callback = safeCallbacks.Dequeue();
                    callback();
                }
            }
            finally { dispatchingSafeCallbacks = false; }
        }
        private bool endingChecksEnabled, endingDirty = true, officialDayManaged;
        private int officialDayProcessed;
        private bool migrateOfficialDay;
        public string SelectedEndingId { get; private set; }
        public bool IsEnded => !string.IsNullOrEmpty(SelectedEndingId);
        public bool CanCaptureSnapshot => !settling && (operationDepth == 0 || activePresentation) && !valueRestore && valueBaselineReady && settledValueChanges.Count == 0;
        public Func<string, int?> ActionProgressReader { get; set; }
        public Func<string, bool?> ActionCompletedReader { get; set; }
        public event Action<NarrativeEndingDefinition> EndingSelected;
        public event Action<string, string> EventFailed;
        public NarrativeMapInventory Inventory
        {
            get => inventory;
            set
            {
                if (inventory == value) return;
                if (inventory != null) inventory.Changed -= OnStoreChanged;
                inventory = value;
                if (inventory != null) inventory.Changed += OnStoreChanged;
                NotifyConditionsChanged();
            }
        }
        public void NotifyConditionsChanged() => endingDirty = true;
        private void OnStoreChanged()
        {
            NotifyConditionsChanged();
            if (endingChecksEnabled) CheckEndingsAtSafeBoundary();
        }
        public void BeginOperation()
        {
            if (IsEnded) throw new InvalidOperationException("Session has ended.");
            if (operationDepth == 0 && !valueRestore)
            {
                operationValues = ReadAllValues();
                if (observedValues != null) foreach (var bar in configuration.bars)
                    if ((bar.sourceMode == NarrativeSourceMode.External || bar.advanceMode == NarrativeAdvanceMode.Derived) && observedValues.TryGetValue(bar.barKey, out int baseline))
                        operationValues[bar.barKey] = baseline;
            }
            operationDepth++;
        }
        public void EndOperation()
        {
            if (operationDepth <= 0) throw new InvalidOperationException("Unbalanced narrative operation.");
            operationDepth--;
            if (operationDepth == 0 && !valueRestore) { CommitValueComparison(operationValues); operationValues = null; FlushValueChanges(); }
            CheckEndingsAtSafeBoundary();
        }
        public void SetEndingChecksEnabled(bool enabled)
        {
            endingChecksEnabled = enabled;
            if (enabled) { endingDirty = true; CheckEndingsAtSafeBoundary(); }
        }
        public NarrativeEndingDefinition CheckEndingsAtSafeBoundary()
        {
            if (IsEnded)
            {
                DispatchSafeCallbacks();
                return configuration.endings.Find(e => e != null && e.endingId == SelectedEndingId);
            }
            if (valueRestore || !valueBaselineReady || !IsSafeBoundary) return null;
            if (!endingChecksEnabled || !endingDirty) { DispatchSafeCallbacks(); return null; }
            endingDirty = false;
            var ending = configuration.endings.Where(e => e != null && e.enabled)
                .OrderByDescending(e => e.priority).ThenBy(e => e.endingId, StringComparer.Ordinal)
                .FirstOrDefault(e => EvaluateConditions("ending", e.endingId));
            if (ending == null) { DispatchSafeCallbacks(); return null; }
            SelectedEndingId = ending.endingId;
            presentationEpoch++;
            turnOpen = turnEnding = processing = false;
            changes.Clear();
            try { EndingSelected?.Invoke(ending); }
            finally { DispatchSafeCallbacks(); }
            return ending;
        }
        public void ResumePendingEvents() { if (!IsEnded) ProcessQueue(); }
        public void SetOfficialDay(int day)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            officialDayManaged = true;
            if (migrateOfficialDay)
            {
                officialDayProcessed = day;
                migrateOfficialDay = false;
            }
            if (IsEnded) return;
            var derived = ReadDerived();
            if (state.SynchronizeCalendar(day, out int oldValue, out int newValue))
            {
                OnBarChanged("world.calendar", oldValue, newValue);
                RecordDerived(derived);
            }
        }

        /// <summary>Schedules the synchronized formal day once, without advancing the calendar.</summary>
        public bool ProcessOfficialDay(int day)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            if (IsEnded || day <= officialDayProcessed) return false;
            if (!officialDayManaged || !state.TryGetBar("world.calendar", out int current) || current != day)
                throw new InvalidOperationException("Synchronize the official day before scheduling it.");
            if (!IsSafeBoundary || IsBusy) throw new InvalidOperationException("Schedule the official day after pending narrative operations finish.");
            // A formal day is a fresh scheduling boundary. Existing queued requests have drained.
            pendingEvents.Clear();
            turnOpen = true;
            turnEnding = resumeBoundary = false;
            transactionDepth = 0;
            settling = true;
            try
            {
                officialDayProcessed = day;
                EvaluateBoundary(0, true);
            }
            finally { settling = false; }
            ProcessQueue();
            return true;
        }

        public NarrativeState State => state;
        public bool IsBusy => processing || activePresentation || draining || queue.Count > 0 || turnEnding;
        public event Action<NarrativeEventRequest> EventQueued;
        public event Action<string, int, int> BarChanged;
        public event Action<string> EventCompleted;

        public NarrativeController(NarrativeConfiguration configuration, INarrativePresenter presenter = null)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            state = new NarrativeState(configuration.bars, configuration.conditionKeys);
            state.ConditionsChanged += OnStoreChanged;
            this.presenter = presenter ?? new ImmediateNarrativePresenter();
            if (this.presenter is INarrativePresentationCancellation cancellation) cancellation.Cancelled += OnPresentationCancelled;
            BuildIndexes();
            valueBaselineReady = !configuration.bars.Any(b => b.sourceMode == NarrativeSourceMode.External);
        }

        public bool QueueEvent(string eventId)
        {
            if (presenterUnavailable || valueRestore || !valueBaselineReady || IsEnded || string.IsNullOrWhiteSpace(eventId) || !events.TryGetValue(eventId, out NarrativeEventDefinition definition)) return false;
            if (activePresentation || draining || queue.Any(request => request.Event.eventId == eventId) || state.IsEventLocked(eventId) || state.IsFlagSet("event.once." + eventId) || definition.onceOnly && state.HasHappenedEvent(eventId)) return false;
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
            if (IsEnded || turnOpen || IsBusy) return;
            turnOpen = true;
            transactionDepth = 0;
            pendingEvents.Clear();
        }

        public void EndTurn()
        {
            if (valueRestore || !valueBaselineReady || IsEnded || turnEnding) return;
            if (!turnOpen) BeginTurn();
            if (!turnOpen) return;
            turnEnding = true;
            if (!resumeBoundary) EvaluateBoundary(0, true);
            resumeBoundary = false;
            ProcessQueue();
        }

        public bool AddBar(string barKey, int delta)
        {
            if (IsEnded || (activePresentation || draining) && !settling) return false;
            var derived = ReadDerived();
            if (!state.TryAddBar(barKey, delta, out int oldValue, out int newValue)) return false;
            OnBarChanged(barKey, oldValue, newValue);
            RecordDerived(derived);
            if (!valueRestore && operationDepth == 0 && observedValues != null) observedValues = ReadAllValues();
            CheckEndingsAtSafeBoundary();
            return true;
        }

        public bool SetBar(string barKey, int value)
        {
            if (IsEnded || (activePresentation || draining) && !settling) return false;
            var derived = ReadDerived();
            if (!state.TrySetBar(barKey, value, out int oldValue, out int newValue)) return false;
            OnBarChanged(barKey, oldValue, newValue);
            RecordDerived(derived);
            if (!valueRestore && operationDepth == 0 && observedValues != null) observedValues = ReadAllValues();
            CheckEndingsAtSafeBoundary();
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
            if (condition == null) throw new ArgumentException("Null condition row.");
            string type = condition.condType?.Trim().ToUpperInvariant();
            string target = StripPrefix(condition.target, "bar:", "rel:", "event:", "item:", "action:");
            InvalidOperationException Invalid() => new InvalidOperationException($"Invalid condition {condition.conditionId} ({type}, target={condition.target}).");
            if (condition.notFlag || !string.IsNullOrWhiteSpace(condition.op) || string.IsNullOrWhiteSpace(target)) throw Invalid();
            switch (type)
            {
                case "BOOL_EQ":
                    if (!state.TryGetCondition(condition.target, out bool fact) || !bool.TryParse(condition.value, out bool expected)) throw Invalid();
                    return fact == expected;
                case "ITEM_GTE":
                    if (inventory == null || !int.TryParse(condition.value, out int count) || count < 0) throw Invalid();
                    return inventory.GetCount(target) >= count;
                case "ACTION_PROGRESS_GTE":
                    int? progress = ActionProgressReader?.Invoke(target);
                    if (!progress.HasValue || !int.TryParse(condition.value, out int threshold) || threshold < 0) throw Invalid();
                    return progress.Value >= threshold;
                case "ACTION_COMPLETED":
                    bool? completed = ActionCompletedReader?.Invoke(target);
                    if (!completed.HasValue || !string.IsNullOrWhiteSpace(condition.value)) throw Invalid();
                    return completed.Value;
                case "BAR_GTE":
                case "REL_GTE":
                case "BAR_LTE":
                    if (!state.TryGetBar(target, out int bar) || !int.TryParse(condition.value, out int bound)
                        || type == "REL_GTE" && state.ResolveRelation(target) == null) throw Invalid();
                    return type == "BAR_LTE" ? bar <= bound : bar >= bound;
                case "BAR_BETWEEN":
                    string[] range = (condition.value ?? string.Empty).Split(new[] { '.', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (!state.TryGetBar(target, out int current) || range.Length != 2 || !int.TryParse(range[0], out int min) || !int.TryParse(range[1], out int max) || min > max) throw Invalid();
                    return current >= min && current <= max;
                case "EVENT_HAPPENED":
                case "EVENT_NOT_HAPPENED":
                    if (!events.ContainsKey(target)) throw Invalid();
                    return state.HasHappenedEvent(target) == (type == "EVENT_HAPPENED");
                default: throw Invalid();
            }
        }

        public bool EvaluateConditions(string ownerType, string ownerId, NarrativeConditionContext context = null)
        {
            if (!conditionsByOwner.TryGetValue(OwnerKey(ownerType, ownerId), out List<NarrativeConditionDefinition> conditions))
            {
                if (string.Equals(ownerType, "ending", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Ending has no conditions: " + ownerId);
                return true;
            }
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
            if (!string.IsNullOrWhiteSpace(choice.checkId))
            {
                if (activeAbilityCheck != null)
                {
                    if (activeAbilityCheckId == choice.checkId) return true;
                    throw new InvalidOperationException("Only one active ability check is supported per event presentation.");
                }
                activeAbilityCheckId = choice.checkId;
                activeAbilityCheck = EvaluateAbilityCheck(choice.checkId);
                AbilityCheckEvaluated?.Invoke(activeAbilityCheck);
                QueueAbilityBranch(choice.checkId, activeAbilityCheck);
            }
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

        public NarrativeAbilityCheckSnapshot EvaluateAbilityCheckForPresentation(string checkId)
        {
            if (!activePresentation) throw new InvalidOperationException("Ability checks require an active narrative presentation.");
            if (activeAbilityCheck != null)
            {
                if (activeAbilityCheckId != checkId) throw new InvalidOperationException("A different ability check is already active.");
                return CloneAbilityCheck(activeAbilityCheck);
            }
            activeAbilityCheckId = checkId;
            activeAbilityCheck = EvaluateAbilityCheck(checkId);
            var setup = configuration.setups.Find(s => s.setupKey == activeRequest.Event.setupKey);
            if (PresentationSlots.Parse(setup).TryGetValue("checkDisplay", out var display) && display == "passive")
                PassiveCheckPresentationConfig.Hint(activeAbilityCheck);
            AbilityCheckEvaluated?.Invoke(activeAbilityCheck);
            QueueAbilityBranch(checkId, activeAbilityCheck);
            return CloneAbilityCheck(activeAbilityCheck);
        }

        private NarrativeAbilityCheckSnapshot EvaluateAbilityCheck(string checkId)
        {
            var definition = configuration.abilityChecks.Find(x => x != null && x.checkId == checkId);
            if (definition == null) throw new InvalidOperationException("Unknown ability check: " + checkId);
            var rows = configuration.abilityCheckConditions.FindAll(x => x != null && x.checkId == checkId);
            return NarrativeAbilityCheckEvaluator.Evaluate(definition, rows, AbilityReader,
                attributeId => AbilityNameResolver?.Invoke(attributeId) ?? attributeId);
        }

        private void QueueAbilityBranch(string checkId, NarrativeAbilityCheckSnapshot result)
        {
            var definition = configuration.abilityChecks.Find(x => x != null && x.checkId == checkId);
            string eventId = result.passed ? definition.successEventId : definition.failureEventId;
            if (string.IsNullOrWhiteSpace(eventId) || !events.TryGetValue(eventId, out var branch))
                throw new InvalidOperationException("Ability check branch event is missing: " + eventId);
            if (pendingEvents.Contains(eventId) || activeRequest.Event.eventId == eventId)
                throw new InvalidOperationException("Ability check branch cannot re-enter its active event: " + eventId);
            pendingEvents.Add(eventId);
            queue.Enqueue(new NarrativeEventRequest(branch, null, activeRequest.Depth + 1, null, Context(branch), passiveHint: string.IsNullOrEmpty(result.presentationWord) ? null : PassiveCheckPresentationConfig.Hint(result)));
            result.branchQueued = true;
        }

        private static NarrativeAbilityCheckSnapshot CloneAbilityCheck(NarrativeAbilityCheckSnapshot source)
        {
            if (source == null) return null;
            var clone = new NarrativeAbilityCheckSnapshot { checkId = source.checkId, presentationWord = source.presentationWord, passed = source.passed, branchQueued = source.branchQueued, resultApplied = source.resultApplied };
            if (source.values != null) foreach (var item in source.values)
                if (item != null) clone.values.Add(new NarrativeAbilityCheckValue { attributeId = item.attributeId, displayName = item.displayName, currentValue = item.currentValue, threshold = item.threshold, groupIndex = item.groupIndex, passed = item.passed });
            return clone;
        }

        public NarrativeSnapshot CaptureSnapshot(bool allowInitialization = false)
        {
            bool safeDraft = allowInitialization && valueRestore && operationDepth == 0 && !settling && !IsBusy && settledValueChanges.Count == 0;
            if (!CanCaptureSnapshot && !safeDraft) throw new InvalidOperationException("NCS snapshots cannot be captured during an atomic result transaction; save after EventCompleted.");
            NarrativeSnapshot snapshot = state.Capture();
            CaptureCafeVisits(snapshot);
            snapshot.selectedEndingId = SelectedEndingId;
            snapshot.officialDayVersion = migrateOfficialDay ? 0 : 1;
            snapshot.officialDayProcessed = officialDayProcessed;
            snapshot.location = Location; snapshot.timeSlot = TimeSlot; snapshot.randomSeed = randomSeed;
            snapshot.turnOpen = turnOpen; snapshot.turnEnding = turnEnding;
            snapshot.turnEvents.AddRange(pendingEvents);
            foreach (NarrativeResultDefinition effect in pendingEffects) snapshot.pendingEffects.Add(CloneEffect(effect));
            foreach (NarrativeEventRequest request in queue) snapshot.pending.Add(ToSaved(request));
            if (activePresentation) { snapshot.active = ToSaved(activeRequest); snapshot.active.selectedChoices.Clear(); snapshot.active.selectedChoices.AddRange(selectedChoices); }
            snapshot.activeAbilityCheck = CloneAbilityCheck(activeAbilityCheck);
            snapshot.activeAbilityCheckId = activeAbilityCheckId;
            foreach (NarrativeBarChange change in changes) snapshot.changes.Add(new NarrativeBarChange { key = change.key, oldValue = change.oldValue, newValue = change.newValue, depth = change.depth });
            if (operationDepth > 0 && operationValues != null)
            {
                var current = ReadAllValues();
                foreach (var pair in operationValues)
                    if (current.TryGetValue(pair.Key, out int next) && next != pair.Value)
                        snapshot.changes.Add(new NarrativeBarChange { key = pair.Key, oldValue = pair.Value, newValue = next, depth = transactionDepth });
                foreach (var change in operationDirectionalChanges.Values)
                    if (change.oldValue != change.newValue) snapshot.changes.Add(new NarrativeBarChange { key = change.key, oldValue = change.oldValue, newValue = change.newValue, depth = change.depth });
            }
            return snapshot;
        }

        public void RestoreSnapshot(NarrativeSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (settling) throw new InvalidOperationException("Restore cannot interrupt an NCS result transaction.");
            endingChecksEnabled = false;
            endingDirty = true;
            operationDepth = 0;
            SelectedEndingId = snapshot.selectedEndingId;
            migrateOfficialDay = snapshot.officialDayVersion == 0;
            officialDayProcessed = Math.Max(0, snapshot.officialDayProcessed);
            presentationEpoch++;
            queue.Clear(); pendingEvents.Clear(); changes.Clear(); selectedChoices.Clear(); pendingEffects.Clear(); safeCallbacks.Clear(); draining = false;
            activeAbilityCheck = CloneAbilityCheck(snapshot.activeAbilityCheck);
            activeAbilityCheckId = snapshot.activeAbilityCheckId ?? activeAbilityCheck?.checkId;
            resumeActiveAbilityCheck = snapshot.active != null && activeAbilityCheck != null;
            processing = false; activePresentation = false; pumping = false; transactionDepth = 0;
            resumeBoundary = snapshot.turnEnding;
            state.Restore(snapshot);
            RestoreCafeVisits(snapshot);
            restoredValueChanges = true;
            Location = snapshot.location ?? string.Empty; TimeSlot = snapshot.timeSlot;
            randomSeed = snapshot.randomSeed == 0 ? 0x6D2B79F5 : snapshot.randomSeed;
            turnOpen = snapshot.turnOpen; turnEnding = snapshot.turnEnding;
            if (snapshot.turnEvents != null) foreach (string id in snapshot.turnEvents) if (!string.IsNullOrEmpty(id)) pendingEvents.Add(id);
            if (snapshot.active != null) RestoreRequest(snapshot.active, true);
            if (snapshot.pending != null) foreach (NarrativePendingValue saved in snapshot.pending) RestoreRequest(saved, false);
            if (snapshot.changes != null) foreach (NarrativeBarChange change in snapshot.changes) if (change != null && state.GetDefinition(state.ResolveRelation(change.key) ?? change.key) != null) changes.Add(change);
            if (snapshot.pendingEffects != null) foreach (NarrativeResultDefinition effect in snapshot.pendingEffects)
                if (effect != null && effect.resultType == NarrativeResultType.PlaySetup) { pendingEffects.Add(CloneEffect(effect)); NotifyEffect(effect); }
            // Resume only after the caller explicitly pumps EndTurn; old presenter callbacks are invalidated.
            if (queue.Count > 0 || pendingEffects.Count > 0 || turnEnding) { resumeBoundary = true; turnOpen = true; turnEnding = false; }
        }

        private NarrativePendingValue ToSaved(NarrativeEventRequest request)
        {
            var saved = new NarrativePendingValue { eventId = request.Event.eventId, triggerId = request.Trigger?.triggerId, requestId = request.RequestId, passiveHint = request.PassiveHint, depth = request.Depth, timeSlot = request.Context?.TimeSlot ?? TimeSlot, location = request.Context?.Location ?? Location };
            if (request.SelectedChoices != null) saved.selectedChoices.AddRange(request.SelectedChoices);
            return saved;
        }
        private void RestoreRequest(NarrativePendingValue saved, bool wasActive)
        {
            if (saved == null || saved.depth < 0 || saved.depth > 3 || !events.TryGetValue(saved.eventId ?? string.Empty, out NarrativeEventDefinition definition)) return;
            NarrativeTriggerDefinition trigger = configuration.triggers.Find(t => t != null && t.triggerId == saved.triggerId);
            var context = Context(definition); context.TimeSlot = saved.timeSlot; context.Location = saved.location ?? Location;
            queue.Enqueue(new NarrativeEventRequest(definition, trigger, saved.depth, saved.selectedChoices == null ? null : saved.selectedChoices.ToArray(), context, saved.requestId, saved.passiveHint));
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
                if (condition == null || string.IsNullOrWhiteSpace(condition.ownerId)) throw new ArgumentException("Condition row requires an owner ID: " + condition?.conditionId);
                string ownerKey = OwnerKey(condition.ownerType, condition.ownerId);
                if (!conditionsByOwner.TryGetValue(ownerKey, out List<NarrativeConditionDefinition> list))
                    conditionsByOwner[ownerKey] = list = new List<NarrativeConditionDefinition>();
                list.Add(condition);
            }
            var endingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ending in configuration.endings)
            {
                if (ending == null || string.IsNullOrWhiteSpace(ending.endingId) || !endingIds.Add(ending.endingId)
                    || !conditionsByOwner.ContainsKey(OwnerKey("ending", ending.endingId)))
                    throw new ArgumentException("Invalid ending or missing conditions: " + ending?.endingId);
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
            NotifyConditionsChanged();
            if (valueRestore) return;
            if (operationDepth > 0)
            {
                if (barKey.Contains(">"))
                {
                    if (!operationDirectionalChanges.TryGetValue(barKey, out var directionChange))
                        operationDirectionalChanges[barKey] = directionChange = new NarrativeBarChange { key = barKey, oldValue = oldValue, depth = transactionDepth };
                    directionChange.newValue = newValue;
                }
                return;
            }
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
            if (valueRestore || !valueBaselineReady || operationDepth > 0) return;
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

        private void OnPresentationCancelled(string reason)
        {
            bool terminal = reason == "disposed" || reason == "host_unavailable";
            if (!activePresentation && !draining && pendingEffects.Count == 0 && !terminal) return;
            string failedEvent = activeRequest.Event?.eventId;
            presentationEpoch++;
            activePresentation = draining = processing = false;
            activeRequest = default;
            activeAbilityCheck = null;
            activeAbilityCheckId = null;
            selectedChoices.Clear(); pendingEffects.Clear();
            if (terminal) presenterUnavailable = true;
            if (!string.IsNullOrEmpty(failedEvent))
            {
                pendingEvents.Remove(failedEvent);
                CompleteCafeHook(failedEvent);
                try { EventFailed?.Invoke(failedEvent, "presentation_cancelled: " + reason); }
                catch (Exception error) { Debug.LogException(error); }
            }
            if (terminal)
            {
                presenterUnavailable = true;
                queue.Clear(); pendingEvents.Clear();
                turnOpen = turnEnding = resumeBoundary = false;
                if (presenter is INarrativePresentationCancellation cancellation) cancellation.Cancelled -= OnPresentationCancelled;
            }
            // During Play's synchronous callback the outer pump owns continuation.
            if (!pumping && !settling) ProcessQueue();
        }

        private void ProcessQueue()
        {
            if (presenterUnavailable)
            {
                queue.Clear(); pendingEffects.Clear(); processing = activePresentation = draining = false;
                CheckEndingsAtSafeBoundary();
                return;
            }
            if (valueRestore || !valueBaselineReady || IsEnded || pumping || draining) return;
            DrainSettledValueChanges();
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
                while (!presenterUnavailable && !activePresentation && queue.Count > 0 && pendingEffects.Count == 0)
                {
                    activeRequest = queue.Dequeue();
                    selectedChoices.Clear();
                    if (!resumeActiveAbilityCheck && (activeRequest.SelectedChoices == null || activeRequest.SelectedChoices.Count == 0))
                    {
                        activeAbilityCheck = null;
                        activeAbilityCheckId = null;
                    }
                    resumeActiveAbilityCheck = false;
                    if (activeRequest.SelectedChoices != null) selectedChoices.AddRange(activeRequest.SelectedChoices);
                    activePresentation = true;
                    long epoch = ++presentationEpoch;
                    NarrativeEventRequest request = new NarrativeEventRequest(activeRequest.Event, activeRequest.Trigger, activeRequest.Depth, selectedChoices.ToArray(), activeRequest.Context, activeRequest.RequestId, activeRequest.PassiveHint);
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
            CheckEndingsAtSafeBoundary();
        }

        private void CompleteEvent(NarrativeEventRequest request)
        {
            presentationEpoch++;
            settling = true;
            int entryOperationDepth = operationDepth;
            transactionDepth = request.Depth + 1;
            var batch = new List<NarrativeResultDefinition>();
            if (resultsByEvent.TryGetValue(request.Event.eventId, out var eventResults)) batch.AddRange(eventResults);
            foreach (string id in selectedChoices)
            {
                NarrativeChoiceDefinition choice = configuration.choices.Find(c => c != null && c.choiceId == id);
                string owner = choice == null ? null : string.IsNullOrWhiteSpace(choice.resultOwnerId) ? choice.choiceId : choice.resultOwnerId;
                if (owner != null && resultsByEvent.TryGetValue(owner, out var choiceResults)) batch.AddRange(choiceResults);
            }
            if (activeAbilityCheck != null)
            {
                NarrativeAbilityCheckDefinition check = configuration.abilityChecks.Find(x => x != null && x.checkId == activeAbilityCheckId);
                string owner = check == null ? null : activeAbilityCheck.passed ? check.successResultOwnerId : check.failureResultOwnerId;
                string branchEventId = check == null ? null : activeAbilityCheck.passed ? check.successEventId : check.failureEventId;
                if (!string.IsNullOrWhiteSpace(owner) && owner != branchEventId
                    && resultsByEvent.TryGetValue(owner, out var checkResults)) batch.AddRange(checkResults);
            }
            try
            {
                PreflightResults(batch, out var boolValues, out var itemCounts, out int? moneyBefore, out int? moneyAfter);
                BeginOperation();
                if (moneyBefore.HasValue && !MoneyAccount.TryCommit(moneyBefore.Value, moneyAfter.Value, out string moneyError))
                    throw new InvalidOperationException("Money batch rejected: " + moneyError);
                // Both stores expose only the completed bool/item batch to change listeners.
                using (inventory?.DeferNotifications())
                {
                    if (itemCounts.Count > 0 && !inventory.TryApplyCounts(itemCounts)) throw new InvalidOperationException("Inventory rejected validated result batch.");
                    state.CommitConditions(boolValues);
                }
                foreach (var result in batch) ApplyResult(result);
                state.MarkEventHappened(request.Event.eventId);
                activePresentation = false;
                activeAbilityCheck = null;
                activeAbilityCheckId = null;
                selectedChoices.Clear();
                EndOperation();
                DrainSettledValueChanges();
                EvaluateBoundary(transactionDepth, false);
            }
            catch (Exception error)
            {
                Debug.LogError($"[NCS] Event {request.Event.eventId} result transaction failed: {error.Message}");
                try { EventFailed?.Invoke(request.Event.eventId, error.Message); }
                catch (Exception listenerError) { Debug.LogException(listenerError); }
                return;
            }
            finally
            {
                if (operationDepth > entryOperationDepth)
                {
                    operationDepth = entryOperationDepth;
                    if (operationDepth == 0 && operationValues != null) { CommitValueComparison(operationValues); operationValues = null; }
                }
                transactionDepth = 0; settling = false;
            }
            CompleteCafeHook(request.Event.eventId);
            try { EventCompleted?.Invoke(request.Event.eventId); }
            catch (Exception error) { Debug.LogException(error); }
            ProcessQueue();
        }

        private void PreflightResults(List<NarrativeResultDefinition> results, out Dictionary<string, bool> boolValues, out Dictionary<string, int> itemCounts, out int? moneyBefore, out int? moneyAfter)
        {
            moneyBefore = moneyAfter = null;
            boolValues = new Dictionary<string, bool>(StringComparer.Ordinal);
            itemCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var result in results)
            {
                string target = StripPrefix(result.target, "flag:", "item:", "event:");
                try
                {
                    if (result.resultType == NarrativeResultType.BarAdd || result.resultType == NarrativeResultType.BarSet)
                    {
                        string barKey = StripPrefix(result.target, "bar:");
                        if (!state.IsWritableBar(barKey)) throw new InvalidOperationException("Bar is external, derived or unknown: " + barKey);
                    }
                    else if (result.resultType == NarrativeResultType.MoneyAdd || result.resultType == NarrativeResultType.MoneyRemove)
                    {
                        if (target != "cafe" || MoneyAccount == null || !int.TryParse(result.value, out int amount) || amount <= 0)
                            throw new InvalidOperationException("Expected ready cafe money account and positive amount.");
                        if (!moneyBefore.HasValue)
                        {
                            if (!MoneyAccount.TryRead(out int balance)) throw new InvalidOperationException("Money account is not ready.");
                            moneyBefore = moneyAfter = balance;
                        }
                        long next = (long)moneyAfter.Value + (result.resultType == NarrativeResultType.MoneyAdd ? amount : -amount);
                        if (next < 0 || next > int.MaxValue) throw new InvalidOperationException("Insufficient money or balance overflow.");
                        moneyAfter = (int)next;
                    }
                    else if (result.resultType == NarrativeResultType.SetFlag || result.resultType == NarrativeResultType.ClearFlag)
                    {
                        bool value = false;
                        if (result.resultType == NarrativeResultType.SetFlag && !bool.TryParse(result.value, out value)) throw new InvalidOperationException("Expected true/false.");
                        if (result.resultType == NarrativeResultType.ClearFlag && !string.IsNullOrWhiteSpace(result.value)
                            && (!bool.TryParse(result.value, out bool clearValue) || clearValue)) throw new InvalidOperationException("CLEAR_FLAG requires false or empty value.");
                        state.ValidateConditionWrite(target, value);
                        boolValues[target] = value;
                    }
                    else if (result.resultType == NarrativeResultType.QueueEvent)
                    {
                        if (string.IsNullOrWhiteSpace(target) || !events.ContainsKey(target)) throw new InvalidOperationException("Unknown queued event: " + target);
                    }
                    else if (result.resultType == NarrativeResultType.ItemAdd || result.resultType == NarrativeResultType.ItemRemove)
                    {
                        if (inventory == null || string.IsNullOrWhiteSpace(target) || !int.TryParse(result.value, out int count) || count <= 0) throw new InvalidOperationException("Expected inventory, item key and positive quantity.");
                        int current = itemCounts.TryGetValue(target, out int previous) ? previous : inventory.GetCount(target);
                        long next = (long)current + (result.resultType == NarrativeResultType.ItemAdd ? count : -count);
                        if (next < 0 || next > int.MaxValue) throw new InvalidOperationException("Insufficient items or quantity overflow: " + target);
                        itemCounts[target] = (int)next;
                    }
                }
                catch (Exception error) { throw new InvalidOperationException($"Result {result.resultId} (owner={result.eventId}): {error.Message}", error); }
            }
        }

        private void FinishTurn()
        {
            // The new date is evaluated at the next boundary, never inside the just-finished day.
            var derived = ReadDerived();
            foreach (NarrativeBarDefinition bar in configuration.bars)
            {
                if (bar == null || officialDayManaged && bar.barKey == "world.calendar" || bar.advanceMode != NarrativeAdvanceMode.Monotonic && bar.barKey != "world.calendar") continue;
                int delta = bar.barKey == "world.calendar" ? 1 : Math.Max(0, bar.autoStep);
                if (state.TryAddBar(bar.barKey, delta, out int oldValue, out int newValue))
                {
                    NotifyConditionsChanged();
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
            string target = StripPrefix(result.target, "bar:", "rel:", "flag:", "switch:", "obj:", "event:", "ach:", "setup:", "item:");
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
                // Bool/item writes were preflighted and committed together before other effects.
                case NarrativeResultType.SetFlag:
                case NarrativeResultType.ClearFlag:
                case NarrativeResultType.ItemAdd:
                case NarrativeResultType.ItemRemove:
                case NarrativeResultType.MoneyAdd:
                case NarrativeResultType.MoneyRemove:
                    break;
                case NarrativeResultType.SetSwitch:
                    if (!bool.TryParse(result.value, out bool on)) return;
                    state.SetSwitch(target, on); break;
                case NarrativeResultType.ObjectState: state.SetObjectState(target, result.value); break;
                case NarrativeResultType.QueueEvent:
                    if (!events.TryGetValue(target, out var next)) throw new InvalidOperationException("Missing next event: " + target);
                    if (activeRequest.Event.eventId != target && !queue.Any(r => r.Event.eventId == target))
                    {
                        pendingEvents.Add(target);
                        queue.Enqueue(new NarrativeEventRequest(next, null, 0, null, Context(next)));
                    }
                    break;
                case NarrativeResultType.UnlockEvent: state.SetEventLocked(target, false); break;
                case NarrativeResultType.LockEvent: state.SetEventLocked(target, true); break;
                case NarrativeResultType.ReserveCafeVisit: ReserveCafeVisit(target, StripPrefix(result.value, "visit:")); break;
                case NarrativeResultType.SpawnNpc: MoveCafeNpc(target, StripPrefix(result.value, "scene:")); break;
                case NarrativeResultType.DespawnNpc: MoveCafeNpc(target, string.Empty); break;
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
            string target = StripPrefix(result.target, "bar:", "rel:", "flag:", "switch:", "obj:", "event:", "ach:", "setup:", "item:");
            try { EffectRequested?.Invoke(new NarrativeEffectNotification { Result = result, Target = target, Value = result.value }); }
            catch (Exception error) { Debug.LogException(error); }
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
