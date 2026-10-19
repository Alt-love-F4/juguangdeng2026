using System;
using NUnit.Framework;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>Headless domain transactions using the shipped catalog, not synthetic success callbacks.</summary>
    public sealed class NarrativeCafeGameplayBridgeTests
    {
        private CafeWorld world;
        private NarrativeCafeGameplayBridge bridge;
        private int callbacks;
        private NarrativeGameplayResult result;

        [SetUp]
        public void SetUp()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction();
            world.EnterProduction();
            callbacks = 0;
            result = null;
        }

        [TearDown]
        public void TearDown() => bridge?.Dispose();

        private void Begin(string key)
        {
            Assert.That(NarrativeCafeGameplayBridge.TryStart(world, key, value => { callbacks++; result = value; },
                out bridge, out string error), Is.True, error);
            Assert.That(callbacks, Is.Zero, "Starting a gameplay page is never itself success.");
        }

        private WorldEntity Prepare(string productId = null, float extractionScore = 100, bool flexible = false)
        {
            Assert.That(world.State.requests, Is.Not.Empty);
            var request = world.Request(world.State.focusedRequestId);
            if (productId != null && !flexible) Assert.That(request.productId, Is.EqualTo(productId));
            string productIdForCup = flexible ? productId : request.productId;
            string cupSize = flexible ? world.Product(productIdForCup).cupSpecId : world.RequiredCupSpecId(request);
            Assert.That(world.SelectServingCup(cupSize == "cup.small" ? "s.cups.small" : "s.cups.large").success, Is.True);
            var cup = world.ServingCup;
            var product = world.Product(productIdForCup);
            float scale = flexible ? 1 : world.OrderPortionScale(request);
            foreach (var need in product.evaluation.amounts)
            {
                var temperature = need.coffeeKind == "black" ? LiquidTemperature.Warm : LiquidTemperature.Room;
                if (need.minimumTemperature >= 0 && (int)temperature < need.minimumTemperature)
                    temperature = (LiquidTemperature)need.minimumTemperature;
                var portion = new Portion { specId = need.ingredientId, milliliters = need.idealMl * scale,
                    grams = need.idealGrams * scale, temperature = temperature,
                    coffeeKind = need.coffeeKind, productionScore = extractionScore };
                if (portion.specId == "coffee")
                {
                    world.AddConstituent(portion, "water", 0, portion.milliliters);
                    world.AddConstituent(portion, "grounds", portion.milliliters * product.evaluation.idealCoffeeStrength, 0);
                }
                cup.contents.Add(portion);
            }
            cup.latteArt = Array.Exists(product.evaluation.amounts, need => need.ingredientId == "milkfoam");
            world.FinishPour(cup.id); // Public change notification after test preparation.
            if (flexible) Assert.That(world.ClassifyCoffee(cup).productId, Is.EqualTo(productIdForCup));
            else Assert.That(world.MatchesOrder(cup, request), Is.True);
            return cup;
        }

        private void Deliver()
        {
            var delivery = world.DeliverProductionCup();
            Assert.That(delivery.success, Is.True, delivery.message);
            Assert.That(delivery.feedback, Is.EqualTo(CafeFeedbackKind.DeliverySatisfied), delivery.message);
        }

        [TestCase("serve:blackcoffee:60:1")]
        [TestCase("serve:espresso:90:2+milkcoffee:60:1")]
        [TestCase("serve:latte:60.25:1+cocoa:0:1")]
        [TestCase("taste:blackcoffee:90:1")]
        [TestCase("taste:espresso:90:1+milkcoffee:60.25:2")]
        [TestCase("serve:any:60:1")] [TestCase("taste:any:90:2")]
        [TestCase("serve:any:60:1+espresso:90:1")]
        [TestCase("serve:any.hot:60:1")] [TestCase("taste:any.hot:60:1")]
        [TestCase("serve:blackcoffee.sweet:60:1")]
        [TestCase("taste:blackcoffee.unsweetened:60:1")]
        public void KeysUseRealCatalogAndInvariantFloatScores(string key)
            => Assert.That(NarrativeCafeGameplayBridge.ValidateKey(world, key, out string error), Is.True, error);

        [TestCase(null)] [TestCase("")] [TestCase("brew:espresso:60:1")]
        [TestCase("serve:black_coffee:60:1")] [TestCase("serve:espresso:normal:1")]
        [TestCase("serve:espresso:NaN:1")] [TestCase("serve:espresso:Infinity:1")]
        [TestCase("serve:espresso:100.1:1")] [TestCase("serve:espresso:-1:1")]
        [TestCase("serve:espresso:60:0")] [TestCase("serve:espresso:60:-1")]
        [TestCase("serve:espresso:60:2147483648")] [TestCase("serve:espresso:60:1+")]
        [TestCase("serve:espresso:60:1+serve:latte:60:1")]
        [TestCase("taste:espresso:90:0")] [TestCase("taste:black_coffee:60:1")]
        [TestCase("taste:espresso:NaN:1")]
        [TestCase("serve:missing:60:1")] [TestCase("taste:missing:60:1")]
        [TestCase("serve:Any:60:1")] [TestCase("serve:anything:60:1")]
        [TestCase("serve:any.cold:60:1")] [TestCase("taste:espresso.sweet:60:1")]
        [TestCase("serve:blackcoffee.unknown:60:1")]
        public void InvalidKeysFailClosedWithoutWorldMutation(string key)
        {
            string before = JsonUtility.ToJson(world.State);
            Assert.That(NarrativeCafeGameplayBridge.ValidateKey(world, key, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }

        [Test]
        public void OnlyActualValidDeliveryPassesAndDoesNotRunOrdinaryEconomy()
        {
            bool open = world.State.open;
            int day = world.State.day, money = world.State.money, served = world.State.served, reputation = world.State.reputation;
            Begin("serve:espresso:90:1");
            var cup = Prepare();
            Assert.That(callbacks, Is.Zero, "Creating a real order and a completed tray cup is not delivery.");
            string cupId = cup.id;
            Deliver();
            Assert.That(world.Find(cupId), Is.Null);
            Assert.That(result.passed, Is.True);
            Assert.That(result.cancelled, Is.False);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(world.State.open, Is.EqualTo(open));
            Assert.That(world.State.day, Is.EqualTo(day));
            Assert.That(world.State.money, Is.EqualTo(money));
            Assert.That(world.State.served, Is.EqualTo(served));
            Assert.That(world.State.reputation, Is.EqualTo(reputation));
            world.FinishPour(null);
            bridge.Cancel("duplicate cancellation");
            Assert.That(callbacks, Is.EqualTo(1));
        }

        [Test]
        public void MultiRequirementConsumesDistinctPhysicalCupsThroughRealTargetedOrders()
        {
            Begin("serve:espresso:60:2+milkcoffee:90:1");
            string firstId = Prepare("espresso").id;
            Deliver();
            world.FinishPour(null);
            Assert.That(callbacks, Is.Zero);
            string secondId = Prepare("espresso").id;
            Assert.That(secondId, Is.Not.EqualTo(firstId));
            Deliver();
            Assert.That(callbacks, Is.Zero);
            Prepare("milkcoffee");
            Deliver();
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.passed, Is.True);
        }

        [Test]
        public void ServeLowScoreWaitsForRetryAndNeverRoundsUp()
        {
            Begin("serve:espresso:99.99:1");
            var cup = Prepare(extractionScore: 73.125f);
            var request = world.Request(world.State.focusedRequestId);
            float actual = world.EvaluateProduction(cup, world.Product(request.productId), world.RequiredCupSpecId(request)).total;
            Assert.That(actual, Is.LessThan(99.99f));
            Assert.That(world.DeliverProductionCup().feedback, Is.EqualTo(CafeFeedbackKind.DeliveryRemake));
            Assert.That(world.Find(cup.id), Is.Null, "Low serve attempts consume the actual cup.");
            Assert.That(world.Request(request.id), Is.SameAs(request));
            Assert.That(callbacks, Is.Zero);
            Prepare();
            Deliver();
            Assert.That(result.passed, Is.True, "A retry must not carry a failed taste outcome into a passing quota.");
        }

        [Test]
        public void TasteLowScoreCompletesWithPassedFalseNotCancellation()
        {
            Begin("taste:espresso:99.99:1");
            var cup = Prepare(extractionScore: 73.125f);
            Deliver();
            Assert.That(world.Find(cup.id), Is.Null);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.passed, Is.False);
            Assert.That(result.cancelled, Is.False);
        }

        [Test]
        public void TasteAggregatesEveryRealAttemptAndCompletesOnlyAfterFinalCup()
        {
            Begin("taste:espresso:99.99:2");
            Prepare(extractionScore: 50);
            Deliver();
            Assert.That(callbacks, Is.Zero);
            Prepare();
            Deliver();
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.passed, Is.False);
            Assert.That(result.cancelled, Is.False);
        }

        [Test]
        public void DiscardCannotProduceAuthoritativeDeliveryReceipt()
        {
            Begin("serve:espresso:60:1");
            Prepare();
            Assert.That(world.DiscardProductionCup().success, Is.True);
            Assert.That(callbacks, Is.Zero);
            Prepare();
            Deliver();
            Assert.That(result.passed, Is.True);
        }

        [Test]
        public void ChangingOrdinaryCountersCannotPassTargetedService()
        {
            Begin("serve:espresso:60:1");
            Prepare();
            world.State.successfulOrders++;
            world.State.todayServed++;
            world.State.money += 100;
            world.FinishPour(null);
            Assert.That(callbacks, Is.Zero, "Only an authoritative delivery receipt completes narrative gameplay.");
        }

        [Test]
        public void DisposeUnsubscribesAndNeverCompletesStaleOwner()
        {
            Begin("serve:espresso:60:1");
            Prepare();
            bridge.Dispose();
            bridge.Dispose();
            Assert.That(world.DeliverProductionCup().success, Is.False, "Disposed service no longer owns an order.");
            Assert.That(world.HasNarrativeService, Is.False);
            Assert.That(callbacks, Is.Zero);
        }

        [Test]
        public void ClosingBusinessCancelsOnceWithoutCalendarAdvance()
        {
            int day = world.State.day;
            Assert.That(world.StartDay().success, Is.True);
            // Ordinary business must already be running. Starting it during a narrative lease
            // is intentionally blocked; give this test fixture one spare, non-displacing seat.
            world.State.seats++;
            Begin("serve:espresso:60:1");
            Prepare();
            Assert.That(world.EndDay().success, Is.True);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.cancelled, Is.True);
            Assert.That(result.passed, Is.False);
            Assert.That(world.State.day, Is.EqualTo(day));
            bridge.Cancel("again");
            Assert.That(callbacks, Is.EqualTo(1));
        }

        [Test]
        public void DomainServiceCancellationWhileAlreadyClosedCancelsBridgeExactlyOnce()
        {
            Begin("serve:espresso:60:1");
            string requestId = world.State.focusedRequestId;
            Assert.That(world.State.open, Is.False);
            int day = world.State.day;
            // The existing domain EndDay disposes its narrative lease even if ordinary business
            // was never open. There is no open→closed counter transition to infer here.
            Assert.That(world.EndDay().success, Is.False);
            Assert.That(world.HasNarrativeService, Is.False);
            Assert.That(world.Request(requestId), Is.Null);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.cancelled, Is.True);
            Assert.That(result.passed, Is.False);
            Assert.That(world.State.day, Is.EqualTo(day));
            world.FinishPour(null);
            bridge.Cancel("duplicate cancellation");
            Assert.That(callbacks, Is.EqualTo(1));
        }

        [Test]
        public void RestoringWorldSnapshotCancelsPendingOwnerAndCupObservation()
        {
            // Gameplay saves are blocked: restore a valid pre-service snapshot, not a forbidden
            // snapshot containing runtime-only narrative reservations.
            var saved = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Begin("serve:espresso:60:1");
            Assert.That(world.SelectServingCup("s.cups.small").success, Is.True);
            var restored = world.Restore(saved);
            Assert.That(restored.success, Is.True, restored.message);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.cancelled, Is.True);
        }

        [Test]
        public void ClosedWorldSupportsActualServiceWithoutReopeningOrCalendarAdvance()
        {
            world.StartDay();
            world.EndDay();
            int day = world.State.day, money = world.State.money, reputation = world.State.reputation;
            Begin("serve:blackcoffee:60:1");
            Assert.That(callbacks, Is.Zero);
            Assert.That(world.State.open, Is.False);
            Assert.That(world.State.businessPhase, Is.EqualTo(BusinessPhase.Night));
            Prepare("blackcoffee");
            Deliver();
            Assert.That(result.passed, Is.True);
            Assert.That(world.State.day, Is.EqualTo(day));
            Assert.That(world.State.open, Is.False);
            Assert.That(world.State.money, Is.EqualTo(money));
            Assert.That(world.State.reputation, Is.EqualTo(reputation));
        }

        [Test]
        public void DisposingLeasePreservesOrdinaryOrdersAndRestoresFocusedRequest()
        {
            world.State.seats = 4;
            world.StartDay();
            world.State.seats = 5; // One spare domain seat in this test fixture.
            string previousFocus = world.State.focusedRequestId;
            var ordinary = world.State.requests.ToArray();
            Begin("serve:espresso:60:1");
            string ownedRequest = world.State.focusedRequestId;
            bridge.Dispose();
            Assert.That(world.Request(ownedRequest), Is.Null);
            foreach (var request in ordinary) Assert.That(world.Request(request.id), Is.SameAs(request));
            Assert.That(world.State.focusedRequestId, Is.EqualTo(previousFocus));
            Assert.That(callbacks, Is.Zero);
        }
        [TestCase("serve:any:60:1")]
        [TestCase("taste:any:60:1")]
        public void WildcardRequiresActualClassifiedDrinkNotDefaultUiProduct(string key)
        {
            Begin(key);
            Assert.That(bridge.GameplayHint, Does.Contain("任意饮品"));
            string displayedProduct = world.Request(world.State.focusedRequestId).productId;
            string actualProduct = displayedProduct == "milkcoffee" ? "espresso" : "milkcoffee";
            var cup = Prepare(actualProduct, flexible: true);
            Assert.That(callbacks, Is.Zero, "A wildcard request and an actual cup are not yet a delivery.");
            Assert.That(actualProduct, Is.Not.EqualTo(displayedProduct));
            Deliver();
            Assert.That(world.Find(cup.id), Is.Null);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.passed, Is.True);
            Assert.That(result.cancelled, Is.False);
        }

        [TestCase("serve:any:0:1")]
        [TestCase("taste:any:0:1")]
        public void WildcardNeverAcceptsEmptyOrUnknownBeverage(string key)
        {
            Begin(key);
            Assert.That(world.SelectServingCup("s.cups.large").success, Is.True);
            var cup = world.ServingCup;
            Assert.That(world.DeliverProductionCup().success, Is.False);
            cup.contents.Add(new Portion { specId = "water", milliliters = 100, units = 1 });
            Assert.That(world.ClassifyCoffee(cup).known, Is.False);
            Assert.That(world.DeliverProductionCup().success, Is.False);
            Assert.That(world.Find(cup.id), Is.SameAs(cup));
            Assert.That(callbacks, Is.Zero);
        }

        [Test]
        public void WildcardTasteLowScoreIsRealFailedOutcome()
        {
            Begin("taste:any:99.99:1");
            Prepare("espresso", extractionScore: 40, flexible: true);
            Deliver();
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.passed, Is.False);
            Assert.That(result.cancelled, Is.False);
        }

        [Test]
        public void MixedWildcardAndFixedQuotasKeepTheirOwnRealOrderSemantics()
        {
            Begin("serve:any:60:1+espresso:90:1");
            Prepare("milkcoffee", flexible: true);
            Deliver();
            Assert.That(callbacks, Is.Zero);
            Assert.That(world.Request(world.State.focusedRequestId).productId, Is.EqualTo("espresso"));
            Prepare("espresso");
            Deliver();
            Assert.That(result.passed, Is.True);
        }

        [Test]
        public void SweetTokenKeepsActualSugarConstraintAndShowsInstruction()
        {
            Begin("serve:blackcoffee.sweet:0:1");
            Assert.That(bridge.GameplayHint, Does.Contain("需要一杯加糖黑咖啡"));
            var cup = Prepare("blackcoffee");
            Assert.That(world.DeliverProductionCup().success, Is.False, "No sugar cannot satisfy the sweet token; it must not have been stripped.");
            Assert.That(callbacks, Is.Zero);
            cup.contents.Add(new Portion { specId = "sugar", grams = 5, units = 1 });
            Deliver();
            Assert.That(result.passed, Is.True);
        }

        [Test]
        public void UnsweetenedTokenRejectsSugarButAcceptsRealUnsweetenedCoffee()
        {
            Begin("taste:blackcoffee.unsweetened:0:1");
            Assert.That(bridge.GameplayHint, Does.Contain("需要一杯无糖黑咖啡"));
            var cup = Prepare("blackcoffee");
            cup.contents.Add(new Portion { specId = "sugar", grams = 5, units = 1 });
            Assert.That(world.DeliverProductionCup().success, Is.False, "Sugar cannot satisfy the unsweetened token.");
            Assert.That(callbacks, Is.Zero);
            world.DiscardProductionCup();
            Prepare("blackcoffee");
            Deliver();
            Assert.That(result.passed, Is.True);
        }

        [TestCase("serve:any.hot:0:1")]
        [TestCase("taste:any.hot:0:1")]
        public void HotTokenRequiresActualTemperatureAndClassifiedDrink(string key)
        {
            Begin(key);
            Assert.That(bridge.GameplayHint, Does.Contain("需要一杯热饮"));
            var cup = Prepare("espresso", flexible: true);
            foreach (var portion in cup.contents) world.SetTemperature(portion, 39.99f);
            Assert.That(world.ClassifyCoffee(cup).known, Is.True);
            Assert.That(world.TemperatureCelsius(cup), Is.LessThan(40));
            Assert.That(world.DeliverProductionCup().success, Is.False, "Cold liquid cannot satisfy any.hot; token must reach the domain intact.");
            Assert.That(callbacks, Is.Zero);
            foreach (var portion in cup.contents) world.SetTemperature(portion, 40);
            Assert.That(world.ClassifyCoffee(cup).known, Is.True);
            Deliver();
            Assert.That(result.passed, Is.True);
            Assert.That(result.cancelled, Is.False);
        }

    }
}
