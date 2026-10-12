using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeDeliveryTests
    {
        private CafeWorld world;
        [SetUp] public void Setup()
        {
            world=new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction();world.EnterProduction();
        }
        [Test] public void DraggedCupIsPlacedWithoutBeingConsumedOrDelivered()
        {
            world.Take("s.cups.small",AreaId.Counter,0,0,out var cup);
            cup.contents.Add(new Portion {specId="milk",milliliters=80,units=1});
            int count=world.State.entities.Count,served=world.State.served;
            Assert.That(world.Interact(cup.id,"service").success,Is.True);
            Assert.That(world.ServingCup,Is.SameAs(cup));
            Assert.That(cup.x,Is.EqualTo(world.Find("service").x));
            Assert.That(cup.y,Is.EqualTo(world.Find("service").y));
            Assert.That(world.LiquidVolume(cup),Is.EqualTo(80));
            Assert.That(world.State.entities.Count,Is.EqualTo(count));
            Assert.That(world.State.served,Is.EqualTo(served));
        }
        [Test] public void MovingCupOffTrayClearsDeliveryAndCanRedock()
        {
            world.SelectServingCup("s.cups.small");var cup=world.ServingCup;
            Assert.That(world.Move(cup.id,AreaId.Counter,0,0).success,Is.True);
            Assert.That(world.ServingCup,Is.Null);
            Assert.That(world.DeliverProductionCup().success,Is.False);
            Assert.That(world.Find(cup.id),Is.SameAs(cup));
            Assert.That(world.Interact(cup.id,"service").success,Is.True);
            Assert.That(world.ServingCup,Is.SameAs(cup));
        }
        [Test] public void OccupiedTrayRejectsSecondCupWithoutConsumingIt()
        {
            world.SelectServingCup("s.cups.small");var first=world.ServingCup;
            world.Take("s.cups.large",AreaId.Counter,0,0,out var second);
            Assert.That(world.Interact(second.id,"service").success,Is.False);
            Assert.That(world.ServingCup,Is.SameAs(first));
            Assert.That(world.Find(second.id),Is.SameAs(second));
        }
        [Test] public void EmptyTrayOrMissingOrderNeverConsumesInventory()
        {
            Assert.That(world.DeliverProductionCup().message,Does.Contain("没有杯子"));
            world.SelectServingCup("s.cups.small");var cup=world.ServingCup;
            Assert.That(world.DeliverProductionCup().success,Is.False);
            Assert.That(world.Find(cup.id),Is.SameAs(cup));
        }
        [Test] public void CupDockedOnStoveCannotBeDeliveredFromTheTray()
        {
            world.SelectServingCup("s.cups.small");var cup=world.ServingCup;
            world.Interact(cup.id,"m.stove");
            Assert.That(world.DeliverProductionCup().success,Is.False);
            Assert.That(world.Find(cup.id),Is.SameAs(cup));
        }
    }
}
