using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeActionTests
    {
        private sealed class Presenter : INarrativePresenter, INarrativePresentationCancellation
        {
            public event Action<string> Cancelled;
            public void Cancel() => Cancelled?.Invoke("user_cancelled");
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
            public Func<string, bool> AfterConsume;
            public bool TryConsume(string itemKey, int count) { if (GetCount(itemKey) < count) return false; items[itemKey] -= count; return AfterConsume?.Invoke(itemKey) ?? true; }
            public bool TryAdd(string itemKey, int count) { items[itemKey] = GetCount(itemKey) + count; return true; }
        }

        private sealed class Wallet : INarrativeMoneyAccount, INarrativeMoneyNotificationScope
        {
            private sealed class Scope : IDisposable { public void Dispose() { } }
            public int Balance = 100;
            public bool Reject;
            public Action OnCommit;
            public bool TryRead(out int value) { value = Balance; return true; }
            public bool TryCommit(int expected, int final, out string error)
            {
                error = null;
                if (Reject || Balance != expected || final < 0) { error = "injected_rejection"; return false; }
                Balance = final;
                OnCommit?.Invoke();
                return true;
            }
            public IDisposable DeferNotifications() => new Scope();
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
            var result = NarrativeActionRequirementParser.Parse("ActionPoint(1);Item(tea,2);Money(25)");
            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result[1].value, Is.EqualTo(2));
            Assert.That(result[2].type, Is.EqualTo(NarrativeActionRequirementType.Money));
            Assert.Throws<FormatException>(() => NarrativeActionRequirementParser.Parse("Money(0)"));
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
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(5), "Presentation does not spend the staged action cost.");
            Assert.That(actions.IsAvailable("daily", out _), Is.False);
            presenter.Complete();
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
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
            var future = new NarrativeActionDefinition { actionKey = "future", type = NarrativeActionType.Future, durationDays = 2, nextActionKey = "next", onExpirePolicy = "RunEvents", onExpireEvents = new List<string> { "EV_ACTION" } };
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
            var action = new NarrativeActionDefinition { actionKey = "timer", type = NarrativeActionType.Repeat, durationDays = 2, onExpirePolicy = "RunEvents", onExpireEvents = new List<string> { "EV_ACTION" } };
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
        public void EventCancellationBeforeFirstSuccessSpendsNothingAndRetryCommitsOnce()
        {
            var presenter = new Presenter(); var stock = new NarrativeMapInventory(); stock.TryAdd("tea", 3);
            var action = new NarrativeActionDefinition { actionKey = "story", type = NarrativeActionType.Once,
                requirements = "Item(tea,2);ActionPoint(1)", resultEvents = new List<string> { "EV_ACTION" } };
            var actions = new NarrativeActionController(Controller(presenter), new[] { action }, 5, stock);
            Assert.That(actions.Execute("story", operationId: "story-1"), Is.True);
            Assert.That(stock.GetCount("tea"), Is.EqualTo(3)); Assert.That(actions.ActionPoints.Current, Is.EqualTo(5));
            presenter.Cancel();
            Assert.That(actions.IsCompleted("story"), Is.False);
            Assert.That(stock.GetCount("tea"), Is.EqualTo(3)); Assert.That(actions.CanCaptureSnapshot, Is.True);
            Assert.That(actions.Execute("story", operationId: "story-1"), Is.True); presenter.Complete();
            Assert.That(stock.GetCount("tea"), Is.EqualTo(1)); Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(actions.IsCompleted("story"), Is.True);
            Assert.That(actions.Execute("story", operationId: "story-1"), Is.True);
            Assert.That(stock.GetCount("tea"), Is.EqualTo(1));
        }

        [Test]
        public void EventChainRetryAfterFirstRootDoesNotReplayOrRepayCommittedCost()
        {
            var presenter = new Presenter(); var narrative = Controller(presenter); var stock = new NarrativeMapInventory(); stock.TryAdd("tea", 3);
            var action = new NarrativeActionDefinition { actionKey = "chain", type = NarrativeActionType.Once,
                requirements = "Item(tea,2);ActionPoint(1)", resultEvents = new List<string> { "EV_ACTION", "EV_ACTION_2" } };
            var actions = new NarrativeActionController(narrative, new[] { action }, 5, stock);
            actions.Execute("chain"); presenter.Complete(); presenter.Cancel();
            Assert.That(stock.GetCount("tea"), Is.EqualTo(1)); Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            var save = new NarrativeActionStateSnapshot(); actions.CaptureInto(save);
            Assert.That(save.rootCostCommitted, Is.True); Assert.That(save.pendingEvents, Is.EqualTo(new[] { "EV_ACTION_2" }));
            var restoredPresenter = new Presenter(); var restoredNarrative = Controller(restoredPresenter);
            restoredNarrative.RestoreSnapshot(narrative.CaptureSnapshot()); var restoredStock = new NarrativeMapInventory();
            var restored = new NarrativeActionController(restoredNarrative, new[] { action }, 5, restoredStock); restored.RestoreFrom(save);
            restored.ResumePendingExecution(); Assert.That(restoredPresenter.Played, Is.EqualTo(new[] { "EV_ACTION_2" }));
            restoredPresenter.Complete();
            Assert.That(restored.IsCompleted("chain"), Is.True); Assert.That(restoredStock.GetCount("tea"), Is.EqualTo(1));
            Assert.That(restored.ActionPoints.Current, Is.EqualTo(4));
        }

        [Test]
        public void StagedEventAppliesSuccessResultGroupOnFinalRealRootOnlyOnce()
        {
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "first", entry = NarrativeEntryPoint.MapAction, onceOnly = true });
            config.events.Add(new NarrativeEventDefinition { eventId = "final", entry = NarrativeEntryPoint.MapAction, onceOnly = true });
            config.results.Add(new NarrativeResultDefinition { resultId = "first-output", eventId = "first", resultType = NarrativeResultType.ItemAdd, target = "token", value = "1" });
            config.results.Add(new NarrativeResultDefinition { resultId = "final-output", eventId = "final", resultType = NarrativeResultType.ItemAdd, target = "token", value = "2" });
            // A result owner is a group, with no fake event definition or presentation.
            config.results.Add(new NarrativeResultDefinition { resultId = "group-output", eventId = "action-success", resultType = NarrativeResultType.ItemAdd, target = "token", value = "3" });
            config.results.Add(new NarrativeResultDefinition { resultId = "group-money", eventId = "action-success", resultType = NarrativeResultType.MoneyAdd, target = "cafe", value = "7" });
            var presenter = new Presenter(); var narrative = new NarrativeController(config, presenter);
            var wallet = new Wallet(); narrative.MoneyAccount = wallet;
            var stock = new NarrativeMapInventory(); stock.TryAdd("tea", 3);
            var action = new NarrativeActionDefinition { actionKey = "story", type = NarrativeActionType.Once,
                executionKind = NarrativeActionExecutionKind.Event, requirements = "Item(tea,2);Money(20);ActionPoint(1)",
                resultEvents = new List<string> { "first", "final" }, successResultOwnerId = "action-success" };
            var actions = new NarrativeActionController(narrative, new[] { action }, 5, stock);
            var completions = new List<bool>();

            Assert.That(actions.Execute("story", completions.Add, "story-1"), Is.True);
            Assert.That(stock.GetCount("tea"), Is.EqualTo(3));
            Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(5));
            Assert.That(stock.GetCount("token"), Is.Zero);
            presenter.Complete();
            Assert.That(presenter.Played, Is.EqualTo(new[] { "first", "final" }));
            Assert.That(stock.GetCount("tea"), Is.EqualTo(1));
            Assert.That(wallet.Balance, Is.EqualTo(80));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(stock.GetCount("token"), Is.EqualTo(1), "The extra result group waits for the final root.");
            Assert.That(actions.IsCompleted("story"), Is.False);
            Assert.That(completions, Is.Empty);
            presenter.Complete();
            Assert.That(stock.GetCount("token"), Is.EqualTo(6), "Both real roots and the extra group each settle once.");
            Assert.That(wallet.Balance, Is.EqualTo(87));
            Assert.That(actions.IsCompleted("story"), Is.True);
            Assert.That(completions, Is.EqualTo(new[] { true }));
            Assert.That(actions.Execute("story", operationId: "story-1"), Is.True);

            var restoredPresenter = new Presenter(); var restoredNarrative = new NarrativeController(config, restoredPresenter);
            restoredNarrative.MoneyAccount = wallet; restoredNarrative.RestoreSnapshot(narrative.CaptureSnapshot());
            var restoredStock = new NarrativeMapInventory();
            var restored = new NarrativeActionController(restoredNarrative, new[] { action }, 5, restoredStock);
            restored.RestoreJson(actions.CaptureJson());
            Assert.That(restored.Execute("story", operationId: "story-1"), Is.True);
            restored.ResumePendingExecution();
            Assert.That(restoredPresenter.Played, Is.Empty);
            Assert.That(presenter.Played, Is.EqualTo(new[] { "first", "final" }));
            Assert.That(restoredStock.GetCount("token"), Is.EqualTo(6));
            Assert.That(restoredStock.GetCount("tea"), Is.EqualTo(1));
            Assert.That(wallet.Balance, Is.EqualTo(87));
            Assert.That(restored.ActionPoints.Current, Is.EqualTo(4));
            var saved = new NarrativeActionStateSnapshot(); restored.CaptureInto(saved);
            Assert.That(saved.receipts, Has.Count.EqualTo(1));
        }

        [TestCase("Remove", 0)]
        [TestCase("RunEvents", 1)]
        public void ExpiryUsesOnlyExplicitExpiryPolicyAndNeverCompletesOnce(string policy, int expectedEvents)
        {
            var presenter = new Presenter();
            var action = new NarrativeActionDefinition { actionKey = "expiry", type = NarrativeActionType.Once, durationDays = 1,
                onExpirePolicy = policy, resultEvents = new List<string> { "EV_ACTION" }, onExpireEvents = new List<string> { "EV_ACTION_2" } };
            var actions = new NarrativeActionController(Controller(presenter), new[] { action });
            actions.Mount("market", "expiry"); actions.BeginDay(2);
            Assert.That(presenter.Played.Count, Is.EqualTo(expectedEvents));
            if (expectedEvents > 0)
            {
                Assert.That(presenter.Played, Is.EqualTo(new[] { "EV_ACTION_2" }));
                presenter.Complete();
            }
            Assert.That(actions.IsCompleted("expiry"), Is.False);
            Assert.That(actions.GetProgress("expiry"), Is.Zero);
            Assert.That(actions.IsVisible("expiry"), Is.False);
            actions.BeginDay(3);
            Assert.That(presenter.Played.Count, Is.EqualTo(expectedEvents));
        }

        [Test]
        public void FailedExpiryPersistsCursorAndRetriesOnlyUnfinishedEvents()
        {
            var presenter = new Presenter(); var narrative = Controller(presenter);
            var action = new NarrativeActionDefinition { actionKey = "expiry", type = NarrativeActionType.Once, durationDays = 1,
                onExpirePolicy = "RunEvents", onExpireEvents = new List<string> { "EV_ACTION", "EV_ACTION_2" } };
            var actions = new NarrativeActionController(narrative, new[] { action });
            actions.Mount("market", "expiry"); actions.BeginDay(2);
            presenter.Complete(); presenter.Cancel();
            Assert.That(actions.IsExecuting, Is.False, "An expiry must not block declared phase processing.");
            var save = new NarrativeActionStateSnapshot(); actions.CaptureInto(save);
            Assert.That(save.dueActions, Is.EqualTo(new[] { "expiry" }));
            Assert.That(save.pendingExpiryEvents, Is.EqualTo(new[] { "EV_ACTION_2" }));
            var restoredPresenter = new Presenter(); var restoredNarrative = Controller(restoredPresenter);
            restoredNarrative.RestoreSnapshot(narrative.CaptureSnapshot());
            var restored = new NarrativeActionController(restoredNarrative, new[] { action });
            restored.RestoreFrom(save); restored.ResumePendingExecution();
            Assert.That(restoredPresenter.Played, Is.EqualTo(new[] { "EV_ACTION_2" }));
            restoredPresenter.Complete(); restored.CaptureInto(save);
            Assert.That(save.dueActions, Is.Empty);
            Assert.That(restored.IsCompleted("expiry"), Is.False);
            restored.ResumePendingExecution(); restored.BeginDay(3);
            Assert.That(restoredPresenter.Played, Is.EqualTo(new[] { "EV_ACTION_2" }));
        }

        [Test]
        public void RejectedExpiryRetainsDueRecordUntilConditionsAllowRetry()
        {
            var config = new NarrativeConfiguration(); config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready" });
            config.events.Add(new NarrativeEventDefinition { eventId = "expiry" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "ready", ownerType = "event", ownerId = "expiry", condType = "BOOL_EQ", target = "ready", value = "true" });
            var presenter = new Presenter(); var narrative = new NarrativeController(config, presenter);
            var action = new NarrativeActionDefinition { actionKey = "timer", durationDays = 1, onExpirePolicy = "RunEvents", onExpireEvents = new List<string> { "expiry" } };
            var actions = new NarrativeActionController(narrative, new[] { action }); actions.Mount("market", "timer");
            LogAssert.Expect(LogType.Error, "[NCS][Action] Cannot queue expiry event expiry for action timer");
            actions.BeginDay(2);
            var save = new NarrativeActionStateSnapshot(); actions.CaptureInto(save);
            Assert.That(save.dueActions, Is.EqualTo(new[] { "timer" }));
            Assert.That(save.pendingExpiryEvents, Is.EqualTo(new[] { "expiry" }));
            narrative.State.SetCondition("ready", true); actions.ResumePendingExecution();
            Assert.That(presenter.Played, Is.EqualTo(new[] { "expiry" }));
            presenter.Complete(); actions.CaptureInto(save); Assert.That(save.dueActions, Is.Empty);
        }

        [Test]
        public void SequentialProviderMutationBeforeRejectionRestoresAllTouchedItems()
        {
            var inventory = new Inventory(); inventory.Set("tea", 3); inventory.Set("food", 4);
            inventory.AfterConsume = key => key != "food";
            var action = new NarrativeActionDefinition { actionKey = "cost", requirements = "Item(tea,2);Item(food,2);ActionPoint(1)" };
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] { action }, 5, inventory);
            Assert.That(actions.Execute("cost"), Is.False);
            Assert.That(inventory.GetCount("tea"), Is.EqualTo(3)); Assert.That(inventory.GetCount("food"), Is.EqualTo(4));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(5)); Assert.That(actions.IsExecuting, Is.False);
        }

        [Test]
        public void RejectedMoneyRestoresMapCostsAndNeverNotifiesPartialInventory()
        {
            var inventory = new NarrativeMapInventory(); inventory.TryAdd("tea", 3);
            var wallet = new Wallet { Reject = true }; var narrative = Controller(new Presenter()); narrative.MoneyAccount = wallet;
            var action = new NarrativeActionDefinition { actionKey = "cost", requirements = "Item(tea,2);Money(20);ActionPoint(1)" };
            var actions = new NarrativeActionController(narrative, new[] { action }, 5, inventory);
            inventory.Changed += () => Assert.That(inventory.GetCount("tea"), Is.EqualTo(3));
            Assert.That(actions.Execute("cost"), Is.False);
            Assert.That(inventory.GetCount("tea"), Is.EqualTo(3)); Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(5)); Assert.That(actions.IsExecuting, Is.False);
        }

        [Test]
        public void PointFailureAfterMoneyCommitRestoresMoneyItemsAndPoints()
        {
            var inventory = new NarrativeMapInventory(); inventory.TryAdd("tea", 3);
            var wallet = new Wallet(); var narrative = Controller(new Presenter()); narrative.MoneyAccount = wallet;
            var action = new NarrativeActionDefinition { actionKey = "cost", requirements = "Item(tea,2);Money(20);ActionPoint(1)" };
            var actions = new NarrativeActionController(narrative, new[] { action }, 5, inventory);
            wallet.OnCommit = () => actions.ActionPoints.Set(0);
            Assert.That(actions.Execute("cost"), Is.False);
            Assert.That(inventory.GetCount("tea"), Is.EqualTo(3)); Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(5)); Assert.That(actions.IsExecuting, Is.False);
        }

        [TestCase("Money(20);ActionPoint(1)", NarrativeResultType.ItemAdd, "tea", 2, 80, 5, 2)]
        [TestCase("Item(tea,2)", NarrativeResultType.MoneyAdd, "cafe", 20, 120, 3, 0)]
        [TestCase("Item(tea,2)", NarrativeResultType.ItemAdd, "food", 3, 100, 3, 3)]
        public void ExchangeCommitsFixedCostsAndOutputsAndReceiptSurvivesRestore(string costs, NarrativeResultType resultType,
            string target, int amount, int balance, int tea, int output)
        {
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "trade-result" });
            config.results.Add(new NarrativeResultDefinition { resultId = "reward", eventId = "trade-result", resultType = resultType, target = target, value = amount.ToString() });
            var presenter = new Presenter(); var narrative = new NarrativeController(config, presenter); var wallet = new Wallet(); narrative.MoneyAccount = wallet;
            var stock = new NarrativeMapInventory(); stock.TryAdd("tea", 5);
            var action = new NarrativeActionDefinition { actionKey = "trade", executionKind = NarrativeActionExecutionKind.Exchange, requirements = costs, successResultOwnerId = "trade-result" };
            var actions = new NarrativeActionController(narrative, new[] { action }, 5, stock);
            Assert.That(actions.Execute("trade", operationId: "trade-1"), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(balance));
            Assert.That(stock.GetCount("tea"), Is.EqualTo(tea + (target == "tea" ? output : 0)));
            if (target == "food") Assert.That(stock.GetCount("food"), Is.EqualTo(output));
            Assert.That(presenter.Played, Is.Empty);
            var snapshot = new NarrativeActionStateSnapshot(); actions.CaptureInto(snapshot);
            Assert.That(snapshot.receipts, Has.Count.EqualTo(1));
            var restoredStock = new NarrativeMapInventory();
            var restored = new NarrativeActionController(narrative, new[] { action }, 5, restoredStock); restored.RestoreFrom(snapshot);
            Assert.That(restored.Execute("trade", operationId: "trade-1"), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(balance)); Assert.That(restoredStock.GetCount("tea"), Is.EqualTo(stock.GetCount("tea")));
            Assert.That(restored.ActionPoints.Current, Is.EqualTo(costs.Contains("ActionPoint") ? 4 : 5));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExchangeOverflowOrWalletRejectionChangesNothing(bool rejectWallet)
        {
            var config = new NarrativeConfiguration(); config.events.Add(new NarrativeEventDefinition { eventId = "trade-result" });
            config.results.Add(new NarrativeResultDefinition { resultId = "reward", eventId = "trade-result", resultType = NarrativeResultType.ItemAdd, target = "tea", value = "1" });
            var narrative = new NarrativeController(config); var wallet = new Wallet { Reject = rejectWallet }; narrative.MoneyAccount = wallet;
            var stock = new NarrativeMapInventory(); stock.TryAdd("tea", rejectWallet ? 3 : int.MaxValue);
            int before = stock.GetCount("tea");
            var action = new NarrativeActionDefinition { actionKey = "trade", executionKind = NarrativeActionExecutionKind.Exchange, requirements = "Money(20);ActionPoint(1)", successResultOwnerId = "trade-result" };
            var actions = new NarrativeActionController(narrative, new[] { action }, 5, stock);
            Assert.That(actions.Execute("trade", operationId: "rejected"), Is.False);
            Assert.That(stock.GetCount("tea"), Is.EqualTo(before)); Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(5)); Assert.That(actions.IsExecuting, Is.False);
            var snapshot = new NarrativeActionStateSnapshot(); actions.CaptureInto(snapshot); Assert.That(snapshot.receipts, Is.Empty);
        }

        [Test]
        public void UnsupportedExchangeNeverConsumesCostsOrStartsSuccessEvent()
        {
            var presenter = new Presenter(); var narrative = Controller(presenter); var inventory = new Inventory(); inventory.Set("tea", 3);
            var action = new NarrativeActionDefinition { actionKey = "trade", executionKind = NarrativeActionExecutionKind.Exchange,
                requirements = "Item(tea,2);ActionPoint(1)", resultEvents = new List<string> { "EV_ACTION" } };
            var actions = new NarrativeActionController(narrative, new[] { action }, 5, inventory);
            Assert.That(actions.Execute("trade"), Is.False);
            Assert.That(inventory.GetCount("tea"), Is.EqualTo(3)); Assert.That(actions.ActionPoints.Current, Is.EqualTo(5));
            Assert.That(presenter.Played, Is.Empty); Assert.That(actions.IsExecuting, Is.False);
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
        public void NavigationAndOptedOutActionsHaveNoBadgeAndCardsUseConfiguredStableOrder()
        {
            var navigation = new NarrativeActionDefinition { actionKey = "navigate", executionKind = NarrativeActionExecutionKind.Navigate, navigationKey = "cafe.enter", sortOrder = 2 };
            var quiet = new NarrativeActionDefinition { actionKey = "quiet", notifyNew = false, sortOrder = 1 };
            var firstTie = new NarrativeActionDefinition { actionKey = "first", sortOrder = 1 };
            var secondTie = new NarrativeActionDefinition { actionKey = "second", sortOrder = 1 };
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] { navigation, quiet, firstTie, secondTie });
            actions.Mount("quiet", "navigate"); actions.Mount("quiet", "quiet");
            Assert.That(actions.HasUnseenActions("quiet"), Is.False);
            actions.Mount("place", "navigate"); actions.Mount("place", "second"); actions.Mount("place", "first");
            var shown = new List<NarrativeActionDefinition>(actions.GetDisplayableActions("place"));
            Assert.That(shown, Is.EqualTo(new[] { secondTie, firstTie, navigation }));
            Assert.That(actions.HasUnseenActions("place"), Is.True);
        }

        [Test]
        public void ExecutionConditionLocksButDoesNotHideAction()
        {
            var config = new NarrativeConfiguration();
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "qualified" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "qualification", ownerType = "actionExecute", ownerId = "locked", groupIndex = 1,
                condType = "BOOL_EQ", target = "qualified", value = "true" });
            var action = new NarrativeActionDefinition { actionKey = "locked", nameText = "action.locked.name" };
            var actions = new NarrativeActionController(new NarrativeController(config), new[] { action });
            Assert.That(actions.GetDisplayableActions("place"), Is.Empty);
            actions.Mount("place", "locked");
            Assert.That(actions.GetDisplayableActions("place"), Contains.Item(action));
            Assert.That(actions.IsAvailable("locked", out var checks), Is.False);
            Assert.That(checks.Exists(check => !check.satisfied), Is.True);
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

        [TestCase(NarrativeDeliveryMode.Cumulative, false, false)]
        [TestCase(NarrativeDeliveryMode.Cumulative, false, true)]
        [TestCase(NarrativeDeliveryMode.Cumulative, true, false)]
        [TestCase(NarrativeDeliveryMode.Cumulative, true, true)]
        [TestCase(NarrativeDeliveryMode.Consecutive, false, false)]
        [TestCase(NarrativeDeliveryMode.Consecutive, false, true)]
        [TestCase(NarrativeDeliveryMode.Consecutive, true, false)]
        [TestCase(NarrativeDeliveryMode.Consecutive, true, true)]
        public void DeliveryFinalEventRetryAfterCancellationOrFailureDoesNotRepayOrRecount(
            NarrativeDeliveryMode mode, bool transactionFailure, bool restore)
        {
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "EV_ACTION", entry = NarrativeEntryPoint.MapAction, onceOnly = true });
            config.events.Add(new NarrativeEventDefinition { eventId = "EV_ACTION_2", entry = NarrativeEntryPoint.MapAction, onceOnly = true });
            config.results.Add(new NarrativeResultDefinition { resultId = "first-output", eventId = "EV_ACTION", resultType = NarrativeResultType.ItemAdd, target = "token", value = "1" });
            config.results.Add(new NarrativeResultDefinition { resultId = "final-money", eventId = "EV_ACTION_2", resultType = NarrativeResultType.MoneyAdd, target = "cafe", value = "10" });
            config.results.Add(new NarrativeResultDefinition { resultId = "final-output", eventId = "EV_ACTION_2", resultType = NarrativeResultType.ItemAdd, target = "reward", value = "1" });
            var presenter = new Presenter(); var narrative = new NarrativeController(config, presenter);
            var wallet = new Wallet(); narrative.MoneyAccount = wallet;
            var stock = Stock(4, 2); var definition = Delivery(mode, 2);
            definition.requirements += ";Money(20)";
            definition.resultEvents = new List<string> { "EV_ACTION", "EV_ACTION_2" };
            var actions = new NarrativeActionController(narrative, new[] { definition }, 5, stock);
            var completions = new List<bool>();
            Assert.That(actions.Execute("delivery"), Is.True);
            Assert.That(actions.GetProgress("delivery"), Is.EqualTo(1));
            Assert.That(presenter.Played, Is.Empty);
            actions.BeginDay(2);
            Assert.That(actions.Execute("delivery", completions.Add), Is.True);
            Assert.That(actions.GetProgress("delivery"), Is.EqualTo(2));
            Assert.That(actions.IsCompleted("delivery"), Is.False, "Reaching the target does not complete an unfinished final chain.");
            presenter.Complete();
            Assert.That(presenter.Played, Is.EqualTo(new[] { "EV_ACTION", "EV_ACTION_2" }));
            Assert.That(actions.IsCompleted("delivery"), Is.False);
            if (transactionFailure)
            {
                wallet.Reject = true;
                LogAssert.Expect(LogType.Error, "[NCS] Event EV_ACTION_2 result transaction failed: Money batch rejected: injected_rejection");
                presenter.Complete();
            }
            else presenter.Cancel();

            Assert.That(completions, Is.EqualTo(new[] { false }));
            Assert.That(actions.IsExecuting, Is.False, "A paused delivery releases the action operation.");
            Assert.That(narrative.IsBusy, Is.False);
            Assert.That(actions.IsCompleted("delivery"), Is.False);
            Assert.That(actions.IsAvailable("delivery", out _), Is.True, "Retry is available with no remaining cost stock and a submission today.");
            Assert.That(actions.HasSubmittedToday("delivery"), Is.True);
            Assert.That(actions.GetProgress("delivery"), Is.EqualTo(2));
            Assert.That(stock.GetCount("medicine"), Is.Zero);
            Assert.That(stock.GetCount("food"), Is.Zero);
            Assert.That(stock.GetCount("token"), Is.EqualTo(1));
            Assert.That(stock.GetCount("reward"), Is.Zero);
            Assert.That(wallet.Balance, Is.EqualTo(60));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(actions.CanCaptureSnapshot, Is.True);
            var saved = new NarrativeActionStateSnapshot(); actions.CaptureInto(saved);
            Assert.That(saved.executingAction, Is.EqualTo("delivery"));
            Assert.That(saved.currentEvent, Is.Null.Or.Empty);
            Assert.That(saved.pendingEvents, Is.EqualTo(new[] { "EV_ACTION_2" }));

            wallet.Reject = false;
            if (restore)
            {
                var restoredPresenter = new Presenter(); var restoredNarrative = new NarrativeController(config, restoredPresenter);
                restoredNarrative.MoneyAccount = wallet; restoredNarrative.RestoreSnapshot(narrative.CaptureSnapshot());
                var restoredStock = new NarrativeMapInventory();
                var restored = new NarrativeActionController(restoredNarrative, new[] { definition }, 5, restoredStock);
                restored.RestoreFrom(saved); restored.RefreshForDay(2);
                actions = restored; narrative = restoredNarrative; presenter = restoredPresenter; stock = restoredStock;
                Assert.That(actions.IsExecuting, Is.False);
                Assert.That(actions.IsCompleted("delivery"), Is.False);
                Assert.That(actions.IsAvailable("delivery", out _), Is.True);
                Assert.That(presenter.Played, Is.Empty, "Restoring a paused cursor waits for an explicit retry.");
            }
            Assert.That(actions.Execute("delivery", completions.Add), Is.True, "Execute on the same action key resumes the committed submission.");
            Assert.That(presenter.Played, Is.EqualTo(restore
                ? new[] { "EV_ACTION_2" } : new[] { "EV_ACTION", "EV_ACTION_2", "EV_ACTION_2" }));
            Assert.That(actions.IsExecuting, Is.True);
            Assert.That(actions.IsCompleted("delivery"), Is.False);
            presenter.Complete();
            Assert.That(completions, Is.EqualTo(new[] { false, true }));
            Assert.That(actions.IsExecuting, Is.False);
            Assert.That(actions.IsCompleted("delivery"), Is.True);
            Assert.That(actions.GetProgress("delivery"), Is.EqualTo(2));
            Assert.That(stock.GetCount("medicine"), Is.Zero);
            Assert.That(stock.GetCount("food"), Is.Zero);
            Assert.That(stock.GetCount("token"), Is.EqualTo(1));
            Assert.That(stock.GetCount("reward"), Is.EqualTo(1));
            Assert.That(wallet.Balance, Is.EqualTo(70));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(actions.Execute("delivery"), Is.False);
            actions.ResumePendingExecution();
            actions.CaptureInto(saved);
            Assert.That(saved.executingAction, Is.Null.Or.Empty);
            Assert.That(saved.pendingEvents, Is.Empty);
            Assert.That(presenter.Played.Count, Is.EqualTo(restore ? 1 : 3));
        }

        [Test]
        public void DeliveryIndependentSuccessOwnerCommitsWithLastRealRootAndRetriesOnlyThatRoot()
        {
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "first", entry = NarrativeEntryPoint.MapAction, onceOnly = true });
            config.events.Add(new NarrativeEventDefinition { eventId = "final", entry = NarrativeEntryPoint.MapAction, onceOnly = true });
            config.results.Add(new NarrativeResultDefinition { resultId = "first-output", eventId = "first", resultType = NarrativeResultType.ItemAdd, target = "token", value = "1" });
            config.results.Add(new NarrativeResultDefinition { resultId = "final-output", eventId = "final", resultType = NarrativeResultType.ItemAdd, target = "token", value = "2" });
            config.results.Add(new NarrativeResultDefinition { resultId = "owner-output", eventId = "delivery-success", resultType = NarrativeResultType.ItemAdd, target = "token", value = "3" });
            config.results.Add(new NarrativeResultDefinition { resultId = "owner-money", eventId = "delivery-success", resultType = NarrativeResultType.MoneyAdd, target = "cafe", value = "7" });
            var presenter = new Presenter(); var narrative = new NarrativeController(config, presenter);
            var wallet = new Wallet(); narrative.MoneyAccount = wallet;
            var stock = Stock(4, 2); var definition = Delivery(NarrativeDeliveryMode.Cumulative, 2);
            definition.successResultOwnerId = "delivery-success";
            definition.resultEvents = new List<string> { "first", "final" };
            var actions = new NarrativeActionController(narrative, new[] { definition }, 5, stock);
            Assert.That(actions.Execute("delivery", operationId: "day-1"), Is.True);
            Assert.That(stock.GetCount("token"), Is.Zero, "Nonfinal submissions do not run the success owner.");
            Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(presenter.Played, Is.Empty);
            actions.BeginDay(2);
            Assert.That(actions.Execute("delivery", operationId: "day-2"), Is.True);
            presenter.Complete();
            Assert.That(stock.GetCount("token"), Is.EqualTo(1));
            Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(actions.IsCompleted("delivery"), Is.False);
            wallet.Reject = true;
            LogAssert.Expect(LogType.Error, "[NCS] Event final result transaction failed: Money batch rejected: injected_rejection");
            presenter.Complete();
            Assert.That(stock.GetCount("token"), Is.EqualTo(1), "Owner failure must also roll back the final root's item output.");
            Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(actions.IsCompleted("delivery"), Is.False);
            Assert.That(actions.IsExecuting, Is.False);
            Assert.That(actions.IsAvailable("delivery", out _), Is.True);
            var snapshot = new NarrativeActionStateSnapshot(); actions.CaptureInto(snapshot);
            Assert.That(snapshot.submissionRecorded, Is.True);
            Assert.That(snapshot.successResultsCommitted, Is.False);
            Assert.That(snapshot.pendingEvents, Is.EqualTo(new[] { "final" }));
            wallet.Reject = false;
            Assert.That(actions.Execute("delivery", operationId: "day-2"), Is.True);
            presenter.Complete();
            Assert.That(presenter.Played, Is.EqualTo(new[] { "first", "final", "final" }), "The result owner has no synthetic event presentation.");
            Assert.That(stock.GetCount("token"), Is.EqualTo(6));
            Assert.That(wallet.Balance, Is.EqualTo(107));
            Assert.That(stock.GetCount("medicine"), Is.Zero);
            Assert.That(stock.GetCount("food"), Is.Zero);
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(actions.GetProgress("delivery"), Is.EqualTo(2));
            Assert.That(actions.IsCompleted("delivery"), Is.True);
            Assert.That(actions.Execute("delivery", operationId: "day-2"), Is.True);
            actions.ResumePendingExecution();
            Assert.That(stock.GetCount("token"), Is.EqualTo(6));
            Assert.That(wallet.Balance, Is.EqualTo(107));
            Assert.That(presenter.Played, Has.Count.EqualTo(3));
        }

        [TestCase(NarrativeDeliveryMode.Cumulative, false)]
        [TestCase(NarrativeDeliveryMode.Cumulative, true)]
        [TestCase(NarrativeDeliveryMode.Consecutive, false)]
        [TestCase(NarrativeDeliveryMode.Consecutive, true)]
        public void DeliveryWithoutRootRetainsFailedSuccessOwnerForRetryWithoutRepayingOrRecounting(
            NarrativeDeliveryMode mode, bool restore)
        {
            var config = new NarrativeConfiguration();
            config.results.Add(new NarrativeResultDefinition { resultId = "owner-item", eventId = "delivery-success", resultType = NarrativeResultType.ItemAdd, target = "reward", value = "3" });
            config.results.Add(new NarrativeResultDefinition { resultId = "owner-money", eventId = "delivery-success", resultType = NarrativeResultType.MoneyAdd, target = "cafe", value = "7" });
            var presenter = new Presenter(); var narrative = new NarrativeController(config, presenter);
            var wallet = new Wallet(); narrative.MoneyAccount = wallet;
            var stock = Stock(4, 2); var definition = Delivery(mode, 2);
            definition.resultEvents.Clear(); definition.successResultOwnerId = "delivery-success";
            var actions = new NarrativeActionController(narrative, new[] { definition }, 5, stock);
            Assert.That(actions.Execute("delivery", operationId: "day-1"), Is.True);
            Assert.That(stock.GetCount("reward"), Is.Zero);
            Assert.That(wallet.Balance, Is.EqualTo(100));
            actions.BeginDay(2);
            var completions = new List<bool>();
            // Submission has only item/point costs; the rejecting wallet is touched by the final owner.
            wallet.Reject = true;
            Assert.That(actions.Execute("delivery", completions.Add, "day-2"), Is.True);
            Assert.That(completions, Is.EqualTo(new[] { false }));
            Assert.That(stock.GetCount("medicine"), Is.Zero);
            Assert.That(stock.GetCount("food"), Is.Zero);
            Assert.That(stock.GetCount("reward"), Is.Zero);
            Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(actions.GetProgress("delivery"), Is.EqualTo(2));
            Assert.That(actions.HasSubmittedToday("delivery"), Is.True);
            Assert.That(actions.IsCompleted("delivery"), Is.False);
            Assert.That(actions.IsExecuting, Is.False);
            Assert.That(actions.IsAvailable("delivery", out _), Is.True);
            Assert.That(actions.CanCaptureSnapshot, Is.True);
            var saved = new NarrativeActionStateSnapshot(); actions.CaptureInto(saved);
            Assert.That(saved.executingAction, Is.EqualTo("delivery"));
            Assert.That(saved.currentEvent, Is.Null.Or.Empty);
            Assert.That(saved.pendingEvents, Is.Empty, "An owner-only failure still retains an executable recovery cursor.");
            Assert.That(saved.submissionRecorded, Is.True);
            Assert.That(saved.successResultsCommitted, Is.False);
            Assert.That(saved.receipts.Find(receipt => receipt.operationId == "day-2").submissionOnly, Is.True);
            if (restore)
            {
                var restoredPresenter = new Presenter(); var restoredNarrative = new NarrativeController(config, restoredPresenter);
                restoredNarrative.MoneyAccount = wallet; restoredNarrative.RestoreSnapshot(narrative.CaptureSnapshot());
                var restoredStock = new NarrativeMapInventory();
                var restored = new NarrativeActionController(restoredNarrative, new[] { definition }, 5, restoredStock);
                restored.RestoreJson(actions.CaptureJson()); restored.RefreshForDay(2);
                actions = restored; presenter = restoredPresenter; stock = restoredStock;
                Assert.That(actions.IsCompleted("delivery"), Is.False);
                Assert.That(actions.IsAvailable("delivery", out _), Is.True);
                Assert.That(actions.IsExecuting, Is.False);
            }
            // Reusing the submission receipt must retry its unfinished owner rather than report success.
            Assert.That(actions.Execute("delivery", completions.Add, "day-2"), Is.True);
            Assert.That(completions, Is.EqualTo(new[] { false, false }));
            Assert.That(actions.GetProgress("delivery"), Is.EqualTo(2));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(actions.IsCompleted("delivery"), Is.False);
            wallet.Reject = false;
            Assert.That(actions.Execute("delivery", completions.Add, "day-2"), Is.True);
            Assert.That(completions, Is.EqualTo(new[] { false, false, true }));
            Assert.That(actions.IsCompleted("delivery"), Is.True);
            Assert.That(actions.IsExecuting, Is.False);
            Assert.That(actions.GetProgress("delivery"), Is.EqualTo(2));
            Assert.That(stock.GetCount("medicine"), Is.Zero);
            Assert.That(stock.GetCount("food"), Is.Zero);
            Assert.That(stock.GetCount("reward"), Is.EqualTo(3));
            Assert.That(wallet.Balance, Is.EqualTo(107));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(actions.Execute("delivery", operationId: "day-2"), Is.True);
            actions.ResumePendingExecution();
            Assert.That(stock.GetCount("reward"), Is.EqualTo(3));
            Assert.That(wallet.Balance, Is.EqualTo(107));
            Assert.That(presenter.Played, Is.Empty);
            actions.CaptureInto(saved);
            Assert.That(saved.executingAction, Is.Null.Or.Empty);
            Assert.That(saved.receipts.Find(receipt => receipt.operationId == "day-2").submissionOnly, Is.False);
        }

        [Test]
        public void ConsecutiveMilestoneDoesNotReplayAfterRestoreAndGapAndCompletionSurvivesLaterDays()
        {
            var presenter = new Presenter(); var definition = Delivery(NarrativeDeliveryMode.Consecutive);
            definition.milestones.Add(new NarrativeActionMilestoneDefinition { milestoneId = "m2", actionKey = "delivery", atProgress = 2, eventId = "EV_ACTION" });
            var narrative = Controller(presenter);
            var actions = new NarrativeActionController(narrative, new[] { definition }, 5, Stock());
            Assert.That(actions.Execute("delivery"), Is.True); actions.BeginDay(2);
            Assert.That(actions.Execute("delivery"), Is.True);
            Assert.That(presenter.Played, Is.EqualTo(new[] { "EV_ACTION" })); presenter.Complete();
            string saved = actions.CaptureJson();
            var restoredPresenter = new Presenter(); var restoredNarrative = Controller(restoredPresenter);
            restoredNarrative.RestoreSnapshot(narrative.CaptureSnapshot());
            var restored = new NarrativeActionController(restoredNarrative, new[] { definition }, 5, new NarrativeMapInventory());
            restored.RestoreJson(saved); restored.ResumePendingExecution();
            Assert.That(restoredPresenter.Played, Is.Empty, "An already completed milestone does not resume after restore.");
            restored.BeginDay(4); Assert.That(restored.GetProgress("delivery"), Is.Zero);
            Assert.That(restored.Execute("delivery"), Is.True); restored.BeginDay(5);
            Assert.That(restored.Execute("delivery"), Is.True);
            Assert.That(restored.GetProgress("delivery"), Is.EqualTo(2));
            Assert.That(restoredPresenter.Played, Is.Empty, "Crossing the saved milestone again cannot replay it after a streak reset.");
            restored.BeginDay(6); Assert.That(restored.Execute("delivery"), Is.True);
            Assert.That(restored.IsCompleted("delivery"), Is.False);
            Assert.That(restoredPresenter.Played, Is.EqualTo(new[] { "EV_ACTION_2" })); restoredPresenter.Complete();
            restored.BeginDay(10);
            Assert.That(restored.IsCompleted("delivery"), Is.True);
            Assert.That(restored.GetProgress("delivery"), Is.EqualTo(3));
            Assert.That(restored.Execute("delivery"), Is.False);
            Assert.That(restoredPresenter.Played, Is.EqualTo(new[] { "EV_ACTION_2" }));
            var snapshot = new NarrativeActionStateSnapshot(); restored.CaptureInto(snapshot);
            Assert.That(snapshot.deliveries[0].triggeredMilestones, Is.EqualTo(new[] { "m2" }));
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
        public void MigratingUndatedProgressPreservesKnownDeliveryProgressAndCompletedRetirement()
        {
            var delivery = Delivery(NarrativeDeliveryMode.Cumulative);
            var ordinary = new NarrativeActionDefinition { actionKey = "ordinary", type = NarrativeActionType.Progress, progressTarget = 8 };
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] { delivery, ordinary }, 5, Stock());
            LogAssert.Expect(LogType.Warning, "[NCS][Action] Preserved undated legacy delivery progress: delivery; consecutive reset begins after the next dated submission.");
            actions.RestoreFrom(new NarrativeActionStateSnapshot { counters = new List<NarrativeActionCounterValue> {
                new NarrativeActionCounterValue { key = "delivery", value = 2 }, new NarrativeActionCounterValue { key = "ordinary", value = 4 } } });
            Assert.That(actions.GetProgress("delivery"), Is.EqualTo(2));
            Assert.That(actions.GetProgress("ordinary"), Is.EqualTo(4));
            actions.RefreshForDay(8);
            Assert.That(actions.GetProgress("delivery"), Is.EqualTo(2));
            var retired = new NarrativeActionStateSnapshot { retired = new List<string> { "delivery" }, counters = new List<NarrativeActionCounterValue> {
                new NarrativeActionCounterValue { key = "delivery", value = delivery.progressTarget } }, daily = new List<NarrativeActionDayValue> {
                new NarrativeActionDayValue { key = "delivery", day = 2 } } };
            actions.RestoreFrom(retired);
            Assert.That(actions.IsCompleted("delivery"), Is.True);
            Assert.That(actions.IsVisible("delivery"), Is.False);
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

        [TestCase(false)]
        [TestCase(true)]
        public void ProgressNonfinalReceiptIsIdempotentAndFinalRootFailureRemainsIncomplete(bool restore)
        {
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "first", entry = NarrativeEntryPoint.MapAction, onceOnly = true });
            config.events.Add(new NarrativeEventDefinition { eventId = "final", entry = NarrativeEntryPoint.MapAction, onceOnly = true });
            config.results.Add(new NarrativeResultDefinition { resultId = "first-output", eventId = "first", resultType = NarrativeResultType.ItemAdd, target = "token", value = "1" });
            config.results.Add(new NarrativeResultDefinition { resultId = "final-output", eventId = "final", resultType = NarrativeResultType.ItemAdd, target = "reward", value = "1" });
            config.results.Add(new NarrativeResultDefinition { resultId = "owner-money", eventId = "progress-success", resultType = NarrativeResultType.MoneyAdd, target = "cafe", value = "7" });
            var presenter = new Presenter(); var narrative = new NarrativeController(config, presenter);
            var wallet = new Wallet(); narrative.MoneyAccount = wallet;
            var stock = new NarrativeMapInventory(); stock.TryAdd("tea", 4);
            var definition = new NarrativeActionDefinition { actionKey = "project", type = NarrativeActionType.Progress, progressTarget = 2,
                requirements = "Item(tea,2);Money(20);ActionPoint(1)", successResultOwnerId = "progress-success",
                resultEvents = new List<string> { "first", "final" } };
            var actions = new NarrativeActionController(narrative, new[] { definition }, 5, stock);
            var completions = new List<bool>();
            Assert.That(actions.Execute("project", completions.Add, "submission-1"), Is.True);
            Assert.That(actions.Execute("project", completions.Add, "submission-1"), Is.True);
            Assert.That(completions, Is.EqualTo(new[] { true, true }));
            Assert.That(actions.GetProgress("project"), Is.EqualTo(1));
            Assert.That(stock.GetCount("tea"), Is.EqualTo(2));
            Assert.That(wallet.Balance, Is.EqualTo(80));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(presenter.Played, Is.Empty);
            var saved = new NarrativeActionStateSnapshot(); actions.CaptureInto(saved);
            Assert.That(saved.receipts, Has.Count.EqualTo(1));
            Assert.That(saved.receipts[0].operationId, Is.EqualTo("submission-1"));
            if (restore)
            {
                var restoredPresenter = new Presenter(); var restoredNarrative = new NarrativeController(config, restoredPresenter);
                restoredNarrative.MoneyAccount = wallet; restoredNarrative.RestoreSnapshot(narrative.CaptureSnapshot());
                var restoredStock = new NarrativeMapInventory();
                var restored = new NarrativeActionController(restoredNarrative, new[] { definition }, 5, restoredStock);
                restored.RestoreJson(actions.CaptureJson());
                actions = restored; narrative = restoredNarrative; presenter = restoredPresenter; stock = restoredStock;
                Assert.That(actions.Execute("project", operationId: "submission-1"), Is.True);
                Assert.That(actions.GetProgress("project"), Is.EqualTo(1));
                Assert.That(stock.GetCount("tea"), Is.EqualTo(2));
                Assert.That(wallet.Balance, Is.EqualTo(80));
                Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
                Assert.That(presenter.Played, Is.Empty);
            }
            completions.Clear();
            Assert.That(actions.Execute("project", completions.Add, "submission-2"), Is.True);
            presenter.Complete();
            Assert.That(stock.GetCount("tea"), Is.Zero);
            Assert.That(wallet.Balance, Is.EqualTo(60));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(3));
            Assert.That(actions.GetProgress("project"), Is.EqualTo(1), "The staged final submission is recorded only after all roots succeed.");
            wallet.Reject = true;
            LogAssert.Expect(LogType.Error, "[NCS] Event final result transaction failed: Money batch rejected: injected_rejection");
            presenter.Complete();
            Assert.That(completions, Is.EqualTo(new[] { false }));
            Assert.That(actions.IsCompleted("project"), Is.False);
            Assert.That(actions.IsExecuting, Is.False);
            Assert.That(actions.IsAvailable("project", out _), Is.True);
            Assert.That(stock.GetCount("reward"), Is.Zero);
            Assert.That(stock.GetCount("token"), Is.EqualTo(1));
            Assert.That(actions.CanCaptureSnapshot, Is.True);
            actions.CaptureInto(saved);
            Assert.That(saved.rootCostCommitted, Is.True);
            Assert.That(saved.submissionRecorded, Is.False);
            Assert.That(saved.successResultsCommitted, Is.False);
            Assert.That(saved.pendingEvents, Is.EqualTo(new[] { "final" }));
            wallet.Reject = false;
            Assert.That(actions.Execute("project", completions.Add, "submission-2"), Is.True);
            Assert.That(actions.IsCompleted("project"), Is.False);
            presenter.Complete();
            Assert.That(completions, Is.EqualTo(new[] { false, true }));
            Assert.That(actions.GetProgress("project"), Is.EqualTo(2));
            Assert.That(actions.IsCompleted("project"), Is.True);
            Assert.That(stock.GetCount("tea"), Is.Zero);
            Assert.That(stock.GetCount("token"), Is.EqualTo(1));
            Assert.That(stock.GetCount("reward"), Is.EqualTo(1));
            Assert.That(wallet.Balance, Is.EqualTo(67));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(3));
            Assert.That(actions.Execute("project", operationId: "submission-1"), Is.True);
            Assert.That(actions.Execute("project", operationId: "submission-2"), Is.True);
            Assert.That(actions.GetProgress("project"), Is.EqualTo(2));
            Assert.That(stock.GetCount("reward"), Is.EqualTo(1));
            Assert.That(wallet.Balance, Is.EqualTo(67));
            Assert.That(presenter.Played, Is.EqualTo(new[] { "first", "final", "final" }));
            actions.CaptureInto(saved);
            Assert.That(saved.receipts, Has.Count.EqualTo(2));
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
