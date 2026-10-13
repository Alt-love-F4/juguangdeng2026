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
        [Test] public void OutputDragPreviewIsDetachedAndDoesNotReleaseOrDuplicateGrounds()
        {
            var bean=Take("s.beans");world.Interact(bean.id,"m.grinder");
            Assert.That(world.DragOutputPreview("m.grinder"),Is.Null);Grind();
            var task=world.Find("m.grinder").task;int count=world.State.entities.Count,stock=world.Find("s.beans").stock;
            var preview=world.DragOutputPreview("m.grinder");
            Assert.That(preview,Is.Not.SameAs(bean));Assert.That(preview.id,Is.EqualTo(bean.id));Assert.That(preview.ownerId,Is.Null);
            preview.x=5;Assert.That(bean.ownerId,Is.EqualTo("m.grinder"));Assert.That(world.Find("m.grinder").task,Is.SameAs(task));
            Assert.That(world.State.entities.Count,Is.EqualTo(count));Assert.That(world.Find("s.beans").stock,Is.EqualTo(stock));
        }
        [Test] public void DragOutputTransfersOriginalGroundsToBrewerExactlyOnce()
        {
            var bean=Take("s.beans");bean.quality=2;world.Interact(bean.id,"m.grinder");Grind();int stock=world.Find("s.beans").stock;
            Assert.That(world.TakeMachineOutput("m.grinder",bean.id,"m.brewer",AreaId.Counter,0,0).success,Is.True);
            Assert.That(world.Find("m.grinder").task,Is.Null);Assert.That(world.Find("m.brewer").task.inputId,Is.EqualTo(bean.id));
            Assert.That(bean.ownerId,Is.EqualTo("m.brewer"));Assert.That(bean.quality,Is.EqualTo(2));Assert.That(world.Find(bean.id),Is.SameAs(bean));
            Assert.That(world.TakeMachineOutput("m.grinder",bean.id,"m.brewer",AreaId.Counter,0,0).success,Is.False);
            Assert.That(world.Find("s.beans").stock,Is.EqualTo(stock));
        }
        [Test] public void DragOutputIntoCupStoresGroundsAndClearsMachine()
        {
            var bean=Take("s.beans");bean.quality=1;world.Interact(bean.id,"m.grinder");Grind();var cup=Take("s.cups.large");
            Assert.That(world.TakeMachineOutput("m.grinder",bean.id,cup.id,AreaId.Counter,0,-2).success,Is.True);
            Assert.That(cup.contents[0].specId,Is.EqualTo("grounds"));Assert.That(cup.contents[0].units,Is.EqualTo(1));
            Assert.That(cup.contents[0].quality,Is.EqualTo(1));Assert.That(world.Find(bean.id),Is.Null);Assert.That(world.Find("m.grinder").task,Is.Null);
        }
        [Test] public void RejectedOutputDropsLeaveReadyJobAndGroundsUntouched()
        {
            var bean=Take("s.beans");world.Interact(bean.id,"m.grinder");Grind();var job=world.Find("m.grinder").task;
            var cup=Take("s.cups.small");cup.contents.Add(new Portion{specId="milk",units=world.Spec(cup.specId).capacity});
            foreach(var target in new[]{cup.id,"m.heater","missing","m.grinder"})
                Assert.That(world.TakeMachineOutput("m.grinder",bean.id,target,AreaId.Counter,0,0).success,Is.False);
            Assert.That(world.TakeMachineOutput("m.grinder",bean.id,null,AreaId.Counter,100,100).success,Is.False);
            Assert.That(world.TakeMachineOutput("m.grinder",bean.id,null,AreaId.Cafe,0,0).success,Is.False);
            Assert.That(world.TakeMachineOutput("m.grinder","stale",null,AreaId.Counter,0,0).success,Is.False);
            Assert.That(bean.ownerId,Is.EqualTo("m.grinder"));Assert.That(world.Find("m.grinder").task,Is.SameAs(job));Assert.That(job.phase,Is.EqualTo(MachinePhase.Ready));
        }
        [Test] public void OutputCanBePlacedFreelyAndTheNextBatchUsesNewIdentity()
        {
            var bean=Take("s.beans");world.Interact(bean.id,"m.grinder");Grind();
            Assert.That(world.TakeMachineOutput("m.grinder",bean.id,null,AreaId.Counter,2,-2).success,Is.True);
            Assert.That(bean.ownerId,Is.Null);Assert.That(bean.x,Is.EqualTo(2));Assert.That(world.Find(bean.id),Is.SameAs(bean));
            var next=Take("s.beans");world.Interact(next.id,"m.grinder");Grind();
            Assert.That(world.TakeMachineOutput("m.grinder",bean.id,null,AreaId.Counter,0,0).success,Is.False);
            Assert.That(world.DragOutputPreview("m.grinder").id,Is.EqualTo(next.id));
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
        { var bean=Take("s.beans");var milk=Take("s.milk");world.Interact(bean.id,"m.grinder");Grind();world.Interact(milk.id,"m.heater");world.Tick(3.1f);Assert.That(bean.specId,Is.EqualTo("grounds"));Assert.That(milk.specId,Is.EqualTo("milk"));Assert.That(world.Find("m.grinder").task.phase,Is.EqualTo(MachinePhase.Ready));Assert.That(world.Find("m.heater").task.phase,Is.EqualTo(MachinePhase.Running));world.Tick(world.Operation("heat").seconds-3.1f);Assert.That(milk.specId,Is.EqualTo("hotmilk"));Assert.That(world.Find(bean.id),Is.SameAs(bean)); }
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
        { var coffee=MakeCoffee();var cup=Take("s.cups.small");world.Interact(coffee.id,cup.id);world.StartDay();int balance=world.State.money;Assert.That(world.Interact(cup.id,"service").success,Is.True);Assert.That(world.State.money,Is.EqualTo(balance+world.Product("espresso").price)); }
        [Test] public void MovingToAnotherAreaDoesNotPauseMachineSimulation()
        { var bean=Take("s.beans");world.Interact(bean.id,"m.grinder");Grind();world.SetArea(AreaId.Basement);world.Tick(1);Assert.That(bean.specId,Is.EqualTo("grounds"));Assert.That(bean.area,Is.EqualTo(AreaId.Counter)); }
        [Test] public void SourceRefillCostsMoneyAndRepeatedRefillCannotCreateMoney()
        { Take("s.beans");int money=world.State.money;Assert.That(world.Refill(0).success,Is.True);Assert.That(world.State.money,Is.EqualTo(money-world.Catalog.economy.beanUnitPrices[0]));Assert.That(world.Refill(0).success,Is.False);Assert.That(world.State.money,Is.EqualTo(money-world.Catalog.economy.beanUnitPrices[0])); }
        [Test] public void DisposalConsumesItemWithoutRefund()
        { var item=Take("s.beans");int stock=world.Find("s.beans").stock;Assert.That(world.Interact(item.id,"waste").success,Is.True);Assert.That(world.Find(item.id),Is.Null);Assert.That(world.Find("s.beans").stock,Is.EqualTo(stock)); }
        [Test] public void SaveRoundtripPreservesMachineJobContentsCoordinatesAndOrders()
        { var bean=Take("s.beans",1.234f,-2.345f);world.Interact(bean.id,"m.grinder");world.Tick(1);world.StartDay();var snapshot=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));Assert.That(world.Restore(snapshot).success,Is.True);Assert.That(world.Find(bean.id).ownerId,Is.EqualTo("m.grinder"));Assert.That(world.Find("m.grinder").task.remaining,Is.EqualTo(3).Within(.001));Assert.That(world.State.requests.Count,Is.EqualTo(3));Grind();Assert.That(world.Find(bean.id).specId,Is.EqualTo("grounds")); }
        [TestCase(false, "s.cups.small")]
        [TestCase(false, "s.cups.large")]
        [TestCase(true, "s.cups.small")]
        [TestCase(true, "s.cups.large")]
        public void EmptyCupReturnsToMatchingStackExactlyOnce(bool production, string sourceId)
        {
            if (production) world.EnableProduction();
            var source = world.Find(sourceId);
            int initialStock = source.stock;
            var cup = Take(sourceId);
            int nextId = world.State.nextId;
            string beforePreview = JsonUtility.ToJson(world.State);
            Assert.That(world.IsReceiver(source), Is.True);
            Assert.That(world.IsReceiver(world.Find("s.beans")), Is.False);
            Assert.That(world.Preview(cup.id, sourceId).success, Is.True);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(beforePreview));
            int notifications = 0;
            world.Changed += () =>
            {
                notifications++;
                Assert.That(world.Find(cup.id), Is.Null);
                Assert.That(source.stock, Is.EqualTo(initialStock));
            };
            Assert.That(world.Interact(cup.id, sourceId).success, Is.True);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(world.State.nextId, Is.EqualTo(nextId));
            beforePreview = JsonUtility.ToJson(world.State);
            Assert.That(world.Interact(cup.id, sourceId).success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(beforePreview));
            Assert.That(notifications, Is.EqualTo(1));
        }

        [TestCase(false, "s.cups.small", "s.cups.large")]
        [TestCase(false, "s.cups.large", "s.cups.small")]
        [TestCase(true, "s.cups.small", "s.cups.large")]
        [TestCase(true, "s.cups.large", "s.cups.small")]
        public void MismatchedCupReturnDoesNotMutateEitherStockOrIdentity(bool production, string sourceId, string wrongSourceId)
        {
            if (production) world.EnableProduction();
            var cup = Take(sourceId);
            // Leave room in the wrong stack so rejection proves cup-type matching, not capacity.
            Take(wrongSourceId);
            string before = JsonUtility.ToJson(world.State);
            var preview = world.Preview(cup.id, wrongSourceId);
            var result = world.Interact(cup.id, wrongSourceId);
            Assert.That(preview.success, Is.False);
            Assert.That(result.message, Does.Contain("杯型不匹配"));
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            Assert.That(world.Find(cup.id), Is.SameAs(cup));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullCupStackRejectsReturnWithoutMutation(bool production)
        {
            if (production) world.EnableProduction();
            var cup = Take("s.cups.small");
            var source = world.Find("s.cups.small");
            source.stock = world.Spec(source.specId).initialStock; // Refilled after withdrawal.
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.Preview(cup.id, source.id).success, Is.False);
            var result = world.Interact(cup.id, source.id);
            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("杯堆已满"));
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            Assert.That(world.Find(cup.id), Is.SameAs(cup));
        }

        [TestCase(false, "s.milk")]
        [TestCase(false, "s.beans")]
        [TestCase(true, "s.milk")]
        [TestCase(true, "s.beans")]
        public void CupWithLiquidOrSolidsCannotReturnAndLoseContents(bool production, string ingredientSource)
        {
            if (production) world.EnableProduction();
            var cup = Take("s.cups.large");
            Assert.That(world.Interact(Take(ingredientSource).id, cup.id).success, Is.True);
            var portion = cup.contents[0];
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.Preview(cup.id, "s.cups.large").success, Is.False);
            var result = world.Interact(cup.id, "s.cups.large");
            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("杯子还有内容"));
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            Assert.That(cup.contents[0], Is.SameAs(portion));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OwnedCupAndCrossAreaCupCannotReturn(bool production)
        {
            if (production) world.EnableProduction();
            var cup = Take("s.cups.small");
            cup.ownerId = "m.grinder";
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.Interact(cup.id, "s.cups.small").success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            cup.ownerId = null;
            Assert.That(world.Move(cup.id, AreaId.Cafe, 0, 0).success, Is.True);
            before = JsonUtility.ToJson(world.State);
            Assert.That(world.Interact(cup.id, "s.cups.small").success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProcessingEmptyCupCannotReturn(bool production)
        {
            if (production) world.EnableProduction();
            var cup = Take("s.cups.small");
            cup.production.phase = ProductionPhase.Processing;
            cup.production.remaining = 2;
            string before = JsonUtility.ToJson(world.State);
            var result = world.Interact(cup.id, "s.cups.small");
            Assert.That(result.success, Is.False);
            Assert.That(result.message, Does.Contain("正在加工"));
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }

        [Test] public void InvalidRestoreDoesNotReplaceLiveWorld()
        { var current=world.State;var candidate=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(current));candidate.entities.Add(candidate.entities[0]);Assert.That(world.Restore(candidate).success,Is.False);Assert.That(world.State,Is.SameAs(current)); }
        [Test] public void RestoreRejectsOrphanedOwnedItems()
        { var bean=Take("s.beans");var candidate=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));candidate.entities.Find(x=>x.id==bean.id).ownerId="missing_machine";Assert.That(world.Restore(candidate).success,Is.False); }
        [Test] public void RestoredAllocatorCannotReuseExistingId()
        { var bean=Take("s.beans");var snapshot=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));snapshot.nextId=1;Assert.That(world.Restore(snapshot).success,Is.True);Assert.That(Take("s.beans").id,Is.Not.EqualTo(bean.id)); }
    }
}
