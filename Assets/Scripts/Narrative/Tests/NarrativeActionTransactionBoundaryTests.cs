using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeActionTransactionBoundaryTests
    {
        private sealed class Presenter : INarrativePresenter
        {
            public readonly List<string> Played = new List<string>();
            public Action Completion;
            public void Play(NarrativeEventRequest request, Action completed)
            {
                Played.Add(request.Event.eventId);
                Completion = completed;
            }
            public void Complete() => Completion();
        }

        private sealed class Wallet : INarrativeMoneyAccount, INarrativeMoneyNotificationScope
        {
            private int notificationDepth;
            private int notificationStart;
            public int Balance = 100;
            public int CommitCount;
            public Action OnCommit;
            public event Action Changed;

            public bool TryRead(out int value) { value = Balance; return true; }
            public bool TryCommit(int expected, int final, out string error)
            {
                error = null;
                if (Balance != expected || final < 0) { error = "compare_failed"; return false; }
                int before = Balance;
                Balance = final;
                CommitCount++;
                OnCommit?.Invoke();
                if (notificationDepth == 0 && before != Balance) Changed?.Invoke();
                return true;
            }
            public IDisposable DeferNotifications()
            {
                if (notificationDepth++ == 0) notificationStart = Balance;
                return new Scope(this);
            }
            private sealed class Scope : IDisposable
            {
                private Wallet owner;
                public Scope(Wallet owner) { this.owner = owner; }
                public void Dispose()
                {
                    if (owner == null) return;
                    var target = owner;
                    owner = null;
                    if (--target.notificationDepth == 0 && target.notificationStart != target.Balance)
                        target.Changed?.Invoke();
                }
            }
        }

        private static NarrativeConfiguration Configuration()
        {
            var config = new NarrativeConfiguration();
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "settled" });
            config.events.Add(new NarrativeEventDefinition { eventId = "root", entry = NarrativeEntryPoint.MapAction, onceOnly = true });
            config.results.Add(new NarrativeResultDefinition { resultId = "root-item", eventId = "root", resultType = NarrativeResultType.ItemAdd, target = "reward", value = "2" });
            config.results.Add(new NarrativeResultDefinition { resultId = "root-flag", eventId = "root", resultType = NarrativeResultType.SetFlag, target = "settled", value = "true" });
            config.results.Add(new NarrativeResultDefinition { resultId = "success-item", eventId = "success", resultType = NarrativeResultType.ItemAdd, target = "reward", value = "3" });
            config.results.Add(new NarrativeResultDefinition { resultId = "success-money", eventId = "success", resultType = NarrativeResultType.MoneyAdd, target = "cafe", value = "7" });
            return config;
        }

        private static NarrativeActionDefinition Story(bool staged = true)
            => new NarrativeActionDefinition
            {
                actionKey = "story", type = NarrativeActionType.Once,
                executionKind = NarrativeActionExecutionKind.Event,
                requirements = "Item(tea,2);Money(20);ActionPoint(1)",
                resultEvents = staged ? new List<string> { "root" } : new List<string>(),
                successResultOwnerId = staged ? "success" : null
            };

        private static NarrativeMapInventory Stock()
        {
            var stock = new NarrativeMapInventory();
            stock.TryAdd("tea", 3);
            return stock;
        }

        private static NarrativeActionStateSnapshot Capture(NarrativeActionController actions)
        {
            var snapshot = new NarrativeActionStateSnapshot();
            actions.CaptureInto(snapshot);
            return snapshot;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WalletMutationThatInvalidatesPointsRejectsBothImmediateAndStagedCostsWithoutSettlement(bool staged)
        {
            var presenter = new Presenter();
            var narrative = new NarrativeController(Configuration(), presenter);
            var wallet = new Wallet();
            narrative.MoneyAccount = wallet;
            var stock = Stock();
            var actions = new NarrativeActionController(narrative, new[] { Story(staged) }, 5, stock);
            var completions = new List<bool>();
            int inventoryNotifications = 0, pointNotifications = 0, moneyNotifications = 0;
            stock.Changed += () => inventoryNotifications++;
            actions.ActionPoints.Changed += (_, __) => pointNotifications++;
            wallet.Changed += () => moneyNotifications++;
            wallet.OnCommit = () => actions.ActionPoints.Set(0);

            Assert.That(actions.Execute("story", completions.Add, "operation"), Is.EqualTo(staged));
            if (staged)
            {
                Assert.That(wallet.CommitCount, Is.Zero, "Presentation must leave payment staged.");
                LogAssert.Expect(LogType.Error, "[NCS] Event root result transaction failed: Action points changed during wallet commit.");
                presenter.Complete();
            }

            Assert.That(stock.GetCount("tea"), Is.EqualTo(3));
            Assert.That(stock.GetCount("reward"), Is.Zero);
            Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(5));
            Assert.That(narrative.State.HasHappenedEvent("root"), Is.False);
            Assert.That(narrative.State.GetCondition("settled"), Is.False);
            Assert.That(actions.IsCompleted("story"), Is.False);
            Assert.That(actions.GetProgress("story"), Is.Zero);
            Assert.That(actions.Receipts, Is.Empty);
            Assert.That(actions.IsExecuting, Is.False);
            Assert.That(narrative.IsBusy, Is.False);
            Assert.That(completions, Is.EqualTo(staged ? new[] { false } : new bool[0]));
            Assert.That(inventoryNotifications, Is.Zero);
            Assert.That(pointNotifications, Is.Zero);
            Assert.That(moneyNotifications, Is.Zero);
            var rejected = Capture(actions);
            Assert.That(rejected.receipts, Is.Empty);
            if (staged)
            {
                Assert.That(rejected.rootCostCommitted, Is.False);
                Assert.That(rejected.pendingEvents, Is.EqualTo(new[] { "root" }));
            }

            wallet.OnCommit = null;
            Assert.That(actions.Execute("story", completions.Add, "operation"), Is.True);
            if (staged) presenter.Complete();
            Assert.That(actions.IsCompleted("story"), Is.True);
            Assert.That(stock.GetCount("tea"), Is.EqualTo(1));
            Assert.That(stock.GetCount("reward"), Is.EqualTo(staged ? 5 : 0));
            Assert.That(wallet.Balance, Is.EqualTo(staged ? 87 : 80));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(actions.Receipts, Has.Count.EqualTo(1));
            Assert.That(completions, Is.EqualTo(staged ? new[] { false, true } : new[] { true }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ListenerExceptionAfterStagedRootCommitStillCompletesOnceAndCannotReplay(bool throwFromPoints)
        {
            var config = Configuration();
            var presenter = new Presenter();
            var narrative = new NarrativeController(config, presenter);
            var wallet = new Wallet();
            narrative.MoneyAccount = wallet;
            var stock = Stock();
            var definition = Story();
            var actions = new NarrativeActionController(narrative, new[] { definition }, 5, stock);
            var completions = new List<bool>();
            var finishes = new List<bool>();
            int eventCompletions = 0, inventoryNotifications = 0, pointNotifications = 0;
            narrative.EventCompleted += _ => eventCompletions++;
            actions.ActionFinished += (_, success) => finishes.Add(success);
            stock.Changed += () =>
            {
                inventoryNotifications++;
                if (!throwFromPoints) throw new InvalidOperationException("inventory observer failed");
            };
            actions.ActionPoints.Changed += (_, __) =>
            {
                pointNotifications++;
                if (throwFromPoints) throw new InvalidOperationException("points observer failed");
            };

            Assert.That(actions.Execute("story", completions.Add, "operation"), Is.True);
            Action staleCompletion = presenter.Completion;
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: "
                + (throwFromPoints ? "points" : "inventory") + " observer failed"));
            presenter.Complete();

            Assert.That(completions, Is.EqualTo(new[] { true }));
            Assert.That(finishes, Is.EqualTo(new[] { true }));
            Assert.That(eventCompletions, Is.EqualTo(1));
            Assert.That(actions.IsCompleted("story"), Is.True);
            Assert.That(actions.IsExecuting, Is.False);
            Assert.That(narrative.IsBusy, Is.False);
            Assert.That(narrative.State.HasHappenedEvent("root"), Is.True);
            Assert.That(narrative.State.GetCondition("settled"), Is.True);
            Assert.That(stock.GetCount("tea"), Is.EqualTo(1));
            Assert.That(stock.GetCount("reward"), Is.EqualTo(5));
            Assert.That(wallet.Balance, Is.EqualTo(87));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(inventoryNotifications, Is.EqualTo(1));
            Assert.That(pointNotifications, Is.EqualTo(1));
            var saved = Capture(actions);
            Assert.That(saved.executingAction, Is.Null.Or.Empty);
            Assert.That(saved.currentEvent, Is.Null.Or.Empty);
            Assert.That(saved.pendingEvents, Is.Empty);
            Assert.That(saved.receipts, Has.Count.EqualTo(1));
            Assert.That(saved.receipts[0].operationId, Is.EqualTo("operation"));
            Assert.That(saved.receipts[0].submissionOnly, Is.False);

            staleCompletion();
            Assert.That(actions.Execute("story", operationId: "operation"), Is.True);
            actions.ResumePendingExecution();
            var restoredPresenter = new Presenter();
            var restoredNarrative = new NarrativeController(config, restoredPresenter);
            restoredNarrative.MoneyAccount = wallet;
            restoredNarrative.RestoreSnapshot(narrative.CaptureSnapshot());
            var restoredStock = new NarrativeMapInventory();
            var restored = new NarrativeActionController(restoredNarrative, new[] { definition }, 5, restoredStock);
            restored.RestoreFrom(saved);
            Assert.That(restored.Execute("story", operationId: "operation"), Is.True);
            restored.ResumePendingExecution();

            Assert.That(presenter.Played, Is.EqualTo(new[] { "root" }));
            Assert.That(restoredPresenter.Played, Is.Empty);
            Assert.That(completions, Is.EqualTo(new[] { true }));
            Assert.That(finishes, Is.EqualTo(new[] { true }));
            Assert.That(eventCompletions, Is.EqualTo(1));
            Assert.That(wallet.CommitCount, Is.EqualTo(1));
            Assert.That(wallet.Balance, Is.EqualTo(87));
            Assert.That(stock.GetCount("tea"), Is.EqualTo(1));
            Assert.That(stock.GetCount("reward"), Is.EqualTo(5));
            Assert.That(restoredStock.GetCount("tea"), Is.EqualTo(1));
            Assert.That(restoredStock.GetCount("reward"), Is.EqualTo(5));
            Assert.That(restored.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(Capture(restored).receipts, Has.Count.EqualTo(1));
        }

        [Test]
        public void DeliveryMoneyAndPointObserversSeeCommittedSubmissionWithRecoverableMilestoneCursor()
        {
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "milestone", entry = NarrativeEntryPoint.MapAction, onceOnly = true });
            var presenter = new Presenter();
            var narrative = new NarrativeController(config, presenter);
            var wallet = new Wallet();
            narrative.MoneyAccount = wallet;
            var stock = Stock();
            var definition = new NarrativeActionDefinition
            {
                actionKey = "delivery", executionKind = NarrativeActionExecutionKind.Delivery,
                deliveryMode = NarrativeDeliveryMode.Cumulative, progressTarget = 2,
                requirements = "Item(tea,2);Money(20);ActionPoint(1)",
                milestones = new List<NarrativeActionMilestoneDefinition>
                {
                    new NarrativeActionMilestoneDefinition { milestoneId = "first", actionKey = "delivery", atProgress = 1, eventId = "milestone" }
                }
            };
            var actions = new NarrativeActionController(narrative, new[] { definition }, 5, stock);
            var observations = new List<string>();
            Action<string> observe = source =>
            {
                // CaptureInto is deliberately blocked during commit. Observe public values now,
                // then verify the durable milestone cursor as soon as Execute returns.
                observations.Add(source + ":" + actions.GetProgress("delivery") + ":" + actions.HasSubmittedToday("delivery")
                    + ":" + actions.Receipts.Count + ":" + stock.GetCount("tea") + ":" + wallet.Balance + ":" + actions.ActionPoints.Current);
            };
            wallet.Changed += () => observe("money");
            actions.ActionPoints.Changed += (_, __) => observe("points");
            var completions = new List<bool>();

            Assert.That(actions.Execute("delivery", completions.Add, "submission"), Is.True);
            Assert.That(observations, Is.EquivalentTo(new[] { "money:1:True:1:1:80:4", "points:1:True:1:1:80:4" }));
            Assert.That(completions, Is.Empty);
            Assert.That(presenter.Played, Is.EqualTo(new[] { "milestone" }));
            var saved = Capture(actions);
            Assert.That(saved.submissionRecorded, Is.True);
            Assert.That(saved.rootCostCommitted, Is.True);
            Assert.That(saved.executingAction, Is.EqualTo("delivery"));
            Assert.That(saved.currentEvent, Is.EqualTo("milestone"));
            Assert.That(saved.pendingEvents, Is.Empty);
            Assert.That(saved.deliveries[0].triggeredMilestones, Is.EqualTo(new[] { "first" }));
            Assert.That(saved.receipts, Has.Count.EqualTo(1));
            Assert.That(saved.receipts[0].operationId, Is.EqualTo("submission"));
            Assert.That(saved.receipts[0].submissionOnly, Is.True);

            presenter.Complete();
            Assert.That(completions, Is.EqualTo(new[] { true }));
            Assert.That(actions.Execute("delivery", operationId: "submission"), Is.True);
            actions.ResumePendingExecution();
            Assert.That(presenter.Played, Is.EqualTo(new[] { "milestone" }));
            Assert.That(actions.GetProgress("delivery"), Is.EqualTo(1));
            Assert.That(actions.HasSubmittedToday("delivery"), Is.True);
            Assert.That(stock.GetCount("tea"), Is.EqualTo(1));
            Assert.That(wallet.Balance, Is.EqualTo(80));
            Assert.That(wallet.CommitCount, Is.EqualTo(1));
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(observations, Has.Count.EqualTo(2));
            Assert.That(Capture(actions).deliveries[0].triggeredMilestones, Is.EqualTo(new[] { "first" }));
        }
    }
}
