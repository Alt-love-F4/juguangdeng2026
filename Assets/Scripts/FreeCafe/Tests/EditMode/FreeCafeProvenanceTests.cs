using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeProvenanceTests
    {
        private CafeWorld world;
        private WorldEntity Jug => world.Find("v.extractor");
        private WorldEntity Filter => world.Find("v.extractor.filter");

        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnterProduction();
            Assert.That(world.SelectServingCup("s.cups.large").success, Is.True);
        }

        private void Add(string spec, WorldEntity target)
        {
            var dose = new WorldEntity { id = "provenance." + world.State.nextId++, specId = spec, area = AreaId.Counter };
            world.State.entities.Add(dose);
            Assert.That(world.Interact(dose.id, target.id).success, Is.True);
        }

        private void SetSource(string ingredient, float ml, LiquidTemperature temperature = LiquidTemperature.Room)
        {
            var source = world.Find("m.heater");
            source.contents.Clear();
            source.contents.Add(new Portion { specId = ingredient, units = 1, milliliters = ml, temperature = temperature });
            source.production.phase = ProductionPhase.Room;
        }

        private void Drain(float ml, int frames = 1)
        {
            float seconds = world.Catalog.production.filterResidenceSeconds + ml / world.Catalog.production.filterDrainMlPerSecond + .01f;
            for (int i = 0; i < frames; i++) world.Tick(seconds / frames);
        }

        private float GroundsIn(WorldEntity entity)
        {
            float sum = 0;
            foreach (var portion in entity.contents) sum += world.CoffeeGroundsGrams(portion);
            return sum;
        }

        [Test] public void OrdinaryGroundsBecomeInternalSourcesAndWaterDilutesWithoutMorePowder()
        {
            var cup = world.ServingCup;
            Add("grounds", cup); Add("water", cup);
            var coffee = cup.contents.Find(p => p.specId == "coffee");
            Assert.That(cup.contents.Exists(p => p.specId == "grounds"), Is.False);
            Assert.That(coffee.grams, Is.Zero);
            Assert.That(world.CoffeeGroundsGrams(coffee), Is.EqualTo(12).Within(.001f));
            Assert.That(world.CoffeeWaterMl(coffee), Is.EqualTo(200).Within(.001f));
            SetSource("water", 100);
            Assert.That(world.PourLiquid("m.heater", cup.id, 100, 1).success, Is.True);
            Assert.That(world.CoffeeGroundsGrams(coffee), Is.EqualTo(12).Within(.001f));
            Assert.That(world.CoffeeWaterMl(coffee), Is.EqualTo(300).Within(.001f));
            Assert.That(coffee.milliliters, Is.EqualTo(300).Within(.001f));
        }

        [TestCase(1)] [TestCase(40)]
        public void PowderConsumedByThroughWaterNotInjectedWaterAndDrainIsFrameIndependent(int frames)
        {
            Add("grounds", Jug); SetSource("water", 200, LiquidTemperature.Boiling);
            world.PourLiquid("m.heater", Jug.id, 100, 1);
            Assert.That(world.SolidMass(Filter), Is.EqualTo(12));
            Assert.That(GroundsIn(Jug), Is.Zero);
            Drain(100, frames);
            Assert.That(world.SolidMass(Filter), Is.EqualTo(6).Within(.002f));
            Assert.That(GroundsIn(Jug), Is.EqualTo(6).Within(.002f));
            Assert.That(Filter.production.filterReferenceGroundsGrams, Is.EqualTo(12));
            Assert.That(Filter.production.filterWaterPassedMl, Is.EqualTo(100).Within(.002f));
            Assert.That(world.ExtractionSnapshot(Jug).idealMl, Is.EqualTo(200));
            world.PourLiquid("m.heater", Jug.id, 100, 1); Drain(100, frames);
            Assert.That(world.SolidMass(Filter), Is.Zero.Within(.002f));
            Assert.That(Filter.contents.Exists(p => p.specId == "grounds"), Is.False);
            Assert.That(GroundsIn(Jug), Is.EqualTo(12).Within(.002f));
        }

        [Test] public void MilkThroughFilterDoesNotSpendPowder()
        {
            Add("grounds", Jug); SetSource("milk", 100);
            world.PourLiquid("m.heater", Jug.id, 100, 1); Drain(100);
            Assert.That(world.SolidMass(Filter), Is.EqualTo(12));
            Assert.That(Filter.production.filterWaterPassedMl, Is.Zero);
            Assert.That(Jug.contents.Exists(p => p.specId == "coffee"), Is.False);
            Assert.That(Jug.contents.Find(p => p.specId == "milk").milliliters, Is.EqualTo(100));
        }

        [Test] public void ExcessWaterDilutesEssenceWithoutGeneratingInfinitePowder()
        {
            Add("grounds", Jug); SetSource("water", 300, LiquidTemperature.Boiling);
            world.PourLiquid("m.heater", Jug.id, 300, 1);
            Drain(300);
            var coffee = Jug.contents.Find(p => p.specId == "coffee");
            Assert.That(world.CoffeeKind(coffee), Is.EqualTo("essence"));
            Assert.That(world.CoffeeGroundsGrams(coffee), Is.EqualTo(12).Within(.002f));
            Assert.That(world.CoffeeWaterMl(coffee), Is.EqualTo(300).Within(.002f));
            Assert.That(world.SolidMass(Filter), Is.Zero);
            Assert.That(Filter.production.filterSpent, Is.True);
        }

        [Test] public void ColdDrainAlsoKeepsInitialReferenceAndCannotSynthesizeBeyondIdeal()
        {
            Add("grounds", Jug); SetSource("water", 300);
            world.PourLiquid("m.heater", Jug.id, 100, 1); Drain(100);
            Assert.That(world.ExtractionSnapshot(Jug).idealMl, Is.EqualTo(200));
            Assert.That(world.SolidMass(Filter), Is.EqualTo(6).Within(.001f));
            world.PourLiquid("m.heater", Jug.id, 200, 1); Drain(200);
            var coffee = Jug.contents.Find(p => p.specId == "coffee");
            Assert.That(world.CoffeeKind(coffee), Is.EqualTo("cold_brew"));
            Assert.That(world.CoffeeGroundsGrams(coffee), Is.EqualTo(12).Within(.002f));
            Assert.That(world.CoffeeWaterMl(coffee), Is.EqualTo(300).Within(.002f));
            Assert.That(world.SolidMass(Filter), Is.Zero);
            Assert.That(Jug.production.brewStarted, Is.False);
        }

        [Test] public void MatchaMixerOutputHasNoFreePowderAndKeepsLedgerWhenTaken()
        {
            var mixer = world.Find("m.mixer");
            Add("matcha.powder", mixer); world.OperateProduction(mixer.id); world.Tick(20);
            var matcha = mixer.contents[0];
            Assert.That(matcha.specId, Is.EqualTo("matcha.drink"));
            Assert.That(matcha.grams, Is.Zero);
            Assert.That(matcha.composition.Find(p => p.ingredientId == "matcha.powder").grams, Is.EqualTo(12));
            var output = world.DragOutputPreview(mixer.id);
            Assert.That(world.TakeMachineOutput(mixer.id, output.id, world.ServingCup.id, AreaId.Counter, 0, 0).success, Is.True);
            var received = world.ServingCup.contents[0];
            Assert.That(received.milliliters, Is.EqualTo(200));
            Assert.That(received.grams, Is.Zero);
            Assert.That(received.composition.Find(p => p.ingredientId == "matcha.powder").grams, Is.EqualTo(12));
        }

        [Test] public void PartialPourCopiesThenScalesProvenanceIncludingSpill()
        {
            var source = world.Find("m.heater");
            source.contents.Clear();
            var coffee = new Portion { specId = "coffee", units = 1, coffeeKind = "essence", milliliters = 200 };
            world.AddConstituent(coffee, "grounds", 12, 0);
            world.AddConstituent(coffee, "water", 0, 200);
            source.contents.Add(coffee); source.production.phase = ProductionPhase.Room;
            world.PourLiquid(source.id, world.ServingCup.id, 50, 1);
            Assert.That(world.CoffeeGroundsGrams(coffee), Is.EqualTo(9));
            Assert.That(GroundsIn(world.ServingCup), Is.EqualTo(3));
            world.PourLiquid(source.id, null, 50, 1);
            Assert.That(world.CoffeeGroundsGrams(coffee), Is.EqualTo(6).Within(.001f));
            Assert.That(world.CoffeeWaterMl(coffee), Is.EqualTo(100).Within(.001f));
            Assert.That(GroundsIn(world.ServingCup), Is.EqualTo(3));
            Assert.That(source.production.spilledMl, Is.EqualTo(50));
        }

        [Test] public void PendingDrainSaveRestoreKeepsReferenceAndRemainingPowder()
        {
            Add("grounds", Jug); SetSource("water", 200, LiquidTemperature.Boiling);
            world.PourLiquid("m.heater", Jug.id, 50, 1); Drain(50);
            world.PourLiquid("m.heater", Jug.id, 50, 1);
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            var restored = new CafeWorld(world.Catalog);
            Assert.That(restored.Restore(snapshot).success, Is.True);
            world = restored; Drain(50, 20);
            Assert.That(world.SolidMass(Filter), Is.EqualTo(6).Within(.002f));
            Assert.That(GroundsIn(Jug), Is.EqualTo(6).Within(.002f));
            Assert.That(world.ExtractionSnapshot(Jug).groundsGrams, Is.EqualTo(12));
        }

        [TestCase("milk")] [TestCase("hotmilk")]
        public void MixerMakesFoamWithRealVolumeAndPayloadProvenance(string input)
        {
            var mixer = world.Find("m.mixer");
            Add(input, mixer); world.OperateProduction(mixer.id);
            world.Tick(20);
            var foam = mixer.contents[0];
            Assert.That(foam.specId, Is.EqualTo("milkfoam"));
            Assert.That(foam.milliliters, Is.EqualTo(200));
            Assert.That(foam.grams, Is.Zero);
            Assert.That(foam.composition.Find(p => p.ingredientId == "milk").milliliters, Is.EqualTo(200));
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(new CafeWorld(world.Catalog).Restore(snapshot).success, Is.True);
            var output = world.DragOutputPreview(mixer.id);
            Assert.That(output.portionPayload.milliliters, Is.EqualTo(200));
            Assert.That(world.TakeMachineOutput(mixer.id, output.id, world.ServingCup.id, AreaId.Counter, 0, 0).success, Is.True);
            var received = world.ServingCup.contents.Find(p => p.specId == "milkfoam");
            Assert.That(received.milliliters, Is.EqualTo(200));
            Assert.That(received.composition.Find(p => p.ingredientId == "milk").milliliters, Is.EqualTo(200));
        }

        [Test] public void ColdWaterReclassifiesOrdinaryWhileEssenceIgnoresTemperature()
        {
            var cup = world.ServingCup;
            Add("grounds", cup); Add("hotwater", cup);
            Assert.That(world.CoffeeKind(cup.contents.Find(p => p.specId == "coffee")), Is.EqualTo("black"));
            world.Tick(100);
            Assert.That(world.CoffeeKind(cup.contents.Find(p => p.specId == "coffee")), Is.EqualTo("black"));
            // Free grounds carry ambient heat until mixing moves them into coffee provenance.
            float hotCelsius = (200 * 100 + 12 * 20) / 212f;
            Assert.That(world.TemperatureCelsius(cup), Is.EqualTo(hotCelsius).Within(.001f));
            var retained = world.Find("m.brewer");
            Assert.That(world.PourLiquid(cup.id, retained.id, 100, 1).success, Is.True);
            SetSource("water", 200);
            Assert.That(world.PourLiquid("m.heater", cup.id, 200, 1).success, Is.True);
            var coffee = cup.contents.Find(p => p.specId == "coffee");
            Assert.That(world.TemperatureCelsius(coffee), Is.EqualTo((100 * hotCelsius + 200 * 20) / 300f).Within(.001f));
            Assert.That(world.CoffeeKind(coffee), Is.EqualTo("cold_brew"));
            Assert.That(world.CoffeeGroundsGrams(coffee), Is.EqualTo(6).Within(.001f));
            Assert.That(world.CoffeeWaterMl(coffee), Is.EqualTo(300).Within(.001f));
            Assert.That(GroundsIn(cup) + GroundsIn(retained), Is.EqualTo(12).Within(.001f));
            Assert.That(world.LiquidVolume(cup), Is.LessThanOrEqualTo(world.LiquidCapacity(cup)));
            Assert.That(world.Find("m.heater").production.spilledMl, Is.Zero);
            coffee.coffeeKind = "essence";
            Assert.That(world.CoffeeKind(coffee), Is.EqualTo("essence"), "A cold essence remains essence");
            Assert.That(world.Interact(cup.id, "m.stove").success, Is.True);
            Assert.That(world.OperateProduction("m.stove").success, Is.True);
            world.Tick(world.Catalog.production.heatSeconds);
            world.Tick(100);
            Assert.That(world.TemperatureCelsius(coffee), Is.EqualTo(100).Within(.001f));
            Assert.That(world.CoffeeKind(coffee), Is.EqualTo("essence"));
            Assert.That(GroundsIn(cup) + GroundsIn(retained), Is.EqualTo(12).Within(.001f));
        }

        [TestCase(false)] [TestCase(true)]
        public void InvalidOrDuplicateProvenanceRejectedBeforeRestore(bool duplicate)
        {
            Add("grounds", world.ServingCup); Add("water", world.ServingCup);
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            var portion = snapshot.entities.Find(p => p.id == world.ServingCup.id).contents[0];
            if (duplicate) portion.composition.Add(new ConstituentAmount { ingredientId = "hotwater", milliliters = 1 });
            else portion.composition[0].grams = float.NaN;
            var prior = world.State;
            Assert.That(world.Restore(snapshot).success, Is.False);
            Assert.That(world.State, Is.SameAs(prior));
        }
    }
}
