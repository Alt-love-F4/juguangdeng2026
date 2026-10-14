using System.Collections;
using System.IO;
using NUnit.Framework;
using Spotlight.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeSplitExtractionPlayModeTests
    {
        private FreeCafeRuntime app;
        [UnitySetUp] public IEnumerator Setup()
        {
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None)) panel.Cancel();
            Time.timeScale = 1; GameSaveParticipantRegistry.PendingRestore = null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop"); yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            app.areaManager.panSmoothTime = 0; app.World.EnterProduction();
            yield return null; app.enabled = false;
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            app.CancelDrag(); Time.timeScale = 1; yield return null;
        }

        private void Grounds()
        {
            app.World.State.entities.Add(new WorldEntity { id = "split.dose", specId = "grounds", units = 1, area = AreaId.Counter });
            Assert.That(app.World.Interact("split.dose", "v.extractor").success, Is.True);
        }

        private void Water()
        {
            var heater = app.World.Find("m.heater"); heater.contents.Clear();
            heater.contents.Add(new Portion { specId = "hotwater", units = 1, milliliters = 200, temperature = LiquidTemperature.Boiling });
            heater.production.phase = ProductionPhase.Room;
        }

        [UnityTest] public IEnumerator ExtractionCardIsOnlyVisibleOnHoverOrReceivingWater()
        {
            Grounds(); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(app.ContainerPanels.GetComponentsInChildren<TMP_Text>().Length, Is.Zero);
            var jug = app.View("v.extractor"); jug.SetHovered(true);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(app.ContainerPanels.GetComponentsInChildren<TMP_Text>().Length, Is.GreaterThan(0));
            jug.SetHovered(false); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(app.ContainerPanels.GetComponentsInChildren<TMP_Text>().Length, Is.Zero);
            var filter = app.View("v.extractor.filter"); filter.SetReceivingPour(true);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(app.ContainerPanels.GetComponentsInChildren<TMP_Text>().Length, Is.GreaterThan(0));
            filter.SetReceivingPour(false); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(app.ContainerPanels.GetComponentsInChildren<TMP_Text>().Length, Is.Zero);
        }

        [UnityTest] public IEnumerator PartialStopKeepsFilterAndReceivingCardAvailableForContinuation()
        {
            Grounds();Water();var world=app.World;var jug=world.Find("v.extractor");var filter=world.ExtractionFilter(jug);
            world.PourLiquid("m.heater",jug.id,150,5.625f);world.Tick(5.625f);world.FinishPour(filter.id);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(world.ExtractionFilter(jug),Is.SameAs(filter));
            Assert.That(app.View(filter.id).CanInteract,Is.True);
            Assert.That(world.SolidMass(filter),Is.EqualTo(3).Within(.001));
            Assert.That(app.View(jug.id).transform.Find("ExtractionAssembly/LowerJugLid").GetComponent<SpriteRenderer>().enabled,Is.False);
            app.View(filter.id).SetReceivingPour(true);yield return new WaitForSecondsRealtime(.2f);
            Assert.That(app.ContainerPanels.GetComponentsInChildren<TMP_Text>().Length,Is.GreaterThan(0));
            Assert.That(world.PourLiquid("m.heater",filter.id,50,1.875f).success,Is.True);world.Tick(1.875f);world.FinishPour(filter.id);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(world.ExtractionFilter(jug),Is.Null);
            Assert.That(world.LiquidVolume(jug),Is.EqualTo(200).Within(.001));
            Assert.That(world.CanPour(jug),Is.True);
        }
        [UnityTest] public IEnumerator SecondPourEmptyStopFreezesVisibleTimeUntilNewWater()
        {
            Grounds();Water();var world=app.World;var jug=world.Find("v.extractor");
            world.PourLiquid("m.heater",jug.id,100,3.75f);world.Tick(3.75f);world.FinishPour(jug.id);
            float first=world.ExtractionSnapshot(jug).elapsedSeconds;world.Tick(2);
            Assert.That(world.ExtractionSnapshot(jug).elapsedSeconds,Is.EqualTo(first));
            world.PourLiquid("m.heater",jug.id,100,3.75f);world.Tick(3.75f);world.FinishPour(jug.id);
            Assert.That(world.LiquidVolume(world.Find("m.heater")),Is.Zero.Within(.001));
            float stopped=world.ExtractionSnapshot(jug).elapsedSeconds;
            app.View(jug.id).SetHovered(true);yield return new WaitForSecondsRealtime(.2f);
            string before=null;foreach(var label in app.ContainerPanels.GetComponentsInChildren<TMP_Text>())
                if(label.text.StartsWith("时间"))before=label.text;
            Assert.That(before,Is.Not.Null);
            world.Tick(30);yield return new WaitForSecondsRealtime(.2f);
            Assert.That(world.ExtractionSnapshot(jug).elapsedSeconds,Is.EqualTo(stopped));
            string after=null;foreach(var label in app.ContainerPanels.GetComponentsInChildren<TMP_Text>())
                if(label.text.StartsWith("时间"))after=label.text;
            Assert.That(after,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator WaterSurfaceDrainsAndPhysicalFilterDetachesWhenCoffeeCompletes()
        {
            Grounds(); Water();
            var world = app.World; var filter = app.View("v.extractor.filter");
            var visual = filter.GetComponent<FreeCafeExtractorVisual>();
            world.PourLiquid("m.heater", "v.extractor", 10, .375f); yield return null;
            Assert.That(visual.UpperWaterMl, Is.EqualTo(10));
            world.Tick(.375f); yield return null;
            Assert.That(visual.UpperWaterMl, Is.Zero.Within(.001f)); Assert.That(visual.Dripping, Is.True);
            for (int index = 0; index < 19; index++) { world.PourLiquid("m.heater", "v.extractor", 10, .375f); world.Tick(.375f); }
            world.FinishPour("v.extractor"); world.Tick(.2f); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(world.ExtractionFilter(world.Find("v.extractor")), Is.Null);
            Assert.That(app.View("v.extractor").transform.Find("ExtractionAssembly/LowerJugLid").GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(world.CanPour(world.Find("v.extractor")), Is.True);
        }

        [UnityTest] public IEnumerator AHotKettleNearbyOpensFilterLidAndConsumedViewsDisappearPromptly()
        {
            Water(); var entity = app.World.Find("m.heater"); var heater = app.View(entity.id);
            var pointer = new PointerEventData(EventSystem.current)
            { position = app.worldCamera.WorldToScreenPoint(heater.transform.position) };
            app.BeginDrag(heater, pointer);
            heater.transform.position = app.View("v.extractor.filter").transform.position + Vector3.up * .5f;
            heater.FreezePointerMotion();
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(app.View("v.extractor.filter").GetComponent<FreeCafeExtractorVisual>().LidOpen, Is.True);
            app.CancelDrag(); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(app.View("v.extractor.filter").GetComponent<FreeCafeExtractorVisual>().LidOpen, Is.False);
            var ghost = Object.Instantiate(app.entityPrefab);
            ghost.StartMotionFrom(Vector3.zero); ghost.AnimateReturn(Vector3.zero, new Vector3(100, 100), true);
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(ghost == null, Is.True);
        }

        [UnityTest] public IEnumerator CaptureSplitExtractionStages()
        {
            string folder = Path.GetFullPath("output/split-extractor"); Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(.3f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "assembled.png"));
            yield return new WaitForSecondsRealtime(.3f);
            Grounds(); Water();
            app.World.PourLiquid("m.heater", "v.extractor", 15, .5625f);
            app.View("v.extractor.filter").SetReceivingPour(true); app.View("v.extractor").SetHovered(true);
            yield return new WaitForSecondsRealtime(.3f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "pouring.png"));
            yield return new WaitForSecondsRealtime(.3f);
            app.World.Tick(.5625f);
            for (int index = 0; index < 13; index++)
            { app.World.PourLiquid("m.heater", "v.extractor", 185f / 13, 6.9375f / 13); app.World.Tick(6.9375f / 13); }
            app.World.FinishPour("v.extractor"); app.World.Tick(.2f);
            app.View("v.extractor.filter").SetReceivingPour(false);
            yield return new WaitForSecondsRealtime(.3f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "completed.png"));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(File.Exists(Path.Combine(folder, "completed.png")), Is.True);
        }
    }
}
