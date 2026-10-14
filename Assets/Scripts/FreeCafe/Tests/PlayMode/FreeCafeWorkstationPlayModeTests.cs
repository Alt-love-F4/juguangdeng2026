using System.Collections;
using System.IO;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeWorkstationPlayModeTests
    {
        private FreeCafeRuntime app;
        [UnitySetUp] public IEnumerator Setup()
        {
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None)) panel.Cancel();
            GameSaveParticipantRegistry.PendingRestore = null; Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            app.areaManager.panSmoothTime = 0; app.World.EnterProduction();
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (app != null) app.CancelDrag(); Time.timeScale = 1; yield return null;
        }
        private PointerEventData Pointer(WorldEntity entity) => new PointerEventData(EventSystem.current)
        { position = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(entity.area, entity.x, entity.y)) };

        [UnityTest] public IEnumerator ActualKettleDropReturnsHomeAndClickHeatsItsContents()
        {
            Assert.That(app.World.TakeAndInteract("s.water", "m.heater", AreaId.Counter, 0, 0).success, Is.True);
            var kettle = app.World.Find("m.heater"); var stove = app.World.Find("m.stove");
            float homeX = kettle.x, homeY = kettle.y;
            Physics2D.SyncTransforms(); app.BeginDrag(app.View(kettle.id), Pointer(kettle)); app.EndDrag(Pointer(stove));
            Assert.That(stove.production.heatingVesselId, Is.Null);
            Assert.That(kettle.x, Is.EqualTo(homeX)); Assert.That(kettle.y, Is.EqualTo(homeY));
            yield return null; // 松手帧抑制点击，下一帧才是独立的炉台操作。
            app.Click(app.View(kettle.id)); Assert.That(kettle.production.phase, Is.EqualTo(ProductionPhase.Processing));
            app.World.Tick(4); yield return null;
            Assert.That(kettle.contents[0].temperature, Is.EqualTo(LiquidTemperature.Boiling));
        }
        [UnityTest] public IEnumerator RealCupUsesOriginalArtOnlyShowingLiquidBoxDuringPouring()
        {
            app.World.SelectServingCup("s.cups.large"); var view = app.View(app.World.ServingCup.id);
            var presenter = (OrthographicGlassCupPresenter)view.liquidPresenter;
            Assert.That(view.body.enabled, Is.True); Assert.That(view.body.sprite, Is.Not.Null);
            Assert.That(presenter.gameObject.activeSelf, Is.False);
            view.PresentPour(.6f, true); yield return null;
            Assert.That(presenter.gameObject.activeSelf, Is.True); Assert.That(view.body.color.a, Is.EqualTo(.22f).Within(.001f));
            view.PresentPour(0, false); yield return null;
            Assert.That(presenter.gameObject.activeSelf, Is.False); Assert.That(view.body.color.a, Is.EqualTo(1));
            view.SetHovered(true);
            Assert.That(view.transform.Find("FillStatus").gameObject.activeSelf, Is.True);
            view.SetHovered(false);
            Assert.That(view.transform.Find("FillStatus").gameObject.activeSelf, Is.False);
        }
        [UnityTest] public IEnumerator AuthoredCounterSourcesMatchPsdLayoutAndMissingStoveArtIsPreserved()
        {
            foreach (string entityId in new[] { "s.beans", "s.water", "s.milk", "s.sugar", "s.cocoa", "s.matcha", "s.honey", "s.cream" })
            {
                var entity = app.World.Find(entityId); var view = app.View(entityId);
                var entry = System.Array.Find(app.World.Catalog.layout, e => e.id == entityId);
                Assert.That(entity.x, Is.EqualTo(entry.x).Within(.001f)); Assert.That(entity.y, Is.EqualTo(entry.y).Within(.001f));
                Assert.That(view.body.sprite, Is.Not.Null);
                Assert.That(view.transform.lossyScale.x / app.areaRoots[1].lossyScale.x, Is.EqualTo(1).Within(.001f));
            }
            Assert.That(app.View("m.stove").body.sprite.name, Is.EqualTo("default"));
            Assert.That(app.World.Find("service").x, Is.GreaterThan(7));
            yield return null;
        }
        [UnityTest] public IEnumerator CaptureIllustratedWorkstationWithOriginalCupAndActualHud()
        {
            app.World.SelectServingCup("s.cups.large");
            app.World.TakeAndInteract("s.water", "m.brewer", AreaId.Counter, 0, 0);
            app.ShowHint("自由加入原料 · 粉与水混合是冷萃 · 容器放到炉台后点击加热 · 悬停看容量");
            yield return new WaitForSecondsRealtime(.5f);
            string folder = Path.GetFullPath("output/production-redesign"); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "workstation.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(File.Exists(path), Is.True);
        }
    }
}
