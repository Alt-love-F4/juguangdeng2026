using System;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeNumericTemperatureTests
    {
        private CafeWorld world;
        private WorldEntity Source => world.Find("m.heater");
        private WorldEntity Cup => world.ServingCup;

        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnterProduction();
            Assert.That(world.SelectServingCup("s.cups.large").success, Is.True);
        }

        private Portion Portion(string ingredient, float ml, float grams, float temperature)
        {
            var portion = new Portion { specId = ingredient, units = 1, milliliters = ml, grams = grams };
            world.SetTemperature(portion, temperature);
            return portion;
        }

        private void SetSource(float ml, float temperature, string ingredient = "water")
        {
            Source.contents.Clear();
            Source.contents.Add(Portion(ingredient, ml, 0, temperature));
            Source.production.phase = ProductionPhase.Room;
            Source.production.remaining = 0;
        }

        private void AddDose(WorldEntity target, string ingredient, float ml, float grams, float temperature)
        {
            var dose = new WorldEntity { id = "numeric." + world.State.nextId++, specId = ingredient,
                units = 1, area = AreaId.Counter, portionPayload = Portion(ingredient, ml, grams, temperature) };
            world.State.entities.Add(dose);
            Assert.That(world.Interact(dose.id, target.id).success, Is.True);
        }

        private CafeSnapshot Copy() => JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
        private double Energy(WorldEntity entity)
        {
            double energy = 0;
            foreach (var portion in entity.contents) energy += world.GetThermalWeight(portion) * world.TemperatureCelsius(portion);
            return energy;
        }

        [Test] public void UnequalVolumesMixExactCelsiusRatherThanRoundedEnum()
        {
            Cup.contents.Add(Portion("milk", 300, 0, 20));
            SetSource(100, 100);
            Assert.That(world.PourLiquid(Source.id, Cup.id, 100, 1).success, Is.True);
            Assert.That(world.LiquidSnapshot(Cup).temperatureCelsius, Is.EqualTo(40).Within(.0001f));
            Assert.That(world.LiquidSnapshot(Cup).temperature, Is.EqualTo(LiquidTemperature.Room));
            foreach (var portion in Cup.contents)
            {
                Assert.That(portion.temperatureCelsius, Is.EqualTo(40).Within(.0001f));
                Assert.That(portion.temperature, Is.EqualTo(LiquidTemperature.Room));
            }
        }

        [Test] public void AddingSolidToHotLiquidUsesActualFreeGrams()
        {
            Cup.contents.Add(Portion("water", 100, 0, 100));
            AddDose(Cup, "sugar", 0, 100, 20);
            Assert.That(world.TemperatureCelsius(Cup), Is.EqualTo(60).Within(.0001f));
            Assert.That(world.SolidMass(Cup), Is.EqualTo(100));
            Assert.That(Energy(Cup), Is.EqualTo(12000).Within(.01));
            foreach (var portion in Cup.contents) Assert.That(portion.temperatureCelsius, Is.EqualTo(60));
        }

        [Test] public void HotWaterMixesPowderBeforeReactionAndProvenanceDoesNotAddThermalMass()
        {
            AddDose(Cup, "grounds", 0, 12, 20);
            AddDose(Cup, "hotwater", 200, 0, 100);
            float expected = (200 * 100f + 12 * 20f) / 212;
            Assert.That(world.TemperatureCelsius(Cup), Is.EqualTo(expected).Within(.0001f));
            var coffee = Cup.contents.Find(p => p.specId == "coffee");
            Assert.That(coffee, Is.Not.Null);
            Assert.That(world.CoffeeGroundsGrams(coffee), Is.EqualTo(12));
            Assert.That(world.GetThermalWeight(coffee), Is.EqualTo(200));
            SetSource(100, 20);
            world.PourLiquid(Source.id, Cup.id, 100, 1);
            Assert.That(world.TemperatureCelsius(Cup), Is.EqualTo((200 * expected + 100 * 20) / 300).Within(.0001f));
            Assert.That(world.CoffeeGroundsGrams(coffee), Is.EqualTo(12));
        }

        [Test] public void AcceptedCapacityNotRequestedOrSpilledVolumeControlsMixing()
        {
            Cup.contents.Add(Portion("milk", 350, 0, 20));
            SetSource(200, 100);
            double before = Energy(Source) + Energy(Cup);
            world.PourLiquid(Source.id, Cup.id, 100, 1);
            Assert.That(world.LiquidVolume(Cup), Is.EqualTo(400));
            Assert.That(world.TemperatureCelsius(Cup), Is.EqualTo(30).Within(.0001f));
            Assert.That(world.LiquidVolume(Source), Is.EqualTo(100));
            Assert.That(Source.production.spilledMl, Is.EqualTo(50));
            Assert.That(Energy(Source) + Energy(Cup) + 50 * 100, Is.EqualTo(before).Within(.01));
        }

        [TestCase(60)] [TestCase(100)]
        public void LongTicksNeverCoolWarmOrBoilingContents(float temperature)
        {
            AddDose(Cup, "milk", 200, 0, temperature);
            var phase = Cup.production.phase;
            world.Tick(100000);
            Assert.That(world.TemperatureCelsius(Cup), Is.EqualTo(temperature));
            Assert.That(Cup.production.phase, Is.EqualTo(phase));
            Assert.That(Cup.production.remaining, Is.Zero);
        }

        [Test] public void HeatingInterpolatesAndInterruptKeepsExactTemperature()
        {
            SetSource(200, 20);
            Assert.That(world.Interact(Source.id, "m.stove").success, Is.True);
            Assert.That(world.OperateProduction("m.stove").success, Is.True);
            world.Tick(1);
            Assert.That(world.TemperatureCelsius(Source), Is.EqualTo(40).Within(.0001f));
            Assert.That(Source.production.remaining, Is.EqualTo(3));
            Assert.That(Source.production.phase, Is.EqualTo(ProductionPhase.Processing));
            world.Tick(.375f);
            Assert.That(world.TemperatureCelsius(Source), Is.EqualTo(47.5f).Within(.0001f));
            world.SuspendContainerHeating(Source.id);
            Assert.That(world.TemperatureCelsius(Source), Is.EqualTo(47.5f).Within(.0001f));
            Assert.That(Source.production.remaining, Is.Zero);
            world.Tick(10000);
            Assert.That(world.TemperatureCelsius(Source), Is.EqualTo(47.5f).Within(.0001f));
            world.OperateProduction("m.stove");
            world.Tick(2);
            Assert.That(world.TemperatureCelsius(Source), Is.EqualTo(73.75f).Within(.0001f));
            world.Tick(10000);
            Assert.That(world.TemperatureCelsius(Source), Is.EqualTo(100));
            Assert.That(Source.production.phase, Is.EqualTo(ProductionPhase.Boiling));
            Assert.That(Source.production.remaining, Is.Zero);
        }

        [Test] public void MovingVesselOffStoveInterruptsWithoutQuantizing()
        {
            SetSource(200, 20);
            world.Interact(Source.id, "m.stove"); world.OperateProduction("m.stove"); world.Tick(.3f);
            float temperature = world.TemperatureCelsius(Source);
            Assert.That(world.Move(Source.id, AreaId.Counter, 0, -2).success, Is.True);
            world.Tick(10000);
            Assert.That(world.TemperatureCelsius(Source), Is.EqualTo(temperature).Within(.0001f));
            Assert.That(Source.production.phase, Is.Not.EqualTo(ProductionPhase.Processing));
        }

        [Test] public void FourQuarterPoursPreserveExactTemperatureFreeSolidsAndEnergy()
        {
            Source.contents.Clear();
            Source.contents.Add(Portion("coffee", 200, 0, 73.25f));
            world.AddConstituent(Source.contents[0], "grounds", 12, 0);
            world.AddConstituent(Source.contents[0], "water", 0, 200);
            Source.contents.Add(Portion("milk", 200, 0, 73.25f));
            Source.contents.Add(Portion("sugar", 0, 20, 73.25f));
            double before = Energy(Source), received = 0;
            for (int i = 0; i < 4; i++)
            {
                Assert.That(world.Take("s.cups.small", AreaId.Counter, 0, -2, out var target).success, Is.True);
                Assert.That(world.PourLiquid(Source.id, target.id, 100, 1).success, Is.True);
                Assert.That(world.TemperatureCelsius(target), Is.EqualTo(73.25f).Within(.0001f));
                Assert.That(world.LiquidVolume(target), Is.EqualTo(100).Within(.0001f));
                Assert.That(world.SolidMass(target), Is.EqualTo(5).Within(.0001f));
                Assert.That(world.CoffeeGroundsGrams(target.contents.Find(p => p.specId == "coffee")), Is.EqualTo(3).Within(.0001f));
                received += Energy(target);
                Assert.That(received + Energy(Source), Is.EqualTo(before).Within(.02));
            }
            Assert.That(Source.contents, Is.Empty);
        }

        [Test] public void FilterDrainTransfersExactMixedTemperatureAndRetainsSolidTemperature()
        {
            var jug = world.Find("v.extractor"); var filter = world.ExtractionFilter(jug);
            AddDose(filter, "grounds", 0, 12, 20);
            SetSource(200, 100);
            world.PourLiquid(Source.id, filter.id, 100, 1);
            float mixed = (100 * 100f + 12 * 20f) / 112;
            Assert.That(world.TemperatureCelsius(filter), Is.EqualTo(mixed).Within(.0001f));
            world.Tick(world.Catalog.production.filterResidenceSeconds + 1);
            Assert.That(world.LiquidVolume(jug), Is.EqualTo(100).Within(.001f));
            Assert.That(world.TemperatureCelsius(jug), Is.EqualTo(mixed).Within(.0001f));
            Assert.That(world.TemperatureCelsius(filter), Is.EqualTo(mixed).Within(.0001f));
            foreach (var portion in filter.contents) Assert.That(portion.temperatureCelsius, Is.EqualTo(mixed).Within(.0001f));
            Assert.That(world.SolidMass(filter), Is.EqualTo(6).Within(.001f));
        }

        [Test] public void SaveRoundtripPreservesFractionalCelsiusAndNormalizesLegacyMirror()
        {
            AddDose(Cup, "milk", 200, 0, 67.375f);
            var candidate = Copy();
            var portion = candidate.entities.Find(e => e.id == Cup.id).contents[0];
            portion.temperature = LiquidTemperature.Boiling; // Stale mirror must not override authoritative Celsius.
            Assert.That(world.Restore(candidate).success, Is.True);
            Assert.That(world.TemperatureCelsius(world.ServingCup), Is.EqualTo(67.375f));
            Assert.That(world.ServingCup.contents[0].temperature, Is.EqualTo(LiquidTemperature.Warm));
            Assert.That(world.ServingCup.production.remaining, Is.Zero);
        }

        [TestCase(LiquidTemperature.Room, 20)]
        [TestCase(LiquidTemperature.Warm, 60)]
        [TestCase(LiquidTemperature.Boiling, 100)]
        public void LegacySavedSentinelMigratesWithoutTemperatureLoss(LiquidTemperature band, float expected)
        {
            var candidate = Copy(); var cup = candidate.entities.Find(e => e.id == Cup.id);
            cup.contents.Add(new Portion { specId = "milk", units = 1, milliliters = 200, temperature = band });
            cup.production.phase = ProductionPhase.Warm; cup.production.remaining = 10;
            Assert.That(world.Restore(candidate).success, Is.True);
            Assert.That(world.ServingCup.contents[0].temperatureCelsius, Is.EqualTo(expected));
            world.Tick(10000);
            Assert.That(world.TemperatureCelsius(world.ServingCup), Is.EqualTo(expected));
            Assert.That(world.ServingCup.production.remaining, Is.Zero);
        }

        [Test] public void MissingNumericJsonFieldRetainsLegacySentinel()
        {
            var portion = JsonUtility.FromJson<Portion>("{\"specId\":\"milk\",\"units\":1,\"milliliters\":200,\"temperature\":2}");
            Assert.That(portion.temperatureCelsius, Is.EqualTo(-1), "Missing numeric data must remain the legacy sentinel.");
            Assert.That(world.TemperatureCelsius(portion), Is.EqualTo(100));
        }

        [Test] public void SnapshotConstructorKeepsLegacyCallsitesAndOptionalExactCelsius()
        {
            var legacy = new CafeLiquidSnapshot(200, 400, LiquidTemperature.Warm, ProductionPhase.Warm, "milk", 100);
            var exact = new CafeLiquidSnapshot(200, 400, LiquidTemperature.Warm, ProductionPhase.Warm, "milk", 100, default, 67.375f);
            Assert.That(legacy.temperatureCelsius, Is.EqualTo(60));
            Assert.That(exact.temperatureCelsius, Is.EqualTo(67.375f));
            Assert.That(world.LiquidSnapshot(null).temperatureCelsius, Is.EqualTo(20));
        }

        [TestCase(-2)] [TestCase(-.25f)] [TestCase(101)]
        public void InvalidSavedCelsiusIsRejectedWithoutChangingWorld(float invalid)
        {
            AssertInvalidRestore(invalid);
        }
        [Test] public void NonFiniteSavedCelsiusIsRejectedWithoutChangingWorld()
        {
            AssertInvalidRestore(float.NaN); AssertInvalidRestore(float.PositiveInfinity);
        }
        private void AssertInvalidRestore(float invalid)
        {
            var original = world.State; string before = JsonUtility.ToJson(original);
            var candidate = Copy();
            candidate.entities.Find(e => e.id == Cup.id).contents.Add(new Portion { specId = "milk", units = 1, milliliters = 100, temperatureCelsius = invalid });
            Assert.That(world.Restore(candidate).success, Is.False);
            Assert.That(world.State, Is.SameAs(original));
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }

        [Test] public void InvalidSetTemperatureLeavesPortionUnchanged()
        {
            var portion = Portion("milk", 100, 0, 62.25f);
            foreach (float invalid in new[] { -1f, 101f, float.NaN, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => world.SetTemperature(portion, invalid));
            Assert.That(portion.temperatureCelsius, Is.EqualTo(62.25f));
            Assert.That(portion.temperature, Is.EqualTo(LiquidTemperature.Warm));
        }

        [Test] public void CoffeeKindUsesNumericThresholdWhileEssenceNeverChanges()
        {
            var coffee = Portion("coffee", 100, 0, 49.99f);
            Assert.That(world.CoffeeKind(coffee), Is.EqualTo("cold_brew"));
            world.SetTemperature(coffee, 50); Assert.That(world.CoffeeKind(coffee), Is.EqualTo("black"));
            coffee.coffeeKind = "essence";
            world.SetTemperature(coffee, 0); Assert.That(world.CoffeeKind(coffee), Is.EqualTo("essence"));
            Assert.That(world.TemperatureBand(94.99f), Is.EqualTo(LiquidTemperature.Warm));
            Assert.That(world.TemperatureBand(95), Is.EqualTo(LiquidTemperature.Boiling));
        }

        [Test] public void ConfigurableDefaultsThresholdsAndHeatingTargetApply()
        {
            var settings = world.Catalog.production;
            settings.ambientTemperatureCelsius = 25; settings.warmTemperatureCelsius = 65;
            settings.boilingTemperatureCelsius = 98; settings.hotThresholdCelsius = 55;
            settings.boilingThresholdCelsius = 96; settings.heatingTargetCelsius = 90;
            Assert.That(world.TemperatureCelsius(new Portion()), Is.EqualTo(25));
            Assert.That(world.TemperatureCelsius(new Portion { temperature = LiquidTemperature.Warm }), Is.EqualTo(65));
            Assert.That(world.TemperatureCelsius(new Portion { temperature = LiquidTemperature.Boiling }), Is.EqualTo(98));
            Assert.That(world.Take("s.milk", AreaId.Counter, 0, -2, out var roomMilk).success, Is.True);
            Assert.That(world.Interact(roomMilk.id, Cup.id).success, Is.True);
            Assert.That(Cup.contents[0].temperatureCelsius, Is.EqualTo(25));
            Assert.That(world.Take("s.milk", AreaId.Counter, 0, -2, out var hotMilk).success, Is.True);
            hotMilk.specId = "hotmilk";
            Assert.That(world.Interact(hotMilk.id, Cup.id).success, Is.True);
            Assert.That(world.TemperatureCelsius(Cup), Is.EqualTo((200 * 25 + 200 * 98f) / 400).Within(.0001f));
            Assert.That(world.TemperatureBand(54), Is.EqualTo(LiquidTemperature.Room));
            SetSource(200, 25); world.Interact(Source.id, "m.stove"); world.OperateProduction("m.stove"); world.Tick(4);
            Assert.That(world.TemperatureCelsius(Source), Is.EqualTo(90));
            Assert.That(Source.production.phase, Is.EqualTo(ProductionPhase.Warm));
        }
    }
}
