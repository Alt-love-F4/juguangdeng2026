using System;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafePrecisionExtractionTests
    {
        private CafeWorld world;
        private const string Target = "v.extractor";
        private WorldEntity Extractor => world.Find(Target);
        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction(); world.EnterProduction();
        }
        private void Add(string ingredient, string target = Target)
        {
            var item = new WorldEntity { id = "precision." + world.State.nextId++, specId = ingredient, units = 1, area = AreaId.Counter };
            world.State.entities.Add(item);
            Assert.That(world.Interact(item.id, target).success, Is.True);
        }
        private void Supply(string ingredient = "water", LiquidTemperature temperature = LiquidTemperature.Boiling)
        {
            var source = world.Find("m.heater"); source.contents.Clear();
            source.contents.Add(new Portion { specId = ingredient, units = 1, milliliters = 400, temperature = temperature });
            source.production.phase = ProductionPhase.Room;
        }
        private void Pour(float ml, float dt) => Assert.That(world.PourLiquid("m.heater", Target, ml, dt).success, Is.True);
        private CafeExtractionSnapshot Snapshot => world.ExtractionSnapshot(Target);

        [Test] public void GroundsScaleAllBandsAndRemainFrozenDuringSession()
        {
            Add("grounds"); Add("grounds"); Supply(); Pour(10, 1);
            Assert.That(Snapshot.idealMl, Is.EqualTo(400)); Assert.That(Snapshot.idealSeconds, Is.EqualTo(15));
            Assert.That(Snapshot.greenMlMin, Is.EqualTo(300));
            var extra = new WorldEntity { id = "precision.extra", specId = "grounds", units = 1, area = AreaId.Counter };
            world.State.entities.Add(extra);
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.Interact(extra.id, Target).success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            Assert.That(Snapshot.groundsGrams, Is.EqualTo(24));
        }
        [Test] public void IndependentGreenLengthsAreExactAndBrewerIsNotPrecisionApparatus()
        {
            var p = world.Catalog.production;
            p.brewGreenLeftMl = 20; p.brewGreenRightMl = 45;
            p.brewGreenLeftSeconds = 2; p.brewGreenRightSeconds = 4;
            Add("grounds");
            Assert.That(Snapshot.greenMlMin, Is.EqualTo(165)); Assert.That(Snapshot.greenMlMax, Is.EqualTo(260));
            Assert.That(Snapshot.greenTimeMin, Is.EqualTo(4.5f)); Assert.That(Snapshot.greenTimeMax, Is.EqualTo(12.5f));
            Assert.That(world.IsPrecisionExtractor(world.Find("m.brewer")), Is.False);
            Assert.That(world.ExtractionSnapshot("m.brewer").available, Is.False);
        }
        [TestCase("water", LiquidTemperature.Room)]
        [TestCase("milk", LiquidTemperature.Boiling)]
        [TestCase("coffee", LiquidTemperature.Boiling)]
        public void NonHotWaterDoesNotTriggerOrAdvanceWaterCounter(string ingredient, LiquidTemperature temperature)
        {
            Add("grounds"); Supply(ingredient, temperature); Pour(100, 1); world.Tick(10);
            Assert.That(Snapshot.inProgress, Is.False); Assert.That(Snapshot.injectedHotWaterMl, Is.Zero);
            Assert.That(Snapshot.elapsedSeconds, Is.Zero);
        }
        [TestCase("milk")]
        [TestCase("coffee")]
        [TestCase("hotwater")]
        public void PrefilledLiquidThenGroundsAndMoreWaterStaysOrdinary(string ingredient)
        {
            Add(ingredient); Add("grounds"); Supply(); Pour(100, 1);
            Assert.That(Snapshot.inProgress, Is.False); Assert.That(Snapshot.injectedHotWaterMl, Is.Zero);
        }
        [Test] public void OnlyAcceptedHotWaterCountsAndFullTargetDoesNotStartTimer()
        {
            Add("grounds"); Add("milk"); Add("milk"); Supply();
            Pour(100, 1); world.Tick(2);
            Assert.That(Snapshot.inProgress, Is.False); Assert.That(Snapshot.elapsedSeconds, Is.Zero);
            Assert.That(Snapshot.injectedHotWaterMl, Is.Zero);
        }
        [Test] public void MixedSourceCountsOnlyItsHotWaterFraction()
        {
            Add("grounds"); Supply();
            var source = world.Find("m.heater"); source.contents[0].milliliters = 200;
            source.contents.Add(new Portion { specId = "hotmilk", units = 1, milliliters = 200, temperature = LiquidTemperature.Boiling });
            Pour(200, 2);
            Assert.That(Snapshot.injectedHotWaterMl, Is.EqualTo(100));
            Assert.That(world.LiquidVolume(Extractor) + world.LiquidVolume(world.ExtractionFilter(Extractor)), Is.EqualTo(200));
            world.FinishPour(Target); Assert.That(Snapshot.completed, Is.False);
        }
        [Test] public void FirstAcceptedDropStartsClockAndStoppedPourPausesUntilResume()
        {
            Add("grounds"); world.Tick(5); Assert.That(Snapshot.elapsedSeconds, Is.Zero);
            Supply(); Pour(100, 2); Assert.That(Snapshot.elapsedSeconds, Is.Zero);
            world.Tick(2); world.FinishPour(Target);
            Assert.That(Snapshot.inProgress, Is.True); Assert.That(Snapshot.completed, Is.False);
            world.Tick(3); Assert.That(Snapshot.elapsedSeconds, Is.EqualTo(2));
            Pour(100, 2); world.Tick(5.5f); world.FinishPour(Target);
            Assert.That(Snapshot.completed, Is.True); Assert.That(Snapshot.qualityLabel, Is.EqualTo("优秀精粹咖啡液"));
            world.Tick(10); Assert.That(Snapshot.elapsedSeconds, Is.EqualTo(7.5f));
            Assert.That(Extractor.contents.Find(p => p.specId == "coffee").extracted, Is.True);
            Assert.That(Extractor.contents.Find(p => p.specId == "coffee").productionScore, Is.EqualTo(100));
        }
        [TestCase(200, 7.5f, "优秀精粹咖啡液")]
        [TestCase(150, 7.5f, "良好精粹咖啡液")]
        [TestCase(200, 4, "良好精粹咖啡液")]
        [TestCase(200, 1, "糟糕精粹咖啡液")]
        [TestCase(200, 12, "糟糕精粹咖啡液")]
        public void StopUsesBothTimeAndWaterBands(float ml, float elapsed, string quality)
        {
            Add("grounds"); Supply(); Pour(ml, elapsed); world.Tick(elapsed); world.FinishPour(Target);
            Assert.That(Snapshot.completed, Is.True); Assert.That(Snapshot.qualityLabel, Is.EqualTo(quality));
        }
        [Test] public void OverGreenCompletesPoorImmediatelyWithoutStop()
        {
            Add("grounds"); Supply(); Pour(251, 7.5f);
            Assert.That(Snapshot.completed, Is.True); Assert.That(Snapshot.qualityLabel, Is.EqualTo("糟糕精粹咖啡液"));
        }
        private float VariableRateUniformity(int subdivisions)
        {
            Setup(); Add("grounds"); Supply();
            for (int segment = 0; segment < 2; segment++)
                for (int i = 0; i < subdivisions; i++)
                {
                    float dt = 3.75f / subdivisions;
                    Pour((segment == 0 ? 50f : 150f) / subdivisions, dt); world.Tick(dt);
                }
            world.FinishPour(Target); return Snapshot.uniformityScore;
        }
        [Test] public void UniformityIsTimeWeightedAndIndependentOfPourFrameSubdivision()
        {
            float coarse = VariableRateUniformity(1), fine = VariableRateUniformity(30);
            Assert.That(coarse, Is.EqualTo(50).Within(.01f)); Assert.That(fine, Is.EqualTo(coarse).Within(.01f));
            Assert.That(Snapshot.qualityLabel, Is.EqualTo("糟糕精粹咖啡液"));
        }
        [Test] public void DepletedFinalPourFrameUsesAcceptedStreamDurationForUniformity()
        {
            Add("grounds"); Supply();
            world.Find("m.heater").contents[0].milliliters = 200;
            Pour(100, 1); Pour(200, 2); // second frame depleted halfway, still a steady 100ml/s stream
            world.Tick(7.5f); world.FinishPour(Target);
            Assert.That(Snapshot.uniformityScore, Is.EqualTo(100).Within(.01f));
            Assert.That(Snapshot.qualityLabel, Is.EqualTo("优秀精粹咖啡液"));
        }
        [Test] public void SavedInProgressSessionResumesAndRejectsMalformedFields()
        {
            Add("grounds"); Supply(); Pour(100, 2); world.Tick(3);
            var saved = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            var restored = new CafeWorld(world.Catalog); Assert.That(restored.Restore(saved).success, Is.True);
            Assert.That(restored.ExtractionSnapshot(Target).elapsedSeconds, Is.EqualTo(3));
            restored.Tick(4.5f); restored.PourLiquid("m.heater", Target, 100, 2); restored.FinishPour(Target);
            Assert.That(restored.ExtractionSnapshot(Target).qualityLabel, Is.EqualTo("优秀精粹咖啡液"));
            saved.entities.Find(e => e.id == Target).production.brewRateSquareIntegral = float.NaN;
            Assert.That(restored.Restore(saved).success, Is.False);
        }
        [Test] public void CompletedBatchResetsForNewGroundsAndRejectsMalformedPourWithoutMutation()
        {
            Add("grounds"); Supply(); Pour(200, 7.5f); world.Tick(7.5f); world.FinishPour(Target);
            Assert.That(world.EmptyProductionMachine(Target).success, Is.True);
            var filter = world.Find(Target + ".filter");
            Assert.That(filter.contents, Is.Empty);
            Assert.That(filter.production.filterSpent, Is.True);
            string emptyFilter = JsonUtility.ToJson(world.State);
            Assert.That(world.EmptyProductionMachine(filter.id).success, Is.False, "A fully depleted filter has no remaining inventory to discard");
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(emptyFilter));
            Assert.That(world.Interact(filter.id, Target).success, Is.True);
            Add("grounds"); Assert.That(Snapshot.completed, Is.False); Assert.That(Snapshot.inProgress, Is.False);
            Assert.That(filter.production.filterSpent, Is.False);
            Assert.That(filter.production.filterWaterPassedMl, Is.Zero);
            Assert.That(world.SolidMass(filter), Is.EqualTo(12));
            Assert.That(Snapshot.elapsedSeconds, Is.Zero); Assert.That(Snapshot.idealMl, Is.EqualTo(200));
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.PourLiquid("m.heater", Target, float.NaN, 1).success, Is.False);
            Assert.That(world.PourLiquid("m.heater", Target, 10, float.PositiveInfinity).success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }
        [Test] public void InvalidNewConfigurationIsRejected()
        {
            world.Catalog.production.brewReferenceGroundsGrams = 0;
            Assert.Throws<ArgumentException>(() => new CafeWorld(world.Catalog));
        }
    }
}
