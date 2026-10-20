using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeCupInventoryOrderTests
    {
        private CafeWorld world;

        [SetUp]
        public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
        }

        private WorldEntity Take(string sourceId)
        {
            Assert.That(world.Take(sourceId, AreaId.Counter, 0, -2, out var cup).success, Is.True);
            return cup;
        }

        [TestCase(false, "small")]
        [TestCase(false, "large")]
        [TestCase(true, "small")]
        [TestCase(true, "large")]
        public void DiscardReturnsOnlyMatchingCupStockExactlyOnce(bool production, string size)
        {
            if (production) world.EnableProduction();
            string sourceId = "s.cups." + size, otherId = "s.cups." + (size == "small" ? "large" : "small");
            int initial = world.Find(sourceId).stock, other = world.Find(otherId).stock, beans = world.Find("s.beans").stock;
            var cup = Take(sourceId);
            Assert.That(world.Interact(Take("s.beans").id, cup.id).success, Is.True);
            string previewState = JsonUtility.ToJson(world.State);
            Assert.That(world.Preview(cup.id, "waste").success, Is.True);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(previewState));
            Assert.That(world.Interact(cup.id, "waste").success, Is.True);
            Assert.That(world.Find(cup.id), Is.Null);
            Assert.That(world.Find(sourceId).stock, Is.EqualTo(initial));
            Assert.That(world.Find(otherId).stock, Is.EqualTo(other));
            Assert.That(world.Find("s.beans").stock, Is.EqualTo(beans - 1));
            Assert.That(world.Interact(cup.id, "waste").success, Is.False);
            Assert.That(world.Find(sourceId).stock, Is.EqualTo(initial));
        }

        [TestCase("small")]
        [TestCase("large")]
        public void TrayDiscardReturnsCupAndClearsActiveAndHeatingReferences(string size)
        {
            world.EnableProduction();
            string sourceId = "s.cups." + size;
            int initial = world.Find(sourceId).stock;
            Assert.That(world.SelectServingCup(sourceId).success, Is.True);
            var cup = world.ServingCup;
            world.Find("m.stove").production.heatingVesselId = cup.id;
            Assert.That(world.DiscardProductionCup().success, Is.True);
            Assert.That(world.Find(sourceId).stock, Is.EqualTo(initial));
            Assert.That(world.ServingCup, Is.Null);
            Assert.That(world.Find("m.stove").production.heatingVesselId, Is.Null);
            Assert.That(world.DiscardProductionCup().success, Is.False);
            Assert.That(world.Find(sourceId).stock, Is.EqualTo(initial));
        }

        [TestCase(false, "small")]
        [TestCase(false, "large")]
        [TestCase(true, "small")]
        [TestCase(true, "large")]
        public void FailedDeliveryAndRemakeRecycleActualCupType(bool production, string size)
        {
            if (production) world.EnableProduction();
            world.StartDay();
            var request = world.State.requests[0];
            request.cupSpecId = "cup." + (size == "small" ? "large" : "small");
            string sourceId = "s.cups." + size;
            int initial = world.Find(sourceId).stock;
            var cup = Take(sourceId);
            cup.contents.Add(new Portion { specId = "milk", units = 1, milliliters = 80 });
            Assert.That(world.Interact(cup.id, "service").success, Is.True);
            if (production)
            {
                Assert.That(world.DeliverProductionCup().feedback, Is.EqualTo(CafeFeedbackKind.DeliveryRemake));
                Assert.That(world.Request(request.id), Is.SameAs(request));
                Assert.That(request.cupSpecId, Is.EqualTo("cup." + (size == "small" ? "large" : "small")));
            }
            Assert.That(world.Find(cup.id), Is.Null);
            Assert.That(world.Find(sourceId).stock, Is.EqualTo(initial));
            Assert.That(world.State.successfulOrders, Is.Zero);
        }

        [Test]
        public void DailyRefreshIsDateKeyedPreservesLiveCupsAndPersistsAcrossRestore()
        {
            world.EnableProduction();
            var small = Take("s.cups.small");
            var large = Take("s.cups.large");
            small.contents.Add(new Portion { specId = "milk", units = 1, milliliters = 80 });
            world.Find("s.cups.small").stock = 0;
            world.Find("s.cups.large").stock = 0;
            world.Find("s.beans").stock = 1;
            int money = world.State.money;
            Assert.That(world.RefreshDailyCupStock("dayflow:2026-07-14").success, Is.True);
            Assert.That(world.Find("s.cups.small").stock, Is.EqualTo(world.Spec("source.cups.small").initialStock - 1));
            Assert.That(world.Find("s.cups.large").stock, Is.EqualTo(world.Spec("source.cups.large").initialStock - 1));
            Assert.That(world.Find(small.id), Is.SameAs(small));
            Assert.That(small.contents.Count, Is.EqualTo(1));
            Assert.That(world.Find(large.id), Is.SameAs(large));
            Assert.That(world.Find("s.beans").stock, Is.EqualTo(1));
            Assert.That(world.State.money, Is.EqualTo(money));
            world.Find("s.cups.small").stock = 0;
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(world.Restore(snapshot).success, Is.True);
            Assert.That(world.RefreshDailyCupStock("dayflow:2026-07-14").success, Is.True);
            Assert.That(world.Find("s.cups.small").stock, Is.Zero);
            Assert.That(world.RefreshDailyCupStock("dayflow:2026-07-15").success, Is.True);
            Assert.That(world.Find("s.cups.small").stock, Is.EqualTo(world.Spec("source.cups.small").initialStock - 1));
            Assert.That(world.RefreshDailyCupStock(" ").success, Is.False);
            Assert.That(world.State.cupStockDayKey, Is.EqualTo("dayflow:2026-07-15"));
        }

        [Test]
        public void SandboxNewDayRefreshesButStartDayPreservesExternalDateKey()
        {
            world.StartDay();
            Assert.That(world.State.cupStockDayKey, Is.EqualTo("sandbox:1"));
            world.Find("s.cups.small").stock = 0;
            world.EndDay(); world.StartDay();
            Assert.That(world.Find("s.cups.small").stock, Is.EqualTo(world.Spec("source.cups.small").initialStock));
            world.EndDay();
            world.RefreshDailyCupStock("dayflow:42");
            world.Find("s.cups.small").stock = 0;
            world.StartDay();
            Assert.That(world.State.cupStockDayKey, Is.EqualTo("dayflow:42"));
            Assert.That(world.Find("s.cups.small").stock, Is.Zero);
        }

        [Test]
        public void RegularOrdersRandomizeBothSizesReproduciblyWithoutChangingCatalog()
        {
            var other = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            var seen = new HashSet<string>();
            string defaultCup = world.Product("espresso").cupSpecId;
            for (int day = 0; day < 8; day++)
            {
                world.StartDay(); other.StartDay();
                for (int i = 0; i < world.State.requests.Count; i++)
                {
                    var request = world.State.requests[i];
                    seen.Add(request.cupSpecId);
                    Assert.That(request.cupSpecId, Is.EqualTo(other.State.requests[i].cupSpecId));
                }
                world.EndDay(); other.EndDay();
            }
            Assert.That(seen, Is.EquivalentTo(new[] { "cup.small", "cup.large" }));
            Assert.That(world.Product("espresso").cupSpecId, Is.EqualTo(defaultCup));
        }

        [TestCase("cup.small", 1f)]
        [TestCase("cup.large", 400f / 240f)]
        public void RequestedCupScalesRecommendedAmountAndEvaluation(string cupSpecId, float scale)
        {
            world.EnableProduction(); world.StartDay();
            var request = world.State.requests[0];
            request.productId = "blackcoffee"; request.cupSpecId = cupSpecId;
            Assert.That(world.SelectServingCup("s.cups." + (cupSpecId == "cup.small" ? "small" : "large")).success, Is.True);
            var cup = world.ServingCup;
            float ml = 240 * scale;
            cup.contents.Add(new Portion { specId = "coffee", coffeeKind = "black", units = 1, milliliters = ml,
                temperatureCelsius = 95, productionScore = 100,
                composition = new List<ConstituentAmount> {
                    new ConstituentAmount { ingredientId = "grounds", grams = ml * .06f },
                    new ConstituentAmount { ingredientId = "water", milliliters = ml } } });
            Assert.That(world.OrderPortionScale(request), Is.EqualTo(scale).Within(.001f));
            Assert.That(world.EvaluateProduction(cup, world.Product(request.productId), cupSpecId).total, Is.GreaterThan(99));
            Assert.That(world.MatchesOrder(cup, request), Is.True);
            string sourceId = "s.cups." + (cupSpecId == "cup.small" ? "small" : "large");
            int before = world.Find(sourceId).stock;
            var delivered = world.DeliverProductionCup();
            Assert.That(delivered.feedback, Is.EqualTo(CafeFeedbackKind.DeliverySatisfied), delivered.message);
            Assert.That(world.Find(sourceId).stock, Is.EqualTo(before + 1));
            Assert.That(world.State.successfulOrders, Is.EqualTo(1));
        }

        [TestCase("cup.small")]
        [TestCase("cup.large")]
        public void EveryProductFitsRequestedCapacityAndRetainsIdealScore(string cupSpecId)
        {
            world.EnableProduction();
            foreach (var product in world.Catalog.products)
            {
                var request = new CustomerRequest { productId = product.id, cupSpecId = cupSpecId };
                float scale = world.OrderPortionScale(request);
                var cup = new WorldEntity { specId = cupSpecId };
                foreach (var need in product.evaluation.amounts)
                {
                    var portion = new Portion { specId = need.ingredientId, coffeeKind = need.coffeeKind,
                        units = 1, milliliters = need.idealMl * scale, grams = need.idealGrams * scale,
                        temperatureCelsius = need.coffeeKind == "cold_brew" ? 22 : 95, productionScore = 100 };
                    if (need.ingredientId == "coffee")
                    {
                        world.AddConstituent(portion, "grounds", portion.milliliters * .06f, 0);
                        world.AddConstituent(portion, "water", 0, portion.milliliters);
                    }
                    cup.contents.Add(portion);
                }
                cup.latteArt = true;
                Assert.That(world.LiquidVolume(cup), Is.LessThanOrEqualTo(world.LiquidCapacity(cup) + .01f), product.id);
                Assert.That(world.EvaluateProduction(cup, product, cupSpecId).total, Is.GreaterThan(99), product.id);
                Assert.That(world.ClassifyCoffee(cup).productId, Is.EqualTo(product.id), product.id);
            }
        }

        [TestCase(40, false)]
        [TestCase(80, false)]
        [TestCase(240, true)]
        [TestCase(320, true)]
        [TestCase(400, true)]
        public void SmallOrderAcceptsFreeLargerCupUnlessServingIsTooSmall(float ml, bool accepted)
        {
            world.EnableProduction(); world.StartDay();
            var request = world.State.requests[0];
            request.productId = "blackcoffee"; request.cupSpecId = "cup.small";
            world.SelectServingCup("s.cups.large");
            var cup = world.ServingCup;
            cup.contents.Add(new Portion { specId = "coffee", coffeeKind = "black", units = 1,
                milliliters = ml, temperatureCelsius = 95, productionScore = 100,
                composition = new List<ConstituentAmount> {
                    new ConstituentAmount { ingredientId = "grounds", grams = ml * .06f },
                    new ConstituentAmount { ingredientId = "water", milliliters = ml } } });
            Assert.That(world.ClassifyCoffee(cup).productId, Is.EqualTo("blackcoffee"));
            Assert.That(world.MatchesOrder(cup, request), Is.EqualTo(accepted));
            var delivery = world.DeliverProductionCup();
            Assert.That(delivery.feedback, Is.EqualTo(accepted ? CafeFeedbackKind.DeliverySatisfied : CafeFeedbackKind.DeliveryRemake), delivery.message);
            Assert.That(request.cupSpecId, Is.EqualTo("cup.small"));
            Assert.That(world.State.successfulOrders, Is.EqualTo(accepted ? 1 : 0));
            Assert.That(world.Find("s.cups.large").stock, Is.EqualTo(world.Spec("source.cups.large").initialStock));
        }

        [Test]
        public void LargeOrderStillRejectsSmallerCup()
        {
            world.EnableProduction(); world.StartDay();
            var request = world.State.requests[0];
            request.productId = "blackcoffee"; request.cupSpecId = "cup.large";
            world.SelectServingCup("s.cups.small");
            world.ServingCup.contents.Add(new Portion { specId = "coffee", coffeeKind = "black", units = 1,
                milliliters = 240, temperatureCelsius = 95, productionScore = 100 });
            Assert.That(world.MatchesOrder(world.ServingCup, request), Is.False);
            Assert.That(world.DeliverProductionCup().feedback, Is.EqualTo(CafeFeedbackKind.DeliveryRemake));
        }

        [Test]
        public void FreeCupUpgradeDoesNotExcuseWrongDrinkOrPoorExtraction()
        {
            world.EnableProduction(); world.StartDay();
            var request = world.State.requests[0];
            request.productId = "blackcoffee"; request.cupSpecId = "cup.small";
            world.SelectServingCup("s.cups.large");
            var cup = world.ServingCup;
            cup.contents.Add(new Portion { specId = "coffee", coffeeKind = "cold_brew", units = 1,
                milliliters = 320, temperatureCelsius = 22 });
            Assert.That(world.MatchesOrder(cup, request), Is.False);
            cup.contents[0].coffeeKind = "black";
            cup.contents[0].temperatureCelsius = 95;
            cup.contents[0].productionScore = 0;
            Assert.That(world.MatchesOrder(cup, request), Is.False);
        }

        [Test]
        public void AutomaticSuccessCountsProgressWithoutCreatingCupStock()
        {
            world.StartDay();
            foreach (var request in world.State.requests) request.automaticRemaining = 0;
            int count = world.State.requests.Count;
            int small = world.Find("s.cups.small").stock, large = world.Find("s.cups.large").stock;
            world.Tick(.1f);
            Assert.That(world.State.successfulOrders, Is.EqualTo(count));
            Assert.That(world.Find("s.cups.small").stock, Is.EqualTo(small));
            Assert.That(world.Find("s.cups.large").stock, Is.EqualTo(large));
        }

        [Test]
        public void RestorePreservesCupRequirementsAndRejectsUnknownCupWithoutReplacingWorld()
        {
            world.StartDay();
            var saved = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            string required = saved.requests[0].cupSpecId;
            Assert.That(world.Restore(saved).success, Is.True);
            Assert.That(world.RequiredCupSpecId(world.State.requests[0]), Is.EqualTo(required));
            saved = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            saved.requests[0].cupSpecId = null;
            Assert.That(world.Restore(saved).success, Is.True);
            Assert.That(world.RequiredCupSpecId(world.State.requests[0]), Is.EqualTo(world.Product(saved.requests[0].productId).cupSpecId));
            var current = world.State;
            saved = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            saved.requests[0].cupSpecId = "container.water";
            Assert.That(world.Restore(saved).success, Is.False);
            Assert.That(world.State, Is.SameAs(current));
        }

        [Test]
        public void TutorialUsesFixedLargeTeachingCupWithoutDrawingRandomness()
        {
            world.EnableProduction();
            uint random = world.State.randomState;
            Assert.That(world.StartTutorialCustomer().success, Is.True);
            Assert.That(world.RequiredCupSpecId(world.Request(CafeWorld.TutorialOrderId)), Is.EqualTo("cup.large"));
            Assert.That(world.State.randomState, Is.EqualTo(random));
        }
    }
}
