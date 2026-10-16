using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeServiceReceiptTests
    {
        private CafeCatalog catalog;
        private CafeWorld world;
        private CustomerRequest order;
        [SetUp] public void Setup()
        {
            catalog = JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text);
            world = new CafeWorld(catalog);
            world.State.automaticOrders = true; catalog.economy.autoChance = 1;
            world.SyncCafeVisits(new[] { new CafeVisitIntent { visitInstanceId = "visit", characterId = "reporter", orderProductId = "espresso",
                requireRealProduction = true, arrivalAuthorized = true, preferredSeatId = "customer_0", leaveAfterServed = false,
                identity = new CafePersonIdentity { personId = "reporter", displayName = "Reporter", role = CafePersonRole.Customer } } });
            world.StartDay(); order = world.State.requests.Find(r => r.customerId == "customer_0");
            world.EnableProduction(); world.EnterProduction(); world.Focus(order.id);
        }
        private CafeSnapshot Copy() => JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
        private WorldEntity Cup(string kind = "essence", float quality = 100, bool art = false)
        {
            Assert.That(world.SelectServingCup("s.cups.small").success);
            var cup = world.ServingCup; cup.latteArt = art;
            cup.contents.Add(new Portion { specId = "coffee", milliliters = 200, units = 1, coffeeKind = kind,
                productionScore = quality, temperature = LiquidTemperature.Boiling });
            return cup;
        }
        [Test] public void SpecifiedProductAndRealOrderCannotBeAutoCompletedEvenWithStaleTimer()
        {
            Assert.That(order.productId, Is.EqualTo("espresso")); Assert.That(order.automaticRemaining, Is.LessThan(0));
            order.automaticRemaining = 0; world.Tick(.01f);
            Assert.That(world.State.requests, Contains.Item(order)); Assert.That(world.CafeVisit("visit").serviceReceipt, Is.Null);
            Assert.That(world.State.cafeVisitFeedback.Any(f => f.stage == "service_completed"), Is.False);
        }
        [Test] public void RemakeDoesNotConfirmFinalFailureCarriesSameOrderAndRestoresWithoutNewSettlement()
        {
            var product = world.Product("espresso"); product.maxRemakes = 1;
            Cup("black"); Assert.That(world.DeliverProductionCup().success);
            Assert.That(order.remakes, Is.EqualTo(1)); Assert.That(world.CafeVisit("visit").serviceReceipt, Is.Null);
            var cup = Cup("black"); Assert.That(world.DeliverProductionCup().success);
            var receipt = world.CafeVisit("visit").serviceReceipt;
            Assert.That(receipt, Is.Not.Null); Assert.That(receipt.satisfied, Is.False); Assert.That(receipt.correctType, Is.False);
            Assert.That(receipt.orderId, Is.EqualTo(order.id)); Assert.That(receipt.cupId, Is.EqualTo(cup.id));
            Assert.That(world.State.requests.Contains(order), Is.False);
            int money = world.State.money, revenue = world.State.todayRevenue, served = world.State.served;
            Assert.That(world.Restore(Copy()).success); Assert.That(world.DeliverProductionCup().success, Is.False);
            Assert.That(world.CafeVisit("visit").serviceReceipt.orderId, Is.EqualTo(order.id));
            Assert.That(world.State.money, Is.EqualTo(money)); Assert.That(world.State.todayRevenue, Is.EqualTo(revenue));
            Assert.That(world.State.served, Is.EqualTo(served));
            var f = world.State.cafeVisitFeedback.Single(x => x.stage == "service_completed");
            world.AcknowledgeCafeVisitFeedback(f.sequence); world.AcknowledgeCafeVisitFeedback(f.sequence);
            Assert.That(world.State.money, Is.EqualTo(money));
        }
        [Test] public void ReceiptUsesBusinessClassificationMinimumScoreAndLatteArtNotAHardcodedThreshold()
        {
            var p = world.Product("espresso"); p.maxRemakes = 0;
            p.minimumScore = 101; Cup(); world.DeliverProductionCup();
            var r = world.CafeVisit("visit").serviceReceipt;
            Assert.That(r.correctType); Assert.That(r.satisfied, Is.False); Assert.That(r.minimumScore, Is.EqualTo(101));
            Setup(); p = world.Product("espresso"); p.maxRemakes = 0; p.requiresLatteArt = true; catalog.production.requireLatteArt = true;
            Cup(); world.DeliverProductionCup(); r = world.CafeVisit("visit").serviceReceipt;
            Assert.That(r.correctType); Assert.That(r.latteArtSatisfied, Is.False); Assert.That(r.satisfied, Is.False);
            Setup(); p = world.Product("espresso"); p.minimumScore = 75; Cup(); world.DeliverProductionCup();
            r = world.CafeVisit("visit").serviceReceipt; Assert.That(r.satisfied); Assert.That(r.score, Is.GreaterThanOrEqualTo(p.minimumScore));
            Assert.That(world.State.todayRevenue, Is.GreaterThan(0));
        }
        [Test] public void UnityInlineEmptyReceiptShellRestoresAsAbsentButNondefaultShellsAreRejected()
        {
            var copy = Copy();
            copy.cafeVisits.Single(v => v.visitInstanceId == "visit").serviceReceipt = new CafeServiceReceipt();
            foreach (var feedback in copy.cafeVisitFeedback) feedback.serviceReceipt = new CafeServiceReceipt();
            var result = world.Restore(copy);
            Assert.That(result.success, Is.True, result.message);
            Assert.That(world.CafeVisit("visit").serviceReceipt, Is.Null);
            Assert.That(world.State.cafeVisitFeedback.All(f => f.serviceReceipt == null));
            // Every receipt field participates in the null sentinel. A partial/invalid receipt must not vanish.
            foreach (var field in typeof(CafeServiceReceipt).GetFields())
            {
                var invalid = new CafeServiceReceipt();
                field.SetValue(invalid, field.FieldType == typeof(string) ? (object)"invalid" : field.FieldType == typeof(bool) ? true : 1f);
                copy = Copy(); copy.cafeVisits.Single(v => v.visitInstanceId == "visit").serviceReceipt = invalid;
                Assert.That(world.Restore(copy).success, Is.False, "Nondefault receipt field erased: " + field.Name);
                copy = Copy(); copy.cafeVisitFeedback[0].serviceReceipt = invalid;
                Assert.That(world.Restore(copy).success, Is.False, "Nondefault feedback receipt field erased: " + field.Name);
            }
        }
        [Test] public void UnservedDepartureNeverFabricatesReceiptAndDelayUsesOnlyBusinessElapsed()
        {
            world.EndCafeVisit("visit", "ncs_despawn");
            Assert.That(world.CafeVisit("visit").serviceReceipt, Is.Null);
            Assert.That(world.State.cafeVisitFeedback.Any(f => f.stage == "service_completed"), Is.False);
            Assert.That(world.Restore(Copy()).success);
            world = new CafeWorld(catalog);
            world.SyncCafeVisits(new[] { new CafeVisitIntent { visitInstanceId = "delayed", characterId = "reporter", arrivalAuthorized = true,
                arrivalDelaySeconds = 150, preferredSeatId = "customer_0", orderProductId = "espresso", requireRealProduction = true,
                identity = new CafePersonIdentity { personId = "reporter", displayName = "Reporter", role = CafePersonRole.Customer } } });
            world.Tick(200); Assert.That(world.State.businessElapsed, Is.Zero);
            world.StartDay(); world.Tick(149); Assert.That(world.CafeVisit("delayed").status, Is.EqualTo(CafeVisitStatus.PendingArrival));
            world.Tick(1); Assert.That(world.CafeVisit("delayed").status, Is.EqualTo(CafeVisitStatus.SeatedBeforeServe));
        }
    }
}
