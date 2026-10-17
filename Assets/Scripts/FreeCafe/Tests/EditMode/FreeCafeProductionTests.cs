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
        private void Load(string source,string machine) => Assert.That((source == "s.water" ? world.PourLiquid(source,machine,200,1) : world.TakeAndInteract(source,machine,AreaId.Counter,0,0)).success,Is.True);
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
        private WorldEntity PerfectCup(string productId, CustomerRequest request = null)
        {
            var product=world.Product(productId);
            string cupSpecId=request==null ? product.cupSpecId : world.RequiredCupSpecId(request);
            float scale=request==null ? 1 : world.OrderPortionScale(request);
            world.SelectServingCup(cupSpecId=="cup.small" ? "s.cups.small" : "s.cups.large");
            var cup=world.ServingCup;int seq=0;
            Assert.That(product.evaluation, Is.Not.Null, productId);
            foreach(var need in product.evaluation.amounts) cup.contents.Add(new Portion { specId=need.ingredientId,units=1,
                milliliters=need.idealMl*scale,grams=need.idealGrams*scale,coffeeKind=need.coffeeKind,
                productionScore=100,temperature=product.requiredTemperature,addedSequence=++seq });
            cup.production.additionSequence=seq;
            cup.latteArt=System.Array.Exists(product.evaluation.amounts, need=>need.ingredientId=="milkfoam");
            return cup;
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
        [TestCase("s.cups.small")]
        [TestCase("s.cups.large")]
        public void ReturningServingCupClearsSelectionAndRestoresStockExactlyOnce(string sourceId)
        {
            int stock = world.Find(sourceId).stock;
            Assert.That(world.SelectServingCup(sourceId).success, Is.True);
            var cup = world.ServingCup;
            int notifications = 0;
            world.Changed += () =>
            {
                notifications++;
                Assert.That(world.ServingCup, Is.Null);
                Assert.That(world.State.production.activeCupId, Is.Null);
                Assert.That(world.Find(cup.id), Is.Null);
                Assert.That(world.Find(sourceId).stock, Is.EqualTo(stock));
            };
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.Preview(cup.id, sourceId).success, Is.True);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            Assert.That(world.Interact(cup.id, sourceId).success, Is.True);
            Assert.That(notifications, Is.EqualTo(1));
            before = JsonUtility.ToJson(world.State);
            Assert.That(world.Interact(cup.id, sourceId).success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test] public void ServingCupRejectedReturnKeepsSelectionContentsAndBothStocks()
        {
            world.SelectServingCup("s.cups.large");
            var cup = world.ServingCup;
            var source = world.Find("s.cups.large");
            world.Take("s.cups.small", AreaId.Counter, 0, -2, out _);
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.Interact(cup.id, "s.cups.small").success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            source.stock = world.Spec(source.specId).initialStock;
            Load("s.water", cup.id);
            before = JsonUtility.ToJson(world.State);
            Assert.That(world.Interact(cup.id, source.id).success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            Assert.That(world.ServingCup, Is.SameAs(cup));
        }

        [Test] public void MiniGameCupReturnAndSwapFailWithoutMutation()
        {
            world.SelectServingCup("s.cups.small");
            var cup = world.ServingCup;
            Assert.That(world.RequestProductionMiniGame().success, Is.True);
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.Interact(cup.id, "s.cups.small").success, Is.False);
            Assert.That(world.SelectServingCup("s.cups.large").success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            Assert.That(world.ServingCup, Is.SameAs(cup));
        }

        [Test] public void FullOldStackAllowsCupSwapAndPreservesAllEmptyCups()
        {
            world.SelectServingCup("s.cups.small"); var cup = world.ServingCup;
            var source = world.Find("s.cups.small"); source.stock = world.Spec(source.specId).initialStock;
            Assert.That(world.SelectServingCup("s.cups.large").success, Is.True);
            Assert.That(source.stock, Is.EqualTo(world.Spec(source.specId).initialStock + 1));
            Assert.That(world.Find(cup.id), Is.Null);
            Assert.That(world.ServingCup.specId, Is.EqualTo("cup.large"));
        }

        [Test] public void ProcessingOldCupPreventsSwapWithoutMutation()
        {
            world.SelectServingCup("s.cups.small");
            var cup = world.ServingCup;
            cup.production.phase = ProductionPhase.Processing;
            cup.production.remaining = 2;
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.SelectServingCup("s.cups.large").success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            Assert.That(world.ServingCup, Is.SameAs(cup));
        }

        [Test] public void MissingOldStackPreventsCupSwapWithoutDeletingOldCup()
        {
            world.SelectServingCup("s.cups.small");
            var cup = world.ServingCup;
            world.State.entities.Remove(world.Find("s.cups.small"));
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.SelectServingCup("s.cups.large").success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            Assert.That(world.ServingCup, Is.SameAs(cup));
        }

        [Test] public void ReturningLooseCupPreservesDifferentServingCupSelection()
        {
            world.SelectServingCup("s.cups.large");
            var selected = world.ServingCup;
            int stock = world.Find("s.cups.small").stock;
            world.Take("s.cups.small", AreaId.Counter, 0, -2, out var cup);
            Assert.That(world.Interact(cup.id, "s.cups.small").success, Is.True);
            Assert.That(world.Find(cup.id), Is.Null);
            Assert.That(world.Find("s.cups.small").stock, Is.EqualTo(stock));
            Assert.That(world.ServingCup, Is.SameAs(selected));
            Assert.That(world.State.production.activeCupId, Is.EqualTo(selected.id));
        }

        [Test] public void EmptyCupReturnClearsStoveDockReference()
        {
            world.Take("s.cups.small", AreaId.Counter, 0, -2, out var cup);
            Assert.That(world.Interact(cup.id, "m.stove").success, Is.True);
            Assert.That(world.Find("m.stove").production.heatingVesselId, Is.EqualTo(cup.id));
            Assert.That(world.Interact(cup.id, "s.cups.small").success, Is.True);
            Assert.That(world.Find("m.stove").production.heatingVesselId, Is.Null);
            Assert.That(world.OperateProduction("m.stove").success, Is.False);
        }

        [Test] public void CupCanReturnAfterContentsAreCompletelyPouredOut()
        {
            int stock = world.Find("s.cups.small").stock;
            world.SelectServingCup("s.cups.small");
            var cup = world.ServingCup;
            Load("s.water", cup.id);
            Assert.That(world.PourLiquid(cup.id, "s.cups.small", 200, 1).success, Is.False);
            Assert.That(world.Interact(cup.id, "s.cups.small").success, Is.False);
            Assert.That(world.PourLiquid(cup.id, null, 200, 1).success, Is.True);
            world.FinishPour(null);
            Assert.That(cup.contents, Is.Empty);
            Assert.That(world.Interact(cup.id, "s.cups.small").success, Is.True);
            Assert.That(world.Find("s.cups.small").stock, Is.EqualTo(stock));
            Assert.That(world.ServingCup, Is.Null);
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
        [Test] public void HeatedMilkKeepsTemperatureAcrossLongTicksAndCanReheat()
        {
            Heat("s.milk");var kettle=world.Find("m.heater");
            Assert.That(kettle.production.phase,Is.EqualTo(ProductionPhase.Boiling));
            Assert.That(world.TemperatureCelsius(kettle),Is.EqualTo(100).Within(.001f));
            world.Tick(100);
            Assert.That(kettle.production.phase,Is.EqualTo(ProductionPhase.Boiling));
            Assert.That(world.TemperatureCelsius(kettle),Is.EqualTo(100).Within(.001f));
            Assert.That(kettle.production.remaining,Is.Zero);
            Assert.That(world.OperateProduction("m.stove").success,Is.True);
            world.Tick(world.Catalog.production.heatSeconds);
            Assert.That(kettle.production.phase,Is.EqualTo(ProductionPhase.Boiling));
            Assert.That(world.TemperatureCelsius(kettle),Is.EqualTo(100).Within(.001f));
            Assert.That(world.LiquidVolume(kettle),Is.EqualTo(200));
        }
        [Test] public void LongTickCompletesHeatingWithoutCoolingOrLosingLiquid()
        {
            Load("s.water","m.heater");world.Interact("m.heater","m.stove");world.OperateProduction("m.stove");world.Tick(100);
            var kettle=world.Find("m.heater");
            Assert.That(kettle.production.phase,Is.EqualTo(ProductionPhase.Boiling));
            Assert.That(world.TemperatureCelsius(kettle),Is.EqualTo(100).Within(.001f));
            Assert.That(kettle.production.remaining,Is.Zero);
            Assert.That(world.LiquidVolume(kettle),Is.EqualTo(200));
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
            Assert.That(brewer.contents.Exists(portion=>portion.specId=="grounds"),Is.False);
            Assert.That(world.SolidMass(brewer)+world.CoffeeGroundsGrams(brewer.contents.Find(portion=>portion.specId=="coffee")),Is.EqualTo(12));
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
                var composition=world.EvaluateProductComposition(cup,product);
                Assert.That(composition.amountScore,Is.EqualTo(100).Within(.1),product.id);
                Assert.That(composition.standardScore,Is.EqualTo(100).Within(.1),product.id);
                Assert.That(composition.total,Is.EqualTo(100).Within(.1),product.id);
                Assert.That(composition.description,Does.StartWith("精品"),product.id);
                Assert.That(score.total,Is.EqualTo(100).Within(.1),product.id);Assert.That(score.grade,Is.EqualTo("匠心佳作"));
                world.DiscardProductionCup();world.SelectServingCup(product.cupSpecId=="cup.small"?"s.cups.small":"s.cups.large");
                Assert.That(world.EvaluateProduction(world.ServingCup,product).total,Is.EqualTo(0));world.DiscardProductionCup();
            }
        }
        [Test] public void ConfiguredCoffeeKindMismatchLowersScoreWithoutAdditionOrderGate()
        {
            var cup=PerfectCup("latte");var product=world.Product("latte");
            Assert.That(product.evaluation.amounts[0].coffeeKind,Is.EqualTo("essence"));
            float score=world.EvaluateProduction(cup,product).total;
            cup.contents[0].addedSequence=3;cup.contents[1].addedSequence=1;
            cup.contents.Reverse();
            foreach(var portion in cup.contents) portion.temperature=LiquidTemperature.Warm;
            Assert.That(world.EvaluateProduction(cup,product).total,Is.EqualTo(score));
            var coffee=cup.contents.Find(p=>p.specId=="coffee");
            coffee.coffeeKind="cold_brew";coffee.temperature=LiquidTemperature.Room;
            var composition=world.EvaluateProductComposition(cup,product);
            float remainingIngredientScore=100f*(product.evaluation.amounts.Length-1)/product.evaluation.amounts.Length;
            Assert.That(composition.amountScore,Is.EqualTo(remainingIngredientScore).Within(.001f));
            Assert.That(composition.standardScore,Is.EqualTo(remainingIngredientScore).Within(.001f));
            Assert.That(composition.total,Is.EqualTo(remainingIngredientScore).Within(.001f));
            Assert.That(world.EvaluateProduction(cup,product).total,Is.EqualTo(composition.total).Within(.001f));
            Assert.That(composition.total,Is.LessThan(score));
        }
        [Test] public void EmptyCupCanBeDeliveredForRemakeWithCupReturnedAndOrderPreserved()
        {
            world.StartDay();world.SelectServingCup("s.cups.small");var request=world.Request(world.State.focusedRequestId);int stock=world.Find("s.cups.small").stock;
            Assert.That(world.DeliverProductionCup().success,Is.True);Assert.That(world.ServingCup,Is.Null);
            Assert.That(world.Request(request.id),Is.SameAs(request));Assert.That(request.remakes,Is.EqualTo(1));Assert.That(world.Find("s.cups.small").stock,Is.EqualTo(stock+1));
            world.SelectServingCup("s.cups.small");world.DeliverProductionCup();Assert.That(world.Request(request.id),Is.Null);
        }
        [Test] public void PagingChangesActualDeliveryTargetAndSuccessfulOrderIsRemovedOnlyOnce()
        {
            world.StartDay();var first=world.Request(world.State.focusedRequestId);world.PageProductionOrder(1);var selected=world.Request(world.State.focusedRequestId);
            Assert.That(selected,Is.Not.SameAs(first));PerfectCup(selected.productId,selected);int money=world.State.money;
            Assert.That(world.DeliverProductionCup().success,Is.True);Assert.That(world.Request(selected.id),Is.Null);Assert.That(world.Request(first.id),Is.Not.Null);
            Assert.That(world.State.money,Is.GreaterThan(money));Assert.That(world.DeliverProductionCup().success,Is.False);
        }
        [Test] public void DiscardReturnsCupWithoutRefundingIngredients()
        {
            world.SelectServingCup("s.cups.small");Load("s.honey",world.ServingCup.id);int cups=world.Find("s.cups.small").stock,honey=world.Find("s.honey").stock;
            world.DiscardProductionCup();Assert.That(world.ServingCup,Is.Null);Assert.That(world.Find("s.cups.small").stock,Is.EqualTo(cups+1));Assert.That(world.Find("s.honey").stock,Is.EqualTo(honey));
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
            var cup=PerfectCup("latte");cup.latteArt=false;int count=cup.contents.Count;string contents=JsonUtility.ToJson(cup);world.RequestProductionMiniGame();var token=world.State.production.miniGameToken;
            world.ResolveProductionMiniGame(token,false,0);Assert.That(cup.contents.Count,Is.EqualTo(count));Assert.That(cup.latteArt,Is.False);
            Assert.That(JsonUtility.ToJson(cup),Is.EqualTo(contents));
            world.RequestProductionMiniGame();token=world.State.production.miniGameToken;world.DiscardProductionCup();
            Assert.That(world.ResolveProductionMiniGame(token,true,100).success,Is.False);
        }
        [Test] public void VersionThreeRoundTripRestoresCelsiusAndCupIdentityWithoutCoolingTimers()
        {
            Heat("s.milk");world.Tick(2);world.SelectServingCup("s.cups.large");world.PourLiquid("m.heater",world.ServingCup.id,40,1);
            var state=Copy();var restored=new CafeWorld(world.Catalog);
            Assert.That(restored.Restore(state).success,Is.True);Assert.That(restored.ProductionEnabled,Is.True);
            Assert.That(restored.LiquidVolume(restored.ServingCup),Is.EqualTo(40));
            Assert.That(restored.ServingCup.id,Is.EqualTo(world.ServingCup.id));
            Assert.That(restored.TemperatureCelsius(restored.ServingCup),Is.EqualTo(100).Within(.001f));
            Assert.That(restored.TemperatureCelsius(restored.Find("m.heater")),Is.EqualTo(100).Within(.001f));
            Assert.That(restored.Find("m.heater").production.remaining,Is.Zero);
            restored.Tick(100);
            Assert.That(restored.TemperatureCelsius(restored.ServingCup),Is.EqualTo(100).Within(.001f));
            Assert.That(restored.TemperatureCelsius(restored.Find("m.heater")),Is.EqualTo(100).Within(.001f));
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
