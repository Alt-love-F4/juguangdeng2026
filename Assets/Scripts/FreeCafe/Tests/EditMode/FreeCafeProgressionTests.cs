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
            Assert.That(world.State.unlockedProducts, Does.Contain("cocoa").And.Not.Contain("latte"));
            world.State.successfulOrders = 10;
            world.ConfigureEquipmentProgression(false);
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
        [TestCase("milkcoffee", 2)]
        [TestCase("creamcoffee", 3)]
        [TestCase("mocha", 4)]
        [TestCase("espresso", 5)]
        [TestCase("cocoa", 6)]
        [TestCase("matchalatte", 7)]
        [TestCase("latte", 8)]
        [TestCase("macchiato", 9)]
        [TestCase("honeylatte", 10)]
        public void EachRecipeHasItsOwnSuccessfulOrderMilestone(string id, int required)
        {
            world.ConfigureEquipmentProgression(false);
            world.State.precisionEquipmentActive = world.State.latteEquipmentActive = true;
            world.State.successfulOrders = required - 1;
            world.ConfigureEquipmentProgression(false);
            Assert.That(world.Product(id).unlockSuccessfulOrders, Is.EqualTo(required));
            Assert.That(world.CanOfferProduct(world.Product(id)), Is.False);
            Assert.That(world.State.unlockedProducts, Does.Not.Contain(id));
            Assert.That(world.ProductLockReason(world.Product(id)), Does.Contain(required + " 单"));
            world.State.successfulOrders++;
            world.ConfigureEquipmentProgression(false);
            Assert.That(world.CanOfferProduct(world.Product(id)), Is.True);
            Assert.That(world.State.unlockedProducts, Does.Contain(id));
            Assert.That(world.ProductLockReason(world.Product(id)), Is.Null);
        }

        [TestCase("espresso")]
        [TestCase("cocoa")]
        [TestCase("matchalatte")]
        [TestCase("latte")]
        [TestCase("honeylatte")]
        [TestCase("macchiato")]
        public void MissingEquipmentPreventsLearningAndOrderingEvenFromOldUnlockLists(string id)
        {
            world.ConfigureEquipmentProgression(false);
            world.State.successfulOrders = 20;
            foreach (var product in world.Catalog.products) world.State.unlockedProducts.Add(product.id);
            var saved = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(world.Restore(saved).success, Is.True);
            Assert.That(world.CanOfferProduct(world.Product(id)), Is.False);
            Assert.That(world.State.unlockedProducts, Does.Not.Contain(id));
            world.StartDay();
            foreach (var request in world.State.requests)
                Assert.That(world.CanOfferProduct(world.Product(request.productId)), Is.True);
        }

        [Test] public void SuccessfulOrdersTeachNewRecipeAtEachMilestoneImmediately()
        {
            world.ConfigureEquipmentProgression(false);
            world.StartDay();
            var first = world.State.requests[0];
            first.automaticRemaining = 0;
            world.Tick(.1f);
            Assert.That(world.State.successfulOrders, Is.EqualTo(1));
            Assert.That(world.State.unlockedProducts, Does.Not.Contain("milkcoffee"));
            world.State.requests[0].automaticRemaining = 0;
            world.Tick(.1f);
            Assert.That(world.State.successfulOrders, Is.EqualTo(2));
            Assert.That(world.State.unlockedProducts, Does.Contain("milkcoffee").And.Not.Contain("creamcoffee"));
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
