using System.Collections;
using System.Linq;
using NUnit.Framework;
using Spotlight.GameFlow;
using Spotlight.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Spotlight.Gameplay.Tests
{
    public sealed class CafePlayModeTests
    {
        [UnityTest] public IEnumerator AllSevenRecipesUsePhysicalItemsRealMinigamesAndSaveRoundTrips()
        {
            Time.timeScale = 1; GameSaveParticipantRegistry.PendingRestore = null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null; yield return null;
            var ctrl = CoffeeGameplayController.Instance; var world = Object.FindFirstObjectByType<CoffeeWorldRuntime>(); var panel = Object.FindFirstObjectByType<CoffeeGameplayPanel>();
            Assert.That(world, Is.Not.Null); Assert.That(panel.beanSelectionPanel, Is.Not.Null); Assert.That(panel.latteArtPanel, Is.Not.Null); Assert.That(panel.cocoaQTEPanel, Is.Not.Null);
            ctrl.Profile.Reputation = 40; ctrl.StartDay();
            // A source stays fixed and canceled pickup changes neither stock nor committed placements.
            var source = world.GetComponentsInChildren<ItemSourceEntity>().First(e => e.definitionId == "source.coffee_bean");
            int stock = ctrl.Profile.GetStock("coffee_bean"); var position = source.transform.position;
            var pointer = new PointerEventData(EventSystem.current) { position = Camera.main.WorldToScreenPoint(position) };
            world.BeginDrag(source, pointer); Assert.That(world.IsDragging); world.CancelDrag(); yield return null;
            Assert.That(ctrl.Profile.GetStock("coffee_bean"), Is.EqualTo(stock)); Assert.That(source.transform.position, Is.EqualTo(position)); Assert.That(world.Ledger.Items.Count, Is.EqualTo(0));
            int completed = 0;
            foreach (var recipe in GameplayConfigService.AllRecipes.ToArray())
            {
                ctrl.Orders.Restore(new[] { new CustomerOrder { OrderId = ++completed, RecipeId = recipe.id, NpcId = "student", CustomerId = 1, CustomerName = "林同学" } });
                ctrl.TakeNextOrder(); yield return null;
                int goldBefore = ctrl.Profile.Gold;
                int guard = 0;
                while (ctrl.Session != null && guard++ < 40)
                {
                    var step = ctrl.Session.CurrentStep;
                    switch (step.Kind)
                    {
                        case RecipeStepKind.SelectCup:
                            if (completed == 1)
                            {
                                var cupSource = world.GetComponentsInChildren<ItemSourceEntity>().First(e => e.definitionId == "source.small");
                                var bench = world.GetComponentsInChildren<PlacementEntity>().First(e => e.definitionId == "bench.1");
                                pointer.position = Camera.main.WorldToScreenPoint(cupSource.transform.position); world.BeginDrag(cupSource, pointer);
                                pointer.position = Camera.main.WorldToScreenPoint(bench.transform.position); Physics2D.SyncTransforms(); world.MoveDrag(pointer); world.EndDrag(pointer);
                            }
                            else panel.selectCupButton.onClick.Invoke();
                            Assert.That(world.ActiveCup, Is.Not.Null, "Physical cup was not created for " + recipe.id);
                            break;
                        case RecipeStepKind.AddIngredient: world.AddFromButton(step.IngredientId); break;
                        case RecipeStepKind.BeanSelection:
                            panel.OpenStepMinigame(); Assert.That(CoffeeGameplayPanel.IsMinigameOpen);
                            foreach (var cell in panel.beanSelectionPanel.grid.GetComponentsInChildren<BeanSelectionMinigame.BeanCell>()) if (cell.IsGood) cell.GetComponent<Button>().onClick.Invoke();
                            panel.beanSelectionPanel.confirmButton.onClick.Invoke(); break;
                        case RecipeStepKind.GrindClick: world.UseCurrentDeviceFromButton(); break;
                        case RecipeStepKind.Process:
                            world.UseCurrentDeviceFromButton(); Assert.That(ctrl.IsProcessing);
                            var cupId = world.ActiveCup.instanceId; string save = ctrl.CaptureProfileJson(), placements = world.CaptureJson();
                            ctrl.RestoreProfileJson(save); world.RestoreJson(placements);
                            Assert.That(world.ActiveCup.instanceId, Is.EqualTo(cupId)); Assert.That(ctrl.IsProcessing); Assert.That(ctrl.Session.CurrentStep.DeviceId, Is.EqualTo(step.DeviceId));
                            ctrl.Tick(ctrl.TimedStepDuration + .1f); break;
                        case RecipeStepKind.CocoaMinigame:
                            world.UseCurrentDeviceFromButton(); Assert.That(CoffeeGameplayPanel.IsMinigameOpen);
                            var qte = panel.cocoaQTEPanel; qte.trackSlider.value = qte.targetZone.rectTransform.anchoredPosition.x / ((RectTransform)qte.trackSlider.transform).rect.width; qte.actionButton.onClick.Invoke(); break;
                        case RecipeStepKind.LatteArt:
                            panel.OpenStepMinigame(); var art = panel.latteArtPanel;
                            var rect = art.canvasImage.rectTransform;
                            pointer.position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(-100, -50, 0))); art.OnPointerDown(pointer);
                            foreach (var point in new[] { new Vector3(100, -50), new Vector3(-100, 20), new Vector3(100, 70) }) { pointer.position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(point)); art.OnDrag(pointer); }
                            art.OnPointerUp(pointer); art.finishButton.onClick.Invoke(); Assert.That(ctrl.CurrentLatteArtShape, Is.EqualTo("自由拉花")); break;
                        case RecipeStepKind.Serve: panel.serveButton.onClick.Invoke(); break;
                        default: Assert.Fail("Unhandled recipe step " + step.Kind); break;
                    }
                    yield return null;
                }
                Assert.That(guard, Is.LessThan(40), "Recipe did not finish: " + recipe.id);
                Assert.That(ctrl.Session, Is.Null); Assert.That(ctrl.Orders.Orders.Single().State, Is.EqualTo(OrderState.Served), recipe.id);
                Assert.That(ctrl.Profile.Gold, Is.GreaterThan(goldBefore)); Assert.That(world.Ledger.Items.Count, Is.EqualTo(0), "Served cup was not removed");
            }
            ctrl.EndDay(); Assert.That(ctrl.Day.Phase, Is.EqualTo(DayPhase.Ended)); Assert.That(ctrl.Profile.TodayRevenue, Is.GreaterThan(0));
            ctrl.StartDay(); Assert.That(ctrl.Day.DayNumber, Is.EqualTo(2));
            ctrl.Profile.IngredientStock["milk"] = 0; int balance = ctrl.Profile.Gold; ctrl.RefillIngredient("milk"); Assert.That(ctrl.Profile.GetStock("milk"), Is.EqualTo(GameplayConfigService.GetIngredient("milk").max_stock)); Assert.That(ctrl.Profile.Gold, Is.LessThan(balance));
            ctrl.Profile.SkillPoints = 1; ctrl.UpgradeDevice("moka_pot"); Assert.That(ctrl.Profile.UpgradedDevices.ContainsKey("moka_pot"));
            panel.pauseButton.onClick.Invoke(); Assert.That(Time.timeScale, Is.EqualTo(0)); panel.resumeButton.onClick.Invoke(); Assert.That(Time.timeScale, Is.EqualTo(1));
            var data = new GameSaveData { SceneName = "CoffeeShop", Participants = GameSaveParticipantRegistry.CaptureAll() };
            Assert.That(data.Participants.Any(p => p.Id == "gameplay")); Assert.That(data.Participants.Any(p => p.Id == "world.placements"));
            Debug.Log("[CafeVerification] All 7 recipes, actual drag/cancel, all 3 minigames, save roundtrip, refill, upgrade, day transition and pause passed.");
        }
    }
}
