using NUnit.Framework;
using UnityEngine;
namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeDomainTests
    {
        private CafeWorld world;
        [SetUp] public void Setup() => world=new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
        private WorldEntity Take(string source,float x=0,float y=-2)
        {
            Assert.That(world.Take(source,AreaId.Counter,x,y,out var item).success,Is.True);return item;
        }
        private void Grind()
        {
            for(int i=0;i<3;i++)
            {
                Assert.That(world.Operate("m.grinder").success,Is.True);
                if(i<2) world.Tick(.5f);
            }
        }
        private WorldEntity MakeCoffee()
        {
            var bean=Take("s.beans");Assert.That(world.Interact(bean.id,"m.grinder").success,Is.True);Grind();
            Assert.That(world.Release("m.grinder").success,Is.True);Assert.That(world.Interact(bean.id,"m.brewer").success,Is.True);world.Tick(6);world.Release("m.brewer");return bean;
        }
        [Test] public void TakeBeforeOpeningOrOrderConsumesExactlyOneStock()
        { int before=world.Find("s.beans").stock;var item=Take("s.beans");Assert.That(world.State.requests.Count,Is.Zero);Assert.That(world.State.open,Is.False);Assert.That(world.Find("s.beans").stock,Is.EqualTo(before-1));Assert.That(item.id,Is.Not.Empty); }
        [Test] public void AnyCoordinatesAndOverlappingPositionsAreAllowedWithoutSlots()
        { var a=Take("s.cups.large");var b=Take("s.beans");Assert.That(world.Move(a.id,AreaId.Counter,3.141f,-2.718f).success,Is.True);Assert.That(world.Move(b.id,AreaId.Counter,3.141f,-2.718f).success,Is.True);Assert.That(world.Find(a.id),Is.SameAs(a));Assert.That(a.x,Is.EqualTo(3.141f));Assert.That(b.x,Is.EqualTo(a.x)); }
        [Test] public void ItemsCanMoveAcrossAllFourAreasWithoutChangingIdentity()
        { var a=Take("s.cups.small");foreach(var area in new[]{AreaId.Cafe,AreaId.TopFloor,AreaId.Basement,AreaId.Counter}) { Assert.That(world.Move(a.id,area,1.25f,-1.75f).success,Is.True);Assert.That(world.Find(a.id),Is.SameAs(a));Assert.That(a.area,Is.EqualTo(area)); } }
        [Test] public void InvalidCoordinatesDoNotWithdrawOrAllocateIdentity()
        { int stock=world.Find("s.beans").stock,id=world.State.nextId;Assert.That(world.Take("s.beans",AreaId.Counter,100,100,out _).success,Is.False);Assert.That(world.Find("s.beans").stock,Is.EqualTo(stock));Assert.That(world.State.nextId,Is.EqualTo(id)); }
        [Test] public void NonFiniteCoordinatesAreRejected()
        { Assert.That(world.Take("s.beans",AreaId.Counter,float.NaN,0,out _).success,Is.False);Assert.That(world.Take("s.beans",AreaId.Counter,float.PositiveInfinity,0,out _).success,Is.False); }
        [Test] public void FailedSourceCombinationDoesNotWithdrawAnything()
        { int stock=world.Find("s.milk").stock,count=world.State.entities.Count;Assert.That(world.TakeAndInteract("s.milk","m.grinder",AreaId.Counter,0,0).success,Is.False);Assert.That(world.Find("s.milk").stock,Is.EqualTo(stock));Assert.That(world.State.entities.Count,Is.EqualTo(count)); }
        [Test] public void TwoMachinesOwnIndependentJobsAndKeepItemIdentity()
        { var bean=Take("s.beans");var milk=Take("s.milk");world.Interact(bean.id,"m.grinder");Grind();world.Interact(milk.id,"m.heater");world.Tick(3.1f);Assert.That(bean.specId,Is.EqualTo("grounds"));Assert.That(milk.specId,Is.EqualTo("milk"));Assert.That(world.Find("m.grinder").task.phase,Is.EqualTo(MachinePhase.Ready));Assert.That(world.Find("m.heater").task.phase,Is.EqualTo(MachinePhase.Running));world.Tick(1);Assert.That(milk.specId,Is.EqualTo("hotmilk"));Assert.That(world.Find(bean.id),Is.SameAs(bean)); }
        [Test] public void ReadyMachineCanPourItsOutputIntoACup()
        { var bean=Take("s.beans");var cup=Take("s.cups.large",2,-2);Assert.That(world.Interact(bean.id,"m.grinder").success,Is.True);Grind();Assert.That(world.Interact(cup.id,"m.grinder").success,Is.True);Assert.That(cup.contents.Count,Is.EqualTo(1));Assert.That(cup.contents[0].specId,Is.EqualTo("grounds"));Assert.That(world.Find(bean.id),Is.Null);Assert.That(world.Find("m.grinder").task,Is.Null); }
        [Test] public void BusyMaterialCannotMoveButCancelPreservesInput()
        { var bean=Take("s.beans");world.Interact(bean.id,"m.grinder");world.Tick(1);Assert.That(world.Move(bean.id,AreaId.Cafe,0,0).success,Is.False);Assert.That(world.Release("m.grinder").success,Is.False);Assert.That(world.Release("m.grinder",true).success,Is.True);Assert.That(bean.specId,Is.EqualTo("beans"));Assert.That(bean.ownerId,Is.Null);Assert.That(world.Find("m.grinder").task,Is.Null); }
        [Test] public void ReadyMachineMustReleaseBeforeNextInput()
        { var bean=Take("s.beans");var second=Take("s.beans");world.Interact(bean.id,"m.grinder");Grind();Assert.That(world.Interact(second.id,"m.grinder").success,Is.False);world.Release("m.grinder");Assert.That(world.Interact(second.id,"m.grinder").success,Is.True); }
        [Test] public void IngredientsCanBeAddedInArbitraryOrderBeforeAnyOrder()
        { var cup=Take("s.cups.large");var honey=Take("s.honey");var milk=Take("s.milk");Assert.That(world.Interact(honey.id,cup.id).success,Is.True);Assert.That(world.Interact(milk.id,cup.id).success,Is.True);Assert.That(world.TotalUnits(cup),Is.EqualTo(2));Assert.That(world.State.requests.Count,Is.Zero); }
        [Test] public void CapacityFailureDoesNotConsumeThirdPortion()
        { var cup=Take("s.cups.small");world.Interact(Take("s.milk").id,cup.id);world.Interact(Take("s.honey").id,cup.id);var extra=Take("s.beans");Assert.That(world.Interact(extra.id,cup.id).success,Is.False);Assert.That(world.Find(extra.id),Is.SameAs(extra));Assert.That(world.TotalUnits(cup),Is.EqualTo(2)); }
        [Test] public void ProductEvaluationUsesCompositionNotOrderOfOperations()
        { var cup=Take("s.cups.large");cup.contents.Add(new Portion{specId="hotmilk",units=2});cup.contents.Add(new Portion{specId="coffee",units=1});Assert.That(world.Evaluate(cup,world.Product("latte")),Is.EqualTo(1));cup.contents.Reverse();Assert.That(world.Evaluate(cup,world.Product("latte")),Is.EqualTo(1)); }
        [Test] public void WrongDrinkCanBeDeliveredWithConsequences()
        { world.StartDay();var cup=Take("s.cups.small");world.Interact(Take("s.milk").id,cup.id);Assert.That(world.Move(cup.id,AreaId.Cafe,0,-1).success,Is.True);int rep=world.State.reputation;Assert.That(world.Interact(cup.id,"customer_0").success,Is.True);Assert.That(world.Find(cup.id),Is.Null);Assert.That(world.State.served,Is.EqualTo(1));Assert.That(world.State.reputation,Is.EqualTo(rep-1)); }
        [Test] public void CoffeeMadeBeforeOpeningCanFulfilNewCustomer()
        { var coffee=MakeCoffee();var cup=Take("s.cups.small");world.Interact(coffee.id,cup.id);world.StartDay();int balance=world.State.money;Assert.That(world.Interact(cup.id,"service").success,Is.True);Assert.That(world.State.money,Is.EqualTo(balance+24)); }
        [Test] public void MovingToAnotherAreaDoesNotPauseMachineSimulation()
        { var bean=Take("s.beans");world.Interact(bean.id,"m.grinder");Grind();world.SetArea(AreaId.Basement);world.Tick(1);Assert.That(bean.specId,Is.EqualTo("grounds"));Assert.That(bean.area,Is.EqualTo(AreaId.Counter)); }
        [Test] public void SourceRefillCostsMoneyAndRepeatedRefillCannotCreateMoney()
        { Take("s.beans");int money=world.State.money;Assert.That(world.Refill().success,Is.True);Assert.That(world.State.money,Is.EqualTo(money-12));Assert.That(world.Refill().success,Is.False);Assert.That(world.State.money,Is.EqualTo(money-12)); }
        [Test] public void DisposalConsumesItemWithoutRefund()
        { var item=Take("s.beans");int stock=world.Find("s.beans").stock;Assert.That(world.Interact(item.id,"waste").success,Is.True);Assert.That(world.Find(item.id),Is.Null);Assert.That(world.Find("s.beans").stock,Is.EqualTo(stock)); }
        [Test] public void SaveRoundtripPreservesMachineJobContentsCoordinatesAndOrders()
        { var bean=Take("s.beans",1.234f,-2.345f);world.Interact(bean.id,"m.grinder");world.Tick(1);world.StartDay();var snapshot=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));Assert.That(world.Restore(snapshot).success,Is.True);Assert.That(world.Find(bean.id).ownerId,Is.EqualTo("m.grinder"));Assert.That(world.Find("m.grinder").task.remaining,Is.EqualTo(3).Within(.001));Assert.That(world.State.requests.Count,Is.EqualTo(3));Grind();Assert.That(world.Find(bean.id).specId,Is.EqualTo("grounds")); }
        [Test] public void InvalidRestoreDoesNotReplaceLiveWorld()
        { var current=world.State;var candidate=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(current));candidate.entities.Add(candidate.entities[0]);Assert.That(world.Restore(candidate).success,Is.False);Assert.That(world.State,Is.SameAs(current)); }
        [Test] public void RestoreRejectsOrphanedOwnedItems()
        { var bean=Take("s.beans");var candidate=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));candidate.entities.Find(x=>x.id==bean.id).ownerId="missing_machine";Assert.That(world.Restore(candidate).success,Is.False); }
        [Test] public void RestoredAllocatorCannotReuseExistingId()
        { var bean=Take("s.beans");var snapshot=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));snapshot.nextId=1;Assert.That(world.Restore(snapshot).success,Is.True);Assert.That(Take("s.beans").id,Is.Not.EqualTo(bean.id)); }
    }
}
