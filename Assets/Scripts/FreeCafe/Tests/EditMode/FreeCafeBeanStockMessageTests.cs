using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeBeanStockMessageTests
    {
        private const string BeanStockMessage = "咖啡豆库存不足，请前往咖啡铺补货";
        private const string OtherStockMessage = "库存不足；点击补货或从经营菜单购买原料";
        private CafeWorld world;

        [SetUp]
        public void SetUp() => world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(
            Resources.Load<TextAsset>("FreeCafe/catalog").text));

        [Test]
        public void EmptyBeanSourceReportsCoffeeShopWithoutSideEffects(
            [Values(false, true)] bool production, [Values(0, -1)] int stock)
        {
            if (production) world.EnableProduction();
            var source = world.Find("s.beans");
            // The message must depend on the source specification, not its layout instance ID.
            source.id = "empty-bean-source";
            source.stock = stock;
            AssertRejectedWithoutSideEffects(source.id, source.area, BeanStockMessage);
        }

        [Test]
        public void EmptyNonBeanSourcesKeepOriginalMessage(
            [Values(false, true)] bool production,
            [Values("s.milk", "s.matcha", "s.cocoa", "s.cream", "s.honey", "s.sugar",
                "s.cups.small", "s.cups.large")] string sourceId)
        {
            if (production) world.EnableProduction();
            var source = world.Find(sourceId);
            source.stock = 0;
            AssertRejectedWithoutSideEffects(source.id, source.area, OtherStockMessage);
        }

        [Test]
        public void BeanStockMessageDoesNotOverrideEarlierValidation()
        {
            var source = world.Find("s.beans");
            source.stock = 0;
            AssertRejectedWithoutSideEffects(source.id, AreaId.Cafe, "请到物品所在区域取出");
            AssertRejectedWithoutSideEffects("missing-source", AreaId.Counter, "不是物品来源");
            AssertRejectedWithoutSideEffects("m.grinder", AreaId.Counter, "不是物品来源");
        }

        [Test]
        public void StockedBeanSourceStillTakesExactlyOneItem([Values(false, true)] bool production)
        {
            if (production) world.EnableProduction();
            var source = world.Find("s.beans");
            source.stock = 1;
            source.quality = 2;
            int count = world.State.entities.Count;
            int nextId = world.State.nextId;
            int changes = 0;
            world.Changed += () => changes++;

            var result = world.Take(source.id, source.area, 0, -2, out var item);

            Assert.That(result.success, Is.True);
            Assert.That(result.message, Is.EqualTo("取出：" + world.Spec("beans").label));
            Assert.That(item, Is.Not.Null);
            Assert.That(item.specId, Is.EqualTo("beans"));
            Assert.That(item.quality, Is.EqualTo(source.quality));
            Assert.That(world.Find(item.id), Is.SameAs(item));
            Assert.That(source.stock, Is.Zero);
            Assert.That(world.State.entities.Count, Is.EqualTo(count + 1));
            Assert.That(world.State.nextId, Is.EqualTo(nextId + 1));
            Assert.That(changes, Is.EqualTo(1));
        }

        private void AssertRejectedWithoutSideEffects(string sourceId, AreaId area, string expectedMessage)
        {
            string before = JsonUtility.ToJson(world.State);
            int changes = 0;
            System.Action onChanged = () => changes++;
            world.Changed += onChanged;
            try
            {
                var result = world.Take(sourceId, area, 0, -2, out var item);
                Assert.That(result.success, Is.False);
                Assert.That(result.message, Is.EqualTo(expectedMessage));
                Assert.That(item, Is.Null);
                Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
                Assert.That(changes, Is.Zero);
            }
            finally
            {
                world.Changed -= onChanged;
            }
        }
    }
}
