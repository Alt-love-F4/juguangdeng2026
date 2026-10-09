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
        public void RequirementParserSupportsAllDeclaredForms()
        {
            List<NarrativeActionRequirement> result = NarrativeActionRequirementParser.Parse("ActionPoint(1);Item(tea,2);Bar(world.calendar,>=,2);Cond(flag.ready);NotCond(flag.done)");
            Assert.That(result, Has.Count.EqualTo(5));
            Assert.That(result[1].value, Is.EqualTo(2));
            Assert.That(result[2].op, Is.EqualTo(">="));
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
        public void SixRequirementsCheckBarsConditionsItemsAndPoints()
        {
            var narrative = Controller(new Presenter());
            narrative.State.SetFlag("ready", true);
            var inventory = new Inventory(); inventory.Set("tea", 2);
            var action = new NarrativeActionDefinition { actionKey = "six", requirements = "Item(tea,2);Bar(world.calendar,>=,1);Bar(world.calendar,<,2);Cond(ready);Cond(blocked,false);ActionPoint(1)" };
            var actions = new NarrativeActionController(narrative, new[] { action }, 5, inventory);
            Assert.That(actions.IsAvailable("six", out var checks), Is.True);
            Assert.That(checks.Count, Is.EqualTo(6));
            narrative.State.SetFlag("blocked", true);
            Assert.That(actions.IsAvailable("six", out checks), Is.False);
            Assert.That(checks[4].satisfied, Is.False);
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
            restoredController.EndTurn();
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
