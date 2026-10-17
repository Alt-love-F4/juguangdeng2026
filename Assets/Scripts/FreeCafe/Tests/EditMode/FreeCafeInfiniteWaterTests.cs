using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeInfiniteWaterTests
    {
        private CafeWorld world;
        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction(); world.EnterProduction();
        }
        [Test] public void RepeatedPoursUseTheSameContainerAndNeverConsumeStockOrMoney()
        {
            var source = world.Find("s.water"); int money = world.State.money, count = world.State.entities.Count;
            Assert.That(world.Spec(source.specId).Has(Capability.Movable | Capability.Container), Is.True);
            Assert.That(world.Spec(source.specId).Has(Capability.Source), Is.False);
            for (int i = 0; i < 25; i++)
            {
                Assert.That(world.PourLiquid(source.id, "m.heater", 200, 1).success, Is.True);
                Assert.That(world.LiquidVolume(world.Find("m.heater")), Is.EqualTo(200).Within(.001f));
                Assert.That(world.TemperatureCelsius(world.Find("m.heater")), Is.EqualTo(20).Within(.001f));
                Assert.That(world.EmptyProductionMachine("m.heater").success, Is.True);
                Assert.That(world.CanPour(source), Is.True);
                Assert.That(world.LiquidVolume(source), Is.EqualTo(1000).Within(.001f));
                Assert.That(world.Find(source.id), Is.SameAs(source));
            }
            Assert.That(world.State.money, Is.EqualTo(money));
            Assert.That(world.State.entities.Count, Is.EqualTo(count));
            Assert.That(source.stock, Is.Zero);
        }
        [Test] public void SpillsAndOverflowDoNotDepleteSupplyOrOverfillTheReceiver()
        {
            Assert.That(world.PourLiquid("s.water", "m.heater", 2500, 1).success, Is.True);
            Assert.That(world.LiquidVolume(world.Find("m.heater")), Is.EqualTo(400).Within(.001f));
            Assert.That(world.LiquidVolume(world.Find("s.water")), Is.EqualTo(1000).Within(.001f));
            Assert.That(world.PourLiquid("s.water", null, 3000, 1).success, Is.True);
            Assert.That(world.Find("s.water").production.spilledMl, Is.EqualTo(5100).Within(.001f));
            Assert.That(world.CanPour(world.Find("s.water")), Is.True);
        }
        [Test] public void SupplyCannotBeContaminatedHeatedDeletedOrDelivered()
        {
            world.PourLiquid("s.water", "m.heater", 200, 1);
            Assert.That(world.PourLiquid("m.heater", "s.water", 30, 1).success, Is.False);
            Assert.That(world.TakeAndInteract("s.milk", "s.water", AreaId.Counter, 0, 0).success, Is.False);
            Assert.That(world.Interact("s.water", "m.stove").success, Is.False);
            Assert.That(world.Interact("s.water", "waste").success, Is.False);
            Assert.That(world.Interact("s.water", "service").success, Is.False);
            Assert.That(world.EmptyProductionMachine("s.water").success, Is.False);
            Assert.That(world.Find("s.water"), Is.Not.Null);
            Assert.That(world.Find("s.water").contents[0].specId, Is.EqualTo("water"));
        }
        [Test] public void OldStockSnapshotMigratesToPureRefillableWaterAndReturnUsesNewHome()
        {
            var saved = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            var old = saved.entities.Find(entity => entity.id == "s.water");
            old.contents.Clear(); old.stock = 0; old.x = -2.5f; old.y = -3.15f;
            Assert.That(world.Restore(saved).success, Is.True);
            Assert.That(world.CanPour(world.Find("s.water")), Is.True);
            Assert.That(world.ReturnVesselHome("s.water").success, Is.True);
            Assert.That(world.Find("s.water").x, Is.EqualTo(10.2f).Within(.001f));
            Assert.That(world.Find("s.water").y, Is.EqualTo(2.2f).Within(.001f));
        }
    }
}
