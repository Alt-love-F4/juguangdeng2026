using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeCupStackTiltPlayModeTests
    {
        private FreeCafeRuntime app;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private readonly List<BaseInputModule> disabledModules = new List<BaseInputModule>();
        private readonly List<DialoguePanel> disabledDialogue = new List<DialoguePanel>();
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private WorldEntity source;
        private FreeCafeCupStackVisual stack;
        private FreeEntityView preview;
        private Vector2 anchor;
        private Vector3 originalTop;
        private int stock, entityCount, nextId;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None)) panel.Cancel();
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>("FreeCafeCupStackTiltMouse");
            MouseAt(Vector2.zero, false, false);
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null; yield return null;
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None))
            {
                panel.Cancel();
                if (panel.enabled) { disabledDialogue.Add(panel); panel.enabled = false; }
            }
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            Assert.That(app, Is.Not.Null);
            app.productionMode = true;
            app.areaManager.panSmoothTime = 0;
            Assert.That(app.World, Is.Not.Null);
            Assert.That(app.World.EnterProduction().success, Is.True);
            yield return null;
            Assert.That(app.World.ProductionEnabled, Is.True);
            Assert.That(app.InputBlocked, Is.False);
            Assert.That(EventSystem.current, Is.Not.Null);
            // Direct BeginDrag owns dispatch; retain EventSystem/raycasters for real pixel hit testing.
            foreach (var module in EventSystem.current.GetComponents<BaseInputModule>())
                if (module.enabled) { disabledModules.Add(module); module.enabled = false; }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (app != null) app.CancelDrag();
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            mouse = null;
            foreach (var module in disabledModules) if (module != null) module.enabled = true;
            disabledModules.Clear();
            foreach (var panel in disabledDialogue) if (panel != null) panel.enabled = true;
            disabledDialogue.Clear();
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            Time.timeScale = 1;
            yield return null;
        }

        private PointerEventData Pointer(Vector2 pixel) => new PointerEventData(EventSystem.current) { position = pixel };

        private void MouseAt(Vector2 pixel, bool left, bool right)
        {
            var state = new MouseState { position = pixel };
            if (left) state = state.WithButton(MouseButton.Left);
            if (right) state = state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse, state);
        }

        private Vector2 FindClearPlacement()
        {
            Physics2D.SyncTransforms();
            Canvas.ForceUpdateCanvases();
            var area = app.World.Area(AreaId.Counter);
            for (float y = -area.halfHeight + .5f; y < area.halfHeight; y += .25f)
                for (float x = -area.halfWidth + .5f; x < area.halfWidth; x += .25f)
                {
                    if (!app.World.CanPosition(preview.PrefabSpecId, AreaId.Counter, x, y).success) continue;
                    Vector2 pixel = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter, x, y));
                    var rect = app.worldCamera.pixelRect;
                    if (pixel.x < rect.xMin + 100 || pixel.x > rect.xMax - 100 ||
                        pixel.y < rect.yMin + 100 || pixel.y > rect.yMax - 100) continue;
                    hits.Clear();
                    EventSystem.current.RaycastAll(Pointer(pixel), hits);
                    if (hits.Count == 0) return pixel;
                }
            Assert.Fail("No legal on-camera cup placement pixel free of UI and scene receivers");
            return default;
        }

        private IEnumerator BeginSourceTilt(string sourceId)
        {
            source = app.World.Find(sourceId);
            Assert.That(source, Is.Not.Null);
            // Avoid authored zero stock and make the reservation visible below the display cap.
            source.stock = 3;
            var sourceView = app.View(sourceId);
            stack = sourceView.GetComponent<FreeCafeCupStackVisual>();
            Assert.That(stack, Is.Not.Null);
            stack.maxVisibleCups = 5;
            app.DebugRefreshViews();
            originalTop = stack.TopPosition;
            stock = source.stock;
            entityCount = app.World.State.entities.Count;
            nextId = app.World.State.nextId;
            Vector2 start = app.worldCamera.WorldToScreenPoint(originalTop);
            Assert.That(app.worldCamera.pixelRect.Contains(start), Is.True, "Source top must be on camera");
            Physics2D.SyncTransforms();
            Canvas.ForceUpdateCanvases();
            hits.Clear();
            EventSystem.current.RaycastAll(Pointer(start), hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.GetComponentInParent<FreeEntityView>(), Is.SameAs(sourceView),
                "Source pixel must hit this stack, not an overlapping machine or UI");
            MouseAt(start, true, false);
            yield return null; yield return null;
            app.BeginDrag(sourceView, Pointer(start));
            Assert.That(app.IsDragging, Is.True, app.Hint);
            preview = app.DraggedView;
            Assert.That(preview, Is.Not.SameAs(sourceView));
            Assert.That(preview.EntityId, Is.Null.Or.Empty);
            AssertUncommitted();
            anchor = FindClearPlacement();
            MouseAt(anchor, true, false);
            app.MoveDrag(Pointer(anchor));
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Vector3.Distance(preview.transform.position, preview.DragPosition) > .001f && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Vector3.Distance(preview.transform.position, preview.DragPosition), Is.LessThan(.001f));
            yield return null;
            MouseAt(anchor, true, true);
            yield return null; yield return null;
            AssertUncommitted();
        }

        private void AssertUncommitted()
        {
            Assert.That(app.IsDragging, Is.True);
            Assert.That(app.DraggedView, Is.SameAs(preview));
            Assert.That(source.stock, Is.EqualTo(stock));
            Assert.That(app.World.State.entities.Count, Is.EqualTo(entityCount));
            Assert.That(app.World.State.nextId, Is.EqualTo(nextId), "Preview must not allocate a real cup");
            Assert.That(stack.VisibleCupCount, Is.EqualTo(stock - 1), "Only the visual top is reserved");
            Assert.That(preview.EntityId, Is.Null.Or.Empty);
        }

        private IEnumerator WaitForAngle(float angle)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (preview != null && Mathf.Abs(Mathf.DeltaAngle(preview.body.transform.localEulerAngles.z, angle)) > 1f &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(preview, Is.Not.Null);
            Assert.That(Mathf.DeltaAngle(0, preview.body.transform.localEulerAngles.z), Is.EqualTo(angle).Within(1f),
                "Empty source preview must visibly tilt; liquid flow is not the tilt-mode signal");
        }

        private IEnumerator AssertCommittedOnce()
        {
            yield return null; yield return null;
            Assert.That(app.IsDragging, Is.False);
            Assert.That(source.stock, Is.EqualTo(stock - 1));
            Assert.That(stack.VisibleCupCount, Is.EqualTo(stock - 1), "Commit must clear the temporary reservation");
            Assert.That(app.World.State.entities.Count, Is.EqualTo(entityCount + 1));
            Assert.That(app.World.State.nextId, Is.EqualTo(nextId + 1), "Exactly one Take must allocate a cup");
            Assert.That(preview, Is.Not.Null, "Do not discard the held preview when both buttons release");
            Assert.That(preview.EntityId, Is.Not.Null.And.Not.Empty);
            var cup = app.World.Find(preview.EntityId);
            Assert.That(cup, Is.Not.Null);
            Assert.That(app.World.IsCup(cup), Is.True);
            Assert.That(cup.specId, Is.EqualTo(app.World.Spec(source.specId).sourceItemId));
            Assert.That(app.View(cup.id), Is.SameAs(preview), "Preview must become the actual registered cup view");
            Assert.That(preview.CanInteract, Is.True);
            Assert.That(app.World.LiquidVolume(cup), Is.Zero, "Tilting an empty cup must not create liquid");
            yield return WaitForAngle(0);
            // Late/repeated pointer-end delivery cannot withdraw a second cup.
            app.EndDrag(Pointer(anchor));
            yield return null; yield return null;
            Assert.That(source.stock, Is.EqualTo(stock - 1));
            Assert.That(app.World.State.entities.Count, Is.EqualTo(entityCount + 1));
            Assert.That(app.World.State.nextId, Is.EqualTo(nextId + 1));
        }

        [UnityTest]
        public IEnumerator SourcePreviewTiltsBothDirectionsWithoutTakingAndCancelRestoresStack()
        {
            yield return BeginSourceTilt("s.cups.small");
            Vector3 frozen = preview.transform.position;
            MouseAt(anchor + Vector2.up * 90, true, true);
            yield return WaitForAngle(-app.maxPourAngle);
            AssertUncommitted();
            Assert.That(Vector3.Distance(preview.transform.position, frozen), Is.LessThan(.001f));
            MouseAt(anchor + Vector2.down * 90, true, true);
            yield return WaitForAngle(app.maxPourAngle);
            AssertUncommitted();
            Assert.That(Vector3.Distance(preview.transform.position, frozen), Is.LessThan(.001f), "Right drag locks position");
            app.CancelDrag();
            MouseAt(anchor, false, false);
            Assert.That(app.IsDragging, Is.False);
            Assert.That(source.stock, Is.EqualTo(stock));
            Assert.That(app.World.State.entities.Count, Is.EqualTo(entityCount));
            Assert.That(app.World.State.nextId, Is.EqualTo(nextId));
            Assert.That(stack.VisibleCupCount, Is.EqualTo(stock));
            Assert.That(Vector3.Distance(stack.TopPosition, originalTop), Is.LessThan(.001f));
            Assert.That(preview.gameObject.activeSelf, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReleasingRightResumesLeftDragAndFinalReleaseTakesExactlyOneCup()
        {
            yield return BeginSourceTilt("s.cups.large");
            MouseAt(anchor + Vector2.down * 90, true, true);
            yield return WaitForAngle(app.maxPourAngle);
            MouseAt(anchor, true, false);
            yield return null; yield return null;
            AssertUncommitted();
            yield return WaitForAngle(0);
            Vector3 held = preview.transform.position;
            Vector2 resumed = anchor + Vector2.right * 20;
            MouseAt(resumed, true, false);
            app.MoveDrag(Pointer(resumed));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(Vector3.Distance(preview.transform.position, held), Is.GreaterThan(.01f), "Left drag must resume after right release");
            AssertUncommitted();
            // Return to the verified clear placement before committing.
            MouseAt(anchor, true, false);
            app.MoveDrag(Pointer(anchor));
            yield return new WaitForSecondsRealtime(.3f);
            MouseAt(anchor, false, false);
            yield return null;
            app.EndDrag(Pointer(anchor));
            yield return AssertCommittedOnce();
        }

        [UnityTest]
        public IEnumerator ReleasingLeftBeforeRightRetainsPreviewThenCommitsExactlyOneCup()
        {
            yield return BeginSourceTilt("s.cups.small");
            MouseAt(anchor + Vector2.up * 90, true, true);
            yield return WaitForAngle(-app.maxPourAngle);
            Vector3 frozen = preview.transform.position;
            MouseAt(anchor + Vector2.up * 90, false, true);
            yield return null; yield return null;
            app.EndDrag(Pointer(anchor + Vector2.up * 90));
            AssertUncommitted();
            Assert.That(Vector3.Distance(preview.transform.position, frozen), Is.LessThan(.001f));
            MouseAt(anchor, false, false);
            // No explicit EndDrag here: right-up must finish the previously deferred left-up.
            yield return AssertCommittedOnce();
            Assert.That(Vector3.Distance(preview.transform.position, frozen), Is.LessThan(.01f), "Commit at the locked pose, not the tilt cursor");
        }
    }
}
