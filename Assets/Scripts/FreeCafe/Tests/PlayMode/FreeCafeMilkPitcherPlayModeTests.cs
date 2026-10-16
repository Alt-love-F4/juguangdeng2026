using System.Collections;
using System.Collections.Generic;
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

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeMilkPitcherPlayModeTests
    {
        private const string PitcherId = "tool.milk-pitcher";
        private const float VolumeTolerance = .01f;
        private FreeCafeRuntime app;
        private FreeCafeLatteArtBench bench;
        private Mouse mouse;
        private Keyboard keyboard;
        private float previousTimeScale;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private List<GameSaveParticipantData> previousPendingRestore;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            previousTimeScale = Time.timeScale;
            previousPendingRestore = GameSaveParticipantRegistry.PendingRestore;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>("MilkPitcherRegressionMouse");
            keyboard = InputSystem.AddDevice<Keyboard>("MilkPitcherRegressionKeyboard");
            MouseAt(Vector2.zero, false);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None)) panel.Cancel();
            Time.timeScale = 1f;
            GameSaveParticipantRegistry.PendingRestore = null;
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null;
            yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            Assert.That(app, Is.Not.Null);
            app.areaManager.panSmoothTime = 0f;
            Assert.That(app.World.EnterProduction().success, Is.True);
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            bench = Object.FindFirstObjectByType<FreeCafeLatteArtBench>();
            Assert.That(bench, Is.Not.Null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (app != null)
            {
                app.CancelDrag();
                var gateway = app.GetComponent<FreeCafeMiniGameGateway>();
                if (gateway != null) gateway.Abort();
            }
            // Never remove, reset, or queue events to a pre-existing user input device.
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            mouse = null;
            keyboard = null;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            GameSaveParticipantRegistry.PendingRestore = previousPendingRestore;
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        private WorldEntity Pitcher()
        {
            var pitcher = app.World.Find(PitcherId);
            Assert.That(pitcher, Is.Not.Null);
            Assert.That(pitcher.specId, Is.EqualTo("container.milk-pitcher"));
            Assert.That(app.View(PitcherId), Is.Not.Null);
            return pitcher;
        }

        private WorldEntity TakeCup()
        {
            Assert.That(app.World.Take("s.cups.large", AreaId.Counter, 0f, -2f, out var cup).success, Is.True);
            return cup;
        }

        private void FillAndFroth(WorldEntity pitcher)
        {
            Assert.That(app.World.TakeAndInteract("s.milk", pitcher.id, AreaId.Counter, 0f, 0f).success, Is.True);
            app.Click(app.View(pitcher.id));
            Assert.That(app.World.MilkFoamVolume(pitcher), Is.GreaterThan(0f), "Clicking the actual pitcher must froth its milk");
            Assert.That(app.World.CanPour(pitcher), Is.False);
        }

        private PointerEventData Pointer(FreeEntityView view) => new PointerEventData(EventSystem.current)
        {
            position = app.worldCamera.WorldToScreenPoint(view.transform.position),
            button = PointerEventData.InputButton.Left,
            pointerId = 41
        };

        private string DropPitcherOnCup(WorldEntity pitcher, WorldEntity cup)
        {
            Physics2D.SyncTransforms();
            app.BeginDrag(app.View(pitcher.id), Pointer(app.View(pitcher.id)));
            Assert.That(app.IsDragging, Is.True);
            app.EndDrag(Pointer(app.View(cup.id)));
            Assert.That(app.IsDragging, Is.False);
            var session = app.World.State.production;
            Assert.That(session.miniGameToken, Is.Not.Null.And.Not.Empty);
            Assert.That(session.miniGamePitcherId, Is.EqualTo(pitcher.id));
            Assert.That(session.miniGameCupId, Is.EqualTo(cup.id));
            Assert.That(bench.IsTransitioning, Is.True);
            return session.miniGameToken;
        }

        private IEnumerator WaitForTransition()
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (bench != null && bench.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(bench, Is.Not.Null);
            Assert.That(bench.IsTransitioning, Is.False, "Entry/exit must finish within three unscaled seconds");
            yield return null;
        }

        private Transform Overlay()
        {
            var overlay = GameObject.Find("LatteArtPresetOverlay");
            Assert.That(overlay, Is.Not.Null);
            return overlay.transform;
        }

        private MonoBehaviour FoamSurface()
        {
            foreach (var component in bench.transform.parent.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(component is ILatteArtSurface)) continue;
                Assert.That(component is ILatteArtFoamSurface, Is.True,
                    "The actual Plane must implement the foam-consumer interface, not a test-only substitute");
                Assert.That(component.isActiveAndEnabled, Is.True);
                Assert.That(component.GetComponent<MeshFilter>(), Is.Not.Null);
                return component;
            }
            Assert.Fail("The authored latte bench must contain its existing GPU Plane surface");
            return null;
        }

        private void MouseAt(Vector2 screen, bool held, bool rightHeld = false)
        {
            var state = new MouseState { position = screen };
            if (held) state = state.WithButton(MouseButton.Left);
            if (rightHeld) state = state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse, state);
        }

        private IEnumerator DrawActualStroke(MonoBehaviour surface)
        {
            var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh, Is.Not.Null);
            for (int sample = 0; sample < 8; sample++)
            {
                // Project real mesh-local points; no private input, solver, or foam callback is invoked.
                float t = sample / 7f;
                Vector3 local = mesh.bounds.center + new Vector3(
                    Mathf.Lerp(-.12f, .12f, t) * mesh.bounds.size.x,
                    0f, Mathf.Sin(t * Mathf.PI) * .04f * mesh.bounds.size.z);
                Vector2 screen = app.worldCamera.WorldToScreenPoint(surface.transform.TransformPoint(local));
                MouseAt(screen, true);
                yield return null;
            }
            MouseAt(app.worldCamera.WorldToScreenPoint(surface.transform.TransformPoint(mesh.bounds.center)), false);
            yield return null;
            yield return null;
            Assert.That(Mouse.current, Is.SameAs(mouse));
            Assert.That(((ILatteArtSurface)surface).StrokePointCount, Is.GreaterThanOrEqualTo(5));
        }

        private IEnumerator ClickUI(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.interactable, Is.True);
            Canvas.ForceUpdateCanvases();
            var rect = button.GetComponent<RectTransform>();
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            MouseAt(screen, false);
            yield return null;
            yield return null;
            MouseAt(screen, true);
            yield return null;
            yield return null;
            MouseAt(screen, false);
            yield return null;
            yield return null;
        }

        private void AssertFoamBalance(WorldEntity pitcher, WorldEntity cup, float initialSource, float initialCupFoam,
            float initialCupLiquid, float used)
        {
            Assert.That(app.World.Find(pitcher.id), Is.SameAs(pitcher));
            Assert.That(app.World.Find(cup.id), Is.SameAs(cup));
            Assert.That(initialSource - app.World.MilkFoamVolume(pitcher), Is.EqualTo(used).Within(VolumeTolerance));
            Assert.That(app.World.MilkFoamVolume(cup) - initialCupFoam, Is.EqualTo(used).Within(VolumeTolerance));
            Assert.That(app.World.LiquidVolume(cup) - initialCupLiquid, Is.EqualTo(used).Within(VolumeTolerance));
        }

        [UnityTest]
        public IEnumerator AuthoredPitcherUsesReducedSceneScaleWithoutDoubleScalingItsBodyAndLiquid()
        {
            var pitcher = Pitcher();
            var view = app.View(pitcher.id);
            Assert.That(view.gameObject.scene.path, Is.EqualTo("Assets/Scenes/CoffeeShop.unity"));
            Assert.That(view.AuthoringEntityId, Is.EqualTo(PitcherId));
            int authoredMatches = 0;
            foreach (var authored in FreeCafeSceneLayout.FindAuthoredEntities(app))
                if (authored.AuthoringEntityId == PitcherId) { authoredMatches++; Assert.That(authored, Is.SameAs(view)); }
            Assert.That(authoredMatches, Is.EqualTo(1));
            var prefab = app.prefabLibrary.Require("container.milk-pitcher");
            Assert.That(prefab.transform.localScale.x, Is.EqualTo(1f).Within(.0001f), "Do not shrink the reusable prefab");
            Assert.That(prefab.transform.localScale.y, Is.EqualTo(1f).Within(.0001f));
            // Runtime adopts authored entities under the area root with world scale preserved.
            // Compare against the source scene grouping, not the old local transform space.
            var authoredGroup=app.authoringPreviews[(int)pitcher.area].transform;
            Assert.That(view.transform.lossyScale.x/authoredGroup.lossyScale.x, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(view.transform.lossyScale.y/authoredGroup.lossyScale.y, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(view.transform.lossyScale.z/authoredGroup.lossyScale.z, Is.EqualTo(1f).Within(.0001f));
            Assert.That(view.body.transform.parent, Is.SameAs(view.transform));
            Assert.That(view.liquidRoot.parent, Is.SameAs(view.transform));
            Assert.That(view.liquidRoot.localScale, Is.EqualTo(prefab.liquidRoot.localScale));
            Assert.That(view.body.transform.localScale, Is.EqualTo(prefab.body.transform.localScale));
            foreach (var anchor in new[] { view.body.transform, view.liquidRoot })
            {
                Assert.That(anchor.lossyScale.x / anchor.localScale.x,
                    Is.EqualTo(view.transform.lossyScale.x).Within(.0001f));
                Assert.That(anchor.lossyScale.y / anchor.localScale.y,
                    Is.EqualTo(view.transform.lossyScale.y).Within(.0001f));
            }
            Assert.That(Vector3.Distance(view.body.transform.position, view.liquidRoot.position), Is.LessThan(.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ForcedVisualOrdinaryMilkUsesTheExistingGlassPresenterWithoutEnablingPlayerPouring()
        {
            var pitcher = Pitcher();
            Assert.That(app.World.TakeAndInteract("s.milk", pitcher.id, AreaId.Counter, 0f, 0f).success, Is.True);
            Assert.That(app.World.CanPour(pitcher), Is.False);
            var view = app.View(pitcher.id);
            view.Refresh(pitcher);
            var presenter = view.liquidPresenter as OrthographicGlassCupPresenter;
            Assert.That(presenter, Is.Not.Null, "EnsureGlassCup must include container.milk-pitcher");
            Assert.That(presenter.gameObject.activeSelf, Is.False);
            var snapshot = app.World.LiquidSnapshot(pitcher);
            Assert.That(snapshot.composition.milkMl, Is.EqualTo(snapshot.volumeMl).Within(VolumeTolerance));
            float initialVolume = snapshot.volumeMl;
            var cup = TakeCup();
            Assert.That(app.World.PourLiquid(pitcher.id, cup.id, 20f, 1f).success, Is.False);
            Assert.That(app.World.LiquidVolume(pitcher), Is.EqualTo(initialVolume));
            Assert.That(app.World.LiquidVolume(cup), Is.Zero);
            view.PresentPour(.5f, true);
            Assert.That(presenter.isActiveAndEnabled, Is.True);
            Assert.That(presenter.transform.IsChildOf(view.liquidRoot), Is.True);
            Assert.That(presenter.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(.82f, 1.02f)));
            Assert.That(view.liquidRoot.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.body.enabled, Is.True);
            Assert.That(view.body.color.a, Is.EqualTo(.22f).Within(.001f));
            Assert.That(presenter.TargetFill, Is.EqualTo(initialVolume / snapshot.capacityMl).Within(.0001f));
            Assert.That(presenter.MixedColor.r, Is.EqualTo(.96f).Within(.001f));
            Assert.That(presenter.MixedColor.g, Is.EqualTo(.92f).Within(.001f));
            Assert.That(presenter.MixedColor.b, Is.EqualTo(.82f).Within(.001f));
            Assert.That(Quaternion.Angle(view.body.transform.rotation, view.liquidRoot.rotation), Is.LessThan(.001f));
            Assert.That(presenter.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(presenter.GetComponent<GravityLiquidImage>().MotionSpace, Is.SameAs(view.transform.parent));
            view.Refresh(pitcher);
            Assert.That(presenter.gameObject.activeSelf, Is.True, "Refresh must not hide an active pour");
            view.PresentPour(0f, false);
            view.Refresh(pitcher);
            Assert.That(presenter.gameObject.activeSelf, Is.False);
            Assert.That(view.body.color.a, Is.EqualTo(1f));
            Assert.That(view.GetComponentsInChildren<OrthographicGlassCupPresenter>(true).Length, Is.EqualTo(1));
            Assert.That(app.World.LiquidVolume(pitcher), Is.EqualTo(initialVolume));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ForcedVisualWaterAndFoamRemainVisibleWithoutAllowingDomainPouring()
        {
            var pitcher = Pitcher();
            var cup = TakeCup();
            var view = app.View(pitcher.id);
            foreach (var specId in new[] { "water", "milkfoam" })
            {
                pitcher.contents.Clear();
                pitcher.contents.Add(new Portion { specId = specId, units = 1, milliliters = 100f });
                view.Refresh(pitcher);
                var presenter = view.liquidPresenter as OrthographicGlassCupPresenter;
                Assert.That(presenter, Is.Not.Null);
                Assert.That(presenter.gameObject.activeSelf, Is.False);
                Assert.That(app.World.CanPour(pitcher), Is.False);
                Assert.That(app.World.PourLiquid(pitcher.id, cup.id, 20f, 1f).success, Is.False);
                view.PresentPour(.5f, true);
                Assert.That(presenter.isActiveAndEnabled, Is.True, specId + " must remain available to forced visuals");
                Assert.That(view.liquidRoot.gameObject.activeInHierarchy, Is.True);
                Assert.That(presenter.TargetFill,
                    Is.EqualTo(100f / app.World.LiquidCapacity(pitcher)).Within(.0001f));
                view.Refresh(pitcher);
                Assert.That(presenter.gameObject.activeSelf, Is.True);
                Assert.That(app.World.LiquidVolume(pitcher), Is.EqualTo(100f));
                Assert.That(app.World.LiquidVolume(cup), Is.Zero);
                Assert.That(app.World.State.production.miniGameToken, Is.Null);
                view.PresentPour(0f, false);
                view.Refresh(pitcher);
                Assert.That(presenter.gameObject.activeSelf, Is.False);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator FrothedPitcherNeverTiltsOrConsumesAndLeftReleaseEndsDragWhileRightIsHeld()
        {
            var pitcher = Pitcher();
            Assert.That(app.World.TakeAndInteract("s.milk", pitcher.id, AreaId.Counter, 0f, 0f).success, Is.True);
            Assert.That(app.World.CanPour(pitcher), Is.False);
            var view = app.View(pitcher.id);
            view.Refresh(pitcher);
            var presenter = view.liquidPresenter as OrthographicGlassCupPresenter;
            Assert.That(presenter, Is.Not.Null);
            float initialVolume = app.World.LiquidVolume(pitcher);
            float initialSpilled = pitcher.production.spilledMl;
            Assert.That(app.World.FrothMilk(pitcher.id).success,Is.True);
            var pointer = Pointer(view);
            MouseAt(pointer.position, true, true);
            yield return null;
            yield return null;
            Assert.That(Mouse.current, Is.SameAs(mouse));
            Assert.That(mouse.rightButton.isPressed, Is.True);
            Physics2D.SyncTransforms();
            app.BeginDrag(view, pointer);
            Assert.That(app.IsDragging, Is.True, "BeginDrag must work even when right is already held");
            Assert.That(app.LockedPourTargetId, Is.Null);

            // Exercise right already held, then a fresh right-down during the same left drag.
            for (int phase = 0; phase < 2; phase++)
            {
                if (phase > 0)
                {
                    MouseAt(pointer.position, true, false);
                    yield return null;
                    MouseAt(pointer.position, true, true);
                    yield return null;
                }
                for (int sample = 1; sample <= 3; sample++)
                {
                    var direction = phase == 0 ? Vector2.down : Vector2.up;
                    MouseAt(pointer.position + direction * (sample * 45f), true, true);
                    yield return null;
                    yield return null;
                    Assert.That(Mouse.current, Is.SameAs(mouse));
                    Assert.That(app.IsDragging, Is.True);
                    Assert.That(app.LockedPourTargetId, Is.Null);
                    Assert.That(presenter.gameObject.activeSelf, Is.False, "Right mouse must never activate a pitcher pour visual");
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(pitcher.rotation, view.body.transform.localEulerAngles.z)),
                        Is.LessThanOrEqualTo(6.01f), "Only the existing small drag lean is allowed, never pour tilt");
                    Assert.That(view.body.color.a, Is.EqualTo(1f));
                    Assert.That(app.World.LiquidVolume(pitcher), Is.EqualTo(initialVolume));
                    Assert.That(pitcher.production.spilledMl, Is.EqualTo(initialSpilled));
                    Assert.That(app.World.MilkFoamVolume(pitcher), Is.EqualTo(initialVolume));
                    Assert.That(app.World.State.production.miniGameToken, Is.Null);
                }
            }

            MouseAt(pointer.position, false, true);
            yield return null;
            yield return null;
            Assert.That(mouse.leftButton.isPressed, Is.False);
            Assert.That(mouse.rightButton.isPressed, Is.True);
            Assert.That(app.IsDragging, Is.False, "Left release must finish a pitcher drag without waiting for right release");
            Assert.That(app.LockedPourTargetId, Is.Null);
            Assert.That(presenter.gameObject.activeSelf, Is.False);
            Assert.That(app.World.LiquidVolume(pitcher), Is.EqualTo(initialVolume));
            Assert.That(pitcher.production.spilledMl, Is.EqualTo(initialSpilled));
            Assert.That(app.World.State.production.miniGameToken, Is.Null);
        }

        [UnityTest]
        public IEnumerator ActualFoamPitcherDropBindsAndLocksTheSameObjectsWithoutAnOrdinaryPour()
        {
            var pitcher = Pitcher();
            FillAndFroth(pitcher);
            var cup = TakeCup();
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 10f });
            yield return null;
            var pitcherView = app.View(pitcher.id);
            var cupView = app.View(cup.id);
            float initialFoam = app.World.MilkFoamVolume(pitcher);
            Vector2 pitcherPosition = new Vector2(pitcher.x, pitcher.y);
            Vector2 cupPosition = new Vector2(cup.x, cup.y);
            string token = DropPitcherOnCup(pitcher, cup);
            Assert.That(app.World.ServingCup, Is.Null, "The dragged cup need not be the serving-tray selection");
            Assert.That(app.World.State.production.miniGameFoamUsedMl, Is.Zero);
            Assert.That(app.World.LiquidVolume(cup), Is.EqualTo(10f));
            Assert.That(app.World.MilkFoamVolume(pitcher), Is.EqualTo(initialFoam));
            Assert.That(app.World.Locked(pitcher), Is.True);
            Assert.That(app.World.Locked(cup), Is.True);
            Assert.That(app.World.Move(pitcher.id, AreaId.Counter, 1f, 1f).success, Is.False);
            Assert.That(app.World.Move(cup.id, AreaId.Counter, 1f, 1f).success, Is.False);
            Assert.That(app.World.PourLiquid(pitcher.id, cup.id, 20f, 1f).success, Is.False);
            Assert.That(app.World.EmptyProductionMachine(pitcher.id).success, Is.False);
            Assert.That(app.World.TakeAndInteract("s.milk", cup.id, AreaId.Counter, 0f, 0f).success, Is.False);
            Assert.That(new Vector2(pitcher.x, pitcher.y), Is.EqualTo(pitcherPosition));
            Assert.That(new Vector2(cup.x, cup.y), Is.EqualTo(cupPosition));
            Assert.That(app.View(pitcher.id), Is.SameAs(pitcherView));
            Assert.That(app.View(cup.id), Is.SameAs(cupView));
            yield return WaitForTransition();
            Assert.That(app.InputBlocked, Is.True);
            Assert.That(app.World.State.production.miniGameToken, Is.EqualTo(token));
            FoamSurface();
            yield return ClickUI(Overlay().Find("Content/PresetPanel/Cancel").GetComponent<Button>());
            yield return WaitForTransition();
            Assert.That(app.World.Locked(pitcher), Is.False);
            Assert.That(app.World.Locked(cup), Is.False);
            Assert.That(app.InputBlocked, Is.False);
            AssertFoamBalance(pitcher, cup, initialFoam, 0f, 10f, 0f);
        }

        [UnityTest]
        public IEnumerator ActualPlaneStrokesConserveFoamAndBothResetsDiscardOnlyCurrentCupFoam()
        {
            var pitcher = Pitcher();
            FillAndFroth(pitcher);
            var cup = TakeCup();
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 100f });
            cup.contents.Add(new Portion { specId = "milkfoam", units = 1, milliliters = 15f });
            float initialSource = app.World.MilkFoamVolume(pitcher);
            float initialCupFoam = app.World.MilkFoamVolume(cup);
            float initialCupLiquid = app.World.LiquidVolume(cup);
            yield return null;
            string token = DropPitcherOnCup(pitcher, cup);
            yield return WaitForTransition();
            var surface = FoamSurface();
            var overlay = Overlay();
            var counter = overlay.Find("Content/FoamUsage").GetComponent<TMP_Text>();
            Assert.That(counter.text, Does.StartWith("已用奶泡"));
            yield return DrawActualStroke(surface);
            float used = app.World.State.production.miniGameFoamUsedMl;
            Assert.That(used, Is.GreaterThan(0f));
            AssertFoamBalance(pitcher, cup, initialSource, initialCupFoam, initialCupLiquid, used);
            Assert.That(counter.text, Does.StartWith("已用奶泡 " + used.ToString("0.0") + " ml"));
            // Verify the renderer's actual GPU deposition without depending on the implementation class.
            var texture = surface.GetComponent<Renderer>().sharedMaterial.GetTexture("_MainTex") as RenderTexture;
            Assert.That(texture, Is.Not.Null);
            RenderTexture previousActive = RenderTexture.active;
            var readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBAFloat, false, true);
            try
            {
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                readback.Apply();
                float deposited = 0f;
                foreach (var pixel in readback.GetPixels()) deposited = Mathf.Max(deposited, pixel.r);
                Assert.That(deposited, Is.GreaterThan(.001f), "Foam consumption must correspond to real GPU milk deposition");
            }
            finally { RenderTexture.active = previousActive; Object.Destroy(readback); }
            yield return null;
            yield return null;
            Assert.That(app.World.State.production.miniGameFoamUsedMl, Is.EqualTo(used));
            yield return ClickUI(overlay.Find("Content/PresetPanel/PresetList/tulipButton").GetComponent<Button>());
            Assert.That(overlay.Find("Content/PresetPanel/PresetList/tulipButton/Selected").gameObject.activeSelf, Is.True);
            Assert.That(app.World.State.production.miniGameFoamUsedMl, Is.EqualTo(used), "UI clicks must not draw or consume foam");
            yield return ClickUI(overlay.Find("Content/Reset").GetComponent<Button>());
            Assert.That(((ILatteArtSurface)surface).StrokePointCount, Is.Zero);
            Assert.That(app.World.State.production.miniGameFoamUsedMl, Is.Zero);
            Assert.That(app.World.MilkFoamVolume(cup), Is.EqualTo(initialCupFoam).Within(VolumeTolerance));
            Assert.That(app.World.LiquidVolume(cup), Is.EqualTo(initialCupLiquid).Within(VolumeTolerance));
            Assert.That(app.World.MilkFoamVolume(pitcher), Is.EqualTo(initialSource - used).Within(VolumeTolerance));
            float afterFirstReset = app.World.MilkFoamVolume(pitcher);
            yield return DrawActualStroke(surface);
            float secondUsed = app.World.State.production.miniGameFoamUsedMl;
            Assert.That(secondUsed, Is.GreaterThan(0));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(((ILatteArtSurface)surface).StrokePointCount, Is.Zero);
            Assert.That(app.World.State.production.miniGameFoamUsedMl, Is.Zero);
            Assert.That(counter.text, Does.StartWith("已用奶泡 0.0 ml"));
            Assert.That(app.World.MilkFoamVolume(pitcher), Is.EqualTo(afterFirstReset - secondUsed).Within(VolumeTolerance));
            Assert.That(app.World.MilkFoamVolume(cup), Is.EqualTo(initialCupFoam).Within(VolumeTolerance));
            Assert.That(app.World.LiquidVolume(cup), Is.EqualTo(initialCupLiquid).Within(VolumeTolerance));
            yield return ClickUI(overlay.Find("Content/PresetPanel/Cancel").GetComponent<Button>());
            yield return WaitForTransition();
            Assert.That(app.InputBlocked, Is.False);
            Assert.That(app.World.State.production.miniGameToken, Is.Null);
            Assert.That(app.World.MilkFoamVolume(cup), Is.EqualTo(initialCupFoam).Within(VolumeTolerance));
            Assert.That(app.World.LiquidVolume(cup), Is.EqualTo(initialCupLiquid).Within(VolumeTolerance));
            Assert.That(app.World.ConsumeLatteArtFoam(token, 5f), Is.Zero);
            Assert.That(app.World.ResolveProductionMiniGame(token, true, 100f).success, Is.False);
        }

        [UnityTest]
        public IEnumerator LatteBaseUsesWeightedCupColorBeforeDrawingAndAfterReset()
        {
            var pitcher = Pitcher(); FillAndFroth(pitcher);
            var cup = TakeCup();
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 100f });
            cup.contents.Add(new Portion { specId = "milkfoam", units = 1, milliliters = 50f });
            yield return null;
            DropPitcherOnCup(pitcher, cup);
            yield return WaitForTransition();
            var surface = FoamSurface();
            var presenter = app.View(cup.id).GetComponentInChildren<OrthographicGlassCupPresenter>(true);
            var material = surface.GetComponent<Renderer>().sharedMaterial;
            Color expected = presenter.MixedColor; expected.a = 1;
            Assert.That(Vector4.Distance(material.GetColor("_CoffeeColor"), expected), Is.LessThan(.001f));
            yield return DrawActualStroke(surface);
            expected = presenter.MixedColor; expected.a = 1;
            Assert.That(Vector4.Distance(material.GetColor("_CoffeeColor"), expected), Is.LessThan(.001f));
            Overlay().Find("Content/Reset").GetComponent<Button>().onClick.Invoke();
            Assert.That(app.World.State.production.miniGameFoamUsedMl, Is.Zero);
            Assert.That(app.World.LiquidSnapshot(cup).composition.milkFoamMl, Is.EqualTo(50f).Within(.001f));
            expected = presenter.MixedColor; expected.a = 1;
            Assert.That(Vector4.Distance(material.GetColor("_CoffeeColor"), expected), Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator LatteSurfaceTracksCupBodyColorAndEveryPresetShowsLiveFitWithOneBest()
        {
            var pitcher = Pitcher(); FillAndFroth(pitcher);
            var cup = TakeCup();
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 100f });
            yield return null;
            DropPitcherOnCup(pitcher, cup);
            yield return WaitForTransition();
            var surface = FoamSurface();
            var presenter = app.View(cup.id).GetComponentInChildren<OrthographicGlassCupPresenter>(true);
            var material = surface.GetComponent<Renderer>().sharedMaterial;
            Color expected = presenter.MixedColor * bench.liquidColorBrightness; expected.a = 1;
            Assert.That(Vector4.Distance(material.GetColor("_CoffeeColor"), expected), Is.LessThan(.001f));
            Assert.That(bench.liquidColorBrightness, Is.EqualTo(1f), "The scene must not darken the drink independently");
            Assert.That(bench.foamMlPerSecond, Is.EqualTo(6f));
            var list = Overlay().Find("Content/PresetPanel/PresetList");
            foreach (Transform preset in list)
                Assert.That(preset.Find("Fit").GetComponent<TMP_Text>().text, Is.EqualTo("拟合 0%"));
            yield return DrawActualStroke(surface);
            yield return new WaitForSecondsRealtime(bench.recognitionInterval + .1f);
            int highlighted = 0;

            foreach (Transform preset in list)
            {
                string fit = preset.Find("Fit").GetComponent<TMP_Text>().text;
                Assert.That(fit, Does.StartWith("拟合 ").And.EndWith("%"));

                if (preset.Find("Best").gameObject.activeSelf) highlighted++;
            }
            // An arbitrary short stroke may have zero similarity to every template.
            // The highest candidate still receives exactly one highlight.
            Assert.That(highlighted, Is.EqualTo(1));
            Overlay().Find("Content/Reset").GetComponent<Button>().onClick.Invoke();
            Assert.That(app.World.State.production.miniGameFoamUsedMl, Is.Zero);
            expected = presenter.MixedColor * bench.liquidColorBrightness; expected.a = 1;
            Assert.That(Vector4.Distance(material.GetColor("_CoffeeColor"), expected), Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator CancelPreservesPreviousCupArtUntilActualFoamIsConsumedAndNeverRestoresItAfterDrawing()
        {
            var pitcher = Pitcher();
            FillAndFroth(pitcher);
            var cup = TakeCup();
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 100f });
            cup.latteArt = true;
            cup.latteArtScore = 93f;
            cup.latteArtPattern = "rosetta";
            float initialFoam = app.World.MilkFoamVolume(pitcher);
            yield return null;
            DropPitcherOnCup(pitcher, cup);
            yield return WaitForTransition();
            Assert.That(cup.latteArt, Is.True);
            Assert.That(cup.latteArtScore, Is.EqualTo(93f));
            Assert.That(cup.latteArtPattern, Is.EqualTo("rosetta"));
            yield return ClickUI(Overlay().Find("Content/Reset").GetComponent<Button>());
            yield return ClickUI(Overlay().Find("Content/PresetPanel/Cancel").GetComponent<Button>());
            yield return WaitForTransition();
            Assert.That(cup.latteArt, Is.True);
            Assert.That(cup.latteArtScore, Is.EqualTo(93f));
            Assert.That(cup.latteArtPattern, Is.EqualTo("rosetta"));
            AssertFoamBalance(pitcher, cup, initialFoam, 0f, 100f, 0f);
            DropPitcherOnCup(pitcher, cup);
            yield return WaitForTransition();
            yield return DrawActualStroke(FoamSurface());
            float used = app.World.State.production.miniGameFoamUsedMl;
            Assert.That(used, Is.GreaterThan(0f));
            Assert.That(cup.latteArt, Is.False);
            Assert.That(cup.latteArtScore, Is.Zero);
            Assert.That(cup.latteArtPattern, Is.Null);
            yield return ClickUI(Overlay().Find("Content/PresetPanel/Cancel").GetComponent<Button>());
            yield return WaitForTransition();
            Assert.That(cup.latteArt, Is.False);
            Assert.That(cup.latteArtScore, Is.Zero);
            Assert.That(cup.latteArtPattern, Is.Null);
            AssertFoamBalance(pitcher, cup, initialFoam, 0f, 100f, used);
        }

        [UnityTest]
        public IEnumerator MilkMustBeClickedIntoFoamBeforeDraggingToCup()
        {
            var pitcher=Pitcher();var view=app.View(pitcher.id);
            Assert.That(app.World.TakeAndInteract("s.milk",pitcher.id,AreaId.Counter,0,0).success,Is.True);
            float volume=app.World.LiquidVolume(pitcher);var origin=view.transform.position;
            app.BeginDrag(view,Pointer(view));
            Assert.That(app.IsDragging,Is.False);
            Assert.That(view.transform.position,Is.EqualTo(origin));
            Assert.That(app.World.MilkFoamVolume(pitcher),Is.Zero);
            Assert.That(app.World.State.production.miniGameToken,Is.Null);
            app.Click(view);
            Assert.That(app.World.MilkFoamVolume(pitcher),Is.EqualTo(volume).Within(VolumeTolerance));
            Assert.That(app.World.CanPour(pitcher),Is.False);
            var cup=TakeCup();
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 10f });
            yield return null;
            Physics2D.SyncTransforms();app.BeginDrag(view,Pointer(view));Assert.That(app.IsDragging,Is.True);
            app.EndDrag(Pointer(app.View(cup.id)));
            Assert.That(app.World.State.production.miniGamePitcherId,Is.EqualTo(pitcher.id));
            Assert.That(app.World.State.production.miniGameCupId,Is.EqualTo(cup.id));
        }

        [UnityTest]
        public IEnumerator EmptyPitcherCannotFrothOrStartASessionWhenDroppedOnAnActualCup()
        {
            var pitcher = Pitcher();
            var cup = TakeCup();
            app.Click(app.View(pitcher.id));
            Assert.That(app.World.MilkFoamVolume(pitcher), Is.Zero);
            yield return null;
            Physics2D.SyncTransforms();
            app.BeginDrag(app.View(pitcher.id), Pointer(app.View(pitcher.id)));
            Assert.That(app.IsDragging, Is.False,"Empty pitcher must be filled and clicked before dragging");
            app.EndDrag(Pointer(app.View(cup.id)));
            Assert.That(app.IsDragging, Is.False);
            Assert.That(app.World.State.production.miniGameToken, Is.Null);
            Assert.That(bench.IsTransitioning, Is.False);
            Assert.That(app.World.Locked(pitcher), Is.False);
            Assert.That(app.World.Locked(cup), Is.False);
            Assert.That(app.World.LiquidVolume(cup), Is.Zero);
            Assert.That(app.World.LiquidVolume(pitcher), Is.Zero);
            yield return null;
        }

        private IEnumerator HoldActualPointerUntilBoundary(MonoBehaviour surface, float acceptedMl)
        {
            var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
            Vector2 center = app.worldCamera.WorldToScreenPoint(surface.transform.TransformPoint(mesh.bounds.center));
            MouseAt(center, true);
            float deadline = Time.realtimeSinceStartup + 1f;
            while (app.World.State.production.miniGameFoamUsedMl < acceptedMl - .001f && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(app.World.State.production.miniGameFoamUsedMl, Is.EqualTo(acceptedMl).Within(.001f));
            int pointCount = ((ILatteArtSurface)surface).StrokePointCount;
            Assert.That(pointCount, Is.GreaterThan(0));
            for (int sample = 1; sample <= 4; sample++)
            {
                MouseAt(center + Vector2.right * sample * 5f, true);
                yield return null;
            }
            Assert.That(((ILatteArtSurface)surface).StrokePointCount, Is.EqualTo(pointCount),
                "Drawing must stop when no foam can be accepted");
            Assert.That(app.World.State.production.miniGameFoamUsedMl, Is.EqualTo(acceptedMl).Within(.001f));
            MouseAt(center, false);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualPointerClampsToRemainingCupSpaceAndStopsWithoutSpillingOrRefunding()
        {
            var pitcher = Pitcher();
            FillAndFroth(pitcher);
            var cup = TakeCup();
            float initialCupLiquid = app.World.LiquidCapacity(cup) - .5f;
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = initialCupLiquid });
            float initialSource = app.World.MilkFoamVolume(pitcher);
            yield return null;
            DropPitcherOnCup(pitcher, cup);
            yield return WaitForTransition();
            yield return HoldActualPointerUntilBoundary(FoamSurface(), .5f);
            AssertFoamBalance(pitcher, cup, initialSource, 0f, initialCupLiquid, .5f);
            Assert.That(app.World.LiquidVolume(cup), Is.EqualTo(app.World.LiquidCapacity(cup)).Within(.001f));
            Assert.That(pitcher.production.spilledMl, Is.Zero);
            Assert.That(cup.production.spilledMl, Is.Zero);
            Assert.That(Overlay().Find("Content/FoamUsage").GetComponent<TMP_Text>().text, Does.Contain("杯子已满"));
            yield return ClickUI(Overlay().Find("Content/PresetPanel/Cancel").GetComponent<Button>());
            yield return WaitForTransition();
            AssertFoamBalance(pitcher, cup, initialSource, 0f, initialCupLiquid, .5f);
        }

        [UnityTest]
        public IEnumerator ActualPointerExhaustsTheSourceAndEmptyPitcherBecomesRefillableOnlyAfterExit()
        {
            var pitcher = Pitcher();
            pitcher.contents.Add(new Portion { specId = "milk", units = 1, milliliters = .5f });
            app.Click(app.View(pitcher.id));
            Assert.That(app.World.MilkFoamVolume(pitcher), Is.EqualTo(.5f));
            var cup = TakeCup();
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 10f });
            yield return null;
            DropPitcherOnCup(pitcher, cup);
            yield return WaitForTransition();
            yield return HoldActualPointerUntilBoundary(FoamSurface(), .5f);
            AssertFoamBalance(pitcher, cup, .5f, 0f, 10f, .5f);
            Assert.That(pitcher.contents, Is.Empty);
            Assert.That(Overlay().Find("Content/FoamUsage").GetComponent<TMP_Text>().text, Does.Contain("奶泡已用完"));
            Assert.That(app.World.TakeAndInteract("s.milk", pitcher.id, AreaId.Counter, 0f, 0f).success, Is.False);
            yield return ClickUI(Overlay().Find("Content/PresetPanel/Cancel").GetComponent<Button>());
            yield return WaitForTransition();
            AssertFoamBalance(pitcher, cup, .5f, 0f, 10f, .5f);
            Assert.That(app.World.TakeAndInteract("s.milk", pitcher.id, AreaId.Counter, 0f, 0f).success, Is.True);
            Assert.That(app.World.CanPour(pitcher), Is.False);
        }

        [UnityTest]
        public IEnumerator LegacyUnlimitedGMRequestRemainsAvailableButNormalHUDCannotStartIt()
        {
            var hud = app.GetComponent<FreeCafeProductionHud>();
            Assert.That(hud.miniGameButton == null || !hud.miniGameButton.gameObject.activeSelf, Is.True);
            Assert.That(app.World.Find("tool.latte-art"), Is.Null);
            Assert.That(app.View("tool.latte-art"), Is.Null, "The obsolete world-tool click route must be absent");
            Assert.That(app.World.SelectServingCup("s.cups.large").success, Is.True);
            if(hud.miniGameButton != null) hud.miniGameButton.onClick.Invoke();
            Assert.That(app.World.State.production.miniGameToken, Is.Null, "Even the hidden HUD button must have no legacy shortcut listener");
            var cup = app.World.ServingCup;
            var pitcher = Pitcher();
            float sourceBefore = app.World.MilkFoamVolume(pitcher);
            float cupBefore = app.World.LiquidVolume(cup);
            Assert.That(app.World.RequestProductionMiniGame().success, Is.True);
            Assert.That(app.World.State.production.miniGamePitcherId, Is.Null);
            Assert.That(app.World.State.production.miniGameCupId, Is.EqualTo(cup.id));
            yield return WaitForTransition();
            Assert.That(Overlay().Find("Content/FoamUsage").GetComponent<TMP_Text>().text, Does.Contain("GM 调试模式"));
            yield return DrawActualStroke(FoamSurface());
            Assert.That(app.World.MilkFoamVolume(pitcher), Is.EqualTo(sourceBefore));
            Assert.That(app.World.LiquidVolume(cup), Is.EqualTo(cupBefore));
            Assert.That(app.World.State.production.miniGameFoamUsedMl, Is.Zero);
            yield return ClickUI(Overlay().Find("Content/PresetPanel/Cancel").GetComponent<Button>());
            yield return WaitForTransition();
            Assert.That(app.InputBlocked, Is.False);
            Assert.That(hud.miniGameButton == null || !hud.miniGameButton.gameObject.activeSelf, Is.True);
        }
    }
}
