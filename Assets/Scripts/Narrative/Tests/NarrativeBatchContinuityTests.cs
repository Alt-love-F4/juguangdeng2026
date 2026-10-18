using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeBatchContinuityTests
    {
        private sealed class Presenter : INarrativePresenter, INarrativePresentationContinuity, INarrativePresentationDrain
        {
            public readonly List<string> Played = new List<string>();
            public Action Completed, Drained;
            public int Begins, Ends;
            public bool Synchronous;
            public void BeginBatch() { Begins++; }
            public void EndBatch() { Ends++; }
            public void Play(NarrativeEventRequest request, Action completed)
            {
                Played.Add(request.Event.eventId); Completed = completed;
                if (Synchronous) completed();
            }
            public void Drain(Action completed) { Drained = completed; }
        }

        private static NarrativeConfiguration Config()
        {
            var config = new NarrativeConfiguration();
            foreach (var id in new[] { "a", "b", "c" })
                config.events.Add(new NarrativeEventDefinition { eventId = id });
            config.bars.Add(new NarrativeBarDefinition { barKey = "value", min = 0, max = 10 });
            return config;
        }

        [Test] public void SettlementDerivedEventSharesBatchAndEndsOnlyAtIdle()
        {
            var config = Config();
            config.results.Add(new NarrativeResultDefinition { eventId = "a", resultType = NarrativeResultType.BarAdd, target = "value", value = "1" });
            config.triggers.Add(new NarrativeTriggerDefinition { triggerId = "next", barKey = "value", eventId = "b", threshold = 1, direction = NarrativeTriggerDirection.Up });
            var p = new Presenter(); var c = new NarrativeController(config, p);
            Assert.That(c.QueueEvent("a"));
            p.Completed();
            Assert.That(p.Played, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(p.Begins, Is.EqualTo(1)); Assert.That(p.Ends, Is.Zero);
            p.Completed(); Assert.That(c.IsSafeBoundary); Assert.That(p.Ends, Is.EqualTo(1));
            c.ResumePendingEvents(); Assert.That(p.Ends, Is.EqualTo(1));
        }

        [Test] public void CompletionListenersAreAtomicAndSafeCallbackContinuationKeepsBatch()
        {
            var p = new Presenter(); var c = new NarrativeController(Config(), p);
            bool secondListenerRan = false;
            c.EventCompleted += id => { if (id == "a") Assert.That(c.QueueEvent("b")); };
            c.EventCompleted += id =>
            {
                if (id != "a") return;
                Assert.That(c.CanCaptureSnapshot, "EventCompleted must remain saveable");
                Assert.That(p.Played, Is.EqualTo(new[] { "a" }));
                secondListenerRan = true;
            };
            Assert.That(c.QueueEvent("a"));
            c.RunAtSafeBoundary(() => { Assert.That(p.Ends, Is.Zero); Assert.That(c.QueueEvent("c")); });
            p.Completed(); Assert.That(secondListenerRan); Assert.That(p.Ends, Is.Zero);
            p.Completed(); Assert.That(p.Played, Is.EqualTo(new[] { "a", "b", "c" })); Assert.That(p.Ends, Is.Zero);
            p.Completed(); Assert.That(p.Ends, Is.EqualTo(1));
        }

        [Test] public void SynchronousContinuationFromTurnCompletedIsNotStrandedOrSplit()
        {
            var p = new Presenter { Synchronous = true }; var c = new NarrativeController(Config(), p);
            c.TurnCompleted += () => Assert.That(c.QueueEvent("b"));
            c.BeginTurn(); Assert.That(c.QueueEvent("a"));
            // The first manual event already reached idle. This turn-end request starts a fresh batch.
            c.EndTurn();
            Assert.That(p.Played, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(c.IsBusy, Is.False); Assert.That(p.Ends, Is.EqualTo(p.Begins));
        }

        [Test] public void PendingSetupDrainRetainsBatchUntilCallbackAndNextEventComplete()
        {
            var config = Config();
            config.results.Add(new NarrativeResultDefinition { eventId = "a", resultType = NarrativeResultType.PlaySetup, target = "after" });
            config.results.Add(new NarrativeResultDefinition { eventId = "a", seq = 1, resultType = NarrativeResultType.QueueEvent, target = "b" });
            var p = new Presenter(); var c = new NarrativeController(config, p);
            Assert.That(c.QueueEvent("a")); p.Completed();
            Assert.That(p.Drained, Is.Not.Null); Assert.That(p.Ends, Is.Zero);
            Assert.That(p.Played, Is.EqualTo(new[] { "a" }));
            var staleDrain = p.Drained; staleDrain();
            Assert.That(p.Played, Is.EqualTo(new[] { "a", "b" })); Assert.That(p.Ends, Is.Zero);
            p.Completed(); staleDrain(); Assert.That(p.Ends, Is.EqualTo(1)); Assert.That(p.Begins, Is.EqualTo(1));
        }

        [TestCase(3)]
        [TestCase(64)]
        public void MultipleSettledProviderBatchesReachIdleWithoutAnotherExternalFlush(int count)
        {
            var config = Config();
            config.bars.Add(new NarrativeBarDefinition { barKey = "external", sourceMode = NarrativeSourceMode.External,
                providerId = "owner", sourceKey = "value", min = 0, max = 100 });
            int value = 0;
            var provider = new DelegateNarrativeValueProvider("owner", key => key == "value" ? value : (int?)null);
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.BeginValueRestore(); c.BindValueProvider(provider); c.CompleteValueRestore(false);
            var delivered = new List<int>();
            c.BarChanged += (key, before, after) => { if (key == "external") delivered.Add(after); };
            Assert.That(c.QueueEvent("a"));
            for (int i = 1; i <= count; i++) { value = i; provider.NotifyChanged(); }
            Assert.That(delivered, Is.Empty); Assert.That(p.Ends, Is.Zero);
            p.Completed();
            Assert.That(delivered.Count, Is.EqualTo(count));
            for (int i = 0; i < count; i++) Assert.That(delivered[i], Is.EqualTo(i + 1));
            Assert.That(c.IsSafeBoundary); Assert.That(c.IsBusy, Is.False);
            Assert.That(p.Begins, Is.EqualTo(1)); Assert.That(p.Ends, Is.EqualTo(1));
        }

        [Test] public void RestoredSetupOnlyDrainAlsoBeginsAndEndsBatch()
        {
            var p = new Presenter(); var c = new NarrativeController(Config(), p);
            var snapshot = c.CaptureSnapshot();
            snapshot.pendingEffects.Add(new NarrativeResultDefinition { resultType = NarrativeResultType.PlaySetup, target = "after" });
            c.RestoreSnapshot(snapshot); c.EndTurn();
            Assert.That(p.Begins, Is.EqualTo(1)); Assert.That(p.Ends, Is.Zero);
            p.Drained(); Assert.That(p.Ends, Is.EqualTo(1));
        }
    }
}
