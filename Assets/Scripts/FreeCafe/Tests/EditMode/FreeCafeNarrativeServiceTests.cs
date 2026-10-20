using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeNarrativeServiceTests
    {
        private CafeWorld world;
        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction(); world.EnterProduction();
        }
        private void Cup()
        {
            var request = world.Request(world.State.focusedRequestId);
            Assert.That(world.SelectServingCup(world.RequiredCupSpecId(request) == "cup.small" ? "s.cups.small" : "s.cups.large").success, Is.True);
            var product = world.Product("blackcoffee");
            var portion = new Portion { specId = "coffee", milliliters = product.evaluation.amounts[0].idealMl * world.OrderPortionScale(request),
                productionScore = 100, coffeeKind = "black", temperature = LiquidTemperature.Warm };
            world.AddConstituent(portion, "water", 0, portion.milliliters);
            world.AddConstituent(portion, "grounds", portion.milliliters * product.evaluation.idealCoffeeStrength, 0);
            world.ServingCup.contents.Add(portion);
            world.FinishPour(world.ServingCup.id);
        }
        [Test] public void ClosedCafeServiceRequiresActualDeliveryAndDoesNotCreateBusinessRevenue()
        {
            var receipts = new List<CafeNarrativeDeliveryReceipt>(); int money = world.State.money, revenue = world.State.todayRevenue;
            Assert.That(world.TryBeginNarrativeService("story-test", new[] { new CafeNarrativeOrderSpec { productId = "blackcoffee", minScore = 0, requirePass = false } }, receipts.Add, out var lease, out var error), Is.True, error);
            Assert.That(world.State.open, Is.False); Assert.That(receipts, Is.Empty);
            Cup(); string cup = world.ServingCup.id;
            var delivery = world.DeliverProductionCup(); Assert.That(delivery.success, Is.True, delivery.message);
            Assert.That(receipts, Has.Count.EqualTo(1)); Assert.That(receipts[0].Completed, Is.True);
            Assert.That(receipts[0].CupId, Is.EqualTo(cup)); Assert.That(world.Find(cup), Is.Null);
            Assert.That(world.State.money, Is.EqualTo(money)); Assert.That(world.State.todayRevenue, Is.EqualTo(revenue));
            Assert.That(world.State.successfulOrders, Is.EqualTo(1)); Assert.That(world.HasNarrativeService, Is.False);
            lease.Dispose(); Assert.That(receipts, Has.Count.EqualTo(1));
        }
        [Test] public void PassingStoryCupsUnlockRecipesOnceEachWithoutBusinessStatistics([Values(false, true)] bool orderOnly)
        {
            var product = world.Product("blackcoffee");
            product.unlockSuccessfulOrders = world.State.successfulOrders + 2;
            product.unlockReputation = world.State.reputation + 2;
            world.State.unlockedProducts.Remove(product.id);
            int reputation = world.State.reputation, served = world.State.todayServed, orders = world.State.successfulOrders;
            Assert.That(world.TryBeginNarrativeService("progress", new[] { new CafeNarrativeOrderSpec { productId = product.id, minScore = 0, count = 2 } },
                _ => { }, out var lease, out var error, orderOnly), Is.True, error);
            Cup(); Assert.That(world.DeliverProductionCup().success, Is.True);
            Assert.That(world.State.successfulOrders, Is.EqualTo(orders + 1));
            Assert.That(world.State.unlockedProducts, Does.Not.Contain(product.id));
            Cup(); Assert.That(world.DeliverProductionCup().success, Is.True);
            Assert.That(world.State.successfulOrders, Is.EqualTo(orders + 2));
            Assert.That(world.State.reputation, Is.EqualTo(reputation + 2));
            Assert.That(world.State.unlockedProducts, Does.Contain(product.id));
            Assert.That(world.State.todayServed, Is.EqualTo(served));
            Assert.That(world.DeliverProductionCup().success, Is.False);
            lease.Dispose();
            Assert.That(world.State.successfulOrders, Is.EqualTo(orders + 2));
        }
        [Test] public void BelowStoryScoreGrantsReputationButNotSuccessfulOrder([Values(false, true)] bool requirePass)
        {
            int orders = world.State.successfulOrders, reputation = world.State.reputation;
            Assert.That(world.TryBeginNarrativeService("failed-progress", new[] { new CafeNarrativeOrderSpec { productId = "blackcoffee", minScore = 100, requirePass = requirePass } },
                _ => { }, out var lease, out var error, orderOnly: true), Is.True, error);
            Cup();
            // A correctly classified but deliberately underfilled cup cannot meet the score requirement.
            world.ServingCup.contents[0].milliliters = 1;
            Assert.That(world.DeliverProductionCup().success, Is.True);
            Assert.That(world.State.successfulOrders, Is.EqualTo(orders));
            Assert.That(world.State.reputation, Is.EqualTo(reputation + 1));
            lease.Dispose();
        }
        [Test] public void CancelOnlyRemovesOwnedRequestAndNeverRefundsAlreadyConsumedIngredients()
        {
            Assert.That(world.TryBeginNarrativeService("cancel", new[] { new CafeNarrativeOrderSpec { productId = "blackcoffee", minScore = 90 } }, _ => Assert.Fail("Cancelled request completed"), out var lease, out _), Is.True);
            string id = world.State.focusedRequestId; Cup(); string cup = world.ServingCup.id; lease.Dispose(); lease.Dispose();
            Assert.That(world.Request(id), Is.Null); Assert.That(world.Find(cup), Is.Not.Null); Assert.That(world.Find(cup).contents, Is.Not.Empty);
            Assert.That(world.HasNarrativeService, Is.False);
        }
        [Test] public void InvalidOrderCannotMutateWorldOrClaimReadiness()
        {
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.TryBeginNarrativeService("bad", new[] { new CafeNarrativeOrderSpec { productId = "missing" } }, _ => { }, out _, out _), Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }
    }
}
