using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeCheckpointTests
    {
        private sealed class Presenter : INarrativePresenter, INarrativePresentationCheckpoints, INarrativePresentationCancellation
        {
            public event Action<NarrativeEventRequest, string, NarrativeCheckpointWait> CheckpointReached;
            public event Action<string> Cancelled;
            public readonly List<NarrativeEventRequest> Requests = new List<NarrativeEventRequest>();
            public readonly List<Action> Completions = new List<Action>();
            public NarrativeEventRequest Current => Requests.Last();
            public void Play(NarrativeEventRequest request, Action completed) { Requests.Add(request); Completions.Add(completed); }
            public void Wait(string page, NarrativeCheckpointWait wait) => Emit(Current, page, wait);
            public void Emit(NarrativeEventRequest request, string page, NarrativeCheckpointWait wait) => CheckpointReached?.Invoke(request, page, wait);
            public void Complete() => Completions.Last()();
            public void Cancel() => Cancelled?.Invoke("host_unavailable");
        }

        private NarrativeConfiguration config;
        private NarrativeEventDefinition root;
        [SetUp] public void SetUp()
        {
            config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 999, initValue = 1, advanceMode = NarrativeAdvanceMode.Monotonic });
            config.bars.Add(new NarrativeBarDefinition { barKey = "reward", min = 0, max = 100 });
            root = new NarrativeEventDefinition { eventId = "chapter", onceOnly = true, entry = NarrativeEntryPoint.MorningBeforeMap, blocksSave = true };
            config.events.Add(root);
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "chapter", day = 2, timeSlot = NarrativeTimeSlot.Morning, repeatRule = NarrativeRepeatRule.Once });
            config.results.Add(new NarrativeResultDefinition { eventId = "chapter", resultType = NarrativeResultType.BarAdd, target = "reward", value = "1" });
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "morning", groupKey = "morning" });
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "afternoon", groupKey = "afternoon" });
        }
        private NarrativeController Start(Presenter p, Action<bool> done = null)
        {
            var controller = new NarrativeController(config, p);
            controller.SetOfficialDay(2); controller.ProcessOfficialDay(2);
            Assert.That(controller.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap, done));
            return controller;
        }
        private static NarrativeSnapshot RoundTrip(NarrativeSnapshot snapshot) => JsonUtility.FromJson<NarrativeSnapshot>(JsonUtility.ToJson(snapshot));

        [Test] public void BusinessAndNightKeepIdentityAndCommitOnlyAtFinalCompletion()
        {
            var p = new Presenter(); var callbacks = new List<bool>(); var c = Start(p, callbacks.Add);
            string requestId = p.Current.RequestId;
            var morningRequest = p.Current;
            int completions = 0; c.EventCompleted += _ => completions++;
            Assert.That(c.BlocksSave); Assert.That(c.ReadPresentationCondition("checkpointStage:0"));
            Assert.That(c.SelectChoice("morning")); var staleMorning = p.Completions.Last();
            p.Wait("tour", NarrativeCheckpointWait.Business);
            Assert.That(callbacks, Is.EqualTo(new[] { true }));
            Assert.That(c.IsBusy, Is.False); Assert.That(c.IsSafeBoundary); Assert.That(c.BlocksSave, Is.False);
            Assert.That(c.State.HasHappenedEvent("chapter"), Is.False); Assert.That(c.CaptureSnapshot().active, Is.Null);
            Assert.That(c.ResumeCheckpoint(NarrativeEntryPoint.AfterBusiness), Is.False);
            Assert.That(c.TryBeginCheckpointBusiness(1, out _), Is.False);
            Assert.That(c.TryBeginCheckpointBusiness(2, out var token));
            Assert.That(c.TryBeginCheckpointBusiness(2, out _), Is.False);
            Assert.That(c.ConfirmCheckpointBusiness(1, token), Is.False);
            Assert.That(c.ConfirmCheckpointBusiness(2, "stale"), Is.False);
            Assert.That(c.ConfirmCheckpointBusiness(2, token));
            Assert.That(c.HasPendingBusinessCheckpoint); Assert.That(c.ConfirmCheckpointBusiness(2, token), Is.False);
            Assert.That(c.TryBeginCheckpointBusiness(2, out _), Is.False);
            Assert.That(c.ResumeCheckpoint(NarrativeEntryPoint.BeforeNight), Is.False);
            Assert.That(c.ProcessEntry(NarrativeEntryPoint.AfterBusiness, callbacks.Add));
            Assert.That(p.Current.RequestId, Is.EqualTo(requestId)); Assert.That(p.Current.ResumePageId, Is.EqualTo("tour"));
            Assert.That(c.ReadPresentationCondition("checkpointStage:1")); Assert.That(c.BlocksSave);
            p.Emit(morningRequest, "stale-night", NarrativeCheckpointWait.Night);
            Assert.That(c.IsBusy); Assert.That(c.CurrentCheckpointStage, Is.EqualTo(1));
            staleMorning(); Assert.That(completions, Is.Zero);
            Assert.That(c.SelectChoice("afternoon")); p.Wait("night", NarrativeCheckpointWait.Night);
            Assert.That(callbacks, Is.EqualTo(new[] { true, true })); Assert.That(c.HasPendingNightCheckpoint);
            Assert.That(c.ResumeCheckpoint(NarrativeEntryPoint.AfterBusiness), Is.False);
            Assert.That(c.ResumeCheckpoint(NarrativeEntryPoint.BeforeNight, callbacks.Add));
            Assert.That(p.Current.RequestId, Is.EqualTo(requestId)); Assert.That(p.Current.ResumePageId, Is.EqualTo("night"));
            Assert.That(c.ReadPresentationCondition("checkpointStage:2"));
            p.Complete(); p.Complete(); staleMorning();
            Assert.That(completions, Is.EqualTo(1)); Assert.That(c.State.HasHappenedEvent("chapter"));
            Assert.That(c.State.IsFlagSet("event.once.chapter"));
            Assert.That(c.CaptureSnapshot().checkpoint, Is.Null); Assert.That(c.BlocksSave, Is.False);
            Assert.That(callbacks, Is.EqualTo(new[] { true, true, true }));
            Assert.That(c.State.TryGetBar("reward", out int reward) && reward == 1);
        }

        [Test] public void RestoreAndCancellationRetainChoicesChecksAndConfirmedBusiness()
        {
            config.abilityChecks.Add(new NarrativeAbilityCheckDefinition { checkId = "inline", mode = NarrativeAbilityCheckMode.Inline });
            config.abilityCheckConditions.Add(new NarrativeAbilityCheckConditionDefinition { checkId = "inline", attributeId = "courage", threshold = 3, groupIndex = 1 });
            config.abilityChecks.Add(new NarrativeAbilityCheckDefinition { checkId = "afternoon-check", mode = NarrativeAbilityCheckMode.Inline });
            config.abilityCheckConditions.Add(new NarrativeAbilityCheckConditionDefinition { checkId = "afternoon-check", attributeId = "courage", threshold = 3, groupIndex = 1 });
            config.abilityNameKeys.Add("courage", "courage");
            config.conditions.Add(new NarrativeConditionDefinition { ownerType = "choice", ownerId = "morning", condType = "ATTRIBUTE_GTE", target = "courage", value = "3" });
            var p = new Presenter(); var c = Start(p); int reads = 0;
            c.AbilityReader = _ => { reads++; return (true, 3); };
            Assert.That(c.EvaluateInlineAbilityCheckForPresentation("inline").passed);
            Assert.That(c.SelectChoice("morning"));
            p.Wait("tour", NarrativeCheckpointWait.Business);
            Assert.That(c.TryBeginCheckpointBusiness(2, out var token));
            var issued = RoundTrip(c.CaptureSnapshot());
            var p2 = new Presenter(); var restored = new NarrativeController(config, p2); restored.RestoreSnapshot(issued); restored.SetOfficialDay(2);
            restored.AbilityReader = _ => { reads++; return (true, 0); };
            Assert.That(restored.ConfirmCheckpointBusiness(2, token));
            var saved = RoundTrip(restored.CaptureSnapshot()); restored.RestoreSnapshot(saved); restored.SetOfficialDay(2);
            var callbacks = new List<bool>(); Assert.That(restored.ProcessEntry(NarrativeEntryPoint.AfterBusiness, callbacks.Add));
            Assert.That(restored.SelectedChoices, Is.EqualTo(new[] { "morning" }));
            Assert.That(restored.EvaluateConditions("choice", "morning"));
            Assert.That(restored.EvaluateInlineAbilityCheckForPresentation("inline").passed); Assert.That(reads, Is.EqualTo(2));
            Assert.That(restored.SelectChoice("afternoon"));
            restored.AbilityReader = _ => { reads++; return (true, 3); };
            Assert.That(restored.EvaluateInlineAbilityCheckForPresentation("afternoon-check").passed);
            restored.AbilityReader = _ => { reads++; return (true, 0); };
            var stale = p2.Completions.Last();
            p2.Cancel();
            Assert.That(callbacks, Is.EqualTo(new[] { false })); Assert.That(restored.IsBusy, Is.False);
            Assert.That(restored.TryBeginCheckpointBusiness(2, out _), Is.False);
            Assert.That(restored.ProcessEntry(NarrativeEntryPoint.AfterBusiness, callbacks.Add));
            Assert.That(restored.SelectedChoices, Is.EqualTo(new[] { "morning", "afternoon" })); Assert.That(restored.ReadPresentationCondition("checkpointStage:1"));
            Assert.That(restored.SelectChoice("afternoon"));
            Assert.That(restored.EvaluateInlineAbilityCheckForPresentation("inline").passed);
            Assert.That(restored.EvaluateInlineAbilityCheckForPresentation("afternoon-check").passed); Assert.That(reads, Is.EqualTo(3));
            stale(); Assert.That(restored.State.HasHappenedEvent("chapter"), Is.False);
            p2.Complete(); Assert.That(callbacks, Is.EqualTo(new[] { false, true }));
            Assert.That(restored.State.HasHappenedEvent("chapter"));
        }

        [Test] public void CancelledAttemptCannotReachCheckpointOnRetryOfSameSegment()
        {
            var p = new Presenter(); var c = Start(p);
            p.Wait("tour", NarrativeCheckpointWait.Business);
            Assert.That(c.TryBeginCheckpointBusiness(2, out var token)); Assert.That(c.ConfirmCheckpointBusiness(2, token));
            Assert.That(c.BusinessCheckpointConfirmed);
            Assert.That(c.ResumeCheckpoint(NarrativeEntryPoint.AfterBusiness));
            var stale = p.Current;
            p.Cancel(); Assert.That(c.BusinessCheckpointConfirmed);
            Assert.That(c.ResumeCheckpoint(NarrativeEntryPoint.AfterBusiness));
            Assert.That(p.Current.RequestId, Is.EqualTo(stale.RequestId));
            Assert.That(p.Current.ResumePageId, Is.EqualTo(stale.ResumePageId));
            Assert.That(p.Current.PresentationToken, Is.Not.EqualTo(stale.PresentationToken));
            Assert.That(p.Current.PresentationToken, Is.GreaterThan(0));
            p.Emit(stale, "night", NarrativeCheckpointWait.Night);
            Assert.That(c.IsBusy); Assert.That(c.CurrentCheckpointStage, Is.EqualTo(1));
            Assert.That(c.HasPendingNightCheckpoint, Is.False);
            p.Wait("night", NarrativeCheckpointWait.Night);
            Assert.That(c.IsBusy, Is.False); Assert.That(c.HasPendingNightCheckpoint);
            Assert.That(c.ResumeCheckpoint(NarrativeEntryPoint.BeforeNight)); p.Complete();
            Assert.That(c.State.HasHappenedEvent("chapter"));
        }

        [Test] public void ActiveSegmentSnapshotIncludesNewChoicesAndChecksWithoutMutatingEarlierSnapshot()
        {
            root.blocksSave = false;
            config.abilityChecks.Add(new NarrativeAbilityCheckDefinition { checkId = "active-check", mode = NarrativeAbilityCheckMode.Inline });
            config.abilityCheckConditions.Add(new NarrativeAbilityCheckConditionDefinition { checkId = "active-check", attributeId = "courage", threshold = 3, groupIndex = 1 });
            config.abilityNameKeys.Add("courage", "courage");
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "afternoon.attribute", ownerType = "choice", ownerId = "afternoon", condType = "ATTRIBUTE_GTE", target = "courage", value = "3" });
            var p = new Presenter(); var c = Start(p);
            Assert.That(c.SelectChoice("morning")); p.Wait("tour", NarrativeCheckpointWait.Business);
            Assert.That(c.TryBeginCheckpointBusiness(2, out var token)); Assert.That(c.ConfirmCheckpointBusiness(2, token));
            var before = RoundTrip(c.CaptureSnapshot());
            Assert.That(c.ResumeCheckpoint(NarrativeEntryPoint.AfterBusiness));
            c.AbilityReader = _ => (true, 3);
            Assert.That(c.SelectChoice("afternoon")); Assert.That(c.EvaluateInlineAbilityCheckForPresentation("active-check").passed);
            Assert.That(c.BlocksSave, Is.False);
            var active = RoundTrip(c.CaptureSnapshot());
            Assert.That(active.checkpoint.root.selectedChoices, Is.EqualTo(new[] { "morning", "afternoon" }));
            Assert.That(active.checkpoint.inlineChecks.Single().checkId, Is.EqualTo("active-check"));
            Assert.That(before.checkpoint.root.selectedChoices, Is.EqualTo(new[] { "morning" }));
            Assert.That(before.checkpoint.inlineChecks, Is.Empty);
            var p2 = new Presenter(); var restored = new NarrativeController(config, p2);
            restored.RestoreSnapshot(active); restored.SetOfficialDay(2);
            int newReads = 0; restored.AbilityReader = _ => { newReads++; return (true, 0); };
            Assert.That(restored.BusinessCheckpointConfirmed); Assert.That(restored.TryBeginCheckpointBusiness(2, out _), Is.False);
            Assert.That(restored.ResumeCheckpoint(NarrativeEntryPoint.AfterBusiness));
            Assert.That(restored.SelectedChoices, Is.EqualTo(new[] { "morning", "afternoon" }));
            Assert.That(restored.EvaluateConditions("choice", "afternoon"));
            Assert.That(restored.EvaluateInlineAbilityCheckForPresentation("active-check").passed); Assert.That(newReads, Is.Zero);
            p2.Complete(); Assert.That(restored.State.HasHappenedEvent("chapter"));
        }

        [Test] public void RetainedRootCannotRetriggerAndResumesBeforeNormalPhaseCandidates()
        {
            root.entry = NarrativeEntryPoint.Immediate; config.eventDates.Clear();
            config.events.Add(new NarrativeEventDefinition { eventId = "other", entry = NarrativeEntryPoint.AfterBusiness });
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "other", day = 2, timeSlot = NarrativeTimeSlot.Noon });
            config.events.Add(new NarrativeEventDefinition { eventId = "action" });
            var p = new Presenter(); var c = new NarrativeController(config, p); c.SetOfficialDay(2);
            Assert.That(c.QueueEvent("chapter")); p.Wait("tour", NarrativeCheckpointWait.Business);
            c.BeginTurn(); c.EndTurn(); Assert.That(c.QueueEvent("chapter"), Is.False);
            Assert.That(c.QueueEvent("action")); p.Complete();
            Assert.That(c.TryBeginCheckpointBusiness(2, out var token)); Assert.That(c.ConfirmCheckpointBusiness(2, token));
            Assert.That(c.ProcessEntry(NarrativeEntryPoint.AfterBusiness));
            Assert.That(p.Current.Event.eventId, Is.EqualTo("chapter"));
            Assert.That(c.QueueEvent("action"), Is.False);
            p.Complete(); Assert.That(p.Current.Event.eventId, Is.EqualTo("other")); p.Complete();
        }

        [Test] public void WrongRequestDayAndFreshBlockersCannotResumeOrSuspendRoot()
        {
            var p = new Presenter(); var c = Start(p);
            p.Emit(new NarrativeEventRequest(root, null, 0), "tour", NarrativeCheckpointWait.Business);
            Assert.That(c.IsBusy); Assert.That(c.HasPendingBusinessCheckpoint, Is.False);
            p.Wait("tour", NarrativeCheckpointWait.Business);
            Assert.That(c.TryBeginCheckpointBusiness(2, out var token)); Assert.That(c.ConfirmCheckpointBusiness(2, token));
            c.State.SetEventLocked("chapter", true); Assert.That(c.ProcessEntry(NarrativeEntryPoint.AfterBusiness), Is.False);
            c.State.SetEventLocked("chapter", false);
            root.eventProperties.Add(new NarrativeEventProperty { property = "blockedBy", value = "construction" });
            Assert.That(c.ResumeCheckpoint(NarrativeEntryPoint.AfterBusiness), Is.False); root.eventProperties.Clear();
            c.SetOfficialDay(3); Assert.That(c.ResumeCheckpoint(NarrativeEntryPoint.AfterBusiness), Is.False);
            Assert.That(c.TryBeginCheckpointBusiness(3, out _), Is.False);
            Assert.That(c.State.HasHappenedEvent("chapter"), Is.False);
        }
    }
}
