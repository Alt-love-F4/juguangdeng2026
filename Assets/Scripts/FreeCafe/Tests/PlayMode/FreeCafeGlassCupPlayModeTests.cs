using System.Collections;
using System.IO;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeGlassCupPlayModeTests
    {
        private FreeCafeRuntime app;
        private float previousCaptureDeltaTime;

        [UnitySetUp] public IEnumerator Setup()
        {
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None)) panel.Cancel();
            Time.timeScale = 1f;
            previousCaptureDeltaTime = Time.captureDeltaTime;
            GameSaveParticipantRegistry.PendingRestore = null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            app.World.EnterProduction();
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = previousCaptureDeltaTime;
            if (app != null) app.CancelDrag();
            yield return null;
        }

        private FreeEntityView SelectCup(bool large)
        {
            Assert.That(app.World.SelectServingCup(large ? "s.cups.large" : "s.cups.small").success, Is.True);
            return app.View(app.World.ServingCup.id);
        }

        [UnityTest] public IEnumerator CupPrefabsUseGlassAndPreserveSizeSortingAndInput()
        {
            foreach (bool large in new[] { false, true })
            {
                var view = SelectCup(large);
                var presenter = view.liquidPresenter as OrthographicGlassCupPresenter;
                Assert.That(presenter, Is.Not.Null);
                Assert.That(view.body.enabled, Is.True);
                Assert.That(presenter.gameObject.activeSelf, Is.False);
                view.PresentPour(.5f, true);
                Assert.That(presenter.gameObject.activeSelf, Is.True);
                Assert.That(view.body.color.a, Is.EqualTo(.22f).Within(.001f));
                view.PresentPour(0f, false);
                Assert.That(presenter.gameObject.activeSelf, Is.False);
                Assert.That(view.hitbox.enabled, Is.True);
                Assert.That(presenter.GetComponent<Image>().sprite, Is.Null);
                Assert.That(presenter.GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(presenter.GetComponent<RectTransform>().sizeDelta,
                    Is.EqualTo(large ? new Vector2(1f, 1.2f) : new Vector2(0.8f, 1f)));
                var canvas = presenter.GetComponentInParent<Canvas>();
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(canvas.sortingOrder, Is.EqualTo(view.body.sortingOrder));
                Assert.That(presenter.GetComponent<GravityLiquidImage>().AllowSpilling, Is.False);
                Assert.That(presenter.GetComponent<GravityLiquidImage>().MotionSpace, Is.EqualTo(view.transform.parent));
                view.Refresh(app.World.ServingCup);
                view.Refresh(app.World.ServingCup);
                Assert.That(view.GetComponentsInChildren<OrthographicGlassCupPresenter>(true).Length, Is.EqualTo(1));
                view.Highlight(true, false);
                Assert.That(presenter.GetComponent<Image>().color, Is.EqualTo(Color.white));
                yield return null;
            }
        }

        [UnityTest] public IEnumerator IncomingLiquidRaisesSurfaceMixesColorsAndCreatesWavesWithoutTiltingCup()
        {
            var view = SelectCup(true);
            var cup = app.World.ServingCup;
            var presenter = (OrthographicGlassCupPresenter)view.liquidPresenter;
            view.PresentPour(0f, true);
            var liquid = presenter.GetComponent<GravityLiquidImage>();
            Assert.That(liquid.CurrentFill, Is.Zero);
            cup.contents.Add(new Portion { specId="coffee", milliliters=100, units=1 });
            view.Refresh(cup);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(liquid.CurrentFill, Is.GreaterThan(0f).And.LessThan(0.25f));
            Color coffee = presenter.MixedColor;
            cup.contents.Add(new Portion { specId="hotmilk", milliliters=100, units=1 });
            view.Refresh(cup);
            Assert.That(presenter.TargetFill, Is.EqualTo(0.5f));
            Assert.That(presenter.MixedColor.r, Is.GreaterThan(coffee.r));
            var image = presenter.GetComponent<Image>();
            Assert.That(image.material.GetVector("_Wave").x, Is.GreaterThan(0f));
            Color mixture = presenter.MixedColor;
            view.Refresh(cup);
            Assert.That(presenter.MixedColor, Is.EqualTo(mixture));
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.That(liquid.CurrentFill, Is.EqualTo(0.5f).Within(0.001f));
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(image.material.GetVector("_Wave").x, Is.LessThan(0.001f));
            cup.contents.Clear();
            view.Refresh(cup);
            Assert.That(liquid.CurrentFill, Is.Zero);
        }

        [UnityTest] public IEnumerator RatioIsOrderIndependentAndRestoreDoesNotInventPourMotion()
        {
            var view = SelectCup(false);
            var presenter = (OrthographicGlassCupPresenter)view.liquidPresenter;
            presenter.gameObject.SetActive(true);
            var first = new CafeLiquidSnapshot(120, 240, LiquidTemperature.Room, ProductionPhase.Ready, "coffee", 100,
                new CafeLiquidComposition(30, 90, 0, 0, 0, 0));
            presenter.Present(first, 0f, false);
            Color expected = presenter.MixedColor;
            presenter.Present(new CafeLiquidSnapshot(120, 240, LiquidTemperature.Room, ProductionPhase.Ready, "hotmilk", 100,
                first.composition), 0f, false);
            Assert.That(presenter.MixedColor, Is.EqualTo(expected));
            view.gameObject.SetActive(false);
            view.gameObject.SetActive(true);
            presenter.gameObject.SetActive(true);
            presenter.Present(first, 0f, false);
            Assert.That(presenter.GetComponent<GravityLiquidImage>().CurrentFill, Is.EqualTo(0.5f));
            Assert.That(presenter.GetComponent<Image>().material.GetVector("_Wave").x, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator ActualWorldPourReachesReceivingGlassWithoutChangingItsRotation()
        {
            var view = SelectCup(true);
            var cup = app.World.ServingCup;
            var presenter = (OrthographicGlassCupPresenter)view.liquidPresenter;
            view.PresentPour(0f, true);
            Assert.That(app.World.TakeAndInteract("s.milk", "m.heater", AreaId.Counter, 0, 0).success, Is.True);
            Assert.That(app.World.Operate("m.heater").success, Is.True);
            app.World.Tick(4f);
            Assert.That(app.World.PourLiquid("m.heater", cup.id, 40f, 1f).success, Is.True);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(presenter.TargetFill, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(presenter.GetComponent<Image>().material.GetVector("_Wave").x, Is.GreaterThan(0f));
            Assert.That(view.liquidRoot.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(app.World.LiquidVolume(cup), Is.EqualTo(40f));
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.That(presenter.GetComponent<GravityLiquidImage>().CurrentFill, Is.EqualTo(0.1f).Within(0.001f));
        }

        [UnityTest] public IEnumerator CaptureActualCupPrefabsWithExistingCoffeeShopCamera()
        {
            foreach (bool large in new[] { false, true })
            {
                var view = SelectCup(large);
                var cup = app.World.ServingCup;
                cup.contents.Add(new Portion { specId="coffee", milliliters=large ? 100f : 120f, units=1 });
                if (large) cup.contents.Add(new Portion { specId="hotmilk", milliliters=100f, units=1 });
                view.Refresh(cup);
                var presenter = (OrthographicGlassCupPresenter)view.liquidPresenter;
                view.PresentPour(0f, true);
                yield return new WaitForSecondsRealtime(1f);
                var camera = app.worldCamera;
                Vector3 previousPosition = camera.transform.position;
                float previousSize = camera.orthographicSize;
                RenderTexture previousTarget = camera.targetTexture;
                RenderTexture previousActive = RenderTexture.active;
                RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24);
                var screenshot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                try
                {
                    camera.transform.position = new Vector3(view.transform.position.x, view.transform.position.y, previousPosition.z);
                    camera.orthographicSize = presenter.GetComponent<RectTransform>().rect.height * Mathf.Abs(view.transform.lossyScale.y) * 1.5f;
                    camera.targetTexture = target;
                    Canvas.ForceUpdateCanvases();
                    presenter.GetComponent<GravityLiquidImage>().RefreshVisual();
                    Assert.That(Mathf.Abs(presenter.GetComponent<GravityLiquidImage>().SurfaceNormal.x), Is.LessThan(0.02f));
                    camera.Render();
                    RenderTexture.active = target;
                    screenshot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    screenshot.Apply();
                    string folder = Path.GetFullPath("output/glass-cups");
                    Directory.CreateDirectory(folder);
                    File.WriteAllBytes(Path.Combine(folder, large ? "large-latte.png" : "small-coffee.png"), screenshot.EncodeToPNG());
                    Color center = screenshot.GetPixel(640, 300);
                    Assert.That(center.r, Is.LessThan(0.7f), "Actual liquid should render in the lower interior, not the old opaque cup art.");
                    Assert.That(presenter.GetComponent<GravityLiquidImage>().CurrentFill, Is.EqualTo(0.5f).Within(0.001f));
                }
                finally
                {
                    camera.transform.position = previousPosition;
                    camera.orthographicSize = previousSize;
                    camera.targetTexture = previousTarget;
                    RenderTexture.active = previousActive;
                    RenderTexture.ReleaseTemporary(target);
                    Object.Destroy(screenshot);
                    cup.contents.Clear();
                }
            }
        }

        [UnityTest] public IEnumerator FilledCupOnlyShowsLiquidDuringPourOrReceivingAndSurvivesRefresh()
        {
            var view = SelectCup(true);
            var cup = app.World.ServingCup;
            cup.contents.Add(new Portion { specId="coffee", units=1, milliliters=120f });
            view.Refresh(cup);
            var presenter = (OrthographicGlassCupPresenter)view.liquidPresenter;
            Assert.That(presenter.gameObject.activeSelf, Is.False, "有液体但未倾倒时不显示液面");
            Assert.That(view.body.color.a, Is.EqualTo(1f));

            view.SetReceivingPour(true);
            view.Refresh(cup);
            Assert.That(presenter.gameObject.activeSelf, Is.True);
            Assert.That(view.body.color.a, Is.EqualTo(.22f).Within(.001f));
            Assert.That(presenter.TargetFill, Is.EqualTo(.3f).Within(.001f));
            view.SetReceivingPour(false);
            view.Refresh(cup);
            Assert.That(presenter.gameObject.activeSelf, Is.False);
            Assert.That(view.body.color.a, Is.EqualTo(1f));

            view.PresentPour(.5f, true);
            Assert.That(presenter.gameObject.activeSelf, Is.True);
            view.PresentPour(0f, false);
            view.Refresh(cup);
            Assert.That(presenter.gameObject.activeSelf, Is.False, "停止倾倒后不能被定期Refresh重新打开");
            Assert.That(view.body.color.a, Is.EqualTo(1f));
            Assert.That(app.World.LiquidVolume(cup), Is.EqualTo(120f));
            yield return null;
        }

        [UnityTest] public IEnumerator RuntimePourRefreshesReceivingGlassInTheSameFrame()
        {
            Time.captureDeltaTime = 1f / 60f;
            var view = SelectCup(true);
            var cup = app.World.ServingCup;
            Assert.That(app.World.TakeAndInteract("s.milk", "m.heater", AreaId.Counter, 0, 0).success, Is.True);
            Assert.That(app.World.Operate("m.heater").success, Is.True);
            app.World.Tick(4f);
            yield return null;
            var source = app.View("m.heater");
            var sourceEntity = app.World.Find("m.heater");
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                position = app.worldCamera.WorldToScreenPoint(source.transform.position)
            };
            app.BeginDrag(source, pointer);
            Assert.That(app.IsDragging, Is.True);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(FreeCafeRuntime).GetField("pointerScreen", flags).SetValue(app, Vector2.right * 90f);
            typeof(FreeCafeRuntime).GetField("vesselTiltPointerOrigin", flags).SetValue(app, Vector2.zero);
            typeof(FreeCafeRuntime).GetField("receiver", flags).SetValue(app, view);
            typeof(FreeCafeRuntime).GetField("overUI", flags).SetValue(app, false);
            typeof(FreeCafeRuntime).GetMethod("UpdateVesselPour", flags).Invoke(app, null);
            float volume = app.World.LiquidVolume(cup);
            Assert.That(volume, Is.GreaterThan(0f));
            Assert.That(app.World.LiquidVolume(sourceEntity), Is.LessThan(200f));
            var presenter = (OrthographicGlassCupPresenter)view.liquidPresenter;
            Assert.That(presenter.TargetFill, Is.EqualTo(volume / app.World.LiquidCapacity(cup)).Within(0.00001f));
            Assert.That(view.IsReceivingPour, Is.True);
            Assert.That(presenter.gameObject.activeSelf, Is.True);
            Assert.That(view.body.color.a, Is.EqualTo(.22f).Within(.001f));
            app.CancelDrag();
            yield return null;
            Assert.That(view.IsReceivingPour, Is.False);
            Assert.That(presenter.gameObject.activeSelf, Is.False);
            Assert.That(view.body.color.a, Is.EqualTo(1f));
            Assert.That(view.liquidRoot.localRotation, Is.EqualTo(Quaternion.identity));
        }
    }
}
