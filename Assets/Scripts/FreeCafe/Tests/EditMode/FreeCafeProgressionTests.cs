using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeProgressionTests
    {
        private CafeWorld world;
        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction();
        }
        [Test] public void StoryEntryStartsWithPlainCoffeeAndHidesAdvancedTools()
        {
            world.ConfigureEquipmentProgression(false);
            Assert.That(world.PrecisionEquipmentAvailable, Is.False);
            Assert.That(world.LatteEquipmentAvailable, Is.False);
            Assert.That(world.State.unlockedProducts, Is.EquivalentTo(new[] { "blackcoffee", "cold_brew" }));
            Assert.That(world.IsEquipmentAvailable(world.Find("v.extractor")), Is.False);
            Assert.That(world.IsEquipmentAvailable(world.Find("tool.milk-pitcher")), Is.False);
            Assert.That(world.RequestProductionMiniGame().success, Is.False);
        }
        [Test] public void ActivationRequiresSuccessesMoneyAndOrderedStages()
        {
            world.ConfigureEquipmentProgression(false);
            Assert.That(world.ActivateEquipment(false).success, Is.False);
            world.State.successfulOrders = CafeWorld.PrecisionOrdersRequired;
            int before = world.State.money;
            Assert.That(world.ActivateEquipment(false).success, Is.True);
            Assert.That(world.State.money, Is.EqualTo(before - CafeWorld.PrecisionActivationCost));
            Assert.That(world.State.unlockedProducts, Does.Contain("espresso").And.Not.Contain("latte"));
            Assert.That(world.ActivateEquipment(false).success, Is.False);
            world.State.successfulOrders = CafeWorld.LatteOrdersRequired;
            world.State.money = 1000;
            Assert.That(world.ActivateEquipment(true).success, Is.True);
            Assert.That(world.State.unlockedProducts, Does.Contain("latte").And.Contain("honeylatte").And.Contain("macchiato"));
            var saved = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(world.Restore(saved).success, Is.True);
            Assert.That(world.PrecisionEquipmentAvailable && world.LatteEquipmentAvailable, Is.True);
        }
        [Test] public void SandboxUnlocksAllWithoutChangingSavedStoryActivations()
        {
            world.ConfigureEquipmentProgression(true);
            Assert.That(world.PrecisionEquipmentAvailable && world.LatteEquipmentAvailable, Is.True);
            Assert.That(world.State.unlockedProducts.Count, Is.EqualTo(world.Catalog.products.Length));
            world.ConfigureEquipmentProgression(false);
            Assert.That(world.PrecisionEquipmentAvailable || world.LatteEquipmentAvailable, Is.False);
            Assert.That(world.State.unlockedProducts, Is.EquivalentTo(new[] { "blackcoffee", "cold_brew" }));
        }
        [Test] public void TutorialVisitorNeverUsesInjectedStoryIdentity()
        {
            var catalog = world.Catalog;
            world = new CafeWorld(catalog, new StoryIdentity());
            Assert.That(world.StartTutorialCustomer().success, Is.True);
            var person = world.Find(CafeWorld.TutorialCustomerId).person;
            Assert.That(person.displayName, Is.EqualTo("B太太"));
            Assert.That(person.characterId, Is.Null.Or.Empty);
            Assert.That(person.narrativeCharacterId, Is.Null.Or.Empty);
            Assert.That(person.visualId, Is.EqualTo("customer"));
        }
        private sealed class StoryIdentity : ICafeCustomerIdentitySource
        {
            public CafePersonIdentity CreateIdentity(string visitId, int seatIndex) => new CafePersonIdentity
            { personId = "laurent", characterId = "laurent", displayName = "主角", role = CafePersonRole.Customer };
        }
    }
}
