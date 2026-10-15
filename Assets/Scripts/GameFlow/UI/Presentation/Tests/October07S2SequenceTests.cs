using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;

namespace Spotlight.GameFlow.Tests
{
    public sealed class October07S2SequenceTests
    {
        // Advance the real sequence deterministically without executing an Editor state change.
        private sealed class Driver : INarrativePresenter, INarrativePresentationCancellation, IDisposable
        {
            public event Action<string> Cancelled;
            public readonly NarrativePresenter Inner;
            public readonly NarrativeController Controller;
            public readonly List<NarrativeConversationPresentationData> Dialogue = new List<NarrativeConversationPresentationData>();
            public readonly List<NarrativeLetterPresentationData> Letters = new List<NarrativeLetterPresentationData>();
            public readonly List<string> Seen = new List<string>();
            public readonly List<string> Hints = new List<string>();
            public Action DialogueDone, LetterDone;
            public readonly List<Action> Callbacks = new List<Action>();
            public int Releases;
            public bool CancelOnRelease;
            public readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
            private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            public Driver(int perception, bool s1Completed = true)
            {
                var config = LubanNarrativeLoader.Load();
                config.bars.Clear(); config.triggers.Clear(); config.endings.Clear();
                config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999, initValue = 2, advanceMode = NarrativeAdvanceMode.Monotonic });
                Inner = new NarrativePresenter(config, key => config.texts.Find(x => x.key == key)?.content ?? string.Empty);
                Inner.Cancelled += reason => Cancelled?.Invoke(reason);
                Controller = new NarrativeController(config, this) { AbilityReader = _ => (true, perception) };
                Inner.InlineAbilityCheck = Controller.EvaluateInlineAbilityCheckForPresentation;
                Inner.PageCondition = Controller.State.GetCondition;
                Inner.ConversationPlayback = (request, data, done) =>
                {
                    Dialogue.Add(data); Seen.Add(data.lines[0].lineId); Hints.Add(request.PassiveHint); DialogueDone = done;
                    data.releaseStage = () => { Releases++; if (CancelOnRelease) data.cancelled?.Invoke(); }; return true;
                };
                Inner.LetterPlayback = (request, data, done) =>
                {
                    Letters.Add(data); Seen.Add(data.pageId); LetterDone = done;
                    data.releaseStage = () => { Releases++; if (CancelOnRelease) data.cancelled?.Invoke(); };
                    if (data.revealOnly) done(); return true;
                };
                Controller.SetOfficialDay(2); Controller.ProcessOfficialDay(2);
                if (s1Completed) Controller.State.SetCondition("story.oct07.s1.completed", true);
            }
            public void Play(NarrativeEventRequest request, Action completed)
            {
                Callbacks.Add(completed);
                typeof(NarrativePresenter).GetField("request", Private).SetValue(Inner, request);
                typeof(NarrativePresenter).GetField("completion", Private).SetValue(Inner, completed);
                typeof(NarrativePresenter).GetField("stageCancellationRequested", Private).SetValue(Inner, false);
                var sequence = (IEnumerator)typeof(NarrativePresenter).GetMethod("PlayEvent", Private).Invoke(Inner, null);
                Stack.Push((IEnumerator)typeof(NarrativePresenter).GetMethod("Guarded", Private).Invoke(Inner, new object[] { sequence }));
            }
            public void Pump()
            {
                // Each pump is a simulated next frame; do not let the real frame counter pin a read-close yield.
                typeof(NarrativePresenter).GetField("resumeFrame", Private).SetValue(Inner, -1);
                for (int n = 0; Stack.Count > 0 && n < 100; n++)
                {
                    var current = Stack.Peek();
                    if (!current.MoveNext()) { (Stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current.Current is IEnumerator child) Stack.Push(child); else return;
                }
            }
            public void Begin() { Assert.That(Controller.ProcessEntry(NarrativeEntryPoint.BeforeNight)); Pump(); }
            public void Next() { DialogueDone(); Pump(); }
            public void HostUnavailable()
            {
                typeof(NarrativePresenter).GetMethod("AbortPresentation", Private).Invoke(Inner, new object[] { "host_unavailable" });
                while (Stack.Count > 0) (Stack.Pop() as IDisposable)?.Dispose();
            }
            public void Dispose()
            {
                Inner.Dispose();
                while (Stack.Count > 0) (Stack.Pop() as IDisposable)?.Dispose();
            }
        }
        private static void UntilLetter(Driver d, bool passed)
        {
            d.Begin(); Assert.That(d.Dialogue.Last().lines.First().lineId, Is.EqualTo("oct07.s2.p003"));
            d.Next(); Assert.That(d.Letters.Single().revealOnly);
            Assert.That(d.Dialogue.Last().lines.First().lineId, Is.EqualTo("oct07.s2.p014"));
            d.Next();
            if (passed)
            {
                Assert.That(d.Dialogue.Last().lines.Single().lineId, Is.EqualTo("oct07.s2.p020"));
                Assert.That(d.Hints.Last(), Is.EqualTo("知觉 ≥ 2 · 已察觉")); d.Next();
            }
            Assert.That(d.Dialogue.Last().lines.Single().lineId, Is.EqualTo("oct07.s2.p021"));
            Assert.That(d.Hints.Last(), Is.Null.Or.Empty); d.Next();
            Assert.That(d.Letters.Count, Is.EqualTo(2)); Assert.That(d.Letters.Last().revealOnly, Is.False);
        }
        [Test] public void C02C03SuccessUsesOneOwnerAndReadClosureResumesExactNextLine()
        {
            using (var d = new Driver(2))
            {
                UntilLetter(d, true);
                var letter = d.Letters.Last();
                Assert.That(letter.title, Is.EqualTo("致新任店长：")); Assert.That(letter.titleKey, Is.EqualTo("oct07.s2.p023"));
                Assert.That(letter.readingPageId, Is.EqualTo("oct07.s2.letter.page"));
                Assert.That(letter.lines.Select(x => x.lineId), Is.EqualTo(Enumerable.Range(24, 7).Select(n => "oct07.s2.p" + n.ToString("000"))));
                Assert.That(letter.allowCancel, Is.False);
                foreach (var data in d.Dialogue) Assert.That(data.continuityOwner, Is.SameAs(letter.continuityOwner));
                Assert.That(d.Letters[0].continuityOwner, Is.SameAs(letter.continuityOwner));
                Assert.That(d.Releases, Is.Zero, "Reveal and read do not prematurely release the shared stage");
                Assert.That(d.Controller.BlocksSave); Assert.That(d.Controller.State.GetCondition("story.oct07.s2.completed"), Is.False);
                int before = d.Dialogue.Count; d.LetterDone(); d.Pump();
                Assert.That(d.Dialogue.Count, Is.EqualTo(before), "Read-close callback yields before the next dialogue to prevent input pass-through");
                d.Pump(); Assert.That(d.Dialogue.Last().lines.First().lineId, Is.EqualTo("oct07.s2.p031"));
                Assert.That(d.Controller.State.GetCondition("story.oct07.s2.completed"), Is.False);
                d.Next(); Assert.That(d.Controller.State.GetCondition("story.oct07.s2.completed"));
                Assert.That(d.Releases, Is.EqualTo(d.Dialogue.Count + d.Letters.Count));
            }
        }
        [Test] public void C02FailureSkipsConditionalLineWithoutHintAndJoinsP021()
        {
            using (var d = new Driver(1))
            {
                UntilLetter(d, false);
                Assert.That(d.Seen, Does.Not.Contain("oct07.s2.p020"));
                Assert.That(d.Hints.All(string.IsNullOrEmpty));
                Assert.That(d.Controller.PendingEventIds, Is.Empty);
            }
        }
        [TestCase("oct07.s1")]
        [TestCase("oct07.s2")]
        public void C06FormalSingleRootHostLossRetriesWithoutConsumingOnceOrLateCompletion(string eventId)
        {
            using (var d = new Driver(2, s1Completed: eventId == "oct07.s2"))
            {
                var failures = new List<string>(); var results = new List<bool>();
                d.Controller.EventFailed += (id, _) => failures.Add(id);
                bool Start() => eventId == "oct07.s1" ? d.Controller.QueueMapActionEvent(eventId)
                    : d.Controller.ProcessEntry(NarrativeEntryPoint.BeforeNight, results.Add);
                Assert.That(Start()); d.Pump(); var stalePage = d.DialogueDone; var staleRoot = d.Callbacks.Last();
                Assert.That(d.Controller.BlocksSave); Assert.That(d.Callbacks.Count, Is.EqualTo(1));
                d.HostUnavailable();
                Assert.That(d.Controller.IsSafeBoundary); Assert.That(d.Controller.IsBusy, Is.False);
                Assert.That(d.Controller.BlocksSave, Is.False); Assert.That(d.Controller.State.HasHappenedEvent(eventId), Is.False);
                Assert.That(failures, Is.EqualTo(new[] { eventId }));
                if (eventId == "oct07.s2") Assert.That(results, Is.EqualTo(new[] { false }));
                Assert.That(Start()); d.Pump(); stalePage(); staleRoot(); d.Pump();
                Assert.That(d.Controller.BlocksSave); Assert.That(d.Controller.State.HasHappenedEvent(eventId), Is.False);
                Assert.That(d.Callbacks.Count, Is.EqualTo(2), "Each retry owns exactly one root, not per-page events.");
                d.Dialogue.Last().cancelled(); d.Pump();
                Assert.That(d.Controller.IsBusy, Is.False); Assert.That(d.Controller.BlocksSave, Is.False);
                Assert.That(failures, Is.EqualTo(new[] { eventId, eventId }), "Host loss must retain the cancellation subscription.");
                Assert.That(Start()); d.Pump();
                for (int n = 0; d.Controller.IsBusy && n < 20; n++)
                {
                    if (d.Seen.Last() == d.Letters.LastOrDefault()?.pageId) { d.LetterDone(); d.Pump(); d.Pump(); }
                    else d.Next();
                }
                Assert.That(d.Controller.IsBusy, Is.False); Assert.That(d.Controller.BlocksSave, Is.False);
                Assert.That(d.Controller.State.HasHappenedEvent(eventId));
                Assert.That(d.Controller.State.GetCondition("story." + eventId + ".completed"));
                Assert.That(d.Callbacks.Count, Is.EqualTo(3));
                if (eventId == "oct07.s2") Assert.That(results, Is.EqualTo(new[] { false, false, true }));
            }
        }
        [Test] public void C06RevealCancellationStillOwnsLaterPageAndStageReleaseDoesNotCancelSuccess()
        {
            using (var d = new Driver(2))
            {
                d.Begin(); d.Next();
                Assert.That(d.Letters.Single().revealOnly);
                Assert.That(d.Dialogue.Last().lines.First().lineId, Is.EqualTo("oct07.s2.p014"));
                var late = d.DialogueDone; d.Letters[0].cancelled(); d.Pump(); late(); d.Pump();
                Assert.That(d.Controller.IsBusy, Is.False); Assert.That(d.Controller.BlocksSave, Is.False);
                Assert.That(d.Controller.State.GetCondition("story.oct07.s2.completed"), Is.False);
                Assert.That(d.Seen, Does.Not.Contain("oct07.s2.p020"));
                Assert.That(d.Releases, Is.EqualTo(d.Dialogue.Count + d.Letters.Count));
            }
            using (var d = new Driver(2))
            {
                UntilLetter(d, true);
                // Cleanup may stop a still-revealed envelope; it must not cancel its successful root.
                d.CancelOnRelease = true;
                d.LetterDone(); d.Pump(); d.Pump(); d.Next();
                Assert.That(d.Controller.State.GetCondition("story.oct07.s2.completed"));
                Assert.That(d.Controller.BlocksSave, Is.False);
            }
        }
        [TestCase(false)]
        [TestCase(true)]
        public void C06ExplicitHostInterruptionReleasesRetainedLetterAndAllowsRetry(bool reading)
        {
            using (var d = new Driver(2))
            {
                if (reading) UntilLetter(d, true);
                else { d.Begin(); d.Next(); }
                var latePage = d.DialogueDone; var lateLetter = d.LetterDone; var lateRoot = d.Callbacks.Last();
                Assert.That(d.Letters.Count, Is.GreaterThan(0));
                d.Inner.InterruptForHost(); d.Pump();
                latePage(); lateLetter(); lateRoot(); d.Pump();
                Assert.That(d.Controller.IsBusy, Is.False);
                Assert.That(d.Controller.BlocksSave, Is.False);
                Assert.That(d.Controller.State.HasHappenedEvent("oct07.s2"), Is.False);
                Assert.That(d.Controller.State.GetCondition("story.oct07.s2.completed"), Is.False);
                Assert.That(d.Controller.State.GetCondition("story.oct09.meeting.known"), Is.False);
                Assert.That(d.Releases, Is.EqualTo(d.Dialogue.Count + d.Letters.Count));
                d.Begin();
                Assert.That(d.Dialogue.Last().lines.First().lineId, Is.EqualTo("oct07.s2.p003"));
            }
        }
        [Test] public void C06DisposedIdlePresenterAfterHostLossRemainsTerminal()
        {
            using (var d = new Driver(2))
            {
                d.Begin(); var late = d.Callbacks.Last(); d.HostUnavailable(); d.Inner.Dispose(); late();
                Assert.That(d.Controller.ProcessEntry(NarrativeEntryPoint.BeforeNight), Is.False);
                Assert.That(d.Controller.IsBusy, Is.False); Assert.That(d.Controller.BlocksSave, Is.False);
                Assert.That(d.Controller.State.GetCondition("story.oct07.s2.completed"), Is.False);
            }
        }
        [Test] public void C06LetterCancellationAbortsRootAndInvalidatesLateCompletion()
        {
            using (var d = new Driver(2))
            {
                UntilLetter(d, true);
                var stale = d.LetterDone; d.Letters.Last().cancelled(); d.Pump(); stale(); d.Pump();
                Assert.That(d.Controller.State.GetCondition("story.oct07.s2.completed"), Is.False);
                Assert.That(d.Controller.State.GetCondition("story.oct09.meeting.known"), Is.False);
                Assert.That(d.Controller.BlocksSave, Is.False);
                Assert.That(d.Seen, Does.Not.Contain("oct07.s2.p031"));
                Assert.That(d.Releases, Is.EqualTo(d.Dialogue.Count + d.Letters.Count));
            }
        }
    }
}
