using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class October07S2InlineTests
    {
        private sealed class Presenter : INarrativePresenter, INarrativePresentationCancellation
        {
            public event Action<string> Cancelled;
            public readonly List<Action> Callbacks = new List<Action>();
            public readonly List<string> Played = new List<string>();
            public void Play(NarrativeEventRequest request, Action completed) { Played.Add(request.Event.eventId); Callbacks.Add(completed); }
            public void Complete() => Callbacks.Last()();
            public void Cancel() => Cancelled?.Invoke("playback_cancelled");
        }
        private static NarrativeConfiguration Config()
        {
            var c = LubanNarrativeLoader.Load();
            // Keep real event/check/page/condition/result data; isolate unrelated external host providers.
            c.bars.Clear(); c.triggers.Clear(); c.endings.Clear();
            c.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999, initValue = 2, advanceMode = NarrativeAdvanceMode.Monotonic });
            return c;
        }
        private static NarrativeController Formal(NarrativeConfiguration config, Presenter p)
        {
            var c = new NarrativeController(config, p);
            c.SetOfficialDay(2); c.ProcessOfficialDay(2);
            return c;
        }
        [Test] public void C01S2RequiresS1AndBeforeNightWithoutBusinessCount()
        {
            var p = new Presenter(); var c = Formal(Config(), p);
            Assert.That(c.QueueEvent("oct07.s2"), Is.False);
            c.ProcessEntry(NarrativeEntryPoint.BeforeNight); Assert.That(p.Played, Is.Empty);
            c.State.SetCondition("story.oct07.s1.completed", true);
            c.ProcessEntry(NarrativeEntryPoint.AfterBusiness); Assert.That(p.Played, Is.Empty);
            bool? complete = null;
            Assert.That(c.ProcessEntry(NarrativeEntryPoint.BeforeNight, value => complete = value));
            Assert.That(p.Played, Is.EqualTo(new[] { "oct07.s2" }));
            Assert.That(c.ProcessEntry(NarrativeEntryPoint.BeforeNight), Is.False);
            Assert.That(c.BlocksSave); Assert.That(complete, Is.Null);
            p.Complete(); Assert.That(complete, Is.True); Assert.That(c.BlocksSave, Is.False);
        }
        private static void CheckSnapshot(int perception, bool passed)
        {
            var p = new Presenter(); var c = Formal(Config(), p);
            c.State.SetCondition("story.oct07.s1.completed", true);
            int reads = 0, evaluations = 0;
            c.AbilityReader = _ => { reads++; return (true, perception); };
            c.AbilityCheckEvaluated += _ => evaluations++;
            c.ProcessEntry(NarrativeEntryPoint.BeforeNight);
            var first = c.EvaluateInlineAbilityCheckForPresentation("oct07.s2.perception");
            Assert.That(first.passed, Is.EqualTo(passed)); Assert.That(first.values.Single().currentValue, Is.EqualTo(perception));
            Assert.That(first.values.Single().displayName, Is.EqualTo("知觉"));
            string hint = PassiveCheckPresentationConfig.Hint(first);
            if (passed) Assert.That(hint, Is.EqualTo("需要 知觉 到达 2 · 已察觉")); else Assert.That(hint, Is.Null);
            first.values[0].currentValue = -100; first.passed = !passed;
            perception = passed ? 1 : 2;
            var second = c.EvaluateAbilityCheckForPresentation("oct07.s2.perception");
            Assert.That(second.passed, Is.EqualTo(passed)); Assert.That(second.values[0].currentValue, Is.Not.EqualTo(-100));
            Assert.That(reads, Is.EqualTo(1)); Assert.That(evaluations, Is.EqualTo(1));
            Assert.That(second.branchQueued || second.resultApplied, Is.False);
            Assert.That(c.ActiveAbilityCheck, Is.Null); Assert.That(c.PendingEventIds, Is.Empty);
            Assert.That(c.State.GetCondition("story.oct07.s2.completed"), Is.False);
            p.Complete(); Assert.That(p.Played, Has.Count.EqualTo(1));
        }
        [Test] public void C02PerceptionOneSkipsWithOneSnapshotAndNoHint() => CheckSnapshot(1, false);
        [Test] public void C02PerceptionTwoPassesWithOneSnapshotAndExactHint() => CheckSnapshot(2, true);
        [Test] public void C03ActualSevenStepsUseReadingTitleAndSevenIntactCipherLines()
        {
            var c = LubanNarrativeLoader.Load();
            var root = c.events.Single(x => x.eventId == "oct07.s2");
            Assert.That(root.entry, Is.EqualTo(NarrativeEntryPoint.BeforeNight)); Assert.That(root.blocksSave && root.onceOnly);
            var pages = c.presentations.conversationPages.Where(x => x.presentationKey == root.presentationKey).OrderBy(x => x.seq).ToArray();
            Assert.That(pages.Select(x => x.stepType), Is.EqualTo(new[] { NarrativeConversationStepType.Dialogue, NarrativeConversationStepType.LetterReveal,
                NarrativeConversationStepType.Dialogue, NarrativeConversationStepType.PassiveCheck, NarrativeConversationStepType.Dialogue,
                NarrativeConversationStepType.LetterRead, NarrativeConversationStepType.Dialogue }));
            Assert.That(pages.Select(x => c.presentations.conversationLines.Count(l => l.pageId == x.pageId)), Is.EqualTo(new[] { 10, 0, 5, 1, 1, 0, 17 }));
            Assert.That(pages[1].readingKey, Is.EqualTo(pages[5].readingKey));
            var reading = c.presentations.readingPages.Single(x => x.presentationKey == pages[5].readingKey);
            Assert.That(c.texts.Single(x => x.key == reading.titleKey).content, Is.EqualTo("致新任店长："));
            var lines = c.presentations.readingLines.Where(x => x.pageId == reading.pageId).OrderBy(x => x.seq).ToArray();
            Assert.That(lines.Length, Is.EqualTo(7));
            foreach (var line in lines)
            {
                var tokens = c.texts.Single(x => x.key == line.textKey).content.Split(' ');
                Assert.That(tokens.Length, Is.EqualTo(33));
                Assert.That(tokens.All(t => t.Length == 2 && int.TryParse(t, out _)));
            }
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(c), Is.Empty);
        }
        [Test] public void C05RootOnlyCommitsTwoFactsAndResumesOnce()
        {
            var config = Config(); var p = new Presenter(); var c = Formal(config, p);
            var results = config.results.Where(x => x.eventId == "oct07.s2").ToArray();
            Assert.That(results, Has.Length.EqualTo(2)); Assert.That(results.All(x => x.resultType == NarrativeResultType.SetFlag));
            Assert.That(results.Select(x => x.target), Is.EquivalentTo(new[] { "story.oct07.s2.completed", "story.oct09.meeting.known" }));
            c.State.SetCondition("story.oct07.s1.completed", true); int continuation = 0;
            c.ProcessEntry(NarrativeEntryPoint.BeforeNight, ok => { Assert.That(ok); continuation++; });
            Assert.That(c.State.GetCondition("story.oct09.meeting.known"), Is.False);
            p.Complete(); p.Complete();
            Assert.That(continuation, Is.EqualTo(1)); Assert.That(c.State.GetCondition("story.oct07.s2.completed"));
            Assert.That(c.State.GetCondition("story.oct09.meeting.known")); Assert.That(c.IsSafeBoundary);
            c.State.TryGetBar("world.calendar", out int day); Assert.That(day, Is.EqualTo(2), "NCS root does not independently advance the host date");
        }
        [Test] public void C06CancelRejectsOldCompletionAndRetryReevaluatesSnapshot()
        {
            var p = new Presenter(); var c = Formal(Config(), p);
            c.State.SetCondition("story.oct07.s1.completed", true); int reads = 0; int perception = 1;
            c.AbilityReader = _ => { reads++; return (true, perception); };
            c.ProcessEntry(NarrativeEntryPoint.BeforeNight);
            Assert.That(c.EvaluateInlineAbilityCheckForPresentation("oct07.s2.perception").passed, Is.False);
            Action stale = p.Callbacks.Last(); p.Cancel(); stale();
            Assert.That(c.State.GetCondition("story.oct07.s2.completed"), Is.False); Assert.That(c.BlocksSave, Is.False);
            perception = 2; Assert.That(c.ProcessEntry(NarrativeEntryPoint.BeforeNight));
            stale(); Assert.That(c.CurrentEventId, Is.EqualTo("oct07.s2"));
            Assert.That(c.EvaluateInlineAbilityCheckForPresentation("oct07.s2.perception").passed);
            Assert.That(reads, Is.EqualTo(2)); p.Complete(); Assert.That(c.State.GetCondition("story.oct07.s2.completed"));
        }
        [Test] public void EmptyModesRemainLegacyAndInlineReferencesFailClosed()
        {
            Assert.That(LubanNarrativeLoader.ParseConversationStepType(""), Is.EqualTo(NarrativeConversationStepType.Dialogue));
            Assert.That(LubanNarrativeLoader.ParseAbilityCheckMode(null), Is.EqualTo(NarrativeAbilityCheckMode.Branch));
            Assert.Throws<FormatException>(() => LubanNarrativeLoader.ParseConversationStepType("typo"));
            Assert.Throws<FormatException>(() => LubanNarrativeLoader.ParseAbilityCheckMode("99"));
            var c = LubanNarrativeLoader.Load(); var check = c.abilityChecks.Single(x => x.checkId == "oct07.s2.perception");
            check.successEventId = "oct07.s2";
            Assert.That(LubanNarrativeLoader.ValidateAbilityCheckMode(c, check), Is.Not.Empty);
            check.successEventId = null; check.mode = NarrativeAbilityCheckMode.Branch;
            Assert.That(LubanNarrativeLoader.ValidateAbilityCheckMode(c, check), Has.Count.EqualTo(2));
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(c), Is.Not.Empty);
        }
    }
}
