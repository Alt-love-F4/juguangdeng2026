using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeSoundDynamicsTests
    {
        private CafeWorld world;

        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction(); world.EnterProduction();
        }

        [Test] public void FlowAndFillRaisePourVolumeAndPitchWithinClampedRange()
        {
            FreeCafeSoundDynamics.Pour(0, 0, out float lowVolume, out float lowPitch);
            FreeCafeSoundDynamics.Pour(1, 0, out float highVolume, out float highPitch);
            FreeCafeSoundDynamics.Pour(1, 1, out float filledVolume, out float filledPitch);
            FreeCafeSoundDynamics.Pour(100, 100, out float clampedVolume, out float clampedPitch);
            Assert.That(highVolume, Is.GreaterThan(lowVolume));
            Assert.That(highPitch, Is.GreaterThan(lowPitch));
            Assert.That(filledVolume, Is.EqualTo(highVolume));
            Assert.That(filledPitch, Is.GreaterThan(highPitch));
            Assert.That(clampedVolume, Is.EqualTo(filledVolume));
            Assert.That(clampedPitch, Is.EqualTo(filledPitch));
        }

        [Test] public void HeatingRespondsToProgressAndTemperature()
        {
            FreeCafeSoundDynamics.Heating(0, 0, out float startVolume, out float startPitch);
            FreeCafeSoundDynamics.Heating(1, 0, out float progressVolume, out float progressPitch);
            FreeCafeSoundDynamics.Heating(0, 1, out float hotVolume, out float hotPitch);
            Assert.That(progressVolume, Is.GreaterThan(startVolume));
            Assert.That(progressPitch, Is.GreaterThan(startPitch));
            Assert.That(hotVolume, Is.EqualTo(progressVolume));
            Assert.That(hotPitch, Is.EqualTo(progressPitch));
        }

        [Test] public void InfiniteWaterOutflowIsNotItsUnchangingVolumeDelta()
        {
            var source = world.Find("s.water");
            float before = world.LiquidVolume(source);
            float flow = FreeCafeSoundDynamics.Outflow(world, source, 1500);
            Assert.That(world.PourLiquid(source.id, null, 1500, 1).success, Is.True);
            Assert.That(world.LiquidVolume(source), Is.EqualTo(before));
            Assert.That(flow, Is.EqualTo(1500));
        }

        [Test] public void FinalFinitePourMeasuresWasteEvenWhenProductionStateIsReplaced()
        {
            var vessel = world.Find("m.heater");
            world.PourLiquid("s.water", vessel.id, 20, 1);
            var prior = vessel.production;
            float flow = FreeCafeSoundDynamics.Outflow(world, vessel, 100);
            Assert.That(world.PourLiquid(vessel.id, null, 100, 1).success, Is.True);
            Assert.That(flow, Is.EqualTo(20));
            Assert.That(vessel.production, Is.Not.SameAs(prior));
            Assert.That(world.LiquidVolume(vessel), Is.Zero);
        }

        [Test] public void PrecisionPourUsesTheFilterForFillAndAcceptedVolume()
        {
            var jug = world.Find("v.extractor");
            var filter = world.ExtractionFilter(jug);
            Assert.That(filter, Is.Not.Null);
            Assert.That(FreeCafeSoundDynamics.PourReceiver(world, jug), Is.SameAs(filter));
            float before = world.LiquidVolume(filter);
            Assert.That(world.PourLiquid("s.water", jug.id, 20, 1).success, Is.True);
            Assert.That(world.LiquidVolume(filter) - before, Is.EqualTo(20).Within(.001f));
            Assert.That(FreeCafeSoundDynamics.Fill(world, filter), Is.GreaterThan(0));
        }

        [Test] public void OverflowWasteIsOutflowMinusActualReceiverGain()
        {
            var vessel = world.Find("m.heater");
            float capacity = world.LiquidCapacity(vessel);
            world.PourLiquid("s.water", vessel.id, capacity - 10, 1);
            float before = world.LiquidVolume(vessel);
            float flow = FreeCafeSoundDynamics.Outflow(world, world.Find("s.water"), 30);
            world.PourLiquid("s.water", vessel.id, 30, 1);
            float waste = flow - (world.LiquidVolume(vessel) - before);
            Assert.That(waste, Is.EqualTo(20).Within(.001f));
        }

        [Test] public void StoveLoopRequiresAttachedProcessingExternalVesselWithinDockTolerance()
        {
            var stove = world.Find("m.stove");
            var vessel = world.Find("m.heater");
            stove.production.heatingVesselId = vessel.id;
            vessel.area = stove.area;
            vessel.x = stove.x;
            vessel.y = stove.y + world.Spec(stove.specId).height * .45f + world.Spec(vessel.specId).height * .5f;
            vessel.production.phase = ProductionPhase.Processing;
            Assert.That(FreeCafeSoundDynamics.StoveVessel(world, stove), Is.SameAs(vessel));
            vessel.production.selfHeating = true;
            Assert.That(FreeCafeSoundDynamics.StoveVessel(world, stove), Is.Null);
            vessel.production.selfHeating = false; vessel.x += .81f;
            Assert.That(FreeCafeSoundDynamics.StoveVessel(world, stove), Is.Null);
            vessel.x = stove.x; vessel.y += .71f;
            Assert.That(FreeCafeSoundDynamics.StoveVessel(world, stove), Is.Null);
            vessel.y -= .71f; vessel.production.phase = ProductionPhase.Boiling;
            Assert.That(FreeCafeSoundDynamics.StoveVessel(world, stove), Is.Null);
        }

        [TestCase(ProductionPhase.Ready)]
        [TestCase(ProductionPhase.Room)]
        [TestCase(ProductionPhase.Boiling)]
        [TestCase(ProductionPhase.Warm)]
        public void CompletedBatchesIncludeAllThermalReadyBandsButNotReplacedStates(ProductionPhase phase)
        {
            var state = new ProductionEntityState { phase = phase };
            Assert.That(FreeCafeSoundDynamics.Finished(state, ProductionPhase.Processing, state), Is.True);
            Assert.That(FreeCafeSoundDynamics.Finished(new ProductionEntityState(), ProductionPhase.Processing, state), Is.False);
            Assert.That(FreeCafeSoundDynamics.Finished(state, phase, state), Is.False);
            state.remaining = 2;
            Assert.That(FreeCafeSoundDynamics.Finished(state, ProductionPhase.Processing, state), Is.False);
        }

        [TestCase("beans", "cafe.beans_add")]
        [TestCase("grounds", "cafe.powder_add")]
        [TestCase("sugar", "cafe.powder_add")]
        [TestCase("milk", "cafe.liquid_add")]
        public void DropsSelectMaterialSpecificCue(string specId, string cue)
            => Assert.That(FreeCafeSoundDynamics.AdditionKey(world, specId), Is.EqualTo(cue));

        [TestCase("beans", "cafe.beans_pickup")]
        [TestCase("cup.small", "cafe.cup_pickup")]
        [TestCase("grounds", "cafe.item_pickup")]
        public void PickupsSelectMaterialCue(string specId, string cue)
            => Assert.That(FreeCafeSoundDynamics.PickupKey(specId), Is.EqualTo(cue));
    }
}
