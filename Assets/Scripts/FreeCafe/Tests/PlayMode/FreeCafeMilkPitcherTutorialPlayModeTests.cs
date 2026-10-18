using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeMilkPitcherTutorialPlayModeTests
    {
        private FreeCafeRuntime app;
        private FreeCafeMilkPitcherTutorial tutorial;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null;
            yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            Assert.That(app, Is.Not.Null);
            app.areaManager.panSmoothTime = 0f;
            Assert.That(app.World.EnterProduction().success, Is.True);
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            tutorial = app.gameObject.AddComponent<FreeCafeMilkPitcherTutorial>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (tutorial != null) tutorial.Stop();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BeginPreparesIsolatedCupAndStopRestoresCounter()
        {
            var before = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var entity in app.World.State.entities) before.Add(entity.id, JsonUtility.ToJson(entity));
            int money = app.World.State.money;
            bool automatic = app.World.State.automaticMachines;
            string activeCup = app.World.State.production.activeCupId;
            Assert.That(tutorial.Begin(app), Is.True);
            Assert.That(tutorial.IsRunning, Is.True);
            Assert.That(tutorial.CurrentStage, Is.EqualTo(FreeCafeMilkPitcherTutorial.Stage.FillPitcher));
            Assert.That(app.World.ServingCup, Is.Not.Null);
            Assert.That(app.World.LiquidVolume(app.World.ServingCup), Is.EqualTo(100f).Within(.01f));
            tutorial.Stop();
            Assert.That(app.World.State.entities.Count, Is.EqualTo(before.Count));
            foreach (var entity in app.World.State.entities) Assert.That(JsonUtility.ToJson(entity), Is.EqualTo(before[entity.id]));
            Assert.That(app.World.State.money, Is.EqualTo(money));
            Assert.That(app.World.State.automaticMachines, Is.EqualTo(automatic));
            Assert.That(app.World.State.production.activeCupId, Is.EqualTo(activeCup));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MilkFillFrothAndTransferEnterRealLatteStage()
        {
            Assert.That(tutorial.Begin(app), Is.True);
            var pitcher = app.World.Find("tool.milk-pitcher");
            Assert.That(app.World.TakeAndInteract("s.milk", pitcher.id, AreaId.Counter, 0f, 0f).success, Is.True);
            yield return null;
            Assert.That(tutorial.CurrentStage, Is.EqualTo(FreeCafeMilkPitcherTutorial.Stage.Froth));
            Assert.That(app.World.FrothMilk(pitcher.id).success, Is.True);
            yield return null;
            Assert.That(tutorial.CurrentStage, Is.EqualTo(FreeCafeMilkPitcherTutorial.Stage.Transfer));
            var cup = app.World.ServingCup;
            Assert.That(app.World.Interact(pitcher.id, cup.id).success, Is.True);
            yield return null;
            Assert.That(app.World.State.production.miniGameToken, Is.Not.Null.And.Not.Empty);
            Assert.That(tutorial.CurrentStage, Is.EqualTo(FreeCafeMilkPitcherTutorial.Stage.DrawLatte));
            tutorial.Stop();
        }
    }
}
