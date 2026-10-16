using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    // Same catalog/real-visit setup and real DeliverProductionCup -> ServeProduction path as FreeCafeServiceReceiptTests.
    public sealed class FreeCafeProductionAttemptTests
    {
        private CafeCatalog catalog;
        private CafeWorld world;
        private CustomerRequest order;
        [SetUp] public void Setup()
        {
            catalog = JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text);
            world = new CafeWorld(catalog);
            world.SyncCafeVisits(new[] { new CafeVisitIntent { visitInstanceId = "visit", characterId = "reporter",
                orderProductId = "espresso", requireRealProduction = true, arrivalAuthorized = true,
                preferredSeatId = "customer_0", leaveAfterServed = false,
                identity = new CafePersonIdentity { personId = "reporter", displayName = "Reporter", role = CafePersonRole.Customer } } });
            world.StartDay(); order = world.State.requests.Find(r => r.customerId == "customer_0");
            world.EnableProduction(); world.EnterProduction(); world.Focus(order.id);
            world.Product("espresso").maxRemakes = 1;
        }
        private CafeSnapshot Copy() => JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
        private WorldEntity Cup(string kind = "essence")
        {
            Assert.That(world.SelectServingCup("s.cups.small").success, Is.True);
            var cup = world.ServingCup;
            cup.contents.Add(new Portion { specId = "coffee", milliliters = 200, units = 1, coffeeKind = kind,
                productionScore = 100, temperature = LiquidTemperature.Boiling });
            return cup;
        }
        private void Deliver(string kind = "essence")
        {
            Cup(kind); Assert.That(world.DeliverProductionCup().success, Is.True);
        }
        [Test] public void RemakeAndFinalEachRecordExactlyOnceAndConsumedCupsCannotBeReused()
        {
            var first = Cup("black"); Assert.That(world.DeliverProductionCup().success, Is.True);
            Assert.That(world.State.entities.Contains(first), Is.False);
            Assert.That(world.DeliverProductionCup().success, Is.False);
            var firstFact = world.ProductionAttempts("visit", order.id).Single();
            Assert.That(firstFact.willRemake, Is.True); Assert.That(firstFact.satisfied, Is.False);
            Assert.That(world.CafeVisit("visit").serviceReceipt, Is.Null);
            var second = Cup(); Assert.That(second.id, Is.Not.EqualTo(first.id));
            Assert.That(world.DeliverProductionCup().success, Is.True);
            Assert.That(world.State.entities.Contains(second), Is.False);
            Assert.That(world.DeliverProductionCup().success, Is.False);
            var facts = world.ProductionAttempts("visit", order.id);
            Assert.That(facts.Select(f => f.attemptSequence), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(facts.Select(f => f.cupId), Is.EqualTo(new[] { first.id, second.id }));
            Assert.That(facts[1].willRemake, Is.False); Assert.That(facts[1].satisfied, Is.True);
            Assert.That(facts.All(f => f.visitInstanceId == "visit" && f.orderId == order.id && f.productId == "espresso"));
            Assert.That(facts[0].classifiedProductId, Is.Not.EqualTo("espresso"));
            Assert.That(facts[1].classifiedProductId, Is.EqualTo("espresso"));
            Assert.That(world.State.cafeVisitFeedback.Count(f => f.stage == "service_completed"), Is.EqualTo(1));
        }
        [Test] public void PreconditionsAndUnservedDepartureNeverRecordFacts()
        {
            Assert.That(world.DeliverProductionCup().success, Is.False);
            var cup = Cup(); world.State.production.miniGameToken = "busy";
            Assert.That(world.DeliverProductionCup().success, Is.False);
            Assert.That(world.State.entities.Contains(cup), Is.True);
            Assert.That(world.ProductionAttempts("visit", order.id), Is.Empty);
            world.State.production.miniGameToken = null;
            world.EndCafeVisit("visit", "test");
            world.DeliverProductionCup(); // Another ordinary order may become focused after departure.
            Assert.That(world.ProductionAttempts("visit", order.id), Is.Empty);
            Assert.That(world.CafeVisit("visit").serviceReceipt, Is.Null);
        }
        [Test] public void LaterOrderScoresCatalogChangesAndQueryMutationsDoNotOverwriteFacts()
        {
            Deliver("black");
            var before = world.ProductionAttempts("visit", order.id).Single();
            string original = JsonUtility.ToJson(before);
            order.lastProductionScore = before.score == 0 ? 100 : 0;
            var detached = world.ProductionAttempts("visit", order.id).Single();
            detached.score = -999; detached.orderId = "changed";
            world.Product("espresso").minimumScore = 0;
            world.Product("espresso").maxRemakes = 0;
            var restored = world.Restore(Copy()); Assert.That(restored.success, Is.True, restored.message);
            order = world.State.requests.Single(r => r.id == order.id);
            Assert.That(world.ProductionAttempts("visit", order.id)[0].minimumScore, Is.EqualTo(before.minimumScore));
            Deliver();
            Assert.That(JsonUtility.ToJson(world.ProductionAttempts("visit", order.id)[0]), Is.EqualTo(original));
            Assert.That(world.ProductionAttempts("other", order.id), Is.Empty);
            Assert.That(world.ProductionAttempts("visit", "other"), Is.Empty);
            Assert.That(world.ProductionAttempts("visit", order.id)[1].score, Is.EqualTo(order.lastProductionScore));
        }
        [Test] public void EndVisitDuringRemakeRetainsFactsWithoutFabricatingFinalService()
        {
            Deliver("black");
            string original = JsonUtility.ToJson(world.ProductionAttempts("visit", order.id).Single());
            Assert.That(world.EndCafeVisit("visit", "test").success, Is.True);
            var result = world.Restore(Copy()); Assert.That(result.success, Is.True, result.message);
            Assert.That(world.CafeVisit("visit").status, Is.EqualTo(CafeVisitStatus.Ended));
            Assert.That(world.CafeVisit("visit").serviceReceipt, Is.Null);
            Assert.That(JsonUtility.ToJson(world.ProductionAttempts("visit", order.id).Single()), Is.EqualTo(original));
            Assert.That(world.State.cafeVisitFeedback.Any(f => f.stage == "service_completed"), Is.False);
        }
        [Test] public void FinalFailureStillRecordsOneAttemptWithOriginalBusinessFlags()
        {
            world.Product("espresso").maxRemakes = 0;
            Deliver("black");
            var fact = world.ProductionAttempts("visit", order.id).Single();
            Assert.That(fact.satisfied, Is.False); Assert.That(fact.correctType, Is.False);
            Assert.That(fact.willRemake, Is.False);
            var result = world.Restore(Copy()); Assert.That(result.success, Is.True, result.message);
        }
        [Test] public void SaveRestoreRetainsFactsAndContinuesSequenceWithoutReplayingSettlement()
        {
            Deliver("black");
            string first = JsonUtility.ToJson(world.ProductionAttempts("visit", order.id).Single());
            var result = world.Restore(Copy()); Assert.That(result.success, Is.True, result.message);
            order = world.State.requests.Single(r => r.id == order.id);
            Assert.That(JsonUtility.ToJson(world.ProductionAttempts("visit", order.id).Single()), Is.EqualTo(first));
            Deliver();
            string facts = JsonUtility.ToJson(world.CafeVisit("visit"));
            int money = world.State.money, served = world.State.served;
            result = world.Restore(Copy()); Assert.That(result.success, Is.True, result.message);
            Assert.That(JsonUtility.ToJson(world.CafeVisit("visit")), Is.EqualTo(facts));
            Assert.That(world.ProductionAttempts("visit", order.id).Select(f => f.attemptSequence), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(world.State.money, Is.EqualTo(money)); Assert.That(world.State.served, Is.EqualTo(served));
        }
        [Test] public void CorruptIdentitiesSequencesScoresAndEmptyItemsAreRejectedAtomically()
        {
            Deliver("black"); Deliver();
            Action<CafeVisitInstance>[] corruptions = {
                v => v.productionAttempts[0].visitInstanceId = "other",
                v => v.productionAttempts[0].orderId = "other",
                v => v.productionAttempts[0].productId = "missing",
                v => v.productionAttempts[0].productId = "latte",
                v => v.productionAttempts[0].cupId = " ",
                v => v.productionAttempts[0].classifiedProductId = "missing",
                v => v.productionAttempts[1].attemptSequence = 1,
                v => v.productionAttempts[0].attemptSequence = 0,
                v => v.productionAttempts[0].score = -1,
                v => v.productionAttempts[0].score = 101,
                v => v.productionAttempts[0].score = float.NaN,
                v => v.productionAttempts[0].score = float.PositiveInfinity,
                v => v.productionAttempts[0].minimumScore = float.NegativeInfinity,
                v => v.productionAttempts[0].satisfied = true,
                v => v.productionAttempts[1].score += 1,
                v => v.productionAttempts[0] = new CafeProductionAttemptReceipt(),
                v => v.productionAttempts[0] = null
            };
            var live = world.State;
            foreach (var corrupt in corruptions)
            {
                var copy = Copy(); corrupt(copy.cafeVisits.Single(v => v.visitInstanceId == "visit"));
                Assert.That(world.Restore(copy).success, Is.False);
                Assert.That(world.State, Is.SameAs(live));
            }
        }
        [Test] public void ActiveOrderCannotLoseRecordedRemakesButMayRetainLegacyRemakes()
        {
            Deliver("black");
            var live = world.State;
            var corrupt = Copy(); corrupt.requests.Single(r => r.id == order.id).remakes = 0;
            Assert.That(world.Restore(corrupt).success, Is.False);
            Assert.That(world.State, Is.SameAs(live));
            var legacy = Copy(); legacy.requests.Single(r => r.id == order.id).remakes = 2;
            var result = world.Restore(legacy);
            Assert.That(result.success, Is.True, result.message);
            Assert.That(world.State.requests.Single(r => r.id == order.id).remakes, Is.EqualTo(2));
            Assert.That(world.ProductionAttempts("visit", order.id).Count, Is.EqualTo(1));
        }
        [Test] public void MinimumScoreAboveOneHundredRemainsValidHistoricalFact()
        {
            world.Product("espresso").minimumScore = 101;
            Deliver();
            Assert.That(world.ProductionAttempts("visit", order.id).Single().minimumScore, Is.EqualTo(101));
            var result = world.Restore(Copy());
            Assert.That(result.success, Is.True, result.message);
        }
        [Test] public void LegacySaveWithoutAttemptsRestoresAndStartsAtOne()
        {
            var copy = Copy(); copy.cafeVisits.Single(v => v.visitInstanceId == "visit").productionAttempts.Clear();
            string json = JsonUtility.ToJson(copy).Replace(",\"productionAttempts\":[]", "").Replace("\"productionAttempts\":[],", "");
            StringAssert.DoesNotContain("\"productionAttempts\"", json);
            var result = world.Restore(JsonUtility.FromJson<CafeSnapshot>(json));
            Assert.That(result.success, Is.True, result.message);
            order = world.State.requests.Single(r => r.id == order.id);
            Assert.That(world.ProductionAttempts("visit", order.id), Is.Empty);
            Deliver(); Assert.That(world.ProductionAttempts("visit", order.id).Single().attemptSequence, Is.EqualTo(1));
        }
    }
}
