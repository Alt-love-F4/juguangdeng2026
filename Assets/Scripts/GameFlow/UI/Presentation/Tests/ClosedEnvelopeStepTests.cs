using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;

namespace Spotlight.GameFlow.Tests
{
    public sealed class ClosedEnvelopeStepTests
    {
        private sealed class Driver : IDisposable
        {
            private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly Func<bool> previousInputBlocker;
            private readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
            private bool disposed;
            public readonly NarrativePresenter Presenter;
            public readonly List<string> Timeline = new List<string>();
            public readonly List<NarrativeConversationPresentationData> Dialogue = new List<NarrativeConversationPresentationData>();
            public readonly List<Action> DialogueDone = new List<Action>();
            public readonly List<string> Cancellations = new List<string>();
            public NarrativeLetterPresentationData Letter;
            public Action RevealDone;
            public int Completions, Finished, Dismissals, LetterReleases, DialogueReleases;

            public Driver(bool repeatHide = false)
            {
                previousInputBlocker = NarrativePresenter.ExternalInputBlocked;
                NarrativePresenter.ExternalInputBlocked = null;
                Presenter = new NarrativePresenter(Configuration(repeatHide), key => key ?? string.Empty);
                Presenter.Cancelled += reason => Cancellations.Add(reason);
                Presenter.Finished += _ => Finished++;
                Presenter.LetterPlayback = (_, data, done) =>
                {
                    Letter = data;
                    RevealDone = done;
                    Timeline.Add("reveal");
                    data.dismissStage = () =>
                    {
                        Dismissals++;
                        Timeline.Add("hide");
                        // Real adapters can report cancellation synchronously while stopping visuals.
                        data.cancelled();
                    };
                    data.releaseStage = () => { LetterReleases++; data.cancelled(); };
                    return true;
                };
                Presenter.ConversationPlayback = (_, data, done) =>
                {
                    Dialogue.Add(data);
                    DialogueDone.Add(done);
                    Timeline.Add(data.lines.Single().lineId);
                    data.releaseStage = () => { DialogueReleases++; data.cancelled(); };
                    return true;
                };
                var request = new NarrativeEventRequest(new NarrativeEventDefinition
                {
                    eventId = "closed-envelope.event", presentationKey = "conversation", blocksSave = true,
                    eventProperties = new List<NarrativeEventProperty>
                    {
                        new NarrativeEventProperty { property = "envelope", value = "test/closed-envelope" }
                    }
                }, null, 0);
                typeof(NarrativePresenter).GetField("request", Private).SetValue(Presenter, request);
                typeof(NarrativePresenter).GetField("completion", Private).SetValue(Presenter,
                    (Action)(() => { Completions++; Timeline.Add("complete"); }));
                var sequence = (IEnumerator)typeof(NarrativePresenter).GetMethod("PlayEvent", Private).Invoke(Presenter, null);
                stack.Push((IEnumerator)typeof(NarrativePresenter).GetMethod("Guarded", Private)
                    .Invoke(Presenter, new object[] { sequence }));
                Pump();
            }

            public void Pump()
            {
                typeof(NarrativePresenter).GetField("resumeFrame", Private).SetValue(Presenter, -1);
                for (int n = 0; stack.Count > 0 && n < 100; n++)
                {
                    var current = stack.Peek();
                    if (!current.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current.Current is IEnumerator nested) stack.Push(nested); else return;
                }
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                try
                {
                    Presenter.Dispose();
                    while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                }
                finally { NarrativePresenter.ExternalInputBlocked = previousInputBlocker; }
            }
        }

        private static NarrativeConfiguration Configuration(bool repeatHide = false)
        {
            var config = new NarrativeConfiguration();
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_TwoPersonTalk", atomSeq = "A_DIALOG" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition
                { presentationKey = "conversation", presentationClass = "Conversation" });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = "reveal", presentationKey = "conversation", seq = 1, stepType = NarrativeConversationStepType.LetterReveal });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = "before-hide", presentationKey = "conversation", seq = 2 });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = "hide", presentationKey = "conversation", seq = 3, stepType = NarrativeConversationStepType.LetterHide });
            if (repeatHide)
                config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                    { pageId = "hide-again", presentationKey = "conversation", seq = 4, stepType = NarrativeConversationStepType.LetterHide });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = "after-hide", presentationKey = "conversation", seq = repeatHide ? 5 : 4 });
            foreach (string id in new[] { "before-hide", "after-hide" })
                config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition
                    { pageId = id, lineId = id, textKey = id, seq = 1 });
            return config;
        }

        [Test]
        public void BodylessRevealDialogueHideDialogueCompletesOnlyAfterFinalStep()
        {
            using (var d = new Driver())
            {
                Assert.That(d.Letter, Is.Not.Null);
                Assert.That(d.Letter.lines, Is.Empty, "A closed prop must not invent reading content.");
                Assert.That(d.Letter.readingKey, Is.Null.Or.Empty);
                Assert.That(d.Letter.readingPageId, Is.Null.Or.Empty);
                Assert.That(d.Letter.title, Is.Empty);
                Assert.That(d.Letter.revealOnly, Is.True);
                Assert.That(d.Letter.allowCancel, Is.False);
                Assert.That(d.Letter.envelopePath, Is.EqualTo("test/closed-envelope"));
                Assert.That(d.Dialogue, Is.Empty);

                var oldCancel = d.Letter.cancelled;
                d.RevealDone();
                d.RevealDone();
                Assert.That(d.Completions, Is.Zero, "Reveal callback acknowledges its step, not the root event.");
                Assert.That(d.Dialogue, Is.Empty, "Callbacks must leave sequencing to the presenter pump.");
                d.Pump();
                Assert.That(d.Timeline, Is.EqualTo(new[] { "reveal", "before-hide" }));
                Assert.That(d.Letter.continuityOwner, Is.SameAs(d.Dialogue[0].continuityOwner));
                Assert.That(d.Dismissals, Is.Zero, "The envelope remains visible through the first dialogue.");
                Assert.That(d.LetterReleases, Is.Zero);
                Assert.That(d.Finished, Is.Zero);
                Assert.That(d.Completions, Is.Zero);

                d.DialogueDone[0]();
                Assert.That(d.Completions, Is.Zero);
                Assert.That(d.Dismissals, Is.Zero);
                d.Pump();
                Assert.That(d.Timeline, Is.EqualTo(new[] { "reveal", "before-hide", "hide", "after-hide" }));
                Assert.That(d.Dismissals, Is.EqualTo(1));
                Assert.That(d.Dialogue[1].continuityOwner, Is.SameAs(d.Letter.continuityOwner));
                Assert.That(d.Cancellations, Is.Empty, "Normal dismiss must ignore its synchronous old cancel callback.");
                Assert.That(d.LetterReleases, Is.Zero, "Hide dismisses the prop; root teardown owns its release.");
                Assert.That(d.Completions, Is.Zero);
                Assert.That(d.Finished, Is.Zero);

                oldCancel();
                d.RevealDone();
                d.DialogueDone[0]();
                d.Pump();
                Assert.That(d.Cancellations, Is.Empty);
                Assert.That(d.Dialogue.Count, Is.EqualTo(2));
                Assert.That(d.Dismissals, Is.EqualTo(1));
                Assert.That(d.Completions, Is.Zero, "Stale step callbacks cannot finish the final dialogue.");
                d.DialogueDone[1]();
                Assert.That(d.Completions, Is.Zero);
                d.Pump();
                Assert.That(d.Timeline, Is.EqualTo(new[] { "reveal", "before-hide", "hide", "after-hide", "complete" }));
                Assert.That(d.Completions, Is.EqualTo(1));
                Assert.That(d.Finished, Is.EqualTo(1));
                Assert.That(d.LetterReleases, Is.EqualTo(1));
                Assert.That(d.DialogueReleases, Is.EqualTo(2));
                Assert.That(d.Presenter.IsPresenting, Is.False);

                oldCancel();
                d.RevealDone();
                d.DialogueDone[1]();
                d.Pump();
                Assert.That(d.Cancellations, Is.Empty);
                Assert.That(d.Completions, Is.EqualTo(1));
                Assert.That(d.Dismissals, Is.EqualTo(1));
            }
        }

        [Test]
        public void RepeatedHideDoesNotInvokeStoredDismissAgain()
        {
            using (var d = new Driver(repeatHide: true))
            {
                d.RevealDone(); d.Pump();
                d.DialogueDone[0](); d.Pump();
                Assert.That(d.Timeline, Is.EqualTo(new[] { "reveal", "before-hide", "hide", "after-hide" }));
                Assert.That(d.Dismissals, Is.EqualTo(1));
                Assert.That(d.Cancellations, Is.Empty);
                d.DialogueDone[1](); d.Pump();
                Assert.That(d.Completions, Is.EqualTo(1));
                Assert.That(d.Dismissals, Is.EqualTo(1));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancelBeforeHideNeverCompletesRoot(bool revealAcknowledged)
        {
            using (var d = new Driver())
            {
                if (revealAcknowledged) { d.RevealDone(); d.Pump(); }
                d.Letter.cancelled();
                d.RevealDone();
                if (d.DialogueDone.Count > 0) d.DialogueDone[0]();
                d.Pump(); d.Pump();
                Assert.That(d.Cancellations, Is.EqualTo(new[] { "playback_cancelled" }));
                Assert.That(d.Completions, Is.Zero);
                Assert.That(d.Finished, Is.Zero);
                Assert.That(d.Dismissals, Is.Zero);
                Assert.That(d.Dialogue.Count, Is.EqualTo(revealAcknowledged ? 1 : 0));
                Assert.That(d.LetterReleases, Is.EqualTo(1));
                Assert.That(d.DialogueReleases, Is.EqualTo(revealAcknowledged ? 1 : 0));
                Assert.That(d.Presenter.IsPresenting, Is.False);
                d.Letter.cancelled(); d.RevealDone(); d.Pump();
                Assert.That(d.Cancellations.Count, Is.EqualTo(1));
                Assert.That(d.Completions, Is.Zero);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisposeReleasesRetainedStagesAndInvalidatesCallbacks(bool afterHide)
        {
            using (var d = new Driver())
            {
                d.RevealDone(); d.Pump();
                if (afterHide) { d.DialogueDone[0](); d.Pump(); }
                int dialogueCount = d.Dialogue.Count;
                d.Dispose();
                Assert.That(d.LetterReleases, Is.EqualTo(1));
                Assert.That(d.DialogueReleases, Is.EqualTo(dialogueCount));
                Assert.That(d.Cancellations, Is.EqualTo(new[] { "disposed" }));
                d.Letter.cancelled(); d.RevealDone();
                foreach (var done in d.DialogueDone) done();
                d.Pump(); d.Dispose();
                Assert.That(d.Completions, Is.Zero);
                Assert.That(d.Finished, Is.Zero);
                Assert.That(d.Dialogue.Count, Is.EqualTo(dialogueCount));
                Assert.That(d.Dismissals, Is.EqualTo(afterHide ? 1 : 0));
                Assert.That(d.LetterReleases, Is.EqualTo(1));
                Assert.That(d.DialogueReleases, Is.EqualTo(dialogueCount));
                Assert.That(d.Cancellations.Count, Is.EqualTo(1));
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void LoaderAcceptsBodylessRevealButRejectsBodylessRead(string readingKey)
        {
            Assert.That(LubanNarrativeLoader.ParseConversationStepType(" letterhide "), Is.EqualTo(NarrativeConversationStepType.LetterHide));
            Assert.That((int)NarrativeConversationStepType.LetterHide, Is.EqualTo((int)NarrativeConversationStepType.WaitService + 1));
            var config = Configuration();
            var reveal = config.presentations.conversationPages.Single(p => p.pageId == "reveal");
            reveal.readingKey = readingKey;
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config), Is.Empty);
            reveal.stepType = NarrativeConversationStepType.LetterRead;
            var errors = LubanNarrativeLoader.ValidateConversationSteps(config);
            Assert.That(errors.Any(e => e.Contains("readingKey must reference a Reading presentation")), Is.True);
        }

        [TestCase("dialogue")]
        [TestCase("check")]
        [TestCase("reading")]
        [TestCase("choices")]
        public void LoaderRejectsEachKindOfHideContent(string content)
        {
            var config = Configuration();
            var hide = config.presentations.conversationPages.Single(p => p.pageId == "hide");
            switch (content)
            {
                case "dialogue":
                    config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition
                        { pageId = "hide", lineId = "forbidden", textKey = "forbidden", seq = 1 });
                    break;
                case "check": hide.checkId = "forbidden"; break;
                case "reading": hide.readingKey = "forbidden"; break;
                case "choices": hide.optionGroupKey = "forbidden"; break;
            }
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config)
                .Any(e => e.Contains("ConversationPage hide: LetterHide cannot contain")), Is.True);
        }

        [TestCase(NarrativeConversationStepType.LetterReveal)]
        [TestCase(NarrativeConversationStepType.LetterRead)]
        public void LoaderStillValidatesExplicitReadingReferences(NarrativeConversationStepType type)
        {
            var config = Configuration();
            var step = config.presentations.conversationPages.Single(p => p.pageId == "reveal");
            step.stepType = type;
            step.readingKey = "explicit-reading";
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config)
                .Any(e => e.Contains("readingKey must reference a Reading presentation")), Is.True);

            var index = new NarrativePresentationIndexDefinition { presentationKey = "explicit-reading", presentationClass = "Conversation" };
            config.presentations.index.Add(index);
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config)
                .Any(e => e.Contains("readingKey must reference a Reading presentation")), Is.True);
            index.presentationClass = "Reading";
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config)
                .Any(e => e.Contains("letter requires exactly one reading page")), Is.True);
            config.presentations.readingPages.Add(new NarrativeReadingPageDefinition
                { pageId = "explicit-page", presentationKey = "explicit-reading", seq = 1 });
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config)
                .Any(e => e.Contains("letter requires reading lines")), Is.True);
            config.presentations.readingLines.Add(new NarrativeReadingLineDefinition
                { pageId = "explicit-page", lineId = "body", textKey = "body", seq = 1 });
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config)
                .Any(e => e.Contains("unknown reading text body")), Is.True);
            config.texts.Add(new NarrativeTextDefinition { key = "body", content = "letter body" });
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config), Is.Empty);
        }
    }
}
