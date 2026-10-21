using System.Collections;
using System.IO;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeCounterArtPlayModeTests
    {
        private FreeCafeRuntime app;
        [UnitySetUp] public IEnumerator Setup()
        {
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None)) panel.Cancel();
            Time.timeScale = 1; GameSaveParticipantRegistry.PendingRestore = null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop"); yield return null; yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            app.areaManager.panSmoothTime = 0; app.World.EnterProduction();
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return null; app.enabled = false;
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (app != null) app.CancelDrag(); Time.timeScale = 1; yield return null; }
        [UnityTest] public IEnumerator SuppliedSpritesAreBoundAndExtractorUsesTwoDistinctAssets()
        {
            Assert.That(app.View("m.heater").body.sprite, Is.SameAs(app.artLibrary.Resolve("machine.heater")));
            Assert.That(app.View("v.extractor").body.sprite, Is.Not.SameAs(app.View("v.extractor.filter").body.sprite));
            Assert.That(app.View("v.extractor").GetComponent<FreeCafeExtractorVisual>().staticArtwork, Is.True);
            Assert.That(app.View("v.extractor.filter").transform.Find("ExtractionAssembly"), Is.Null, "Do not add placeholder lid/liquid animations to the supplied static art");
            Assert.That(app.areaRoots[1].Find("SuppliedCounterBackdrop").GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
            Assert.That(app.artLibrary.Resolve("grounds"), Is.Not.Null, "Missing PSD art must retain its previous fallback");
            yield return null;
        }
        [UnityTest] public IEnumerator CupStockChangesNestedLayersAndSingleCupHasNoLowerLayers()
        {
            var source = app.World.Find("s.cups.large"); var view = app.View(source.id);
            source.stock = 1; app.DebugRefreshViews(); yield return null;
            var stack = view.GetComponent<FreeCafeCupStackVisual>(); Assert.That(stack.VisibleCupCount, Is.EqualTo(1));
            source.stock = 3; app.DebugRefreshViews(); yield return null;
            Assert.That(stack.VisibleCupCount, Is.EqualTo(3));
            var lower = view.transform.Find("NestedCup0").GetComponent<SpriteRenderer>();
            Assert.That(lower.gameObject.activeSelf, Is.True); Assert.That(lower.sprite, Is.Not.SameAs(view.body.sprite));
            Assert.That(lower.transform.localPosition.y, Is.EqualTo(0));
            Assert.That(view.body.transform.localPosition.y, Is.EqualTo(2 * stack.StackStep).Within(.0001f));
            Assert.That(lower.sortingOrder, Is.GreaterThan(view.body.sortingOrder));
            source.stock = 0; app.DebugRefreshViews(); yield return null;
            Assert.That(view.body.enabled, Is.False); Assert.That(lower.gameObject.activeSelf, Is.False);
        }
        [UnityTest] public IEnumerator ClickingSilverKettleStartsTheExistingHeatingPipeline()
        {
            app.World.PourLiquid("s.water", "m.heater", 200, 1);
            app.Click(app.View("m.heater"));
            Assert.That(app.World.Find("m.heater").production.phase, Is.EqualTo(ProductionPhase.Processing));
            app.World.Tick(4); yield return null;
            Assert.That(app.World.TemperatureCelsius(app.World.Find("m.heater")), Is.EqualTo(100).Within(.001f));
        }
        private PointerEventData Pointer(FreeEntityView view) => new PointerEventData(EventSystem.current)
        { position = app.worldCamera.WorldToScreenPoint(view.transform.position) };
        [UnityTest] public IEnumerator ReleasingReusableVesselReturnsItButCupCanStayOnWorkMat()
        {
            var kettle = app.View("m.heater"); var home = kettle.transform.position; var p = Pointer(kettle);
            app.BeginDrag(kettle, p); Assert.That(app.IsDragging, Is.True);
            p.position = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter, 0, -1)); app.MoveDrag(p);
            app.EndDrag(p); Assert.That(app.IsDragging, Is.False);
            yield return new WaitForSecondsRealtime(1.5f); Assert.That(Vector3.Distance(kettle.transform.position, home), Is.LessThan(.02f));
            yield return null;
            app.World.Take("s.cups.small", AreaId.Counter, -1, -1, out var cup); var view = app.View(cup.id); p = Pointer(view);
            app.BeginDrag(view, p); Assert.That(app.IsDragging, Is.True);
            p.position = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter, 1, -.5f)); app.MoveDrag(p); app.EndDrag(p);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(app.World.Find(cup.id).x, Is.EqualTo(1).Within(.02f));
        }
        [UnityTest] public IEnumerator LooseCupBottomUsesTheNestedSpriteAndRestoresAfterTakingTop()
        {
            app.World.Take("s.cups.small", AreaId.Counter, 0, -1, out var a);
            app.World.Take("s.cups.small", AreaId.Counter, 1, -1, out var b);
            app.World.Interact(b.id, a.id); yield return null;
            Assert.That(app.View(a.id).body.sprite, Is.SameAs(app.artLibrary.Resolve("cup.small", "nested")));
            Assert.That(app.View(b.id).body.sprite, Is.SameAs(app.artLibrary.Resolve("cup.small")));
            var topView = app.View(b.id); app.BeginDrag(topView, Pointer(topView));
            Assert.That(app.View(a.id).body.sprite, Is.SameAs(app.artLibrary.Resolve("cup.small")));
            app.CancelDrag();
            Assert.That(app.View(a.id).body.sprite, Is.SameAs(app.artLibrary.Resolve("cup.small", "nested")));
            app.World.Move(b.id, AreaId.Counter, 2, -1); yield return null;
            Assert.That(app.View(a.id).body.sprite, Is.SameAs(app.artLibrary.Resolve("cup.small")));
        }
        [UnityTest] public IEnumerator DraggingUpperMovesWholeAssemblyAndReturnsBothHome()
        {
            var jug = app.World.Find("v.extractor"); var filter = app.World.ExtractionFilter(jug);
            var lowerView = app.View(jug.id); var view = app.View(filter.id);
            var lowerHome = lowerView.transform.position; var upperHome = view.transform.position;
            var pointer = Pointer(view); Physics2D.SyncTransforms(); app.BeginDrag(view, pointer);
            Assert.That(app.IsDragging, Is.True); Assert.That(lowerView.hitbox.enabled, Is.False);
            pointer.position = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter, 0, 1));
            app.MoveDrag(pointer); yield return new WaitForSecondsRealtime(.4f);
            var offset = app.areaRoots[(int)AreaId.Counter].TransformVector(new Vector3(
                app.World.Catalog.production.extractionFilterOffsetX, app.World.Catalog.production.extractionFilterOffsetY, 0));
            Assert.That(Vector3.Distance(view.transform.position - lowerView.transform.position, offset), Is.LessThan(.02f));
            Assert.That(Vector3.Distance(lowerView.transform.position, lowerHome), Is.GreaterThan(.1f));
            Assert.That(app.World.ExtractionFilter(jug), Is.SameAs(filter));
            app.EndDrag(pointer); yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(app.World.ExtractionFilter(jug), Is.SameAs(filter));
            Assert.That(lowerView.hitbox.enabled, Is.True);
            Assert.That(Vector3.Distance(lowerView.transform.position, lowerHome), Is.LessThan(.02f));
            Assert.That(Vector3.Distance(view.transform.position, upperHome), Is.LessThan(.02f));
        }
        [UnityTest] public IEnumerator DraggingLowerLeavesUpperStationaryAndReassemblyRemainsPossible()
        {
            var jug = app.World.Find("v.extractor"); var filter = app.World.ExtractionFilter(jug);
            var lowerView = app.View(jug.id); var upperView = app.View(filter.id); var upperHome = upperView.transform.position;
            var pointer = Pointer(lowerView); Physics2D.SyncTransforms(); app.BeginDrag(lowerView, pointer);
            Assert.That(app.IsDragging, Is.True); Assert.That(app.World.ExtractionFilter(jug), Is.Null);
            pointer.position = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter, 0, -.5f));
            app.MoveDrag(pointer); yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Vector3.Distance(upperView.transform.position, upperHome), Is.LessThan(.001f));
            app.CancelDrag(); yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(app.World.ExtractionFilter(jug), Is.Null);
            Assert.That(Vector3.Distance(upperView.transform.position, upperHome), Is.LessThan(.001f));
            Assert.That(app.World.AttachExtractionFilter(filter.id, jug.id).success, Is.True);
            Assert.That(app.World.ExtractionFilter(jug), Is.SameAs(filter));
        }
        [UnityTest] public IEnumerator UsedExtractorPartsCanBeDroppedTogetherInBothDirections()
        {
            var world = app.World;
            var jug = world.Find("v.extractor"); var filter = world.Find("v.extractor.filter");
            foreach (bool lowerOntoUpper in new[] { false, true })
            {
                Assert.That(world.PrepareExtractionDrag(jug.id).success, Is.True);
                Assert.That(world.ReturnVesselHome(filter.id).success, Is.True);
                jug.contents.Clear(); filter.contents.Clear();
                jug.contents.Add(new Portion { specId = "water", milliliters = 80, units = 1 });
                filter.contents.Add(new Portion { specId = "sugar", grams = 5, units = 1 });
                filter.production.filterSpent = true;
                jug.production.brewStarted = jug.production.brewCompleted = true;
                app.DebugRefreshViews(); yield return new WaitForSecondsRealtime(1.5f);
                var source = app.View(lowerOntoUpper ? jug.id : filter.id);
                var target = app.View(lowerOntoUpper ? filter.id : jug.id);
                Physics2D.SyncTransforms();
                var pointer = Pointer(source); app.BeginDrag(source, pointer);
                Assert.That(app.IsDragging, Is.True);
                pointer.position = app.worldCamera.WorldToScreenPoint(target.transform.position);
                app.MoveDrag(pointer);
                Assert.That(target.IsDropTarget, Is.True);
                app.EndDrag(pointer);
                Assert.That(app.IsDragging, Is.False);
                world.Tick(1);
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.That(world.ExtractionFilter(jug), Is.SameAs(filter));
                Assert.That(world.ExtractionJug(filter), Is.SameAs(jug));
                Assert.That(world.LiquidVolume(jug), Is.EqualTo(80));
                Assert.That(world.SolidMass(filter), Is.EqualTo(5));
            }
        }

        [UnityTest] public IEnumerator HigherDisplayLimitAllowsOneTopAndEveryRemainingLowerCup()
        {
            foreach (string id in new[] { "s.cups.small", "s.cups.large" })
            {
                var source = app.World.Find(id); var view = app.View(id); var stack = view.GetComponent<FreeCafeCupStackVisual>();
                stack.maxVisibleCups = 33;
                float pitch = stack.StackStep;
                foreach (int count in new[] { 0, 1, 2, 7, 30, 33 })
                {
                    source.stock = count; app.DebugRefreshViews();
                    Assert.That(stack.VisibleCupCount, Is.EqualTo(count));
                    Assert.That(stack.StackStep, Is.EqualTo(pitch));
                    Assert.That(view.body.enabled, Is.EqualTo(count > 0));
                    string cupId = app.World.Spec(source.specId).sourceItemId;
                    Assert.That(view.body.sprite, Is.SameAs(app.artLibrary.Resolve(cupId)));
                    int lowerCount = 0;
                    foreach (var child in view.GetComponentsInChildren<SpriteRenderer>())
                        if (child.name.StartsWith("NestedCup") && child.gameObject.activeSelf)
                        {
                            lowerCount++;
                            Assert.That(child.sprite, Is.SameAs(app.artLibrary.Resolve(cupId, "nested")));
                        }
                    Assert.That(lowerCount, Is.EqualTo(Mathf.Max(0, count - 1)));
                    Assert.That(view.body.transform.localPosition.y, Is.EqualTo(Mathf.Max(0, count - 1) * pitch).Within(.0001f));
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator TakingTopPromotesNextImmediatelyWithoutReflowingBottom()
        {
            var source = app.World.Find("s.cups.large"); source.stock = 7; app.DebugRefreshViews();
            var view = app.View(source.id); var stack = view.GetComponent<FreeCafeCupStackVisual>();
            stack.maxVisibleCups = 10; app.DebugRefreshViews();
            float topY = view.body.transform.localPosition.y;
            var bottom = view.transform.Find("NestedCup0"); var bottomPosition = bottom.localPosition;
            Assert.That(app.World.Take(source.id, AreaId.Counter, 0, -1, out var cup).success, Is.True);
            Assert.That(source.stock, Is.EqualTo(6)); Assert.That(stack.VisibleCupCount, Is.EqualTo(6));
            Assert.That(view.body.sprite, Is.SameAs(app.artLibrary.Resolve("cup.large")));
            Assert.That(view.body.transform.localPosition.y, Is.EqualTo(topY - stack.StackStep).Within(.0001f));
            Assert.That(bottom.localPosition, Is.EqualTo(bottomPosition));
            yield return null;
        }
        [UnityTest] public IEnumerator DisplayLimitCapsBothCupStacksWithoutChangingStockAndSaveData()
        {
            foreach (string id in new[] { "s.cups.small", "s.cups.large" })
            {
                var source = app.World.Find(id); var view = app.View(id); var stack = view.GetComponent<FreeCafeCupStackVisual>();
                stack.maxVisibleCups = 5; source.stock = 6; app.DebugRefreshViews();
                float topY = view.body.transform.localPosition.y;
                Assert.That(stack.VisibleCupCount, Is.EqualTo(5)); Assert.That(source.stock, Is.EqualTo(6));
                var saved = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(app.World.State));
                Assert.That(saved.entities.Find(e => e.id == id).stock, Is.EqualTo(6));
                var pointer = new PointerEventData(EventSystem.current) { position = app.worldCamera.WorldToScreenPoint(stack.TopPosition) };
                app.BeginDrag(view, pointer); Assert.That(app.IsDragging, Is.True);
                Assert.That(stack.VisibleCupCount, Is.EqualTo(5), "Reserved pickup reduces real stock 6 to 5 before applying display limit");
                Assert.That(view.body.transform.localPosition.y, Is.EqualTo(topY));
                Assert.That(source.stock, Is.EqualTo(6)); app.CancelDrag();
                Assert.That(stack.VisibleCupCount, Is.EqualTo(5)); Assert.That(source.stock, Is.EqualTo(6));
                Assert.That(app.World.Take(id, AreaId.Counter, 0, -1, out var first).success, Is.True);
                Assert.That(source.stock, Is.EqualTo(5)); Assert.That(stack.VisibleCupCount, Is.EqualTo(5));
                Assert.That(view.body.transform.localPosition.y, Is.EqualTo(topY));
                Assert.That(app.World.Take(id, AreaId.Counter, 0, -1, out var second).success, Is.True);
                Assert.That(source.stock, Is.EqualTo(4)); Assert.That(stack.VisibleCupCount, Is.EqualTo(4));
                Assert.That(view.body.transform.localPosition.y, Is.EqualTo(topY - stack.StackStep).Within(.0001f));
                int lowerCount = 0;
                foreach (var renderer in view.GetComponentsInChildren<SpriteRenderer>())
                    if (renderer.name.StartsWith("NestedCup") && renderer.gameObject.activeSelf) lowerCount++;
                Assert.That(lowerCount, Is.EqualTo(3));
                Assert.That(view.body.sprite, Is.SameAs(app.artLibrary.Resolve(app.World.Spec(source.specId).sourceItemId)));
                stack.maxVisibleCups = 2; app.DebugRefreshViews();
                Assert.That(stack.VisibleCupCount, Is.EqualTo(2)); Assert.That(source.stock, Is.EqualTo(4));
                var otherStack = app.View(id == "s.cups.small" ? "s.cups.large" : "s.cups.small").GetComponent<FreeCafeCupStackVisual>();
                Assert.That(otherStack.maxVisibleCups, Is.Not.EqualTo(stack.maxVisibleCups), "Display limits are independent");
                Assert.That(app.World.Restore(saved).success, Is.True);
                Assert.That(app.World.Find(id).stock, Is.EqualTo(6)); Assert.That(stack.VisibleCupCount, Is.EqualTo(2));
                stack.maxVisibleCups = 5; app.DebugRefreshViews();
                yield return null;
            }
        }
        [UnityTest] public IEnumerator BothCupSourcesKeepCompleteSpritesThroughoutUncommittedDrag()
        {
            foreach (string id in new[] { "s.cups.small", "s.cups.large" })
            {
                var source = app.World.Find(id); var view = app.View(id);
                string cupId = app.World.Spec(source.specId).sourceItemId;
                var stack = view.GetComponent<FreeCafeCupStackVisual>();
                var pointer = new PointerEventData(EventSystem.current) { position = app.worldCamera.WorldToScreenPoint(stack.TopPosition) };
                app.BeginDrag(view, pointer); Assert.That(app.IsDragging, Is.True);
                var preview = app.DraggedView;
                Assert.That(preview.EntityId, Is.Null);
                Assert.That(preview.body.sprite, Is.SameAs(app.artLibrary.Resolve(cupId)));
                pointer.position = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter, 0, -.5f));
                app.MoveDrag(pointer); yield return new WaitForSecondsRealtime(.2f);
                app.DebugRefreshViews();
                Assert.That(preview.body.sprite, Is.SameAs(app.artLibrary.Resolve(cupId)));
                Assert.That(preview.body.sprite, Is.Not.SameAs(app.artLibrary.Resolve(cupId, "nested")));
                app.CancelDrag(); yield return null;
            }
        }
        [UnityTest] public IEnumerator CupPickupReservesTopVisuallyAndCancelRestoresWithoutStockLoss()
        {
            var source = app.World.Find("s.cups.small"); source.stock = 7; app.DebugRefreshViews();
            var view = app.View(source.id); var stack = view.GetComponent<FreeCafeCupStackVisual>();
            stack.maxVisibleCups = 10; app.DebugRefreshViews(); var top = stack.TopPosition;
            var pointer = new PointerEventData(EventSystem.current) { position = app.worldCamera.WorldToScreenPoint(top) };
            app.BeginDrag(view, pointer); Assert.That(app.IsDragging, Is.True);
            Assert.That(Vector3.Distance(app.DraggedView.transform.position, top), Is.LessThan(.001f));
            Assert.That(source.stock, Is.EqualTo(7)); Assert.That(stack.VisibleCupCount, Is.EqualTo(6));
            app.DebugRefreshViews(); Assert.That(stack.VisibleCupCount, Is.EqualTo(6));
            var preview = app.DraggedView;
            app.CancelDrag(); Assert.That(source.stock, Is.EqualTo(7)); Assert.That(stack.VisibleCupCount, Is.EqualTo(7));
            Assert.That(preview.gameObject.activeSelf, Is.False, "Restoring stock must not leave a duplicate returning preview");
            yield return null;
            app.BeginDrag(view, pointer); pointer.position = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter, 1, -.5f));
            app.MoveDrag(pointer); yield return new WaitForSecondsRealtime(.4f); app.EndDrag(pointer);
            Assert.That(source.stock, Is.EqualTo(6)); Assert.That(stack.VisibleCupCount, Is.EqualTo(6));
        }
        [UnityTest] public IEnumerator RightButtonRetainsVesselAndTiltedCupStaysWhereItWasHeld()
        {
            var priorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            var priorBackground = InputSystem.settings.backgroundBehavior;
            var mouse = InputSystem.AddDevice<Mouse>("CounterArtMouse");
            var events = EventSystem.current; bool priorEvents = events.enabled;
            // This test drives the runtime directly; do not also dispatch the synthetic pointer through UI.
            events.enabled = false;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            try
            {
                app.World.Take("s.cups.small", AreaId.Counter, -1, -.5f, out var cup);
                var view = app.View(cup.id); var pointer = Pointer(view);
                var held = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter, 1, -.5f));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer.position }.WithButton(MouseButton.Left));
                yield return null;
                app.BeginDrag(view, pointer); Assert.That(app.IsDragging, Is.True);
                pointer.position = held; app.MoveDrag(pointer);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = held }.WithButton(MouseButton.Right));
                yield return null;
                app.MoveDrag(pointer); app.EndDrag(pointer);
                var frozenPosition = app.WorldToArea(AreaId.Counter, view.DragPosition);
                Assert.That(frozenPosition.x, Is.GreaterThan(cup.x), "Tilt locks the current visible held pose, not its old home");
                Assert.That(app.IsDragging, Is.True, "Right button must retain the lifted vessel after left release");
                app.enabled = true;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = held });
                yield return null; yield return null;
                Assert.That(app.IsDragging, Is.False);
                Assert.That(cup.x, Is.EqualTo(frozenPosition.x).Within(.02f)); Assert.That(cup.y, Is.EqualTo(frozenPosition.y).Within(.02f));
            }
            finally
            {
                app.enabled = false; app.CancelDrag(); InputSystem.RemoveDevice(mouse);
                events.enabled = priorEvents;
                InputSystem.settings.editorInputBehaviorInPlayMode = priorInput;
                InputSystem.settings.backgroundBehavior = priorBackground;
            }
        }
        [UnityTest] public IEnumerator CaptureSuppliedCounterArt()
        {
            yield return new WaitForSecondsRealtime(.4f);
            var camera = app.worldCamera; var prior = camera.targetTexture; var active = RenderTexture.active; float aspect = camera.aspect;
            var rt = new RenderTexture(1920, 1080, 24); var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.aspect = 16f / 9; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
                string folder = Path.GetFullPath("output/counter-art-validation"); Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "counter-art.png"), texture.EncodeToPNG());
            }
            finally { camera.targetTexture = prior; camera.aspect = aspect; RenderTexture.active = active; Object.Destroy(rt); Object.Destroy(texture); }
        }
    }
}
