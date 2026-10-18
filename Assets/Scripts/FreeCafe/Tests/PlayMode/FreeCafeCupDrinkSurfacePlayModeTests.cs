using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    /// <summary>识别出的饮品在杯口椭圆上显示对应液面；未识别、空杯、倾倒时保留原杯面。</summary>
    public sealed class FreeCafeCupDrinkSurfacePlayModeTests
    {
        private FreeCafeRuntime app;
        private CafeWorld world;

        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop");
            yield return null; yield return null;
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            app.areaManager.panSmoothTime = 0;
            world = app.World;
            Assert.That(world.StartDay().success, Is.True);
            app.Enter(AreaId.Counter);
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (app != null) app.CancelDrag();
            Time.timeScale = 1;
            yield return null;
        }

        private WorldEntity ServingCup(string sourceId)
        {
            if (world.ServingCup != null && world.ServingCup.contents.Count > 0) world.ServingCup.contents.Clear();
            Assert.That(world.SelectServingCup(sourceId).success, Is.True, sourceId);
            return world.ServingCup;
        }

        /// <summary>Same recipe filling the domain classification tests use.</summary>
        private void Fill(WorldEntity cup, ProductSpec product)
        {
            cup.contents.Clear();
            foreach (var need in product.evaluation.amounts)
            {
                var temperature = need.coffeeKind == "black" ? LiquidTemperature.Warm : LiquidTemperature.Room;
                if (need.minimumTemperature >= 0 && (int)temperature < need.minimumTemperature)
                    temperature = (LiquidTemperature)need.minimumTemperature;
                var portion = new Portion { specId = need.ingredientId, milliliters = need.idealMl, grams = need.idealGrams,
                    temperature = temperature, coffeeKind = need.coffeeKind, productionScore = 100 };
                if (portion.specId == "coffee")
                {
                    world.AddConstituent(portion, "water", 0, portion.milliliters);
                    world.AddConstituent(portion, "grounds", portion.milliliters * product.evaluation.idealCoffeeStrength, 0);
                }
                cup.contents.Add(portion);
            }
            cup.latteArt = System.Array.Exists(product.evaluation.amounts, need => need.ingredientId == "milkfoam");
        }

        private IEnumerator Show(WorldEntity cup)
        {
            app.DebugRefreshViews();
            float deadline = Time.realtimeSinceStartup + 2f;
            var surface = app.View(cup.id).GetComponent<FreeCafeCupDrinkSurface>();
            while (Time.realtimeSinceStartup < deadline && (surface == null || surface.Renderer == null
                || surface.Visible != (surface.Current != null) || surface.Visible && surface.Renderer.color.a < .99f))
            {
                yield return null;
                surface = app.View(cup.id).GetComponent<FreeCafeCupDrinkSurface>();
            }
        }

        [UnityTest] public IEnumerator EveryRecognisedDrinkShowsItsOwnSurfaceInsideTheCupMouth()
        {
            var library = CafeDrinkSurfaceLibrary.Default;
            Assert.That(library, Is.Not.Null, "Resources/FreeCafe/DrinkSurfaces.asset");
            var cup = ServingCup("s.cups.large");
            int checkedDrinks = 0;
            foreach (var product in world.Catalog.products)
            {
                if (library.Surface(product.id) == null) continue;
                Fill(cup, product);
                Assert.That(world.ClassifyCoffee(cup).productId, Is.EqualTo(product.id), "Fixture recipe must classify.");
                yield return Show(cup);
                var view = app.View(cup.id);
                var surface = view.GetComponent<FreeCafeCupDrinkSurface>();
                Assert.That(surface, Is.Not.Null);
                Assert.That(surface.ProductId, Is.EqualTo(product.id));
                Assert.That(surface.Visible, Is.True, product.id);
                Assert.That(surface.Renderer.sprite, Is.SameAs(library.Surface(product.id)));
                // The ellipse sits in the upper part of the cup art, inside its silhouette.
                Bounds cupBounds = view.body.bounds, liquid = surface.Renderer.bounds;
                Assert.That(liquid.min.x, Is.GreaterThanOrEqualTo(cupBounds.min.x - .01f));
                Assert.That(liquid.max.x, Is.LessThanOrEqualTo(cupBounds.max.x + .01f));
                Assert.That(liquid.center.y, Is.GreaterThan(cupBounds.center.y), "Surface belongs at the cup mouth.");
                Assert.That(liquid.size.x, Is.GreaterThan(liquid.size.y * 1.5f), "Shown as a 2D ellipse, wider than tall.");
                Assert.That(surface.Renderer.sortingOrder, Is.GreaterThan(view.body.sortingOrder));
                checkedDrinks++;
            }
            Assert.That(checkedDrinks, Is.EqualTo(world.Catalog.products.Length), "Every catalog drink has surface art.");
        }

        [UnityTest] public IEnumerator EmptyUnknownAndPouringCupsKeepTheirOwnArt()
        {
            var cup = ServingCup("s.cups.small");
            yield return Show(cup);
            var view = app.View(cup.id);
            var surface = view.GetComponent<FreeCafeCupDrinkSurface>();
            Assert.That(surface == null || !surface.Visible, Is.True, "Empty cup shows no drink surface.");

            Fill(cup, world.Product("blackcoffee"));
            yield return Show(cup);
            surface = view.GetComponent<FreeCafeCupDrinkSurface>();
            Assert.That(surface.Visible, Is.True);
            Assert.That(surface.ProductId, Is.EqualTo("blackcoffee"));

            view.SetReceivingPour(true);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(surface.Visible, Is.False, "The live liquid takes over while pouring.");
            view.SetReceivingPour(false);
            yield return Show(cup);
            Assert.That(surface.Visible, Is.True);

            cup.contents.Add(new Portion { specId = "sugar", units = 1, grams = 5 });
            Assert.That(world.ClassifyCoffee(cup).known, Is.False);
            yield return new WaitForSecondsRealtime(.4f);
            app.DebugRefreshViews();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(surface.ProductId, Is.Null);
            Assert.That(surface.Visible, Is.False, "Unrecognised drinks keep the plain cup art.");

            yield return new WaitForEndOfFrame();
            var directory = System.IO.Path.Combine(Application.dataPath, "../Logs/GameplayHud");
            System.IO.Directory.CreateDirectory(directory);
            Fill(cup, world.Product("blackcoffee"));
            yield return Show(cup);
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, "cup-drink-surface.png"), shot.EncodeToPNG());
            Object.Destroy(shot);
        }
    }
}
