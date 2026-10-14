using System;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeClassificationTests
    {
        private CafeWorld world;

        [SetUp]
        public void Setup() => world = new CafeWorld(LoadCatalog());

        private static CafeCatalog LoadCatalog()
        {
            var resource = Resources.Load<TextAsset>("FreeCafe/catalog");
            Assert.That(resource, Is.Not.Null, "Tests must use the shipped Resources catalog.");
            return JsonUtility.FromJson<CafeCatalog>(resource.text);
        }

        private WorldEntity RecommendedCup(ProductSpec product, float fraction = 1)
        {
            var cup = new WorldEntity { id = "classification.cup", specId = product.cupSpecId };
            foreach (var need in product.evaluation.amounts)
            {
                // Current temperature determines ordinary coffee kind; essence survives any temperature.
                var temperature = need.coffeeKind == "black" ? LiquidTemperature.Warm : LiquidTemperature.Room;
                if (need.minimumTemperature >= 0 && (int)temperature < need.minimumTemperature)
                    temperature = (LiquidTemperature)need.minimumTemperature;
                var portion = new Portion
                {
                    specId = need.ingredientId,
                    milliliters = need.idealMl * fraction,
                    grams = need.idealGrams * fraction,
                    temperature = temperature,
                    coffeeKind = need.coffeeKind,
                    productionScore = 100
                };
                if (portion.specId == "coffee")
                {
                    world.AddConstituent(portion, "water", 0, portion.milliliters);
                    world.AddConstituent(portion, "grounds",
                        portion.milliliters * product.evaluation.idealCoffeeStrength, 0);
                    Assert.That(world.CoffeeKind(portion), Is.EqualTo(need.coffeeKind));
                }
                cup.contents.Add(portion);
            }
            cup.latteArt = Array.Exists(product.evaluation.amounts, need => need.ingredientId == "milkfoam");
            return cup;
        }

        private static void AssertUnknown(CafeCoffeeClassification result, string label = "迷之饮品")
        {
            Assert.That(result.known, Is.False);
            Assert.That(result.productId, Is.Null);
            Assert.That(result.label, Is.EqualTo(label));
            Assert.That(result.amountScore, Is.Zero);
            Assert.That(result.standardScore, Is.Zero);
            Assert.That(result.concentrationScore, Is.Zero);
            Assert.That(result.total, Is.Zero);
        }

        [TestCase("latte")] [TestCase("honeylatte")] [TestCase("macchiato")]
        public void RawFoamDoesNotSatisfyLatteArtButCompletionAllowsAnyPatternOrScore(string id)
        {
            var product = world.Product(id);
            var cup = RecommendedCup(product); cup.latteArt = false;
            AssertUnknown(world.ClassifyCoffee(cup));
            var raw = world.EvaluateProductComposition(cup, product);
            float remaining = 100f * (product.evaluation.amounts.Length - 1) / product.evaluation.amounts.Length;
            Assert.That(raw.amountScore, Is.EqualTo(remaining).Within(.001f));
            Assert.That(raw.standardScore, Is.EqualTo(remaining).Within(.001f));
            Assert.That(world.LatteArtFoamVolume(cup), Is.Zero);
            cup.latteArt = true; cup.latteArtScore = 0; cup.latteArtPattern = "unknown";
            product.preferredLatteArtPattern = "heart";
            Assert.That(world.ClassifyCoffee(cup).productId, Is.EqualTo(id));
            Assert.That(world.EvaluateProductComposition(cup, product).total, Is.EqualTo(100).Within(.001f));
            Assert.That(world.EvaluateProduction(cup, product).total, Is.EqualTo(100).Within(.001f));
            Assert.That(world.LatteArtFoamVolume(cup), Is.GreaterThan(0));
            cup.latteArtPattern = "custom";
            Assert.That(world.ClassifyCoffee(cup).productId, Is.EqualTo(id));
        }

        [Test] public void FinishedFlagCannotInventFoamFromZeroVolumeOrUnits()
        {
            var product = world.Product("macchiato");
            var cup = RecommendedCup(product);
            var foam = cup.contents.Find(p => p.specId == "milkfoam");
            foam.milliliters = 0; foam.units = 100;
            Assert.That(world.LatteArtFoamVolume(cup), Is.Zero);
            Assert.That(world.ClassifyCoffee(cup).productId, Is.Not.EqualTo("macchiato"));
            Assert.That(world.EvaluateProductComposition(cup, product).amountScore, Is.EqualTo(50).Within(.001f));
        }

        [Test]
        public void ResourceCatalogContainsAllElevenClassifiableProducts()
        {
            Assert.That(world.Catalog.products, Has.Length.EqualTo(11));
            foreach (var product in world.Catalog.products)
            {
                Assert.That(product.evaluation, Is.Not.Null, product.id);
                Assert.That(product.evaluation.classify, Is.True, product.id);
            }
        }

        [TestCase("espresso", "精粹咖啡")]
        [TestCase("latte", "拿铁")]
        [TestCase("honeylatte", "蜂蜜拿铁")]
        [TestCase("matchalatte", "抹茶拿铁")]
        [TestCase("cocoa", "热可可")]
        [TestCase("macchiato", "玛奇朵")]
        [TestCase("mocha", "摩卡")]
        [TestCase("blackcoffee", "黑咖啡")]
        [TestCase("creamcoffee", "奶油黑咖啡")]
        [TestCase("cold_brew", "冷萃咖啡")]
        [TestCase("milkcoffee", "奶咖啡")]
        public void RecommendedAndQuarterServingsKeepKnownIdAndLabelWhileAmountScoresDiffer(string id, string label)
        {
            var product = world.Product(id);
            Assert.That(product, Is.Not.Null);
            var recommended = world.ClassifyCoffee(RecommendedCup(product));
            Assert.That(recommended.known, Is.True);
            Assert.That(recommended.productId, Is.EqualTo(id));
            Assert.That(recommended.label, Is.EqualTo(label));
            Assert.That(recommended.amountScore, Is.EqualTo(100).Within(.001f));
            Assert.That(recommended.standardScore, Is.EqualTo(100).Within(.001f));
            Assert.That(recommended.total, Is.EqualTo(100).Within(.001f));
            Assert.That(recommended.description, Does.StartWith("精品"));

            var smallCup = RecommendedCup(product, .25f);
            var small = world.ClassifyCoffee(smallCup);
            Assert.That(small.known, Is.True);
            Assert.That(small.productId, Is.EqualTo(id));
            Assert.That(small.label, Is.EqualTo(label));
            Assert.That(small.amountScore, Is.LessThan(recommended.amountScore));
            Assert.That(small.standardScore, Is.EqualTo(100).Within(.001f));
            Assert.That(small.total, Is.LessThan(product.minimumScore), "A poor serving must retain its category.");
            if (Array.Exists(product.evaluation.amounts, a => a.ingredientId == "coffee"))
            {
                Assert.That(recommended.concentrationScore, Is.EqualTo(100).Within(.001f));
                Assert.That(small.concentrationScore, Is.EqualTo(100).Within(.001f));
            }
        }

        [TestCase("latte", .0001f)]
        [TestCase("latte", .029f)]
        [TestCase("latte", .03f)]
        [TestCase("latte", .12f)]
        [TestCase("latte", .121f)]
        [TestCase("latte", .99f)]
        [TestCase("honeylatte", .0001f)]
        [TestCase("honeylatte", .029f)]
        [TestCase("honeylatte", .03f)]
        [TestCase("honeylatte", .12f)]
        [TestCase("honeylatte", .121f)]
        [TestCase("honeylatte", .99f)]
        public void AnyPositiveFoamRatioKeepsTheDefinedCategory(string id, float ratio)
        {
            var cup = RecommendedCup(world.Product(id));
            cup.contents.Find(p => p.specId == "coffee").milliliters = 100 * (1 - ratio) * .45f;
            cup.contents.Find(p => p.specId == "milk").milliliters = 100 * (1 - ratio) * .55f;
            cup.contents.Find(p => p.specId == "milkfoam").milliliters = 100 * ratio;
            var result = world.ClassifyCoffee(cup);
            Assert.That(result.known, Is.True);
            Assert.That(result.productId, Is.EqualTo(id));
        }

        [TestCase("honeylatte", "honey")]
        [TestCase("mocha", "cocoa.powder")]
        [TestCase("mocha", "cream")]
        [TestCase("creamcoffee", "cream")]
        public void ResourceSolidsNeedOnlyAPositiveRecordedAmount(string id, string ingredient)
        {
            var product = world.Product(id);
            var cup = RecommendedCup(product);
            cup.contents.Find(p => p.specId == ingredient).grams = .0001f;
            Assert.That(world.ClassifyCoffee(cup).productId, Is.EqualTo(id));
        }

        [TestCase("honeylatte", "honey")]
        [TestCase("mocha", "cocoa.powder")]
        [TestCase("mocha", "cream")]
        [TestCase("creamcoffee", "cream")]
        public void ConfiguredSolidBoundsAreIgnoredButZeroCannotUseAUnitFallback(string id, string ingredient)
        {
            var catalog = LoadCatalog();
            var configured = Array.Find(catalog.products, p => p.id == id);
            var need = Array.Find(configured.evaluation.amounts, a => a.ingredientId == ingredient);
            need.classificationMinimumGrams = .1f;
            need.classificationMaximumGrams = .2f;
            // Isolate this recipe: removing honey or cream could otherwise identify a simpler product.
            foreach (var product in catalog.products) product.evaluation.classify = product.id == id;
            world = new CafeWorld(catalog);
            var cup = RecommendedCup(world.Product(id));
            var solid = cup.contents.Find(p => p.specId == ingredient);
            solid.units = 20;
            foreach (float amount in new[] { .0001f, .099f, .1f, .2f, .3f })
            {
                solid.grams = amount;
                var result = world.ClassifyCoffee(cup);
                Assert.That(result.known, Is.True, "Actual grams: " + amount);
                Assert.That(result.productId, Is.EqualTo(id));
            }
            solid.grams = 0;
            AssertUnknown(world.ClassifyCoffee(cup));
        }

        [TestCase(1, 350, .0001f)]
        [TestCase(350, 1, 49)]
        public void NonproportionalPositiveLatteAmountsIdentifyWithoutMutationButScoreLow(
            float coffeeMl, float milkMl, float foamMl)
        {
            var product = world.Product("latte");
            var cup = RecommendedCup(product);
            cup.contents.Find(p => p.specId == "coffee").milliliters = coffeeMl;
            cup.contents.Find(p => p.specId == "milk").milliliters = milkMl;
            cup.contents.Find(p => p.specId == "milkfoam").milliliters = foamMl;
            world.State.entities.Add(cup);
            string before = JsonUtility.ToJson(world.State);
            int notifications = 0;
            world.Changed += () => notifications++;

            var result = world.ClassifyCoffee(cup);

            Assert.That(result.known, Is.True);
            Assert.That(result.productId, Is.EqualTo("latte"));
            Assert.That(result.label, Is.EqualTo("拿铁"));
            Assert.That(result.amountScore, Is.LessThan(20));
            Assert.That(result.total, Is.LessThan(product.minimumScore));
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            Assert.That(notifications, Is.Zero);
        }

        [TestCase("coffee")]
        [TestCase("milk")]
        [TestCase("milkfoam")]
        public void ZeroRecordedRequiredLatteLiquidStillFailsEvenWithUnits(string ingredient)
        {
            // Without milk, essence + foam could legitimately identify as macchiato instead.
            foreach (var product in world.Catalog.products) product.evaluation.classify = product.id == "latte";
            var cup = RecommendedCup(world.Product("latte"));
            var portion = cup.contents.Find(p => p.specId == ingredient);
            portion.milliliters = 0;
            portion.units = 20;
            AssertUnknown(world.ClassifyCoffee(cup));
        }

        [TestCase("latte")]
        [TestCase("honeylatte")]
        [TestCase("macchiato")]
        public void NonEssenceCoffeeCannotSubstituteForRequiredEssence(string id)
        {
            var cup = RecommendedCup(world.Product(id));
            var coffee = cup.contents.Find(p => p.specId == "coffee");
            coffee.coffeeKind = "black";
            coffee.temperature = LiquidTemperature.Boiling;
            AssertUnknown(world.ClassifyCoffee(cup));
            coffee.temperature = LiquidTemperature.Room;
            AssertUnknown(world.ClassifyCoffee(cup));
        }

        [TestCase("espresso")]
        [TestCase("latte")]
        [TestCase("honeylatte")]
        [TestCase("matchalatte")]
        [TestCase("cocoa")]
        [TestCase("macchiato")]
        [TestCase("mocha")]
        [TestCase("blackcoffee")]
        [TestCase("creamcoffee")]
        [TestCase("cold_brew")]
        [TestCase("milkcoffee")]
        public void ExtraRawBeansInvalidateEveryKnownProduct(string id)
        {
            var cup = RecommendedCup(world.Product(id));
            cup.contents.Add(new Portion { specId = "beans", grams = .1f, productionScore = 100, coffeeKind = "essence" });
            AssertUnknown(world.ClassifyCoffee(cup));
        }

        [Test]
        public void MixedEssenceAndOrdinaryCoffeeCannotHideWrongKindInOtherwisePerfectLatte()
        {
            var cup = RecommendedCup(world.Product("latte"));
            cup.contents.Add(new Portion { specId = "coffee", milliliters = 1,
                coffeeKind = "black", temperature = LiquidTemperature.Warm, productionScore = 100 });
            AssertUnknown(world.ClassifyCoffee(cup));
        }

        [TestCase("milk", LiquidTemperature.Room, false)]
        [TestCase("milk", LiquidTemperature.Warm, true)]
        [TestCase("milk", LiquidTemperature.Boiling, true)]
        [TestCase("hotmilk", LiquidTemperature.Room, false)]
        [TestCase("hotmilk", LiquidTemperature.Warm, true)]
        [TestCase("hotmilk", LiquidTemperature.Boiling, true)]
        public void MilkCoffeeNeedsCurrentlyHotMilkRegardlessOfAlias(string alias, LiquidTemperature temperature, bool known)
        {
            var cup = RecommendedCup(world.Product("milkcoffee"));
            var milk = cup.contents.Find(p => p.specId == "milk");
            milk.specId = alias;
            milk.temperature = temperature;
            var result = world.ClassifyCoffee(cup);
            if (known)
            {
                Assert.That(result.known, Is.True);
                Assert.That(result.productId, Is.EqualTo("milkcoffee"));
            }
            else AssertUnknown(result);
        }

        [TestCase("latte", "macchiato", false)]
        [TestCase("espresso", "espresso", true)]
        public void HighScoreCannotSatisfyDeliveryWithWrongOrUnknownCategory(string actualId, string requestedId, bool addBeans)
        {
            world.EnableProduction();
            world.EnterProduction();
            Assert.That(world.StartDay().success, Is.True);
            var request = world.Request(world.State.focusedRequestId);
            request.productId = requestedId;
            var requested = world.Product(requestedId);
            int money = world.State.money;
            for (int delivery = 0; delivery <= requested.maxRemakes; delivery++)
            {
                Assert.That(world.SelectServingCup(world.Product(actualId).cupSpecId == "cup.small"
                    ? "s.cups.small" : "s.cups.large").success, Is.True);
                var cup = world.ServingCup;
                cup.contents.AddRange(RecommendedCup(world.Product(actualId)).contents);
                cup.latteArt = true;
                if (addBeans) cup.contents.Add(new Portion { specId = "beans", grams = .1f });
                var score = world.EvaluateProduction(cup, requested);
                Assert.That(score.total, Is.GreaterThanOrEqualTo(requested.minimumScore));
                Assert.That(world.ClassifyCoffee(cup).productId, Is.Not.EqualTo(requestedId));
                Assert.That(world.Matches(cup, requested), Is.False);
                Assert.That(world.DeliverProductionCup().success, Is.True, "Acceptance is not satisfaction.");
                Assert.That(request.lastProductionScore, Is.EqualTo(score.total).Within(.001f));
                Assert.That(world.State.todayServed, Is.Zero);
                Assert.That(world.State.todayRevenue, Is.Zero);
                Assert.That(world.State.production.lastDelivery, Does.Not.Contain("订单完成"));
                if (delivery < requested.maxRemakes)
                {
                    Assert.That(world.Request(request.id), Is.SameAs(request));
                    Assert.That(request.remakes, Is.EqualTo(delivery + 1));
                    Assert.That(world.State.money, Is.EqualTo(money));
                    Assert.That(world.State.production.lastDelivery, Does.Contain("重做"));
                }
            }
            Assert.That(world.Request(request.id), Is.Null);
            Assert.That(world.State.todayFailed, Is.EqualTo(1));
            Assert.That(world.State.money, Is.LessThan(money));
            Assert.That(world.State.production.lastDelivery, Does.Contain("不满意"));
        }

        [TestCase("minimumRatio", -.01f)]
        [TestCase("minimumRatio", 1.01f)]
        [TestCase("minimumRatio", float.NaN)]
        [TestCase("maximumRatio", -.01f)]
        [TestCase("maximumRatio", 1.01f)]
        [TestCase("maximumRatio", float.PositiveInfinity)]
        [TestCase("concentrationWeight", -.01f)]
        [TestCase("concentrationWeight", 1.01f)]
        [TestCase("concentrationWeight", float.NaN)]
        [TestCase("coffeeStrengthTolerance", -.01f)]
        [TestCase("coffeeStrengthTolerance", 1)]
        [TestCase("coffeeStrengthTolerance", float.NaN)]
        public void InvalidClassificationFractionsAndConcentrationConfigurationAreRejected(string field, float value)
        {
            var catalog = LoadCatalog();
            var evaluation = Array.Find(catalog.products, p => p.id == "espresso").evaluation;
            switch (field)
            {
                case "minimumRatio": evaluation.amounts[0].classificationMinimumRatio = value; break;
                case "maximumRatio": evaluation.amounts[0].classificationMaximumRatio = value; break;
                case "concentrationWeight": evaluation.concentrationWeight = value; break;
                case "coffeeStrengthTolerance": evaluation.coffeeStrengthTolerance = value; break;
            }
            Assert.Throws<ArgumentException>(() => new CafeWorld(catalog));
        }

        [Test]
        public void ReversedRatioAndGramEndpointsAreRejected()
        {
            var catalog = LoadCatalog();
            var coffee = Array.Find(catalog.products, p => p.id == "espresso").evaluation.amounts[0];
            coffee.classificationMinimumRatio = .8f;
            coffee.classificationMaximumRatio = .7f;
            Assert.Throws<ArgumentException>(() => new CafeWorld(catalog));
            catalog = LoadCatalog();
            var honey = Array.Find(catalog.products, p => p.id == "honeylatte").evaluation.amounts[3];
            honey.classificationMinimumGrams = 2;
            honey.classificationMaximumGrams = 1;
            Assert.Throws<ArgumentException>(() => new CafeWorld(catalog));
        }

        [TestCase(0, 0, true)]
        [TestCase(0, 1, true)]
        [TestCase(1, 1, true)]
        public void RatioZeroAndOneAreValidCompatibilityEndpointsAndDoNotRestrictPresence(float minimum, float maximum, bool known)
        {
            var catalog = LoadCatalog();
            var need = Array.Find(catalog.products, p => p.id == "espresso").evaluation.amounts[0];
            need.classificationMinimumRatio = minimum;
            need.classificationMaximumRatio = maximum;
            Assert.DoesNotThrow(() => world = new CafeWorld(catalog));
            var result = world.ClassifyCoffee(RecommendedCup(world.Product("espresso")));
            if (known) Assert.That(result.productId, Is.EqualTo("espresso"));
            else AssertUnknown(result);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void ConcentrationWeightEndpointsPreservePerfectRecommendedScore(float weight)
        {
            var catalog = LoadCatalog();
            Array.Find(catalog.products, p => p.id == "espresso").evaluation.concentrationWeight = weight;
            Assert.DoesNotThrow(() => world = new CafeWorld(catalog));
            var result = world.ClassifyCoffee(RecommendedCup(world.Product("espresso")));
            Assert.That(result.productId, Is.EqualTo("espresso"));
            Assert.That(result.total, Is.EqualTo(100).Within(.001f));
        }

        [Test]
        public void MissingOrDisabledEvaluationRowsDoNotFallBackToLegacyNamingRules()
        {
            var cup = RecommendedCup(world.Product("espresso"));
            world.Catalog.drinkNameRules = new[] { new DrinkNameRuleSpec { id = "legacy-name", label = "旧规则咖啡",
                coffeeKind = "essence", requiredIngredients = new[] { "coffee" }, allowedIngredients = new[] { "coffee" }, priority = 999 } };
            world.Product("espresso").evaluation.classify = false;
            AssertUnknown(world.ClassifyCoffee(cup));
            world.Product("espresso").evaluation = null;
            AssertUnknown(world.ClassifyCoffee(cup));
            Assert.That(world.CompositionName(cup), Is.EqualTo("迷之饮品"));
        }

        [Test]
        public void SnapshotIsReadOnlyIndependentOfOrderAndKeepsItsCapturedScores()
        {
            var cup = RecommendedCup(world.Product("latte"));
            world.State.entities.Add(cup);
            string before = JsonUtility.ToJson(world.State);
            int notifications = 0;
            world.Changed += () => notifications++;
            var captured = world.ClassifyCoffee(cup);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            Assert.That(notifications, Is.Zero);
            cup.contents.Reverse();
            Assert.That(world.ClassifyCoffee(cup).total, Is.EqualTo(captured.total));
            foreach (var portion in cup.contents) portion.milliliters *= .25f;
            Assert.That(world.ClassifyCoffee(cup).total, Is.LessThan(captured.total));
            Assert.That(captured.productId, Is.EqualTo("latte"));
            Assert.That(captured.total, Is.EqualTo(100).Within(.001f));
        }

        [Test]
        public void NullEmptyAndZeroAmountContentsHaveNoCategoryOrScores()
        {
            AssertUnknown(world.ClassifyCoffee(null), "空容器");
            var cup = new WorldEntity { specId = "cup.small" };
            AssertUnknown(world.ClassifyCoffee(cup), "空容器");
            cup.contents.Add(null);
            cup.contents.Add(new Portion { specId = "coffee", units = 100, coffeeKind = "essence" });
            AssertUnknown(world.ClassifyCoffee(cup), "空容器");
            cup.contents = null;
            AssertUnknown(world.ClassifyCoffee(cup), "空容器");
        }
    }
}
