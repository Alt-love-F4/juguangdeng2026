using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeInactiveAuthoredPlayModeTests
    {
        private FreeCafeRuntime app;

        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1;
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None)) panel.Cancel();
            GameSaveParticipantRegistry.PendingRestore = null;
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            // Unload a previous test's adapter before binding a map-owned domain world.
            var empty = SceneManager.GetSceneByName("InactiveAuthoredTestSetup");
            if (!empty.IsValid()) empty = SceneManager.CreateScene("InactiveAuthoredTestSetup");
            SceneManager.SetActiveScene(empty);
            var scenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i) != empty) scenes.Add(SceneManager.GetSceneAt(i));
            foreach (var scene in scenes) yield return SceneManager.UnloadSceneAsync(scene);
            PlayerCafeOwner.EndSession();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            if (app != null) yield return SceneManager.UnloadSceneAsync(app.gameObject.scene);
            PlayerCafeOwner.EndSession();
        }

        private IEnumerator LoadCafe()
        {
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Additive);
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            Assert.That(app, Is.Not.Null);
            SceneManager.SetActiveScene(app.gameObject.scene);
            yield return null;
            yield return null;
            app.World.EnterProduction();
            app.areaManager.panSmoothTime = 0;
            yield return null;
        }

        private List<FreeEntityView> AssertHiddenAuthoredHaveNoViews()
        {
            var authored = FreeCafeSceneLayout.FindAuthoredEntities(app);
            var hidden = authored.Where(view => !view.gameObject.activeInHierarchy).ToList();
            Assert.That(hidden.Any(view => view.AuthoringEntityId == "m.brewer"), Is.True,
                "The formal scene must retain its disabled brewer fixture");
            var all = Object.FindObjectsByType<FreeEntityView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var view in hidden)
            {
                string id = view.AuthoringEntityId;
                Assert.That(app.View(id), Is.Null, id + " must not be registered");
                Assert.That(all.Any(candidate => candidate.gameObject.activeInHierarchy &&
                    (candidate.EntityId == id || candidate.AuthoringEntityId == id)), Is.False,
                    id + " must not acquire a visible clone");
            }
            foreach (var view in authored.Where(view => view.gameObject.activeInHierarchy))
                Assert.That(app.View(view.AuthoringEntityId), Is.SameAs(view), "Active authored instances must be reused");
            return hidden;
        }

        [UnityTest] public IEnumerator DirectEntryAndRepeatedRefreshKeepDisabledAuthoredEntitiesHidden()
        {
            yield return LoadCafe();
            AssertHiddenAuthoredHaveNoViews();
            app.DebugRefreshViews();
            app.RestoreJson(app.CaptureJson());
            yield return null;
            AssertHiddenAuthoredHaveNoViews();
        }

        [UnityTest] public IEnumerator MapCatalogCaptureBridgePreservesDataWithoutResurrectingDisabledViews()
        {
            var catalog = JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text);
            var world = new CafeWorld(catalog);
            world.EnableProduction();
            world.EnterProduction();
            Assert.That(world.SelectServingCup("s.cups.small").success, Is.True);
            string cupId = world.ServingCup.id;
            world.State.money = 12345;
            var brewer = world.Find("m.brewer");
            Assert.That(brewer, Is.Not.Null);
            // An authored ID is suppressed, never the whole specification.
            world.State.entities.Add(new WorldEntity
            {
                id = "dynamic.brewer.regression", specId = brewer.specId,
                area = brewer.area, x = brewer.x, y = brewer.y
            });
            PlayerCafeOwner.Bind(world);
            GameSaveParticipantRegistry.PendingRestore = GameSaveParticipantRegistry.CaptureAll();
            Assert.That(GameSaveParticipantRegistry.PendingRestore.Any(p => p.Id == FreeCafeRuntime.SaveId), Is.True);

            yield return LoadCafe();
            Assert.That(app.World.State.money, Is.EqualTo(12345));
            Assert.That(app.World.Find(cupId), Is.Not.Null);
            Assert.That(app.View(cupId), Is.Not.Null);
            Assert.That(app.View(cupId).gameObject.activeInHierarchy, Is.True);
            Assert.That(app.View("dynamic.brewer.regression"), Is.Not.Null);
            Assert.That(GameSaveParticipantRegistry.PendingRestore.Any(p => p.Id == FreeCafeRuntime.SaveId), Is.False);
            var hidden = AssertHiddenAuthoredHaveNoViews();
            foreach (var view in hidden)
                if (world.Find(view.AuthoringEntityId) != null)
                    Assert.That(app.World.Find(view.AuthoringEntityId), Is.Not.Null, "Hidden saved domain data must survive");

            var grinderView = app.View("m.grinder");
            app.World.Find("m.grinder").x += .25f;
            float savedX = app.World.Find("m.grinder").x;
            string saved = app.CaptureJson();
            app.World.Find("m.grinder").x -= .5f;
            app.RestoreJson(saved);
            app.DebugRefreshViews();
            yield return null;
            AssertHiddenAuthoredHaveNoViews();
            Assert.That(app.World.Find("m.grinder").x, Is.EqualTo(savedX));
            Assert.That(app.View("m.grinder"), Is.SameAs(grinderView));
            Assert.That(app.World.State.money, Is.EqualTo(12345));
            Assert.That(app.View(cupId), Is.Not.Null);
        }
    }
}
