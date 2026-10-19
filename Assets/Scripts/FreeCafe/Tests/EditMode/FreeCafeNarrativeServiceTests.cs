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
            Assert.That(world.State.successfulOrders, Is.Zero); Assert.That(world.HasNarrativeService, Is.False);
            lease.Dispose(); Assert.That(receipts, Has.Count.EqualTo(1));
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
