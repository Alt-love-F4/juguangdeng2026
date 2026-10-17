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
    public sealed class FreeCafeIngredientReturnPlayModeTests
    {
        private FreeCafeRuntime app;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;

        [UnitySetUp] public IEnumerator Setup()
        {
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None)) panel.Cancel();
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>("FreeCafeIngredientReturnMouse");
            keyboard = InputSystem.AddDevice<Keyboard>("FreeCafeIngredientReturnKeyboard");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null; yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            Assert.That(app, Is.Not.Null);
            Assert.That(app.enabled, Is.True, "Scene adapter must initialize rather than fail as a duplicate");
            Assert.That(app.World, Is.Not.Null);
            Assert.That(PlayerCafeOwner.Current, Is.SameAs(app.World));
            Assert.That(GameSaveParticipantRegistry.Capturers.ContainsKey(FreeCafeRuntime.SaveId), Is.True);
            Assert.That(GameSaveParticipantRegistry.Restorers.ContainsKey(FreeCafeRuntime.SaveId), Is.True);
            Assert.That(GameSaveParticipantRegistry.Capturers[FreeCafeRuntime.SaveId].Target, Is.SameAs(app));
            Assert.That(GameSaveParticipantRegistry.Restorers[FreeCafeRuntime.SaveId].Target, Is.SameAs(app));
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
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            Time.timeScale = 1;
            yield return null;
        }

        private PointerEventData Pointer(float x, float y) => new PointerEventData(EventSystem.current)
        { position = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter, x, y)) };

        private IEnumerator WaitUntil(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        private WorldEntity Take(string sourceId)
        {
            Assert.That(app.World.Take(sourceId, AreaId.Counter, 0, -2, out var item).success, Is.True);
            Assert.That(app.View(item.id), Is.Not.Null);
            return item;
        }

        private void Begin(WorldEntity entity)
        {
            Physics2D.SyncTransforms();
            app.BeginDrag(app.View(entity.id), Pointer(entity.x, entity.y));
            Assert.That(app.IsDragging, Is.True);
        }

        private void AssertHighlight(WorldEntity source, bool valid)
        {
            var view = app.View(source.id);
            Assert.That(view.IsDropTarget, Is.True, source.id + " must be detected as a receiver");
            if (valid) Assert.That(view.body.color.g, Is.GreaterThan(view.body.color.r));
            else
            {
                Assert.That(view.body.color.r, Is.EqualTo(1f));
                Assert.That(view.body.color.g, Is.LessThan(.5f));
            }
        }

        [UnityTest] public IEnumerator PlacedBeansSugarAndCocoaReturnTheirUnitsExactlyOnce()
        {
            foreach (string sourceId in new[] { "s.beans", "s.sugar", "s.cocoa" })
            {
                var source = app.World.Find(sourceId);
                int originalCount = app.World.State.entities.Count;
                var item = Take(sourceId);
                // Model an unused multi-unit portion as well as ordinary single pickups.
                if (sourceId == "s.cocoa") item.units = 3;
                int stockBeforeReturn = source.stock, units = item.units;
                Assert.That(item.quality, Is.EqualTo(source.quality));
                var preview = app.World.Preview(item, source);
                Assert.That(preview.success, Is.True, preview.message);
                Assert.That(source.stock, Is.EqualTo(stockBeforeReturn), "Preview is read-only");
                var view = app.View(item.id);
                Begin(item);
                var target = Pointer(source.x, source.y);
                app.MoveDrag(target);
                AssertHighlight(source, true);
                Assert.That(app.Hint, Does.Contain(preview.message));
                app.EndDrag(target);
                Assert.That(app.IsDragging, Is.False);
                Assert.That(app.World.Find(item.id), Is.Null);
                Assert.That(app.View(item.id), Is.Null);
                Assert.That(source.stock, Is.EqualTo(stockBeforeReturn + units));
                Assert.That(app.World.State.entities.Count, Is.EqualTo(originalCount));
                app.EndDrag(target);
                Assert.That(app.World.Interact(item.id, source.id).success, Is.False);
                app.DebugRefreshViews();
                yield return WaitUntil(() => view == null, "Returned ingredient ghost must be retired");
                Assert.That(source.stock, Is.EqualTo(stockBeforeReturn + units), "Repeated release cannot credit stock twice");
                Assert.That(app.World.State.entities.Count, Is.EqualTo(originalCount));
            }
        }

        [UnityTest] public IEnumerator SourcePreviewReleasedOnItsOwnStackIsSuccessfulWithoutStockOrViewLeaks()
        {
            foreach (string sourceId in new[] { "s.beans", "s.sugar", "s.cocoa" })
            {
                var source = app.World.Find(sourceId);
                int stock = source.stock, count = app.World.State.entities.Count;
                int viewCount = app.areaRoots[(int)AreaId.Counter].GetComponentsInChildren<FreeEntityView>(true).Length;
                var sourceView = app.View(source.id);
                Begin(source);
                var previewObject = GameObject.Find("UncommittedPickup");
                Assert.That(previewObject, Is.Not.Null);
                var preview = previewObject.GetComponent<FreeEntityView>();
                app.MoveDrag(Pointer(0, -2));
                var target = Pointer(source.x, source.y);
                app.MoveDrag(target);
                AssertHighlight(source, true);
                Assert.That(source.stock, Is.EqualTo(stock));
                app.EndDrag(target);
                Assert.That(app.IsDragging, Is.False);
                Assert.That(sourceView.IsDropTarget, Is.False, "Successful no-op must not leave rejection feedback");
                app.EndDrag(target);
                app.DebugRefreshViews();
                Assert.That(source.stock, Is.EqualTo(stock));
                Assert.That(app.World.State.entities.Count, Is.EqualTo(count));
                Assert.That(app.View(source.id), Is.SameAs(sourceView));
                yield return WaitUntil(() => preview == null, "Uncommitted pickup must not leak after returning to its source");
                Assert.That(app.areaRoots[(int)AreaId.Counter].GetComponentsInChildren<FreeEntityView>(true).Length, Is.EqualTo(viewCount));
            }
        }

        [UnityTest] public IEnumerator WrongStackAndQualityRejectWithConsistentHintsAndCancelPreservesIngredients()
        {
            var source = app.World.Find("s.beans");
            var wrong = app.World.Find("s.cocoa");
            var item = Take(source.id);
            int stock = source.stock, wrongStock = wrong.stock, count = app.World.State.entities.Count;
            var view = app.View(item.id);
            Vector3 home = view.transform.position;
            foreach (bool wrongQuality in new[] { false, true })
            {
                var targetSource = wrongQuality ? source : wrong;
                if (wrongQuality) item.quality = (source.quality + 1) % 3;
                var result = app.World.Preview(item, targetSource);
                Assert.That(result.success, Is.False);
                Assert.That(result.message, Is.Not.Null.And.Not.Empty);
                Begin(item);
                var target = Pointer(targetSource.x, targetSource.y);
                app.MoveDrag(target);
                AssertHighlight(targetSource, false);
                Assert.That(app.Hint, Does.Contain(result.message));
                app.EndDrag(target);
                Assert.That(app.Hint, Is.EqualTo(result.message), "Release and preview must explain the same rejection");
                Assert.That(app.IsDragging, Is.False);
                Assert.That(app.World.Find(item.id), Is.SameAs(item));
                Assert.That(app.View(item.id), Is.SameAs(view));
                Assert.That(item.x, Is.EqualTo(0)); Assert.That(item.y, Is.EqualTo(-2));
                Assert.That(source.stock, Is.EqualTo(stock)); Assert.That(wrong.stock, Is.EqualTo(wrongStock));
                yield return WaitUntil(() => view != null && Vector3.Distance(view.transform.position, home) < .001f,
                    "Rejected ingredient must settle back at its original position");
            }
            item.quality = source.quality;
            Begin(item);
            app.MoveDrag(Pointer(source.x, source.y));
            app.CancelDrag();
            app.EndDrag(Pointer(source.x, source.y));
            Assert.That(app.World.Find(item.id), Is.SameAs(item));
            Assert.That(source.stock, Is.EqualTo(stock));
            yield return WaitUntil(() => Vector3.Distance(view.transform.position, home) < .001f, "Cancellation must return the placed ingredient home");
            Begin(source);
            var preview = GameObject.Find("UncommittedPickup");
            Assert.That(preview, Is.Not.Null);
            app.MoveDrag(Pointer(wrong.x, wrong.y));
            app.CancelDrag();
            app.EndDrag(Pointer(wrong.x, wrong.y));
            yield return WaitUntil(() => preview == null, "Cancelled source preview must be destroyed");
            Assert.That(app.IsDragging, Is.False);
            Assert.That(source.stock, Is.EqualTo(stock)); Assert.That(wrong.stock, Is.EqualTo(wrongStock));
            Assert.That(app.World.State.entities.Count, Is.EqualTo(count));
        }

        [UnityTest] public IEnumerator ShiftOverMatchingSourceOnlyPlacesAndDoesNotReturnStock()
        {
            var source = app.World.Find("s.beans");
            var item = Take(source.id);
            int stock = source.stock, count = app.World.State.entities.Count;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
            yield return null; yield return null;
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(keyboard.leftShiftKey.isPressed, Is.True);
            var view = app.View(item.id);
            Begin(item);
            var target = Pointer(source.x, source.y);
            app.MoveDrag(target);
            Assert.That(app.View(source.id).IsDropTarget, Is.False);
            Assert.That(app.Hint, Does.Contain("仅摆放"));
            app.EndDrag(target);
            app.EndDrag(target);
            Assert.That(app.IsDragging, Is.False);
            Assert.That(app.World.Find(item.id), Is.SameAs(item));
            Assert.That(app.View(item.id), Is.SameAs(view));
            Assert.That(item.x, Is.EqualTo(source.x).Within(.001f));
            Assert.That(item.y, Is.EqualTo(source.y).Within(.001f));
            Assert.That(source.stock, Is.EqualTo(stock));
            Assert.That(app.World.State.entities.Count, Is.EqualTo(count));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
    }
}
