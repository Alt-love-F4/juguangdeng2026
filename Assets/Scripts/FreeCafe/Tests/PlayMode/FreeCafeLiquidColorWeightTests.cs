using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeLiquidColorWeightTests
    {
        private const float Tolerance = 0.00001f;
        private GameObject root;
        private OrthographicGlassCupPresenter presenter;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("LiquidColorWeightTests", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            presenter = CreatePresenter("Cup");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [Test]
        public void EqualCoffeeAndMilkUseThreeToOneColorWeights()
        {
            Color coffee = PureColor("coffee");
            Color milk = PureColor("milk");

            Color actual = Present(new CafeLiquidComposition(100f, 100f, 0f, 0f, 0f, 0f));

            AssertColor(actual, (coffee * 300f + milk * 100f) / 400f);
        }

        [Test]
        public void EqualCoffeeAndFoamUseThreeToPointOneWeightsAndFoamHasLessInfluenceThanMilk()
        {
            Color coffee = PureColor("coffee");
            Color milk = PureColor("milk");
            Color withMilk = Present(new CafeLiquidComposition(100f, 100f, 0f, 0f, 0f, 0f));
            // Foam is included in milkMl, not an additional 100 ml.
            Color withFoam = Present(new CafeLiquidComposition(100f, 100f, 0f, 0f, 0f, 0f, 100f));

            AssertColor(withFoam, (coffee * 300f + milk * 10f) / 310f);
            float milkInfluence = ((Vector4)withMilk - (Vector4)coffee).sqrMagnitude;
            float foamInfluence = ((Vector4)withFoam - (Vector4)coffee).sqrMagnitude;
            Assert.That(milkInfluence, Is.GreaterThan(Tolerance), "The sampled palette must distinguish coffee and milk.");
            Assert.That(foamInfluence, Is.GreaterThan(0f).And.LessThan(milkInfluence));
        }

        [TestCase("milk")]
        [TestCase("water")]
        [TestCase("matcha.drink")]
        [TestCase("cocoa.drink")]
        [TestCase("other")]
        public void EachNonCoffeeNonFoamComponentHasWeightOne(string ingredient)
        {
            Color coffee = PureColor("coffee");
            Color component = PureColor(ingredient);
            CafeLiquidComposition pure = PureComposition(ingredient);
            var mixed = new CafeLiquidComposition(100f, pure.milkMl, pure.waterMl,
                pure.matchaMl, pure.cocoaMl, pure.otherMl);

            AssertColor(Present(mixed), (coffee * 300f + component * 100f) / 400f);
        }

        [Test]
        public void PureFoamKeepsMilkColorIncludingAlpha()
        {
            Color milk = PureColor("milk");

            AssertColor(PureColor("milkfoam"), milk);
        }

        [Test]
        public void PartialFoamOnlyReweightsItsSubsetOfMilk()
        {
            Color coffee = PureColor("coffee");
            Color milk = PureColor("milk");
            var composition = new CafeLiquidComposition(100f, 100f, 0f, 0f, 0f, 0f, 40f);

            // 60 ml normal milk + 40 ml foam contributes 60 + 4 color units.
            AssertColor(Present(composition), (coffee * 300f + milk * 64f) / 364f);
            Assert.That(composition.TotalMl, Is.EqualTo(200f).Within(Tolerance));
        }

        [TestCase(100f, 0f, 0f, 100f, 0.25f)]
        [TestCase(0f, 100f, 0f, 100f, 0.25f)]
        [TestCase(0f, 100f, 100f, 100f, 0.25f)]
        [TestCase(100f, 100f, 0f, 200f, 0.5f)]
        [TestCase(100f, 100f, 100f, 200f, 0.5f)]
        public void ColorWeightsDoNotChangeActualVolumeCapacityOrTargetFill(
            float coffeeMl, float milkMl, float foamMl, float expectedVolume, float expectedFill)
        {
            var composition = new CafeLiquidComposition(coffeeMl, milkMl, 0f, 0f, 0f, 0f, foamMl);
            CafeLiquidSnapshot snapshot = Snapshot(composition, 400f);

            presenter.Present(snapshot, 0f, false);

            Assert.That(composition.TotalMl, Is.EqualTo(expectedVolume).Within(Tolerance));
            Assert.That(snapshot.volumeMl, Is.EqualTo(expectedVolume).Within(Tolerance));
            Assert.That(snapshot.capacityMl, Is.EqualTo(400f).Within(Tolerance));
            Assert.That(presenter.TargetFill, Is.EqualTo(expectedFill).Within(Tolerance));
            Assert.That(presenter.GetComponent<GravityLiquidImage>().CurrentFill,
                Is.EqualTo(expectedFill).Within(Tolerance), "The first snapshot must initialize the actual unweighted fill.");
        }

        [Test]
        public void AllComponentsContributeToVolumeWithoutAddingFoamAgain()
        {
            var composition = new CafeLiquidComposition(100f, 100f, 100f, 100f, 100f, 100f, 100f);
            CafeLiquidSnapshot snapshot = Snapshot(composition, 1000f);

            presenter.Present(snapshot, 0f, false);

            Assert.That(composition.TotalMl, Is.EqualTo(600f).Within(Tolerance));
            Assert.That(snapshot.volumeMl, Is.EqualTo(600f).Within(Tolerance));
            Assert.That(snapshot.capacityMl, Is.EqualTo(1000f).Within(Tolerance));
            Assert.That(presenter.TargetFill, Is.EqualTo(0.6f).Within(Tolerance));
        }

        [TestCase("milk")]
        [TestCase("milkfoam")]
        public void FinalColorIsIndependentOfPresentationOrderAndPrimaryIngredient(string ingredient)
        {
            Color coffee = PureColor("coffee");
            Color milk = PureColor("milk");
            OrthographicGlassCupPresenter otherPresenter = CreatePresenter("ReverseOrderCup");
            CafeLiquidComposition added = PureComposition(ingredient);
            var mixed = new CafeLiquidComposition(100f, 100f, 0f, 0f, 0f, 0f, added.milkFoamMl);

            presenter.Present(Snapshot(PureComposition("coffee"), 400f, "coffee"), 0f, false);
            presenter.Present(Snapshot(mixed, 400f, "coffee"), 0f, false);
            otherPresenter.Present(Snapshot(added, 400f, ingredient), 0f, false);
            otherPresenter.Present(Snapshot(mixed, 400f, ingredient), 0f, false);

            float milkColorAmount = ingredient == "milkfoam" ? 10f : 100f;
            Color expected = (coffee * 300f + milk * milkColorAmount) / (300f + milkColorAmount);
            AssertColor(presenter.MixedColor, expected);
            AssertColor(otherPresenter.MixedColor, expected);
            AssertColor(presenter.MixedColor, otherPresenter.MixedColor);
            Assert.That(presenter.TargetFill, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(otherPresenter.TargetFill, Is.EqualTo(0.5f).Within(Tolerance));

            presenter.Present(Snapshot(mixed, 400f, ingredient), 0f, false);
            AssertColor(presenter.MixedColor, expected);
        }

        private OrthographicGlassCupPresenter CreatePresenter(string name)
        {
            var cup = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(GravityLiquidImage));
            cup.transform.SetParent(root.transform, false);
            cup.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 120f);
            return cup.AddComponent<OrthographicGlassCupPresenter>();
        }

        private Color PureColor(string ingredient)
        {
            presenter.Present(Snapshot(PureComposition(ingredient), 400f, ingredient), 0f, false);
            return presenter.MixedColor;
        }

        private Color Present(CafeLiquidComposition composition)
        {
            presenter.Present(Snapshot(composition, 400f), 0f, false);
            return presenter.MixedColor;
        }

        private static CafeLiquidSnapshot Snapshot(CafeLiquidComposition composition, float capacity,
            string primaryIngredient = "coffee")
        {
            return new CafeLiquidSnapshot(composition.TotalMl, capacity, LiquidTemperature.Room,
                ProductionPhase.Ready, primaryIngredient, 100f, composition);
        }

        private static CafeLiquidComposition PureComposition(string ingredient)
        {
            switch (ingredient)
            {
                case "coffee": return new CafeLiquidComposition(100f, 0f, 0f, 0f, 0f, 0f);
                case "milk": return new CafeLiquidComposition(0f, 100f, 0f, 0f, 0f, 0f);
                case "milkfoam": return new CafeLiquidComposition(0f, 100f, 0f, 0f, 0f, 0f, 100f);
                case "water": return new CafeLiquidComposition(0f, 0f, 100f, 0f, 0f, 0f);
                case "matcha.drink": return new CafeLiquidComposition(0f, 0f, 0f, 100f, 0f, 0f);
                case "cocoa.drink": return new CafeLiquidComposition(0f, 0f, 0f, 0f, 100f, 0f);
                case "other": return new CafeLiquidComposition(0f, 0f, 0f, 0f, 0f, 100f);
                default: throw new System.ArgumentOutOfRangeException(nameof(ingredient), ingredient, "Unknown test ingredient.");
            }
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(Tolerance), "red");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(Tolerance), "green");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(Tolerance), "blue");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(Tolerance), "alpha");
        }
    }
}
