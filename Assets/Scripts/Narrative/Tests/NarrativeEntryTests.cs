using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeEntryTests
    {
        private NarrativeConfiguration config;
        private sealed class Presenter : INarrativePresenter, INarrativePresentationCancellation
        {
            public event Action<string> Cancelled;
            public readonly List<string> Played = new List<string>();
            public readonly List<Action> Callbacks = new List<Action>();
            public void Play(NarrativeEventRequest request, Action completed) { Played.Add(request.Event.eventId); Callbacks.Add(completed); }
            public void Complete() => Callbacks.Last()();
            public void Cancel(string reason = "user_cancelled") => Cancelled?.Invoke(reason);
        }
        [SetUp] public void Setup()
        {
            config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "a", min = 0, max = 20, advanceMode = NarrativeAdvanceMode.EventDriven });
            config.bars.Add(new NarrativeBarDefinition { barKey = "b", min = 0, max = 20, advanceMode = NarrativeAdvanceMode.EventDriven });
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999, initValue = 1, advanceMode = NarrativeAdvanceMode.Monotonic });
        }
        private NarrativeEventDefinition Event(string id, NarrativeEntryPoint entry, string bar = null, NarrativeCrossType cross = NarrativeCrossType.Cross, int priority = 0)
        {
            var definition = new NarrativeEventDefinition { eventId = id, entry = entry, onceOnly = true };
            config.events.Add(definition);
            if (bar != null) config.triggers.Add(new NarrativeTriggerDefinition { triggerId = id + ".trigger", eventId = id, barKey = bar, threshold = 2, priority = priority,
                crossType = cross, direction = NarrativeTriggerDirection.Up, onceType = NarrativeOnceType.Once, resetOnExit = true });
            return definition;
        }
        private NarrativeController Formal(Presenter presenter)
        {
            var c = new NarrativeController(config, presenter); c.SetOfficialDay(2); c.ProcessOfficialDay(2); return c;
        }
        [Test] public void CrossWaitsWithoutBusySurvivesValueDropAndSnapshotWithOriginalCount()
        {
            Event("cross", NarrativeEntryPoint.BeforeNight, "a");
            var p = new Presenter(); var c = Formal(p);
            c.SetBar("a", 2); c.EndTurn(); c.SetBar("a", 0); c.EndTurn();
            Assert.That(p.Played, Is.Empty); Assert.That(c.IsBusy, Is.False); Assert.That(c.IsSafeBoundary);
            var saved = c.CaptureSnapshot();
            Assert.That(saved.candidates.Single().count, Is.EqualTo(1));
            Assert.That(saved.firedTriggers, Is.Empty);
            var p2 = new Presenter(); var restored = new NarrativeController(config, p2);
            restored.RestoreSnapshot(saved); restored.SetOfficialDay(2); restored.ResumePendingEvents();
            bool? done = null; restored.ProcessEntry(NarrativeEntryPoint.BeforeNight, success => done = success);
            Assert.That(p2.Played, Is.EqualTo(new[] { "cross" })); Assert.That(done, Is.Null);
            p2.Complete(); Assert.That(done, Is.True);
            Assert.That(restored.State.HasFiredTrigger("cross.trigger"));
            Assert.That(restored.WaitingCandidates, Is.Empty);
        }
        [Test] public void HoldRechecksAtEntranceAndCanQualifyAgainAfterDropping()
        {
            Event("hold", NarrativeEntryPoint.BeforeNight, "a", NarrativeCrossType.Hold);
            var p = new Presenter(); var c = Formal(p);
            c.SetBar("a", 2); c.EndTurn(); c.SetBar("a", 0); c.EndTurn();
            Assert.That(c.ProcessEntry(NarrativeEntryPoint.BeforeNight)); Assert.That(p.Played, Is.Empty);
            Assert.That(c.State.HasFiredTrigger("hold.trigger"), Is.False);
            c.SetBar("a", 2); c.EndTurn(); c.ProcessEntry(NarrativeEntryPoint.BeforeNight);
            Assert.That(p.Played, Is.EqualTo(new[] { "hold" })); p.Complete();
        }
        [Test] public void SameEntranceSortsPriorityThenConfigurationOrderWithoutPreempting()
        {
            Event("z", NarrativeEntryPoint.BeforeNight, "a", priority: 5);
            Event("a", NarrativeEntryPoint.BeforeNight, "b", priority: 5);
            Event("high", NarrativeEntryPoint.BeforeNight, "b", priority: 10);
            Event("now", NarrativeEntryPoint.Immediate);
            var p = new Presenter(); var c = Formal(p);
            c.SetBar("a", 2); c.SetBar("b", 2); c.EndTurn();
            Assert.That(c.QueueEvent("now")); Assert.That(c.ProcessEntry(NarrativeEntryPoint.BeforeNight), Is.False);
            Assert.That(p.Played, Is.EqualTo(new[] { "now" })); p.Complete();
            c.ProcessEntry(NarrativeEntryPoint.BeforeNight); p.Complete(); p.Complete(); p.Complete();
            Assert.That(p.Played, Is.EqualTo(new[] { "now", "high", "z", "a" }));
        }
        [Test] public void NightDateNeverCollectsInMorningOrRollsIntoAnotherDay()
        {
            Event("night", NarrativeEntryPoint.BeforeNight);
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "night", day = 2, timeSlot = NarrativeTimeSlot.Night, repeatRule = NarrativeRepeatRule.Once });
            var p = new Presenter(); var c = Formal(p);
            c.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap); c.ProcessEntry(NarrativeEntryPoint.AfterBusiness);
            Assert.That(p.Played, Is.Empty); Assert.That(c.WaitingCandidates, Is.Empty);
            Assert.That(c.QueueEvent("night"), Is.False); Assert.That(c.QueueMapActionEvent("night"), Is.False);
            c.SetOfficialDay(3); c.ProcessEntry(NarrativeEntryPoint.BeforeNight);
            Assert.That(p.Played, Is.Empty); Assert.That(c.State.HasHappenedEvent("night"), Is.False);
        }
        [Test] public void CancelledDateBatchRetriesAndStaleCallbackCannotComplete()
        {
            Event("night", NarrativeEntryPoint.BeforeNight);
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "night", day = 2, timeSlot = NarrativeTimeSlot.Night, repeatRule = NarrativeRepeatRule.Once });
            var p = new Presenter(); var c = Formal(p); var results = new List<bool>();
            c.ProcessEntry(NarrativeEntryPoint.BeforeNight, results.Add); var stale = p.Callbacks.Last(); p.Cancel();
            Assert.That(results, Is.EqualTo(new[] { false })); Assert.That(c.IsSafeBoundary);
            Assert.That(c.State.HasHappenedEvent("night"), Is.False); Assert.That(c.CaptureSnapshot().flags.Any(f => f.value && f.key.StartsWith("date.once.")), Is.False);
            c.ProcessEntry(NarrativeEntryPoint.BeforeNight, results.Add); stale();
            Assert.That(c.State.HasHappenedEvent("night"), Is.False); p.Complete();
            Assert.That(results, Is.EqualTo(new[] { false, true }));
        }
        [Test] public void HostUnavailableEntryCanRetryRepeatedlyAndRejectsBothOldCallbacks()
        {
            Event("night", NarrativeEntryPoint.BeforeNight).blocksSave = true;
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "night", day = 2, timeSlot = NarrativeTimeSlot.Night, repeatRule = NarrativeRepeatRule.Once });
            var p = new Presenter(); var c = Formal(p); var results = new List<bool>();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(c.ProcessEntry(NarrativeEntryPoint.BeforeNight, results.Add)); Assert.That(c.BlocksSave);
                p.Cancel("host_unavailable");
                Assert.That(c.IsSafeBoundary); Assert.That(c.IsBusy, Is.False); Assert.That(c.BlocksSave, Is.False);
                Assert.That(c.State.HasHappenedEvent("night"), Is.False);
            }
            Assert.That(results, Is.EqualTo(new[] { false, false }));
            Assert.That(c.ProcessEntry(NarrativeEntryPoint.BeforeNight, results.Add));
            p.Callbacks[0](); p.Callbacks[1]();
            Assert.That(c.BlocksSave); Assert.That(c.State.HasHappenedEvent("night"), Is.False);
            Assert.That(results.Count, Is.EqualTo(2)); p.Complete();
            Assert.That(results, Is.EqualTo(new[] { false, false, true }));
            Assert.That(c.State.HasHappenedEvent("night")); Assert.That(c.BlocksSave, Is.False);
        }
        [Test] public void DisposedAfterRecoverableHostLossRemainsTerminalAndRejectsLateCompletion()
        {
            Event("night", NarrativeEntryPoint.BeforeNight).blocksSave = true;
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "night", day = 2, timeSlot = NarrativeTimeSlot.Night, repeatRule = NarrativeRepeatRule.Once });
            var p = new Presenter(); var c = Formal(p); var results = new List<bool>();
            c.ProcessEntry(NarrativeEntryPoint.BeforeNight, results.Add); var stale = p.Callbacks.Last();
            p.Cancel("host_unavailable"); p.Cancel("disposed"); stale();
            Assert.That(c.ProcessEntry(NarrativeEntryPoint.BeforeNight, results.Add), Is.False);
            Assert.That(c.QueueEvent("night"), Is.False); Assert.That(p.Played.Count, Is.EqualTo(1));
            Assert.That(c.State.HasHappenedEvent("night"), Is.False); Assert.That(c.BlocksSave, Is.False);
            Assert.That(c.IsSafeBoundary); Assert.That(c.IsBusy, Is.False);
            Assert.That(results, Is.EqualTo(new[] { false }));
        }
        [Test] public void MapActionOnlyStartsOnClickAndCancellationDoesNotConsumeOnceOrSaveGate()
        {
            Event("map", NarrativeEntryPoint.MapAction, "a").blocksSave = true;
            var p = new Presenter(); var c = Formal(p); c.SetBar("a", 2); c.EndTurn();
            Assert.That(c.BlocksSave, Is.False); Assert.That(c.QueueEvent("map"), Is.False);
            Assert.That(c.QueueMapActionEvent("map")); Assert.That(c.BlocksSave); p.Cancel();
            Assert.That(c.BlocksSave, Is.False); Assert.That(c.State.HasFiredTrigger("map.trigger"), Is.False);
            Assert.That(c.QueueMapActionEvent("map")); p.Complete();
            Assert.That(c.QueueMapActionEvent("map"), Is.False);
        }
        [Test] public void LocksAndIndependentPreconditionsRecheckBeforeEachQueuedStart()
        {
            Event("first", NarrativeEntryPoint.BeforeNight, "a", priority: 10); Event("second", NarrativeEntryPoint.BeforeNight, "a");
            config.results.Add(new NarrativeResultDefinition { eventId = "first", resultType = NarrativeResultType.LockEvent, target = "second" });
            var p = new Presenter(); var c = Formal(p); c.SetBar("a", 2); c.EndTurn();
            c.ProcessEntry(NarrativeEntryPoint.BeforeNight); p.Complete();
            Assert.That(p.Played, Is.EqualTo(new[] { "first" })); Assert.That(c.IsSafeBoundary);
            c.State.SetEventLocked("second", false); c.ProcessEntry(NarrativeEntryPoint.BeforeNight);
            Assert.That(p.Played, Is.EqualTo(new[] { "first", "second" })); p.Complete();
        }
        [Test] public void MorningRunsOncePerDayAndFailureCanRetryWithoutAdvancingCalendar()
        {
            Event("morning", NarrativeEntryPoint.MorningBeforeMap).onceOnly = false;
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "morning", day = 1, timeSlot = NarrativeTimeSlot.Morning, repeatRule = NarrativeRepeatRule.Daily });
            var p = new Presenter(); var c = Formal(p);
            c.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap); p.Cancel();
            c.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap); p.Complete();
            c.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap); Assert.That(p.Played.Count, Is.EqualTo(2));
            Assert.That(c.State.TryGetBar("world.calendar", out int day) && day == 2);
            var saved = c.CaptureSnapshot(); c.RestoreSnapshot(saved); c.SetOfficialDay(2);
            c.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap); Assert.That(p.Played.Count, Is.EqualTo(2));
        }
        [Test] public void ContinuationStaysAheadOfUnrelatedPhaseEventInSameQueue()
        {
            Event("root", NarrativeEntryPoint.BeforeNight, "a", priority: 10);
            Event("other", NarrativeEntryPoint.BeforeNight, "a"); Event("letter", NarrativeEntryPoint.Immediate); Event("resume", NarrativeEntryPoint.Immediate);
            config.results.Add(new NarrativeResultDefinition { eventId = "root", resultType = NarrativeResultType.QueueEvent, target = "letter" });
            config.results.Add(new NarrativeResultDefinition { eventId = "letter", resultType = NarrativeResultType.QueueEvent, target = "resume" });
            var p = new Presenter(); var c = Formal(p); c.SetBar("a", 2); c.EndTurn(); bool? done = null;
            c.ProcessEntry(NarrativeEntryPoint.BeforeNight, success => done = success); p.Complete();
            Assert.That(p.Played.Last(), Is.EqualTo("letter")); Assert.That(done, Is.Null); p.Complete(); p.Complete(); p.Complete();
            Assert.That(p.Played, Is.EqualTo(new[] { "root", "letter", "resume", "other" })); Assert.That(done, Is.True);
        }
        [Test] public void EmptyPhaseCompletesSynchronouslyAtSafeBoundaryAndAllowsNextPhase()
        {
            var p = new Presenter(); var c = Formal(p); var results = new List<bool>();
            Assert.That(c.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap, success =>
            {
                Assert.That(c.IsSafeBoundary); Assert.That(c.IsBusy, Is.False); results.Add(success);
                Assert.That(c.ProcessEntry(NarrativeEntryPoint.AfterBusiness, results.Add));
            }));
            Assert.That(results, Is.EqualTo(new[] { true, true })); Assert.That(p.Played, Is.Empty);
            Assert.That(c.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap, results.Add));
            Assert.That(results.Count, Is.EqualTo(3));
        }
        [Test] public void AcceptedMapRequestRejectedByFreshLockNotifiesOwnerWithoutCompleting()
        {
            Event("map", NarrativeEntryPoint.MapAction); var p = new Presenter(); var c = Formal(p);
            var failed = new List<string>(); c.EventFailed += (id, _) => failed.Add(id);
            c.EventQueued += request => c.State.SetEventLocked(request.Event.eventId, true);
            Assert.That(c.QueueMapActionEvent("map"));
            Assert.That(p.Played, Is.Empty); Assert.That(failed, Is.EqualTo(new[] { "map" }));
            Assert.That(c.State.HasHappenedEvent("map"), Is.False); Assert.That(c.IsSafeBoundary);
        }
        [Test] public void EntryConfigurationUsesExistingPropertiesAndRejectsMalformedRouting()
        {
            Assert.That(LubanNarrativeLoader.ReadEntryPoint(null), Is.EqualTo(NarrativeEntryPoint.Immediate));
            Assert.That(LubanNarrativeLoader.ReadEntryPoint("entry=MapAction;blocksSave=true"), Is.EqualTo(NarrativeEntryPoint.MapAction));
            Assert.That(LubanNarrativeLoader.ReadBlocksSave("entry=BeforeNight;blocksSave=true"));
            Assert.Throws<FormatException>(() => LubanNarrativeLoader.ReadEntryPoint("entry=3"));
            Assert.Throws<FormatException>(() => LubanNarrativeLoader.ReadEntryPoint("entry=MapAction;entry=Immediate"));
            Assert.Throws<FormatException>(() => LubanNarrativeLoader.ReadBlocksSave("blocksSave=yes"));
        }
    }
}
