using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeProductionTests
    {
        private CafeWorld world;
        [SetUp] public void Setup()
        {
            world=new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction(); world.EnterProduction();
        }
        private void Load(string source,string machine) => Assert.That(world.TakeAndInteract(source,machine,AreaId.Counter,0,0).success,Is.True);
        private void Heat(string source="s.water",string machine="m.heater") { Load(source,machine);world.Interact(machine,"m.stove");world.OperateProduction("m.stove");world.Tick(4); }
        private void Grounds()
        {
            Load("s.beans","m.grinder");world.OperateProduction("m.grinder");world.Tick(12);
            var preview=world.DragOutputPreview("m.grinder");
            Assert.That(world.TakeMachineOutput("m.grinder",preview.id,"m.brewer",AreaId.Counter,-3.3f,.2f).success,Is.True);
        }
        private CafeSnapshot Copy() => JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
        [Test] public void LiquidSnapshotIncludesActualIngredientVolumesWithoutChangingInventory()
        {
            world.SelectServingCup("s.cups.large");
            var cup = world.ServingCup;
            cup.contents.Add(new Portion { specId="coffee", milliliters=100, units=1 });
            cup.contents.Add(new Portion { specId="hotmilk", milliliters=200, units=1 });
            cup.contents.Add(new Portion { specId="hotwater", milliliters=50, units=1 });
            cup.contents.Add(new Portion { specId="sugar", units=1 });
            string before = JsonUtility.ToJson(world.State);
            var snapshot = world.LiquidSnapshot(cup);
            Assert.That(snapshot.volumeMl, Is.EqualTo(350));
            Assert.That(snapshot.capacityMl, Is.EqualTo(400));
            Assert.That(snapshot.composition.coffeeMl, Is.EqualTo(100));
            Assert.That(snapshot.composition.milkMl, Is.EqualTo(200));
            Assert.That(snapshot.composition.waterMl, Is.EqualTo(50));
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }

        [Test] public void LiquidSnapshotAggregatesMatchaCocoaAndDuplicatePortions()
        {
            world.SelectServingCup("s.cups.large");
            var cup = world.ServingCup;
            cup.contents.Add(new Portion { specId="matcha.drink", milliliters=60 });
            cup.contents.Add(new Portion { specId="matcha.drink", milliliters=40 });
            cup.contents.Add(new Portion { specId="cocoa.drink", milliliters=80 });
            cup.contents.Add(new Portion { specId="milk", milliliters=120 });
            var snapshot = world.LiquidSnapshot(cup);
            Assert.That(snapshot.composition.matchaMl, Is.EqualTo(100));
            Assert.That(snapshot.composition.cocoaMl, Is.EqualTo(80));
            Assert.That(snapshot.composition.milkMl, Is.EqualTo(120));
            Assert.That(snapshot.composition.TotalMl, Is.EqualTo(snapshot.volumeMl));
            Assert.That(world.LiquidSnapshot(null).volumeMl, Is.Zero);
        }

        [Test] public void LegacyLiquidSnapshotMapsUnitsToCupCapacityWithoutMigratingSave()
        {
            var legacy = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            var cup = new WorldEntity { specId="cup.small" };
            cup.contents.Add(new Portion { specId="coffee", units=1 });
            var snapshot = legacy.LiquidSnapshot(cup);
            Assert.That(snapshot.volumeMl / snapshot.capacityMl, Is.EqualTo(0.5f));
            Assert.That(snapshot.composition.coffeeMl, Is.EqualTo(120));
            Assert.That(cup.contents[0].milliliters, Is.Zero);
        }
        private WorldEntity PerfectCup(string productId)
        {
            var product=world.Product(productId);world.SelectServingCup(product.cupSpecId=="cup.small" ? "s.cups.small" : "s.cups.large");
            var cup=world.ServingCup;int seq=0;
            foreach(var need in product.ingredients) cup.contents.Add(new Portion { specId=need.specId,units=need.units,
                milliliters=need.milliliters,productionScore=100,temperature=product.requiredTemperature,addedSequence=++seq });
            cup.production.additionSequence=seq; return cup;
        }
        [Test] public void CupSwapReturnsOnlyEmptyCupAndRefusesNonemptyCup()
        {
            int small=world.Find("s.cups.small").stock;
            world.SelectServingCup("s.cups.small");var old=world.ServingCup;
            Assert.That(world.SelectServingCup("s.cups.large").success,Is.True);
            Assert.That(world.Find(old.id),Is.Null);Assert.That(world.Find("s.cups.small").stock,Is.EqualTo(small));
            Load("s.honey",world.ServingCup.id);int large=world.Find("s.cups.large").stock;
            Assert.That(world.SelectServingCup("s.cups.small").success,Is.False);Assert.That(world.Find("s.cups.large").stock,Is.EqualTo(large));
        }
        [Test] public void FailedSourceDropIsFullyTransactional()
        {
            int stock=world.Find("s.milk").stock,count=world.State.entities.Count;
            Assert.That(world.TakeAndInteract("s.milk","m.grinder",AreaId.Counter,0,0).success,Is.False);
            Assert.That(world.Find("s.milk").stock,Is.EqualTo(stock));Assert.That(world.State.entities.Count,Is.EqualTo(count));
            Assert.That(world.Find("m.grinder").production.phase,Is.EqualTo(ProductionPhase.Empty));
        }
        [Test] public void GrinderWaitsForClickThenAutomaticallyFinishesAfterTwelveSeconds()
        {
            Load("s.beans","m.grinder");world.Tick(20);
            Assert.That(world.Find("m.grinder").production.phase,Is.EqualTo(ProductionPhase.Loaded));
            world.OperateProduction("m.grinder");world.Tick(11.9f);
            Assert.That(world.Find("m.grinder").production.phase,Is.EqualTo(ProductionPhase.Processing));
            world.Tick(.1f);Assert.That(world.Find("m.grinder").production.phase,Is.EqualTo(ProductionPhase.Ready));
        }
        [Test] public void GrinderClicksAdvanceTimeAndCannotSpamSameFrame()
        {
            Load("s.beans","m.grinder");world.OperateProduction("m.grinder");world.OperateProduction("m.grinder");
            Assert.That(world.Find("m.grinder").production.remaining,Is.EqualTo(11));
            Assert.That(world.OperateProduction("m.grinder").success,Is.False);
            world.Tick(.12f);world.OperateProduction("m.grinder");Assert.That(world.Find("m.grinder").production.remaining,Is.EqualTo(9.88f).Within(.01f));
        }
        [Test] public void GrinderOutputRejectsWrongTargetWithoutChangingJob()
        {
            Load("s.beans","m.grinder");world.OperateProduction("m.grinder");world.Tick(12);
            var output=world.DragOutputPreview("m.grinder");
            Assert.That(world.TakeMachineOutput("m.grinder",output.id,"m.mixer",AreaId.Counter,0,0).success,Is.False);
            Assert.That(world.Find("m.grinder").contents[0].specId,Is.EqualTo("grounds"));
        }
        [Test] public void HeaterBoilsFiveSecondsThenWarmsTenThenCanReheat()
        {
            Heat("s.milk");var kettle=world.Find("m.heater");
            Assert.That(kettle.production.phase,Is.EqualTo(ProductionPhase.Boiling));world.Tick(5);
            Assert.That(kettle.production.phase,Is.EqualTo(ProductionPhase.Warm));world.Tick(10);
            Assert.That(kettle.production.phase,Is.EqualTo(ProductionPhase.Room));Assert.That(world.OperateProduction("m.stove").success,Is.True);
            world.Tick(4);Assert.That(kettle.production.phase,Is.EqualTo(ProductionPhase.Boiling));
        }
        [Test] public void LongTickConsumesAllThermalPhasesWithoutLosingElapsedTime()
        {
            Load("s.water","m.heater");world.Interact("m.heater","m.stove");world.OperateProduction("m.stove");world.Tick(19);
            Assert.That(world.Find("m.heater").production.phase,Is.EqualTo(ProductionPhase.Room));
        }
        [Test] public void ParallelStovesOwnIndependentContentsAndTimers()
        {
            world.State.staff=2;world.State.money=1000;world.AddMachine("machine.heater");
            var extra=world.State.entities.Find(e=>e.specId=="machine.heater" && e.id!="m.heater");
            var stove=new WorldEntity { id="test.stove",specId="machine.stove",area=AreaId.Counter,x=5,y=-2.5f };
            world.State.entities.Add(stove);
            Load("s.milk","m.heater");Load("s.water",extra.id);Load("s.cocoa",extra.id);
            world.Interact("m.heater","m.stove");world.OperateProduction("m.stove");world.Tick(2);
            world.Interact(extra.id,stove.id);world.OperateProduction(stove.id);world.Tick(2);
            Assert.That(world.Find("m.heater").production.phase,Is.EqualTo(ProductionPhase.Boiling));
            Assert.That(extra.production.phase,Is.EqualTo(ProductionPhase.Processing));Assert.That(extra.contents.Exists(portion=>portion.specId=="cocoa.drink"),Is.True);
        }
        [Test] public void BrewingAcceptsWaterBeforeGroundsAndDoesNotRequireHeat()
        {
            Load("s.water","m.heater");
            Assert.That(world.PourLiquid("m.heater","m.brewer",20,1).success,Is.True);
            Grounds();
            Assert.That(world.ContainerDescription(world.Find("m.brewer")),Does.Contain("冷萃"));
            Assert.That(world.CanPour(world.Find("m.brewer")),Is.True);
        }
        [Test] public void UnderfilledBrewCanResumeWithoutTimingOrVolumeGate()
        {
            Grounds();Load("s.water","m.heater");world.PourLiquid("m.heater","m.brewer",100,1);
            Assert.That(world.FinishPour("m.brewer").success,Is.True);
            Assert.That(world.LiquidVolume(world.Find("m.brewer")),Is.EqualTo(100));
            world.PourLiquid("m.heater","m.brewer",100,1);
            Assert.That(world.FinishPour("m.brewer").success,Is.True);
            var brewer=world.Find("m.brewer");Assert.That(brewer.production.phase,Is.EqualTo(ProductionPhase.Room));
            Assert.That(brewer.contents.Exists(portion=>portion.specId=="coffee"),Is.True);
            Assert.That(brewer.contents.Exists(portion=>portion.specId=="grounds"),Is.True);
            Assert.That(world.LiquidVolume(world.Find("m.heater")),Is.EqualTo(0));
        }
        [Test] public void FluidConservationIncludesOverflowAndGroundSpills()
        {
            Heat("s.milk");world.SelectServingCup("s.cups.small");
            var cup=world.ServingCup;cup.contents.Add(new Portion{specId="water",units=1,milliliters=230,productionScore=100});
            world.PourLiquid("m.heater",cup.id,30,1);
            Assert.That(world.LiquidVolume(cup),Is.EqualTo(240));Assert.That(world.LiquidVolume(world.Find("m.heater")),Is.EqualTo(170));
            Assert.That(world.Find("m.heater").production.spilledMl,Is.EqualTo(20));
            world.PourLiquid("m.heater",null,20,1);Assert.That(world.LiquidVolume(world.Find("m.heater")),Is.EqualTo(150));
            Assert.That(world.Find("m.heater").production.spilledMl,Is.EqualTo(40));
        }
        [Test] public void InvalidPourAmountsDoNotChangeLiquids()
        {
            Heat();foreach(var amount in new[]{float.NaN,float.PositiveInfinity,-1,0}) Assert.That(world.PourLiquid("m.heater",null,amount,1).success,Is.False);
            Assert.That(world.LiquidVolume(world.Find("m.heater")),Is.EqualTo(200));
        }
        [Test] public void EveryConfiguredPerfectRecipeScoresHundredAndEmptyCupScoresZero()
        {
            foreach(var product in world.Catalog.products)
            {
                var cup=PerfectCup(product.id);var score=world.EvaluateProduction(cup,product);
                Assert.That(score.total,Is.EqualTo(100).Within(.1),product.id);Assert.That(score.grade,Is.EqualTo("匠心佳作"));
                world.DiscardProductionCup();world.SelectServingCup(product.cupSpecId=="cup.small"?"s.cups.small":"s.cups.large");
                Assert.That(world.EvaluateProduction(world.ServingCup,product).total,Is.EqualTo(0));world.DiscardProductionCup();
            }
        }
        [Test] public void WrongTemperatureLowersScoreWithoutAdditionOrderGate()
        {
            var cup=PerfectCup("latte");var product=world.Product("latte");float score=world.EvaluateProduction(cup,product).total;
            cup.contents[1].temperature=LiquidTemperature.Room;cup.contents[0].addedSequence=3;
            Assert.That(world.EvaluateProduction(cup,product).total,Is.LessThan(score));
        }
        [Test] public void EmptyCupCanBeDeliveredForRemakeWithoutRefundOrRemovingOrder()
        {
            world.StartDay();world.SelectServingCup("s.cups.small");var request=world.Request(world.State.focusedRequestId);int stock=world.Find("s.cups.small").stock;
            Assert.That(world.DeliverProductionCup().success,Is.True);Assert.That(world.ServingCup,Is.Null);
            Assert.That(world.Request(request.id),Is.SameAs(request));Assert.That(request.remakes,Is.EqualTo(1));Assert.That(world.Find("s.cups.small").stock,Is.EqualTo(stock));
            world.SelectServingCup("s.cups.small");world.DeliverProductionCup();Assert.That(world.Request(request.id),Is.Null);
        }
        [Test] public void PagingChangesActualDeliveryTargetAndSuccessfulOrderIsRemovedOnlyOnce()
        {
            world.StartDay();var first=world.Request(world.State.focusedRequestId);world.PageProductionOrder(1);var selected=world.Request(world.State.focusedRequestId);
            Assert.That(selected,Is.Not.SameAs(first));PerfectCup(selected.productId);int money=world.State.money;
            Assert.That(world.DeliverProductionCup().success,Is.True);Assert.That(world.Request(selected.id),Is.Null);Assert.That(world.Request(first.id),Is.Not.Null);
            Assert.That(world.State.money,Is.GreaterThan(money));Assert.That(world.DeliverProductionCup().success,Is.False);
        }
        [Test] public void DiscardConsumesCupAndResourcesWithoutRefund()
        {
            world.SelectServingCup("s.cups.small");Load("s.honey",world.ServingCup.id);int cups=world.Find("s.cups.small").stock,honey=world.Find("s.honey").stock;
            world.DiscardProductionCup();Assert.That(world.ServingCup,Is.Null);Assert.That(world.Find("s.cups.small").stock,Is.EqualTo(cups));Assert.That(world.Find("s.honey").stock,Is.EqualTo(honey));
        }
        [Test] public void MiniGameStartsWithAnyServingCupAndRejectsStaleOrDuplicateCallbacks()
        {
            world.SelectServingCup("s.cups.large");Assert.That(world.RequestProductionMiniGame().success,Is.True);
            var cup=world.ServingCup;string token=world.State.production.miniGameToken;
            Assert.That(world.RequestProductionMiniGame().success,Is.False);Assert.That(world.ResolveProductionMiniGame("stale",true,100).success,Is.False);
            Assert.That(world.ResolveProductionMiniGame(token,true,100).success,Is.True);Assert.That(cup.latteArt,Is.True);
            Assert.That(world.ResolveProductionMiniGame(token,true,100).success,Is.False);
        }
        [Test] public void MiniGameCancellationPreservesContentsAndDiscardInvalidatesToken()
        {
            var cup=PerfectCup("latte");world.RequestProductionMiniGame();var token=world.State.production.miniGameToken;
            world.ResolveProductionMiniGame(token,false,0);Assert.That(cup.contents.Count,Is.EqualTo(2));Assert.That(cup.latteArt,Is.False);
            world.RequestProductionMiniGame();token=world.State.production.miniGameToken;world.DiscardProductionCup();
            Assert.That(world.ResolveProductionMiniGame(token,true,100).success,Is.False);
        }
        [Test] public void VersionThreeRoundTripRestoresLiquidsThermalTimersAndCupIdentity()
        {
            Heat("s.milk");world.Tick(2);world.SelectServingCup("s.cups.large");world.PourLiquid("m.heater",world.ServingCup.id,40,1);
            var state=Copy();var restored=new CafeWorld(world.Catalog);
            Assert.That(restored.Restore(state).success,Is.True);Assert.That(restored.ProductionEnabled,Is.True);
            Assert.That(restored.LiquidVolume(restored.ServingCup),Is.EqualTo(40));Assert.That(restored.Find("m.heater").production.remaining,Is.EqualTo(3));
        }
        [Test] public void CorruptFluidSnapshotIsRejectedAtomically()
        {
            Heat();var state=Copy();state.entities.Find(e=>e.id=="m.heater").contents[0].milliliters=float.NaN;var original=world.State;
            Assert.That(world.Restore(state).success,Is.False);Assert.That(world.State,Is.SameAs(original));Assert.That(world.LiquidVolume(world.Find("m.heater")),Is.EqualTo(200));
        }
        
        [Test] public void LegacyMigrationPreservesFilledRatioAndAddsOnlyNewRequiredSources()
        {
            var legacy=new CafeWorld(world.Catalog);
            legacy.Take("s.cups.large",AreaId.Counter,0,-2,out var cup);
            cup.contents.Add(new Portion{specId="coffee",units=2,quality=1});cup.contents.Add(new Portion{specId="hotmilk",units=4});
            legacy.State.entities.RemoveAll(e=>e.id=="s.water" || e.id=="s.sugar" || e.id=="tool.latte-art");
            legacy.EnableProduction();Assert.That(legacy.LiquidVolume(cup),Is.EqualTo(400).Within(.01));
            Assert.That(legacy.Find("s.water"),Is.Not.Null);Assert.That(legacy.Find(cup.id),Is.SameAs(cup));
            Assert.That(legacy.Restore(JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(legacy.State))).success,Is.True);
        }
        [Test] public void CorruptThermalPhaseCannotTurnAGrinderIntoAKettle()
        {
            Load("s.beans","m.grinder");var state=Copy();state.entities.Find(e=>e.id=="m.grinder").production.phase=ProductionPhase.Boiling;
            Assert.That(world.Restore(state).success,Is.False);
        }
        [Test] public void MiniGameActiveCupRefusesIngredientsAndPouring()
        {
            var cup=PerfectCup("latte");world.RequestProductionMiniGame();int stock=world.Find("s.honey").stock;
            Assert.That(world.TakeAndInteract("s.honey",cup.id,AreaId.Counter,0,0).success,Is.False);
            Assert.That(world.Find("s.honey").stock,Is.EqualTo(stock));
        }

        [Test] public void ReturnUsesActualEntryAreaWithoutResettingMachine()
        {
            world.SetArea(AreaId.TopFloor);world.EnterProduction();Load("s.beans","m.grinder");world.OperateProduction("m.grinder");world.LeaveProduction();
            Assert.That(world.State.currentArea,Is.EqualTo(AreaId.TopFloor));world.Tick(12);Assert.That(world.Find("m.grinder").production.phase,Is.EqualTo(ProductionPhase.Ready));
        }
    }
}
