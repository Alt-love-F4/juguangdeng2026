using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    /// <summary>Legacy catalog snapshots own inventory, not fixed sources' scene-authored poses.</summary>
    public sealed class FreeCafeStaleSourceSnapshotPlayModeTests
    {
        private const string CupId = "stale.source.snapshot.filled.cup";
        private const float CupX = 1.234f, CupY = -.876f, Tolerance = .001f;
        private FreeCafeRuntime app;
        private string oldSnapshot;
        private CafeCatalog oldCatalog;
        private readonly Dictionary<string, int> savedStock = new Dictionary<string, int>();
        private readonly List<InitialEntity> fixedSources = new List<InitialEntity>();

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.timeScale = 1;
            foreach (var panel in UnityEngine.Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None))
                panel.Cancel();
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            GameSaveParticipantRegistry.PendingRestore = null;
            savedStock.Clear();
            fixedSources.Clear();

            var resource = Resources.Load<TextAsset>("FreeCafe/catalog");
            Assert.That(resource, Is.Not.Null);
            oldCatalog = JsonUtility.FromJson<CafeCatalog>(resource.text);
            var catalogWorld = new CafeWorld(oldCatalog);
            // Match the Map-owned catalog world: production is enabled only by scene entry.
            Assert.That(catalogWorld.ProductionEnabled, Is.False);
            foreach (var entity in catalogWorld.State.entities)
            {
                var spec = catalogWorld.Spec(entity.specId);
                if (!spec.Has(Capability.Source) || spec.Has(Capability.Movable)) continue;
                entity.stock = 17 + savedStock.Count;
                savedStock.Add(entity.id, entity.stock);
            }
            var largeSource = catalogWorld.Find("s.cups.large");
            Assert.That(largeSource, Is.Not.Null);
            var cup = new WorldEntity
            {
                id = CupId,
                specId = catalogWorld.Spec(largeSource.specId).sourceItemId,
                area = AreaId.Counter, x = CupX, y = CupY
            };
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 120f });
            catalogWorld.State.entities.Add(cup);
            oldSnapshot = JsonUtility.ToJson(catalogWorld.State);
            GameSaveParticipantRegistry.PendingRestore = new List<GameSaveParticipantData>
            {
                new GameSaveParticipantData { Id = FreeCafeRuntime.SaveId, Json = oldSnapshot }
            };

            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null;
            yield return null;
            app = UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            Assert.That(app, Is.Not.Null);
            Assert.That(app.World.ProductionEnabled, Is.True);
            Assert.That(GameSaveParticipantRegistry.TakePending(FreeCafeRuntime.SaveId), Is.Null,
                "Start must consume the catalog pending snapshot.");
            foreach (var initial in app.World.Catalog.layout)
            {
                var spec = app.World.Spec(initial.specId);
                if (spec.Has(Capability.Source) && !spec.Has(Capability.Movable)) fixedSources.Add(initial);
            }
            Assert.That(fixedSources.Count, Is.GreaterThanOrEqualTo(2));
            // Fixture guard: these legacy resource poses genuinely differ from Awake's scene capture.
            foreach (string id in new[] { "s.cups.small", "s.cups.large" })
            {
                var old = Array.Find(oldCatalog.layout, entry => entry.id == id);
                var authored = fixedSources.Find(entry => entry.id == id);
                Assert.That(old, Is.Not.Null);
                Assert.That(authored, Is.Not.Null);
                Assert.That(Mathf.Abs(old.y - authored.y), Is.GreaterThan(.5f),
                    id + " must exercise the stale catalog y, not an already-corrected snapshot.");
            }
            app.areaManager.panSmoothTime = 0;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (app != null) app.CancelDrag();
            GameSaveParticipantRegistry.PendingRestore = null;
            Time.timeScale = 1;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CatalogPendingKeepsAuthoredSourcesAndSavedInventoryAcrossCounterRoundTrips()
        {
            AssertDomainAndInventory();
            yield return WaitForViews();
            for (int trip = 0; trip < 3; trip++)
            {
                app.Enter(AreaId.Counter);
                Assert.That(app.World.State.currentArea, Is.EqualTo(AreaId.Counter));
                AssertDomainAndInventory();
                yield return WaitForViews();
                app.ReturnFromProduction();
                Assert.That(app.World.State.currentArea, Is.EqualTo(AreaId.Cafe));
                AssertDomainAndInventory();
                yield return WaitForViews();
            }
        }

        [UnityTest]
        public IEnumerator ExplicitOldRestoreUsesAwakeSourcePosesButRestoresStockAndDynamicCup()
        {
            app.Enter(AreaId.Counter);
            yield return WaitForViews();
            // Deliberately disturb live source/view poses: Restore must use the Awake capture,
            // not a fresh capture of already-displaced transforms or the catalog snapshot.
            foreach (var initial in fixedSources)
            {
                var entity = app.World.Find(initial.id);
                entity.x += .2f;
                entity.y += .15f;
                entity.stock = 1;
            }
            var cup = app.World.Find(CupId);
            Assert.That(cup, Is.Not.Null);
            cup.x = CupX + .3f;
            cup.y = CupY + .2f;
            cup.contents[0].milliliters = 45f;
            app.DebugRefreshViews();
            yield return new WaitForSecondsRealtime(.3f);

            app.RestoreJson(oldSnapshot);
            AssertDomainAndInventory();
            yield return WaitForViews();
            app.Enter(AreaId.Counter);
            app.ReturnFromProduction();
            app.Enter(AreaId.Counter);
            AssertDomainAndInventory();
            yield return WaitForViews();
        }

        private void AssertDomainAndInventory()
        {
            foreach (var initial in fixedSources)
            {
                var entity = app.World.Find(initial.id);
                Assert.That(entity, Is.Not.Null, initial.id);
                Assert.That(entity.area, Is.EqualTo(initial.area), initial.id + " authored area");
                Assert.That(entity.x, Is.EqualTo(initial.x).Within(Tolerance), initial.id + " authored x");
                Assert.That(entity.y, Is.EqualTo(initial.y).Within(Tolerance), initial.id + " authored y");
                Assert.That(entity.stock, Is.EqualTo(savedStock[initial.id]), initial.id + " saved stock");
            }
            var cup = app.World.Find(CupId);
            Assert.That(cup, Is.Not.Null, "Pending/explicit restore must retain the dynamic filled cup.");
            Assert.That(cup.area, Is.EqualTo(AreaId.Counter));
            Assert.That(cup.x, Is.EqualTo(CupX).Within(Tolerance));
            Assert.That(cup.y, Is.EqualTo(CupY).Within(Tolerance));
            Assert.That(app.World.LiquidVolume(cup), Is.EqualTo(120f).Within(Tolerance));
            Assert.That(cup.contents.Count, Is.EqualTo(1));
            Assert.That(cup.contents[0].specId, Is.EqualTo("coffee"));
        }

        private IEnumerator WaitForViews()
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!ViewsMatch() && Time.realtimeSinceStartup < deadline) yield return null;
            foreach (var initial in fixedSources)
                AssertViewPose(initial.id, initial.area, initial.x, initial.y);
            AssertViewPose(CupId, AreaId.Counter, CupX, CupY);
        }

        private bool ViewsMatch()
        {
            foreach (var initial in fixedSources)
                if (!ViewMatches(initial.id, initial.area, initial.x, initial.y)) return false;
            return ViewMatches(CupId, AreaId.Counter, CupX, CupY);
        }

        private bool ViewMatches(string id, AreaId area, float x, float y)
        {
            var view = app.View(id);
            if (view == null || view.transform.parent != app.areaRoots[(int)area]) return false;
            var local = app.areaRoots[(int)area].InverseTransformPoint(view.transform.position);
            return Mathf.Abs(local.x - x) < Tolerance && Mathf.Abs(local.y - y) < Tolerance;
        }

        private void AssertViewPose(string id, AreaId area, float x, float y)
        {
            var view = app.View(id);
            Assert.That(view, Is.Not.Null, id + " view");
            Assert.That(view.transform.parent, Is.SameAs(app.areaRoots[(int)area]), id + " view area root");
            var local = app.areaRoots[(int)area].InverseTransformPoint(view.transform.position);
            Assert.That(local.x, Is.EqualTo(x).Within(Tolerance), id + " settled area-local view x");
            Assert.That(local.y, Is.EqualTo(y).Within(Tolerance), id + " settled area-local view y");
        }
    }
}
