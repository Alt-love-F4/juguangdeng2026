using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeAutomationPresenterTests
    {
        private NarrativeConfiguration config;
        private NarrativeAutomationPresenter presenter;
        private NarrativeController controller;

        [SetUp] public void Setup()
        {
            config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 999, initValue = 1 });
            config.bars.Add(new NarrativeBarDefinition { barKey = "skill", min = 0, max = 20, initValue = 3 });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "done" });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "reward" });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "hidden" });
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_TwoPersonTalk", atomSeq = "A_NARRATE" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "pk", presentationClass = "Conversation" });
            config.events.Add(new NarrativeEventDefinition { eventId = "root", presentationKey = "pk" });
            config.results.Add(new NarrativeResultDefinition { resultId = "done.r", eventId = "root", resultType = NarrativeResultType.SetFlag, target = "done", value = "true" });
        }
        [TearDown] public void TearDown() => presenter?.Dispose();
        private void Bind(NarrativeAutomationPlan plan = null)
        {
            presenter = new NarrativeAutomationPresenter(config, plan);
            controller = new NarrativeController(config, presenter) { AbilityReader = _ => (true, 3) };
            presenter.Bind(controller);
        }
        private void Page(string id, int seq, string guard = null, string next = null,
            NarrativeConversationStepType kind = NarrativeConversationStepType.Dialogue, string group = null, string check = null)
            => config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
            { pageId = id, presentationKey = "pk", seq = seq, pageCondKeys = guard, nextPageId = next, stepType = kind,
                optionGroupKey = group, checkId = check, gameplayKey = kind == NarrativeConversationStepType.Gameplay ? "brew" : null });
        private void Run(int max = 2000)
        {
            for (int i = 0; i < max && presenter.IsPresenting; i++)
            {
                int used = presenter.Tick(1);
                Assert.That(used, Is.LessThanOrEqualTo(1));
                if (used == 0) break;
            }
        }
        private void Inline(string id)
        {
            config.abilityChecks.Add(new NarrativeAbilityCheckDefinition { checkId = id, mode = NarrativeAbilityCheckMode.Inline });
            config.abilityCheckConditions.Add(new NarrativeAbilityCheckConditionDefinition { conditionId = id + ".c", checkId = id,
                groupIndex = 1, attributeId = "language", threshold = 4 });
        }

        [Test] public void RootQueuesUntilTickAndHiddenEdgesAndNumericGuardsUseProductionTraversal()
        {
            Page("hidden", 1, "hidden", "end");
            Page("numeric", 2, "bar:skill:ge:3");
            Page("skip", 3, "not:bar:skill:ge:3");
            Page("end", 4, kind: NarrativeConversationStepType.End);
            Bind();
            var pages = new List<string>(); presenter.PageChanged += p => pages.Add(p.PageId);
            Assert.That(controller.QueueEvent("root"), Is.True);
            Assert.That(controller.State.GetCondition("done"), Is.False);
            Assert.That(presenter.Tick(1), Is.EqualTo(1));
            Assert.That(pages, Is.Empty);
            Run();
            Assert.That(pages, Is.EqualTo(new[] { "numeric", "end" }));
            Assert.That(controller.State.GetCondition("done"), Is.True);
            Assert.That(controller.IsSafeBoundary, Is.True);
        }

        [Test] public void FailedPassiveDoesNotCommitExplicitJump()
        {
            Inline("inline");
            Page("passive", 1, next: "end", kind: NarrativeConversationStepType.PassiveCheck, check: "inline");
            Page("fallback", 2);
            Page("end", 3, kind: NarrativeConversationStepType.End);
            Bind(); var pages = new List<string>(); presenter.PageChanged += p => pages.Add(p.PageId);
            controller.QueueEvent("root"); Run();
            Assert.That(pages, Is.EqualTo(new[] { "passive", "fallback", "end" }));
            Assert.That(controller.State.GetCondition("done"), Is.True);
        }

        [Test] public void LegacyPageBarConditionsFailInsteadOfBypassingThem()
        {
            Page("unsupported", 1); config.presentations.conversationPages[0].pageBarConds = "skill>=3";
            Bind(); controller.QueueEvent("root"); Run();
            Assert.That(presenter.LastError, Does.Contain("Unsupported presentation page bar conditions"));
            Assert.That(controller.State.GetCondition("done"), Is.False);
            Assert.That(controller.IsBusy, Is.False);
        }

        [Test] public void ChoiceAndInlineResultsCommitOnlyWithRealRootCompletionAndNullDelegateFallsBack()
        {
            Inline("inline");
            config.abilityChecks[0].failureResultOwnerId = "failed.check";
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "a", groupKey = "q", seq = 1 });
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "b", groupKey = "q", seq = 2, checkId = "inline" });
            config.results.Add(new NarrativeResultDefinition { resultId = "reward.r", eventId = "failed.check", resultType = NarrativeResultType.SetFlag, target = "reward", value = "true" });
            Page("choose", 1, group: "q"); Page("after", 2, "not:check:inline"); Page("end", 3, kind: NarrativeConversationStepType.End);
            var plan = new NarrativeAutomationPlan(); plan.ChoiceOverrides["q"] = "b"; Bind(plan);
            presenter.ChoiceSelector = context => { Assert.That(context.Choices.Count, Is.EqualTo(2)); return null; };
            int evaluations = 0; controller.AbilityCheckEvaluated += _ => evaluations++;
            controller.QueueEvent("root");
            while (presenter.CurrentPage?.SelectedChoiceId != "b") presenter.Tick(1);
            Assert.That(controller.State.GetCondition("reward"), Is.False);
            Run();
            Assert.That(evaluations, Is.EqualTo(1));
            Assert.That(controller.State.GetCondition("reward"), Is.True);
            Assert.That(controller.State.GetCondition("done"), Is.True);
        }

        [Test] public void GameplayWaitsForHostAndDefersReceiptToTickAndIgnoresStaleReceipt()
        {
            Page("brew", 1, kind: NarrativeConversationStepType.Gameplay);
            Page("passed", 2, "gameplay:brew:passed"); Page("end", 3, kind: NarrativeConversationStepType.End);
            Bind(); NarrativeAutomationGameplayRequest pending = null; presenter.GameplayRequested += p => pending = p;
            controller.QueueEvent("root"); Run();
            Assert.That(pending, Is.Not.Null); Assert.That(controller.IsBusy, Is.True);
            Assert.That(presenter.Tick(100), Is.Zero); Assert.That(controller.State.GetCondition("done"), Is.False);
            pending.Complete(new NarrativeGameplayResult { passed = true });
            Assert.That(controller.State.GetCondition("done"), Is.False);
            presenter.Tick(1); Run();
            Assert.That(controller.State.GetCondition("done"), Is.True);
            pending.Complete(new NarrativeGameplayResult { cancelled = true });
            Assert.That(controller.State.GetCondition("done"), Is.True);
        }

        [Test] public void GameplayCancellationDoesNotSetTerminalFactsOrCompleteRoot()
        {
            Page("brew", 1, kind: NarrativeConversationStepType.Gameplay); Bind();
            NarrativeAutomationGameplayRequest pending = null; presenter.GameplayRequested += p => pending = p;
            controller.QueueEvent("root"); Run(); presenter.Cancel();
            pending.Complete(new NarrativeGameplayResult { passed = true }); Run();
            Assert.That(controller.State.GetCondition("done"), Is.False);
            Assert.That(controller.IsSafeBoundary, Is.True);
        }

        [Test] public void SynchronousHostReceiptCannotRecursivelyFinishRoot()
        {
            Page("brew", 1, kind: NarrativeConversationStepType.Gameplay); Bind();
            presenter.GameplayRequested += p =>
            {
                p.Complete(new NarrativeGameplayResult { passed = true });
                Assert.That(presenter.Tick(1000), Is.Zero, "Reentrant Tick must be inert.");
                Assert.That(controller.State.GetCondition("done"), Is.False);
            };
            controller.QueueEvent("root"); Run();
            Assert.That(controller.State.GetCondition("done"), Is.True);
        }

        [Test] public void ReadingNoticeAndLetterStepsFinishWithoutUiAndEffectDrainRemainsBudgeted()
        {
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_Narration", atomSeq = "A_NARRATE" });
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_WorldBroadcast", atomSeq = "A_NARRATE" });
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "effect", atomSeq = "A_WAIT A_FADE" });
            config.setups.Add(new NarrativeSetupParamsDefinition { setupKey = "effect", tplName = "effect" });
            config.presentations.readingPages.Add(new NarrativeReadingPageDefinition { pageId = "letter", presentationKey = "letter.pk" });
            Page("reveal", 1, kind: NarrativeConversationStepType.LetterReveal);
            config.presentations.conversationPages[0].readingKey = "letter.pk";
            Page("read", 2, kind: NarrativeConversationStepType.LetterRead);
            config.presentations.conversationPages[1].readingKey = "letter.pk";
            Page("end", 3, kind: NarrativeConversationStepType.End);
            config.results.Add(new NarrativeResultDefinition { resultId = "effect.r", eventId = "root", resultType = NarrativeResultType.PlaySetup, target = "effect" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "reading", presentationClass = "Reading" });
            config.presentations.readingPages.Add(new NarrativeReadingPageDefinition { pageId = "reading.page", presentationKey = "reading" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "notice", presentationClass = "Notice" });
            config.presentations.notices.Add(new NarrativeNoticeDefinition { presentationKey = "notice" });
            config.events.Add(new NarrativeEventDefinition { eventId = "reading", presentationKey = "reading" });
            config.events.Add(new NarrativeEventDefinition { eventId = "notice", presentationKey = "notice" });
            Bind(); controller.QueueEvent("root"); Run();
            Assert.That(controller.IsSafeBoundary, Is.True); Assert.That(presenter.LastError, Is.Null);
            controller.QueueEvent("reading"); Run(); controller.QueueEvent("notice"); Run();
            Assert.That(controller.State.HasHappenedEvent("reading"), Is.True);
            Assert.That(controller.State.HasHappenedEvent("notice"), Is.True);
        }

        [Test] public void ActiveBranchCheckUsesControllerBranchQueueAndAppliesRootThenBranch()
        {
            config.abilityChecks.Add(new NarrativeAbilityCheckDefinition { checkId = "branch", mode = NarrativeAbilityCheckMode.Branch,
                successEventId = "success", failureEventId = "failure" });
            config.abilityCheckConditions.Add(new NarrativeAbilityCheckConditionDefinition { conditionId = "branch.c", checkId = "branch",
                groupIndex = 1, attributeId = "language", threshold = 4 });
            config.setups.Add(new NarrativeSetupParamsDefinition { setupKey = "root.setup", tplName = "Tpl_TwoPersonTalk", checkId = "branch" });
            config.events[0].setupKey = "root.setup";
            config.events.Add(new NarrativeEventDefinition { eventId = "success" });
            config.events.Add(new NarrativeEventDefinition { eventId = "failure" });
            config.results.Add(new NarrativeResultDefinition { resultId = "failure.reward", eventId = "failure", resultType = NarrativeResultType.SetFlag, target = "reward", value = "true" });
            Page("end", 1, kind: NarrativeConversationStepType.End);
            Bind(); var completed = new List<string>(); controller.EventCompleted += id => completed.Add(id);
            controller.QueueEvent("root"); Run();
            Assert.That(completed, Is.EqualTo(new[] { "root", "failure" }));
            Assert.That(controller.State.GetCondition("done"), Is.True);
            Assert.That(controller.State.GetCondition("reward"), Is.True);
            Assert.That(presenter.LastError, Is.Null);
        }

        [Test] public void LockedExplicitChoiceAndGraphLoopFailWithoutRootCommit()
        {
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "locked", groupKey = "q" });
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "open", groupKey = "q" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "lock", ownerType = "choice", ownerId = "locked",
                groupIndex = 1, condType = "BOOL_EQ", target = "hidden", value = "true" });
            Page("choose", 1, group: "q");
            var plan = new NarrativeAutomationPlan(); plan.ChoiceOverrides["q"] = "locked"; Bind(plan);
            controller.QueueEvent("root"); Run();
            Assert.That(presenter.LastError, Does.Contain("locked or absent"));
            Assert.That(controller.State.GetCondition("done"), Is.False);
            presenter.Dispose();
            config.presentations.conversationPages.Clear(); Page("loop", 1, next: "loop");
            Bind(); controller.QueueEvent("root"); Run();
            Assert.That(presenter.LastError, Does.Contain("traversal limit"));
            Assert.That(controller.State.GetCondition("done"), Is.False);
        }

        [Test] public void HugeLinearConversationIsBoundedByTickAndProductionCursor()
        {
            for (int i = 0; i < 1500; i++) Page("p" + i, i);
            Bind(); controller.QueueEvent("root"); presenter.Tick(4);
            Assert.That(controller.State.GetCondition("done"), Is.False);
            Run(10000); Assert.That(controller.State.GetCondition("done"), Is.True);
        }
    }
}
