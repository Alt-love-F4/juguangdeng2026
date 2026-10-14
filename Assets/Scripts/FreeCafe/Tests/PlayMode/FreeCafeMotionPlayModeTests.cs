using System;
using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeMotionPlayModeTests
    {
        private FreeCafeRuntime app;
        private Mouse mouse;
        private Keyboard placementKeyboard;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
        private InputSettings.BackgroundBehavior previousInputBehavior;

        [UnitySetUp] public IEnumerator Setup()
        {
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None)) panel.Cancel();
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousInputBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>("FreeCafeMotionRegressionMouse");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null; yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            Assert.That(app, Is.Not.Null);
            app.productionMode = false;
            app.areaManager.panSmoothTime = 0;
            Assert.That(app.World.Restore(new CafeWorld(app.World.Catalog).State).success, Is.True);
            app.Enter(AreaId.Counter);
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (app != null) app.CancelDrag();
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (placementKeyboard != null && placementKeyboard.added) InputSystem.RemoveDevice(placementKeyboard);
            placementKeyboard = null;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            InputSystem.settings.backgroundBehavior = previousInputBehavior;
            Time.timeScale = 1;
            yield return null;
        }

        private PointerEventData Pointer(float x, float y) => new PointerEventData(EventSystem.current)
        { position = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter, x, y)) };

        private IEnumerator EnablePlacementOnly()
        {
            // Authored machine colliders may overlap these arbitrary coordinates.
            // Explicit Shift placement isolates view motion from receiver interaction rules.
            placementKeyboard = InputSystem.AddDevice<Keyboard>("FreeCafeMotionPlacementKeyboard");
            InputSystem.QueueStateEvent(placementKeyboard, new KeyboardState(Key.LeftShift));
            yield return null; yield return null;
            Assert.That(Keyboard.current, Is.SameAs(placementKeyboard));
            Assert.That(placementKeyboard.leftShiftKey.isPressed, Is.True);
        }

        private WorldEntity TakeCup()
        {
            Assert.That(app.World.Take("s.cups.large", AreaId.Counter, 0, -2, out var cup).success, Is.True);
            Assert.That(app.View(cup.id), Is.Not.Null);
            return cup;
        }

        private IEnumerator WaitUntil(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        private IEnumerator WaitForPosition(FreeEntityView view, float x, float y)
        {
            yield return WaitUntil(() => view != null && Vector3.Distance(view.transform.position,
                app.AreaToWorld(AreaId.Counter, x, y)) < .001f, "View must converge to the logical position within three seconds");
        }

        [UnityTest] public IEnumerator RightwardDragLeansPositiveZWithoutMovingTheLogicalEntity()
        {
            yield return CheckDragLean(3, 1);
        }

        [UnityTest] public IEnumerator LeftwardDragLeansNegativeZWithoutMovingTheLogicalEntity()
        {
            yield return CheckDragLean(-3, -1);
        }

        private IEnumerator CheckDragLean(float targetX, float direction)
        {
            var cup = TakeCup(); var view = app.View(cup.id);
            Vector3 origin = view.transform.position;
            Physics2D.SyncTransforms(); app.BeginDrag(view, Pointer(cup.x, cup.y));
            Assert.That(app.IsDragging, Is.True);
            app.MoveDrag(Pointer(targetX, -2));
            Assert.That(view.transform.position, Is.EqualTo(origin), "MoveDrag must update the target, not teleport the visible view");
            Assert.That(Vector3.Distance(view.DragPosition, app.AreaToWorld(AreaId.Counter, targetX, -2)), Is.LessThan(.001f));
            yield return WaitUntil(() => direction * Mathf.DeltaAngle(0, view.body.transform.localEulerAngles.z) > .1f,
                "Visible drag velocity must lean in the signed Z direction");
            Assert.That(direction * (view.transform.position.x - origin.x), Is.GreaterThan(0));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, view.body.transform.localEulerAngles.z)), Is.LessThanOrEqualTo(view.maxDragLean + .01f));
            Assert.That(cup.x, Is.EqualTo(0)); Assert.That(cup.y, Is.EqualTo(-2));
            app.CancelDrag();
            yield return WaitForPosition(view, 0, -2);
            yield return WaitUntil(() => Mathf.Abs(Mathf.DeltaAngle(0, view.body.transform.localEulerAngles.z)) < .01f,
                "Cancelled drag lean must return upright within three seconds");
        }

        [UnityTest] public IEnumerator ReleaseCommitsCursorImmediatelyButPreservesVisibleMotionThroughRefresh()
        {
            yield return EnablePlacementOnly();
            var cup = TakeCup(); var view = app.View(cup.id);
            Physics2D.SyncTransforms(); app.BeginDrag(view, Pointer(cup.x, cup.y));
            Assert.That(app.IsDragging, Is.True);
            app.MoveDrag(Pointer(3, -2.5f));
            yield return WaitUntil(() => view.transform.localPosition.x > .1f, "Dragging must visibly move the cup");
            Vector3 visible = view.transform.position;
            // Release at a new cursor target, not at the lagging visible pose.
            app.EndDrag(Pointer(-2, -2.5f));
            Assert.That(app.IsDragging, Is.False);
            Assert.That(cup.x, Is.EqualTo(-2).Within(.001f)); Assert.That(cup.y, Is.EqualTo(-2.5f).Within(.001f));
            Assert.That(app.View(cup.id), Is.SameAs(view));
            Assert.That(view.transform.position, Is.EqualTo(visible), "Release must not snap to the committed cursor target");
            Assert.That(Vector3.Distance(visible, app.AreaToWorld(AreaId.Counter, cup.x, cup.y)), Is.GreaterThan(.1f));
            app.DebugRefreshViews(); view.Refresh(cup);
            Assert.That(view.transform.position, Is.EqualTo(visible), "Explicit refresh must retain the visible pose and smooth target");
            yield return WaitForPosition(view, cup.x, cup.y);
            // Cross the runtime's periodic refresh interval after convergence as well.
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(app.View(cup.id), Is.SameAs(view));
            Assert.That(Vector3.Distance(view.transform.position, app.AreaToWorld(AreaId.Counter, -2, -2.5f)), Is.LessThan(.001f));
            Assert.That(cup.x, Is.EqualTo(-2).Within(.001f)); Assert.That(cup.y, Is.EqualTo(-2.5f).Within(.001f));
        }

        [UnityTest] public IEnumerator SourcePreviewIsAdoptedWithoutSnappingAndWithdrawsStockExactlyOnce()
        {
            yield return EnablePlacementOnly();
            var source = app.World.Find("s.beans"); int stock = source.stock, count = app.World.State.entities.Count;
            var sourceView = app.View(source.id);
            Physics2D.SyncTransforms(); app.BeginDrag(sourceView, Pointer(source.x, source.y));
            Assert.That(app.IsDragging, Is.True);
            var preview = GameObject.Find("UncommittedPickup").GetComponent<FreeEntityView>();
            Assert.That(preview, Is.Not.SameAs(sourceView));
            Assert.That(source.stock, Is.EqualTo(stock), "Preview creation must not withdraw stock");
            app.MoveDrag(Pointer(2.137f, -2.613f));
            Vector3 visible = preview.transform.position;
            app.EndDrag(Pointer(2.137f, -2.613f));
            Assert.That(app.IsDragging, Is.False);
            Assert.That(source.stock, Is.EqualTo(stock - 1)); Assert.That(app.World.State.entities.Count, Is.EqualTo(count + 1));
            var placed = app.World.Find(preview.EntityId);
            Assert.That(placed, Is.Not.Null); Assert.That(placed.specId, Is.EqualTo("beans"));
            Assert.That(placed.x, Is.EqualTo(2.137f).Within(.001f)); Assert.That(placed.y, Is.EqualTo(-2.613f).Within(.001f));
            Assert.That(app.View(placed.id), Is.SameAs(preview), "The visible preview must become the committed entity view");
            Assert.That(app.View(source.id), Is.SameAs(sourceView));
            Assert.That(preview.transform.position, Is.EqualTo(visible), "Adoption must not snap the preview to its target");
            Assert.That(preview.hitbox.enabled, Is.True);
            app.EndDrag(Pointer(2.137f, -2.613f)); app.DebugRefreshViews();
            yield return WaitForPosition(preview, placed.x, placed.y);
            Assert.That(source.stock, Is.EqualTo(stock - 1), "Duplicate release and refresh must not consume another unit");
            Assert.That(app.World.State.entities.Count, Is.EqualTo(count + 1));
            int matchingViews = 0;
            foreach (var other in app.areaRoots[1].GetComponentsInChildren<FreeEntityView>(true))
                if (other.EntityId == placed.id) matchingViews++;
            Assert.That(matchingViews, Is.EqualTo(1), "Adoption must not create a duplicate view");
        }

        [UnityTest] public IEnumerator MachineOutputPreviewIsAdoptedAndClearsOnlyTheCompletedJob()
        {
            yield return EnablePlacementOnly();
            Assert.That(app.World.Take("s.beans", AreaId.Counter, 0, -2, out var bean).success, Is.True);
            Assert.That(app.World.Interact(bean.id, "m.grinder").success, Is.True);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(app.World.Operate("m.grinder").success, Is.True);
                if (i < 2) app.World.Tick(.5f);
            }
            var grinder = app.World.Find("m.grinder"); var machineView = app.View(grinder.id);
            Vector3 machinePosition = machineView.transform.position;
            int stock = app.World.Find("s.beans").stock, count = app.World.State.entities.Count;
            Physics2D.SyncTransforms(); app.BeginDrag(machineView, Pointer(grinder.x, grinder.y));
            Assert.That(app.IsDragging, Is.True);
            var preview = GameObject.Find("UncommittedMachineOutput").GetComponent<FreeEntityView>();
            app.MoveDrag(Pointer(2.137f, -2.613f)); Vector3 visible = preview.transform.position;
            app.EndDrag(Pointer(2.137f, -2.613f));
            Assert.That(app.IsDragging, Is.False); Assert.That(grinder.task, Is.Null);
            Assert.That(bean.ownerId, Is.Null); Assert.That(bean.specId, Is.EqualTo("grounds"));
            Assert.That(bean.x, Is.EqualTo(2.137f).Within(.001f)); Assert.That(bean.y, Is.EqualTo(-2.613f).Within(.001f));
            Assert.That(app.View(bean.id), Is.SameAs(preview));
            Assert.That(preview.transform.position, Is.EqualTo(visible));
            Assert.That(machineView.transform.position, Is.EqualTo(machinePosition));
            yield return WaitForPosition(preview, bean.x, bean.y);
            app.DebugRefreshViews();
            Assert.That(app.World.Find("s.beans").stock, Is.EqualTo(stock));
            Assert.That(app.World.State.entities.Count, Is.EqualTo(count));
        }

        [UnityTest] public IEnumerator CancelReturnsSmoothlyAndRegrabDuringReturnKeepsTheSameVisiblePose()
        {
            yield return EnablePlacementOnly();
            var cup = TakeCup(); var view = app.View(cup.id);
            Physics2D.SyncTransforms(); app.BeginDrag(view, Pointer(cup.x, cup.y));
            app.MoveDrag(Pointer(3, -2.5f));
            yield return WaitUntil(() => view.transform.localPosition.x > .5f, "Dragging must move the cup away from home");
            Vector3 visible = view.transform.position;
            app.CancelDrag();
            Assert.That(app.IsDragging, Is.False); Assert.That(view.transform.position, Is.EqualTo(visible));
            Assert.That(cup.x, Is.EqualTo(0)); Assert.That(cup.y, Is.EqualTo(-2));
            Assert.That(view.hitbox.enabled, Is.True);
            yield return WaitForPosition(view, 0, -2);

            Physics2D.SyncTransforms(); app.BeginDrag(view, Pointer(0, -2)); app.MoveDrag(Pointer(3, -2.5f));
            yield return WaitUntil(() => view.transform.localPosition.x > .5f, "Second drag must move away from home");
            app.CancelDrag(); yield return null;
            visible = view.transform.position;
            Assert.That(Vector3.Distance(visible, app.AreaToWorld(AreaId.Counter, 0, -2)), Is.GreaterThan(.1f));
            var regrab = new PointerEventData(EventSystem.current) { position = app.worldCamera.WorldToScreenPoint(visible) };
            Physics2D.SyncTransforms(); app.BeginDrag(view, regrab);
            Assert.That(app.IsDragging, Is.True); Assert.That(view.transform.position, Is.EqualTo(visible), "Regrab must not reset to logical home");
            app.EndDrag(Pointer(-2, -2.5f));
            Assert.That(app.IsDragging, Is.False); Assert.That(app.View(cup.id), Is.SameAs(view));
            Assert.That(cup.x, Is.EqualTo(-2).Within(.001f)); Assert.That(cup.y, Is.EqualTo(-2.5f).Within(.001f));
            yield return WaitForPosition(view, cup.x, cup.y);
        }

        private void QueueMouse(Vector2 position, bool held)
        {
            var state = new MouseState { position = position };
            if (held) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state);
        }

        [UnityTest] public IEnumerator QueuedLeftMouseDragsKettleOntoStoveWithoutEnteringTiltMode()
        {
            Assert.That(app.World.EnterProduction().success, Is.True);
            Assert.That(app.World.TakeAndInteract("s.water", "m.heater", AreaId.Counter, 0, 0).success, Is.True);
            var kettle = app.World.Find("m.heater"); var stove = app.World.Find("m.stove"); var view = app.View(kettle.id);
            Physics2D.SyncTransforms();
            Vector2 start = app.worldCamera.WorldToScreenPoint(view.transform.position);
            Vector2 end = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter, stove.x, stove.y));
            QueueMouse(start, false); yield return null; yield return null;
            QueueMouse(start, true); yield return null; yield return null;
            QueueMouse(start + Vector2.right * 30, true); yield return null; yield return null;
            Assert.That(app.IsDragging, Is.True, "Queued left mouse must reach the kettle through EventSystem raycasts");
            Assert.That(mouse.rightButton.isPressed, Is.False);
            QueueMouse(end, true); yield return null; yield return null;
            Assert.That(app.World.LiquidVolume(kettle), Is.EqualTo(200), "Plain left dragging must not pour");
            QueueMouse(end, false); yield return null; yield return null;
            Assert.That(app.IsDragging, Is.False, "Left release must end the drag through UpdateVesselInput");
            Assert.That(stove.production.heatingVesselId, Is.EqualTo(kettle.id), "Real input release must commit the stove receiver");
            Assert.That(kettle.x, Is.EqualTo(stove.x));
            float dockHeight = stove.y + app.World.Spec(stove.specId).height * .45f + app.World.Spec(kettle.specId).height * .5f;
            Assert.That(kettle.y, Is.EqualTo(dockHeight).Within(.001f));
            Assert.That(app.World.LiquidVolume(kettle), Is.EqualTo(200));
            Assert.That(app.View(kettle.id), Is.SameAs(view));
            yield return WaitForPosition(view, kettle.x, kettle.y);
        }

        [UnityTest] public IEnumerator KettleStoveDockCommitsImmediatelyAndMovesTheSameViewSmoothly()
        {
            Assert.That(app.World.EnterProduction().success, Is.True);
            Assert.That(app.World.TakeAndInteract("s.water", "m.heater", AreaId.Counter, 0, 0).success, Is.True);
            var kettle = app.World.Find("m.heater"); var stove = app.World.Find("m.stove"); var view = app.View(kettle.id);
            Vector3 visible = view.transform.position;
            Assert.That(Vector3.Distance(visible, app.AreaToWorld(AreaId.Counter, stove.x, stove.y)), Is.GreaterThan(.1f));
            Physics2D.SyncTransforms(); app.BeginDrag(view, Pointer(kettle.x, kettle.y));
            Assert.That(app.IsDragging, Is.True); app.EndDrag(Pointer(stove.x, stove.y));
            Assert.That(app.IsDragging, Is.False);
            Assert.That(stove.production.heatingVesselId, Is.EqualTo(kettle.id));
            Assert.That(kettle.x, Is.EqualTo(stove.x));
            float dockHeight = stove.y + app.World.Spec(stove.specId).height * .45f + app.World.Spec(kettle.specId).height * .5f;
            Assert.That(kettle.y, Is.EqualTo(dockHeight).Within(.001f));
            Assert.That(app.View(kettle.id), Is.SameAs(view)); Assert.That(view.transform.position, Is.EqualTo(visible), "Stove docking must not teleport");
            app.DebugRefreshViews(); Assert.That(view.transform.position, Is.EqualTo(visible));
            yield return WaitForPosition(view, kettle.x, kettle.y);
            app.Click(app.View(stove.id));
            Assert.That(kettle.production.phase, Is.EqualTo(ProductionPhase.Processing));
            app.World.Tick(4); yield return null;
            Assert.That(kettle.contents[0].temperature, Is.EqualTo(LiquidTemperature.Boiling));
            Assert.That(app.View(kettle.id), Is.SameAs(view));
        }
    }
}
