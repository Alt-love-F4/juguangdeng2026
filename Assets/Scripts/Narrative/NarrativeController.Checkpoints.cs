using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativeController
    {
        private NarrativeCheckpointSave checkpoint;
        private bool checkpointResuming;
        private long activePresentationToken;

        public bool BusinessCheckpointConfirmed => HasPendingBusinessCheckpoint && checkpoint.confirmed;
        public bool HasPendingBusinessCheckpoint => checkpoint != null && !checkpointResuming && checkpoint.wait == NarrativeCheckpointWait.Business;
        public bool HasPendingNightCheckpoint => checkpoint != null && !checkpointResuming && checkpoint.wait == NarrativeCheckpointWait.Night;
        public int CurrentCheckpointStage => activePresentation && IsCheckpointRoot(activeRequest) ? checkpoint.stage : 0;

        public bool TryBeginCheckpointBusiness(int day, out string token)
        {
            token = null;
            if (!HasPendingBusinessCheckpoint || day != GetDay() || day != checkpoint.day || checkpoint.confirmed
                || !string.IsNullOrEmpty(checkpoint.token) || IsBusy || IsEnded || presenterUnavailable || !IsSafeBoundary) return false;
            token = checkpoint.token = Guid.NewGuid().ToString("N");
            return true;
        }

        public bool ConfirmCheckpointBusiness(int day, string token)
        {
            if (!HasPendingBusinessCheckpoint || day != GetDay() || day != checkpoint.day || checkpoint.confirmed
                || string.IsNullOrEmpty(token) || token != checkpoint.token || IsEnded) return false;
            checkpoint.confirmed = true;
            return true;
        }

        public bool ResumeCheckpoint(NarrativeEntryPoint entry, Action<bool> completed = null)
        {
            if (!CheckpointMatchesEntry(entry) || activeEntry.HasValue || IsEnded || presenterUnavailable || !IsSafeBoundary || IsBusy
                || !CheckpointCanResume()) return false;
            return ProcessEntry(entry, completed);
        }

        private bool CheckpointMatchesEntry(NarrativeEntryPoint entry) => checkpoint != null && !checkpointResuming
            && (entry == NarrativeEntryPoint.AfterBusiness && checkpoint.wait == NarrativeCheckpointWait.Business && checkpoint.confirmed
                || entry == NarrativeEntryPoint.BeforeNight && checkpoint.wait == NarrativeCheckpointWait.Night);

        private bool CheckpointCanResume() => checkpoint != null && checkpoint.day == GetDay()
            && events.TryGetValue(checkpoint.root.eventId, out var definition)
            && string.IsNullOrWhiteSpace(ConstructionBlocker(definition)) && !state.IsEventLocked(definition.eventId)
            && !state.IsFlagSet("event.once." + definition.eventId) && !(definition.onceOnly && state.HasHappenedEvent(definition.eventId));

        private bool IsCheckpointRoot(NarrativeEventRequest request) => checkpoint != null && request.Event != null
            && request.RequestId == checkpoint.root.requestId && request.Event.eventId == checkpoint.root.eventId;

        // Resume authorization is owned by this controller, never by a caller-supplied page id/candidate.
        private bool IsCheckpointContinuation(NarrativeEventRequest request) => checkpointResuming && IsCheckpointRoot(request)
            && request.ResumePageId == checkpoint.resumePageId;

        private void EnqueueCheckpoint()
        {
            var saved = checkpoint.root;
            var definition = events[saved.eventId];
            var context = Context(definition); context.TimeSlot = TimeSlot; context.Location = saved.location ?? Location;
            var request = new NarrativeEventRequest(definition, configuration.triggers.Find(t => t != null && t.triggerId == saved.triggerId),
                saved.depth, saved.selectedChoices?.ToArray(), context, saved.requestId, saved.passiveHint,
                candidate: CloneCandidate(saved.candidate), resumePageId: checkpoint.resumePageId, cafeVisit: saved.cafeVisit);
            checkpointResuming = true;
            queue.Enqueue(request);
            pendingEvents.Add(definition.eventId);
        }

        private void OnCheckpointReached(NarrativeEventRequest request, string nextPageId, NarrativeCheckpointWait wait)
        {
            if (!activePresentation || request.PresentationToken != activePresentationToken || request.Event == null || request.RequestId != activeRequest.RequestId
                || request.Event != activeRequest.Event || request.ResumePageId != activeRequest.ResumePageId || string.IsNullOrWhiteSpace(nextPageId)
                || !Enum.IsDefined(typeof(NarrativeCheckpointWait), wait)) return;
            // Only one retained chapter can own the checkpoint protocol at a time.
            if (checkpoint != null && !IsCheckpointRoot(activeRequest)) return;
            NarrativeCafeVisitContext serviceContext = null;
            if (wait == NarrativeCheckpointWait.Service)
            {
                serviceContext = activeRequest.CafeVisit?.Copy();
                if (!ValidServiceContext(serviceContext))
                    throw new InvalidOperationException("WaitService requires a live, explicit visit/order hook context.");
            }
            var retainedService = IsCheckpointRoot(activeRequest) ? checkpoint.serviceReceipt?.Copy() : null;
            int stage = CurrentCheckpointStage + 1;
            var root = ToSaved(activeRequest);
            root.selectedChoices.Clear(); root.selectedChoices.AddRange(selectedChoices);
            root.resumePageId = nextPageId;
            checkpoint = new NarrativeCheckpointSave { root = root, resumePageId = nextPageId, day = GetDay(), wait = wait, stage = stage,
                serviceContext = serviceContext ?? (IsCheckpointRoot(activeRequest) ? checkpoint.serviceContext?.Copy() : null),
                serviceReceipt = retainedService }; 
            if (wait == NarrativeCheckpointWait.Service) BindServiceCheckpoint();
            CaptureCheckpointCaches(checkpoint);
            checkpointResuming = false;
            presentationEpoch++;
            activePresentation = processing = false;
            activeRequest = default;
            ClearPresentationCaches();
            pendingEvents.Add(root.eventId);
            if (wait == NarrativeCheckpointWait.Service) ReleaseServiceHook(root.eventId);
            if (!pumping && !settling) ProcessQueue();
        }

        private void PreserveCheckpointSegment()
        {
            if (!IsCheckpointContinuation(activeRequest)) return;
            checkpoint.root.selectedChoices.Clear(); checkpoint.root.selectedChoices.AddRange(selectedChoices);
            CaptureCheckpointCaches(checkpoint);
        }

        private void CaptureCheckpointCaches(NarrativeCheckpointSave target)
        {
            target.abilityCheck = CloneAbilityCheck(activeAbilityCheck); target.abilityCheckId = activeAbilityCheckId;
            target.inlineChecks ??= new List<NarrativeAbilityCheckSnapshot>();
            target.attributeChoices ??= new List<NarrativeBoolValue>();
            target.attributeConditions ??= new List<NarrativeCounterValue>();
            target.inlineChecks.Clear(); target.attributeChoices.Clear(); target.attributeConditions.Clear();
            foreach (var pair in inlineAbilityChecks) target.inlineChecks.Add(CloneAbilityCheck(pair.Value));
            foreach (var pair in attributeChoiceResults) target.attributeChoices.Add(new NarrativeBoolValue { key = pair.Key, value = pair.Value });
            foreach (var pair in attributeConditionValues)
                target.attributeConditions.Add(new NarrativeCounterValue { key = !string.IsNullOrEmpty(pair.Key.conditionId)
                    ? "id:" + pair.Key.conditionId : "row:" + configuration.conditions.IndexOf(pair.Key).ToString(System.Globalization.CultureInfo.InvariantCulture), value = pair.Value });
        }

        private void RestoreCheckpointCaches()
        {
            ClearPresentationCaches();
            activeAbilityCheck = CloneAbilityCheck(checkpoint.abilityCheck); activeAbilityCheckId = checkpoint.abilityCheckId;
            if (checkpoint.inlineChecks != null) foreach (var item in checkpoint.inlineChecks)
                if (item != null && !string.IsNullOrEmpty(item.checkId)) inlineAbilityChecks[item.checkId] = CloneAbilityCheck(item);
            if (checkpoint.attributeChoices != null) foreach (var item in checkpoint.attributeChoices)
                if (item != null && !string.IsNullOrEmpty(item.key)) attributeChoiceResults[item.key] = item.value;
            if (checkpoint.attributeConditions != null) foreach (var item in checkpoint.attributeConditions)
            {
                if (item == null || string.IsNullOrEmpty(item.key)) continue;
                var condition = item.key.StartsWith("id:", StringComparison.Ordinal)
                    ? configuration.conditions.Find(c => c.conditionId == item.key.Substring(3)) : null;
                string row = item.key.StartsWith("row:", StringComparison.Ordinal) ? item.key.Substring(4) : item.key;
                if (condition == null && !item.key.StartsWith("id:", StringComparison.Ordinal)
                    && int.TryParse(row, out int index) && index >= 0 && index < configuration.conditions.Count)
                    condition = configuration.conditions[index];
                if (condition != null) attributeConditionValues[condition] = item.value;
            }
        }

        private void ClearPresentationCaches()
        {
            selectedChoices.Clear(); activeAbilityCheck = null; activeAbilityCheckId = null;
            inlineAbilityChecks.Clear(); attributeChoiceResults.Clear(); attributeConditionValues.Clear(); resumeActiveAbilityCheck = false;
        }

        private void RetainCheckpointPending()
        {
            if (checkpoint != null) pendingEvents.Add(checkpoint.root.eventId);
        }

        private NarrativeCheckpointSave CaptureCheckpoint()
        {
            var saved = CloneCheckpoint(checkpoint);
            if (saved != null && activePresentation && IsCheckpointContinuation(activeRequest))
            {
                saved.root.selectedChoices.Clear(); saved.root.selectedChoices.AddRange(selectedChoices);
                CaptureCheckpointCaches(saved);
            }
            return saved;
        }

        private static NarrativeCheckpointSave CloneCheckpoint(NarrativeCheckpointSave source)
        {
            if (source == null) return null;
            var copy = JsonUtility.FromJson<NarrativeCheckpointSave>(JsonUtility.ToJson(source));
            copy.serviceContext = copy.serviceContext?.Copy();
            copy.serviceReceipt = copy.serviceReceipt?.Copy();
            if (copy.root != null) copy.root.cafeVisit = copy.root.cafeVisit?.Copy();
            return copy;
        }

        private void RestoreCheckpoint(NarrativeCheckpointSave saved)
        {
            checkpoint = CloneCheckpoint(saved); checkpointResuming = false;
            if (checkpoint == null) return;
            if (checkpoint.root == null || !events.ContainsKey(checkpoint.root.eventId ?? string.Empty)
                || string.IsNullOrEmpty(checkpoint.root.requestId) || string.IsNullOrWhiteSpace(checkpoint.resumePageId)
                || checkpoint.root.depth < 0 || checkpoint.root.depth > 3
                || checkpoint.day < 1 || !Enum.IsDefined(typeof(NarrativeCheckpointWait), checkpoint.wait))
            { checkpoint = null; return; }
            checkpoint.stage = Math.Max(1, checkpoint.stage);
            checkpoint.root.selectedChoices ??= new List<string>();
            checkpoint.root.resumePageId = checkpoint.resumePageId;
            if (checkpoint.wait == NarrativeCheckpointWait.Service)
            {
                if (!ValidServiceContext(checkpoint.serviceContext)) { checkpoint = null; return; }
                BindServiceCheckpoint();
            }
            RetainCheckpointPending();
        }
    }
}
