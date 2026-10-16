using System;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeDebugSessionTests
    {
        private sealed class DelayedPresenter : INarrativePresenter
        {
            public Action Completion;
            public void Play(NarrativeEventRequest request, Action completed) { Completion = completed; }
        }

        [TestCase("EV_FIRST_DAY_PERCEPTION_CHECK", "first_day_perception", 2, false)]
        [TestCase("EV_FIRST_DAY_PERCEPTION_CHECK", "first_day_perception", 3, true)]
        [TestCase("arrival.check", "arrival.perception", 2, false)]
        [TestCase("arrival.check", "arrival.perception", 3, true)]
        public void GeneratedConfigurationUsesRealChecksWithoutRemovingProvidersOrTriggers(string eventId, string checkId, int value, bool expected)
        {
            using (var session = new NarrativeDebugSession())
            {
                session.SetAbility("perception", value);
                session.EstablishBaseline();
                string before = JsonUtility.ToJson(session.Controller.CaptureSnapshot());
                var diagnosis = session.Controller.InspectAbilityCheck(checkId);
                Assert.That(diagnosis.passed, Is.EqualTo(expected));
                Assert.That(diagnosis.values[0].currentValue, Is.EqualTo(value));
                Assert.That(JsonUtility.ToJson(session.Controller.CaptureSnapshot()), Is.EqualTo(before), "inspection cannot change caches, queues or random state");
                Assert.That(session.RequestEvent(eventId), Is.True, session.Controller.ExplainEvent(eventId).ToString());
                var actual = session.EvaluateAbilityCheck(checkId);
                Assert.That(actual.passed, Is.EqualTo(expected));
                Assert.That(actual.branchQueued, Is.True);
                var definition = session.Configuration.abilityChecks.Find(c => c.checkId == checkId);
                string branch = expected ? definition.successEventId : definition.failureEventId;
                Assert.That(session.Controller.PendingEventIds, Does.Contain(branch));
                session.CompletePresentation();
                Assert.That(session.CurrentRequest.Event.eventId, Is.EqualTo(branch));
            }
        }

        private static NarrativeConfiguration RewardConfig()
        {
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "cash", sourceMode = NarrativeSourceMode.External, providerId = "cafe", sourceKey = "money", initValue = 10 });
            config.events.Add(new NarrativeEventDefinition { eventId = "reward", onceOnly = true });
            config.results.Add(new NarrativeResultDefinition { resultId = "cash", eventId = "reward", resultType = NarrativeResultType.MoneyAdd, target = "cafe", value = "5" });
            config.results.Add(new NarrativeResultDefinition { resultId = "item", eventId = "reward", resultType = NarrativeResultType.ItemAdd, target = "item:token", value = "1" });
            return config;
        }

        [Test]
        public void RepeatedTrialRestoresMoneyInventoryFactsQueuesAndRejectsOldCompletion()
        {
            var presenter = new DelayedPresenter();
            using (var session = new NarrativeDebugSession(RewardConfig(), presenter))
            {
                session.EstablishBaseline();
                for (int i = 0; i < 3; i++)
                {
                    session.RestoreBaseline();
                    Assert.That(session.Inventory.GetCount("token"), Is.Zero);
                    Assert.That(session.Controller.State.HasHappenedEvent("reward"), Is.False);
                    Assert.That(session.Controller.PendingEventIds, Is.Empty);
                    session.TryRead(out int startingMoney); Assert.That(startingMoney, Is.EqualTo(10));
                    Assert.That(session.RequestEvent("reward"), Is.True);
                    presenter.Completion();
                    session.TryRead(out int money); Assert.That(money, Is.EqualTo(15));
                    Assert.That(session.Inventory.GetCount("token"), Is.EqualTo(1));
                }
                session.RestoreBaseline(); session.RequestEvent("reward");
                Action stale = presenter.Completion;
                session.RestoreBaseline(); stale();
                Assert.That(session.Controller.State.HappenedEvents, Is.Empty);
                Assert.That(session.Inventory.GetCount("token"), Is.Zero);
                session.TryRead(out int restored); Assert.That(restored, Is.EqualTo(10));
            }
        }

        [Test]
        public void EditingAnotherTrialCannotImportPreviousRewardsAndUnknownFactsAreRejected()
        {
            using (var session = new NarrativeDebugSession(RewardConfig()))
            {
                session.EstablishBaseline(); session.RequestEvent("reward"); session.CompletePresentation();
                Assert.Throws<InvalidOperationException>(() => session.EstablishBaseline());
                session.SetAbility("perception", 3); session.EstablishBaseline();
                session.TryRead(out int balance); Assert.That(balance, Is.EqualTo(10));
                Assert.That(session.Inventory.GetCount("token"), Is.Zero);
                Assert.That(session.Controller.State.HappenedEvents, Is.Empty);
                Assert.Throws<ArgumentException>(() => session.PrepareEventFact("not-configured"));
            }
        }

        [Test]
        public void NaturalThresholdAndDateUseSchedulingEntrypoints()
        {
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 100, initValue = 1 });
            config.bars.Add(new NarrativeBarDefinition { barKey = "score", min = 0, max = 100 });
            config.events.Add(new NarrativeEventDefinition { eventId = "threshold" });
            config.events.Add(new NarrativeEventDefinition { eventId = "date" });
            config.triggers.Add(new NarrativeTriggerDefinition { triggerId = "score3", barKey = "score", threshold = 3, eventId = "threshold", direction = NarrativeTriggerDirection.Up, crossType = NarrativeCrossType.Cross });
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "date", day = 2, repeatRule = NarrativeRepeatRule.Once });
            using (var session = new NarrativeDebugSession(config))
            {
                session.SetDate(1); session.EstablishBaseline();
                session.SubmitBar("score", 2); Assert.That(session.CurrentRequest.Event, Is.Null);
                session.SubmitBar("score", 3); Assert.That(session.CurrentRequest.Event.eventId, Is.EqualTo("threshold"));
                session.RestoreBaseline();
                session.SubmitDate(2); Assert.That(session.CurrentRequest.Event.eventId, Is.EqualTo("date"));
            }
        }

        [Test]
        public void EventInspectionMatchesRequestGateWithoutSideEffects()
        {
            var config = RewardConfig();
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "poor", ownerType = "event", ownerId = "reward", groupIndex = 1, condType = "BAR_GTE", target = "bar:cash", value = "20" });
            using (var session = new NarrativeDebugSession(config))
            {
                session.EstablishBaseline();
                string before = JsonUtility.ToJson(session.Controller.CaptureSnapshot());
                var report = session.Controller.ExplainEvent("reward");
                Assert.That(report.CanRequest, Is.False);
                Assert.That(report.Conditions[0].Current, Is.EqualTo("10"));
                Assert.That(report.Conditions[0].Required, Is.EqualTo("20"));
                Assert.That(JsonUtility.ToJson(session.Controller.CaptureSnapshot()), Is.EqualTo(before));
                Assert.That(session.RequestEvent("reward"), Is.False);
                Assert.That(session.CurrentRequest.Event, Is.Null);
            }
        }
    }
}
