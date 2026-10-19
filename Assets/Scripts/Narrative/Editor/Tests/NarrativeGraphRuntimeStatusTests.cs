using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.Narrative.Editor.Tests
{
    /// <summary>Pure configuration/controller tests. No scene, runtime singleton, default backend or UI.</summary>
    public sealed class NarrativeGraphRuntimeStatusTests
    {
        private sealed class HoldingPresenter : INarrativePresenter
        {
            public int Calls;
            public void Play(NarrativeEventRequest request, Action completed) { Calls++; }
        }

        private static NarrativeConfiguration Config()
        {
            var c = new NarrativeConfiguration();
            c.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 30, initValue = 1 });
            c.bars.Add(new NarrativeBarDefinition { barKey = "player.wisdom", min = 0, max = 10, initValue = 3 });
            c.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready", initialValue = true });
            c.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "construction.pending" });
            c.events.Add(new NarrativeEventDefinition { eventId = "once", onceOnly = true, presentationKey = "story" });
            c.events.Add(new NarrativeEventDefinition { eventId = "repeat", presentationKey = "story" });
            c.events.Add(new NarrativeEventDefinition { eventId = "map", entry = NarrativeEntryPoint.MapAction });
            c.events.Add(new NarrativeEventDefinition { eventId = "phase", entry = NarrativeEntryPoint.AfterBusiness });
            c.events.Add(new NarrativeEventDefinition { eventId = "blocked", eventProperties = new List<NarrativeEventProperty>
                { new NarrativeEventProperty { property = "blockedBy", value = "construction.pending" } } });
            c.events.Add(new NarrativeEventDefinition { eventId = "conditional", conditionOwnerId = "shared" });
            c.conditions.Add(Condition("event", "shared", "ready", "true"));
            c.endings.Add(new NarrativeEndingDefinition { endingId = "end", enabled = true });
            c.endings.Add(new NarrativeEndingDefinition { endingId = "disabled", enabled = false });
            c.conditions.Add(Condition("ending", "end", "ready", "true"));
            c.conditions.Add(Condition("ending", "disabled", "ready", "true"));
            c.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "yes", presentationKey = "story", pageCondKeys = "ready" });
            c.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "no", presentationKey = "story", pageCondKeys = "not:ready" });
            return c;
        }

        private static NarrativeConditionDefinition Condition(string type, string owner, string target, string required, int group = 0)
            => new NarrativeConditionDefinition { conditionId = owner + "." + target + "." + group, ownerType = type,
                ownerId = owner, condType = "BOOL_EQ", target = target, value = required, groupIndex = group };

        private static StoryGraphNode Node(string key, StoryNodeKind kind = StoryNodeKind.Event)
            => new StoryGraphNode { Id = kind + ":" + key, Key = key, Kind = kind };

        private static NarrativeController Controller(NarrativeConfiguration config) => new NarrativeController(config, new HoldingPresenter());
        private static NarrativeGraphRuntimeStatus Reader(NarrativeConfiguration config, NarrativeController controller)
        {
            var reader = new NarrativeGraphRuntimeStatus(config);
            reader.Refresh(controller);
            return reader;
        }

        [Test]
        public void NullSessionIsExplicitAndStillShowsStaticBlockersAndUnavailableConditionRows()
        {
            var c = Config();
            var reader = Reader(c, null);
            Assert.That(reader.Label(Node("once")), Is.EqualTo("无会话"));
            Assert.That(reader.Details(Node("conditional")), Does.Contain("ready").And.Contain("当前=不可用").And.Contain("无会话"));
            Assert.That(reader.Label(Node("blocked")), Does.StartWith("建设阻塞：construction.pending"));
            Assert.That(reader.Label(Node("disabled", StoryNodeKind.Ending)), Is.EqualTo("禁用"));
            Assert.That(reader.Label(null), Does.Contain("未知"));
            Assert.That(reader.PageStatus("once", "yes"), Does.Contain("无会话").And.Contain("无逐页历史"));
        }

        [TestCase("once")]
        [TestCase("map")]
        public void EligibleMeansRequestableAtTheActualEntryNotAlreadyPlayed(string id)
        {
            var c = Config(); var controller = Controller(c); var reader = Reader(c, controller);
            Assert.That(reader.Label(Node(id)), Does.StartWith("当前入口可请求"));
            Assert.That(controller.State.HasHappenedEvent(id), Is.False);
            Assert.That(controller.CurrentEventId, Is.Null);
            if (id == "map") Assert.That(controller.ExplainEvent(id).CanRequest, Is.False, "Generic entry is not MapAction.");
        }

        [Test]
        public void EntryDateConditionAndLockRejectionsKeepTheirExactReason()
        {
            var c = Config();
            c.events.Add(new NarrativeEventDefinition { eventId = "dated" });
            c.eventDates.Add(new NarrativeEventDateDefinition { eventId = "dated", day = 2 });
            var controller = Controller(c); var reader = Reader(c, controller);
            Assert.That(reader.Label(Node("phase")), Does.Contain("事件等待声明入口：AfterBusiness"));
            Assert.That(reader.Label(Node("dated")), Does.Contain("日期或真实阶段不匹配"));
            controller.State.SetCondition("ready", false);
            reader.Refresh(controller);
            Assert.That(reader.Label(Node("conditional")), Does.Contain("条件不满足"));
            Assert.That(reader.Details(Node("conditional")), Does.Contain("ready").And.Contain("当前=False").And.Contain("要求=true").And.Contain("组内 AND／组间 OR"));
            controller.State.SetEventLocked("map", true);
            reader.Refresh(controller);
            Assert.That(reader.Label(Node("map")), Does.Contain("事件被锁定"));
        }

        [Test]
        public void ConstructionAndDisabledHavePriorityOverHistoryAndQueues()
        {
            var c = Config(); var controller = Controller(c);
            var snapshot = controller.State.Capture();
            snapshot.happenedEvents.Add("blocked");
            snapshot.pending.Add(new NarrativePendingValue { eventId = "blocked" });
            controller.RestoreSnapshot(snapshot);
            var reader = Reader(c, controller);
            Assert.That(reader.Label(Node("blocked")), Does.StartWith("建设阻塞"));
            var disabled = Node("once"); disabled.Disabled = true;
            Assert.That(reader.Label(disabled), Is.EqualTo("禁用"));
            Assert.That(reader.Details(Node("blocked")), Does.Contain("施工阻塞：construction.pending"));
            Assert.That(controller.State.GetCondition("construction.pending"), Is.False);
        }

        [TestCase(true, "已发生（一次性）")]
        [TestCase(false, "已发生（可重复）；当前入口可请求")]
        public void CompletedOnceAndRepeatAreDifferent(bool once, string expected)
        {
            var c = Config(); var controller = Controller(c); string id = once ? "once" : "repeat";
            controller.State.MarkEventHappened(id);
            var reader = Reader(c, controller);
            Assert.That(reader.Label(Node(id)), Does.StartWith(expected));
            if (once) Assert.That(reader.Details(Node(id)), Does.Contain("一次性事件已发生"));
        }

        [Test]
        public void OnceFlagRestrictsACompletedOtherwiseRepeatableEvent()
        {
            var c = Config(); var controller = Controller(c);
            controller.State.MarkEventHappened("repeat");
            controller.State.SetFlag("event.once.repeat", true);
            Assert.That(Reader(c, controller).Label(Node("repeat")), Is.EqualTo("已发生（一次性）"));
        }

        [Test]
        public void ActiveAndQueuedTakePriorityOverRepeatedHistoryAndBusyIsNotEligibility()
        {
            var c = Config(); var controller = Controller(c);
            var snapshot = controller.State.Capture();
            snapshot.happenedEvents.Add("repeat");
            snapshot.happenedEvents.Add("map");
            snapshot.pending.Add(new NarrativePendingValue { eventId = "repeat" });
            snapshot.pending.Add(new NarrativePendingValue { eventId = "map" });
            controller.RestoreSnapshot(snapshot);
            controller.ResumePendingEvents(); // Explicit fixture setup, never called by the reader.
            Assert.That(controller.CurrentEventId, Is.EqualTo("repeat"));
            Assert.That(controller.PendingEventIds, Is.EquivalentTo(new[] { "map" }));
            var reader = Reader(c, controller);
            Assert.That(reader.Label(Node("repeat")), Is.EqualTo("当前播放"));
            Assert.That(reader.Label(Node("map")), Is.EqualTo("排队中"));
            Assert.That(reader.Label(Node("once")), Does.Contain("系统忙：活动演出或等待演出结束"));
        }

        [Test]
        public void RepeatHistoryRemainsVisibleEvenWhenCurrentEntryIsBusy()
        {
            var c = Config(); var controller = Controller(c);
            controller.State.MarkEventHappened("repeat");
            Assert.That(controller.QueueEvent("once"), Is.True);
            Assert.That(Reader(c, controller).Label(Node("repeat")), Does.StartWith("已发生（可重复）").And.Contain("系统忙"));
        }

        [Test]
        public void MissingFactsAndMissingProvidersAreUnknownNotFalseOrCompleted()
        {
            var c = Config();
            c.conditions.Add(Condition("event", "once", "unregistered", "true"));
            c.bars.Add(new NarrativeBarDefinition { barKey = "external", sourceMode = NarrativeSourceMode.External, providerId = "missing", sourceKey = "value" });
            c.conditions.Add(new NarrativeConditionDefinition { conditionId = "external-row", ownerType = "event", ownerId = "once", condType = "BAR_GTE", target = "external", value = "1" });
            var controller = Controller(c); var reader = Reader(c, controller);
            Assert.That(reader.Label(Node("once")), Does.Contain("未知").And.Contain("数值未就绪"));
            Assert.That(reader.Details(Node("once")), Does.Contain("unregistered").And.Contain("external-row").And.Contain("不可用"));
            Assert.That(reader.Label(Node("unregistered", StoryNodeKind.Fact)), Does.Contain("未知"));
            Assert.That(reader.Label(Node("nonexistent")), Does.Contain("事件不存在"));
        }

        [Test]
        public void KnownBoolIsReadEvenWithoutATableProducerAndOpeningHasNoIndependentHistory()
        {
            var c = Config(); var controller = Controller(c);
            controller.State.MarkEventHappened("once");
            var reader = Reader(c, controller);
            Assert.That(reader.Label(Node("ready", StoryNodeKind.Fact)), Is.EqualTo("事实 = true"));
            Assert.That(reader.Label(Node("construction.pending", StoryNodeKind.Fact)), Is.EqualTo("事实 = false"));
            Assert.That(reader.Details(Node("ready", StoryNodeKind.Fact)), Does.Contain("未找到表内置真生产者").And.Contain("不等于缺少宿主实现"));
            Assert.That(reader.Label(Node("once", StoryNodeKind.Opening)), Does.Contain("Opening 无独立历史"));
            Assert.That(reader.Details(Node("once", StoryNodeKind.Opening)), Does.Contain("不能证明"));
        }

        [Test]
        public void EndingEligibilityDoesNotSelectEndingButExistingSelectionIsReported()
        {
            var c = Config(); var controller = Controller(c); var reader = Reader(c, controller);
            Assert.That(reader.Label(Node("end", StoryNodeKind.Ending)), Is.EqualTo("结局条件满足（尚未选中）"));
            Assert.That(controller.SelectedEndingId, Is.Null);
            var snapshot = controller.State.Capture(); snapshot.selectedEndingId = "end";
            controller.RestoreSnapshot(snapshot);
            reader.Refresh(controller);
            Assert.That(reader.Label(Node("end", StoryNodeKind.Ending)), Is.EqualTo("已达成（已选择结局）"));
            Assert.That(reader.Label(Node("disabled", StoryNodeKind.Ending)), Is.EqualTo("禁用"));
        }

        [Test]
        public void EndingConditionGroupsKeepOrAlternativesAndUnknownValues()
        {
            var c = Config();
            c.conditions.Add(Condition("ending", "end", "unregistered", "true", 1));
            var controller = Controller(c); var reader = Reader(c, controller);
            Assert.That(reader.Label(Node("end", StoryNodeKind.Ending)), Does.Contain("条件满足"));
            controller.State.SetCondition("ready", false); reader.Refresh(controller);
            Assert.That(reader.Label(Node("end", StoryNodeKind.Ending)), Does.Contain("未知"));
            Assert.That(reader.Details(Node("end", StoryNodeKind.Ending)), Does.Contain("unregistered").And.Contain("判定未知"));
            Assert.That(controller.SelectedEndingId, Is.Null);
        }

        [Test]
        public void EndingForecastUsesPriorityAndGroupedOrConditionsThenRefreshesItsSnapshot()
        {
            var c = Config();
            c.endings[0].priority = 100;
            c.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "late", initialValue = false });
            c.endings.Add(new NarrativeEndingDefinition { endingId = "early", enabled = true, priority = 200 });
            c.conditions.Add(Condition("ending", "early", "late", "true"));
            c.endings.Add(new NarrativeEndingDefinition { endingId = "later", enabled = true, priority = 50 });
            c.conditions.Add(Condition("ending", "later", "ready", "false", 0));
            c.conditions.Add(Condition("ending", "later", "ready", "true", 1));

            var controller = Controller(c);
            var reader = Reader(c, controller);
            string first = reader.EndingForecast;
            Assert.That(first, Does.Contain("[end]").And.Contain("优先级 100"));
            Assert.That(first, Does.Not.Contain("[early]").And.Not.Contain("[later]"));
            Assert.That(controller.SelectedEndingId, Is.Null);

            controller.State.SetCondition("ready", false);
            Assert.That(reader.EndingForecast, Is.EqualTo(first), "The snapshot stays stable until the owner refreshes it.");
            reader.Refresh(controller);
            Assert.That(reader.EndingForecast, Does.Contain("[later]").And.Contain("只读推演"));
            Assert.That(controller.SelectedEndingId, Is.Null);
        }

        [Test]
        public void UnknownHigherPriorityEndingBlocksLowerPriorityPrediction()
        {
            var c = Config();
            c.endings[0].priority = 10;
            c.endings.Add(new NarrativeEndingDefinition { endingId = "unknown.high", enabled = true, priority = 100 });
            c.conditions.Add(Condition("ending", "unknown.high", "not.registered", "true"));
            c.endings.Add(new NarrativeEndingDefinition { endingId = "known.low", enabled = true, priority = -10 });
            c.conditions.Add(Condition("ending", "known.low", "ready", "true"));

            var controller = Controller(c);
            string forecast = Reader(c, controller).EndingForecast;
            Assert.That(forecast, Does.Contain("无法确定").And.Contain("unknown.high").And.Contain("不能跳过"));
            Assert.That(forecast, Does.Not.Contain("预测首个满足的结局"));
            Assert.That(controller.SelectedEndingId, Is.Null);
        }

        [Test]
        public void EndingForecastReportsSelectedOutcomeAsFactInsteadOfRecomputingIt()
        {
            var c = Config();
            var controller = Controller(c);
            var snapshot = controller.State.Capture();
            snapshot.selectedEndingId = "end";
            controller.RestoreSnapshot(snapshot);

            string forecast = Reader(c, controller).EndingForecast;
            Assert.That(forecast, Does.Contain("实际结果").And.Contain("end"));
            Assert.That(forecast, Does.Not.Contain("预测首个满足的结局"));
        }


        [Test]
        public void RootCompletionNeverMarksMutuallyExclusivePagesAsSeenOrCurrent()
        {
            var c = Config(); var controller = Controller(c);
            controller.State.MarkEventHappened("once");
            var reader = Reader(c, controller);
            foreach (string page in new[] { "yes", "no" })
            {
                string status = reader.PageStatus("once", page);
                Assert.That(status, Does.Contain("系统无逐页历史").And.Contain("根事件已完成不代表每个互斥页均看过").And.Contain("活动当前页不可得"));
            }
            Assert.That(reader.PageStatus("once", "yes"), Does.Contain("仅条件预判：满足"));
            Assert.That(reader.PageStatus("once", "no"), Does.Contain("仅条件预判：不满足"));
            Assert.That(reader.PageStatus("map", "yes"), Does.Contain("未知：未找到属于该根事件"));
            Assert.That(reader.PageStatus(null, null), Does.Contain("未知：事件不存在"));
        }

        [TestCase("choice:chosen")]
        [TestCase("not:choice:chosen")]
        [TestCase("anyChoice:a;b")]
        [TestCase("check:not-evaluated")]
        [TestCase("not:check:not-evaluated")]
        [TestCase("not:gameplay:work:passed")]
        [TestCase("dependency:missing")]
        [TestCase("not:bar:missing:ge:1")]
        [TestCase("not:unregistered")]
        public void ExpiredOrUnavailableLocalAndWorldGuardsRemainUnknownIncludingNegation(string guard)
        {
            var c = Config(); c.presentations.conversationPages[0].pageCondKeys = guard;
            var controller = Controller(c);
            controller.State.MarkEventHappened("once");
            string status = Reader(c, controller).PageStatus("once", "yes");
            Assert.That(status, Does.Contain("仅条件预判：未知／不可用"));
        }

        [Test]
        public void ActiveRootCheckExceptionsAreSafeAndDoNotClaimAnActivePage()
        {
            var c = Config(); c.presentations.conversationPages[0].pageCondKeys = "not:check:not-evaluated";
            var controller = Controller(c); Assert.That(controller.QueueEvent("once"), Is.True);
            string status = Reader(c, controller).PageStatus("once", "yes");
            Assert.That(status, Does.Contain("未知／不可用").And.Contain("has not been evaluated").And.Contain("活动当前页不可得"));
        }

        [TestCase("bar:player.wisdom:ge:3", "满足")]
        [TestCase("not:bar:player.wisdom:ge:3", "不满足")]
        [TestCase("ready|not:construction.pending", "满足")]
        public void PagePredictionReusesTheExistingNumericGuardGrammarAndAndSemantics(string guard, string result)
        {
            var c = Config(); c.presentations.conversationPages[0].pageCondKeys = guard;
            Assert.That(Reader(c, Controller(c)).PageStatus("once", "yes"), Does.Contain("仅条件预判：" + result));
        }

        [Test]
        public void UnsupportedLegacyBarConditionsAreNotSilentlyAccepted()
        {
            var c = Config(); c.presentations.conversationPages[0].pageBarConds = "legacy";
            Assert.That(Reader(c, Controller(c)).PageStatus("once", "yes"), Does.Contain("不受现有演出支持").And.Contain("仅条件预判：未知"));
        }

        [Test]
        public void RefreshInvalidatesNodeAndPageCachesAndDetachesAnOldSession()
        {
            var c = Config(); var controller = Controller(c); var reader = Reader(c, controller);
            var node = Node("ready", StoryNodeKind.Fact);
            Assert.That(reader.Label(node), Is.EqualTo("事实 = true"));
            Assert.That(reader.PageStatus("once", "yes"), Does.Contain("仅条件预判：满足"));
            controller.State.SetCondition("ready", false);
            Assert.That(reader.Label(node), Is.EqualTo("事实 = true"), "OnGUI reuses the cached observation until Refresh.");
            reader.Refresh(controller);
            Assert.That(reader.Label(node), Is.EqualTo("事实 = false"));
            Assert.That(reader.PageStatus("once", "yes"), Does.Contain("仅条件预判：不满足"));
            reader.Refresh(null);
            Assert.That(reader.Label(node), Is.EqualTo("无会话"));
            Assert.That(reader.PageStatus("once", "yes"), Does.Contain("无会话"));
        }

        [Test]
        public void RepeatedOnGuiReadsDoNotRequeryAnInjectedProviderUntilRefresh()
        {
            var c = Config();
            c.bars.Add(new NarrativeBarDefinition { barKey = "external", sourceMode = NarrativeSourceMode.External, providerId = "test", sourceKey = "value" });
            c.conditions.Add(new NarrativeConditionDefinition { conditionId = "external-row", ownerType = "event", ownerId = "once", condType = "BAR_GTE", target = "external", value = "1" });
            var controller = Controller(c); int reads = 0;
            controller.BindValueProvider(new DelegateNarrativeValueProvider("test", _ => { reads++; return 2; }));
            controller.CompleteValueRestore(evaluateInitial: false); // Fixture owns injection and baseline, not reader.
            var reader = Reader(c, controller); var node = Node("once");
            reader.Label(node); int afterFirst = reads;
            for (int i = 0; i < 5; i++) { reader.Label(node); reader.Details(node); }
            Assert.That(reads, Is.EqualTo(afterFirst));
            reader.Refresh(controller); reader.Label(node);
            Assert.That(reads, Is.GreaterThan(afterFirst));
            controller.UnbindValueProviders();
        }

        private static object Field(NarrativeController controller, string name)
            => typeof(NarrativeController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);

        private static string JsonRows<T>(IEnumerable<T> rows) => string.Join("\n", rows.Select(row => JsonUtility.ToJson(row)));
        private static string ConfigurationData(NarrativeConfiguration c)
            => JsonRows(c.events) + JsonRows(c.results) + JsonRows(c.conditions) + JsonRows(c.conditionKeys)
                + JsonRows(c.bars) + JsonRows(c.endings) + JsonRows(c.presentations.conversationPages);

        // State.Capture is the pure data copy, NOT controller/runtime.CaptureSnapshot (which can read live values).
        private static string Observation(NarrativeController controller)
            => JsonUtility.ToJson(controller.State.Capture()) + "\n" + controller.CurrentEventId
                + "\n" + string.Join(",", controller.PendingEventIds) + "\n" + string.Join(",", controller.SelectedChoices)
                + "\n" + JsonUtility.ToJson(controller.ActiveAbilityCheck) + "\n" + controller.SelectedEndingId
                + "\n" + JsonRows(controller.WaitingCandidates)
                + "\n" + controller.IsBusy + ":" + controller.ValuesReady + ":" + controller.IsSafeBoundary
                + "\n" + Field(controller, "randomSeed") + ":" + Field(controller, "presentationEpoch")
                + ":" + Field(controller, "endingDirty") + ":" + Field(controller, "operationDepth")
                + ":" + string.Join(",", (HashSet<string>)Field(controller, "warnedPresentationDependencies"));

        [TestCase(false)]
        [TestCase(true)]
        public void ReadingAllStatusesLeavesDataSnapshotQueueRandomAndCallbacksUnchanged(bool active)
        {
            var c = Config();
            c.presentations.conversationPages[0].pageCondKeys = "not:gameplay:missing:passed|dependency:missing|check:missing";
            var presenter = new HoldingPresenter(); var controller = new NarrativeController(c, presenter);
            if (active) Assert.That(controller.QueueEvent("once"), Is.True);
            int callbacks = 0;
            controller.EventCompleted += _ => callbacks++;
            controller.EventQueued += _ => callbacks++;
            controller.EndingSelected += _ => callbacks++;
            controller.AbilityCheckEvaluated += _ => callbacks++;
            var graph = NarrativeRelationshipGraph.Build(c);
            string before = Observation(controller), configBefore = ConfigurationData(c);
            int presentations = presenter.Calls;
            var reader = Reader(c, controller);
            for (int pass = 0; pass < 2; pass++)
            {
                Assert.That(reader.EndingForecast, Is.Not.Null);

                foreach (var node in graph.Nodes) { reader.Label(node); reader.Details(node); }
                reader.Label(Node("ready", StoryNodeKind.Fact));
                reader.Details(Node("unknown", StoryNodeKind.Fact));
                reader.PageStatus("once", "yes"); reader.PageStatus("once", "no");
                reader.Refresh(controller);
            }
            Assert.That(Observation(controller), Is.EqualTo(before));
            Assert.That(ConfigurationData(c), Is.EqualTo(configBefore));
            Assert.That(presenter.Calls, Is.EqualTo(presentations));
            Assert.That(callbacks, Is.Zero);
        }
    }
}
