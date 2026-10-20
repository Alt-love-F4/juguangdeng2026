using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeTutorialRecipeTests
    {
        [TestCase(false, "black")]
        [TestCase(true, "essence")]
        public void GuidedRecipeProducesExpectedCoffee(bool extraction, string kind)
        {
            var world = MakeGuidedCoffee(extraction);
            Assert.That(world.ServingCup.contents.Find(portion => portion.specId == "coffee")?.coffeeKind, Is.EqualTo(kind));
        }

        private static CafeWorld MakeGuidedCoffee(bool extraction)
        {
            var w = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            w.EnableProduction(); w.EnterProduction();
            Assert.That(w.TakeAndInteract("s.beans", "m.grinder", AreaId.Counter, 0, 0).success);
            Assert.That(w.OperateProduction("m.grinder").success);
            w.Tick(20);
            var output = w.DragOutputPreview("m.grinder");
            Assert.That(output, Is.Not.Null);
            Assert.That(w.TakeMachineOutput("m.grinder", output.id, extraction ? "v.extractor.filter" : "m.heater", AreaId.Counter, 0, 0).success);
            Assert.That(w.PourLiquid("s.water", "m.heater", 200, 1).success);
            Assert.That(w.OperateProduction("m.heater").success);
            w.Tick(4.1f);
            if (extraction)
            {
                for (int i = 0; i < 100; i++)
                {
                    Assert.That(w.PourLiquid("m.heater", "v.extractor.filter", 2, .075f).success);
                    w.Tick(.075f);
                }
                w.FinishPour("v.extractor.filter");
                Assert.That(w.ExtractionSnapshot("v.extractor").completed);
                w.Tick(10);
                Assert.That(w.PrepareExtractionDrag("v.extractor").success);
            }
            Assert.That(w.SelectServingCup("s.cups.large").success);
            Assert.That(w.PourLiquid(extraction ? "v.extractor" : "m.heater", w.ServingCup.id, 40, 1).success);
            return w;
        }

        [Test]
        public void TutorialDeliveryRequiresTrayAndBlackCoffeeWithoutStartingBusinessOrAwardingMoney()
        {
            var world = MakeGuidedCoffee(false);
            Assert.That(world.StartTutorialCustomer().success, Is.True);
            var cup = world.ServingCup;
            var tray = world.State.entities.Find(entity => world.Spec(entity.specId).Has(Capability.Delivery));
            int money = world.State.money, reputation = world.State.reputation, served = world.State.served;
            cup.x = tray.x + 2;
            Assert.That(world.DeliverProductionCup().success, Is.False);
            Assert.That(world.HasTutorialCustomer, Is.True);
            cup.x = tray.x; cup.y = tray.y;
            Assert.That(world.HasTutorialCustomer, Is.True);
            Assert.That(world.State.open, Is.False);
            var result = world.DeliverProductionCup();
            Assert.That(result.success, Is.True, result.message);
            Assert.That(result.feedback, Is.EqualTo(CafeFeedbackKind.DeliverySatisfied));
            Assert.That(world.Find(cup.id), Is.Null);
            Assert.That(world.HasTutorialCustomer, Is.False);
            Assert.That(world.Find(CafeWorld.TutorialCustomerId), Is.Null);
            Assert.That(world.State.open, Is.False);
            Assert.That(world.State.businessPhase, Is.EqualTo(BusinessPhase.Preparation));
            Assert.That(world.State.money, Is.EqualTo(money));
            Assert.That(world.State.reputation, Is.EqualTo(reputation + 1));
            Assert.That(world.State.served, Is.EqualTo(served));
            Assert.That(world.State.todayServed, Is.Zero);
            Assert.That(world.State.todayRevenue, Is.Zero);
            Assert.That(world.StartDay().success, Is.True);
        }

        [Test]
        public void TutorialRejectsEmptyOrWrongDrinkWithoutConsumingCupOrOrder()
        {
            var world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction(); world.EnterProduction();
            world.StartTutorialCustomer();
            world.SelectServingCup("s.cups.large");
            var cup = world.ServingCup;
            Assert.That(world.DeliverProductionCup().success, Is.False);
            cup.contents.Add(new Portion { specId = "water", units = 1, milliliters = 200 });
            Assert.That(world.DeliverProductionCup().success, Is.False);
            Assert.That(world.ServingCup, Is.SameAs(cup));
            Assert.That(world.LiquidVolume(cup), Is.EqualTo(200));
            Assert.That(world.HasTutorialCustomer, Is.True);
            Assert.That(world.State.todayFailed, Is.Zero);
        }
    }
}
