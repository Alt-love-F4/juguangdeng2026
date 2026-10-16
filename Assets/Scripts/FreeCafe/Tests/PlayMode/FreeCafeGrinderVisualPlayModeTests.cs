using System.Collections;
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
    public sealed class FreeCafeGrinderVisualPlayModeTests
    {
        private FreeCafeRuntime app;
        private FreeCafeGrinderVisual visual;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private InputSettings.BackgroundBehavior previousBackground;

        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1; GameSaveParticipantRegistry.PendingRestore = null;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>("GrinderVisualRegressionMouse");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.zero });
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null; yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            app.areaManager.panSmoothTime = 0;
            Assert.That(app.World.EnterProduction().success, Is.True);
            visual = app.View("m.grinder").GetComponent<FreeCafeGrinderVisual>();
            Assert.That(visual, Is.Not.Null, "Use the animated parts on the actual CoffeeShop grinder prefab");
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (app != null) app.CancelDrag();
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            InputSystem.settings.backgroundBehavior = previousBackground;
            yield return null;
        }

        private void StartGrind()
        {
            Assert.That(app.World.TakeAndInteract("s.beans", "m.grinder", AreaId.Counter, 0, 0).success, Is.True);
            Assert.That(app.World.OperateProduction("m.grinder").success, Is.True);
        }

        private IEnumerator ReadyDrawer()
        {
            StartGrind();
            app.World.Tick(app.World.Find("m.grinder").production.remaining + .01f);
            yield return new WaitForSeconds(.4f);
            Assert.That(visual.DrawerOpenAmount, Is.EqualTo(1));
            Assert.That(visual.filledDrawer.enabled, Is.True);
        }

        private PointerEventData Pointer(Vector3 world) => new PointerEventData(EventSystem.current)
        { position = app.worldCamera.WorldToScreenPoint(world) };

        [UnityTest] public IEnumerator CrankTurnsAroundTheAxleWhileBodyAndGripStayUprightAndPauseFreezesMotion()
        {
            StartGrind(); yield return null;
            float first = visual.CrankAngle;
            yield return new WaitForSeconds(.15f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(first, visual.CrankAngle)), Is.GreaterThan(5));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(app.View("m.grinder").body.transform.localEulerAngles.z, 0)), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(visual.crankKnob.transform.localRotation, Quaternion.identity), Is.LessThan(.01f));
            Time.timeScale = 0;
            float paused = visual.CrankAngle;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(visual.CrankAngle, Is.EqualTo(paused).Within(.001f));
            Time.timeScale = 1;
            app.World.EmptyProductionMachine("m.grinder");
            yield return new WaitForSeconds(.55f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(visual.CrankAngle, 0)), Is.LessThan(.01f));
            Assert.That(visual.closedDrawer.enabled, Is.True);
            Assert.That(visual.filledDrawer.enabled, Is.False);
        }

        [UnityTest] public IEnumerator DrawerPickupCancelAndPlacementPreserveOneBatchAndTheSameVisiblePowderSprite()
        {
            yield return ReadyDrawer();
            var grinder = app.World.Find("m.grinder");
            var sprite = visual.filledDrawer.sprite;
            Assert.That(sprite, Is.SameAs(app.artLibrary.Resolve("grounds")));
            Assert.That(sprite, Is.SameAs(app.artLibrary.Resolve("equipment.grinder.drawer")));
            var pointer = Pointer(visual.DrawerWorldPosition);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer.position }.WithButton(MouseButton.Left));
            yield return null;
            app.BeginDrag(app.View(grinder.id), pointer);
            Assert.That(app.IsDraggingOutputFrom(grinder.id), Is.True);
            Assert.That(app.DraggedView.body.sprite, Is.SameAs(sprite));
            Assert.That(app.DraggedView.body.bounds.size.x, Is.EqualTo(visual.filledDrawer.bounds.size.x).Within(.05f), "Pickup must not shrink back into the tiny legacy icon");
            Assert.That(grinder.contents.Count, Is.EqualTo(1));
            yield return null;
            Assert.That(visual.filledDrawer.enabled, Is.False, "The reserved drawer must leave a visible empty slot, without duplicate powder");
            app.CancelDrag(); yield return null;
            Assert.That(grinder.production.phase, Is.EqualTo(ProductionPhase.Ready));
            Assert.That(grinder.contents.Count, Is.EqualTo(1));
            Assert.That(visual.filledDrawer.enabled, Is.True);
            yield return new WaitForSecondsRealtime(.2f);
            app.BeginDrag(app.View(grinder.id), pointer);
            Assert.That(app.IsDragging, Is.True);
            app.EndDrag(Pointer(app.AreaToWorld(AreaId.Counter, 0, -2)));
            yield return null;
            Assert.That(grinder.production.phase, Is.EqualTo(ProductionPhase.Empty));
            WorldEntity grounds = null;
            foreach (var entity in app.World.State.entities) if (entity.specId == "grounds" && string.IsNullOrEmpty(entity.ownerId))
            { Assert.That(grounds, Is.Null, "Only one batch may be committed"); grounds = entity; }
            Assert.That(grounds, Is.Not.Null);
            Assert.That(app.View(grounds.id).body.sprite, Is.SameAs(sprite), "The placed drawer must keep its coffee powder artwork");
            Assert.That(app.View(grounds.id).body.enabled, Is.True);
            Assert.That(app.View(grounds.id).body.color.a, Is.GreaterThan(.9f));
        }

        [UnityTest] public IEnumerator BeanBagPickupUsesMatchingLooseBeansAndCancelDoesNotConsumeStock()
        {
            var source = app.World.Find("s.beans"); int stock = source.stock;
            var pointer = Pointer(app.View(source.id).transform.position);
            app.BeginDrag(app.View(source.id), pointer);
            Assert.That(app.IsDragging, Is.True);
            Assert.That(app.DraggedView.body.sprite.name, Is.EqualTo("handful"));
            Assert.That(app.DraggedView.body.bounds.size.x, Is.GreaterThan(.8f));
            Assert.That(source.stock, Is.EqualTo(stock));
            app.CancelDrag(); yield return null;
            Assert.That(source.stock, Is.EqualTo(stock));
        }
    }
}
