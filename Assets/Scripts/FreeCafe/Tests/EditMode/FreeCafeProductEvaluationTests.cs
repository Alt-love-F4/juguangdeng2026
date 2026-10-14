using System;
using NUnit.Framework;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeProductEvaluationTests
    {
        private CafeWorld world;

        [SetUp] public void Setup() => world = CreateWorld();

        private static CafeWorld CreateWorld(IngredientRuleSpec[] rules = null)
        {
            string[] ids = { "coffee", "water", "hotwater", "milk", "hotmilk", "grounds", "beans", "sugar", "honey", "cream",
                "cocoa.drink", "cocoa.powder", "matcha.drink", "matcha.powder", "cup.small", "cup.large" };
            var entities = new EntitySpec[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                entities[i] = new EntitySpec { id = ids[i], capacity = ids[i].StartsWith("cup.") ? 10 : 0,
                    capabilities = ids[i].StartsWith("cup.") ? Capability.Container : Capability.Portion };
            return new CafeWorld(new CafeCatalog
            {
                entities = entities, operations = Array.Empty<OperationSpec>(), products = Array.Empty<ProductSpec>(),
                layout = Array.Empty<InitialEntity>(),
                areas = new[] { new AreaSpec { id = AreaId.Cafe }, new AreaSpec { id = AreaId.Counter },
                    new AreaSpec { id = AreaId.TopFloor }, new AreaSpec { id = AreaId.Basement } },
                ingredientRules = rules, compositionRules = Array.Empty<CompositionRuleSpec>(), drinkNameRules = Array.Empty<DrinkNameRuleSpec>()
            });
        }

        private static ProductAmountSpec Ml(string id, float ideal = 100, string kind = null, float minimum = 0) =>
            new ProductAmountSpec { ingredientId = id, idealMl = ideal, coffeeKind = kind, minimumStandard = minimum };
        private static ProductAmountSpec Grams(string id, float ideal) => new ProductAmountSpec { ingredientId = id, idealGrams = ideal };
        private static ProductSpec Product(params ProductAmountSpec[] amounts) => new ProductSpec
        {
            id = "handmade", cupSpecId = "cup.small", evaluation = new ProductEvaluationSpec { amounts = amounts }
        };
        private static Portion Liquid(string id, float ml = 100, string kind = null, float score = 100) => new Portion
        {
            specId = id, milliliters = ml, coffeeKind = kind, productionScore = score, temperature = LiquidTemperature.Boiling
        };
        private WorldEntity Cup(params Portion[] portions)
        {
            var cup = new WorldEntity { id = "test.cup", specId = "cup.small" };
            cup.contents.AddRange(portions); world.State.entities.Add(cup);
            return cup;
        }
        private static void Zero(CafeProductEvaluation score)
        {
            Assert.That(score.amountScore, Is.Zero); Assert.That(score.standardScore, Is.Zero);
            Assert.That(score.total, Is.Zero); Assert.That(score.description, Does.StartWith("糟糕"));
        }

        [Test] public void ExactCompositionIsPerfectWithoutLegacyRecipeOrProductionMode()
        {
            var product = Product(Ml("coffee", 100, "essence", 70), Ml("milk"), Grams("sugar", 5));
            var cup = Cup(Liquid("coffee", kind: "essence"), Liquid("hotmilk"), new Portion { specId = "sugar", grams = 5 });
            var score = world.EvaluateProductComposition(cup, product);
            Assert.That(world.ProductionEnabled, Is.False); Assert.That(product.ingredients, Is.Null);
            Assert.That(score.amountScore, Is.EqualTo(100)); Assert.That(score.standardScore, Is.EqualTo(100));
            Assert.That(score.total, Is.EqualTo(100)); Assert.That(score.description, Does.StartWith("精品"));
        }

        [TestCase(80, 100)] [TestCase(100, 100)] [TestCase(120, 100)]
        [TestCase(40, 50)] [TestCase(160, 50)] [TestCase(0, 0)] [TestCase(200, 0)] [TestCase(300, 0)]
        public void AmountToleranceIsContinuousAndPenalizesShortageAndExcess(float actual, float expected)
        {
            var product = Product(Ml("milk"));
            var score = world.EvaluateProductComposition(Cup(Liquid("milk", actual)), product);
            Assert.That(score.amountScore, Is.EqualTo(expected).Within(.001f));
            if (actual == 0) Assert.That(score.standardScore, Is.Zero);
        }

        [Test] public void ZeroToleranceUsesExactIdealAndLinearDeterioration()
        {
            var product = Product(Ml("milk")); product.evaluation.amounts[0].tolerance = 0;
            Assert.That(world.EvaluateProductComposition(Cup(Liquid("milk", 75)), product).amountScore, Is.EqualTo(75));
            Assert.That(world.EvaluateProductComposition(Cup(Liquid("milk", 125)), product).amountScore, Is.EqualTo(75));
        }

        [Test] public void RequiredIngredientsHaveEqualImportanceRegardlessOfIdealSize()
        {
            var product = Product(Ml("milk", 500), Grams("sugar", 5));
            var score = world.EvaluateProductComposition(Cup(Liquid("milk", 500)), product);
            Assert.That(score.amountScore, Is.EqualTo(50)); Assert.That(score.standardScore, Is.EqualTo(50));
            Assert.That(score.total, Is.EqualTo(50));
        }

        [TestCase("cold_brew")] [TestCase("black")] [TestCase(null)]
        public void MissingOrWrongCoffeeKindDoesNotQualifyForEssence(string kind)
        {
            var product = Product(Ml("coffee", kind: "essence"));
            Zero(world.EvaluateProductComposition(Cup(Liquid("coffee", kind: kind)), product));
            Zero(world.EvaluateProductComposition(Cup(new Portion { specId = "beans", units = 1, grams = 12 }), product));
        }

        [Test] public void WrongKindCannotDiluteOrBoostMatchingAmountOrQuality()
        {
            var cup = Cup(Liquid("coffee", 100, "essence", 70), Liquid("coffee", 400, "black", 100));
            var score = world.EvaluateProductComposition(cup, Product(Ml("coffee", kind: "essence", minimum: 70)));
            Assert.That(score.amountScore, Is.EqualTo(100)); Assert.That(score.standardScore, Is.EqualTo(70));
            Assert.That(score.total, Is.EqualTo(89.5f).Within(.001f));
        }

        [TestCase(LiquidTemperature.Room, false, "cold_brew")]
        [TestCase(LiquidTemperature.Warm, false, "black")]
        [TestCase(LiquidTemperature.Boiling, false, "black")]
        [TestCase(LiquidTemperature.Room, true, "cold_brew")]
        public void LegacyCoffeeKindsAreInferredBySharedDomainApi(LiquidTemperature temperature, bool extracted, string kind)
        {
            var portion = Liquid("coffee"); portion.temperature = temperature; portion.extracted = extracted;
            Assert.That(world.CoffeeKind(portion), Is.EqualTo(kind));
            Assert.That(world.EvaluateProductComposition(Cup(portion), Product(Ml("coffee", kind: kind))).total, Is.EqualTo(100));
        }

        [Test] public void NonCoffeeNeverBecomesCoffeeByMetadataOrRawBeans()
        {
            Assert.That(world.CoffeeKind(new Portion { specId = "beans", coffeeKind = "essence" }), Is.Null);
            var product = Product(Ml("coffee", kind: "essence"));
            Zero(world.EvaluateProductComposition(Cup(Liquid("beans", kind: "essence")), product));
        }

        [Test] public void CoffeeStandardIsVolumeWeightedAndIndependentOfFragments()
        {
            var product = Product(Ml("coffee", kind: "essence", minimum: 70));
            var cup = Cup(Liquid("coffee", 75, "essence", 100), Liquid("coffee", 25, "essence", 30));
            var score = world.EvaluateProductComposition(cup, product);
            Assert.That(score.standardScore, Is.EqualTo(82.5f));
            cup.contents.RemoveAt(1);
            for (int i = 0; i < 5; i++) cup.contents.Add(Liquid("coffee", 5, "essence", 30));
            Assert.That(world.EvaluateProductComposition(cup, product).total, Is.EqualTo(score.total));
        }

        [TestCase(100, 100)] [TestCase(70, 70)] [TestCase(35, 17.5f)] [TestCase(0, 0)]
        public void StandardKeepsActualQualityAndAddsRelativePenaltyBelowThreshold(float quality, float expected)
        {
            var score = world.EvaluateProductComposition(Cup(Liquid("coffee", kind: "essence", score: quality)),
                Product(Ml("coffee", kind: "essence", minimum: 70)));
            Assert.That(score.standardScore, Is.EqualTo(expected).Within(.001f));
        }

        [Test] public void ZeroMinimumDoesNotPromoteBadCoffeeToPerfect()
        {
            var score = world.EvaluateProductComposition(Cup(Liquid("coffee", kind: "essence", score: 30)),
                Product(Ml("coffee", kind: "essence")));
            Assert.That(score.standardScore, Is.EqualTo(30));
        }

        [Test] public void NonCoffeeStandardIsPerfectWhenPresentWithoutProcessingScore()
        {
            var score = world.EvaluateProductComposition(Cup(Liquid("milk", score: 0)), Product(Ml("milk", minimum: 100)));
            Assert.That(score.standardScore, Is.EqualTo(100)); Assert.That(score.total, Is.EqualTo(100));
        }

        [TestCase("water", "hotwater")] [TestCase("hotwater", "water")]
        [TestCase("milk", "hotmilk")] [TestCase("hotmilk", "milk")]
        public void HotAliasesMatchInBothDirections(string expected, string actual)
        {
            Assert.That(world.EvaluateProductComposition(Cup(Liquid(actual)), Product(Ml(expected))).total, Is.EqualTo(100));
        }

        [Test] public void RecordedAmountsWinOverUnitsAndConfiguredFallbacks()
        {
            world = CreateWorld(new[] { new IngredientRuleSpec { id = "milk", label = "Milk", liquid = true, millilitersPerUnit = 125 },
                new IngredientRuleSpec { id = "honey", label = "Honey", gramsPerUnit = 8 } });
            var cup = Cup(new Portion { specId = "milk", units = 7, milliliters = 100 }, new Portion { specId = "honey", units = 9, grams = 10 });
            Assert.That(world.EvaluateProductComposition(cup, Product(Ml("milk"), Grams("honey", 10))).total, Is.EqualTo(100));
            cup.contents[0].milliliters = 0; cup.contents[0].units = 2;
            cup.contents[1].grams = 0; cup.contents[1].units = 3;
            Assert.That(world.EvaluateProductComposition(cup, Product(Ml("milk", 250), Grams("honey", 24))).total, Is.EqualTo(100));
        }

        [Test] public void LegacyFallbackUsesConfiguredPortionMlTwelveGramsAndFiveForSugar()
        {
            world.Catalog.production.portionMl = 150;
            var cup = Cup(new Portion { specId = "milk", units = 2 }, new Portion { specId = "honey", units = 2 },
                new Portion { specId = "sugar", units = 2 });
            Assert.That(world.EvaluateProductComposition(cup, Product(Ml("milk", 300), Grams("honey", 24), Grams("sugar", 10))).total, Is.EqualTo(100));
        }

        [Test] public void ConfigurableWeightsAreNormalizedAndCanDisableADimension()
        {
            var product = Product(Ml("coffee", kind: "essence"));
            var cup = Cup(Liquid("coffee", kind: "essence", score: 30));
            product.evaluation.amountWeight = 2; product.evaluation.standardWeight = 1;
            Assert.That(world.EvaluateProductComposition(cup, product).total, Is.EqualTo(230f / 3).Within(.001f));
            product.evaluation.amountWeight = 0;
            Assert.That(world.EvaluateProductComposition(cup, product).total, Is.EqualTo(30));
            product.evaluation.amountWeight = 1; product.evaluation.standardWeight = 0;
            Assert.That(world.EvaluateProductComposition(cup, product).total, Is.EqualTo(100));
        }

        [Test] public void UnexpectedBeansArePenalizedOncePerIngredientNotPerFragment()
        {
            var product = Product(Ml("coffee", kind: "essence"));
            var cup = Cup(Liquid("coffee", kind: "essence"), new Portion { specId = "beans", grams = 12 });
            var score = world.EvaluateProductComposition(cup, product);
            Assert.That(score.total, Is.EqualTo(85).Within(.001f));
            cup.contents[1].grams = 4;
            cup.contents.Add(new Portion { specId = "beans", grams = 4 }); cup.contents.Add(new Portion { specId = "beans", grams = 4 });
            Assert.That(world.EvaluateProductComposition(cup, product).total, Is.EqualTo(score.total));
            cup.contents.Add(new Portion { specId = "sugar", grams = 5 });
            Assert.That(world.EvaluateProductComposition(cup, product).total, Is.EqualTo(70).Within(.001f));
        }

        [Test] public void ExtraHotAliasesCountAsOneIngredientAndPenaltyCannotGoNegative()
        {
            var product = Product(Ml("coffee", kind: "essence"));
            var cup = Cup(Liquid("coffee", kind: "essence"), Liquid("water", 50), Liquid("hotwater", 50));
            Assert.That(world.EvaluateProductComposition(cup, product).total, Is.EqualTo(85).Within(.001f));
            product.evaluation.extraIngredientPenalty = .8f; cup.contents.Add(Liquid("milk"));
            Assert.That(world.EvaluateProductComposition(cup, product).total, Is.Zero);
        }

        [TestCase("coffee", "grounds", "essence")]
        [TestCase("cocoa.drink", "cocoa.powder", null)] [TestCase("matcha.drink", "matcha.powder", null)]
        public void RequestedDrinkPermitsCatalystResidueButOtherDrinksDoNot(string drink, string residue, string kind)
        {
            var cup = Cup(Liquid(drink, kind: kind), new Portion { specId = residue, grams = 12 });
            Assert.That(world.EvaluateProductComposition(cup, Product(Ml(drink, kind: kind))).total, Is.EqualTo(100));
            cup.contents[0] = Liquid("milk");
            Assert.That(world.EvaluateProductComposition(cup, Product(Ml("milk"))).total, Is.EqualTo(85).Within(.001f));
        }

        [Test] public void ExplicitGroundsAmountStillScoresResidueQuantity()
        {
            var cup = Cup(Liquid("coffee", kind: "essence"), new Portion { specId = "grounds", grams = 24 });
            var score = world.EvaluateProductComposition(cup, Product(Ml("coffee", kind: "essence"), Grams("grounds", 12)));
            Assert.That(score.amountScore, Is.EqualTo(50)); Assert.That(score.standardScore, Is.EqualTo(100));
            Assert.That(score.total, Is.EqualTo(67.5f).Within(.001f));
        }

        [Test] public void CupRestrictionIsOptInAndDoesNotChangeCompositionSubscores()
        {
            var product = Product(Ml("milk")); var cup = Cup(Liquid("milk")); cup.specId = "cup.large";
            Assert.That(world.EvaluateProductComposition(cup, product).total, Is.EqualTo(100));
            product.evaluation.enforceCup = true;
            var score = world.EvaluateProductComposition(cup, product);
            Assert.That(score.total, Is.Zero); Assert.That(score.amountScore, Is.EqualTo(100)); Assert.That(score.standardScore, Is.EqualTo(100));
        }

        [TestCase(100, "精品")] [TestCase(90, "精品")] [TestCase(70, "一般")] [TestCase(60, "一般")] [TestCase(30, "糟糕")]
        public void DescriptionUsesHumanReadableGradeAndSubscores(float quality, string grade)
        {
            var product = Product(Ml("coffee", kind: "essence")); product.evaluation.amountWeight = 0; product.evaluation.standardWeight = 1;
            var score = world.EvaluateProductComposition(Cup(Liquid("coffee", kind: "essence", score: quality)), product);
            Assert.That(score.description, Does.StartWith(grade)); Assert.That(score.description, Does.Contain("含量"));
            Assert.That(score.description, Does.Contain("水准"));
        }

        [Test] public void EvaluationIsIndependentOfSequenceAndDoesNotMutateIngredientsOrWorld()
        {
            var product = Product(Ml("coffee", kind: "essence"), Ml("milk"));
            var coffee = Liquid("coffee", kind: "essence", score: 70); coffee.addedSequence = 99;
            var milk = Liquid("hotmilk"); milk.addedSequence = 1;
            var cup = Cup(coffee, milk); int count = world.State.entities.Count, nextId = world.State.nextId;
            int notifications = 0; world.Changed += () => notifications++;
            var score = world.EvaluateProductComposition(cup, product);
            Assert.That(cup.contents[0], Is.SameAs(coffee)); cup.contents.Reverse();
            Assert.That(world.EvaluateProductComposition(cup, product).total, Is.EqualTo(score.total));
            Assert.That(coffee.milliliters, Is.EqualTo(100)); Assert.That(coffee.productionScore, Is.EqualTo(70));
            Assert.That(coffee.addedSequence, Is.EqualTo(99)); Assert.That(milk.specId, Is.EqualTo("hotmilk"));
            Assert.That(world.State.entities.Count, Is.EqualTo(count)); Assert.That(world.State.nextId, Is.EqualTo(nextId));
            Assert.That(notifications, Is.Zero);
        }

        [Test] public void NullEmptyOrMissingConfigurationReturnsSafeZero()
        {
            var cup = Cup(Liquid("milk")); var product = Product(Ml("milk"));
            Zero(world.EvaluateProductComposition(null, product)); Zero(world.EvaluateProductComposition(cup, null));
            Zero(world.EvaluateProductComposition(Cup(), product));
            product.evaluation = null; Zero(world.EvaluateProductComposition(cup, product));
            product.evaluation = new ProductEvaluationSpec(); Zero(world.EvaluateProductComposition(cup, product));
            product.evaluation.amounts = Array.Empty<ProductAmountSpec>(); Zero(world.EvaluateProductComposition(cup, product));
            product.evaluation.amounts = new ProductAmountSpec[] { null }; Zero(world.EvaluateProductComposition(cup, product));
            cup.contents = null; Zero(world.EvaluateProductComposition(cup, Product(Ml("milk"))));
        }

        [Test] public void RuntimeMalformedParametersDoNotLeakNaNOrDivideByZero()
        {
            var cup = Cup(Liquid("milk"));
            Action<ProductEvaluationSpec>[] corrupt = {
                e => e.amountWeight = float.NaN, e => e.standardWeight = float.PositiveInfinity,
                e => e.amountWeight = -1, e => { e.amountWeight = 0; e.standardWeight = 0; },
                e => e.extraIngredientPenalty = -1, e => e.extraIngredientPenalty = 2,
                e => e.amounts[0].idealMl = 0, e => e.amounts[0].idealMl = float.NaN,
                e => e.amounts[0].idealGrams = 5, e => e.amounts[0].tolerance = 1,
                e => e.amounts[0].tolerance = -.1f, e => e.amounts[0].minimumStandard = 101,
                e => e.amounts[0].minimumStandard = float.NaN, e => e.amounts[0].ingredientId = null
            };
            foreach (var mutate in corrupt)
            {
                var product = Product(Ml("milk")); mutate(product.evaluation);
                Zero(world.EvaluateProductComposition(cup, product));
            }
        }

        [Test] public void DuplicateAliasesAndInvalidCoffeeSelectorsAreGuardedAtRuntime()
        {
            var cup = Cup(Liquid("milk"));
            Zero(world.EvaluateProductComposition(cup, Product(Ml("milk"), Ml("hotmilk"))));
            Zero(world.EvaluateProductComposition(cup, Product(Ml("milk", kind: "essence"))));
            Zero(world.EvaluateProductComposition(cup, Product(Ml("coffee", kind: "unknown"))));
        }

        [Test] public void SeparateCoffeeKindsCanHaveIndependentAmountRequirements()
        {
            var cup = Cup(Liquid("coffee", 100, "black", 100), Liquid("coffee", 50, "essence", 70));
            var score = world.EvaluateProductComposition(cup, Product(Ml("coffee", 100, "black"), Ml("coffee", 50, "essence")));
            Assert.That(score.amountScore, Is.EqualTo(100)); Assert.That(score.standardScore, Is.EqualTo(85));
        }

        [Test] public void HotAliasFallbackUsesCanonicalIngredientDefinition()
        {
            world = CreateWorld(new[] { new IngredientRuleSpec { id = "milk", label = "Milk", liquid = true, millilitersPerUnit = 125 } });
            var cup = Cup(new Portion { specId = "hotmilk", units = 2 });
            Assert.That(world.EvaluateProductComposition(cup, Product(Ml("milk", 250))).total, Is.EqualTo(100));
        }

        [Test] public void InvalidRecordedAmountsNeverUseUnitsAndInvalidCoffeeQualityScoresZero()
        {
            var milk = Liquid("milk", float.NaN); milk.units = 1;
            var cup = Cup(null, milk);
            Zero(world.EvaluateProductComposition(cup, Product(Ml("milk"))));
            cup.contents.Clear(); cup.contents.Add(Liquid("coffee", kind: "essence", score: float.NaN));
            var score = world.EvaluateProductComposition(cup, Product(Ml("coffee", kind: "essence")));
            Assert.That(score.amountScore, Is.EqualTo(100)); Assert.That(score.standardScore, Is.Zero);
            Assert.That(float.IsNaN(score.total), Is.False);
        }
    }
}
