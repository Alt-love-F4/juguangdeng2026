using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeAutomationTests
    {
        private CafeWorld world;

        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction();
        }

        [Test] public void UnlockAndRefillIsFreeIdempotentAndDoesNotGrantProgressOrPurchaseEvents()
        {
            world.ConfigureEquipmentProgression(false);
            int money = world.State.money, reputation = world.State.reputation;
            int equipmentEvents = 0; world.EquipmentAcquired += _ => equipmentEvents++;
            world.Find("s.milk").stock = 0;
            world.Find("s.honey").stock = 99;
            Assert.That(world.AutomationUnlockAndRefill().success, Is.True);
            Assert.That(world.PrecisionEquipmentAvailable && world.LatteEquipmentAvailable, Is.True);
            foreach (var product in world.Catalog.products)
            {
                Assert.That(world.State.unlockedProducts, Contains.Item(product.id));
                Assert.That(world.CanOfferProduct(product), Is.True);
            }
            Assert.That(world.Find("s.milk").stock, Is.EqualTo(world.Spec(world.Find("s.milk").specId).initialStock));
            Assert.That(world.Find("s.honey").stock, Is.EqualTo(99));
            string after = JsonUtility.ToJson(world.State);
            world.AutomationUnlockAndRefill();
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(after));
            Assert.That(world.State.money, Is.EqualTo(money));
            Assert.That(world.State.reputation, Is.EqualTo(reputation));
            Assert.That(world.State.served, Is.Zero);
            Assert.That(world.State.successfulOrders, Is.Zero);
            Assert.That(equipmentEvents, Is.Zero);
        }

        [Test] public void OrdinaryDeliveryUsesRealPriceEvaluationCountersAndDoesNotSettleTwice()
        {
            world.AutomationUnlockAndRefill(); world.StartDay(false);
            var orders = world.State.requests.ToArray();
            Assert.That(orders, Is.Not.Empty);
            int money = world.State.money;
            int price = orders.Sum(r => world.Product(r.productId).price);
            var report = world.AutomationServePendingOrders();
            Assert.That(report.StopReason, Is.Null);
            Assert.That(report.OrdinaryOrdersServed, Is.EqualTo(orders.Length));
            Assert.That(report.Delivered, Is.EqualTo(orders.Length));
            Assert.That(world.State.money, Is.EqualTo(money + price));
            Assert.That(world.State.todayRevenue, Is.EqualTo(price));
            Assert.That(world.State.served, Is.EqualTo(orders.Length));
            Assert.That(world.State.successfulOrders, Is.EqualTo(orders.Length));
            Assert.That(orders.All(r => r.lastProductionScore >= world.Product(r.productId).minimumScore), Is.True);
            string after = JsonUtility.ToJson(world.State);
            Assert.That(world.AutomationServePendingOrders().Delivered, Is.Zero);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(after));
        }

        [Test] public void AllCatalogProductsAndBothOrderCupSizesPassTheRealPipeline()
        {
            world.AutomationUnlockAndRefill(); world.StartDay(false);
            Assert.That(world.AutomationServePendingOrders().StopReason, Is.Null);
            foreach (var product in world.Catalog.products)
                foreach (string cup in new[] { "cup.small", "cup.large" })
                {
                    var request = new CustomerRequest { id = "automation-test-" + product.id + cup,
                        customerId = "customer_0", productId = product.id, cupSpecId = cup, patience = 100 };
                    world.State.entities.Add(new WorldEntity { id = "customer_0", specId = "customer", area = AreaId.Cafe });
                    world.State.requests.Add(request); world.Focus(request.id);
                    int successful = world.State.successfulOrders;
                    var report = world.AutomationServePendingOrders();
                    Assert.That(report.StopReason, Is.Null, product.id + " / " + cup);
                    Assert.That(report.OrdinaryOrdersServed, Is.EqualTo(1), product.id + " / " + cup);
                    Assert.That(world.State.successfulOrders, Is.EqualTo(successful + 1));
                    Assert.That(request.lastProductionScore, Is.EqualTo(100).Within(.001f));
                }
        }

        [Test] public void NarrativeQuotasEmitActualCupReceiptsWithoutBusinessRevenueAndOnlyOnce()
        {
            world.AutomationUnlockAndRefill();
            var receipts = new List<CafeNarrativeDeliveryReceipt>();
            int money = world.State.money;
            Assert.That(world.TryBeginNarrativeService("automation-story", new[] {
                new CafeNarrativeOrderSpec { productId = "latte", minScore = 100, count = 2 },
                new CafeNarrativeOrderSpec { productId = "blackcoffee.sweet", minScore = 60 },
                new CafeNarrativeOrderSpec { productId = "blackcoffee.unsweetened", minScore = 100 },
                new CafeNarrativeOrderSpec { productId = "any.hot", minScore = 100 }
            }, receipts.Add, out var lease, out var error), Is.True, error);
            using (lease)
            {
                var report = world.AutomationServePendingOrders();
                Assert.That(report.StopReason, Is.Null);
                Assert.That(report.NarrativeDeliveries, Is.EqualTo(5));
                Assert.That(report.Attempts, Is.EqualTo(6), "Sweet variant uses one non-consuming rejection before adding sugar.");
                Assert.That(receipts, Has.Count.EqualTo(5));
                Assert.That(receipts.All(r => r.Passed), Is.True);
                Assert.That(receipts.Last().Completed, Is.True);
                Assert.That(receipts.Select(r => r.CupId).Distinct().Count(), Is.EqualTo(5));
                Assert.That(receipts.All(r => r.OwnerKey == "automation-story" && world.Find(r.CupId) == null), Is.True);
                Assert.That(world.HasNarrativeService, Is.False);
                Assert.That(world.State.open, Is.False);
                Assert.That(world.State.money, Is.EqualTo(money));
                Assert.That(world.State.todayRevenue, Is.Zero);
                Assert.That(world.State.served, Is.Zero);
                Assert.That(world.State.successfulOrders, Is.Zero);
                Assert.That(world.AutomationServePendingOrders().Delivered, Is.Zero);
                Assert.That(receipts, Has.Count.EqualTo(5));
            }
        }

        [Test] public void BatchLimitAllowsResumingNarrativeQuotasAndRejectsInvalidBoundsWithoutMutation()
        {
            world.AutomationUnlockAndRefill();
            var receipts = new List<CafeNarrativeDeliveryReceipt>();
            Assert.That(world.TryBeginNarrativeService("bounded", new[] {
                new CafeNarrativeOrderSpec { productId = "blackcoffee", minScore = 100, count = 3 }
            }, receipts.Add, out var lease, out _), Is.True);
            using (lease)
            {
                string before = JsonUtility.ToJson(world.State);
                Assert.That(world.AutomationServePendingOrders(0).StopReason, Is.Not.Null);
                Assert.That(world.AutomationServePendingOrders(33).StopReason, Is.Not.Null);
                Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
                var first = world.AutomationServePendingOrders(1);
                Assert.That(first.Attempts, Is.EqualTo(1)); Assert.That(first.ReachedLimit, Is.True);
                Assert.That(receipts, Has.Count.EqualTo(1)); Assert.That(receipts[0].Completed, Is.False);
                var rest = world.AutomationServePendingOrders(2);
                Assert.That(rest.NarrativeDeliveries, Is.EqualTo(2)); Assert.That(rest.RemainingOrders, Is.Zero);
                Assert.That(receipts.Last().Completed, Is.True);
            }
        }

        [Test] public void UnreachableNarrativeQualityStopsAfterOneRealRemakeWithoutFakePassOrInfiniteRetry()
        {
            world.AutomationUnlockAndRefill();
            var receipts = new List<CafeNarrativeDeliveryReceipt>();
            Assert.That(world.TryBeginNarrativeService("strict-sweet", new[] {
                new CafeNarrativeOrderSpec { productId = "blackcoffee.sweet", minScore = 100 }
            }, receipts.Add, out var lease, out _), Is.True);
            using (lease)
            {
                var report = world.AutomationServePendingOrders();
                Assert.That(report.StopReason, Is.Not.Null);
                Assert.That(report.Attempts, Is.EqualTo(2));
                Assert.That(receipts, Has.Count.EqualTo(1));
                Assert.That(receipts[0].Passed || receipts[0].Completed, Is.False);
                Assert.That(world.HasNarrativeService, Is.True);
                Assert.That(world.State.requests.Single().remakes, Is.EqualTo(1));
                Assert.That(world.State.served, Is.Zero);
            }
        }

        [Test] public void AuthorizedVisitGetsRealServedFeedbackButUnauthorizedVisitIsNotAdvanced()
        {
            world.AutomationUnlockAndRefill();
            Assert.That(world.SyncCafeVisits(new[] {
                Visit("authorized", true, "customer_0"), Visit("blocked", false, "customer_1")
            }).success, Is.True);
            world.StartDay(false);
            var report = world.AutomationServePendingOrders();
            Assert.That(report.StopReason, Is.Null);
            Assert.That(world.CafeVisit("authorized").status, Is.EqualTo(CafeVisitStatus.Served));
            Assert.That(world.State.cafeVisitFeedback.Count(f => f.visitInstanceId == "authorized" && f.stage == "served" && !f.isClick), Is.EqualTo(1));
            Assert.That(world.CafeVisit("blocked").status, Is.EqualTo(CafeVisitStatus.Reserved));
            Assert.That(world.State.cafeVisitFeedback.Any(f => f.visitInstanceId == "blocked" && f.stage == "served"), Is.False);
            int feedback = world.State.cafeVisitFeedback.Count;
            world.AutomationServePendingOrders();
            Assert.That(world.State.cafeVisitFeedback.Count, Is.EqualTo(feedback));
        }

        [Test] public void ExistingPlayerCupAndFocusSurviveBoundedBackgroundDelivery()
        {
            world.AutomationUnlockAndRefill(); world.StartDay(false);
            Assert.That(world.State.requests.Count, Is.GreaterThan(1));
            string focus = world.State.requests[1].id;
            world.Focus(world.State.requests[0].id);
            world.SelectServingCup("s.cups.large");
            var cup = world.ServingCup; cup.contents.Add(new Portion { specId = "milk", milliliters = 25 });
            string beforeCup = JsonUtility.ToJson(cup);
            world.AutomationServePendingOrders(1);
            Assert.That(world.ServingCup, Is.SameAs(cup));
            Assert.That(JsonUtility.ToJson(cup), Is.EqualTo(beforeCup));
            Assert.That(world.State.focusedRequestId, Is.EqualTo(focus));
        }

        [Test] public void IdleAutomationDoesNotCreateNarrativeServiceBusinessOrOrders()
        {
            world.AutomationUnlockAndRefill();
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.AutomationServePendingOrders().Attempts, Is.Zero);
            Assert.That(world.HasNarrativeService, Is.False);
            Assert.That(world.State.open, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }

        [Test] public void UnavailableCupStockStopsWithoutCreatingEntitiesOrSettlingAnOrder()
        {
            world.AutomationUnlockAndRefill(); world.StartDay(false);
            world.Find("s.cups.small").stock = world.Find("s.cups.large").stock = 0;
            string before = JsonUtility.ToJson(world.State);
            var report = world.AutomationServePendingOrders();
            Assert.That(report.StopReason, Is.Not.Null);
            Assert.That(report.Attempts, Is.Zero);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }

        [Test] public void ActiveMiniGamePreventsAutomationFromChangingThePlayerSession()
        {
            world.AutomationUnlockAndRefill(); world.StartDay(false);
            world.SelectServingCup("s.cups.large");
            Assert.That(world.RequestProductionMiniGame().success, Is.True);
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.AutomationServePendingOrders().StopReason, Is.Not.Null);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }

        [Test] public void CallbackReentrancyIsBoundedAndCannotDuplicateTheNarrativeTransaction()
        {
            world.AutomationUnlockAndRefill();
            CafeAutomationResult nested = null;
            Assert.That(world.TryBeginNarrativeService("reentrant", new[] {
                new CafeNarrativeOrderSpec { productId = "blackcoffee", minScore = 100 }
            }, _ => nested = world.AutomationServePendingOrders(), out var lease, out _), Is.True);
            using (lease)
            {
                Assert.That(world.AutomationServePendingOrders().NarrativeDeliveries, Is.EqualTo(1));
                Assert.That(nested, Is.Not.Null); Assert.That(nested.Attempts, Is.Zero);
                Assert.That(nested.StopReason, Is.Not.Null);
            }
        }

        private static CafeVisitIntent Visit(string id, bool authorized, string seat) => new CafeVisitIntent
        {
            visitInstanceId = id, characterId = id, arrivalAuthorized = authorized, preferredSeatId = seat,
            leaveAfterServed = false,
            identity = new CafePersonIdentity { personId = id, displayName = id, role = CafePersonRole.Customer }
        };
    }
}
