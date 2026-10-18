using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativePresenterBatchContinuityTests
    {
        // Same deterministic real-sequence driver as October07S2SequenceTests; no Editor mode changes.
        private sealed class Driver : INarrativePresenter, INarrativePresentationContinuity, IDisposable
        {
            private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            public readonly NarrativeConfiguration Config = new NarrativeConfiguration();
            public readonly NarrativePresenter Inner;
            public readonly NarrativeController Controller;
            public readonly List<NarrativeConversationPresentationData> Pages = new List<NarrativeConversationPresentationData>();
            private readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
            private readonly Queue<IEnumerator> roots = new Queue<IEnumerator>();
            public Action Done;
            public int Releases;
            public Driver()
            {
                Config.templates.Add(new NarrativeTemplateDefinition { tplName = "talk", atomSeq = "A_DIALOG" });
                foreach (var id in new[] { "a", "b", "c", "d" })
                {
                    Config.events.Add(new NarrativeEventDefinition { eventId = id, presentationClass = "conversation", presentationKey = id, setupType = "talk" });
                    Config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = id, presentationClass = "conversation" });
                    Config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { presentationKey = id, pageId = id, bgOverride = id == "a" ? "room" : id == "c" ? "scene.cafe" : null });
                    Config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition { pageId = id, lineId = id, textKey = id });
                }
                Inner = new NarrativePresenter(Config);
                Inner.ConversationPlayback = (request, data, done) =>
                {
                    Pages.Add(data); Done = done;
                    data.releaseStage = () => { Releases++; data.cancelled?.Invoke(); };
                    return true;
                };
                Controller = new NarrativeController(Config, this);
            }
            public void BeginBatch() => Inner.BeginBatch();
            public void EndBatch() => Inner.EndBatch();
            public void Play(NarrativeEventRequest request, Action completed)
            {
                typeof(NarrativePresenter).GetField("request", Private).SetValue(Inner, request);
                typeof(NarrativePresenter).GetField("completion", Private).SetValue(Inner, completed);
                typeof(NarrativePresenter).GetField("stageCancellationRequested", Private).SetValue(Inner, false);
                var root = (IEnumerator)typeof(NarrativePresenter).GetMethod("PlayEvent", Private).Invoke(Inner, null);
                roots.Enqueue((IEnumerator)typeof(NarrativePresenter).GetMethod("Guarded", Private).Invoke(Inner, new object[] { root }));
            }
            public void Pump()
            {
                for (int i = 0; (stack.Count > 0 || roots.Count > 0) && i < 100; i++)
                {
                    if (stack.Count == 0) stack.Push(roots.Dequeue());
                    var iterator = stack.Peek();
                    if (!iterator.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (iterator.Current is IEnumerator nested) stack.Push(nested); else return;
                }
            }
            public void Next() { Done(); Pump(); }
            public void Dispose()
            {
                Inner.Dispose();
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                while (roots.Count > 0) (roots.Dequeue() as IDisposable)?.Dispose();
            }
        }

        [Test] public void BatchReusesOwnerInheritsEmptyBackgroundAndReleasesOnlyLatestAtIdle()
        {
            using (var d = new Driver())
            {
                // Enqueue through completion listeners to exercise synchronous controller reentry.
                d.Controller.EventCompleted += id => { if (id == "a") d.Controller.QueueEvent("b"); else if (id == "b") d.Controller.QueueEvent("c"); else if (id == "c") d.Controller.QueueEvent("d"); };
                Assert.That(d.Controller.QueueEvent("a")); d.Pump();
                var owner = d.Pages[0].continuityOwner;
                d.Next(); Assert.That(d.Releases, Is.Zero);
                Assert.That(d.Pages[1].backgroundPath, Is.EqualTo("room"));
                d.Next(); Assert.That(d.Pages[2].backgroundPath, Is.EqualTo("scene.cafe"));
                d.Next(); Assert.That(d.Pages[3].backgroundPath, Is.EqualTo("scene.cafe"));
                foreach (var page in d.Pages) { Assert.That(page.continuityOwner, Is.SameAs(owner)); Assert.That(page.continues); }
                Assert.That(d.Releases, Is.Zero); d.Next();
                Assert.That(d.Controller.IsSafeBoundary); Assert.That(d.Releases, Is.EqualTo(1));
                d.Inner.EndBatch(); Assert.That(d.Releases, Is.EqualTo(1));
                Assert.That(d.Controller.QueueEvent("b")); d.Pump();
                Assert.That(d.Pages[4].continuityOwner, Is.Not.SameAs(owner));
                Assert.That(d.Pages[4].backgroundPath, Is.Null.Or.Empty, "Idle clears inherited background");
            }
        }

        [Test] public void DisposalReleasesRetainedBatchImmediatelyAndLateCompletionCannotReopenIt()
        {
            using (var d = new Driver())
            {
                Assert.That(d.Controller.QueueEvent("a")); d.Pump(); var late = d.Done;
                Assert.That(d.Releases, Is.Zero); d.Inner.Dispose();
                Assert.That(d.Releases, Is.EqualTo(1)); late(); d.Pump();
                Assert.That(d.Pages.Count, Is.EqualTo(1)); Assert.That(d.Releases, Is.EqualTo(1));
            }
        }

        [Test] public void DirectSequenceStillReleasesItsStageWithoutBatchEnd()
        {
            using (var d = new Driver())
            {
                bool completed = false;
                d.Play(new NarrativeEventRequest(d.Config.events[0], null, 0), () => completed = true);
                d.Pump(); Assert.That(d.Pages[0].continues, Is.False);
                d.Next(); Assert.That(completed); Assert.That(d.Releases, Is.EqualTo(1));
            }
        }
    }
}
