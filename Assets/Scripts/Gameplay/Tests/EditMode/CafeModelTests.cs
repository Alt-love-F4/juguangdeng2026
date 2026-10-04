using System;
using System.Linq;
using NUnit.Framework;
using Spotlight.Interaction;
using UnityEngine;

namespace Spotlight.Gameplay.Tests
{
    public sealed class CafeModelTests
    {
        private static EntityDefinition[] Definitions() => new[] {
            new EntityDefinition { id = "cup", role = "item", tags = "cup|container" },
            new EntityDefinition { id = "bean", role = "item", tags = "ingredient" },
            new EntityDefinition { id = "slot1", role = "slot", accepts = "cup", capacity = 1 },
            new EntityDefinition { id = "slot2", role = "slot", accepts = "cup", capacity = 1 }
        };
        [Test] public void MovingItemTransfersOwnershipWithoutCopyingIt()
        {
            var ledger = new PlacementLedger(Definitions()); var item = new ItemState { definitionId = "cup" };
            Assert.That(ledger.Place(item, "slot1").Success); string id = item.instanceId;
            Assert.That(ledger.Place(item, "slot2").Success); Assert.That(item.instanceId, Is.EqualTo(id));
            Assert.That(ledger.At("slot1"), Is.Null); Assert.That(ledger.At("slot2"), Is.SameAs(item)); Assert.That(ledger.Items.Count, Is.EqualTo(1));
        }
        [Test] public void OccupiedTargetRejectsMoveAndPreservesOrigin()
        {
            var ledger = new PlacementLedger(Definitions()); var first = new ItemState { definitionId = "cup" }; var second = new ItemState { definitionId = "cup" };
            ledger.Place(first, "slot1"); ledger.Place(second, "slot2"); Assert.That(ledger.Place(first, "slot2").Success, Is.False);
            Assert.That(ledger.At("slot1"), Is.SameAs(first)); Assert.That(ledger.At("slot2"), Is.SameAs(second));
        }
        [Test] public void WrongItemRejectsProvisionalPlacementWithoutAllocatingAnId()
        {
            var ledger = new PlacementLedger(Definitions()); var item = new ItemState { definitionId = "bean" };
            Assert.That(ledger.Place(item, "slot1").Success, Is.False); Assert.That(item.instanceId, Is.Null); Assert.That(ledger.Items, Is.Empty);
        }
        [Test] public void PlacementSavePreservesIdentityContentsAndLocation()
        {
            var ledger = new PlacementLedger(Definitions()); var item = new ItemState { definitionId = "cup", bindingId = "order42" }; item.contents.Add("milk"); ledger.Place(item, "slot2");
            var saved = JsonUtility.FromJson<PlacementSnapshot>(JsonUtility.ToJson(ledger.Capture())); var restored = new PlacementLedger(Definitions());
            Assert.That(restored.Restore(saved).Success); Assert.That(restored.At("slot2").contents.Single(), Is.EqualTo("milk")); Assert.That(restored.At("slot2").bindingId, Is.EqualTo("order42"));
        }
        [Test] public void InvalidRestoreDoesNotReplaceTheCurrentLedger()
        {
            var ledger = new PlacementLedger(Definitions()); var item = new ItemState { definitionId = "cup" }; ledger.Place(item, "slot1");
            var snapshot = new PlacementSnapshot(); snapshot.items.Add(new ItemState { instanceId = "bad", definitionId = "cup", locationId = "missing" });
            Assert.That(ledger.Restore(snapshot).Success, Is.False); Assert.That(ledger.At("slot1"), Is.SameAs(item));
        }
        [Test] public void DuplicateInstanceIdsAreRejected()
        {
            var ledger = new PlacementLedger(Definitions()); var snapshot = new PlacementSnapshot();
            snapshot.items.Add(new ItemState { instanceId = "same", definitionId = "cup", locationId = "slot1" }); snapshot.items.Add(new ItemState { instanceId = "same", definitionId = "cup", locationId = "slot2" });
            Assert.That(ledger.Restore(snapshot).Success, Is.False); Assert.That(ledger.Items, Is.Empty);
        }
        [Test] public void UnknownLegacyRecipeTokenFailsInsteadOfSkippingIt() => Assert.Throws<FormatException>(() => RecipeStepParser.Parse("select_cup|unknown_action|serve"));
        [Test] public void InvalidNumericRecipeParameterFails() => Assert.Throws<FormatException>(() => RecipeStepParser.Parse("grind_click_zero"));
        [Test] public void AllRecipesUseStructuredStepsAndEndInServe()
        {
            foreach (var recipe in GameplayConfigService.AllRecipes) { var steps = RecipeStepParser.ForRecipe(recipe); Assert.That(steps.First().Kind, Is.EqualTo(RecipeStepKind.SelectCup)); Assert.That(steps.Last().Kind, Is.EqualTo(RecipeStepKind.Serve)); Assert.That(recipe.steps, Is.Empty); }
        }
        private static CraftingSession Espresso(PlayerProfile profile)
        {
            var session = new CraftingSession(new CustomerOrder { RecipeId = "espresso" }, profile); session.SelectCup(); return session;
        }
        [Test] public void MilkCannotBypassACoffeeBeanStep()
        {
            var profile = PlayerProfile.CreateDefault(); var session = Espresso(profile); int milk = profile.GetStock("milk"), gold = profile.Gold;
            Assert.That(session.AddIngredient("milk").Success, Is.False); Assert.That(profile.GetStock("milk"), Is.EqualTo(milk)); Assert.That(profile.Gold, Is.EqualTo(gold)); Assert.That(session.CurrentStep.Kind, Is.EqualTo(RecipeStepKind.AddIngredient));
        }
        [Test] public void EmptyStockDoesNotAutoSpendOrAdvance()
        {
            var profile = PlayerProfile.CreateDefault(); profile.IngredientStock["coffee_bean"] = 0; var session = Espresso(profile); int gold = profile.Gold;
            Assert.That(session.AddIngredient("coffee_bean").Success, Is.False); Assert.That(profile.Gold, Is.EqualTo(gold)); Assert.That(session.CurrentStep.Kind, Is.EqualTo(RecipeStepKind.AddIngredient));
        }
        [Test] public void CorrectIngredientIsConsumedExactlyOnce()
        {
            var profile = PlayerProfile.CreateDefault(); var session = Espresso(profile); int stock = profile.GetStock("coffee_bean");
            Assert.That(session.AddIngredient("coffee_bean").Success); Assert.That(session.AddIngredient("coffee_bean").Success, Is.False);
            Assert.That(profile.GetStock("coffee_bean"), Is.EqualTo(stock - 1)); Assert.That(session.CurrentStep.Kind, Is.EqualTo(RecipeStepKind.BeanSelection));
        }
        [Test] public void PrepaidIngredientDoesNotConsumeStockAgain()
        {
            var profile = PlayerProfile.CreateDefault(); var session = Espresso(profile); int stock = profile.GetStock("coffee_bean"); profile.Consume("coffee_bean", 1);
            Assert.That(session.AddIngredient("coffee_bean", true).Success); Assert.That(profile.GetStock("coffee_bean"), Is.EqualTo(stock - 1));
        }
        [Test] public void WrongCupSizeIsRejected()
        {
            var session = new CraftingSession(new CustomerOrder { RecipeId = "espresso" }, PlayerProfile.CreateDefault()); Assert.That(session.CanSelectCup("large").Success, Is.False); Assert.That(session.CanSelectCup("small").Success);
        }
        [Test] public void SkillPointsFollowReputationThresholdsAcrossDays()
        {
            var profile = PlayerProfile.CreateDefault(); profile.Reputation = 9; var orders = new OrderManager(profile); var day = new DayCycleController(profile, orders);
            day.StartDay(); orders.Restore(new[] { new CustomerOrder { OrderId = 1, RecipeId = "espresso", State = OrderState.Served } }); day.EndDay();
            Assert.That(profile.Reputation, Is.EqualTo(10)); Assert.That(profile.SkillPoints, Is.EqualTo(1));
            day.StartDay(); orders.Restore(new[] { new CustomerOrder { OrderId = 2, RecipeId = "espresso", State = OrderState.Served } }); day.EndDay();
            Assert.That(profile.Reputation, Is.EqualTo(11)); Assert.That(profile.SkillPoints, Is.EqualTo(1));
        }
        [Test] public void SessionRoundTripRetainsStepAndQuality()
        {
            var profile = PlayerProfile.CreateDefault(); var session = Espresso(profile); session.AddIngredient("coffee_bean"); session.CompleteBeanSelection(true, 80);
            var state = JsonUtility.FromJson<CraftingSnapshot>(JsonUtility.ToJson(session.Capture())); var restored = new CraftingSession(new CustomerOrder { RecipeId = "espresso" }, profile); restored.Restore(state);
            Assert.That(restored.CurrentStep.Kind, Is.EqualTo(session.CurrentStep.Kind)); Assert.That(restored.QualityMultiplier, Is.EqualTo(session.QualityMultiplier));
        }
        [Test] public void GeneratedVisitorsHaveStableNpcIdentity()
        {
            var orders = new OrderManager(PlayerProfile.CreateDefault()); orders.GenerateDailyOrders(0); Assert.That(orders.Orders, Is.Not.Empty);
            foreach (var order in orders.Orders) { Assert.That(GameplayConfigService.GetNpc(order.NpcId), Is.Not.Null); Assert.That(order.CustomerId, Is.GreaterThan(0)); }
        }
    }
}
