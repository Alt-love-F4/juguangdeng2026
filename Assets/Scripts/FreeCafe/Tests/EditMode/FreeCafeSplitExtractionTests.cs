using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeSplitExtractionTests
    {
        private CafeWorld world;
        private WorldEntity Jug => world.Find("v.extractor");
        private WorldEntity Filter => world.Find("v.extractor.filter");

        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnterProduction();
        }

        private void Grounds()
        {
            var item = new WorldEntity { id = "dose." + world.State.nextId++, specId = "grounds", units = 1, area = AreaId.Counter };
            world.State.entities.Add(item);
            Assert.That(world.Interact(item.id, Jug.id).success, Is.True);
        }

        private void Water(float volume = 200, LiquidTemperature temperature = LiquidTemperature.Boiling)
        {
            var heater = world.Find("m.heater");
            heater.contents.Clear();
            heater.contents.Add(new Portion { specId = "water", units = 1, milliliters = volume, temperature = temperature });
            heater.production.phase = ProductionPhase.Room;
        }

        [Test] public void InitiallyEmptyPartsAreIndependentAndAssembled()
        {
            Assert.That(world.ExtractionFilter(Jug), Is.SameAs(Filter));
            Assert.That(world.ExtractionJug(Filter), Is.SameAs(Jug));
            Assert.That(Filter.contents, Is.Empty); Assert.That(Jug.contents, Is.Empty);
            Assert.That(Filter.y, Is.GreaterThan(Jug.y));
        }

        [Test] public void GroundsStayInFilterAndEachDragConsumesOneDose()
        {
            Grounds(); Grounds();
            Assert.That(world.SolidMass(Filter), Is.EqualTo(24));
            Assert.That(world.SolidMass(Jug), Is.Zero);
            Assert.That(world.ExtractionSnapshot(Jug).idealSeconds, Is.EqualTo(15));
            Assert.That(world.ExtractionSnapshot(Filter).idealMl, Is.EqualTo(400));
        }

        [Test] public void FirstWaterPoolsBrieflyThenDripsWithoutDuplicatingInventory()
        {
            Grounds(); Water();
            Assert.That(world.PourLiquid("m.heater", Filter.id, 10, .2f).success, Is.True);
            Assert.That(world.LiquidVolume(Filter), Is.EqualTo(10));
            Assert.That(world.LiquidVolume(Jug), Is.Zero);
            world.Tick(.05f);
            Assert.That(world.LiquidVolume(Filter), Is.EqualTo(10));
            world.Tick(.2f);
            Assert.That(world.LiquidVolume(Filter), Is.Zero.Within(.001f));
            Assert.That(world.LiquidVolume(Jug), Is.EqualTo(10).Within(.001f));
            Assert.That(world.LiquidVolume(world.Find("m.heater")) + world.LiquidVolume(Filter) + world.LiquidVolume(Jug), Is.EqualTo(200).Within(.001f));
            Assert.That(Jug.contents[0].specId, Is.EqualTo("coffee"));
            Assert.That(world.CoffeeKind(Jug.contents[0]), Is.EqualTo("black"));
            Assert.That(Jug.production.brewCompleted, Is.False);
            Assert.That(world.SolidMass(Filter), Is.EqualTo(11.4f).Within(.001f));
            Assert.That(world.CoffeeGroundsGrams(Jug.contents[0]), Is.EqualTo(.6f).Within(.001f));
            Assert.That(world.SolidMass(Filter) + world.CoffeeGroundsGrams(Jug.contents[0]), Is.EqualTo(12).Within(.001f));
        }

        [Test] public void DetachedFilterDoesNotCreateCoffeeInRemoteJugAndCanBeReassembled()
        {
            Grounds();
            Assert.That(world.Move(Filter.id, AreaId.Counter, -3, -3).success, Is.True);
            Assert.That(world.ExtractionFilter(Jug), Is.Null);
            Water(); Assert.That(world.PourLiquid("m.heater", Filter.id, 20, 1).success, Is.True);
            world.Tick(1);
            Assert.That(world.LiquidVolume(Filter), Is.EqualTo(20));
            Assert.That(world.LiquidVolume(Jug), Is.Zero);
            Assert.That(world.EmptyProductionMachine(Filter.id).success, Is.True);
            Assert.That(world.Interact(Filter.id, Jug.id).success, Is.True);
            Assert.That(world.ExtractionFilter(Jug), Is.SameAs(Filter));
        }

        [Test] public void CompletionCapsLowerJugOnlyAfterLastDropsAndExhaustsUsedGroundsAbove()
        {
            Grounds(); Water();
            for (int index = 0; index < 15; index++)
            {
                world.PourLiquid("m.heater", Jug.id, 200f / 15, .5f);
                world.Tick(.5f);
            }
            world.FinishPour(Filter.id); world.Tick(.2f);
            Assert.That(world.ExtractionSnapshot(Jug).qualityLabel, Is.EqualTo("优秀精粹咖啡液"));
            Assert.That(world.ExtractionFilter(Jug), Is.Null);
            Assert.That(world.ExtractionJug(Filter), Is.Null);
            Assert.That(world.LiquidVolume(Jug), Is.EqualTo(200).Within(.002f));
            Assert.That(world.CanPour(Jug), Is.True);
            Assert.That(world.SolidMass(Jug), Is.Zero);
            Assert.That(Filter.production.filterSpent, Is.True);
            Assert.That(world.SolidMass(Filter), Is.Zero);
            Assert.That(Filter.contents, Is.Empty);
            Assert.That(world.CoffeeGroundsGrams(Jug.contents[0]), Is.EqualTo(12).Within(.002f));
            Assert.That(Jug.contents[0].productionScore, Is.EqualTo(100));
            Assert.That(world.CoffeeKind(Jug.contents[0]), Is.EqualTo("essence"));
            Assert.That(Jug.production.brewCompleted, Is.True);
        }

        [Test] public void ReservedWaterCannotOverflowLowerCapacity()
        {
            Grounds(); Water(600);
            world.PourLiquid("m.heater", Jug.id, 350, 1);
            world.PourLiquid("m.heater", Jug.id, 200, 1);
            Assert.That(world.LiquidVolume(Filter) + world.LiquidVolume(Jug), Is.EqualTo(400).Within(.001f));
            world.Tick(3);
            Assert.That(world.LiquidVolume(Jug), Is.EqualTo(400).Within(.001f));
        }

        [Test] public void AssemblyFollowsLowerJugAndCannotPourBackIntoItsOwnFilter()
        {
            world.Move(Jug.id, AreaId.Counter, -1, 0);
            Assert.That(Filter.x, Is.EqualTo(Jug.x - .22f)); Assert.That(Filter.y, Is.EqualTo(Jug.y + .91f));
            Jug.contents.Add(new Portion { specId = "water", units = 1, milliliters = 10 });
            Assert.That(world.PourLiquid(Jug.id, Filter.id, 5, .1f).success, Is.False);
            Assert.That(world.LiquidVolume(Jug), Is.EqualTo(10));
        }

        [Test] public void ColdWaterDrainsAsColdCoffeeWithoutStartingPrecisionTimer()
        {
            Grounds(); Water(200, LiquidTemperature.Room);
            world.PourLiquid("m.heater", Jug.id, 20, 1); world.Tick(1);
            Assert.That(world.ExtractionSnapshot(Jug).inProgress, Is.False);
            Assert.That(Jug.contents[0].specId, Is.EqualTo("coffee"));
            Assert.That(world.CoffeeKind(Jug.contents[0]), Is.EqualTo("cold_brew"));
            Assert.That(Jug.production.brewCompleted, Is.False);
        }

        [Test] public void SavingPendingWaterKeepsBothInventoriesAndAssemblyLinks()
        {
            Grounds(); Water(); world.PourLiquid("m.heater", Jug.id, 20, 1);
            var restored = new CafeWorld(world.Catalog);
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(restored.Restore(snapshot).success, Is.True);
            var jug = restored.Find(Jug.id); var filter = restored.Find(Filter.id);
            Assert.That(restored.ExtractionFilter(jug), Is.SameAs(filter));
            Assert.That(restored.LiquidVolume(filter), Is.EqualTo(20));
            restored.Tick(1);
            Assert.That(restored.LiquidVolume(jug), Is.EqualTo(20));
            Assert.That(restored.SolidMass(filter), Is.EqualTo(10.8f).Within(.001f));
            Assert.That(restored.CoffeeGroundsGrams(jug.contents[0]), Is.EqualTo(1.2f).Within(.001f));
            Assert.That(restored.SolidMass(filter) + restored.CoffeeGroundsGrams(jug.contents[0]), Is.EqualTo(12).Within(.001f));
        }

        [Test] public void InvalidAssemblyReferenceIsRejectedBeforeRestoring()
        {
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            snapshot.entities.Find(entity => entity.id == Jug.id).production.extractionFilterId = "missing";
            Assert.That(world.Restore(snapshot).success, Is.False);
        }
    }
}
