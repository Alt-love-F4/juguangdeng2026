using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeAmbientAdelePlayModeTests
    {
        private FreeCafeRuntime app;
        private FreeCafeAmbientAdele adele;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            DialoguePanel.Active?.Stop();
            Time.timeScale = 1;
            PlayerCafeOwner.EndSession();
            GameSaveParticipantRegistry.PendingRestore = null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop");
            yield return null;
            yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            app.areaManager.panSmoothTime = 0;
            app.Enter(AreaId.Cafe);
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            adele = Object.FindFirstObjectByType<FreeCafeAmbientAdele>();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            DialoguePanel.Active?.Stop();
            PlayerCafeOwner.EndSession();
            Time.timeScale = 1;
            yield return null;
        }

        [Test]
        public void OnlyExistingTwoPersonChatsAreUsedAndMissingConfigHasNoFallback()
        {
            var config = LubanNarrativeLoader.Load();
            for (int i = 0; i < 2; i++)
            {
                Assert.That(NarrativeCafeAmbientContent.TryCreateAdeleChat(config, i, out var script), Is.True);
                Assert.That(script.RightCharacter.CharacterKey, Is.EqualTo("adele"));
                Assert.That(script.RightCharacter.Portrait, Is.SameAs(Resources.Load<Sprite>("Characters/adele/neutral")));
                for (int line = 0; line < script.Count; line++)
                {
                    Assert.That(script[line].Speaker, Is.EqualTo(DialogueSpeaker.Left).Or.EqualTo(DialogueSpeaker.Right));
                    Assert.That(script[line].Text, Does.Not.Contain("{playername}"));
                }
            }
            Assert.That(NarrativeCafeAmbientContent.TryCreateAdeleChat(new NarrativeConfiguration(), 0, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator ClosedShopCompanionFadesAndChatsWithoutChangingDomainOrCreatingOrders()
        {
            Assert.That(adele, Is.Not.Null);
            Assert.That(app.World.State.open, Is.False);
            yield return new WaitForSeconds(.15f);
            Assert.That(adele.Visual.color.a, Is.GreaterThan(0).And.LessThan(1));
            yield return new WaitForSeconds(.5f);
            Assert.That(adele.Visual.color.a, Is.EqualTo(1).Within(.01));
            string before = app.CaptureJson();
            adele.OnPointerClick(new PointerEventData(EventSystem.current));
            Assert.That(DialoguePanel.IsAnyPlaying, Is.True);
            Assert.That(app.CaptureJson(), Is.EqualTo(before), "Ambient chat cannot create customer/order/visit facts or consume AP.");
            DialoguePanel.Active.Stop();
            Assert.That(app.World.StartDay().success, Is.True);
            yield return new WaitForSeconds(.15f);
            Assert.That(adele.Visual.color.a, Is.GreaterThan(0).And.LessThan(1));
            Assert.That(adele.GetComponent<BoxCollider2D>().enabled, Is.False);
            yield return new WaitForSeconds(.5f);
            Assert.That(adele.Visual.color.a, Is.Zero);
            Assert.That(app.World.State.cafeVisits.Exists(v => v.characterId == "adele"), Is.False);
        }
    }
}
