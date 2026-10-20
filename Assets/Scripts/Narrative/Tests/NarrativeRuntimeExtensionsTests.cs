using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeRuntimeExtensionsTests
    {
        private sealed class Presenter : INarrativePresenter, INarrativePresentationCancellation, INarrativeGameplayResultSource
        {
            public event Action<string> Cancelled;
            public event Action<string, bool> GameplayCompleted;
            public Action Complete;
            public void Play(NarrativeEventRequest request, Action completed) { Complete = completed; }
            public void Cancel() => Cancelled?.Invoke("playback_cancelled");
            public void Gameplay(string page, bool passed) => GameplayCompleted?.Invoke(page, passed);
        }
        private static NarrativeConfiguration Config()
        {
            var c = new NarrativeConfiguration();
            c.events.Add(new NarrativeEventDefinition { eventId = "root", presentationKey = "story" });
            c.bars.Add(new NarrativeBarDefinition { barKey = "player.language", min = 0, max = 20, initValue = 3 });
            c.bars.Add(new NarrativeBarDefinition { barKey = "training.language", min = 0, max = 9 });
            c.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "earned", nameText = "earned.name" });
            c.texts.Add(new NarrativeTextDefinition { key = "earned.name", content = "Earned" });
            return c;
        }
        private static NarrativeConversationPageDefinition Page(string id, int seq, NarrativeConversationStepType kind = NarrativeConversationStepType.Dialogue, string guard = null, string next = null) =>
            new NarrativeConversationPageDefinition { pageId = id, presentationKey = "story", seq = seq, stepType = kind, pageCondKeys = guard, nextPageId = next, gameplayKey = kind == NarrativeConversationStepType.Gameplay ? "serve:black_coffee:60:1" : null };
        private static NarrativeResultDefinition Train(string value) => new NarrativeResultDefinition { resultId = "train", eventId = "root", resultType = NarrativeResultType.TrainAttribute, target = "language", value = value };
        private static int Bar(NarrativeController c, string key) { Assert.That(c.State.TryGetBar(key, out int value)); return value; }

        [Test] public void PresentationCancellationRequestArgumentIsOptionalAndReadonly()
        {
            var legacy = new NarrativeEventRequest(new NarrativeEventDefinition(), null, 0);
            Assert.That(legacy.PresentationCancelled, Is.Null);
            bool cancelled = false;
            var request = new NarrativeEventRequest(new NarrativeEventDefinition(), null, 0, presentationCancelled: () => cancelled = true);
            request.PresentationCancelled(); Assert.That(cancelled);
            Assert.That(typeof(NarrativeEventRequest).GetField("PresentationCancelled").IsInitOnly);
        }
        [TestCase("ge", 3, true)] [TestCase("ge", 4, false)]
        [TestCase("gt", 2, true)] [TestCase("gt", 3, false)]
        [TestCase("le", 3, true)] [TestCase("le", 2, false)]
        [TestCase("lt", 4, true)] [TestCase("lt", 3, false)]
        [TestCase("eq", 3, true)] [TestCase("eq", 4, false)]
        [TestCase("ne", 4, true)] [TestCase("ne", 3, false)]
        public void NumericGuardsSupportAllOperators(string op, int bound, bool expected)
        {
            var c = new NarrativeController(Config());
            Assert.That(c.ReadPresentationCondition("bar:player.language:" + op + ":" + bound), Is.EqualTo(expected));
            Assert.That(c.ReadPresentationCondition("not:bar:player.language:" + op + ":" + bound), Is.EqualTo(!expected));
        }
        [Test] public void MissingNumericProviderFailsClosedEvenUnderNotOrExpectedFalse()
        {
            var config = Config();
            config.bars.Add(new NarrativeBarDefinition { barKey = "outside", sourceMode = NarrativeSourceMode.External, providerId = "missing", sourceKey = "value" });
            var c = new NarrativeController(config);
            LogAssert.Expect(LogType.Warning, "[NCS] Presentation dependency unavailable; dependent page will be skipped: bar:outside:ge:0");
            Assert.That(c.ReadPresentationCondition("bar:outside:ge:0"), Is.False);
            Assert.That(c.ReadPresentationCondition("not:bar:outside:ge:0"), Is.False);
            Assert.That(c.EvaluateCondition(new NarrativeConditionDefinition { ownerType = "result", condType = "BOOL_EQ", target = "bar:outside:ge:0", value = "false" }), Is.False);
        }
        [Test] public void ConditionalResultsFreezeChoiceAndBarEvidenceBeforeAnyWrites()
        {
            var config = Config();
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "yes", groupKey = "choice" });
            config.results.Add(new NarrativeResultDefinition { resultId = "increase", eventId = "root", resultType = NarrativeResultType.BarSet, target = "player.language", value = "10" });
            config.results.Add(new NarrativeResultDefinition { resultId = "reward", eventId = "root", resultType = NarrativeResultType.SetFlag, target = "earned", value = "true", seq = 1 });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "selected", ownerType = "result", ownerId = "reward", groupIndex = 1, condType = "BOOL_EQ", target = "choice:yes", value = "true" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "oldBar", ownerType = "result", ownerId = "reward", groupIndex = 1, condType = "BOOL_EQ", target = "bar:player.language:eq:3", value = "true" });
            var p = new Presenter(); var c = new NarrativeController(config, p);
            Assert.That(c.QueueEvent("root")); Assert.That(c.SelectChoice("yes")); p.Complete();
            Assert.That(c.State.GetCondition("earned")); Assert.That(Bar(c, "player.language"), Is.EqualTo(10));
            Assert.That(c.SelectedChoices, Is.Empty);
        }
        [TestCase(true)] [TestCase(false)] public void GameplayGuardsAndConditionalRootResultsUseRealPublishedOutcome(bool passed)
        {
            var config = Config(); config.presentations.conversationPages.Add(Page("brew", 1, NarrativeConversationStepType.Gameplay));
            config.results.Add(new NarrativeResultDefinition { resultId = "reward", eventId = "root", resultType = NarrativeResultType.SetFlag, target = "earned", value = "true" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "brewPassed", ownerType = "result", ownerId = "reward", groupIndex = 1, condType = "BOOL_EQ", target = "gameplay:brew:passed", value = "true" });
            var p = new Presenter(); var c = new NarrativeController(config, p); Assert.That(c.QueueEvent("root"));
            p.Gameplay("brew", passed);
            Assert.That(c.ReadPresentationCondition("gameplay:brew:passed"), Is.EqualTo(passed));
            Assert.That(c.ReadPresentationCondition("not:gameplay:brew:passed"), Is.EqualTo(!passed));
            p.Complete(); Assert.That(c.State.GetCondition("earned"), Is.EqualTo(passed));
            Assert.That(c.CaptureSnapshot().gameplayResults, Is.Empty);
        }
        [Test] public void GameplaySnapshotsStayLocalAndCancellationDoesNotCommit()
        {
            var config = Config(); config.presentations.conversationPages.Add(Page("brew", 1, NarrativeConversationStepType.Gameplay));
            var p = new Presenter(); var c = new NarrativeController(config, p); Assert.That(c.QueueEvent("root"));
            p.Gameplay("brew", true); var saved = c.CaptureSnapshot(); Assert.That(saved.gameplayResults.Count, Is.EqualTo(1));
            p.Cancel(); Assert.That(c.State.HasHappenedEvent("root"), Is.False);
            Assert.Throws<InvalidOperationException>(() => p.Gameplay("brew", true));
            var restoredPresenter = new Presenter(); var restored = new NarrativeController(config, restoredPresenter);
            restored.RestoreSnapshot(saved); restored.ResumePendingEvents();
            Assert.That(restored.ReadPresentationCondition("gameplay:brew:passed"));
            restoredPresenter.Complete(); Assert.That(restored.CaptureSnapshot().gameplayResults, Is.Empty);
        }
        [TestCase(0, 3, 2, 0)]
        [TestCase(9, 3, 2, 1)]
        [TestCase(9, 19, 1, 1)]
        [TestCase(0, 20, 0, 0)]
        public void TrainingNotifiesOnlyCommittedActualReward(int remainder, int current, int earned, int increase)
        {
            var config = Config(); config.bars[0].initValue = current; config.bars[1].initValue = remainder;
            config.results.Add(Train("2"));
            var p = new Presenter(); var c = new NarrativeController(config, p);
            var rewards = new List<(string attribute, int experience, int increase)>();
            c.TrainingCompleted += (a, xp, points) => rewards.Add((a, xp, points));
            Assert.That(c.QueueEvent("root")); Assert.That(rewards, Is.Empty);
            p.Complete();
            if (earned == 0) Assert.That(rewards, Is.Empty);
            else Assert.That(rewards, Is.EqualTo(new[] { ("language", earned, increase) }));
        }
        [Test] public void CancelledTrainingDoesNotNotify()
        {
            var config = Config(); config.results.Add(Train("2"));
            var p = new Presenter(); var c = new NarrativeController(config, p);
            int notifications = 0; c.TrainingCompleted += (_, __, ___) => notifications++;
            Assert.That(c.QueueEvent("root")); p.Cancel();
            Assert.That(notifications, Is.Zero); Assert.That(Bar(c, "training.language"), Is.Zero);
        }
        [Test] public void TrainingAccumulatesTenthsInOneCentralAttributeAndPersists()
        {
            var config = Config(); config.results.Add(Train("7"));
            config.results.Add(new NarrativeResultDefinition { resultId = "train2", eventId = "root", resultType = NarrativeResultType.TrainAttribute, target = "player.language", value = "6", seq = 1 });
            var p = new Presenter(); var c = new NarrativeController(config, p); Assert.That(c.QueueEvent("root")); p.Complete();
            Assert.That(Bar(c, "player.language"), Is.EqualTo(4)); Assert.That(Bar(c, "training.language"), Is.EqualTo(3));
            var restored = new NarrativeController(config, new Presenter()); restored.RestoreSnapshot(c.CaptureSnapshot());
            Assert.That(Bar(restored, "player.language"), Is.EqualTo(4)); Assert.That(Bar(restored, "training.language"), Is.EqualTo(3));
        }
        [Test] public void DailyTrainingThresholdGuardsAreFrozenBeforeCrossingTen()
        {
            var config = Config(); config.bars[0].initValue = 9; config.bars[1].initValue = 9;
            var low = Train("2"); low.target = "player.language"; config.results.Add(low);
            config.results.Add(new NarrativeResultDefinition { resultId = "high", eventId = "root", resultType = NarrativeResultType.TrainAttribute, target = "player.language", value = "1", seq = 1 });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "lowGuard", ownerType = "result", ownerId = "train", groupIndex = 1, condType = "BOOL_EQ", target = "bar:player.language:lt:10", value = "true" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "highGuard", ownerType = "result", ownerId = "high", groupIndex = 1, condType = "BOOL_EQ", target = "bar:player.language:ge:10", value = "true" });
            var p = new Presenter(); var c = new NarrativeController(config, p); Assert.That(c.QueueEvent("root")); p.Complete();
            Assert.That(Bar(c, "player.language"), Is.EqualTo(10)); Assert.That(Bar(c, "training.language"), Is.EqualTo(1));
        }
        [Test] public void TrainingSeesEarlierBarWritesAndCapsAtTwentyDiscardingExcess()
        {
            var config = Config(); config.results.Add(new NarrativeResultDefinition { resultId = "base", eventId = "root", resultType = NarrativeResultType.BarSet, target = "player.language", value = "19" });
            var training = Train(int.MaxValue.ToString()); training.seq = 1; config.results.Add(training);
            var p = new Presenter(); var c = new NarrativeController(config, p); Assert.That(c.QueueEvent("root")); p.Complete();
            Assert.That(Bar(c, "player.language"), Is.EqualTo(20)); Assert.That(Bar(c, "training.language"), Is.Zero);
        }
        [TestCase("0")] [TestCase("-1")] [TestCase("0.2")] [TestCase("2147483648")]
        public void InvalidTrainingRollsBackWholeRootPreflight(string reward)
        {
            var config = Config(); config.results.Add(Train("9"));
            config.results.Add(new NarrativeResultDefinition { resultId = "bad", eventId = "root", resultType = NarrativeResultType.TrainAttribute, target = "language", value = reward, seq = 1 });
            config.results.Add(new NarrativeResultDefinition { resultId = "flag", eventId = "root", resultType = NarrativeResultType.SetFlag, target = "earned", value = "true", seq = 2 });
            var p = new Presenter(); var c = new NarrativeController(config, p); Assert.That(c.QueueEvent("root"));
            int notifications = 0; c.TrainingCompleted += (_, __, ___) => notifications++;
            LogAssert.Expect(LogType.Error, new Regex("result transaction failed: .*positive integer XP")); p.Complete();
            Assert.That(notifications, Is.Zero);
            Assert.That(Bar(c, "player.language"), Is.EqualTo(3)); Assert.That(Bar(c, "training.language"), Is.Zero);
            Assert.That(c.State.GetCondition("earned"), Is.False); Assert.That(c.State.HasHappenedEvent("root"), Is.False);
        }
        [Test] public void MissingTrainingBarRejectsBatchBeforeOrdinaryWrites()
        {
            var config = Config(); config.bars.RemoveAt(1);
            config.results.Add(new NarrativeResultDefinition { resultId = "first", eventId = "root", resultType = NarrativeResultType.BarSet, target = "player.language", value = "9" });
            var training = Train("2"); training.seq = 1; config.results.Add(training);
            var p = new Presenter(); var c = new NarrativeController(config, p); Assert.That(c.QueueEvent("root"));
            LogAssert.Expect(LogType.Error, new Regex("result transaction failed: .*training.language")); p.Complete();
            Assert.That(Bar(c, "player.language"), Is.EqualTo(3)); Assert.That(c.State.HasHappenedEvent("root"), Is.False);
        }
        [Test] public void FalseConditionalRewardIsFilteredBeforePreflight()
        {
            var config = Config(); config.results.Add(Train("0"));
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "skip", ownerType = "result", ownerId = "train", groupIndex = 1, condType = "BOOL_EQ", target = "bar:player.language:ge:10", value = "true" });
            var p = new Presenter(); var c = new NarrativeController(config, p); Assert.That(c.QueueEvent("root")); p.Complete();
            Assert.That(Bar(c, "player.language"), Is.EqualTo(3)); Assert.That(Bar(c, "training.language"), Is.Zero);
            Assert.That(c.State.HasHappenedEvent("root"));
            // Invalid authored rewards are still rejected at configuration-validation time.
            Assert.That(LubanNarrativeLoader.ValidateConditionConfiguration(config).Any(e => e.Contains("positive integer XP")));
        }
        [Test] public void ConditionalGuardDiagnosticsReadTransientPresentationEvidence()
        {
            var config = Config(); config.results.Add(Train("2"));
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "guard", ownerType = "result", ownerId = "train", groupIndex = 1, condType = "BOOL_EQ", target = "bar:player.language:lt:10", value = "true" });
            var c = new NarrativeController(config, new Presenter()); Assert.That(c.QueueEvent("root"));
            var diagnostics = c.ExplainConditions("result", "train");
            Assert.That(diagnostics.Count, Is.EqualTo(1)); Assert.That(diagnostics[0].Passed);
            Assert.That(diagnostics[0].Current, Is.EqualTo("True")); Assert.That(diagnostics[0].Error, Is.Null);
        }
        [Test] public void ActionBoolGuardsReadCurrentBarsAndNegatedWorldFlagsWithoutTransientState()
        {
            var config = Config(); config.actions.Add(new NarrativeActionDefinition { actionKey = "daily" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "lock", ownerType = "action", ownerId = "daily", groupIndex = 1, condType = "BOOL_EQ", target = "not:bar:player.language:ge:10", value = "true" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "flag", ownerType = "action", ownerId = "daily", groupIndex = 1, condType = "BOOL_EQ", target = "not:earned", value = "true" });
            Assert.That(LubanNarrativeLoader.ValidateConditionConfiguration(config), Is.Empty);
            var c = new NarrativeController(config, new Presenter());
            Assert.That(c.EvaluateConditions("action", "daily"));
            Assert.That(c.ExplainConditions("action", "daily").All(d => d.Passed && d.Current == "True" && d.Error == null));
            Assert.That(c.SetBar("player.language", 10)); Assert.That(c.EvaluateConditions("action", "daily"), Is.False);
            Assert.That(c.SetBar("player.language", 9)); c.State.SetCondition("earned", true);
            Assert.That(c.EvaluateConditions("action", "daily"), Is.False);
        }
        [Test] public void ActionNumericGuardsFailClosedForUnavailableProviderEvenExpectedFalse()
        {
            var config = Config(); config.actions.Add(new NarrativeActionDefinition { actionKey = "daily" });
            config.bars.Add(new NarrativeBarDefinition { barKey = "outside", sourceMode = NarrativeSourceMode.External, providerId = "missing", sourceKey = "value" });
            var condition = new NarrativeConditionDefinition { conditionId = "lock", ownerType = "action", ownerId = "daily", groupIndex = 1, condType = "BOOL_EQ", target = "not:bar:outside:ge:10", value = "false" };
            config.conditions.Add(condition); Assert.That(LubanNarrativeLoader.ValidateConditionConfiguration(config), Is.Empty);
            var c = new NarrativeController(config, new Presenter());
            LogAssert.Expect(LogType.Warning, "[NCS] Presentation dependency unavailable; dependent page will be skipped: bar:outside:ge:10");
            Assert.That(c.EvaluateCondition(condition), Is.False);
            condition.value = "true"; Assert.That(c.EvaluateCondition(condition), Is.False);
        }
        [TestCase("choice:yes")] [TestCase("not:anyChoice:yes;no")]
        [TestCase("check:check")] [TestCase("not:gameplay:brew:passed")]
        [TestCase("dependency:arbitrary")]
        public void ActionGuardsRejectRootTransientExpressions(string target)
        {
            var config = Config(); config.actions.Add(new NarrativeActionDefinition { actionKey = "daily" });
            var condition = new NarrativeConditionDefinition { conditionId = "bad", ownerType = "action", ownerId = "daily", groupIndex = 1, condType = "BOOL_EQ", target = target, value = "true" };
            config.conditions.Add(condition);
            Assert.That(LubanNarrativeLoader.ValidateConditionConfiguration(config).Any(e => e.Contains("read-only")));
            var c = new NarrativeController(config, new Presenter()); Assert.Throws<InvalidOperationException>(() => c.EvaluateCondition(condition));
        }
        [Test] public void PageWorldBoolReadsAreLiveButResultBoolGuardsFreezeBeforeAnyMutation()
        {
            var config = Config(); var cursor = new NarrativeConversationTraversal(new List<NarrativeConversationPageDefinition> { Page("before", 1, guard: "earned"), Page("hidden", 2, guard: "earned"), Page("after", 3, guard: "not:earned") });
            config.results.Add(new NarrativeResultDefinition { resultId = "clear", eventId = "root", resultType = NarrativeResultType.SetFlag, target = "earned", value = "false" });
            config.results.Add(new NarrativeResultDefinition { resultId = "reward", eventId = "root", resultType = NarrativeResultType.BarAdd, target = "player.language", value = "1", seq = 1 });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "earnedGuard", ownerType = "result", ownerId = "reward", groupIndex = 1, condType = "BOOL_EQ", target = "earned", value = "true" });
            var p = new Presenter(); var c = new NarrativeController(config, p); Assert.That(c.QueueEvent("root"));
            c.State.SetCondition("earned", true); Assert.That(cursor.MoveNext(page => c.ReadPresentationCondition(page.pageCondKeys))); Assert.That(cursor.Current.pageId, Is.EqualTo("before"));
            c.State.SetCondition("earned", false); Assert.That(cursor.MoveNext(page => c.ReadPresentationCondition(page.pageCondKeys))); Assert.That(cursor.Current.pageId, Is.EqualTo("after"));
            c.State.SetCondition("earned", true); p.Complete();
            Assert.That(c.State.GetCondition("earned"), Is.False); Assert.That(Bar(c, "player.language"), Is.EqualTo(4));
        }
        [TestCase("player.language", "external")] [TestCase("player.language", "derived")]
        [TestCase("training.language", "external")] [TestCase("training.language", "derived")]
        public void TrainingRejectsReadOnlyBarsBeforeAnyFlagOrAttributeWrites(string key, string mode)
        {
            var config = Config(); var bar = config.bars.Find(b => b.barKey == key);
            if (mode == "external") { bar.sourceMode = NarrativeSourceMode.External; bar.providerId = "owner"; bar.sourceKey = "readOnly"; }
            else
            {
                config.bars.Add(new NarrativeBarDefinition { barKey = "seed", min = 0, max = 20, initValue = 3 });
                bar.advanceMode = NarrativeAdvanceMode.Derived; bar.derivedSources.Add("seed");
            }
            config.results.Add(new NarrativeResultDefinition { resultId = "flag", eventId = "root", resultType = NarrativeResultType.SetFlag, target = "earned", value = "true" });
            var training = Train("2"); training.target = "player.language"; training.seq = 1; config.results.Add(training);
            Assert.That(LubanNarrativeLoader.ValidateTrainingResult(config, training), Is.Not.Null);
            var p = new Presenter(); var c = new NarrativeController(config, p);
            if (mode == "external")
            {
                c.BeginValueRestore(); c.BindValueProvider(new DelegateNarrativeValueProvider("owner", _ => (int?)3)); c.CompleteValueRestore(false);
            }
            int beforePlayer = Bar(c, "player.language"), beforeTraining = Bar(c, "training.language");
            Assert.That(c.QueueEvent("root")); LogAssert.Expect(LogType.Error, new Regex("result transaction failed: .*TRAIN_ATTRIBUTE requires")); p.Complete();
            Assert.That(Bar(c, "player.language"), Is.EqualTo(beforePlayer)); Assert.That(Bar(c, "training.language"), Is.EqualTo(beforeTraining));
            Assert.That(c.State.GetCondition("earned"), Is.False); Assert.That(c.State.HasHappenedEvent("root"), Is.False);
        }
        [TestCase(5001)] [TestCase(20001)]
        public void LargeLinearAndExplicitEdgePresentationsHaveEnoughBoundedBudget(int count)
        {
            var pages = new List<NarrativeConversationPageDefinition>(count);
            for (int i = 0; i < count; i++) pages.Add(Page("p" + i, i + 1, i == count - 1 ? NarrativeConversationStepType.End : NarrativeConversationStepType.Dialogue, next: i == count - 1 ? null : "p" + (i + 1)));
            var cursor = new NarrativeConversationTraversal(pages); int examined = 0;
            while (cursor.MoveNext(_ => true)) examined++;
            Assert.That(examined, Is.EqualTo(count));
            foreach (var page in pages) page.nextPageId = null;
            cursor = new NarrativeConversationTraversal(pages); examined = 0;
            while (cursor.MoveNext(_ => true)) examined++;
            Assert.That(examined, Is.EqualTo(count));
        }
        [TestCase(true)] [TestCase(false)] public void EndIsolatesShortAndLongBranches(bool shortPath)
        {
            var pages = new List<NarrativeConversationPageDefinition> { Page("start", 1), Page("short", 2, guard: "short"), Page("shortEnd", 3, NarrativeConversationStepType.End, "short"), Page("long", 4), Page("longEnd", 5, NarrativeConversationStepType.End), Page("unreachable", 6) };
            var cursor = new NarrativeConversationTraversal(pages); var visited = new List<string>();
            while (cursor.MoveNext(p => p.pageCondKeys != "short" || shortPath)) visited.Add(cursor.Current.pageId);
            Assert.That(visited, Is.EqualTo(shortPath ? new[] { "start", "short", "shortEnd" } : new[] { "start", "long", "longEnd" }));
        }
        [Test] public void ExplicitGraphEdgeSkipsOtherBranchButHiddenPageFallsThroughLinearly()
        {
            var pages = new List<NarrativeConversationPageDefinition> { Page("hidden", 1, guard: "hidden", next: "end"), Page("first", 2, next: "end"), Page("wrong", 3), Page("end", 4, NarrativeConversationStepType.End) };
            var cursor = new NarrativeConversationTraversal(pages); var visited = new List<string>();
            while (cursor.MoveNext(p => p.pageCondKeys != "hidden")) visited.Add(cursor.Current.pageId);
            Assert.That(visited, Is.EqualTo(new[] { "first", "end" }));
        }
        [Test] public void ConversationGraphLoopsFailExplicitlyInsteadOfHanging()
        {
            var cursor = new NarrativeConversationTraversal(new List<NarrativeConversationPageDefinition> { Page("loop", 1, next: "loop") });
            Assert.Throws<InvalidOperationException>(() => { for (int i = 0; i < 129; i++) cursor.MoveNext(_ => true); });
        }
        [Test] public void ValidatorsAcceptNewKindsAndResultGuardsButRejectBadEdgesAndRewards()
        {
            var c = Config(); c.presentations.conversationPages.Add(Page("brew", 1, NarrativeConversationStepType.Gameplay, next: "end")); c.presentations.conversationPages.Add(Page("end", 2, NarrativeConversationStepType.End));
            c.results.Add(Train("2"));
            c.conditions.Add(new NarrativeConditionDefinition { conditionId = "condition", ownerType = "result", ownerId = "train", groupIndex = 1, condType = "BOOL_EQ", target = "gameplay:brew:passed", value = "true" });
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(c), Is.Empty);
            Assert.That(LubanNarrativeLoader.ValidateConditionConfiguration(c), Is.Empty);
            Assert.That(LubanNarrativeLoader.ParseConversationStepType("End"), Is.EqualTo(NarrativeConversationStepType.End));
            Assert.That(LubanNarrativeLoader.ParseConversationStepType("Gameplay"), Is.EqualTo(NarrativeConversationStepType.Gameplay));
            c.presentations.conversationPages[0].nextPageId = "missing";
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(c).Any(e => e.Contains("nextPageId")));
            c.results[0].value = "0"; Assert.That(LubanNarrativeLoader.ValidateConditionConfiguration(c).Any(e => e.Contains("positive integer XP")));
            Assert.That(NarrativePresentationGuards.Validate(c, "bar:player.language:ge:3"), Is.Null);
            Assert.That(NarrativePresentationGuards.Validate(c, "bar:player.language:bad:3"), Is.Not.Null);
            Assert.That(NarrativePresentationGuards.TryParseGameplay("gameplay:passed", out _), Is.False);
        }
    }
}
