using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;

namespace Spotlight.GameFlow.Tests
{
    public sealed class ConversationReadingStepTests
    {
        private sealed class Driver : IDisposable
        {
            private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            public readonly NarrativePresenter Presenter;
            public readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
            public readonly List<NarrativeConversationPresentationData> Dialogue = new List<NarrativeConversationPresentationData>();
            public NarrativeConversationReadingPresentationData Reading;
            public Action ReadDone, DialogueDone;
            public bool Completed;
            public int Releases, Cancellations, LetterCalls;
            public Driver(bool history)
            {
                var config = Configuration();
                Presenter = new NarrativePresenter(config, key => "resolved:" + key);
                Presenter.PageCondition = _ => history;
                Presenter.Cancelled += _ => Cancellations++;
                Presenter.LetterPlayback = (_, __, ___) => { LetterCalls++; return false; };
                Presenter.ConversationPlayback = (_, data, done) =>
                {
                    Dialogue.Add(data); DialogueDone = done;
                    data.releaseStage = () => Releases++;
                    if (data.lines[0].lineId == "notice") done();
                    return true;
                };
                Presenter.ConversationReadingPlayback = (_, data, done) =>
                {
                    Reading = data; ReadDone = done;
                    data.releaseStage = () => Releases++;
                    return true;
                };
                var request = new NarrativeEventRequest(new NarrativeEventDefinition
                { eventId = "root", presentationKey = "conversation", blocksSave = true }, null, 0);
                typeof(NarrativePresenter).GetField("request", Private).SetValue(Presenter, request);
                typeof(NarrativePresenter).GetField("completion", Private).SetValue(Presenter, (Action)(() => Completed = true));
                var sequence = (IEnumerator)typeof(NarrativePresenter).GetMethod("PlayEvent", Private).Invoke(Presenter, null);
                Stack.Push((IEnumerator)typeof(NarrativePresenter).GetMethod("Guarded", Private).Invoke(Presenter, new object[] { sequence }));
                Pump();
            }
            public void Pump()
            {
                typeof(NarrativePresenter).GetField("resumeFrame", Private).SetValue(Presenter, -1);
                for (int n = 0; Stack.Count > 0 && n < 100; n++)
                {
                    var current = Stack.Peek();
                    if (!current.MoveNext()) { (Stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current.Current is IEnumerator nested) Stack.Push(nested); else return;
                }
            }
            public void Dispose()
            {
                Presenter.Dispose();
                while (Stack.Count > 0) (Stack.Pop() as IDisposable)?.Dispose();
            }
        }

        public static NarrativeConfiguration Configuration()
        {
            var config = new NarrativeConfiguration();
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_TwoPersonTalk", atomSeq = "A_DIALOG" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "conversation", presentationClass = "Conversation" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "card", presentationClass = "Reading" });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "notice", presentationKey = "conversation", seq = 1 });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
            { pageId = "card-step", presentationKey = "conversation", seq = 2, stepType = NarrativeConversationStepType.Reading, readingKey = "card" });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "recognition", presentationKey = "conversation", seq = 3, pageCondKeys = "history" });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "full-end", presentationKey = "conversation", seq = 4 });
            foreach (string id in new[] { "notice", "recognition", "full-end" })
                config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition { pageId = id, lineId = id, textKey = id, seq = 1 });
            config.presentations.readingPages.Add(new NarrativeReadingPageDefinition { pageId = "card-page", presentationKey = "card", seq = 1 });
            for (int seq = 3; seq > 0; seq--)
            {
                string id = "card-" + seq;
                config.presentations.readingLines.Add(new NarrativeReadingLineDefinition { pageId = "card-page", lineId = id, textKey = id, seq = seq, mode = "narration" });
                config.texts.Add(new NarrativeTextDefinition { key = id, content = id });
            }
            return config;
        }

        [TestCase(false, "full-end")]
        [TestCase(true, "recognition")]
        public void ThreeOrderedCardLinesResumeConditionalRootOnFreshPump(bool history, string next)
        {
            using (var d = new Driver(history))
            {
                Assert.That(d.Reading, Is.Not.Null);
                Assert.That(d.Reading.title, Is.Empty, "Do not repeat card row one as an invented title.");
                Assert.That(d.Reading.lines.Select(x => x.text), Is.EqualTo(new[] { "resolved:card-1", "resolved:card-2", "resolved:card-3" }));
                Assert.That(d.Reading.continuityOwner, Is.SameAs(d.Dialogue[0].continuityOwner));
                Assert.That(d.Reading.allowCancel, Is.False);
                Assert.That(d.LetterCalls, Is.Zero);
                var lateCancel = d.Reading.cancelled;
                d.Reading.playbackStopped(); d.ReadDone(); d.ReadDone();
                d.Pump();
                Assert.That(d.Dialogue.Count, Is.EqualTo(1), "Close input cannot advance the resumed root in this frame.");
                Assert.That(d.Completed, Is.False);
                d.Pump();
                Assert.That(d.Dialogue.Last().lines[0].lineId, Is.EqualTo(next));
                lateCancel(); d.ReadDone(); d.Pump();
                Assert.That(d.Cancellations, Is.Zero);
                if (history) { d.DialogueDone(); d.Pump(); }
                d.DialogueDone(); d.Pump();
                Assert.That(d.Completed, Is.True);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancelOrStopAloneNeverResumesOrCompletesRoot(bool stopOnly)
        {
            using (var d = new Driver(true))
            {
                if (stopOnly) d.Reading.playbackStopped(); else d.Reading.cancelled();
                if (!stopOnly) d.ReadDone();
                d.Pump(); d.Pump();
                d.ReadDone(); d.Pump();
                Assert.That(d.Completed, Is.False);
                Assert.That(d.Cancellations, Is.EqualTo(1));
                Assert.That(d.Dialogue.Count, Is.EqualTo(1));
                Assert.That(d.Releases, Is.GreaterThanOrEqualTo(2));
            }
        }

        [Test]
        public void DisposeInvalidatesAllReadingCallbacksAndReleasesRootStage()
        {
            using (var d = new Driver(true))
            {
                d.Dispose();
                d.ReadDone(); d.Reading.cancelled(); d.Reading.playbackStopped(); d.Pump();
                Assert.That(d.Completed, Is.False);
                Assert.That(d.Dialogue.Count, Is.EqualTo(1));
                Assert.That(d.Releases, Is.GreaterThanOrEqualTo(2));
            }
        }

        [Test]
        public void ReadingParsesAndValidatesWithoutLetterOrDuplicateTitleRequirement()
        {
            Assert.That(LubanNarrativeLoader.ParseConversationStepType(" reading "), Is.EqualTo(NarrativeConversationStepType.Reading));
            var config = Configuration();
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config), Is.Empty);
            var step = config.presentations.conversationPages.Single(p => p.pageId == "card-step");
            step.optionGroupKey = "choice";
            step.checkId = "check";
            var errors = LubanNarrativeLoader.ValidateConversationSteps(config);
            Assert.That(errors.Any(e => e.Contains("Reading cannot contain dialogue or choices")), Is.True);
            Assert.That(errors.Any(e => e.Contains("checkId requires PassiveCheck")), Is.True);
            step.optionGroupKey = step.checkId = null;
            config.presentations.readingLines[0].seq = config.presentations.readingLines[1].seq;
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config).Any(e => e.Contains("invalid reading line sequence")), Is.True);
        }
    }
}
