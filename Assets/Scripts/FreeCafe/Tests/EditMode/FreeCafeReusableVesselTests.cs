using System;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeReusableVesselTests
    {
        private CafeWorld world;
        [SetUp] public void Setup()
        {
            world=new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction();world.EnterProduction();world.StartDay();
        }
        [TestCase("m.brewer")]
        [TestCase("m.heater")]
        [TestCase("tool.milk-pitcher")]
        public void RepeatedDeliveryRetainsEmptyVesselAndCapturedHome(string id)
        {
            var vessel=world.Find(id);var home=Array.Find(world.Catalog.layout,e=>e.id==id);
            for(int i=0;i<3;i++)
            {
                if(world.State.requests.Count==0) {world.EndDay();world.StartDay();}
                Assert.That(world.PourLiquid("s.water", id, 200, 1).success,Is.True);
                vessel.latteArt=true;vessel.latteArtScore=88;vessel.latteArtPattern="heart";
                world.Find("m.stove").production.heatingVesselId=id;
                Assert.That(world.Interact(id,"service").success,Is.True);
                Assert.That(world.DeliverProductionCup().success,Is.True);
                Assert.That(world.Find(id),Is.SameAs(vessel));Assert.That(vessel.contents,Is.Empty);
                Assert.That(vessel.production.phase,Is.EqualTo(ProductionPhase.Empty));
                Assert.That(vessel.x,Is.EqualTo(home.x));Assert.That(vessel.y,Is.EqualTo(home.y));
                Assert.That(vessel.latteArt,Is.False);Assert.That(vessel.latteArtPattern,Is.Null);
                Assert.That(world.ServingCup,Is.Null);Assert.That(world.Find("m.stove").production.heatingVesselId,Is.Null);
                var saved=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
                Assert.That(world.Restore(saved).success,Is.True);vessel=world.Find(id);
            }
        }
        [TestCase("m.brewer",true)]
        [TestCase("m.heater",false)]
        [TestCase("tool.milk-pitcher",false)]
        public void DiscardClearsContentsRatherThanDeletingReusableEquipment(string id,bool onTray)
        {
            var vessel=world.Find(id);
            world.PourLiquid("s.water", id, 200, 1);
            if(onTray) {world.Interact(id,"service");Assert.That(world.DiscardProductionCup().success,Is.True);}
            else Assert.That(world.Interact(id,"waste").success,Is.True);
            Assert.That(world.Find(id),Is.SameAs(vessel));Assert.That(vessel.contents,Is.Empty);
            Assert.That(vessel.production.phase,Is.EqualTo(ProductionPhase.Empty));
        }
        [Test] public void RestoreRepairsPreviouslyDeliveredMissingStationVessels()
        {
            var saved=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            saved.entities.RemoveAll(e=>e.id=="m.brewer"||e.id=="m.heater");
            Assert.That(world.Restore(saved).success,Is.True);
            Assert.That(world.Find("m.brewer"),Is.Not.Null);Assert.That(world.Find("m.heater"),Is.Not.Null);
        }
    }
}
