using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeActionTests
    {
        private sealed class Presenter : INarrativePresenter
        {
            public readonly List<string> Played = new List<string>();
            private readonly List<Action> callbacks = new List<Action>();
            public void Play(NarrativeEventRequest request, Action completed) { Played.Add(request.Event.eventId); callbacks.Add(completed); }
            public void Complete() { Action callback = callbacks[callbacks.Count - 1]; callbacks.RemoveAt(callbacks.Count - 1); callback(); }
        }

        private sealed class DrainingPresenter : INarrativePresenter, INarrativePresentationDrain
        {
            public Action EventCallback, DrainCallback;
            public void Play(NarrativeEventRequest request, Action completed) => EventCallback = completed;
            public void Drain(Action completed) => DrainCallback = completed;
        }

        private sealed class Inventory : INarrativeActionInventory
        {
            private readonly Dictionary<string, int> items = new Dictionary<string, int>();
            public int GetCount(string itemKey) => items.TryGetValue(itemKey, out int value) ? value : 0;
            public void Set(string key, int value) => items[key] = value;
            public bool TryConsume(string itemKey, int count) { if (GetCount(itemKey) < count) return false; items[itemKey] -= count; return true; }
            public bool TryAdd(string itemKey, int count) { items[itemKey] = GetCount(itemKey) + count; return true; }
        }

        private static NarrativeController Controller(Presenter presenter)
        {
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999, initValue = 1, advanceMode = NarrativeAdvanceMode.Monotonic });
            config.events.Add(new NarrativeEventDefinition { eventId = "EV_ACTION" });
            config.events.Add(new NarrativeEventDefinition { eventId = "EV_ACTION_2" });
            return new NarrativeController(config, presenter);
        }

        [Test]
        public void RequirementParserAcceptsCostsAndRejectsLegacyReadOnlyChecks()
        {
            var result = NarrativeActionRequirementParser.Parse("ActionPoint(1);Item(tea,2)");
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[1].value, Is.EqualTo(2));
            foreach (string expression in new[] { "Bar(world.calendar,>=,2)", "Cond(flag.ready)", "NotCond(flag.done)" })
                Assert.Throws<FormatException>(() => NarrativeActionRequirementParser.Parse(expression));
        }

        [Test]
        public void DailyActionConsumesPointAndCannotRepeatUntilNextDay()
        {
            var presenter = new Presenter();
            var controller = Controller(presenter);
            var action = new NarrativeActionDefinition { actionKey = "daily", type = NarrativeActionType.Daily, requirements = "ActionPoint(1)", resultEvents = new List<string> { "EV_ACTION" } };
            var actions = new NarrativeActionController(controller, new[] { action }, 5);
            Assert.That(actions.Execute("daily"), Is.True);
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(actions.IsAvailable("daily", out _), Is.False);
            presenter.Complete();
            actions.BeginDay(2);
            Assert.That(actions.IsAvailable("daily", out _), Is.True);
        }

        [Test]
        public void ActionEventsPlayInConfiguredOrder()
        {
            var presenter = new Presenter();
            var controller = Controller(presenter);
            var action = new NarrativeActionDefinition { actionKey = "once", type = NarrativeActionType.Once, resultEvents = new List<string> { "EV_ACTION", "EV_ACTION_2" } };
            var actions = new NarrativeActionController(controller, new[] { action });
            Assert.That(actions.Execute("once"), Is.True);
            Assert.That(presenter.Played, Is.EqualTo(new[] { "EV_ACTION" }));
            presenter.Complete();
            Assert.That(presenter.Played, Is.EqualTo(new[] { "EV_ACTION", "EV_ACTION_2" }));
            presenter.Complete();
            Assert.That(actions.IsAvailable("once", out _), Is.False);
        }

        [Test]
        public void CombinedDailyProgressTriggersOnlyAtTarget()
        {
            var presenter = new Presenter();
            var action = new NarrativeActionDefinition { actionKey = "project", type = NarrativeActionType.Daily, progressTarget = 2, durationDays = 4, resultEvents = new List<string> { "EV_ACTION" } };
            var actions = new NarrativeActionController(Controller(presenter), new[] { action });
            actions.Mount("market", "project");
            Assert.That(actions.Execute("project"), Is.True);
            Assert.That(presenter.Played, Is.Empty);
            Assert.That(actions.GetProgress("project"), Is.EqualTo(1));
            Assert.That(actions.Execute("project"), Is.False);
            actions.BeginDay(2);
            Assert.That(actions.Execute("project"), Is.True);
            Assert.That(presenter.Played, Is.EqualTo(new[] { "EV_ACTION" }));
            presenter.Complete();
            Assert.That(actions.IsVisible("project"), Is.False);
        }

        [Test]
        public void FutureCountsFromMountAndReplacesInSameLocationAfterRestore()
        {
            var presenter = new Presenter();
            var future = new NarrativeActionDefinition { actionKey = "future", type = NarrativeActionType.Future, durationDays = 2, nextActionKey = "next", resultEvents = new List<string> { "EV_ACTION" } };
            var next = new NarrativeActionDefinition { actionKey = "next" };
            var actions = new NarrativeActionController(Controller(presenter), new[] { future, next });
            actions.BeginDay(3); actions.Mount("market", "future");
            Assert.That(actions.GetRemainingDays("future"), Is.EqualTo(2));
            Assert.That(actions.Execute("future"), Is.False);
            actions.BeginDay(4);
            string saved = actions.CaptureJson();
            var restored = new NarrativeActionController(Controller(presenter), new[] { future, next });
            restored.RestoreJson(saved);
            Assert.That(restored.GetRemainingDays("future"), Is.EqualTo(1));
            restored.BeginDay(5);
            Assert.That(restored.IsVisible("future"), Is.False);
            Assert.That(new List<NarrativeActionDefinition>(restored.GetMountedActions("market"))[0].actionKey, Is.EqualTo("next"));
            Assert.That(presenter.Played.Count, Is.EqualTo(1));
            presenter.Complete(); restored.BeginDay(6);
            Assert.That(presenter.Played.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnmetCrucialActionStillBlocksEndDay()
        {
            var presenter = new Presenter();
            var crucial = new NarrativeActionDefinition { actionKey = "crucial", mustExecute = true, requirements = "Item(tea)" };
            var end = new NarrativeActionDefinition { actionKey = "end", type = NarrativeActionType.Daily, endDay = true };
            var inventory = new Inventory();
            var actions = new NarrativeActionController(Controller(presenter), new[] { crucial, end }, 5, inventory);
            actions.Mount("market", "crucial"); actions.Mount("home", "end");
            Assert.That(actions.HasBlockingActions(), Is.True);
            Assert.That(actions.Execute("end"), Is.False);
            inventory.Set("tea", 1);
            Assert.That(actions.Execute("crucial"), Is.True);
            Assert.That(actions.HasBlockingActions(), Is.False);
            Assert.That(actions.Execute("end"), Is.True);
        }

        [Test]
        public void DuplicateCostsCannotOverspendOrConsumeOnRejection()
        {
            var inventory = new Inventory(); inventory.Set("tea", 2);
            var action = new NarrativeActionDefinition { actionKey = "cost", requirements = "Item(tea,2);Item(tea,1);ActionPoint(2);ActionPoint(2)" };
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] { action }, 3, inventory);
            Assert.That(actions.Execute("cost"), Is.False);
            Assert.That(inventory.GetCount("tea"), Is.EqualTo(2));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(3));
            inventory.Set("tea", 3); actions.ActionPoints.SetMax(4); actions.ActionPoints.Set(4);
            Assert.That(actions.Execute("cost"), Is.True);
            Assert.That(inventory.GetCount("tea"), Is.Zero);
            Assert.That(actions.ActionPoints.Current, Is.Zero);
        }

        [Test]
        public void TimedActionRetiresAndFiresEventsAtExactDeadline()
        {
            var presenter = new Presenter();
            var action = new NarrativeActionDefinition { actionKey = "timer", type = NarrativeActionType.Repeat, durationDays = 2, resultEvents = new List<string> { "EV_ACTION" } };
            var actions = new NarrativeActionController(Controller(presenter), new[] { action });
            actions.Mount("market", "timer"); actions.BeginDay(2);
            Assert.That(actions.IsVisible("timer"), Is.True);
            actions.BeginDay(3);
            Assert.That(actions.IsVisible("timer"), Is.False);
            Assert.That(presenter.Played.Count, Is.EqualTo(1));
            presenter.Complete(); actions.BeginDay(4);
            Assert.That(presenter.Played.Count, Is.EqualTo(1));
        }

        [Test]
        public void ActionConditionsUseSharedAndOrReaderButNeverChooseAlternativeCosts()
        {
            var config = new NarrativeConfiguration();
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "key", ownerType = "action", ownerId = "door", groupIndex = 1, condType = "ITEM_GTE", target = "key", value = "1" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "ready", ownerType = "action", ownerId = "door", groupIndex = 2, condType = "BOOL_EQ", target = "ready", value = "true" });
            var narrative = new NarrativeController(config);
            var inventory = new NarrativeMapInventory(); inventory.TryAdd("key", 1); inventory.TryAdd("tea", 2);
            var action = new NarrativeActionDefinition { actionKey = "door", requirements = "Item(tea,2);ActionPoint(1)" };
            var actions = new NarrativeActionController(narrative, new[] { action }, 5, inventory);
            Assert.That(actions.IsAvailable("door", out _), Is.True);
            Assert.That(inventory.GetCount("key"), Is.EqualTo(1), "Condition checks do not consume keys.");
            inventory.TryConsume("key", 1);
            Assert.That(actions.IsAvailable("door", out _), Is.False);
            narrative.State.SetCondition("ready", true);
            Assert.That(actions.Execute("door"), Is.True);
            Assert.That(inventory.GetCount("tea"), Is.Zero);
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(actions.Execute("door"), Is.False, "A true OR branch cannot waive explicit costs.");
        }

        [Test]
        public void SavingDuringFirstEventRestoresRemainingEventChain()
        {
            var presenter = new Presenter();
            var definition = new NarrativeActionDefinition { actionKey = "chain", resultEvents = new List<string> { "EV_ACTION", "EV_ACTION_2" } };
            var controller = Controller(presenter);
            var actions = new NarrativeActionController(controller, new[] { definition });
            actions.Execute("chain");
            var narrativeSnapshot = controller.CaptureSnapshot();
            string actionJson = actions.CaptureJson();
            var restoredPresenter = new Presenter();
            var restoredController = Controller(restoredPresenter);
            var restoredActions = new NarrativeActionController(restoredController, new[] { definition });
            restoredController.RestoreSnapshot(narrativeSnapshot);
            restoredActions.RestoreJson(actionJson);
            Assert.That(restoredActions.IsExecuting, Is.True);
            restoredActions.ResumePendingExecution();
            restoredPresenter.Complete();
            Assert.That(restoredPresenter.Played, Is.EqualTo(new[] { "EV_ACTION", "EV_ACTION_2" }));
            restoredPresenter.Complete();
            Assert.That(restoredActions.IsExecuting, Is.False);
        }

        [Test]
        public void CompletedProgressDoesNotFireAgainOnTimerExpiry()
        {
            var presenter = new Presenter();
            var definition = new NarrativeActionDefinition { actionKey = "progress_timer", progressTarget = 1, durationDays = 1, resultEvents = new List<string> { "EV_ACTION" } };
            var actions = new NarrativeActionController(Controller(presenter), new[] { definition });
            actions.Mount("market", definition.actionKey); actions.Execute(definition.actionKey); presenter.Complete();
            actions.BeginDay(2);
            Assert.That(presenter.Played.Count, Is.EqualTo(1));
        }

        [Test]
        public void InvalidRequirementsAndFutureConfigurationsFailEarly()
        {
            Assert.Throws<FormatException>(() => NarrativeActionRequirementParser.Parse("ActionPoint(0)"));
            Assert.Throws<FormatException>(() => NarrativeActionRequirementParser.Parse("Bar(world.calendar,>=,2147483648)"));
            Assert.Throws<FormatException>(() => NarrativeActionRequirementParser.Parse("Cond(flag,maybe)"));
            Assert.Throws<FormatException>(() => NarrativeActionRequirementParser.Parse("Cond(a);Cond(b);Cond(c);Cond(d);Cond(e);Cond(f);Cond(g)"));
            Assert.Throws<FormatException>(() => new NarrativeActionController(Controller(new Presenter()), new[] { new NarrativeActionDefinition { actionKey="bad",type=NarrativeActionType.Future,durationDays=2 } }));
        }

        [Test]
        public void MapInventoryRoundTripsWithActionSnapshot()
        {
            var inventory = new NarrativeMapInventory(); inventory.TryAdd("tea", 3);
            var actions = new NarrativeActionController(Controller(new Presenter()), new NarrativeActionDefinition[0], 5, inventory);
            string json = actions.CaptureJson();
            var restoredInventory = new NarrativeMapInventory();
            var restored = new NarrativeActionController(Controller(new Presenter()), new NarrativeActionDefinition[0], 5, restoredInventory);
            restored.RestoreJson(json);
            Assert.That(restoredInventory.GetCount("tea"), Is.EqualTo(3));
            restored.ActionPoints.TryConsume(1); restored.BeginDay(1);
            Assert.That(restored.ActionPoints.Current, Is.EqualTo(4), "Re-entering the same day must not refill points.");
        }

        private static NarrativeActionDefinition Delivery(NarrativeDeliveryMode mode, int target = 3)
            => new NarrativeActionDefinition { actionKey = "delivery", deliveryMode = mode, progressTarget = target,
                requirements = "Item(medicine,2);Item(food,1);ActionPoint(1)", resultEvents = new List<string> { "EV_ACTION_2" } };

        private static NarrativeMapInventory Stock(int medicine = 20, int food = 20)
        {
            var inventory = new NarrativeMapInventory();
            if (medicine > 0) inventory.TryAdd("medicine", medicine);
            if (food > 0) inventory.TryAdd("food", food);
            return inventory;
        }

        [TestCase(1, 20, 5)]
        [TestCase(20, 0, 5)]
        [TestCase(20, 20, 0)]
        public void DeliveryInsufficientCostLeavesEveryResourceAndProgressUnchanged(int medicine, int food, int points)
        {
            var inventory = Stock(medicine, food);
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] { Delivery(NarrativeDeliveryMode.Cumulative) }, 5, inventory);
            actions.ActionPoints.Set(points);
            Assert.That(actions.Execute("delivery"), Is.False);
            Assert.That(inventory.GetCount("medicine"), Is.EqualTo(medicine));
            Assert.That(inventory.GetCount("food"), Is.EqualTo(food));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(points));
            Assert.That(actions.GetProgress("delivery"), Is.Zero);
        }

        [Test]
        public void DailyDeliveryCannotSpendFiveDaysOfStockAndSameDayRestoreCannotRepeat()
        {
            var inventory = Stock(); var definition = Delivery(NarrativeDeliveryMode.Cumulative);
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] { definition }, 5, inventory);
            Assert.That(actions.Execute("delivery"), Is.True);
            Assert.That(actions.Execute("delivery"), Is.False);
            var restoredStock = new NarrativeMapInventory();
            var restored = new NarrativeActionController(Controller(new Presenter()), new[] { definition }, 5, restoredStock);
            restored.RestoreJson(actions.CaptureJson()); restored.RefreshForDay(1);
            Assert.That(restored.Execute("delivery"), Is.False);
            Assert.That(restoredStock.GetCount("medicine"), Is.EqualTo(18));
            Assert.That(restoredStock.GetCount("food"), Is.EqualTo(19));
            Assert.That(restored.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(restored.GetProgress("delivery"), Is.EqualTo(1));
        }

        [TestCase(NarrativeDeliveryMode.Cumulative, 1)]
        [TestCase(NarrativeDeliveryMode.Consecutive, 0)]
        public void SkippingDaysRefreshesEffectiveProgressBeforeAnotherClick(NarrativeDeliveryMode mode, int expected)
        {
            var stock = Stock(); var definition = Delivery(mode);
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] { definition }, 5, stock);
            actions.Execute("delivery");
            string json = actions.CaptureJson();
            actions.BeginDay(4);
            Assert.That(actions.GetProgress("delivery"), Is.EqualTo(expected));
            var restored = new NarrativeActionController(Controller(new Presenter()), new[] { definition }, 5, new NarrativeMapInventory());
            restored.RestoreJson(json); restored.RefreshForDay(4);
            Assert.That(restored.GetProgress("delivery"), Is.EqualTo(expected));
            Assert.That(restored.ActionPoints.Current, Is.EqualTo(4), "Restoration refresh must not refill spent points.");
            Assert.That(stock.GetCount("medicine"), Is.EqualTo(18), "Streak breaks never refund costs.");
        }

        [Test]
        public void ConsecutiveMilestoneDoesNotReplayAfterGapAndCompletionSurvivesLaterDays()
        {
            var presenter = new Presenter(); var definition = Delivery(NarrativeDeliveryMode.Consecutive);
            definition.milestones.Add(new NarrativeActionMilestoneDefinition { milestoneId = "m2", actionKey = "delivery", atProgress = 2, eventId = "EV_ACTION" });
            var actions = new NarrativeActionController(Controller(presenter), new[] { definition }, 5, Stock());
            actions.Execute("delivery"); actions.BeginDay(2); actions.Execute("delivery");
            Assert.That(presenter.Played, Is.EqualTo(new[] { "EV_ACTION" })); presenter.Complete();
            actions.BeginDay(4); Assert.That(actions.GetProgress("delivery"), Is.Zero);
            actions.Execute("delivery"); actions.BeginDay(5); actions.Execute("delivery");
            Assert.That(presenter.Played, Is.EqualTo(new[] { "EV_ACTION" }));
            actions.BeginDay(6); actions.Execute("delivery"); presenter.Complete();
            actions.BeginDay(10);
            Assert.That(actions.IsCompleted("delivery"), Is.True);
            Assert.That(actions.GetProgress("delivery"), Is.EqualTo(3));
            Assert.That(actions.Execute("delivery"), Is.False);
            Assert.That(presenter.Played, Is.EqualTo(new[] { "EV_ACTION", "EV_ACTION_2" }));
        }

        [Test]
        public void MilestonePresentationRestoreResumesWithoutRepayingOrRecounting()
        {
            var presenter = new Presenter(); var definition = Delivery(NarrativeDeliveryMode.Cumulative);
            definition.milestones.Add(new NarrativeActionMilestoneDefinition { milestoneId = "m1", actionKey = "delivery", atProgress = 1, eventId = "EV_ACTION" });
            var narrative = Controller(presenter);
            var actions = new NarrativeActionController(narrative, new[] { definition }, 5, Stock());
            actions.Execute("delivery");
            var narrativeSave = narrative.CaptureSnapshot(); string actionSave = actions.CaptureJson();
            var restoredPresenter = new Presenter(); var restoredNarrative = Controller(restoredPresenter); var stock = new NarrativeMapInventory();
            var restored = new NarrativeActionController(restoredNarrative, new[] { definition }, 5, stock);
            restoredNarrative.RestoreSnapshot(narrativeSave); restored.RestoreJson(actionSave); restored.RefreshForDay(1); restored.ResumePendingExecution();
            Assert.That(restoredPresenter.Played, Is.EqualTo(new[] { "EV_ACTION" }));
            Assert.That(restored.Execute("delivery"), Is.False);
            restoredPresenter.Complete();
            Assert.That(restored.IsExecuting, Is.False);
            Assert.That(restored.GetProgress("delivery"), Is.EqualTo(1));
            Assert.That(stock.GetCount("medicine"), Is.EqualTo(18));
            Assert.That(restored.ActionPoints.Current, Is.EqualTo(4));
            var snapshot = new NarrativeActionStateSnapshot(); restored.CaptureInto(snapshot);
            Assert.That(snapshot.deliveries[0].triggeredMilestones, Is.EqualTo(new[] { "m1" }));
            Assert.That(restoredNarrative.State.TryGetBar("world.calendar", out int day), Is.True);
            Assert.That(day, Is.EqualTo(1), "Restoring an event cannot implicitly advance the day.");
        }

        [Test]
        public void MigratingUndatedProgressResetsOnlyExplicitDeliveries()
        {
            var delivery = Delivery(NarrativeDeliveryMode.Cumulative);
            var ordinary = new NarrativeActionDefinition { actionKey = "ordinary", type = NarrativeActionType.Progress, progressTarget = 8 };
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] { delivery, ordinary }, 5, Stock());
            actions.RestoreFrom(new NarrativeActionStateSnapshot { counters = new List<NarrativeActionCounterValue> {
                new NarrativeActionCounterValue { key = "delivery", value = 2 }, new NarrativeActionCounterValue { key = "ordinary", value = 4 } } });
            Assert.That(actions.GetProgress("delivery"), Is.Zero);
            Assert.That(actions.GetProgress("ordinary"), Is.EqualTo(4));
        }

        [Test]
        public void InventoryObserversSeeEntireDeliveryAndCannotSaveMidCommit()
        {
            var stock = Stock(); var actions = new NarrativeActionController(Controller(new Presenter()), new[] { Delivery(NarrativeDeliveryMode.Cumulative) }, 5, stock);
            int notices = 0;
            stock.Changed += () => {
                notices++;
                Assert.That(stock.GetCount("medicine"), Is.EqualTo(18)); Assert.That(stock.GetCount("food"), Is.EqualTo(19));
                Assert.That(actions.ActionPoints.Current, Is.EqualTo(4)); Assert.That(actions.GetProgress("delivery"), Is.EqualTo(1));
                Assert.That(actions.CanCaptureSnapshot, Is.False);
            };
            Assert.That(actions.Execute("delivery"), Is.True);
            Assert.That(notices, Is.EqualTo(1)); Assert.That(actions.CanCaptureSnapshot, Is.True);
        }

        [Test]
        public void EndingWaitsForEntireActionEventChainAndUsesFinalCondition()
        {
            var config = new NarrativeConfiguration();
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "candidate" });
            config.events.Add(new NarrativeEventDefinition { eventId = "first" }); config.events.Add(new NarrativeEventDefinition { eventId = "second" });
            config.results.Add(new NarrativeResultDefinition { resultId = "set", eventId = "first", resultType = NarrativeResultType.SetFlag, target = "candidate", value = "true" });
            config.results.Add(new NarrativeResultDefinition { resultId = "clear", eventId = "second", resultType = NarrativeResultType.ClearFlag, target = "candidate" });
            config.endings.Add(new NarrativeEndingDefinition { endingId = "ending", enabled = true });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "ending-condition", ownerType = "ending", ownerId = "ending", condType = "BOOL_EQ", target = "candidate", value = "true" });
            var presenter = new Presenter(); var narrative = new NarrativeController(config, presenter);
            var action = new NarrativeActionDefinition { actionKey = "chain", resultEvents = new List<string> { "first", "second" } };
            var actions = new NarrativeActionController(narrative, new[] { action }); narrative.SetEndingChecksEnabled(true);
            actions.Execute("chain"); presenter.Complete();
            Assert.That(narrative.IsEnded, Is.False); Assert.That(presenter.Played, Is.EqualTo(new[] { "first", "second" }));
            presenter.Complete(); Assert.That(narrative.IsEnded, Is.False); Assert.That(actions.IsExecuting, Is.False);
        }

        [Test]
        public void ActionCallbackWaitsForExtraPresentationAndCannotAdvanceAfterEnding()
        {
            var config = new NarrativeConfiguration();
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready" });
            config.events.Add(new NarrativeEventDefinition { eventId = "finish" });
            config.results.Add(new NarrativeResultDefinition { resultId = "ready", eventId = "finish", resultType = NarrativeResultType.SetFlag, target = "ready", value = "true" });
            config.results.Add(new NarrativeResultDefinition { resultId = "extra", eventId = "finish", seq = 1, resultType = NarrativeResultType.PlaySetup, target = "setup:extra" });
            config.endings.Add(new NarrativeEndingDefinition { endingId = "ending", enabled = true });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "ready", ownerType = "ending", ownerId = "ending", condType = "BOOL_EQ", target = "ready", value = "true" });
            var presenter = new DrainingPresenter(); var narrative = new NarrativeController(config, presenter);
            var definition = new NarrativeActionDefinition { actionKey = "end", type = NarrativeActionType.Daily, endDay = true, resultEvents = new List<string> { "finish" } };
            var actions = new NarrativeActionController(narrative, new[] { definition }); narrative.SetEndingChecksEnabled(true);
            bool callbackCalled = false, advanced = false;
            actions.Execute("end", success => { callbackCalled = true; advanced = success; });
            presenter.EventCallback();
            Assert.That(callbackCalled, Is.False); Assert.That(actions.IsExecuting, Is.True); Assert.That(narrative.IsEnded, Is.False);
            presenter.DrainCallback();
            Assert.That(narrative.IsEnded, Is.True); Assert.That(callbackCalled, Is.True); Assert.That(advanced, Is.False);
            Assert.That(actions.IsExecuting, Is.False); Assert.That(actions.Execute("end"), Is.False);
        }

        [Test]
        public void OrdinaryProgressCanStillRepeatWithinOneDay()
        {
            var definition = new NarrativeActionDefinition { actionKey = "ordinary", type = NarrativeActionType.Progress, progressTarget = 3 };
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] { definition });
            Assert.That(actions.Execute("ordinary"), Is.True); Assert.That(actions.Execute("ordinary"), Is.True);
            Assert.That(actions.GetProgress("ordinary"), Is.EqualTo(2));
        }

        [Test]
        public void ActionStateRoundTrips()
        {
            var presenter = new Presenter();
            var controller = Controller(presenter);
            var action = new NarrativeActionDefinition { actionKey = "daily", type = NarrativeActionType.Daily };
            var actions = new NarrativeActionController(controller, new[] { action }, 5);
            actions.BeginDay(3);
            actions.ActionPoints.TryConsume(2);
            Assert.That(actions.Execute("daily"), Is.True);
            string json = actions.CaptureJson();
            var restored = new NarrativeActionController(controller, new[] { action }, 1);
            restored.RestoreJson(json);
            Assert.That(restored.CurrentDay, Is.EqualTo(3));
            Assert.That(restored.ActionPoints.Current, Is.EqualTo(3));
            Assert.That(restored.IsAvailable("daily", out _), Is.False);
        }
    }
}
