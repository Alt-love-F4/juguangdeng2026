using System;
using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeInteractionPolishPlayModeTests
    {
        private FreeCafeRuntime app;
        private FreeCafeLatteArtBench bench;
        private Mouse mouse;
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
            mouse = InputSystem.AddDevice<Mouse>("FreeCafeInteractionPolishMouse");
            MouseAt(Vector2.zero, false);
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null; yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            Assert.That(app, Is.Not.Null);
            app.areaManager.panSmoothTime = 0;
            Assert.That(app.World.EnterProduction().success, Is.True);
            bench = Object.FindFirstObjectByType<FreeCafeLatteArtBench>();
            Assert.That(bench, Is.Not.Null);
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (app != null)
            {
                app.CancelDrag();
                app.GetComponent<FreeCafeMiniGameGateway>().Abort();
            }
            if (bench != null) yield return WaitForTransition();
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            InputSystem.settings.backgroundBehavior = previousInputBehavior;
            yield return null;
        }

        private PointerEventData Pointer(float x, float y) => new PointerEventData(EventSystem.current)
        { position = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter, x, y)) };

        private void MouseAt(Vector2 position, bool held)
        {
            var state = new MouseState { position = position };
            if (held) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state);
        }

        private IEnumerator WaitUntil(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        private IEnumerator WaitForTransition() => WaitUntil(() => !bench.IsTransitioning,
            "Latte transition must complete within three unscaled seconds");

        private WorldEntity TakeCup(string sourceId)
        {
            Assert.That(app.World.Take(sourceId, AreaId.Counter, 0, -2, out var cup).success, Is.True);
            Assert.That(app.View(cup.id), Is.Not.Null);
            return cup;
        }

        [UnityTest] public IEnumerator SmallEmptyCupReturnsStockExactlyOnceAndItsGhostDisappearsSmoothly()
        {
            yield return CheckMatchedReturn("s.cups.small");
        }

        [UnityTest] public IEnumerator LargeEmptyCupReturnsStockExactlyOnceAndItsGhostDisappearsSmoothly()
        {
            yield return CheckMatchedReturn("s.cups.large");
        }

        private IEnumerator CheckMatchedReturn(string sourceId)
        {
            var source = app.World.Find(sourceId);
            int stock = source.stock, count = app.World.State.entities.Count;
            var cup = TakeCup(sourceId);
            var view = app.View(cup.id);
            var stack = app.View(sourceId);
            Vector3 origin = view.transform.position;
            Assert.That(source.stock, Is.EqualTo(stock - 1));
            Assert.That(Vector3.Distance(origin, stack.transform.position), Is.GreaterThan(.5f));
            Physics2D.SyncTransforms();
            app.BeginDrag(view, Pointer(cup.x, cup.y));
            Assert.That(app.IsDragging, Is.True);
            var target = Pointer(source.x, source.y);
            app.MoveDrag(target);
            Assert.That(stack.IsDropTarget, Is.True);
            Assert.That(stack.body.color.g, Is.GreaterThan(stack.body.color.r), "A matching empty cup has a valid return highlight");
            app.EndDrag(target);
            Assert.That(app.IsDragging, Is.False);
            Assert.That(app.World.Find(cup.id), Is.Null);
            Assert.That(app.World.State.entities.Count, Is.EqualTo(count));
            Assert.That(source.stock, Is.EqualTo(stock));
            Assert.That(app.View(cup.id), Is.Null, "Removed identity must leave the runtime view map immediately");
            Assert.That(view, Is.Not.Null, "Keep the existing visible cup as a returning ghost");
            Assert.That(view.transform.position, Is.EqualTo(origin), "Return must not teleport or replace the visible cup");
            Assert.That(view.hitbox.enabled, Is.False, "Returning ghost must not accept another interaction");
            app.EndDrag(target);
            Assert.That(app.World.Interact(cup.id, sourceId).success, Is.False);
            app.DebugRefreshViews();
            Assert.That(source.stock, Is.EqualTo(stock), "Repeated release/interaction/refresh must not duplicate stock");
            yield return WaitUntil(() => view == null || Vector3.Distance(view.transform.position, origin) > .001f,
                "Returning ghost must visibly move toward its cup stack");
            Assert.That(view, Is.Not.Null, "Ghost must travel smoothly before destruction, not disappear on the next frame");
            Assert.That(Vector3.Distance(view.transform.position, stack.transform.position),
                Is.LessThan(Vector3.Distance(origin, stack.transform.position)));
            yield return WaitUntil(() => view == null, "Returning cup ghost must disappear within three unscaled seconds");
            Assert.That(source.stock, Is.EqualTo(stock));
        }

        [UnityTest] public IEnumerator MismatchedStackRejectsTheCupWithAnInvalidHighlightAndNoStockChange()
        {
            yield return CheckRejectedReturn("s.cups.large", false, false);
        }

        [UnityTest] public IEnumerator FilledCupRejectsReturnWithAnInvalidHighlightAndPreservesItsContents()
        {
            yield return CheckRejectedReturn("s.cups.small", true, false);
        }

        [UnityTest] public IEnumerator FullStackAcceptsAnAdditionalEmptyCup()
        {
            var source=app.World.Find("s.cups.small");
            app.World.Take(source.id,AreaId.Counter,0,-2,out var cup);
            source.stock=app.World.Spec(source.specId).initialStock;
            Assert.That(app.World.Interact(cup.id,source.id).success,Is.True);
            Assert.That(source.stock,Is.EqualTo(app.World.Spec(source.specId).initialStock+1));
            yield return null;
        }

        private IEnumerator CheckRejectedReturn(string targetId, bool filled, bool fullStack)
        {
            var cup = TakeCup("s.cups.small");
            var portion = new Portion { specId = "milk", units = 1, milliliters = 20, productionScore = 100 };
            if (filled) cup.contents.Add(portion);
            var target = app.World.Find(targetId);
            if (fullStack) target.stock = app.World.Spec(target.specId).initialStock;
            int smallStock = app.World.Find("s.cups.small").stock;
            int largeStock = app.World.Find("s.cups.large").stock;
            int count = app.World.State.entities.Count;
            var view = app.View(cup.id); var stack = app.View(targetId);
            Vector3 origin = view.transform.position;
            Assert.That(app.World.Preview(cup, target).success, Is.False);
            Physics2D.SyncTransforms();
            app.BeginDrag(view, Pointer(cup.x, cup.y));
            Assert.That(app.IsDragging, Is.True);
            app.MoveDrag(Pointer(target.x, target.y));
            Assert.That(stack.IsDropTarget, Is.True);
            Assert.That(stack.body.color.r, Is.EqualTo(1f));
            Assert.That(stack.body.color.g, Is.LessThan(.5f), "Rejected cup return must highlight the stack in red");
            app.EndDrag(Pointer(target.x, target.y));
            Assert.That(app.IsDragging, Is.False);
            Assert.That(stack.body.color.g, Is.LessThan(.5f), "Invalid highlight persists briefly after rejected release");
            Assert.That(app.World.Find(cup.id), Is.SameAs(cup));
            Assert.That(app.View(cup.id), Is.SameAs(view));
            Assert.That(cup.x, Is.EqualTo(0)); Assert.That(cup.y, Is.EqualTo(-2));
            Assert.That(app.World.State.entities.Count, Is.EqualTo(count));
            Assert.That(app.World.Find("s.cups.small").stock, Is.EqualTo(smallStock));
            Assert.That(app.World.Find("s.cups.large").stock, Is.EqualTo(largeStock));
            Assert.That(cup.contents.Count, Is.EqualTo(filled ? 1 : 0));
            if (filled)
            {
                Assert.That(cup.contents[0], Is.SameAs(portion));
                Assert.That(app.World.LiquidVolume(cup), Is.EqualTo(20));
            }
            yield return WaitUntil(() => view != null && Vector3.Distance(view.transform.position, origin) < .001f,
                "Rejected return must retain the cup and settle back at its original position");
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(view, Is.Not.Null);
            Assert.That(stack.IsDropTarget, Is.False);
            Assert.That(stack.body.color.g, Is.GreaterThan(.5f), "Rejection highlight must clear after its feedback interval");
        }

        [UnityTest] public IEnumerator LatteOverlayHasSixPreviewsResetClearsDrawingAndSubmitRequiresAStroke()
        {
            Assert.That(app.World.SelectServingCup("s.cups.small").success, Is.True);
            var cup = app.World.ServingCup;
            Assert.That(app.World.RequestProductionMiniGame().success, Is.True);
            yield return WaitForTransition();
            var overlay = GameObject.Find("LatteArtPresetOverlay");
            Assert.That(overlay, Is.Not.Null);
            var panel = overlay.transform.Find("Content/PresetPanel");
            var list = panel.Find("PresetList");
            Assert.That(list.childCount, Is.EqualTo(6));
            foreach (Transform preset in list)
            {
                Assert.That(preset.GetComponent<Button>(), Is.Not.Null);
                var preview = preset.Find("Preview").GetComponent<RawImage>();
                Assert.That(preview.texture, Is.Not.Null);
                Assert.That(preview.texture.width, Is.GreaterThan(1));
                Assert.That(preview.texture.height, Is.GreaterThan(1));
                Assert.That(preview.raycastTarget, Is.False);
                Assert.That(preset.Find("Fit").GetComponent<TMP_Text>().text, Does.StartWith("拟合 "));
                Assert.That(preset.Find("Best").gameObject.activeSelf, Is.False);
            }
            var surface = FindSurface();
            var effect = (ILatteArtSurface)surface;
            var submit = panel.parent.Find("Submit").GetComponent<Button>();
            string token = app.World.State.production.miniGameToken;
            Assert.That(effect.StrokePointCount, Is.Zero);
            Assert.That(submit.interactable, Is.False);
            submit.onClick.Invoke();
            Assert.That(app.World.State.production.miniGameToken, Is.EqualTo(token), "Programmatic submit must also reject an empty drawing");
            list.GetChild(0).GetComponent<Button>().onClick.Invoke();
            Assert.That(effect.StrokePointCount, Is.Zero, "Selecting a preview must not draw or stamp a template");
            yield return DrawStroke(surface);
            yield return WaitUntil(() => submit.interactable, "Submit must become available after a real pointer stroke");
            panel.parent.Find("Reset").GetComponent<Button>().onClick.Invoke();
            Assert.That(effect.StrokePointCount, Is.Zero);
            Assert.That(submit.interactable, Is.False);
            Assert.That(bench.LastPatternId, Is.Null);
            Assert.That(app.World.State.production.miniGameToken, Is.EqualTo(token));
            Assert.That(app.World.ServingCup, Is.SameAs(cup));
            yield return DrawStroke(surface);
            yield return WaitUntil(() => submit.interactable, "Drawing again after Reset must re-enable Submit");
            submit.onClick.Invoke();
            Assert.That(app.World.State.production.miniGameToken, Is.Null.Or.Empty);
            Assert.That(bench.IsTransitioning, Is.True);
            yield return WaitForTransition();
            Assert.That(app.World.ServingCup, Is.SameAs(cup));
            Assert.That(cup.latteArtPattern, Is.Not.Null.And.Not.Empty);
            Assert.That(cup.latteArtScore, Is.InRange(0f, 100f));
            Assert.That(cup.latteArt, Is.True, "Completing a drawing counts as latte art regardless of recognition score or pattern");
            Assert.That(app.World.LiquidVolume(cup), Is.Zero, "Practice drawing must not add milk to authoritative cup contents");
        }

        [UnityTest] public IEnumerator LatteEntryAndExitBlockUnderlyingGameplayAndRestoreTheLayoutUsingUnscaledTime()
        {
            Assert.That(app.World.SelectServingCup("s.cups.small").success, Is.True);
            var cup = app.World.ServingCup;
            var source = app.World.Find("s.cups.large"); int stock = source.stock;
            var layout = bench.transform.parent;
            Vector3 home = layout.localPosition, scale = layout.localScale;
            Vector3 cameraPosition = app.worldCamera.transform.position;
            float cameraSize = app.worldCamera.orthographicSize;
            var hud = app.GetComponent<FreeCafeProductionHud>();
            Assert.That(app.World.RequestProductionMiniGame().success, Is.True);
            Assert.That(bench.IsTransitioning, Is.True);
            AssertUnderlyingGameplayBlocked(source, cup, stock);
            Time.timeScale = 0;
            yield return WaitForTransition();
            Time.timeScale = 1;
            yield return WaitUntil(() => !hud.productionRoot.activeSelf, "Latte presentation must hide production HUD");
            var overlay = GameObject.Find("LatteArtPresetOverlay");
            Assert.That(overlay, Is.Not.Null);
            var content = overlay.transform.Find("Content").GetComponent<CanvasGroup>();
            Assert.That(content.interactable, Is.True);
            Assert.That(content.blocksRaycasts, Is.True);
            overlay.transform.Find("Content/PresetPanel/Cancel").GetComponent<Button>().onClick.Invoke();
            Assert.That(app.World.State.production.miniGameToken, Is.Null.Or.Empty);
            Assert.That(bench.IsTransitioning, Is.True);
            Assert.That(content.interactable, Is.False);
            Assert.That(content.blocksRaycasts, Is.False);
            AssertUnderlyingGameplayBlocked(source, cup, stock);
            Time.timeScale = 0;
            yield return WaitForTransition();
            Time.timeScale = 1;
            yield return WaitUntil(() => hud.productionRoot.activeSelf, "Production HUD must return after latte exit");
            Assert.That(app.InputBlocked, Is.False);
            Assert.That(layout.localPosition, Is.EqualTo(home));
            Assert.That(layout.localScale, Is.EqualTo(scale));
            Assert.That(app.worldCamera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(app.worldCamera.orthographicSize, Is.EqualTo(cameraSize));
            Assert.That(GameObject.Find("LatteArtPresetOverlay"), Is.Null);
            Assert.That(app.areaRoots[(int)AreaId.Counter].GetComponent<FreeCafeSubSceneDock>().IsDocked, Is.True);
            app.Click(app.View(source.id));
            Assert.That(app.World.ServingCup.specId, Is.EqualTo("cup.large"), "Normal cup selection must work again after exit");
            Assert.That(source.stock, Is.EqualTo(stock - 1));
        }

        private void AssertUnderlyingGameplayBlocked(WorldEntity source, WorldEntity cup, int stock)
        {
            Assert.That(app.InputBlocked, Is.True);
            app.Click(app.View(source.id));
            app.BeginDrag(app.View(cup.id), Pointer(cup.x, cup.y));
            app.Enter(AreaId.TopFloor);
            Assert.That(app.IsDragging, Is.False);
            Assert.That(app.World.State.currentArea, Is.EqualTo(AreaId.Counter));
            Assert.That(app.World.ServingCup, Is.SameAs(cup));
            Assert.That(source.stock, Is.EqualTo(stock));
        }

        private MonoBehaviour FindSurface()
        {
            foreach (var component in bench.transform.parent.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is ILatteArtSurface) return component;
            Assert.Fail("Latte bench must contain the existing Plane surface");
            return null;
        }

        private IEnumerator DrawStroke(MonoBehaviour surface)
        {
            var map = surface.GetType().GetMethod("TryGetSurfaceUV");
            Assert.That(map, Is.Not.Null);
            // Keep samples inside the rendered bowl, away from the left-side preset panel.
            for (int i = 0; i < 8; i++)
            {
                Vector2 screen = app.worldCamera.WorldToScreenPoint(surface.transform.TransformPoint(new Vector3(-1.05f + i * .3f, 0, .3f)));
                object[] args = { screen, Vector2.zero };
                Assert.That((bool)map.Invoke(surface, args), Is.True, "Stroke samples must map to the actual Plane UVs");
                MouseAt(screen, true);
                yield return null; yield return null;
            }
            MouseAt(mouse.position.ReadValue(), false);
            yield return null; yield return null;
            Assert.That(((ILatteArtSurface)surface).StrokePointCount, Is.GreaterThanOrEqualTo(5));
        }
    }
}
