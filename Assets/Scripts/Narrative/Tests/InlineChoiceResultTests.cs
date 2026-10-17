using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spotlight.Narrative.Tests
{
    /// <summary>Choice-referenced Inline checks: single cached evaluation, check:/not:check: routing,
    /// owner-result collection at root completion, cancel cleanup and snapshot carry.</summary>
    public sealed class InlineChoiceResultTests
    {
        private sealed class Presenter : INarrativePresenter, INarrativePresentationCancellation
        {
            public event Action<string> Cancelled;
            public readonly List<Action> Callbacks = new List<Action>();
            public readonly List<string> Played = new List<string>();
            public void Play(NarrativeEventRequest request, Action completed) { Played.Add(request.Event.eventId); Callbacks.Add(completed); }
            public void Complete() => Callbacks[Callbacks.Count - 1]();
            public void Cancel() => Cancelled?.Invoke("playback_cancelled");
        }

        private NarrativeConfiguration config;
        private int language, reads, evaluations;

        [SetUp] public void Setup()
        {
            config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999, initValue = 1, advanceMode = NarrativeAdvanceMode.Monotonic });
            config.events.Add(new NarrativeEventDefinition { eventId = "root" });
            config.abilityChecks.Add(new NarrativeAbilityCheckDefinition
            {
                checkId = "q1b", mode = NarrativeAbilityCheckMode.Inline,
                successResultOwnerId = "owner.success", failureResultOwnerId = "owner.failure"
            });
            config.abilityCheckConditions.Add(new NarrativeAbilityCheckConditionDefinition { conditionId = "q1b.c1", checkId = "q1b", attributeId = "language", threshold = 2, groupIndex = 1 });
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "ch.b", groupKey = "q1", checkId = "q1b" });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "b.success" });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "b.failure" });
            config.results.Add(new NarrativeResultDefinition { eventId = "owner.success", resultType = NarrativeResultType.SetFlag, target = "b.success", value = "true" });
            config.results.Add(new NarrativeResultDefinition { eventId = "owner.failure", resultType = NarrativeResultType.SetFlag, target = "b.failure", value = "true" });
            language = 2; reads = 0; evaluations = 0;
        }

        private NarrativeController Controller(Presenter p)
        {
            var controller = new NarrativeController(config, p)
            {
                AbilityReader = _ => { reads++; return (true, language); },
                AbilityNameResolver = _ => "语言"
            };
            controller.AbilityCheckEvaluated += _ => evaluations++;
            return controller;
        }

        [Test] public void ChoiceReferencedInlineCheckEvaluatesOnceAndAppliesSuccessOwner()
        {
            var p = new Presenter(); var c = Controller(p);
            Assert.That(c.QueueEvent("root"), Is.True);
            Assert.Throws<InvalidOperationException>(() => c.ReadPresentationCondition("check:q1b"));
            Assert.That(c.SelectChoice("ch.b"), Is.True);
            Assert.That(evaluations, Is.EqualTo(1)); Assert.That(reads, Is.EqualTo(1));
            Assert.That(c.PendingEventIds, Is.Empty); Assert.That(c.ActiveAbilityCheck, Is.Null);
            Assert.That(c.ReadPresentationCondition("check:q1b"), Is.True);
            Assert.That(c.ReadPresentationCondition("not:check:q1b"), Is.False);
            p.Complete();
            Assert.That(c.State.GetCondition("b.success"), Is.True);
            Assert.That(c.State.GetCondition("b.failure"), Is.False);
            Assert.That(c.IsSafeBoundary);
            Assert.Throws<InvalidOperationException>(() => c.ReadPresentationCondition("check:q1b"));
        }

        [Test] public void ChoiceReferencedInlineFailureAppliesFailureOwnerOnly()
        {
            var p = new Presenter(); var c = Controller(p);
            language = 1;
            Assert.That(c.QueueEvent("root"), Is.True);
            Assert.That(c.SelectChoice("ch.b"), Is.True);
            Assert.That(c.ReadPresentationCondition("check:q1b"), Is.False);
            Assert.That(c.ReadPresentationCondition("not:check:q1b"), Is.True);
            p.Complete();
            Assert.That(c.State.GetCondition("b.success"), Is.False);
            Assert.That(c.State.GetCondition("b.failure"), Is.True);
        }

        [Test] public void PlainChoiceWithoutCheckDoesNotTouchInlineRouting()
        {
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "ch.a", groupKey = "q1" });
            var p = new Presenter(); var c = Controller(p);
            Assert.That(c.QueueEvent("root"), Is.True);
            Assert.That(c.SelectChoice("ch.a"), Is.True);
            Assert.That(reads, Is.EqualTo(0)); Assert.That(evaluations, Is.EqualTo(0));
            Assert.That(c.ReadPresentationCondition("choice:ch.a"), Is.True);
            p.Complete();
            Assert.That(c.State.GetCondition("b.success"), Is.False); Assert.That(c.State.GetCondition("b.failure"), Is.False);
        }

        [Test] public void InlineOwnerResultsJoinTheRootTransactionWithoutPartialCommit()
        {
            config.results.Add(new NarrativeResultDefinition { eventId = "owner.success", resultType = NarrativeResultType.BarAdd, target = "bar:world.calendar", value = "1", seq = 1 });
            config.results.Add(new NarrativeResultDefinition { eventId = "owner.success", resultType = NarrativeResultType.BarAdd, target = "bar:missing", value = "1", seq = 2 });
            var p = new Presenter(); var c = Controller(p);
            Assert.That(c.QueueEvent("root"), Is.True);
            Assert.That(c.SelectChoice("ch.b"), Is.True);
            LogAssert.Expect(LogType.Error, new Regex(@"\[NCS\] Event root result transaction failed: .*missing"));
            p.Complete();
            Assert.That(c.State.GetCondition("b.success"), Is.False, "Preflight failure must roll the whole batch back.");
            Assert.That(c.IsSafeBoundary, Is.False);
            Assert.That(c.State.TryGetBar("world.calendar", out int day) && day == 1, Is.True,
                "A failed preflight must not apply the valid BarAdd either.");
        }

        [Test] public void CancelClearsInlineCacheAndRetryReevaluates()
        {
            var p = new Presenter(); var c = Controller(p);
            language = 1;
            Assert.That(c.QueueEvent("root"), Is.True);
            Assert.That(c.SelectChoice("ch.b"), Is.True);
            Assert.That(c.ReadPresentationCondition("check:q1b"), Is.False);
            Action stale = p.Callbacks[p.Callbacks.Count - 1];
            p.Cancel(); stale();
            Assert.That(c.IsSafeBoundary, Is.True);
            language = 2;
            Assert.That(c.QueueEvent("root"), Is.True);
            Assert.That(c.SelectChoice("ch.b"), Is.True);
            Assert.That(c.ReadPresentationCondition("check:q1b"), Is.True);
            Assert.That(reads, Is.EqualTo(2));
            p.Complete();
            Assert.That(c.State.GetCondition("b.success"), Is.True);
        }

        [Test] public void SnapshotCarriesInlineCacheAndResumedPresentationReusesIt()
        {
            var p = new Presenter(); var c = Controller(p);
            Assert.That(c.QueueEvent("root"), Is.True);
            Assert.That(c.SelectChoice("ch.b"), Is.True);
            var first = c.EvaluateInlineAbilityCheckForPresentation("q1b");
            Assert.That(first.passed, Is.True);
            first.values[0].currentValue = -100; first.passed = false;
            NarrativeSnapshot saved = c.CaptureSnapshot();
            Assert.That(saved.inlineAbilityChecks, Has.Count.EqualTo(1));
            language = 1;
            var restored = new Presenter(); var c2 = Controller(restored);
            c2.RestoreSnapshot(saved);
            c2.EndTurn();
            Assert.That(restored.Played, Is.EqualTo(new[] { "root" }));
            var again = c2.EvaluateInlineAbilityCheckForPresentation("q1b");
            Assert.That(again.passed, Is.True, "Restored cache must be reused, not re-evaluated.");
            Assert.That(again.values[0].currentValue, Is.EqualTo(2));
            Assert.That(reads, Is.EqualTo(1), "Restore must not re-read player attributes.");
            restored.Complete();
            Assert.That(c2.State.GetCondition("b.success"), Is.True, "Restored cache collects the success owner.");
        }

        [Test] public void ValidatorAllowsChoiceOnlyInlineAndRejectsOtherConsumers()
        {
            var check = config.abilityChecks[0];
            Assert.That(LubanNarrativeLoader.ValidateAbilityCheckMode(config, check), Is.Empty);
            check.successEventId = "root";
            Assert.That(LubanNarrativeLoader.ValidateAbilityCheckMode(config, check), Has.Some.Contains("Inline forbids branch events"));
            check.successEventId = null;
            config.setups.Add(new NarrativeSetupParamsDefinition { setupKey = "s", checkId = "q1b" });
            Assert.That(LubanNarrativeLoader.ValidateAbilityCheckMode(config, check), Has.Some.Contains("Inline cannot be referenced by SetupParams"));
            config.setups.Clear();
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "pc", presentationKey = "pk", seq = 1, stepType = NarrativeConversationStepType.PassiveCheck, checkId = "q1b" });
            config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition { lineId = "l", pageId = "pc", seq = 1, textKey = "t" });
            Assert.That(LubanNarrativeLoader.ValidateAbilityCheckMode(config, check), Has.Some.Contains("Inline cannot be used by both a Choice and a PassiveCheck page"));
            check.mode = NarrativeAbilityCheckMode.Branch;
            Assert.That(LubanNarrativeLoader.ValidateAbilityCheckMode(config, check), Is.Not.Empty, "Branch mode still requires both branch events.");
        }

        [Test] public void DistinctInlineChecksMayServeAPageAndAChoice()
        {
            config.abilityChecks.Add(new NarrativeAbilityCheckDefinition { checkId = "q2", mode = NarrativeAbilityCheckMode.Inline });
            config.abilityCheckConditions.Add(new NarrativeAbilityCheckConditionDefinition { conditionId = "q2.c1", checkId = "q2", attributeId = "language", threshold = 1, groupIndex = 1 });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "pc", presentationKey = "pk", seq = 1, stepType = NarrativeConversationStepType.PassiveCheck, checkId = "q2" });
            config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition { lineId = "l", pageId = "pc", seq = 1, textKey = "t" });
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config), Is.Empty);
        }
    }
}
