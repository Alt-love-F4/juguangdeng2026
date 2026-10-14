using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeExtractionContainerTests
    {
        private const string ExtractorId = "v.extractor";
        private CafeWorld world;
        private WorldEntity Extractor => world.Find(ExtractorId);

        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction();
            world.EnterProduction();
            Assert.That(world.SelectServingCup("s.cups.large").success, Is.True);
            Assert.That(Extractor, Is.Not.Null, "The catalog must provide the v.extractor layout entity.");
        }

        private WorldEntity Ingredient(string specId)
        {
            var item = new WorldEntity { id = "test." + world.State.nextId++, specId = specId,
                units = 1, area = AreaId.Counter };
            world.State.entities.Add(item);
            return item;
        }

        private void Add(string specId, string targetId)
        {
            Assert.That(world.Interact(Ingredient(specId).id, targetId).success, Is.True, specId + " -> " + targetId);
        }

        private void PrepareHotWater(LiquidTemperature temperature)
        {
            Add("hotwater", "m.heater");
            var source = world.Find("m.heater");
            if (temperature == LiquidTemperature.Warm)
            {
                foreach (var portion in source.contents) world.SetTemperature(portion, 60);
                source.production.phase = ProductionPhase.Warm;
                source.production.remaining = 0;
            }
            Assert.That(world.LiquidSnapshot(source).temperature, Is.EqualTo(temperature));
            Assert.That(world.LiquidSnapshot(source).temperatureCelsius,
                Is.EqualTo(temperature == LiquidTemperature.Warm ? 60 : 100).Within(.001f));
        }

        private float TotalSolidMass(WorldEntity container)
        {
            float total = world.SolidMass(container);
            if (container != null) foreach (var portion in container.contents)
                if (portion.composition != null)
                    foreach (var constituent in portion.composition) total += constituent.grams;
            return total;
        }

        private void AssertCoffee(WorldEntity container, float volume, float totalGrams, bool essence)
        {
            var filter = world.ExtractionFilter(container);
            if (filter != null && world.LiquidVolume(filter) > 0)
                world.Tick(world.Catalog.production.filterResidenceSeconds + world.LiquidVolume(filter) / world.Catalog.production.filterDrainMlPerSecond);
            var liquid = world.LiquidSnapshot(container);
            Assert.That(liquid.composition.coffeeMl, Is.EqualTo(volume).Within(.001f));
            Assert.That(liquid.composition.waterMl, Is.Zero);
            Assert.That(container.contents.Exists(p => p.specId == "water" || p.specId == "hotwater"), Is.False);
            var coffee = container.contents.Find(p => p.specId == "coffee");
            Assert.That(coffee, Is.Not.Null);
            Assert.That(world.CoffeeKind(coffee) == "essence", Is.EqualTo(essence));
            if (!essence) Assert.That(container.production.brewCompleted, Is.False);
            Assert.That(TotalSolidMass(container) + (filter != null ? TotalSolidMass(filter) : 0), Is.EqualTo(totalGrams).Within(.001f));
        }

        [Test] public void CatalogProvidesMovableFourHundredMlHotExtractionContainer()
        {
            var spec = world.Spec(Extractor.specId);
            Assert.That(spec.id, Is.EqualTo("container.extractor"));
            Assert.That(spec.Has(Capability.Container | Capability.Movable), Is.True);
            Assert.That(System.Enum.GetName(typeof(Capability), (Capability)512), Is.EqualTo("HotExtraction"));
            Assert.That(spec.Has((Capability)512), Is.True);
            Assert.That(world.LiquidCapacity(Extractor), Is.EqualTo(400));
            Assert.That(Extractor.area, Is.EqualTo(AreaId.Counter));
            Assert.That(world.ServingCup.id, Is.Not.EqualTo(ExtractorId));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DirectBoilingWaterStartsOnlyAfterGroundsAndDoesNotCompleteImmediately(bool groundsFirst)
        {
            if (groundsFirst) Add("grounds", ExtractorId);
            Add("hotwater", ExtractorId);
            if (!groundsFirst) Add("grounds", ExtractorId);
            AssertCoffee(Extractor, 200, 12, false);
            Assert.That(world.ExtractionSnapshot(Extractor).inProgress, Is.EqualTo(groundsFirst));
            Assert.That(world.LiquidSnapshot(Extractor).temperature, Is.EqualTo(LiquidTemperature.Boiling));
            Assert.That(Extractor.production.phase, Is.Not.EqualTo(ProductionPhase.Processing));
            Assert.That(world.CanPour(Extractor), Is.EqualTo(!groundsFirst));
        }

        [TestCase(LiquidTemperature.Boiling, true)]
        [TestCase(LiquidTemperature.Boiling, false)]
        [TestCase(LiquidTemperature.Warm, true)]
        [TestCase(LiquidTemperature.Warm, false)]
        public void WaterPouredFromAnotherVesselStartsOnlyAfterGrounds(LiquidTemperature temperature, bool groundsFirst)
        {
            PrepareHotWater(temperature);
            if (groundsFirst) Add("grounds", ExtractorId);
            Assert.That(world.PourLiquid("m.heater", ExtractorId, 200, 1).success, Is.True);
            if (!groundsFirst) Add("grounds", ExtractorId);
            AssertCoffee(Extractor, 200, 12, false);
            Assert.That(world.ExtractionSnapshot(Extractor).inProgress, Is.EqualTo(groundsFirst));
            Assert.That(world.LiquidSnapshot(Extractor).temperature, Is.EqualTo(temperature));
            // Free filter grounds share heat with incoming water before becoming internal provenance.
            float expectedCelsius = (200 * (temperature == LiquidTemperature.Warm ? 60 : 100) + 12 * 20) / 212f;
            Assert.That(world.LiquidSnapshot(Extractor).temperatureCelsius, Is.EqualTo(expectedCelsius).Within(.001f));
            Assert.That(world.LiquidVolume(world.Find("m.heater")), Is.Zero);
        }

        [Test] public void StoveHeatedWaterFromAnotherKettleAlsoExtractsOnPour()
        {
            Add("water", "m.heater");
            Assert.That(world.Interact("m.heater", "m.stove").success, Is.True);
            Assert.That(world.OperateProduction("m.stove").success, Is.True);
            world.Tick(world.Catalog.production.heatSeconds);
            Add("grounds", ExtractorId);
            Assert.That(world.PourLiquid("m.heater", ExtractorId, 100, 1).success, Is.True);
            AssertCoffee(Extractor, 100, 12, false);
            Assert.That(world.ExtractionSnapshot(Extractor).inProgress, Is.True);
            Assert.That(world.LiquidVolume(world.Find("m.heater")), Is.EqualTo(100));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RoomWaterAndGroundsStillBecomeColdBrew(bool groundsFirst)
        {
            if (groundsFirst) Add("grounds", ExtractorId);
            Add("water", ExtractorId);
            if (!groundsFirst) Add("grounds", ExtractorId);
            AssertCoffee(Extractor, 200, 12, false);
            Assert.That(world.LiquidSnapshot(Extractor).temperature, Is.EqualTo(LiquidTemperature.Room));
        }

        [Test] public void AddingGroundsToExistingColdCoffeeDoesNotMakeEssence()
        {
            Add("coffee", ExtractorId);
            Add("grounds", ExtractorId);
            AssertCoffee(Extractor, 200, 24, false);
        }

        [Test] public void AddingGroundsToColdCoffeeMixedWithHotMilkDoesNotMakeEssence()
        {
            Add("coffee", ExtractorId);
            Add("hotmilk", ExtractorId);
            Add("grounds", ExtractorId);
            AssertCoffee(Extractor, 200, 24, false);
            Assert.That(world.LiquidSnapshot(Extractor).composition.milkMl, Is.EqualTo(200));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OrdinaryCupMakesBlackCoffeeWithoutCompletingPrecisionExtraction(bool groundsFirst)
        {
            string cupId = world.ServingCup.id;
            if (groundsFirst) Add("grounds", cupId);
            Add("hotwater", cupId);
            if (!groundsFirst) Add("grounds", cupId);
            AssertCoffee(world.ServingCup, 200, 12, false);
            Assert.That(world.CoffeeKind(world.ServingCup.contents.Find(p => p.specId == "coffee")), Is.EqualTo("black"));
        }

        [TestCase("beans")]
        [TestCase("grounds")]
        [TestCase("cocoa.powder")]
        [TestCase("matcha.powder")]
        [TestCase("milk")]
        [TestCase("sugar")]
        public void ExtractionContainerStillAcceptsOrdinaryRawIngredients(string specId)
        {
            Add(specId, ExtractorId);
            var receiver = specId == "grounds" || specId == "milk" ? world.ExtractionFilter(Extractor) : Extractor;
            Assert.That(receiver.contents.Exists(p => p.specId == specId), Is.True);
        }

        [TestCase("cocoa.powder", "cocoa.drink")]
        [TestCase("matcha.powder", "matcha.drink")]
        public void NonCoffeePowdersStillUseGenericMixingRules(string powderId, string drinkId)
        {
            Add(powderId, ExtractorId);
            Add("water", ExtractorId);
            world.Tick(2);
            Assert.That(Extractor.contents.Exists(p => p.specId == drinkId && p.milliliters == 200), Is.True);
            Assert.That(TotalSolidMass(Extractor), Is.EqualTo(12));
            Assert.That(Extractor.contents.Exists(p => p.specId == powderId), Is.False);
            Assert.That(Extractor.contents.Exists(p => p.extracted), Is.False);
        }

        [Test] public void MilkAndSugarKeepExtractedCoffeeAndAllIngredientAmounts()
        {
            Add("grounds", ExtractorId);
            Add("hotwater", ExtractorId);
            world.Tick(7.5f); world.FinishPour(ExtractorId);
            Add("milk", ExtractorId);
            Add("sugar", ExtractorId);
            AssertCoffee(Extractor, 200, 17, true);
            Assert.That(world.LiquidSnapshot(Extractor).composition.milkMl, Is.EqualTo(200));
            Assert.That(world.LiquidVolume(Extractor), Is.EqualTo(400));
            Assert.That(Extractor.contents.Exists(p => p.specId == "grounds"), Is.False);
            Assert.That(world.SolidMass(world.Find(ExtractorId + ".filter")), Is.Zero);
            Assert.That(Extractor.contents.Find(p => p.specId == "sugar").grams, Is.EqualTo(5));
        }

        [Test] public void PourTransfersExtractedCoffeeMilkAndSugarWithoutTransferringFilteredGrounds()
        {
            Add("grounds", ExtractorId);
            Add("hotwater", ExtractorId);
            world.Tick(7.5f); world.FinishPour(ExtractorId);
            Add("milk", ExtractorId);
            Add("sugar", ExtractorId);
            var cup = world.ServingCup;
            Assert.That(world.PourLiquid(ExtractorId, cup.id, 100, 1).success, Is.True);
            Assert.That(world.LiquidVolume(cup), Is.EqualTo(100));
            Assert.That(world.LiquidVolume(Extractor), Is.EqualTo(300));
            Assert.That(world.LiquidSnapshot(cup).composition.coffeeMl, Is.EqualTo(50));
            Assert.That(world.LiquidSnapshot(cup).composition.milkMl, Is.EqualTo(50));
            Assert.That(world.CoffeeKind(cup.contents.Find(p => p.specId == "coffee")), Is.EqualTo("essence"));
            Assert.That(cup.contents.Exists(p => p.specId == "grounds"), Is.False);
            Assert.That(cup.contents.Find(p => p.specId == "sugar").grams, Is.EqualTo(1.25f));
            Assert.That(world.SolidMass(Extractor), Is.EqualTo(3.75f));
            Assert.That(world.LiquidVolume(cup) + world.LiquidVolume(Extractor), Is.EqualTo(400));
            Assert.That(TotalSolidMass(cup) + TotalSolidMass(Extractor), Is.EqualTo(17));
            Assert.That(world.CoffeeGroundsGrams(cup.contents.Find(p => p.specId == "coffee")), Is.EqualTo(3));
            Assert.That(world.CoffeeGroundsGrams(Extractor.contents.Find(p => p.specId == "coffee")), Is.EqualTo(9));
            Assert.That(world.SolidMass(world.Find(ExtractorId + ".filter")), Is.Zero);
            Assert.That(world.PourLiquid(ExtractorId, cup.id, 300, 1).success, Is.True);
            Assert.That(world.LiquidVolume(Extractor), Is.Zero);
            Assert.That(world.SolidMass(Extractor), Is.Zero);
            Assert.That(Extractor.contents, Is.Empty);
            AssertCoffee(cup, 200, 17, true);
            Assert.That(world.LiquidVolume(cup), Is.EqualTo(400));
            Assert.That(Extractor.production.spilledMl, Is.Zero);
        }

        [Test] public void IngredientOverCapacityIsRejectedWithoutChangingEitherEntity()
        {
            Add("grounds", ExtractorId);
            Add("hotwater", ExtractorId);
            world.Tick(7.5f); world.FinishPour(ExtractorId);
            Add("milk", ExtractorId);
            var extra = Ingredient("water");
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.Interact(extra.id, ExtractorId).success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            Assert.That(world.Find(extra.id), Is.SameAs(extra));
            Assert.That(world.LiquidVolume(Extractor), Is.EqualTo(400));
            AssertCoffee(Extractor, 200, 12, true);
        }

        [Test] public void RestoreRoundTripPreservesExtractionContainerCompositionAndAllowsFurtherPouring()
        {
            Add("grounds", ExtractorId);
            Add("hotwater", ExtractorId);
            world.Tick(7.5f); world.FinishPour(ExtractorId);
            Add("milk", ExtractorId);
            Add("sugar", ExtractorId);
            Assert.That(world.PourLiquid(ExtractorId, world.ServingCup.id, 100, 1).success, Is.True);
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            var restored = new CafeWorld(world.Catalog);
            Assert.That(restored.Restore(snapshot).success, Is.True);
            var extractor = restored.Find(ExtractorId);
            Assert.That(restored.ProductionEnabled, Is.True);
            Assert.That(extractor.specId, Is.EqualTo("container.extractor"));
            Assert.That(restored.LiquidCapacity(extractor), Is.EqualTo(400));
            Assert.That(restored.LiquidSnapshot(extractor).composition.coffeeMl, Is.EqualTo(150));
            Assert.That(restored.LiquidSnapshot(extractor).composition.milkMl, Is.EqualTo(150));
            Assert.That(restored.CoffeeKind(extractor.contents.Find(p => p.specId == "coffee")), Is.EqualTo("essence"));
            Assert.That(extractor.production.brewCompleted, Is.True);
            Assert.That(extractor.contents.Exists(p => p.specId == "grounds"), Is.False);
            Assert.That(restored.SolidMass(restored.Find(ExtractorId + ".filter")), Is.Zero);
            Assert.That(extractor.contents.Find(p => p.specId == "sugar").grams, Is.EqualTo(3.75f));
            Assert.That(restored.LiquidSnapshot(extractor).temperature, Is.EqualTo(world.LiquidSnapshot(Extractor).temperature));
            Assert.That(restored.LiquidSnapshot(extractor).temperatureCelsius,
                Is.EqualTo(world.LiquidSnapshot(Extractor).temperatureCelsius).Within(.001f));
            Assert.That(restored.CanPour(extractor), Is.True);
            Assert.That(restored.PourLiquid(ExtractorId, restored.ServingCup.id, 300, 1).success, Is.True);
            Assert.That(restored.LiquidVolume(restored.ServingCup), Is.EqualTo(400));
            Assert.That(restored.SolidMass(restored.ServingCup), Is.EqualTo(5));
            Assert.That(restored.CoffeeGroundsGrams(restored.ServingCup.contents.Find(p => p.specId == "coffee")), Is.EqualTo(12));
            Assert.That(restored.CoffeeKind(restored.ServingCup.contents.Find(p => p.specId == "coffee")), Is.EqualTo("essence"));
            Assert.That(extractor.contents, Is.Empty);
            Assert.That(restored.Restore(JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(restored.State))).success, Is.True);
            Assert.That(restored.Find(ExtractorId).contents, Is.Empty);
        }
    }
}
