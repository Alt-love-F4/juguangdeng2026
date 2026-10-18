using System.Collections;
using System.Linq;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeEquipmentTutorialPromptPlayModeTests
    {
        private FreeCafeRuntime app;
        private FreeCafeEquipmentTutorialPrompt prompt;
        private CoffeeCompendiumPanel book;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return null;
            yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            Assert.That(app, Is.Not.Null);
            Assert.That(app.IsSandboxMode, Is.True, "Direct scene entry supplies the sandbox fixture.");
            app.areaManager.panSmoothTime = 0f;
            prompt = app.GetComponent<FreeCafeEquipmentTutorialPrompt>();
            Assert.That(prompt, Is.Not.Null, "Runtime must initialize the purchase prompt itself.");
            app.World.ConfigureEquipmentProgression(false);
            app.World.State.open = false;
            app.World.State.successfulOrders = CafeWorld.LatteOrdersRequired;
            app.World.State.money = 1000;
            yield return null;
            Assert.That(app.World.PrecisionEquipmentAvailable, Is.False);
            Assert.That(app.World.LatteEquipmentAvailable, Is.False);
            Assert.That(prompt.IsOpen, Is.False);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (book != null) book.CloseImmediate();
            if (app != null)
            {
                if (app.MilkPitcherTutorial != null) app.MilkPitcherTutorial.Stop();
                if (app.WorkstationTutorial != null) app.WorkstationTutorial.Stop();
            }
            Time.timeScale = 1;
            yield return null;
        }

        private IEnumerator WaitForPrompt()
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!prompt.IsOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(prompt.IsOpen, Is.True, "A successful acquisition should offer optional guidance.");
            Assert.That(app.InputBlocked, Is.True);
            Assert.That(app.BusinessSimulationPaused, Is.True);
        }

        private void Click(string name)
        {
            var canvas = GameObject.Find("EquipmentTutorialPromptCanvas");
            Assert.That(canvas, Is.Not.Null);
            var target = canvas.transform.Find("QuestionShade/QuestionCard/" + name);
            Assert.That(target, Is.Not.Null);
            var button = target.GetComponent<Button>();
            Assert.That(button.isActiveAndEnabled && button.interactable, Is.True);
            button.onClick.Invoke();
        }

        private IEnumerator AssertDismissed()
        {
            for (int i = 0; i < 5; i++)
            {
                yield return null;
                Assert.That(prompt.IsOpen, Is.False, "A consumed purchase must not offer the tutorial again.");
            }
            Assert.That(app.PracticeTutorialActive, Is.False);
            Assert.That(app.InputBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator BothEquipmentPurchasesOfferLaterWithoutRepeating()
        {
            Assert.That(app.World.ActivateEquipment(false).success, Is.True);
            yield return WaitForPrompt();
            Click("Later");
            Assert.That(prompt.IsOpen, Is.False);
            yield return AssertDismissed();

            Assert.That(app.World.ActivateEquipment(true).success, Is.True);
            yield return WaitForPrompt();
            Click("Later");
            Assert.That(prompt.IsOpen, Is.False);
            yield return AssertDismissed();
            Assert.That(app.World.PrecisionEquipmentAvailable, Is.True);
            Assert.That(app.World.LatteEquipmentAvailable, Is.True);
            Assert.That(app.World.State.money,
                Is.EqualTo(1000 - CafeWorld.PrecisionActivationCost - CafeWorld.LatteActivationCost));
        }

        [UnityTest]
        public IEnumerator NextLattePurchaseLearnNowEntersCounterAndStopRestoresProduction()
        {
            Assert.That(app.World.State.currentArea, Is.EqualTo(AreaId.Cafe));
            Assert.That(app.World.ActivateEquipment(false).success, Is.True);
            yield return WaitForPrompt();
            Click("Later");
            yield return AssertDismissed();

            Assert.That(app.World.ActivateEquipment(true).success, Is.True);
            yield return WaitForPrompt();
            var entitiesBefore = app.World.State.entities.OrderBy(entity => entity.id).Select(entity => JsonUtility.ToJson(entity)).ToArray();
            string activeCupBefore = app.World.State.production.activeCupId;
            bool automaticBefore = app.World.State.automaticMachines;
            int moneyBefore = app.World.State.money;
            Click("LearnNow");

            Assert.That(prompt.IsOpen, Is.False);
            Assert.That(app.World.State.currentArea, Is.EqualTo(AreaId.Counter));
            Assert.That(app.MilkPitcherTutorial, Is.Not.Null);
            Assert.That(app.MilkPitcherTutorial.IsRunning, Is.True);
            Assert.That(app.PracticeTutorialActive, Is.True);
            Assert.That(app.World.ServingCup, Is.Not.Null);
            app.MilkPitcherTutorial.Stop();
            Assert.That(app.MilkPitcherTutorial.IsRunning, Is.False);
            CollectionAssert.AreEqual(entitiesBefore,
                app.World.State.entities.OrderBy(entity => entity.id).Select(entity => JsonUtility.ToJson(entity)).ToArray());
            Assert.That(app.World.State.production.activeCupId, Is.EqualTo(activeCupBefore));
            Assert.That(app.World.State.automaticMachines, Is.EqualTo(automaticBefore));
            Assert.That(app.World.State.money, Is.EqualTo(moneyBefore));
            Assert.That(app.World.LatteEquipmentAvailable, Is.True);
            yield return AssertDismissed();
        }

        [UnityTest]
        public IEnumerator ProductionBookQueuesPurchaseUntilClosed()
        {
            app.Enter(AreaId.Counter);
            yield return null;
            var production = app.GetComponent<FreeCafeProductionHud>();
            Assert.That(production, Is.Not.Null);
            book = production.bookPanel.GetComponent<CoffeeCompendiumPanel>();
            Assert.That(book, Is.Not.Null);
            production.bookButton.onClick.Invoke();
            yield return null;
            Assert.That(book.IsOpen, Is.True);
            Assert.That(app.InputBlocked, Is.True);
            Assert.That(app.World.ActivateEquipment(false).success, Is.True);
            for (int i = 0; i < 5; i++)
            {
                yield return null;
                Assert.That(prompt.IsOpen, Is.False, "The purchase must not interrupt the open book.");
            }
            book.CloseImmediate();
            yield return WaitForPrompt();
            Click("Later");
            yield return AssertDismissed();
        }
    }
}
