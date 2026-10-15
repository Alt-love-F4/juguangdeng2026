using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeIngredientReturnTests
    {
        private CafeWorld world;

        [SetUp]
        public void Setup() => world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(
            Resources.Load<TextAsset>("FreeCafe/catalog").text));

        private WorldEntity Source(string ingredient) => world.State.entities.Find(e =>
            world.Spec(e.specId).Has(Capability.Source) && world.Spec(e.specId).sourceItemId == ingredient);

        private WorldEntity Take(WorldEntity source)
        {
            Assert.That(world.Take(source.id, source.area, 0, -2, out var item).success, Is.True);
            return item;
        }

        private string Snapshot() => JsonUtility.ToJson(world.State);

        [Test]
        public void AllUnusedIngredientsReturnExactlyOnce(
            [Values(false, true)] bool production,
            [Values("beans", "milk", "matcha.powder", "cocoa.powder", "cream", "honey", "water", "sugar")] string ingredient)
        {
            if (production) world.EnableProduction();
            var source = Source(ingredient);
            var item = Take(source);
            item.units = 3;
            int stock = source.stock, nextId = world.State.nextId;
            int notifications = 0;
            world.Changed += () =>
            {
                notifications++;
                Assert.That(world.Find(item.id), Is.Null);
                Assert.That(source.stock, Is.EqualTo(stock + 3));
            };
            string before = Snapshot();
            Assert.That(world.IsReceiver(source), Is.True);
            Assert.That(world.Preview(item.id, source.id).success, Is.True);
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(notifications, Is.Zero);
            Assert.That(world.Interact(item.id, source.id).success, Is.True);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(world.State.nextId, Is.EqualTo(nextId));
            before = Snapshot();
            Assert.That(world.Interact(item.id, source.id).success, Is.False);
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void InvalidReturnsHaveNoSideEffects(
            [Values(false, true)] bool production,
            [Values("wrong-source", "quality", "payload", "contents", "task", "processing", "locked", "zero", "negative", "overflow", "transformed")] string reason)
        {
            if (production) world.EnableProduction();
            var source = Source("beans");
            var item = Take(source);
            switch (reason)
            {
                case "wrong-source": source = Source("milk"); break;
                case "quality": item.quality = source.quality + 1; break;
                case "payload": item.portionPayload = new Portion { specId = "beans", units = 1, grams = .5f }; break;
                case "contents": item.contents.Add(new Portion { specId = "water", units = 1 }); break;
                case "task": item.task = new ProcessingTask(); break;
                case "processing": item.production.phase = ProductionPhase.Processing; break;
                case "locked": item.ownerId = "m.grinder"; break;
                case "zero": item.units = 0; break;
                case "negative": item.units = -1; break;
                case "overflow": source.stock = int.MaxValue - 1; item.units = 2; break;
                case "transformed": item.specId = "grounds"; break;
            }
            int notifications = 0;
            world.Changed += () => notifications++;
            string before = Snapshot();
            Assert.That(world.Preview(item.id, source.id).success, Is.False);
            Assert.That(world.Interact(item.id, source.id).success, Is.False);
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(world.Find(item.id), Is.SameAs(item));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void ReturnAcceptsEmptyExcessAndMaximumSafeInventory(
            [Values(false, true)] bool production, [Values(0, 10000, int.MaxValue - 3)] int stock)
        {
            if (production) world.EnableProduction();
            var source = Source("beans");
            var item = Take(source);
            source.stock = stock;
            item.units = 3;
            Assert.That(world.Interact(item.id, source.id).success, Is.True);
            Assert.That(source.stock, Is.EqualTo(stock + 3));
        }

        [Test]
        public void SameSourceDropIsANetNoOp(
            [Values(false, true)] bool production,
            [Values("beans", "milk", "matcha.powder", "cocoa.powder", "cream", "honey", "water", "sugar")] string ingredient,
            [Values(1, int.MaxValue)] int stock)
        {
            if (production) world.EnableProduction();
            var source = Source(ingredient);
            source.stock = stock;
            int notifications = 0;
            world.Changed += () => notifications++;
            string before = Snapshot();
            Assert.That(world.TakeAndInteract(source.id, source.id, source.area, 0, -2).success, Is.True);
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(world.Find(source.id), Is.SameAs(source));
            Assert.That(notifications, Is.Zero);
        }

        [TestCase("empty")]
        [TestCase("position")]
        [TestCase("area")]
        [TestCase("locked")]
        public void SameSourceDropStillValidatesBeforeSuccess(string reason)
        {
            var source = Source("beans");
            if (reason == "empty") source.stock = 0;
            if (reason == "locked") source.ownerId = "m.grinder";
            string before = Snapshot();
            int notifications = 0;
            world.Changed += () => notifications++;
            Assert.That(world.TakeAndInteract(source.id, source.id,
                reason == "area" ? AreaId.Cafe : source.area,
                reason == "position" ? float.NaN : 0, -2).success, Is.False);
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void UnusedIngredientAndReturnedExcessStockSurviveSaveRoundtrip(
            [Values(false, true)] bool production,
            [Values("beans", "milk", "matcha.powder", "cocoa.powder", "cream", "honey", "water", "sugar")] string ingredient)
        {
            if (production) world.EnableProduction();
            var source = Source(ingredient);
            var item = Take(source);
            item.units = 3;
            source.stock = world.Spec(source.specId).initialStock + 5;
            int expected = source.stock + item.units;
            Assert.That(world.Restore(JsonUtility.FromJson<CafeSnapshot>(Snapshot())).success, Is.True);
            Assert.That(world.Interact(item.id, source.id).success, Is.True);
            Assert.That(world.Restore(JsonUtility.FromJson<CafeSnapshot>(Snapshot())).success, Is.True);
            Assert.That(world.Find(item.id), Is.Null);
            Assert.That(world.Find(source.id).stock, Is.EqualTo(expected));
        }

        [Test]
        public void FreeRefillRestoresMissingStockWithoutChargingOrRepeatedNotifications()
        {
            var milk = Source("milk");
            foreach (var entity in world.State.entities)
            {
                var spec = world.Spec(entity.specId);
                if (!spec.Has(Capability.Source)) continue;
                spec.refillCost = 0;
                entity.stock = spec.initialStock;
            }
            milk.stock = 0;
            world.State.money = 0;
            int notifications = 0;
            world.Changed += () => notifications++;
            Assert.That(world.Refill().success, Is.True);
            Assert.That(milk.stock, Is.EqualTo(world.Spec(milk.specId).initialStock));
            Assert.That(world.State.money, Is.Zero);
            Assert.That(notifications, Is.EqualTo(1));
            string before = Snapshot();
            Assert.That(world.Refill().success, Is.False);
            Assert.That(Snapshot(), Is.EqualTo(before));
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void RefillPreservesExcessInventoryWhileRefillingOtherSources([Values(false, true)] bool production)
        {
            if (production) world.EnableProduction();
            var beans = Source("beans");
            var milk = Source("milk");
            beans.stock = world.Spec(beans.specId).initialStock + 10;
            int excess = beans.stock;
            milk.stock = 0;
            world.State.money = 100000;
            Assert.That(world.Refill().success, Is.True);
            Assert.That(beans.stock, Is.EqualTo(excess));
            Assert.That(milk.stock, Is.EqualTo(world.Spec(milk.specId).initialStock));
            string before = Snapshot();
            Assert.That(world.Refill().success, Is.False);
            Assert.That(Snapshot(), Is.EqualTo(before));
        }
    }
}
