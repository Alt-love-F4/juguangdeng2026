using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeCafeVisitTests
    {
        private sealed class Presenter : INarrativePresenter
        {
            public readonly List<string> played = new List<string>();
            public Action complete;
            public void Play(NarrativeEventRequest request, Action completed) { played.Add(request.Event.eventId); complete = completed; }
            public void Finish() { var callback = complete; complete = null; callback(); }
        }
        private sealed class CancellablePresenter : INarrativePresenter, INarrativePresentationCancellation, INarrativePresentationDrain
        {
            public event Action<string> Cancelled;
            public Action complete, drain;
            public readonly List<string> played = new List<string>();
            public void Play(NarrativeEventRequest request, Action completed) { played.Add(request.Event.eventId); complete = completed; }
            public void Drain(Action completed) { drain = completed; }
            public void Cancel(string reason) => Cancelled?.Invoke(reason);
        }
        private NarrativeConfiguration config;
        [SetUp] public void Setup()
        {
            config = new NarrativeConfiguration();
            config.objects.Add(new NarrativeObjectDefinition { objId = "npc", objType = NarrativeObjectType.Npc });
            config.cafeVisits.Add(new NarrativeCafeVisitDefinition { visitKey = "visit", characterId = "npc", preferredSeatId = "seat", arrivalDelaySeconds = 20, leaveAfterServed = false });
            AddEvent("reserve", NarrativeResultType.ReserveCafeVisit, "visit:visit");
            AddEvent("spawn", NarrativeResultType.SpawnNpc, "scene:cafe");
            AddEvent("move", NarrativeResultType.SpawnNpc, "scene:park");
            AddEvent("despawn", NarrativeResultType.DespawnNpc, "");
        }
        private void AddEvent(string id, NarrativeResultType type, string value)
        {
            config.events.Add(new NarrativeEventDefinition { eventId = id });
            config.results.Add(new NarrativeResultDefinition { eventId = id, resultType = type, target = "obj:npc", value = value });
        }
        private void Hook(string id, string stage, string trigger = "auto", int seq = 0)
        {
            config.events.Add(new NarrativeEventDefinition { eventId = id });
            config.cafeVisitHooks.Add(new NarrativeCafeVisitHookDefinition { visitKey = "visit", stage = stage, trigger = trigger, seq = seq, eventId = id });
        }
        private static NarrativeCafeVisitFeedback Fact(NarrativeController c, string stage, long seq = 1, bool click = false) => new NarrativeCafeVisitFeedback
        { visitInstanceId = c.GetCommittedCafeVisits().Visits[0].VisitInstanceId, characterId = "npc", stage = stage, sequence = seq, isClick = click, reason = "business_closed" };

        [Test] public void ReserveOnlyAndSpawnAuthorizationAreSeparateImmutableCommits()
        {
            var c = new NarrativeController(config);
            c.QueueEvent("reserve");
            var reserved = c.GetCommittedCafeVisits();
            Assert.That(reserved.Visits[0].ArrivalAuthorized, Is.False);
            Assert.That(c.State.GetNpcLocation("npc"), Is.Empty);
            c.QueueEvent("spawn"); c.QueueEvent("spawn");
            var spawned = c.GetCommittedCafeVisits();
            Assert.That(spawned.Visits.Count, Is.EqualTo(1));
            Assert.That(spawned.Visits[0].VisitInstanceId, Is.EqualTo(reserved.Visits[0].VisitInstanceId));
            Assert.That(spawned.Visits[0].ArrivalDelaySeconds, Is.EqualTo(20));
            Assert.That(spawned.Visits[0].ArrivalAuthorized, Is.True);
            Assert.That(reserved.Visits[0].ArrivalAuthorized, Is.False);
        }

        [Test] public void BatchPublishesOnlyFinalStateAndOldTerminalFactCannotClearNewOwner()
        {
            config.results.Add(new NarrativeResultDefinition { eventId = "reserve", seq = 1, resultType = NarrativeResultType.SpawnNpc, target = "obj:npc", value = "scene:cafe" });
            var c = new NarrativeController(config);
            int notifications = 0; c.CafeVisitsCommitted += _ => notifications++;
            c.QueueEvent("reserve");
            Assert.That(notifications, Is.EqualTo(1));
            var old = Fact(c, "ended");
            c.QueueEvent("move");
            Assert.That(c.GetCommittedCafeVisits().Visits[0].EndReason, Is.EqualTo("location_changed"));
            c.QueueEvent("spawn");
            c.SubmitCafeVisitFeedback(old);
            Assert.That(c.State.GetNpcLocation("npc"), Is.EqualTo("cafe"));
            Assert.That(c.GetCommittedCafeVisits().Visits[1].IsEnded, Is.False);
        }

        [Test] public void FactsWaitDuringPresentationAndHooksRunSequentiallyOnce()
        {
            Hook("first", "served", seq: 1); Hook("second", "served", seq: 2);
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.QueueEvent("spawn"); p.Finish();
            var fact = Fact(c, "served"); int completions = 0;
            Assert.That(c.SubmitCafeVisitFeedback(fact, () => completions++));
            Assert.That(p.played, Is.EqualTo(new[] { "spawn", "first" }));
            c.SubmitCafeVisitFeedback(fact);
            Assert.That(completions, Is.Zero);
            p.Finish(); Assert.That(p.played[2], Is.EqualTo("second"));
            p.Finish();
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(c.IsCafeVisitFeedbackComplete(fact));
            c.SubmitCafeVisitFeedback(fact);
            Assert.That(p.played.Count, Is.EqualTo(3));
        }

        [Test] public void ClickSequencesAreIndependentAndUseOnceOnlyAndLocks()
        {
            Hook("repeat", "before_served", "click");
            Hook("once", "before_served", "click", 1);
            Hook("locked", "before_served", "click", 2);
            config.events.Find(e => e.eventId == "once").onceOnly = true;
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.State.SetEventLocked("locked", true);
            c.QueueEvent("spawn"); p.Finish(); c.SubmitCafeVisitFeedback(Fact(c, "arrived"));
            var first = Fact(c, "before_served", 10, true);
            c.SubmitCafeVisitFeedback(first); p.Finish(); p.Finish();
            c.SubmitCafeVisitFeedback(first);
            c.SubmitCafeVisitFeedback(Fact(c, "before_served", 11, true)); p.Finish();
            Assert.That(p.played, Is.EqualTo(new[] { "spawn", "repeat", "once", "repeat" }));
        }

        [Test] public void PendingFeedbackAndHookCursorSurviveJsonRestore()
        {
            Hook("first", "served", seq: 1); Hook("second", "served", seq: 2);
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.QueueEvent("spawn"); p.Finish();
            var fact = Fact(c, "served"); c.SubmitCafeVisitFeedback(fact);
            var saved = JsonUtility.FromJson<NarrativeSnapshot>(JsonUtility.ToJson(c.CaptureSnapshot()));
            var restoredPresenter = new Presenter(); var restored = new NarrativeController(config, restoredPresenter);
            restored.BeginValueRestore(); restored.RestoreSnapshot(saved);
            Assert.That(restored.GetCommittedCafeVisits().IsReady, Is.False);
            restored.CompleteValueRestore(false); restored.ResumePendingEvents();
            restoredPresenter.Finish(); restoredPresenter.Finish();
            Assert.That(restored.IsCafeVisitFeedbackComplete(fact));
            Assert.That(restoredPresenter.played, Is.EqualTo(new[] { "first", "second" }));
            Assert.That(restored.GetCommittedCafeVisits().IsReady);
            restored.SubmitCafeVisitFeedback(fact);
            Assert.That(restoredPresenter.played.Count, Is.EqualTo(2));
        }

        [Test] public void CancelledHookDoesNotCommitAndAdvancesToNextHook()
        {
            Hook("first", "served", seq: 1); Hook("second", "served", seq: 2);
            config.results.Add(new NarrativeResultDefinition { eventId = "first", resultType = NarrativeResultType.DespawnNpc, target = "obj:npc" });
            var p = new CancellablePresenter(); var c = new NarrativeController(config, p);
            c.QueueEvent("spawn"); p.complete();
            var fact = Fact(c, "served"); int completions = 0;
            c.SubmitCafeVisitFeedback(fact, () => completions++);
            var stale = p.complete;
            int failures = 0;
            c.EventFailed += (_, __) => { failures++; c.CheckEndingsAtSafeBoundary(); };
            p.Cancel("playback_error");
            Assert.That(p.played, Is.EqualTo(new[] { "spawn", "first", "second" }));
            stale();
            Assert.That(c.State.HasHappenedEvent("first"), Is.False);
            Assert.That(c.State.GetNpcLocation("npc"), Is.EqualTo("cafe"));
            Assert.That(failures, Is.EqualTo(1));
            p.complete(); Assert.That(c.IsCafeVisitFeedbackComplete(fact));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(c.IsBusy, Is.False);
            Assert.That(c.State.HasHappenedEvent("second"));
            Assert.That(p.played, Is.EqualTo(new[] { "spawn", "first", "second" }));
        }

        [Test] public void DrainCancellationClearsPendingEffectsAndInvalidatesOldCallback()
        {
            config.results.Add(new NarrativeResultDefinition { eventId = "spawn", seq = 1, resultType = NarrativeResultType.PlaySetup, target = "setup:extra" });
            var p = new CancellablePresenter(); var c = new NarrativeController(config, p);
            c.QueueEvent("spawn"); p.complete(); var oldDrain = p.drain;
            Assert.That(c.IsBusy);
            p.Cancel("drain_error");
            Assert.That(c.IsBusy, Is.False);
            Assert.That(c.CaptureSnapshot().pendingEffects, Is.Empty);
            oldDrain(); Assert.That(c.IsBusy, Is.False);
            Assert.That(c.State.HasHappenedEvent("spawn")); // The event committed before its extra setup began.
        }

        [Test] public void HostCancellationFinishesFeedbackWithoutPumpingLostQueueAndAllowsExplicitRetry()
        {
            Hook("first", "served", seq: 1); Hook("second", "served", seq: 2);
            var p = new CancellablePresenter(); var c = new NarrativeController(config, p);
            c.QueueEvent("spawn"); p.complete();
            var fact = Fact(c, "served"); int completions = 0;
            c.SubmitCafeVisitFeedback(fact, () => completions++);
            var stale = p.complete;
            c.EventFailed += (_, __) => c.CheckEndingsAtSafeBoundary();
            p.Cancel("host_unavailable");
            Assert.That(c.IsBusy, Is.False);
            Assert.That(c.IsCafeVisitFeedbackComplete(fact));
            Assert.That(p.played, Is.EqualTo(new[] { "spawn", "first" }), "Lost-host cancellation must not automatically start the second hook.");
            Assert.That(c.State.HasHappenedEvent("first"), Is.False);
            Assert.That(c.State.HasHappenedEvent("second"), Is.False);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(c.CaptureSnapshot().cafeFeedback, Is.Empty);
            stale(); Assert.That(c.IsBusy, Is.False);
            Assert.That(c.QueueEvent("move"));
            Assert.That(p.played, Is.EqualTo(new[] { "spawn", "first", "move" }));
            stale(); Assert.That(c.State.HasHappenedEvent("move"), Is.False);
            p.complete();
            Assert.That(c.State.HasHappenedEvent("move"));
            p.Cancel("disposed");
            Assert.That(c.QueueEvent("move"), Is.False);
        }

        [Test] public void ReservedHookRunsOnceAfterSpawnWithoutRegressingCurrentStage()
        {
            Hook("reserved_hook", "reserved");
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.QueueEvent("reserve"); p.Finish(); c.QueueEvent("spawn"); p.Finish();
            var fact = Fact(c, "reserved");
            c.SubmitCafeVisitFeedback(fact);
            Assert.That(p.played, Is.EqualTo(new[] { "reserve", "spawn", "reserved_hook" }));
            Assert.That(c.GetCommittedCafeVisits().Visits[0].Stage, Is.EqualTo("waiting"));
            p.Finish(); c.SubmitCafeVisitFeedback(fact);
            Assert.That(p.played.Count, Is.EqualTo(3));
            Assert.That(c.IsCafeVisitFeedbackComplete(fact));
        }

        [TestCase(false, "reservation_cancelled")]
        [TestCase(true, "ncs_despawn")]
        public void DespawnReasonDistinguishesReservationFromAuthorizedArrival(bool authorize, string reason)
        {
            var c = new NarrativeController(config);
            c.QueueEvent("reserve");
            if (authorize) c.QueueEvent("spawn");
            c.QueueEvent("despawn");
            Assert.That(c.GetCommittedCafeVisits().Visits[0].IsEnded);
            Assert.That(c.GetCommittedCafeVisits().Visits[0].EndReason, Is.EqualTo(reason));
        }

        [Test] public void EndedPersistsAndRejectsStaleClicksBeforeEndedHook()
        {
            Hook("ended", "ended"); Hook("click", "before_served", "click");
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.QueueEvent("spawn"); p.Finish();
            var ended = Fact(c, "ended"); c.SubmitCafeVisitFeedback(ended);
            Assert.That(c.GetCommittedCafeVisits().Visits[0].IsEnded);
            Assert.That(c.State.GetNpcLocation("npc"), Is.Empty);
            var click = Fact(c, "before_served", 99, true); c.SubmitCafeVisitFeedback(click);
            var saved = c.CaptureSnapshot();
            Assert.That(saved.cafeFeedback.Count, Is.EqualTo(2));
            p.Finish();
            Assert.That(c.IsCafeVisitFeedbackComplete(click));
            Assert.That(p.played, Is.EqualTo(new[] { "spawn", "ended" }));
        }
    }
}
