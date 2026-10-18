using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeEquipmentAcquiredTests
    {
        private CafeWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction();
            world.ConfigureEquipmentProgression(false);
        }

        [Test]
        public void SuccessfulPurchasesEmitOnceAfterStateAndContentAreUpdated()
        {
            world.State.successfulOrders = CafeWorld.LatteOrdersRequired;
            world.State.money = 1000;
            var events = new List<bool>();
            int changes = 0;
            world.Changed += () => changes++;
            world.EquipmentAcquired += latte =>
            {
                events.Add(latte);
                Assert.That(changes, Is.EqualTo(events.Count), "Changed should precede EquipmentAcquired.");
                Assert.That(latte ? world.State.latteEquipmentActive : world.State.precisionEquipmentActive, Is.True);
                Assert.That(world.State.unlockedProducts, Does.Contain(latte ? "latte" : "espresso"));
                Assert.That(world.State.money, Is.EqualTo(latte ? 450 : 800));
            };

            Assert.That(world.ActivateEquipment(false).success, Is.True);
            Assert.That(world.ActivateEquipment(false).success, Is.False);
            Assert.That(world.ActivateEquipment(true).success, Is.True);
            Assert.That(world.ActivateEquipment(true).success, Is.False);
            Assert.That(events, Is.EqualTo(new[] { false, true }));
        }

        [Test]
        public void FailedPurchasesAndRestoreNeverEmitAcquired()
        {
            int events = 0;
            world.EquipmentAcquired += _ => events++;
            Assert.That(world.ActivateEquipment(false).success, Is.False);
            world.State.successfulOrders = CafeWorld.LatteOrdersRequired;
            world.State.money = 1000;
            Assert.That(world.ActivateEquipment(true).success, Is.False, "Precision equipment is required first.");
            world.State.money = 0;
            Assert.That(world.ActivateEquipment(false).success, Is.False);
            Assert.That(events, Is.Zero);

            world.State.money = 1000;
            Assert.That(world.ActivateEquipment(false).success, Is.True);
            Assert.That(world.ActivateEquipment(true).success, Is.True);
            events = 0;
            var saved = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(world.Restore(saved).success, Is.True);
            Assert.That(events, Is.Zero);
            Assert.That(world.ActivateEquipment(false).success, Is.False);
            Assert.That(world.ActivateEquipment(true).success, Is.False);
            Assert.That(events, Is.Zero);

            var restored = new CafeWorld(world.Catalog);
            restored.EquipmentAcquired += _ => events++;
            Assert.That(restored.Restore(JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(saved))).success, Is.True);
            Assert.That(events, Is.Zero);
        }
    }
}
