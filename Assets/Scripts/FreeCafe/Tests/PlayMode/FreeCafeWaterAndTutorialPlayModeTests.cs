using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeWaterAndTutorialPlayModeTests
    {
        private FreeCafeRuntime app;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1;
            Spotlight.GameFlow.GameSaveParticipantRegistry.PendingRestore = null;
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop"); yield return null; yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            app.areaManager.panSmoothTime = 0; app.Enter(AreaId.Counter); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            app.WorkstationTutorial.Stop(); app.CancelDrag(); Time.timeScale = 1; yield return null;
        }
        private static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/WaterAndWaste"));
            Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
        [UnityTest] public IEnumerator WaterUsesItsOwnContainerAndWasteAcceptsTheEnlargedLowerLeftTarget()
        {
            var world = app.World;
            var water = app.View("s.water"); var heater = app.View("m.heater"); var waste = app.View("waste");
            Assert.That(water.transform.position.x, Is.GreaterThan(heater.transform.position.x));

            Assert.That(water.body.sprite.name, Is.EqualTo("default"));
            Assert.That(waste.hitbox.size.x, Is.GreaterThanOrEqualTo(1.9f));
            Assert.That(waste.hitbox.size.y, Is.GreaterThanOrEqualTo(1.8f));
            Assert.That(world.Find("waste").x, Is.LessThan(0)); Assert.That(world.Find("waste").y, Is.LessThan(0));
            float homeX = world.Find("s.water").x;
            yield return Capture("workstation-water-and-waste");
            Physics2D.SyncTransforms();
            app.BeginDrag(water, new PointerEventData(EventSystem.current) { position = app.worldCamera.WorldToScreenPoint(water.transform.position) });
            Assert.That(app.IsDragging, Is.True);
            Assert.That(GameObject.Find("UncommittedPickup"), Is.Null, "Water is the actual reusable container, not an ingredient ghost");
            Assert.That(world.PourLiquid("s.water", "m.heater", 200, 1).success, Is.True);
            app.CancelDrag(); yield return null;
            Assert.That(world.CanPour(world.Find("s.water")), Is.True);
            Assert.That(world.Find("s.water").x, Is.EqualTo(homeX).Within(.001f));
            Assert.That(world.Take("s.beans", AreaId.Counter, -7, -4, out var beans).success, Is.True);
            app.DebugRefreshViews(); yield return null;
            var beanView = app.View(beans.id);
            app.BeginDrag(beanView, new PointerEventData(EventSystem.current) { position = app.worldCamera.WorldToScreenPoint(beanView.transform.position) });
            Vector3 edge = waste.transform.TransformPoint(new Vector3(.8f, .5f));
            var pointer = new PointerEventData(EventSystem.current) { position = app.worldCamera.WorldToScreenPoint(edge) };
            Physics2D.SyncTransforms(); app.MoveDrag(pointer); app.EndDrag(pointer); yield return null;
            Assert.That(world.Find(beans.id), Is.Null, "Dropping near the expanded rim must discard the ingredient");
        }
        [UnityTest] public IEnumerator AuthoredPresentationAndSupplyHomesSurviveBindingRefreshAndOldSaveRestore()
        {
            foreach (string id in new[] { "s.water", "waste" })
            {
                var entity = app.World.Find(id);
                var prefab = app.prefabLibrary.Require(entity.specId);
                var authored = Object.Instantiate(prefab, app.areaRoots[(int)entity.area]);
                authored.ConfigureAuthoring(app.World.Spec(entity.specId), app.artLibrary, id, entity.x, entity.y);
                var artScale = new Vector3(.345f, .381f, 1);
                var artPosition = new Vector3(.46f, .69f, 0);
                var boxSize = new Vector2(2.3f, 2.1f);
                var boxOffset = new Vector2(.4f, .3f);
                authored.body.transform.localScale = artScale;
                authored.body.transform.localPosition = artPosition;
                authored.hitbox.size = boxSize;
                authored.hitbox.offset = boxOffset;
                var captionPosition = new Vector3(.2f, -1.5f, 0);
                authored.label.transform.localPosition = captionPosition;
                authored.Bind(app, entity, true);
                authored.Refresh(entity);
                Assert.That(Vector3.Distance(authored.body.transform.localScale, artScale), Is.LessThan(.0001f));
                Assert.That(authored.body.transform.localPosition, Is.EqualTo(artPosition));
                Assert.That(authored.hitbox.size, Is.EqualTo(boxSize));
                Assert.That(authored.hitbox.offset, Is.EqualTo(boxOffset));
                Assert.That(authored.label.transform.localPosition, Is.EqualTo(captionPosition));
                Object.Destroy(authored.gameObject);
            }
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(app.CaptureJson());
            foreach (string id in new[] { "s.water", "waste" })
            {
                var entry = snapshot.entities.Find(e => e.id == id);
                entry.area = AreaId.Cafe; entry.x = 0; entry.y = 0;
            }
            app.RestoreJson(JsonUtility.ToJson(snapshot));
            yield return null;
            foreach (string id in new[] { "s.water", "waste" })
            {
                var initial = System.Array.Find(app.World.Catalog.layout, e => e.id == id);
                var entity = app.World.Find(id);
                Assert.That(entity.area, Is.EqualTo(initial.area));
                Assert.That(entity.x, Is.EqualTo(initial.x).Within(.0001f));
                Assert.That(entity.y, Is.EqualTo(initial.y).Within(.0001f));
            }
        }
        [UnityTest] public IEnumerator RestartButtonIsAlwaysAvailableAndRestoresThePracticeWhileDragging()
        {
            var guide = app.WorkstationTutorial; var world = app.World;
            Assert.That(guide.Begin(false), Is.True); yield return null;
            var panel = Object.FindFirstObjectByType<FreeCafeTutorialPanel>();
            Assert.That(panel.retry.activeInHierarchy, Is.True);
            Assert.That(panel.retryButton.interactable, Is.True);
            int money = world.State.money, stock = world.Find("s.beans").stock;
            Assert.That(world.TakeAndInteract("s.beans", "m.grinder", AreaId.Counter, 0, 0).success, Is.True); yield return null;
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Grind));
            panel.retryButton.onClick.Invoke(); yield return null;
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Beans));
            Assert.That(world.Find("s.beans").stock, Is.EqualTo(stock));
            Assert.That(world.Find("m.grinder").contents, Is.Empty);
            Assert.That(world.State.money, Is.EqualTo(money));
            typeof(FreeCafeWorkstationTutorial).GetMethod("SetStage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(guide, new object[] { FreeCafeWorkstationTutorial.Stage.Water });
            var water = app.View("s.water"); Physics2D.SyncTransforms();
            app.BeginDrag(water, new PointerEventData(EventSystem.current) { position = app.worldCamera.WorldToScreenPoint(water.transform.position) });
            Assert.That(app.IsDragging, Is.True);
            world.PourLiquid("s.water", "m.heater", 50, 1);
            panel.retryButton.onClick.Invoke(); yield return null;
            Assert.That(app.IsDragging, Is.False);
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Beans));
            Assert.That(world.LiquidVolume(world.Find("m.heater")), Is.Zero);
            Assert.That(panel.retry.activeInHierarchy, Is.True);
            yield return Capture("tutorial-persistent-restart");
        }
        [UnityTest] public IEnumerator TutorialWaterStepMeasuresTheReceiverRatherThanEmptyingUnlimitedWater()
        {
            var guide = app.WorkstationTutorial; var world = app.World;
            Assert.That(guide.Begin(true), Is.True);
            typeof(FreeCafeWorkstationTutorial).GetMethod("SetStage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(guide, new object[] { FreeCafeWorkstationTutorial.Stage.Water });
            Assert.That(guide.AllowPour("s.water", "m.heater"), Is.True);
            Assert.That(guide.AllowPour("s.water", "waste"), Is.False);
            // The following domain commands model measured water without holding a hardware mouse button.
            typeof(FreeCafeWorkstationTutorial).GetMethod("SetStage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(guide, new object[] { FreeCafeWorkstationTutorial.Stage.Water });
            world.PourLiquid("s.water", "m.heater", 150, 1); yield return null;
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Water));
            yield return Capture("tutorial-measured-water");
            Assert.That(guide.LimitPourAmount("s.water", 100), Is.EqualTo(50).Within(.001f));
            world.PourLiquid("s.water", "m.heater", 50, 1); yield return null; yield return null;
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Heat));
            Assert.That(world.CanPour(world.Find("s.water")), Is.True);
            var panel = Object.FindFirstObjectByType<FreeCafeTutorialPanel>();
            Assert.That(panel.retry.activeInHierarchy, Is.True);
            panel.retryButton.onClick.Invoke(); yield return null;
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Beans));
        }
    }
}
