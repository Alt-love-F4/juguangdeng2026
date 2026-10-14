using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeExtractionTimerTests
    {
        [TestCase(40,40)]
        [TestCase(100,50)]
        [TestCase(150,35)]
        [TestCase(150,50)]
        public void SecondPourEmptyStopFreezesClockButFurtherAcceptedWaterCanResume(float first,float second)
        {
            var world=new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction();world.EnterProduction();var jug=world.Find("v.extractor");var source=world.Find("m.heater");
            var grounds=new WorldEntity{id="timer.grounds",specId="grounds",units=1,area=AreaId.Counter};world.State.entities.Add(grounds);
            Assert.That(world.Interact(grounds.id,jug.id).success,Is.True);
            source.contents.Add(new Portion{specId="water",units=1,milliliters=first+second,temperature=LiquidTemperature.Boiling});source.production.phase=ProductionPhase.Room;
            world.PourLiquid(source.id,jug.id,first,first/25);world.Tick(first/25);world.FinishPour(jug.id);
            float firstTime=world.ExtractionSnapshot(jug).elapsedSeconds;world.Tick(5);
            Assert.That(world.ExtractionSnapshot(jug).elapsedSeconds,Is.EqualTo(firstTime));
            Assert.That(world.PourLiquid(source.id,jug.id,second,second/25).success,Is.True);world.Tick(second/25);world.FinishPour(jug.id);
            Assert.That(world.LiquidVolume(source),Is.Zero.Within(.001));
            var stopped=world.ExtractionSnapshot(jug);world.Tick(100);
            Assert.That(world.ExtractionSnapshot(jug).elapsedSeconds,Is.EqualTo(stopped.elapsedSeconds));
            var saved=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));Assert.That(world.Restore(saved).success,Is.True);world.Tick(100);
            Assert.That(world.ExtractionSnapshot(jug.id).elapsedSeconds,Is.EqualTo(stopped.elapsedSeconds));
            if(first+second<200)
            {
                source=world.Find(source.id);source.contents.Add(new Portion{specId="water",units=1,milliliters=10,temperature=LiquidTemperature.Boiling});source.production.phase=ProductionPhase.Room;
                Assert.That(world.PourLiquid(source.id,jug.id,10,.4f).success,Is.True);world.Tick(.4f);
                Assert.That(world.ExtractionSnapshot(jug.id).elapsedSeconds,Is.GreaterThan(stopped.elapsedSeconds));
                Assert.That(world.ExtractionSnapshot(jug.id).groundsGrams,Is.EqualTo(12));
            }
        }
    }
}
