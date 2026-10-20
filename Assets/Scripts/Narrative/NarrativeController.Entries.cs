using System;
using System.Collections.Generic;
using System.Linq;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativeController
    {
        private readonly List<NarrativeCandidateValue> candidates = new List<NarrativeCandidateValue>();
        private readonly HashSet<string> warnedPresentationDependencies = new HashSet<string>(StringComparer.Ordinal);
        private long candidateSequence;
        private int morningEntryProcessedDay;
        private NarrativeEntryPoint? activeEntry;
        private int activeEntryDay;
        private bool entryFailed;
        private Action<bool> entryCompleted;

        public IReadOnlyList<NarrativeCandidateValue> WaitingCandidates => candidates.Select(CloneCandidate).ToArray();

        /// <summary>Runs one formal phase on the existing playback queue. False means not accepted.
        /// The callback runs at a safe boundary, true only after the whole batch succeeds.
        /// Morning is idempotent per day; a failed batch remains retryable.</summary>
        public bool ProcessEntry(NarrativeEntryPoint entry, Action<bool> completed = null)
        {
            if (entry == NarrativeEntryPoint.MapAction || !Enum.IsDefined(typeof(NarrativeEntryPoint), entry)
                || activeEntry.HasValue || IsEnded || presenterUnavailable || !IsSafeBoundary || IsBusy) return false;
            int day = GetDay();
            if (entry == NarrativeEntryPoint.MorningBeforeMap && morningEntryProcessedDay == day)
            {
                completed?.Invoke(true);
                return true;
            }
            activeEntry = entry; activeEntryDay = day; entryFailed = false; entryCompleted = completed;
            if (entry != NarrativeEntryPoint.Immediate) TimeSlot = EntryTimeSlot(entry);
            pendingEvents.Clear();
            settling = true;
            try { EvaluateBoundary(0, true); }
            catch
            {
                activeEntry = null; entryCompleted = null; entryFailed = false;
                throw;
            }
            finally { settling = false; }
            ProcessQueue();
            return true;
        }

        public bool QueueMapActionEvent(string eventId) => QueueManualEvent(eventId, true);
        public NarrativeEventDiagnostic ExplainMapActionEvent(string eventId) => ExplainMapActionEvent(eventId, ignoreTransientBusy: false);
        internal NarrativeEventDiagnostic ExplainMapActionEvent(string eventId, bool ignoreTransientBusy)
            => ExplainEventAtEntry(eventId, true, ignoreTransientBusy);

        private bool QueueManualEvent(string eventId, bool mapAction)
        {
            if (activeEntry.HasValue || GetEventRejection(eventId, mapAction) != null) return false;
            var definition = events[eventId];
            if (!turnOpen) BeginTurn();
            // Collect current Hold/Cross eligibility without bypassing the explicit MapAction entrance.
            EvaluateBoundary(0, true, false);
            // An explicit map click may consume a previously earned Cross candidate, but never invents one.
            var candidate = candidates.Where(c => c.eventId == eventId && CanStartCandidate(c))
                .OrderByDescending(c => c.priority).ThenBy(c => c.row).ThenBy(c => c.sequence).FirstOrDefault();
            if (candidate == null && definition.entry == NarrativeEntryPoint.MapAction
                && configuration.triggers.Any(t => t != null && t.enabled && t.eventId == eventId)) return false;
            if (candidate == null)
            {
                candidate = new NarrativeCandidateValue { eventId = eventId, entry = definition.entry,
                    source = NarrativeCandidateSource.Manual, day = GetDay(), sequence = ++candidateSequence };
                candidates.Add(candidate);
            }
            EnqueueCandidate(candidate);
            ProcessQueue();
            return true;
        }

        private static NarrativeTimeSlot EntryTimeSlot(NarrativeEntryPoint entry)
        {
            switch (entry)
            {
                case NarrativeEntryPoint.MorningBeforeMap: return NarrativeTimeSlot.Morning;
                case NarrativeEntryPoint.AfterBusiness: return NarrativeTimeSlot.Noon;
                case NarrativeEntryPoint.BeforeNight: return NarrativeTimeSlot.Night;
                default: throw new ArgumentException("Not a date phase: " + entry);
            }
        }

        private static NarrativeEntryPoint DateEntry(NarrativeTimeSlot slot) => slot == NarrativeTimeSlot.Morning
            ? NarrativeEntryPoint.MorningBeforeMap : slot == NarrativeTimeSlot.Noon ? NarrativeEntryPoint.AfterBusiness : NarrativeEntryPoint.BeforeNight;

        private bool DateBoundaryMatches(NarrativeEventDateDefinition date)
        {
            // Legacy isolated turns still honor their REAL TimeSlot. Formal days require the explicit phase API.
            return activeEntry.HasValue ? activeEntry.Value == DateEntry(date.timeSlot)
                : !officialDayManaged && TimeSlot == date.timeSlot;
        }

        public string ConstructionBlocker(NarrativeEventDefinition definition)
            => definition?.eventProperties?.FirstOrDefault(p => p.property == "blockedBy")?.value;

        /// <summary>Read-only guards for the active root; never exports transient choices as world facts.</summary>
        public bool ReadPresentationCondition(string key) => TryReadPresentationCondition(key, out bool value) && value;

        private string assumedGuardKey;
        private bool assumedGuardValue;

        /// <summary>
        /// Read-only evaluation as if one world guard had another value, e.g. "is this map task still
        /// outstanding" while the player stands in the cafe and world.map.active is false. No state is written.
        /// </summary>
        public T EvaluateAssumingGuard<T>(string key, bool value, Func<T> evaluate)
        {
            string previousKey = assumedGuardKey; bool previousValue = assumedGuardValue;
            assumedGuardKey = key; assumedGuardValue = value;
            try { return evaluate(); }
            finally { assumedGuardKey = previousKey; assumedGuardValue = previousValue; }
        }

        private bool TryReadPresentationCondition(string key, out bool value, int depth = 0)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(key) || depth > 64) throw new InvalidOperationException("Empty or over-nested presentation condition.");
            key = key.Trim();
            if (key.StartsWith("not:", StringComparison.Ordinal))
            {
                if (!TryReadPresentationCondition(key.Substring(4), out bool inner, depth + 1)) return false;
                value = !inner; return true;
            }
            if (key.StartsWith("bar:", StringComparison.Ordinal))
            {
                if (!NarrativePresentationGuards.TryParseNumeric(key, out string bar, out string op, out int bound))
                    throw new InvalidOperationException("Invalid numeric presentation condition: " + key);
                if (!state.TryGetBar(bar, out int current)) return UnavailablePresentationGuard(key);
                value = NarrativePresentationGuards.Compare(current, op, bound); return true;
            }
            if (key.StartsWith("gameplay:", StringComparison.Ordinal))
            {
                if (!NarrativePresentationGuards.TryParseGameplay(key, out string page)) throw new InvalidOperationException("Invalid gameplay condition: " + key);
                if (!activePresentation || !gameplayResults.TryGetValue(page, out value)) return UnavailablePresentationGuard(key);
                return true;
            }
            if (key.StartsWith("choice:", StringComparison.Ordinal)) { value = selectedChoices.Contains(key.Substring(7)); return true; }
            if (key.StartsWith("anyChoice:", StringComparison.Ordinal)) { value = key.Substring(10).Split(';').Any(selectedChoices.Contains); return true; }
            if (key.StartsWith("check:", StringComparison.Ordinal))
            {
                string checkId = key.Substring(6);
                if (string.IsNullOrWhiteSpace(checkId)) throw new InvalidOperationException("Empty presentation ability check condition.");
                if (!inlineAbilityChecks.TryGetValue(checkId, out NarrativeAbilityCheckSnapshot snapshot))
                    throw new InvalidOperationException("Presentation ability check has not been evaluated: " + checkId);
                value = snapshot.passed; return true;
            }
            if (key.StartsWith("dependency:", StringComparison.Ordinal))
            {
                string dependency = key.Substring("dependency:".Length).Trim();
                string resolvedCondition = string.Equals(dependency, "C-B06.history-specialization", StringComparison.OrdinalIgnoreCase)
                    ? "specialization.player.letter.politics" : null;
                if (resolvedCondition != null && state.TryGetCondition(resolvedCondition, out value)) return true;
                // Preserve the existing explicitly declared construction-dependency fallback.
                UnavailablePresentationGuard(key); value = false; return true;
            }
            if (assumedGuardKey != null && string.Equals(key, assumedGuardKey, StringComparison.Ordinal))
            { value = assumedGuardValue; return true; }
            if (!state.TryGetCondition(key, out value)) throw new InvalidOperationException("Unknown presentation condition: " + key);
            return true;
        }

        private bool UnavailablePresentationGuard(string key)
        {
            if (warnedPresentationDependencies.Add(key))
                UnityEngine.Debug.LogWarning("[NCS] Presentation dependency unavailable; dependent page will be skipped: " + key);
            return false;
        }

        private bool EventCanStart(NarrativeEventDefinition definition, bool evaluateConditions = true) => definition != null
            && string.IsNullOrWhiteSpace(ConstructionBlocker(definition))
            && !state.IsEventLocked(definition.eventId) && !state.IsFlagSet("event.once." + definition.eventId)
            && !(definition.onceOnly && state.HasHappenedEvent(definition.eventId))
            && (!evaluateConditions || EvaluateConditions("event", string.IsNullOrWhiteSpace(definition.conditionOwnerId) ? definition.eventId : definition.conditionOwnerId, Context(definition)));

        private bool CanStartCandidate(NarrativeCandidateValue candidate)
        {
            if (!events.TryGetValue(candidate.eventId ?? string.Empty, out var definition)
                || !EventCanStart(definition, !candidate.conditionsFrozen)) return false;
            if (!string.IsNullOrEmpty(candidate.triggerId))
            {
                var trigger = configuration.triggers.Find(t => t != null && t.triggerId == candidate.triggerId);
                if (trigger == null || !trigger.enabled || state.IsFlagSet("trigger.once." + trigger.triggerId)
                    || trigger.onceType == NarrativeOnceType.Once && state.HasFiredTrigger(trigger.triggerId)) return false;
                // Cross uses the captured fact/count, not the current bar or the current shared counter.
                if (candidate.source == NarrativeCandidateSource.Hold
                    && (!state.TryGetBar(trigger.barKey, out int current) || !IsInside(trigger, current)
                        || !trigger.MatchesReach(state.GetCounter(CounterKey(trigger))))) return false;
            }
            if (candidate.source == NarrativeCandidateSource.Date)
            {
                if (candidate.day != GetDay() || !DateMatches(new NarrativeEventDateDefinition
                    { day = candidate.dateDay, repeatRule = candidate.repeatRule }, GetDay())) return false;
                if (candidate.repeatRule == NarrativeRepeatRule.Once && state.IsFlagSet(DateOnceKey(candidate))) return false;
            }
            // A manual click cannot bypass a date restriction even when Event has the legacy Immediate default.
            var dates = configuration.eventDates.Where(d => d != null && d.eventId == candidate.eventId).ToArray();
            if (dates.Length > 0 && !dates.Any(d => DateMatches(d, GetDay()) && DateBoundaryMatches(d))) return false;
            return true;
        }

        private void CaptureHit(Hit hit, int depth)
        {
            var trigger = hit.Trigger;
            if (depth > 3 || trigger != null && (state.IsFlagSet("trigger.once." + trigger.triggerId)
                || trigger.onceType == NarrativeOnceType.Once && state.HasFiredTrigger(trigger.triggerId))) return;
            var source = hit.Date != null ? NarrativeCandidateSource.Date : trigger.crossType == NarrativeCrossType.Cross ? NarrativeCandidateSource.Cross : NarrativeCandidateSource.Hold;
            var entry = hit.Date == null ? hit.Event.entry : DateEntry(hit.Date.timeSlot);
            // EventDate is a constraint, not an alternate bypass for a differently routed event.
            if (hit.Date != null && hit.Event.entry != NarrativeEntryPoint.Immediate && hit.Event.entry != entry) return;
            if (hit.Event.onceOnly && state.HasHappenedEvent(hit.Event.eventId)) return;
            if (candidates.Any(c => c.eventId == hit.Event.eventId && c.source == source && c.triggerId == trigger?.triggerId
                && (source == NarrativeCandidateSource.Hold || source == NarrativeCandidateSource.Cross && trigger.onceType == NarrativeOnceType.Once || c.count == hit.Count && c.day == GetDay())
                && (hit.Date == null || c.dateDay == hit.Date.day && c.timeSlot == hit.Date.timeSlot))) return;
            candidates.Add(new NarrativeCandidateValue
            {
                eventId = hit.Event.eventId, triggerId = trigger?.triggerId, source = source, entry = entry,
                priority = hit.Priority, row = hit.Row, count = hit.Count, depth = depth, day = GetDay(),
                dateDay = hit.Date?.day ?? 0, timeSlot = hit.Date?.timeSlot ?? TimeSlot,
                repeatRule = hit.Date?.repeatRule ?? NarrativeRepeatRule.Once, sequence = ++candidateSequence
            });
        }

        private void PromoteCandidates()
        {
            if (entryFailed) return;
            candidates.RemoveAll(c => c.source == NarrativeCandidateSource.Date && c.day != GetDay());
            foreach (var candidate in candidates.OrderByDescending(c => c.priority).ThenBy(c => c.row).ThenBy(c => c.sequence).ToArray())
            {
                bool allowed = candidate.entry == NarrativeEntryPoint.Immediate || activeEntry == candidate.entry
                    || !officialDayManaged && !activeEntry.HasValue && candidate.source == NarrativeCandidateSource.Date && candidate.timeSlot == TimeSlot;
                if (!allowed || pendingEvents.Contains(candidate.eventId) || !CanStartCandidate(candidate)) continue;
                EnqueueCandidate(candidate);
            }
        }

        private void EnqueueCandidate(NarrativeCandidateValue candidate)
        {
            var definition = events[candidate.eventId];
            var trigger = configuration.triggers.Find(t => t != null && t.triggerId == candidate.triggerId);
            // Preserve the condition decision made when this candidate entered the queue.
            // Fresh locks, completion markers, date and phase checks remain live.
            candidate.conditionsFrozen = true;
            pendingEvents.Add(candidate.eventId);
            var request = new NarrativeEventRequest(definition, trigger, candidate.depth, context: Context(definition), candidate: candidate);
            queue.Enqueue(request);
            try { EventQueued?.Invoke(request); }
            catch (Exception error) { UnityEngine.Debug.LogException(error); }
        }

        private void CommitCandidate(NarrativeEventRequest request)
        {
            if (request.Event.onceOnly) state.SetFlag("event.once." + request.Event.eventId, true);
            if (request.Trigger != null && request.Trigger.onceType == NarrativeOnceType.Once)
            {
                state.MarkTriggerFired(request.Trigger.triggerId);
                state.SetFlag("trigger.once." + request.Trigger.triggerId, true);
            }
            var candidate = request.Candidate;
            if (candidate == null) return;
            if (candidate.source == NarrativeCandidateSource.Date && candidate.repeatRule == NarrativeRepeatRule.Once) state.SetFlag(DateOnceKey(candidate), true);
            candidates.RemoveAll(c => c.sequence == candidate.sequence || request.Event.onceOnly && c.eventId == request.Event.eventId
                || request.Trigger != null && request.Trigger.onceType == NarrativeOnceType.Once && c.triggerId == request.Trigger.triggerId);
        }

        private static string DateOnceKey(NarrativeCandidateValue candidate) => "date.once." + candidate.eventId + "." + candidate.dateDay + "." + candidate.timeSlot;

        private void CompleteEntryAtSafeBoundary()
        {
            if (!activeEntry.HasValue || !IsSafeBoundary || IsBusy) return;
            bool success = !entryFailed;
            if (success && activeEntry == NarrativeEntryPoint.MorningBeforeMap) morningEntryProcessedDay = activeEntryDay;
            var callback = entryCompleted;
            activeEntry = null; entryCompleted = null; entryFailed = false;
            callback?.Invoke(success);
        }

        private void FailEntryBatch()
        {
            if (!activeEntry.HasValue) return;
            entryFailed = true;
            // Eligibility stays in candidates; no failure consumes the entrance or a once marker.
            queue.Clear(); pendingEvents.Clear();
        }

        private void PrependContinuation(NarrativeEventRequest request)
        {
            // Keep authored continuation order ahead of unrelated eligible inserts, in ONE actual queue.
            var tail = queue.ToList();
            int index = tail.FindIndex(r => r.Candidate?.source != NarrativeCandidateSource.Continuation);
            if (index < 0) index = tail.Count;
            tail.Insert(index, request);
            queue.Clear();
            foreach (var item in tail) queue.Enqueue(item);
        }

        private NarrativeCandidateValue ContinuationCandidate(string eventId) => new NarrativeCandidateValue
        { eventId = eventId, entry = NarrativeEntryPoint.Immediate, source = NarrativeCandidateSource.Continuation, sequence = ++candidateSequence, day = GetDay(), conditionsFrozen = true };

        private static NarrativeCandidateValue CloneCandidate(NarrativeCandidateValue c) => c == null ? null : new NarrativeCandidateValue
        {
            eventId = c.eventId, triggerId = c.triggerId, source = c.source, entry = c.entry, priority = c.priority,
            row = c.row, count = c.count, depth = c.depth, day = c.day, dateDay = c.dateDay, sequence = c.sequence,
            timeSlot = c.timeSlot, repeatRule = c.repeatRule, conditionsFrozen = c.conditionsFrozen
        };
    }
}
