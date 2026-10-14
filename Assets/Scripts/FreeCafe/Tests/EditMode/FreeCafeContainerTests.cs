using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeContainerTests
    {
        private CafeWorld world;
        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction(); world.EnterProduction(); world.SelectServingCup("s.cups.large");
        }
        private void Add(string ingredient, string target)
        {
            var item = new WorldEntity { id = "test." + world.State.nextId++, specId = ingredient, units = 1, area = AreaId.Counter };
            world.State.entities.Add(item);
            Assert.That(world.Interact(item.id, target).success, Is.True);
        }
        private float TotalSolidMass(WorldEntity container)
        {
            float total = world.SolidMass(container);
            foreach (var portion in container.contents)
                if (portion.composition != null)
                    foreach (var constituent in portion.composition) total += constituent.grams;
            return total;
        }
        [TestCase("beans")]
        [TestCase("grounds")]
        [TestCase("cocoa.powder")]
        [TestCase("matcha.powder")]
        [TestCase("milk")]
        [TestCase("sugar")]
        public void EveryContainerAcceptsRawIngredients(string ingredient)
        {
            foreach (string target in new[] { world.ServingCup.id, "m.heater", "m.brewer" })
            {
                Add(ingredient, target);
                Assert.That(world.Find(target).contents.Exists(portion => portion.specId == ingredient), Is.True);
            }
        }
        [Test] public void GroundsAndWaterBecomeColdCoffeeWithoutClickOrTimingGate()
        {
            Add("grounds", world.ServingCup.id); Add("water", world.ServingCup.id);
            Assert.That(world.ContainerDescription(world.ServingCup), Is.EqualTo("冷萃咖啡"));
            Assert.That(world.LiquidSnapshot(world.ServingCup).composition.coffeeMl, Is.EqualTo(200));
            Assert.That(TotalSolidMass(world.ServingCup), Is.EqualTo(12));
            Assert.That(world.SolidMass(world.ServingCup), Is.Zero);
            Assert.That(world.ServingCup.contents.Exists(p => p.specId == "grounds"), Is.False);
            Assert.That(world.CanPour(world.ServingCup), Is.True);
        }
        [Test] public void WaterFirstAndPowderFirstProduceTheSameComposition()
        {
            Add("water", "m.heater"); Add("grounds", "m.heater");
            Add("grounds", "m.brewer"); Add("water", "m.brewer");
            Assert.That(world.ContainerDescription(world.Find("m.heater")), Is.EqualTo(world.ContainerDescription(world.Find("m.brewer"))));
            Assert.That(world.LiquidSnapshot(world.Find("m.heater")).composition.coffeeMl, Is.EqualTo(200));
        }
        [Test] public void HeatingRequiresDockedContainerAndColdWaterReversesBlackCoffeeWithoutNaturalCooling()
        {
            Assert.That(world.OperateProduction("m.stove").success, Is.False);
            Add("grounds", "m.heater"); Add("water", "m.heater");
            var kettle = world.Find("m.heater");
            Assert.That(world.Interact(kettle.id, "m.stove").success, Is.True);
            Assert.That(kettle.production.phase, Is.EqualTo(ProductionPhase.Room));
            Assert.That(world.OperateProduction("m.stove").success, Is.True);
            world.Tick(world.Catalog.production.heatSeconds);
            Assert.That(world.LiquidSnapshot(kettle).temperature, Is.EqualTo(LiquidTemperature.Boiling));
            Assert.That(world.LiquidSnapshot(kettle).temperatureCelsius, Is.EqualTo(100).Within(.001f));
            Assert.That(world.ContainerDescription(kettle), Is.EqualTo("黑咖啡"));
            world.Tick(100);
            Assert.That(world.LiquidSnapshot(kettle).temperatureCelsius, Is.EqualTo(100).Within(.001f));
            Assert.That(world.ContainerDescription(kettle), Is.EqualTo("黑咖啡"));
            Assert.That(TotalSolidMass(kettle), Is.EqualTo(12));
            // Leave 100 ml hot coffee, then add 200 ml at 20 C: 46 2/3 C, within the kettle's capacity.
            Assert.That(world.PourLiquid(kettle.id, world.ServingCup.id, 100, 1).success, Is.True);
            Add("water", kettle.id);
            Assert.That(world.LiquidSnapshot(kettle).temperatureCelsius, Is.EqualTo((100 * 100 + 200 * 20) / 300f).Within(.001f));
            Assert.That(world.ContainerDescription(kettle), Is.EqualTo("冷萃咖啡"));
            Assert.That(world.LiquidVolume(kettle), Is.EqualTo(300));
            Assert.That(world.LiquidVolume(kettle), Is.LessThanOrEqualTo(world.LiquidCapacity(kettle)));
            Assert.That(TotalSolidMass(kettle), Is.EqualTo(6));
            Assert.That(TotalSolidMass(kettle) + TotalSolidMass(world.ServingCup), Is.EqualTo(12));
            Assert.That(kettle.production.spilledMl, Is.Zero);
        }
        [Test] public void CupCanBeHeatedAndAddingMilkAndCocoaKeepsAmounts()
        {
            Add("grounds", world.ServingCup.id); Add("water", world.ServingCup.id);
            Assert.That(world.Interact(world.ServingCup.id, "m.stove").success, Is.True);
            Assert.That(world.OperateProduction("m.stove").success, Is.True); world.Tick(4);
            Add("milk", world.ServingCup.id); Add("cocoa.powder", world.ServingCup.id);
            Assert.That(world.ContainerDescription(world.ServingCup), Is.EqualTo("迷之饮品"));
            Assert.That(world.LiquidVolume(world.ServingCup), Is.EqualTo(400));
            Assert.That(TotalSolidMass(world.ServingCup), Is.EqualTo(24));
        }
        [Test] public void PourTransfersAllIngredientsInProportionAndConservesSolidMass()
        {
            Add("grounds", "m.heater"); Add("water", "m.heater"); Add("milk", "m.heater");
            Assert.That(world.PourLiquid("m.heater", world.ServingCup.id, 100, 1).success, Is.True);
            var composition = world.LiquidSnapshot(world.ServingCup).composition;
            Assert.That(composition.coffeeMl, Is.EqualTo(50)); Assert.That(composition.milkMl, Is.EqualTo(50));
            Assert.That(TotalSolidMass(world.ServingCup), Is.EqualTo(3));
            Assert.That(TotalSolidMass(world.Find("m.heater")), Is.EqualTo(9));
        }
        [Test] public void DilutionKeepsConfiguredColdBrewNameAndConservesAmounts()
        {
            Add("grounds", "m.heater"); Add("water", "m.heater");
            var container = world.Find("m.heater");
            Assert.That(world.ContainerDescription(container), Is.EqualTo("冷萃咖啡"));
            Add("water", "m.heater");
            Assert.That(world.ContainerDescription(container), Is.EqualTo("冷萃咖啡"));
            Assert.That(world.LiquidSnapshot(container).composition.coffeeMl, Is.EqualTo(400));
            Assert.That(TotalSolidMass(container), Is.EqualTo(12));
        }
        [Test] public void MovingOffTheBurnerStopsHeatingWithoutDestroyingIngredients()
        {
            Add("water", "m.heater"); world.Interact("m.heater", "m.stove"); world.OperateProduction("m.stove");
            world.Find("m.heater").x += 3; world.Tick(4);
            Assert.That(world.LiquidSnapshot(world.Find("m.heater")).temperature, Is.EqualTo(LiquidTemperature.Room));
            Assert.That(world.LiquidVolume(world.Find("m.heater")), Is.EqualTo(200));
        }
        [Test] public void RepeatedSmallPoursMergeCoffeeDespiteTemperatureExtractionAndQualityChanges()
        {
            var source = world.Find("m.heater"); var cup = world.ServingCup;
            source.contents.Add(new Portion { specId = "coffee", coffeeKind = "cold_brew", units = 1, milliliters = 100,
                temperature = LiquidTemperature.Boiling, extracted = true, quality = 2, productionScore = 90 });
            source.production.phase = ProductionPhase.Boiling;
            cup.contents.Add(new Portion { specId = "coffee", coffeeKind = "cold_brew", units = 1, milliliters = 20,
                temperature = LiquidTemperature.Room, extracted = false, quality = 0, productionScore = 50 });
            for (int i = 0; i < 500; i++)
            {
                Assert.That(world.PourLiquid(source.id, cup.id, .1f, .02f).success, Is.True);
                Assert.That(cup.contents.Count, Is.EqualTo(1));
            }
            Assert.That(world.LiquidVolume(cup), Is.EqualTo(70).Within(.01f));
            Assert.That(world.LiquidVolume(source) + world.LiquidVolume(cup), Is.EqualTo(120).Within(.01f));
            Assert.That(cup.contents[0].productionScore, Is.EqualTo((20 * 50 + 50 * 90) / 70f).Within(.01f));
            Assert.That(cup.contents[0].quality, Is.EqualTo(0));
            // Repeated pours retain Celsius energy instead of rounding each drop back to the Room enum.
            Assert.That(world.TemperatureCelsius(cup.contents[0]), Is.EqualTo((20 * 20 + 50 * 100) / 70f).Within(.01f));
            Assert.That(cup.contents[0].temperature, Is.EqualTo(LiquidTemperature.Warm));
            Assert.That(world.CoffeeKind(cup.contents[0]), Is.EqualTo("black"));
            Assert.That(cup.contents[0].extracted, Is.True, "Extraction metadata is preserved; ordinary coffee kind follows the actual temperature");
            string token = " · " + world.IngredientLabel(cup.contents[0]) + " ";
            Assert.That(world.ContainerInspection(cup).Split(new[] { token }, System.StringSplitOptions.None).Length - 1, Is.EqualTo(1));
        }

        [Test] public void RepeatedWaterPoursConvertAndMergeWithCoffeeWithoutLosingGrounds()
        {
            Add("water", "m.heater"); Add("grounds", world.ServingCup.id);
            for (int i = 0; i < 200; i++)
                Assert.That(world.PourLiquid("m.heater", world.ServingCup.id, .2f, .02f).success, Is.True);
            Assert.That(world.ServingCup.contents.Count, Is.EqualTo(1));
            Assert.That(world.LiquidSnapshot(world.ServingCup).composition.coffeeMl, Is.EqualTo(40).Within(.01f));
            Assert.That(world.LiquidVolume(world.Find("m.heater")) + world.LiquidVolume(world.ServingCup), Is.EqualTo(200).Within(.01f));
            Assert.That(TotalSolidMass(world.ServingCup), Is.EqualTo(12));
            Assert.That(world.SolidMass(world.ServingCup), Is.Zero);
            Assert.That(world.ServingCup.contents.Exists(p => p.specId == "grounds"), Is.False);
        }

        [Test] public void SameIngredientPoursWeightScoresAndCelsiusWithoutCoolingOnTick()
        {
            var source = world.Find("m.heater"); var cup = world.ServingCup;
            cup.contents.Add(new Portion { specId = "coffee", coffeeKind = "cold_brew", units = 1, milliliters = 20, productionScore = 100,
                temperature = LiquidTemperature.Room });
            source.contents.Add(new Portion { specId = "coffee", coffeeKind = "cold_brew", units = 1, milliliters = 80, productionScore = 60,
                temperature = LiquidTemperature.Boiling, extracted = true });
            source.production.phase = ProductionPhase.Boiling;
            Assert.That(world.PourLiquid(source.id, cup.id, 80, 1).success, Is.True);
            Assert.That(cup.contents.Count, Is.EqualTo(1));
            Assert.That(cup.contents[0].productionScore, Is.EqualTo(68).Within(.001f));
            Assert.That(world.LiquidSnapshot(cup).temperatureCelsius, Is.EqualTo(84).Within(.001f));
            Assert.That(world.LiquidSnapshot(cup).temperature, Is.EqualTo(LiquidTemperature.Warm));
            world.Tick(100);
            Assert.That(world.LiquidSnapshot(cup).temperatureCelsius, Is.EqualTo(84).Within(.001f));
            Assert.That(world.LiquidSnapshot(cup).temperature, Is.EqualTo(LiquidTemperature.Warm));
            Assert.That(world.LiquidVolume(cup), Is.EqualTo(100));
        }

        [Test] public void RepeatedMixedPoursConserveSolidsAndWeightTheirScoresByMass()
        {
            var source = world.Find("m.heater"); var cup = world.ServingCup;
            source.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 100 });
            world.AddConstituent(source.contents[0], "water", 0, 100);
            source.contents.Add(new Portion { specId = "sugar", units = 1, grams = 12, productionScore = 80 });
            source.production.phase = ProductionPhase.Room;
            cup.contents.Add(new Portion { specId = "sugar", units = 1, grams = 3, productionScore = 20 });
            for (int i = 0; i < 500; i++) world.PourLiquid(source.id, cup.id, .1f, .02f);
            Assert.That(cup.contents.Count, Is.EqualTo(2));
            Assert.That(TotalSolidMass(cup), Is.EqualTo(9).Within(.01f));
            Assert.That(TotalSolidMass(cup) + TotalSolidMass(source), Is.EqualTo(15).Within(.01f));
            Assert.That(cup.contents.Find(p => p.specId == "sugar").productionScore, Is.EqualTo(60).Within(.01f));
        }

        [Test] public void InspectionAggregatesFragmentsAndTemperatureAliasesWithoutMutatingData()
        {
            var cup = world.ServingCup;
            cup.contents.Add(new Portion { specId = "coffee", coffeeKind = "cold_brew", units = 1, milliliters = .1f });
            cup.contents.Add(new Portion { specId = "coffee", coffeeKind = "cold_brew", units = 1, milliliters = .2f, quality = 2, extracted = true });
            cup.contents.Add(new Portion { specId = "milk", units = 1, milliliters = 10 });
            cup.contents.Add(new Portion { specId = "hotmilk", units = 1, milliliters = 20, temperature = LiquidTemperature.Boiling });
            string before = JsonUtility.ToJson(world.State);
            string inspection = world.ContainerInspection(cup);
            string coffeeToken = " · " + world.IngredientLabel(cup.contents[0]) + " ";
            Assert.That(inspection.Split(new[] { coffeeToken }, System.StringSplitOptions.None).Length - 1, Is.EqualTo(1));
            Assert.That(inspection, Does.Contain(coffeeToken + .3f.ToString("0.0") + "ml"));
            Assert.That(inspection, Does.Contain(" · 牛奶 " + 30f.ToString("0.0") + "ml"));
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }

        [Test] public void RestoreCompactsFragmentedSaveAfterValidationAndPreservesComposition()
        {
            var cup = world.ServingCup;
            cup.contents.Add(new Portion { specId = "coffee", coffeeKind = "cold_brew", units = 1, milliliters = 10, productionScore = 20, quality = 2, addedSequence = 1 });
            cup.contents.Add(new Portion { specId = "coffee", coffeeKind = "cold_brew", units = 1, milliliters = 30, productionScore = 100, quality = 1,
                extracted = true, temperature = LiquidTemperature.Boiling, addedSequence = 3 });
            cup.contents.Add(new Portion { specId = "grounds", units = 1, grams = 3, productionScore = 20 });
            cup.contents.Add(new Portion { specId = "grounds", units = 1, grams = 9, productionScore = 100 });
            cup.production.phase = ProductionPhase.Boiling; cup.production.remaining = 3; cup.production.additionSequence = 3;
            string id = cup.id;
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(world.Restore(snapshot).success, Is.True);
            cup = world.Find(id);
            Assert.That(cup.contents.Count, Is.EqualTo(1));
            Assert.That(world.LiquidSnapshot(cup).composition.coffeeMl, Is.EqualTo(40));
            Assert.That(TotalSolidMass(cup), Is.EqualTo(12));
            var coffee = cup.contents.Find(p => p.specId == "coffee");
            Assert.That(coffee.productionScore, Is.EqualTo(80)); Assert.That(coffee.quality, Is.EqualTo(1));
            Assert.That(world.CoffeeKind(coffee), Is.EqualTo("black"));
            Assert.That(coffee.extracted, Is.True); Assert.That(coffee.addedSequence, Is.EqualTo(1));
            // The 12 g free grounds still carry 20 C before restore absorbs them into provenance.
            Assert.That(world.LiquidSnapshot(cup).temperatureCelsius, Is.EqualTo((10 * 20 + 30 * 100 + 12 * 20) / 52f).Within(.001f));
            Assert.That(world.LiquidSnapshot(cup).temperature, Is.EqualTo(LiquidTemperature.Warm));
            Assert.That(cup.production.remaining, Is.Zero, "A saved cooling timer is obsolete outside Processing");
            Assert.That(cup.contents.Exists(p => p.specId == "grounds"), Is.False);
            Assert.That(world.CoffeeGroundsGrams(coffee), Is.EqualTo(12));
            Assert.That(world.Restore(JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State))).success, Is.True);
            Assert.That(world.Find(id).contents.Count, Is.EqualTo(1));
        }

        [Test] public void RepeatedSolidAdditionsAccumulateMassWithoutInflatingUnitsAndKeepLowestQuality()
        {
            for (int i = 0; i < 20; i++)
            {
                var item = new WorldEntity { id = "test." + world.State.nextId++, specId = "grounds", units = 1,
                    quality = i == 10 ? 0 : 2, area = AreaId.Counter };
                world.State.entities.Add(item);
                Assert.That(world.Interact(item.id, world.ServingCup.id).success, Is.True);
            }
            Assert.That(world.ServingCup.contents.Count, Is.EqualTo(1));
            Assert.That(world.ServingCup.contents[0].units, Is.EqualTo(1));
            Assert.That(world.ServingCup.contents[0].quality, Is.EqualTo(0));
            Assert.That(TotalSolidMass(world.ServingCup), Is.EqualTo(240));
        }

        [Test] public void RestoreMergesHotAndColdMilkAliasesAndRemainsValidOnNextSave()
        {
            var cup = world.ServingCup;
            cup.contents.Add(new Portion { specId = "hotmilk", units = 1, milliliters = 10,
                temperature = LiquidTemperature.Boiling });
            cup.contents.Add(new Portion { specId = "milk", units = 1, milliliters = 90,
                temperature = LiquidTemperature.Room });
            cup.production.phase = ProductionPhase.Room;
            string id = cup.id;
            Assert.That(world.Restore(JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State))).success, Is.True);
            cup = world.Find(id);
            Assert.That(cup.contents.Count, Is.EqualTo(1));
            Assert.That(cup.contents[0].specId, Is.EqualTo("hotmilk"));
            Assert.That(world.LiquidSnapshot(cup).temperature, Is.EqualTo(LiquidTemperature.Room));
            Assert.That(world.LiquidSnapshot(cup).temperatureCelsius, Is.EqualTo(28).Within(.001f));
            Assert.That(world.LiquidSnapshot(cup).composition.milkMl, Is.EqualTo(100));
            Assert.That(world.Restore(JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State))).success, Is.True);
            Assert.That(world.Find(id).contents.Count, Is.EqualTo(1));
        }

        [Test] public void InvalidFragmentedSaveIsRejectedBeforeCompactionOfLiveWorld()
        {
            Add("water", world.ServingCup.id);
            string before = JsonUtility.ToJson(world.State);
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(before);
            var cup = snapshot.entities.Find(e => e.id == world.ServingCup.id);
            cup.contents.Add(new Portion { specId = "water", units = 1, milliliters = -1 });
            Assert.That(world.Restore(snapshot).success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }

        [Test] public void PourDoesNotDiscardTinyRemainingVolume()
        {
            var source = world.Find("m.heater");
            source.contents.Add(new Portion { specId = "water", units = 1, milliliters = .001f });
            source.production.phase = ProductionPhase.Room;
            Assert.That(world.PourLiquid(source.id, world.ServingCup.id, .0005f, 1).success, Is.True);
            Assert.That(source.contents.Count, Is.EqualTo(1));
            Assert.That(world.LiquidVolume(source) + world.LiquidVolume(world.ServingCup), Is.EqualTo(.001f).Within(.000001f));
        }

        [Test] public void MixedContainerRoundTripRetainsGramsThermalStateAndStoveReference()
        {
            Add("grounds", "m.heater"); Add("water", "m.heater"); world.Interact("m.heater", "m.stove");
            world.OperateProduction("m.stove"); world.Tick(2);
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(world.Restore(snapshot).success, Is.True);
            Assert.That(world.Find("m.heater").production.phase, Is.EqualTo(ProductionPhase.Processing));
            Assert.That(world.Find("m.heater").production.remaining, Is.EqualTo(2).Within(.001f));
            world.Tick(2);
            Assert.That(TotalSolidMass(world.Find("m.heater")), Is.EqualTo(12));
            Assert.That(world.Find("m.stove").production.heatingVesselId, Is.EqualTo("m.heater"));
            var coffee = world.Find("m.heater").contents.Find(portion => portion.specId == "coffee");
            Assert.That(world.CoffeeKind(coffee), Is.EqualTo("black"));
            Assert.That(world.LiquidSnapshot(world.Find("m.heater")).temperature, Is.EqualTo(LiquidTemperature.Boiling));
            Assert.That(world.LiquidSnapshot(world.Find("m.heater")).temperatureCelsius, Is.EqualTo(100).Within(.001f));
            Assert.That(world.Find("m.heater").production.remaining, Is.Zero);
        }
    }
}
