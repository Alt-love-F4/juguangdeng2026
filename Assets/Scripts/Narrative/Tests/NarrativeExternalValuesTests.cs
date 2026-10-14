using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeExternalValuesTests
    {
        private sealed class DeferredPresenter : INarrativePresenter
        {
            public Action Complete;
            public void Play(NarrativeEventRequest request, Action completed) => Complete = completed;
        }
        [Test] public void DirectionalRelationInsideOperationPreservesFinalNotification()
        {
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "relation", ownerId = "player", targetId = "npc", barType = NarrativeBarType.Relation, min = -2, max = 5 });
            var c = new NarrativeController(config);
            var notifications = new List<string>();
            c.BarChanged += (key, before, after) => notifications.Add(key + ":" + before + ":" + after);
            c.BeginOperation(); c.AddBar("npc>player", 2); c.AddBar("npc>player", 1);
            Assert.That(notifications, Is.Empty);
            c.EndOperation();
            Assert.That(notifications, Is.EqualTo(new[] { "npc>player:0:3" }));
        }
        [Test] public void DeferredOperationRetainsDepthLimitWhenConsumedLater()
        {
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "a", max = 20 });
            for (int i = 1; i <= 5; i++)
            {
                string id = "e" + i;
                config.events.Add(new NarrativeEventDefinition { eventId = id });
                config.triggers.Add(new NarrativeTriggerDefinition { triggerId = id, eventId = id, barKey = "a", threshold = i, direction = NarrativeTriggerDirection.Up });
                config.results.Add(new NarrativeResultDefinition { resultId = id, eventId = id, resultType = NarrativeResultType.BarAdd, target = "a", value = "1" });
            }
            var presenter = new DeferredPresenter(); var c = new NarrativeController(config, presenter);
            c.BeginOperation(); c.AddBar("a", 1); c.EndOperation();
            for (int i = 0; i < 3; i++) presenter.Complete();
            LogAssert.Expect(LogType.Warning, "[NCS] Trigger depth exceeds 3: e5");
            presenter.Complete();
            Assert.That(c.State.HasHappenedEvent("e5"), Is.False);
        }
        private sealed class Wallet : INarrativeMoneyAccount
        {
            public int Balance = 900;
            public Action Changed;
            public bool TryRead(out int value) { value = Balance; return true; }
            public bool TryCommit(int expected, int final, out string error)
            {
                error = null;
                if (expected != Balance || final < 0) return false;
                Balance = final; Changed?.Invoke(); return true;
            }
        }
        private static NarrativeConfiguration Config()
        {
            var c = new NarrativeConfiguration();
            c.bars.Add(new NarrativeBarDefinition { barKey = "cafe.money", sourceMode = NarrativeSourceMode.External, providerId = "cafe", sourceKey = "money", max = int.MaxValue });
            c.bars.Add(new NarrativeBarDefinition { barKey = "sum", advanceMode = NarrativeAdvanceMode.Derived, max = int.MaxValue, derivedSources = new List<string> { "cafe.money" }, derivedWeights = new List<float> { 2 } });
            c.events.Add(new NarrativeEventDefinition { eventId = "up" });
            c.triggers.Add(new NarrativeTriggerDefinition { triggerId = "up", eventId = "up", barKey = "sum", threshold = 2000, direction = NarrativeTriggerDirection.Up, crossType = NarrativeCrossType.Cross, onceType = NarrativeOnceType.Repeat });
            return c;
        }
        private static NarrativeController Bind(NarrativeConfiguration config, Wallet wallet, out DelegateNarrativeValueProvider provider)
        {
            var controller = new NarrativeController(config);
            provider = new DelegateNarrativeValueProvider("cafe", key => key == "money" ? wallet.Balance : (int?)null);
            wallet.Changed = provider.NotifyChanged;
            controller.BeginValueRestore();
            controller.BindValueProvider(provider);
            controller.MoneyAccount = wallet;
            controller.CompleteValueRestore();
            return controller;
        }
        [Test] public void MissingSourceIsNotZeroAndExternalOrDerivedNeverPersist()
        {
            var c = new NarrativeController(Config());
            Assert.That(c.State.TryGetBar("cafe.money", out _), Is.False);
            var w = new Wallet { Balance = 0 };
            c = Bind(Config(), w, out _);
            Assert.That(c.State.TryGetBar("cafe.money", out int value), Is.True);
            Assert.That(value, Is.Zero);
            Assert.That(c.SetBar("cafe.money", 7), Is.False);
            Assert.That(c.SetBar("sum", 7), Is.False);
            Assert.That(c.CaptureSnapshot().bars, Is.Empty);
        }
        [Test] public void SameOperationComparesOnlyFinalValueIncludingDerived()
        {
            var w = new Wallet(); var c = Bind(Config(), w, out var p);
            c.BeginOperation();
            w.Balance = 1100; p.NotifyChanged();
            w.Balance = 900; p.NotifyChanged();
            c.EndOperation();
            Assert.That(c.State.HasHappenedEvent("up"), Is.False);
            w.Balance = 1100; p.NotifyChanged();
            Assert.That(c.State.HasHappenedEvent("up"), Is.False, "Owner callback must not recursively play.");
            c.FlushValueChanges();
            Assert.That(c.State.HasHappenedEvent("up"), Is.True);
            Assert.That(c.State.TryGetBar("sum", out int sum), Is.True);
            Assert.That(sum, Is.EqualTo(2200));
        }
        [Test] public void IndependentOperationsRetainCrossingBeforeConsumption()
        {
            var w = new Wallet(); var c = Bind(Config(), w, out var p);
            w.Balance = 1100; p.NotifyChanged();
            w.Balance = 900; p.NotifyChanged();
            Assert.That(c.CanCaptureSnapshot, Is.False);
            c.FlushValueChanges(); c.FlushValueChanges();
            Assert.That(c.State.HasHappenedEvent("up"), Is.True);
        }
        [Test] public void RestoreRebuildsBaselineWithoutCrossingAndRejectsMissingProvider()
        {
            var w = new Wallet(); var c = Bind(Config(), w, out var p);
            c.BeginValueRestore(); c.BeginValueRestore();
            w.Balance = 1100; p.NotifyChanged();
            Assert.Throws<InvalidOperationException>(() => c.CaptureSnapshot());
            Assert.That(c.CaptureSnapshot(true), Is.Not.Null);
            c.CompleteValueRestore(); c.FlushValueChanges();
            Assert.That(c.State.HasHappenedEvent("up"), Is.False);
            c.BeginValueRestore(); c.UnbindValueProviders();
            Assert.Throws<InvalidOperationException>(() => c.CompleteValueRestore());
            Assert.That(c.ValuesReady, Is.False);
        }
        [Test] public void MoneyBatchPreflightsRepeatedDebitWithoutPartialCommit()
        {
            var config = Config();
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "reward" });
            config.events.Add(new NarrativeEventDefinition { eventId = "pay" });
            config.results.Add(new NarrativeResultDefinition { resultId = "flag", eventId = "pay", resultType = NarrativeResultType.SetFlag, target = "reward", value = "true" });
            config.results.Add(new NarrativeResultDefinition { resultId = "first", eventId = "pay", seq = 1, resultType = NarrativeResultType.MoneyRemove, target = "cafe", value = "600" });
            config.results.Add(new NarrativeResultDefinition { resultId = "second", eventId = "pay", seq = 2, resultType = NarrativeResultType.MoneyRemove, target = "cafe", value = "600" });
            var w = new Wallet(); var c = Bind(config, w, out _);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Event pay result transaction failed: Result second"));
            c.QueueEvent("pay");
            Assert.That(w.Balance, Is.EqualTo(900));
            Assert.That(c.State.GetCondition("reward"), Is.False);
            Assert.That(c.State.HasHappenedEvent("pay"), Is.False);
        }
        [Test] public void ValueValidationRejectsWritesDuplicateFactsAndCycles()
        {
            var config = Config();
            config.bars.Add(new NarrativeBarDefinition { barKey = "duplicate", sourceMode = NarrativeSourceMode.External, providerId = "cafe", sourceKey = "money", max = int.MaxValue });
            config.bars[1].derivedSources.Add("sum");
            config.results.Add(new NarrativeResultDefinition { resultId = "illegal", resultType = NarrativeResultType.BarSet, target = "cafe.money", value = "1" });
            var errors = LubanNarrativeLoader.ValidateValueConfiguration(config);
            Assert.That(errors.Exists(e => e.Contains("duplicate external")), Is.True);
            Assert.That(errors.Exists(e => e.Contains("cycle")), Is.True);
            Assert.That(errors.Exists(e => e.Contains("illegal") && e.Contains("cannot write")), Is.True);
        }
        [Test] public void StaleProviderCallbacksAfterUnbindCannotEnqueueChanges()
        {
            var w = new Wallet(); var c = Bind(Config(), w, out var old);
            c.BeginValueRestore(); c.UnbindValueProviders();
            var replacement = new DelegateNarrativeValueProvider("cafe", key => 900);
            c.BindValueProvider(replacement); c.CompleteValueRestore();
            w.Balance = 1100; old.NotifyChanged(); c.FlushValueChanges();
            Assert.That(c.State.HasHappenedEvent("up"), Is.False);
            Assert.That(c.CanCaptureSnapshot, Is.True);
        }
        [Test] public void MoneyOverflowFailsBeforeConditionsAreCommitted()
        {
            var config = Config(); config.events.Add(new NarrativeEventDefinition { eventId = "overflow" });
            config.results.Add(new NarrativeResultDefinition { resultId = "overflow-result", eventId = "overflow", resultType = NarrativeResultType.MoneyAdd, target = "cafe", value = "1" });
            var w = new Wallet { Balance = int.MaxValue }; var c = Bind(config, w, out _);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Event overflow result transaction failed: Result overflow-result"));
            c.QueueEvent("overflow");
            Assert.That(w.Balance, Is.EqualTo(int.MaxValue));
            Assert.That(c.State.HasHappenedEvent("overflow"), Is.False);
        }
        [Test] public void MoneyAddThenRemoveUsesTemporaryBalance()
        {
            var config = Config(); config.events.Add(new NarrativeEventDefinition { eventId = "pay" });
            config.results.Add(new NarrativeResultDefinition { resultId = "credit", eventId = "pay", resultType = NarrativeResultType.MoneyAdd, target = "cafe", value = "300" });
            config.results.Add(new NarrativeResultDefinition { resultId = "debit", eventId = "pay", seq = 1, resultType = NarrativeResultType.MoneyRemove, target = "cafe", value = "1100" });
            var w = new Wallet(); var c = Bind(config, w, out _);
            c.QueueEvent("pay");
            Assert.That(w.Balance, Is.EqualTo(100));
            Assert.That(c.State.HasHappenedEvent("pay"), Is.True);
            Assert.That(c.State.HasHappenedEvent("up"), Is.False);
        }
    }
}
