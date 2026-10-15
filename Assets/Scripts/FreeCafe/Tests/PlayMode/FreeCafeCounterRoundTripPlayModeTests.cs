using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeCounterRoundTripPlayModeTests
    {
        private const float PositionTolerance = .0001f;
        private FreeCafeRuntime app;
        private Transform counter;
        private FreeCafeSubSceneDock dock;
        private Vector3 counterHome;
        private Scene cafeScene;
        private StackSnapshot[] stacks;
        private readonly List<DialoguePanel> disabledDialogue = new List<DialoguePanel>();
        private readonly List<BaseInputModule> disabledModules = new List<BaseInputModule>();

        private sealed class LocalPose
        {
            public readonly Transform target;
            private readonly Vector3 position, scale;
            private readonly Quaternion rotation;
            private readonly bool active;

            public LocalPose(Transform target)
            {
                this.target = target;
                position = target.localPosition;
                scale = target.localScale;
                rotation = target.localRotation;
                active = target.gameObject.activeSelf;
            }

            public void AssertUnchanged(string context)
            {
                Assert.That(target, Is.Not.Null, context + " must retain its authored transform");
                AssertVector(target.localPosition, position, context + " local position");
                AssertVector(target.localScale, scale, context + " local scale");
                Assert.That(Quaternion.Angle(target.localRotation, rotation), Is.LessThan(.001f), context + " local rotation");
                Assert.That(target.gameObject.activeSelf, Is.EqualTo(active), context + " active state");
            }
        }

        private sealed class StackSnapshot
        {
            public string id;
            public WorldEntity entity;
            public FreeEntityView view;
            public FreeCafeCupStackVisual stack;
            public float x, y;
            public int stock, visibleCount;
            public Vector3 relativePosition;
            public LocalPose root;
            public LocalPose[] artwork;
        }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None)) panel.Cancel();
            Time.timeScale = 1f;
            GameSaveParticipantRegistry.PendingRestore = null;
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null; yield return null;
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None))
            {
                panel.Cancel();
                if (panel.enabled) { disabledDialogue.Add(panel); panel.enabled = false; }
            }
            if (EventSystem.current != null)
                foreach (var module in EventSystem.current.GetComponents<BaseInputModule>())
                    if (module.enabled) { disabledModules.Add(module); module.enabled = false; }

            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            Assert.That(app, Is.Not.Null);
            Assert.That(app.World.ProductionEnabled, Is.True);
            Assert.That(app.InputBlocked, Is.False);
            Assert.That(app.World.State.currentArea, Is.EqualTo(AreaId.Cafe));
            cafeScene = SceneManager.GetActiveScene();
            counter = app.areaRoots[(int)AreaId.Counter];
            dock = counter.GetComponent<FreeCafeSubSceneDock>();
            Assert.That(dock, Is.Not.Null);
            counterHome = counter.localPosition;
            Assert.That(Vector3.Distance(counterHome, dock.dockedLocalPosition), Is.GreaterThan(1f));
            app.areaManager.panSmoothTime = .15f;

            stacks = new StackSnapshot[2];
            string[] ids = { "s.cups.small", "s.cups.large" };
            for (int i = 0; i < ids.Length; i++)
            {
                var entity = app.World.Find(ids[i]);
                var view = app.View(ids[i]);
                Assert.That(entity, Is.Not.Null);
                Assert.That(view, Is.Not.Null);
                var stack = view.GetComponent<FreeCafeCupStackVisual>();
                Assert.That(stack, Is.Not.Null);
                // Nonzero, below-cap stocks exercise both Art and every visible nested layer.
                entity.stock = 3 + i;
                stack.maxVisibleCups = 5;
            }
            app.DebugRefreshViews();
            yield return null;
            for (int i = 0; i < ids.Length; i++) stacks[i] = CaptureStack(ids[i]);
            app.Enter(AreaId.Counter);
            yield return WaitForConvergence(AreaId.Counter, dock.dockedLocalPosition);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (app != null) app.CancelDrag();
            foreach (var module in disabledModules) if (module != null) module.enabled = true;
            disabledModules.Clear();
            foreach (var panel in disabledDialogue) if (panel != null) panel.enabled = true;
            disabledDialogue.Clear();
            Time.timeScale = 1f;
            yield return null;
        }

        private StackSnapshot CaptureStack(string id)
        {
            var entity = app.World.Find(id);
            var view = app.View(id);
            var stack = view.GetComponent<FreeCafeCupStackVisual>();
            Assert.That(view.transform.parent, Is.SameAs(counter));
            var art = view.transform.Find("Art");
            Assert.That(art, Is.Not.Null);
            Assert.That(view.body.transform, Is.SameAs(art));
            Assert.That(stack.VisibleCupCount, Is.EqualTo(entity.stock));
            var poses = new List<LocalPose> { new LocalPose(art) };
            for (int i = 0; ; i++)
            {
                var nested = view.transform.Find("NestedCup" + i);
                if (nested == null) break;
                poses.Add(new LocalPose(nested));
            }
            Assert.That(poses.Count, Is.GreaterThanOrEqualTo(stack.VisibleCupCount), "Capture every visible nested cup");
            return new StackSnapshot
            {
                id = id, entity = entity, view = view, stack = stack,
                x = entity.x, y = entity.y, stock = entity.stock, visibleCount = stack.VisibleCupCount,
                relativePosition = counter.InverseTransformPoint(view.transform.position),
                root = new LocalPose(view.transform), artwork = poses.ToArray()
            };
        }

        private static void AssertVector(Vector3 actual, Vector3 expected, string context)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(PositionTolerance), context);
        }

        private void AssertStacksUnchanged()
        {
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(cafeScene), "Cafe/Counter navigation must stay in the same scene");
            Assert.That(app.gameObject.scene, Is.EqualTo(cafeScene));
            foreach (var saved in stacks)
            {
                Assert.That(app.World.Find(saved.id), Is.SameAs(saved.entity), saved.id + " entity identity");
                Assert.That(app.View(saved.id), Is.SameAs(saved.view), saved.id + " view identity");
                Assert.That(saved.entity.area, Is.EqualTo(AreaId.Counter));
                Assert.That(saved.entity.x, Is.EqualTo(saved.x), saved.id + " domain x");
                Assert.That(saved.entity.y, Is.EqualTo(saved.y), saved.id + " domain y");
                Assert.That(saved.entity.stock, Is.EqualTo(saved.stock), saved.id + " stock");
                Assert.That(saved.stack.VisibleCupCount, Is.EqualTo(saved.visibleCount), saved.id + " visible count");
                Assert.That(saved.view.transform.parent, Is.SameAs(counter));
                saved.root.AssertUnchanged(saved.id + " root");
                AssertVector(counter.InverseTransformPoint(saved.view.transform.position), saved.relativePosition,
                    saved.id + " root must not slide relative to Counter during pan");
                foreach (var pose in saved.artwork) pose.AssertUnchanged(saved.id + "/" + pose.target.name);
            }
        }

        private IEnumerator WaitForConvergence(AreaId area, Vector3 target)
        {
            Assert.That(app.World.State.currentArea, Is.EqualTo(area));
            Assert.That(app.areaManager.CurrentArea, Is.EqualTo(area));
            float deadline = Time.realtimeSinceStartup + 3f;
            int stableFrames = 0;
            do
            {
                yield return null;
                // Check all intermediate frames, not just the eventual settled pose.
                AssertStacksUnchanged();
                bool settled = !app.areaManager.IsPanning && Vector3.Distance(counter.localPosition, target) < .001f;
                stableFrames = settled ? stableFrames + 1 : 0;
            }
            while (stableFrames < 3 && Time.realtimeSinceStartup < deadline);
            Assert.That(stableFrames, Is.EqualTo(3), "Camera and Counter dock must converge within three seconds");
            Assert.That(dock.IsDocked, Is.EqualTo(area == AreaId.Counter));
            AssertVector(counter.localPosition, target, "Counter dock endpoint");
        }

        [UnityTest]
        public IEnumerator NormalCafeCounterRoundTripsPreserveBothCupStacksThroughoutSmoothPan()
        {
            for (int trip = 0; trip < 4; trip++)
            {
                Assert.That(app.InputBlocked, Is.False);
                app.ReturnFromProduction();
                Assert.That(app.areaManager.IsPanning, Is.True, "Return must exercise the .15s smooth pan");
                AssertStacksUnchanged();
                yield return WaitForConvergence(AreaId.Cafe, counterHome);
                app.Enter(AreaId.Counter);
                Assert.That(app.areaManager.IsPanning, Is.True);
                AssertStacksUnchanged();
                yield return WaitForConvergence(AreaId.Counter, dock.dockedLocalPosition);
            }
        }

        [UnityTest]
        public IEnumerator RapidCafeCounterRoundTripsCanReverseUnfinishedPanWithoutCupStackDrift()
        {
            for (int trip = 0; trip < 6; trip++)
            {
                app.ReturnFromProduction();
                Assert.That(app.World.State.currentArea, Is.EqualTo(AreaId.Cafe));
                Assert.That(dock.IsDocked, Is.False);
                AssertStacksUnchanged();
                yield return null;
                AssertStacksUnchanged();
                Assert.That(Vector3.Distance(counter.localPosition, counterHome), Is.GreaterThan(.001f),
                    "Rapid re-entry must interrupt an unfinished outgoing dock movement");
                // Re-enter without waiting for the outgoing dock/pan to settle.
                app.Enter(AreaId.Counter);
                Assert.That(app.World.State.currentArea, Is.EqualTo(AreaId.Counter));
                Assert.That(dock.IsDocked, Is.True);
                AssertStacksUnchanged();
                yield return null;
                AssertStacksUnchanged();
            }
            yield return WaitForConvergence(AreaId.Counter, dock.dockedLocalPosition);
            app.ReturnFromProduction();
            yield return WaitForConvergence(AreaId.Cafe, counterHome);
            app.Enter(AreaId.Counter);
            yield return WaitForConvergence(AreaId.Counter, dock.dockedLocalPosition);
        }

        [UnityTest]
        public IEnumerator ReenablingCupParentRestoresCurrentFillImmediatelyWithoutPresentOrInventoryChanges()
        {
            Assert.That(app.World.SelectServingCup("s.cups.large").success, Is.True);
            var cup = app.World.ServingCup;
            var view = app.View(cup.id);
            var presenter = view.liquidPresenter as OrthographicGlassCupPresenter;
            Assert.That(presenter, Is.Not.Null);
            var liquid = presenter.GetComponent<GravityLiquidImage>();
            Assert.That(liquid, Is.Not.Null);
            Assert.That(presenter.gameObject.activeSelf, Is.False, "A stationary cup starts with its liquid hidden");

            view.PresentPour(.5f, true);
            Assert.That(presenter.gameObject.activeInHierarchy, Is.True);
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 120f });
            view.Refresh(cup);
            Assert.That(app.World.LiquidCapacity(cup), Is.EqualTo(400f));
            Assert.That(presenter.TargetFill, Is.EqualTo(.3f).Within(.0001f));
            string inventoryBefore = app.CaptureJson();
            for (int toggle = 0; toggle < 3; toggle++)
            {
                view.gameObject.SetActive(false);
                Assert.That(presenter.gameObject.activeSelf, Is.True, "Only the parent cup is disabled");
                Assert.That(presenter.gameObject.activeInHierarchy, Is.False);
                view.gameObject.SetActive(true);
                // No yield, Refresh, PresentPour or Present is allowed before these assertions.
                Assert.That(presenter.gameObject.activeInHierarchy, Is.True);
                Assert.That(presenter.TargetFill, Is.EqualTo(.3f).Within(.0001f));
                Assert.That(liquid.CurrentFill, Is.EqualTo(.3f).Within(.0001f), "OnEnable must restore the latest authoritative fill immediately");
                Assert.That(app.CaptureJson(), Is.EqualTo(inventoryBefore), "Parent visibility cannot mutate any inventory/domain data");
            }

            view.PresentPour(0f, false);
            view.Refresh(cup);
            Assert.That(presenter.gameObject.activeSelf, Is.False, "Stopping the pour must keep stationary liquid hidden");
            Assert.That(view.body.color.a, Is.EqualTo(1f).Within(.0001f));
            Assert.That(app.CaptureJson(), Is.EqualTo(inventoryBefore));
            yield return null;
            Assert.That(presenter.gameObject.activeSelf, Is.False, "Periodic refresh cannot reveal stationary liquid");
            Assert.That(app.World.LiquidVolume(cup), Is.EqualTo(120f));
        }
    }
}
