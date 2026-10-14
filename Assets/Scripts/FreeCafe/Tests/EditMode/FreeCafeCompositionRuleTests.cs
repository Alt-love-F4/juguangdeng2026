using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeCompositionRuleTests
    {
        private CafeWorld w;private WorldEntity cup;
        [SetUp] public void Setup(){w=new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));w.EnableProduction();w.EnterProduction();w.SelectServingCup("s.cups.large");cup=w.ServingCup;}
        private void Add(string spec){var e=new WorldEntity{id="rule."+w.State.nextId++,specId=spec,area=AreaId.Counter};w.State.entities.Add(e);Assert.That(w.Interact(e.id,cup.id).success,Is.True);}
        [Test] public void WaterAndGroundsBecomeColdBrewAndTemperatureReversiblyControlsBlackCoffee()
        {
            Add("grounds");Add("water");var coffee=cup.contents.Find(p=>p.specId=="coffee");Assert.That(w.CoffeeKind(coffee),Is.EqualTo("cold_brew"));
            w.Interact(cup.id,"m.stove");w.OperateProduction("m.stove");w.Tick(w.Catalog.production.heatSeconds);
            Assert.That(w.CoffeeKind(coffee),Is.EqualTo("black"));
            w.Tick(100);Assert.That(w.CoffeeKind(coffee),Is.EqualTo("black"),"Ticks do not cool ordinary coffee");
            Assert.That(w.TemperatureCelsius(coffee),Is.EqualTo(100).Within(.001f));
            var source=w.Find("m.heater");
            Assert.That(w.PourLiquid(cup.id,source.id,100,1).success,Is.True);
            Add("water"); // 100 ml at 100 C + 200 ml at 20 C = 46 2/3 C, below the 50 C boundary.
            coffee=cup.contents.Find(p=>p.specId=="coffee");
            Assert.That(w.TemperatureCelsius(coffee),Is.EqualTo((100*100+200*20)/300f).Within(.001f));
            Assert.That(w.CoffeeKind(coffee),Is.EqualTo("cold_brew"));
            Assert.That(w.LiquidVolume(cup),Is.EqualTo(300));
            Assert.That(w.LiquidVolume(cup),Is.LessThanOrEqualTo(w.LiquidCapacity(cup)));
            Assert.That(coffee.composition.Find(p=>p.ingredientId=="grounds").grams,Is.EqualTo(6));
            Assert.That(w.CoffeeGroundsGrams(coffee)+w.CoffeeGroundsGrams(source.contents.Find(p=>p.specId=="coffee")),Is.EqualTo(12));
            Assert.That(cup.production.spilledMl,Is.Zero);
        }
        [Test] public void BeansNeverDissolveAndUnknownMixHasOnlyMysteryProductName()
        {
            Add("beans");Add("water");Assert.That(cup.contents.Exists(p=>p.specId=="coffee"),Is.False);
            Assert.That(w.CompositionName(cup),Is.EqualTo("迷之饮品"));
            Assert.That(w.ContainerInspection(cup),Does.Contain("咖啡豆"));Assert.That(w.ContainerInspection(cup),Does.Contain("清水"));
        }
        [Test] public void CreamBlackCoffeeUsesExplicitNameWithoutBeingForcedIntoLatte()
        {
            Add("grounds");Add("hotwater");Add("cream");Assert.That(w.CompositionName(cup),Is.EqualTo("奶油黑咖啡"));
            Add("beans");Assert.That(w.CompositionName(cup),Is.EqualTo("迷之饮品"));Assert.That(w.ContainerInspection(cup),Does.Contain("咖啡豆"));
        }
        [Test] public void DifferentCoffeeKindsSurvivePourAndRestore()
        {
            cup.contents.Add(new Portion{specId="coffee",coffeeKind="essence",units=1,milliliters=80,productionScore=70,extracted=true});
            var source=w.Find("m.heater");source.contents.Add(new Portion{specId="coffee",coffeeKind="cold_brew",units=1,milliliters=80});source.production.phase=ProductionPhase.Room;
            w.PourLiquid(source.id,cup.id,80,1);
            Assert.That(cup.contents.FindAll(p=>p.specId=="coffee").Count,Is.EqualTo(2));
            var copy=JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(w.State));Assert.That(w.Restore(copy).success,Is.True);
            Assert.That(w.ServingCup.contents.FindAll(p=>p.specId=="coffee").Count,Is.EqualTo(2));
        }
    }
}
