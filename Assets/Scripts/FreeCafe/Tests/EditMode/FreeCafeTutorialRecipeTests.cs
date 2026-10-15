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
            var w = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            w.EnableProduction(); w.EnterProduction();
            Assert.That(w.TakeAndInteract("s.beans", "m.grinder", AreaId.Counter, 0, 0).success);
            Assert.That(w.OperateProduction("m.grinder").success);
            w.Tick(20);
            var output = w.DragOutputPreview("m.grinder");
            Assert.That(output, Is.Not.Null);
            Assert.That(w.TakeMachineOutput("m.grinder", output.id, extraction ? "v.extractor.filter" : "m.heater", AreaId.Counter, 0, 0).success);
            Assert.That(w.TakeAndInteract("s.water", "m.heater", AreaId.Counter, 0, 0).success);
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
            Assert.That(w.ServingCup.contents.Find(p => p.specId == "coffee")?.coffeeKind, Is.EqualTo(kind));
        }
    }
}
