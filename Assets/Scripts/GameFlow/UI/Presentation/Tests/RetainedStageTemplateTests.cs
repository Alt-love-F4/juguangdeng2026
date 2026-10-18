#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class RetainedStageTemplateTests
    {
        private const string Tag = "retained-stage-template-test";
        private Scene scene;
        private NarrativePresenter presenter;
        private object owner;
        private int originalPauseOwners;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalPauseOwners = PresentationPauseRegistry.OwnerCount;
            scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            presenter?.Dispose();
            presenter = null;
            DialoguePlayback.ReleaseStage(owner);
            UIKit.ClosePanelsByTag(Tag);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(originalPauseOwners));
        }

        [UnityTest]
        public IEnumerator TrailingClickWaitRendersAboveRetainedOpaqueDialogueAndReleasesAtIdle()
        {
            var config = new NarrativeConfiguration();
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "talk.wait", atomSeq = "A_DIALOG,A_WAIT" });
            config.setups.Add(new NarrativeSetupParamsDefinition
                { setupKey = "talk.wait.setup", tplName = "talk.wait", slotValues = "untilClick=true" });
            config.events.Add(new NarrativeEventDefinition
                { eventId = "talk.wait.event", presentationKey = "talk.wait.content", setupKey = "talk.wait.setup" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition
                { presentationKey = "talk.wait.content", presentationClass = "Conversation" });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = "talk.wait.page", presentationKey = "talk.wait.content", bgOverride = "street.day" });
            config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition
                { pageId = "talk.wait.page", lineId = "talk.wait.line", textKey = "尾随点击等待测试", seq = 1 });
            presenter = new NarrativePresenter(config);
            DialoguePanel panel = null;
            presenter.ConversationPlayback = (request, data, done) =>
            {
                owner = data.continuityOwner;
                bool handled = NarrativeConversationEventPlayback.Play(request, data, done, Tag, PanelCachePolicy.Transient);
                panel = UIKit.GetPanel<DialoguePanel>();
                return handled;
            };
            var controller = new NarrativeController(config, presenter);
            int completed = 0;
            controller.EventCompleted += id => completed++;
            Assert.That(controller.QueueEvent("talk.wait.event"), Is.True);
            float deadline = Time.realtimeSinceStartup + 8f;
            while (panel == null || panel.IsPlaying)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "The real dialogue bridge did not finish.");
                if (panel != null && panel.IsReady) panel.Advance();
                yield return null;
            }
            var ui = (NarrativePresentationUI)typeof(NarrativePresenter)
                .GetField("ui", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter);
            while (ui.GetComponent<Canvas>().sortingOrder != 810 || ui.GetComponent<CanvasGroup>().alpha == 0f)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Trailing wait did not become visible above the retained stage.");
                yield return null;
            }
            Assert.That(panel.SeamlessChoicePending, Is.True);
            Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(panel.BackgroundLayer.enabled, Is.True);
            Assert.That(panel.BackgroundLayer.color.a, Is.EqualTo(1f));
            Assert.That(ui.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(panel.GetComponent<Canvas>().sortingOrder));
            var blocker = ui.transform.Find("Blocker").GetComponent<Image>();
            Assert.That(blocker.color.a, Is.Zero, "The fallback input surface must not darken the retained CG.");
            Assert.That(blocker.raycastTarget, Is.True);
            Assert.That(ui.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            Assert.That(completed, Is.Zero, "Finishing dialogue alone must not bypass the trailing click wait.");
            Assert.That(controller.IsSafeBoundary, Is.False);
            yield return null;
            Assert.That(completed, Is.Zero);
            blocker.GetComponent<Button>().onClick.Invoke();
            while (!controller.IsSafeBoundary)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Click did not settle the wait and release the batch.");
                yield return null;
            }
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
            if (panel != null) Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(ui.GetComponent<CanvasGroup>().alpha, Is.Zero);
        }

        [Test]
        public void OrdinaryShowRestoresBaseSortingAndDimBlockerAfterRetainedStageMode()
        {
            var ui = NarrativePresentationUI.Create();
            try
            {
                ui.Show("retained", true);
                Assert.That(ui.GetComponent<Canvas>().sortingOrder, Is.EqualTo(810));
                Assert.That(ui.transform.Find("Blocker").GetComponent<Image>().color.a, Is.Zero);
                ui.Show("ordinary");
                Assert.That(ui.GetComponent<Canvas>().sortingOrder, Is.EqualTo(250));
                Assert.That(ui.transform.Find("Blocker").GetComponent<Image>().color.a, Is.EqualTo(.55f).Within(.001f));
            }
            finally { ui.Dispose(); }
        }
    }
}
#endif
