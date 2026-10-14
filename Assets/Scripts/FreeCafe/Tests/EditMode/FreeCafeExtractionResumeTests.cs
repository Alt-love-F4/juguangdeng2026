using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeExtractionResumeTests
    {
        private CafeWorld world;
        private WorldEntity jug,source;
        [SetUp] public void Setup()
        {
            world=new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction();world.EnterProduction();jug=world.Find("v.extractor");source=world.Find("m.heater");
            var grounds=new WorldEntity{id="resume.grounds",specId="grounds",units=1,area=AreaId.Counter};
            world.State.entities.Add(grounds);Assert.That(world.Interact(grounds.id,jug.id).success,Is.True);
            source.contents.Add(new Portion{specId="water",units=1,milliliters=400,temperature=LiquidTemperature.Boiling});
            source.production.phase=ProductionPhase.Room;
        }
        [TestCase(50f)]
        [TestCase(150f)]
        [TestCase(185f)]
        public void StopAndResumeKeepsOriginalFilterDoseAndConsumesRemainingPowder(float first)
        {
            var filter=world.ExtractionFilter(jug);
            world.PourLiquid(source.id,jug.id,first,first/25);world.Tick(first/25);world.FinishPour(jug.id);
            world.Tick(.2f);
            Assert.That(world.ExtractionFilter(jug),Is.SameAs(filter),"Partial stop must not remove the still-usable filter");
            Assert.That(world.SolidMass(filter),Is.EqualTo(12*(1-first/200)).Within(.001));
            var elapsed=world.ExtractionSnapshot(jug).elapsedSeconds;
            world.Tick(.5f);Assert.That(world.ExtractionSnapshot(jug).elapsedSeconds,Is.EqualTo(elapsed).Within(.001));
            Assert.That(world.TemperatureCelsius(source),Is.EqualTo(100).Within(.001),"Pausing extraction does not cool the remaining supply");
            Assert.That(world.PourLiquid(source.id,jug.id,200-first,(200-first)/25).success,Is.True);
            Assert.That(world.ExtractionSnapshot(jug).inProgress,Is.True);
            Assert.That(world.ExtractionSnapshot(jug).groundsGrams,Is.EqualTo(12));
            world.Tick((200-first)/25);world.FinishPour(jug.id);world.Tick(.2f);
            Assert.That(world.ExtractionSnapshot(jug).injectedHotWaterMl,Is.EqualTo(200).Within(.001));
            Assert.That(world.SolidMass(filter),Is.Zero);Assert.That(world.ExtractionSnapshot(jug).completed,Is.True);
            Assert.That(world.ExtractionFilter(jug),Is.Null,"Only the exhausted filter may detach");
            float grams=0;foreach(var p in jug.contents)grams+=world.CoffeeGroundsGrams(p);
            Assert.That(grams,Is.EqualTo(12).Within(.001));
        }
        [Test] public void CompletedAndDetachedJugStillAcceptsExtraWaterAsDilution()
        {
            world.PourLiquid(source.id,jug.id,200,7.5f);world.Tick(7.5f);world.FinishPour(jug.id);world.Tick(.2f);
            Assert.That(world.ExtractionFilter(jug),Is.Null);
            Assert.That(world.PourLiquid(source.id,jug.id,50,2).success,Is.True);
            Assert.That(world.LiquidVolume(jug),Is.EqualTo(250).Within(.001));
            Assert.That(jug.contents.Find(p=>p.specId=="coffee").coffeeKind,Is.EqualTo("essence"));
            Assert.That(world.ExtractionSnapshot(jug).injectedHotWaterMl,Is.EqualTo(200).Within(.001),"No new extraction without a powder bed");
        }
        [Test] public void PartialCompletedBatchCanResumeAfterSaveRestore()
        {
            world.PourLiquid(source.id,jug.id,150,6);world.Tick(6);world.FinishPour(jug.id);world.Tick(.2f);
            float priorCelsius=world.TemperatureCelsius(jug);
            var saved=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(world.Restore(saved).success,Is.True);
            jug=world.Find(jug.id);source=world.Find(source.id);
            Assert.That(world.ExtractionFilter(jug),Is.Not.Null);
            Assert.That(world.TemperatureCelsius(jug),Is.EqualTo(priorCelsius).Within(.001));
            Assert.That(world.TemperatureCelsius(source),Is.EqualTo(100).Within(.001));
            Assert.That(world.PourLiquid(source.id,jug.id,50,2).success,Is.True);world.Tick(2);world.FinishPour(jug.id);
            Assert.That(world.ExtractionSnapshot(jug).completed,Is.True);Assert.That(world.SolidMass(world.Find("v.extractor.filter")),Is.Zero);
        }
        [Test] public void HeatedLiquidKeepsCelsiusAndPhaseAcrossLongTicks()
        {
            source.contents.Clear();var water=new WorldEntity{id="resume.water",specId="water",units=1,area=AreaId.Counter};world.State.entities.Add(water);
            Assert.That(world.Interact(water.id,source.id).success,Is.True);
            Assert.That(world.Interact(source.id,"m.stove").success,Is.True);
            Assert.That(world.OperateProduction("m.stove").success,Is.True);
            world.Tick(world.Catalog.production.heatSeconds);
            Assert.That(world.LiquidSnapshot(source).temperature,Is.EqualTo(LiquidTemperature.Boiling));
            Assert.That(world.LiquidSnapshot(source).temperatureCelsius,Is.EqualTo(100).Within(.001f));
            world.Tick(100);
            Assert.That(world.LiquidSnapshot(source).temperature,Is.EqualTo(LiquidTemperature.Boiling));
            Assert.That(world.LiquidSnapshot(source).temperatureCelsius,Is.EqualTo(100).Within(.001f));
            Assert.That(source.production.phase,Is.EqualTo(ProductionPhase.Boiling));
            Assert.That(source.production.remaining,Is.Zero);
            Assert.That(world.LiquidVolume(source),Is.EqualTo(200));
        }
    }
}
