using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class CafeCustomerDialoguePlayModeTests
    {
        private FreeCafeRuntime app;
        private NarrativeRuntime narrative;
        private bool addedNarrative;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            DialoguePanel.Active?.Stop();
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            PlayerCafeOwner.EndSession();
            yield return SceneManager.LoadSceneAsync("CoffeeShop");
            yield return null;
            yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            app.areaManager.panSmoothTime = 0;
            app.Enter(AreaId.Cafe);
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            narrative = NarrativeRuntime.Instance;
            addedNarrative = narrative == null;
            if (addedNarrative) narrative = new GameObject("Order dialogue test runtime").AddComponent<NarrativeRuntime>();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            DialoguePanel.Active?.Stop();
            if (addedNarrative && narrative != null) Object.Destroy(narrative.gameObject);
            PlayerCafeOwner.EndSession();
            Time.timeScale = 1;
            yield return null;
        }

        [Test]
        public void EveryAuthoredOrderSeparatesCustomerAndAdeleWithHerPortrait()
        {
            var config = narrative.Configuration;
            var person = new CafePersonIdentity { personId = "test", characterId = "guest_a", displayName = "普通客人" };
            int templates = 0;
            foreach (var presentation in config.openingPresentations)
            {
                if (!presentation.presentationId.StartsWith("cafe.ambient.order.")) continue;
                var rows = config.openingLines.FindAll(l => l.presentationId == presentation.presentationId);
                rows.Sort((a, b) => a.seq.CompareTo(b.seq));
                string text = string.Join("\n", rows.ConvertAll(l => narrative.ResolveText(l.textKey)));
                var script = CafeDialoguePlayback.CreateCustomerScript(person, text);
                Assert.That(script.Count, Is.EqualTo(2), presentation.presentationId);
                Assert.That(script[0].Speaker, Is.EqualTo(DialogueSpeaker.Right));
                Assert.That(script[0].Text, Does.Not.StartWith("顾客："));
                Assert.That(script[1].Speaker, Is.EqualTo(DialogueSpeaker.Left));
                Assert.That(script[1].LeftCharacter.CharacterKey, Is.EqualTo("adele"));
                Assert.That(script[1].LeftCharacter.Portrait, Is.SameAs(Resources.Load<Sprite>("Characters/adele/neutral")));
                Assert.That(script[1].Text, Does.Not.StartWith("阿黛尔："));
                templates++;
            }
            Assert.That(templates, Is.EqualTo(10));
        }

        [Test]
        public void AnonymousLegacyCustomerStillHasDialogueAndSilhouette()
        {
            var script = CafeDialoguePlayback.CreateCustomerScript(
                new CafePersonIdentity { personId = "legacy", displayName = "顾客" }, "顾客：请看看我的订单。");
            Assert.That(script, Is.Not.Null);
            Assert.That(script[0].RightCharacter.Portrait, Is.SameAs(Resources.Load<Sprite>("Characters/guest_a/neutral")));
        }

        [UnityTest]
        public IEnumerator SecondDayClickUsesDialogueAndPausesPatienceUntilClosed()
        {
            app.World.State.day = 2;
            Assert.That(app.World.StartDay().success, Is.True);
            yield return null;
            Assert.That(app.CafeTutorialActive, Is.False);
            var request = app.World.State.requests[0];
            var customer = app.World.Find(request.customerId);
            Assert.That(NarrativeCafeAmbientContent.TryCreateOrderDialogue(app.World, customer, out var script), Is.True);
            foreach (var lineIndex in new[] { 0, 1 })
                Assert.That(script[lineIndex].Text, Does.Not.Contain("{饮品名}"));
            app.Click(app.View(request.customerId));
            yield return null;
            Assert.That(DialoguePanel.IsAnyPlaying, Is.True);
            Assert.That(app.World.State.focusedRequestId, Is.EqualTo(request.id));
            Assert.That(app.BusinessSimulationPaused, Is.True);
            float patience = request.patience;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(request.patience, Is.EqualTo(patience));
            var panel = DialoguePanel.Active;
            float deadline = Time.realtimeSinceStartup + 5;
            while (panel.LineIndex == 0 && Time.realtimeSinceStartup < deadline)
            {
                panel.Advance();
                yield return new WaitForSecondsRealtime(.15f);
            }
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(panel.LineIndex, Is.EqualTo(1));
            Assert.That(panel.LeftCharacter.CharacterKey, Is.EqualTo("adele"));
            Assert.That(panel.LeftCharacter.Portrait, Is.SameAs(Resources.Load<Sprite>("Characters/adele/neutral")));
            Assert.That(panel.CenterCharacter, Is.Null, "No prefab-default character should force Adele into a third slot.");
            panel.Stop();
            yield return null;
            Assert.That(app.BusinessSimulationPaused, Is.False);
        }
    }
}
