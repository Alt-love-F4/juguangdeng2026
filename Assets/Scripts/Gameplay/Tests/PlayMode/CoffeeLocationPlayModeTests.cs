using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Spotlight.GameFlow;
using Spotlight.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.Gameplay.Tests
{
    public sealed class CoffeeLocationPlayModeTests
    {
        [UnitySetUp] public IEnumerator LoadCafe()
        {
            Time.timeScale = 1; GameSaveParticipantRegistry.PendingRestore = null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null; yield return null;
        }
        [UnityTearDown] public IEnumerator RestoreTime()
        {
            Time.timeScale = 1;
            yield return null;
        }
        [UnityTest] public IEnumerator NavigationAndWorldPortalUseActualRaycastPaths()
        {
            var locations = Object.FindFirstObjectByType<CoffeeLocationController>();
            var nav = Object.FindFirstObjectByType<CoffeeLocationNavigationUI>();
            var hud = Object.FindFirstObjectByType<CoffeeGameplayPanel>();
            Assert.That(locations.CurrentLocation, Is.EqualTo(CoffeeLocationId.FirstFloorCafe));
            Assert.That(hud.gameObject.activeInHierarchy, Is.True);
            var portal = Object.FindFirstObjectByType<CoffeeLocationPortal>();
            Physics2D.SyncTransforms();
            var pointer = new PointerEventData(EventSystem.current) { position = Camera.main.WorldToScreenPoint(portal.transform.position) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.GetComponentInParent<CoffeeLocationPortal>(), Is.EqualTo(portal), "An overlay blocked the world portal");
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(locations.CurrentLocation, Is.EqualTo(CoffeeLocationId.FirstFloorServingCounter));
            Assert.That(hud.gameObject.activeInHierarchy, Is.True, "Shared HUD disappeared in counter");
            foreach (var pair in new[] { (nav.secondFloorButton, CoffeeLocationId.SecondFloor), (nav.basementButton, CoffeeLocationId.Basement), (nav.cafeButton, CoffeeLocationId.FirstFloorCafe), (nav.counterButton, CoffeeLocationId.FirstFloorServingCounter) })
            {
                Canvas.ForceUpdateCanvases();
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, pair.Item1.transform.position);
                hits.Clear(); EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits.Count, Is.GreaterThan(0));
                Assert.That(hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>(), Is.EqualTo(pair.Item1), "Navigation blocked by another UI");
                ExecuteEvents.Execute(pair.Item1.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(locations.CurrentLocation, Is.EqualTo(pair.Item2));
                Assert.That(locations.locations.Count(x => x.root.activeInHierarchy), Is.EqualTo(1));
                Assert.That(hud.gameObject.activeInHierarchy, Is.True);
                var entities = Object.FindFirstObjectByType<CoffeeWorldRuntime>().transform.Find("ServingCounterEntities");
                Assert.That(entities.gameObject.activeInHierarchy, Is.EqualTo(pair.Item2 == CoffeeLocationId.FirstFloorServingCounter));
                yield return null;
            }
            // Camera viewport and floor buttons occupy separate rendering/input regions.
            Assert.That(Camera.main.rect.y, Is.EqualTo(160f / 1080f).Within(.001f));
            Assert.That(Camera.main.GetComponent<CoffeeWorldViewport>(), Is.Not.Null);
        }
        [UnityTest] public IEnumerator SwitchingRetainsItemsOrdersStockAndCancelsDrag()
        {
            var locations = Object.FindFirstObjectByType<CoffeeLocationController>();
            var world = Object.FindFirstObjectByType<CoffeeWorldRuntime>();
            var ctrl = CoffeeGameplayController.Instance;
            ctrl.Profile.Reputation = 40; ctrl.StartDay();
            var recipe = GameplayConfigService.AllRecipes.First();
            ctrl.Orders.Restore(new[] { new CustomerOrder { OrderId = 901, RecipeId = recipe.id, NpcId = "student", CustomerId = 1, CustomerName = "测试顾客" } });
            ctrl.TakeNextOrder(); locations.Enter(CoffeeLocationId.FirstFloorServingCounter);
            world.SelectCupFromButton(); Assert.That(world.ActiveCup, Is.Not.Null);
            string cup = world.ActiveCup.instanceId, placements = world.CaptureJson();
            int stock = ctrl.Profile.GetStock("coffee_bean"), order = ctrl.CurrentOrder.OrderId;
            var source = world.GetComponentsInChildren<ItemSourceEntity>().First(x => x.definitionId == "source.coffee_bean");
            var pointer = new PointerEventData(EventSystem.current) { position = Camera.main.WorldToScreenPoint(source.transform.position) };
            world.BeginDrag(source, pointer); Assert.That(world.IsDragging, Is.True);
            locations.Enter(CoffeeLocationId.SecondFloor);
            Assert.That(world.IsDragging, Is.False);
            foreach (var id in new[] { CoffeeLocationId.Basement, CoffeeLocationId.FirstFloorCafe, CoffeeLocationId.FirstFloorServingCounter })
            {
                locations.Enter(id); yield return null;
                Assert.That(world.CaptureJson(), Is.EqualTo(placements));
                Assert.That(ctrl.Profile.GetStock("coffee_bean"), Is.EqualTo(stock));
                Assert.That(ctrl.CurrentOrder.OrderId, Is.EqualTo(order));
                Assert.That(world.ActiveCup.instanceId, Is.EqualTo(cup));
            }
            var view = world.GetComponentsInChildren<CarryableEntity>().First(x => x.Item.instanceId == cup);
            var origin = view.transform.position;
            pointer.position = Camera.main.WorldToScreenPoint(origin); world.BeginDrag(view, pointer);
            Assert.That(world.IsDragging, Is.True);
            pointer.position += new Vector2(40, 40); world.MoveDrag(pointer);
            locations.Enter(CoffeeLocationId.FirstFloorCafe);
            Assert.That(world.IsDragging, Is.False); Assert.That(view.transform.position, Is.EqualTo(origin));
            Assert.That(view.gameObject.activeInHierarchy, Is.False);
            locations.Enter(CoffeeLocationId.FirstFloorServingCounter); Assert.That(view.gameObject.activeInHierarchy, Is.True);
            Time.timeScale = 0; locations.Enter(CoffeeLocationId.Basement);
            Assert.That(locations.CurrentLocation, Is.EqualTo(CoffeeLocationId.FirstFloorServingCounter));
            Time.timeScale = 1;
            // Modal navigation is locked without destroying an unfinished minigame.
            while (ctrl.Session.CurrentStep.Kind != RecipeStepKind.BeanSelection && ctrl.Session.CurrentStep.Kind == RecipeStepKind.AddIngredient)
                world.AddFromButton(ctrl.Session.CurrentStep.IngredientId);
            if (ctrl.Session.CurrentStep.Kind == RecipeStepKind.BeanSelection)
            {
                var panel = Object.FindFirstObjectByType<CoffeeGameplayPanel>(); panel.OpenStepMinigame();
                Assert.That(CoffeeGameplayPanel.IsMinigameOpen, Is.True);
                locations.Enter(CoffeeLocationId.Basement);
                Assert.That(locations.CurrentLocation, Is.EqualTo(CoffeeLocationId.FirstFloorServingCounter));
                Assert.That(panel.beanPopup.activeInHierarchy, Is.True);
                foreach (var cell in panel.beanSelectionPanel.grid.GetComponentsInChildren<BeanSelectionMinigame.BeanCell>())
                    if (cell.IsGood) cell.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                panel.beanSelectionPanel.confirmButton.onClick.Invoke();
            }
            Debug.Log("[CoffeeLocationVerification] Raycasts, visibility, persistent items/orders/stock, cancel drag and pause passed.");
            yield return null;
        }
        [Test] public void SharedVisibleRootUsesUnionInsteadOfLastBindingWins()
        {
            var shared = new GameObject("SharedHudTest");
            var a = new GameObject("AreaATest"); var b = new GameObject("AreaBTest");
            try
            {
                var bindings = new[] {
                    new CoffeeLocationController.LocationBinding { id = CoffeeLocationId.FirstFloorCafe, root = a, visibleRoots = new[] { shared } },
                    new CoffeeLocationController.LocationBinding { id = CoffeeLocationId.FirstFloorServingCounter, root = b, visibleRoots = new[] { shared } }
                };
                CoffeeLocationController.ApplyVisibility(bindings, CoffeeLocationId.FirstFloorCafe);
                Assert.That(shared.activeSelf, Is.True); Assert.That(a.activeSelf, Is.True); Assert.That(b.activeSelf, Is.False);
                CoffeeLocationController.ApplyVisibility(bindings, CoffeeLocationId.FirstFloorServingCounter);
                Assert.That(shared.activeSelf, Is.True); Assert.That(a.activeSelf, Is.False); Assert.That(b.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(shared); Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }
    }
}
