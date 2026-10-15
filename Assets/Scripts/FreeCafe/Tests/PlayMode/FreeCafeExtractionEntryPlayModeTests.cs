using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spotlight.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    /// <summary>Entry regression: authored and Map-pending worlds must work before any assembly drag.</summary>
    public sealed class FreeCafeExtractionEntryPlayModeTests
    {
        private FreeCafeRuntime app;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None))
                panel.Cancel();
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (app != null)
            {
                app.ContainerPanels.ClearInspection("v.extractor.filter");
                app.CancelDrag();
                app.enabled = true;
            }
            GameSaveParticipantRegistry.PendingRestore = null;
            Time.timeScale = 1;
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirectCoffeeShopEntryExtractsWithoutDetachingOrReattaching()
        {
            yield return LoadCoffeeShop(false);
            yield return AssertFreshAssemblyExtracts();
        }

        [UnityTest]
        public IEnumerator PendingMapCatalogWorldExtractsWithoutDetachingOrReattaching()
        {
            yield return LoadCoffeeShop(true);
            yield return AssertFreshAssemblyExtracts();
        }

        private IEnumerator LoadCoffeeShop(bool pendingMapWorld)
        {
            if (pendingMapWorld)
            {
                var catalogAsset = Resources.Load<TextAsset>("FreeCafe/catalog");
                Assert.That(catalogAsset, Is.Not.Null);
                var mapWorld = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(catalogAsset.text));
                Assert.That(mapWorld.ProductionEnabled, Is.False,
                    "Map creates a catalog world without enabling production.");
                // Prove Start consumed this snapshot rather than testing Awake's fresh scene world twice.
                mapWorld.State.entities.Add(new WorldEntity
                {
                    id = "extraction.entry.pending.marker", specId = "grounds", units = 1, area = AreaId.Cafe
                });
                GameSaveParticipantRegistry.PendingRestore = new List<GameSaveParticipantData>
                {
                    new GameSaveParticipantData
                    {
                        Id = FreeCafeRuntime.SaveId,
                        Json = JsonUtility.ToJson(mapWorld.State)
                    }
                };
            }

            yield return SceneManager.LoadSceneAsync("CoffeeShop");
            yield return null;
            yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            Assert.That(app, Is.Not.Null);
            Assert.That(app.World.ProductionEnabled, Is.True,
                "Scene entry must enable production; the test must not repair it with EnterProduction.");
            if (pendingMapWorld)
            {
                Assert.That(app.World.Find("extraction.entry.pending.marker"), Is.Not.Null);
                Assert.That(GameSaveParticipantRegistry.TakePending(FreeCafeRuntime.SaveId), Is.Null);
            }
            app.areaManager.panSmoothTime = 0;
            app.Enter(AreaId.Counter);
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return null;
            // Keep panel Updates running but use explicit domain ticks for deterministic extraction.
            app.enabled = false;
        }

        private IEnumerator AssertFreshAssemblyExtracts()
        {
            var world = app.World;
            var jug = world.Find("v.extractor");
            var filter = world.Find("v.extractor.filter");
            Assert.That(jug, Is.Not.Null);
            Assert.That(filter, Is.Not.Null);
            Assert.That(world.ExtractionFilter(jug), Is.SameAs(filter), "Fresh lower jug must reference its upper filter.");
            Assert.That(world.ExtractionJug(filter), Is.SameAs(jug), "Fresh upper filter must reference its lower jug.");
            Assert.That(app.View(jug.id), Is.Not.Null);
            Assert.That(app.View(filter.id), Is.Not.Null);
            Assert.That(world.LiquidVolume(jug), Is.Zero);

            // Intentionally target the upper filter, with no BeginDrag/PrepareExtractionDrag/Attach call.
            world.State.entities.Add(new WorldEntity
            {
                id = "extraction.entry.dose", specId = "grounds", units = 1, area = AreaId.Counter
            });
            Assert.That(world.Interact("extraction.entry.dose", filter.id).success, Is.True);
            Assert.That(world.SolidMass(filter), Is.GreaterThan(0));
            Assert.That(world.ExtractionFilter(jug), Is.SameAs(filter));
            Assert.That(world.ExtractionJug(filter), Is.SameAs(jug));
            app.ContainerPanels.Inspect(filter.id);
            yield return new WaitForSecondsRealtime(.3f);
            AssertGauge("TimeGauge", "时间");
            AssertGauge("WaterGauge", "热水");

            var heater = world.Find("m.heater");
            Assert.That(heater, Is.Not.Null);
            heater.contents.Clear();
            heater.contents.Add(new Portion
            {
                specId = "hotwater", units = 1, milliliters = 200, temperature = LiquidTemperature.Boiling
            });
            heater.production.phase = ProductionPhase.Room;
            Assert.That(world.PourLiquid(heater.id, filter.id, 100, 3.75f).success, Is.True);
            world.Tick(3.75f);
            Assert.That(world.LiquidVolume(jug), Is.EqualTo(100).Within(.01f));
            Assert.That(jug.contents.Exists(portion => portion.specId == "coffee" && portion.milliliters > 0), Is.True,
                "Hot water poured into the untouched upper filter must produce coffee in the lower jug.");
            Assert.That(world.ExtractionFilter(jug), Is.SameAs(filter));
            Assert.That(world.ExtractionJug(filter), Is.SameAs(jug));
        }

        private void AssertGauge(string gaugeName, string captionPrefix)
        {
            TMP_Text caption = null;
            foreach (var label in app.ContainerPanels.GetComponentsInChildren<TMP_Text>(true))
                if (label.name == "Caption" && label.transform.parent.name == gaugeName
                    && label.text.StartsWith(captionPrefix) && label.gameObject.activeInHierarchy)
                {
                    caption = label;
                    break;
                }
            Assert.That(caption, Is.Not.Null, "Expected active " + captionPrefix + " TMP caption when inspecting the upper filter.");
            Assert.That(caption.isActiveAndEnabled, Is.True);
            Assert.That(caption.transform.parent.gameObject.activeSelf, Is.True, gaugeName + " must be enabled.");
            Assert.That(caption.transform.parent.gameObject.activeInHierarchy, Is.True, gaugeName + " must be visible.");
        }
    }
}
