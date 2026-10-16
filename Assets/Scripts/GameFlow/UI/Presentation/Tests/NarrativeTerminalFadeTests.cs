#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativeTerminalFadeTests
    {
        private const string EventId = "fixture.terminal-fade";
        private const string Tag = "narrative-terminal-fade-tests";
        private const string LastLine = "The last line stays readable until the reader advances.";
        private Scene scene, previousScene;
        private bool ownsScene;
        private float previousTimeScale;
        private Func<bool> previousDialogueBlocker, previousPresenterBlocker;
        private int pauseOwners, completed, cancelled;
        private NarrativePresenter presenter;
        private NarrativeController controller;
        private DialoguePanel panel;
        private NarrativeConversationPresentationData conversation;
        private readonly List<bool> fadeResults = new List<bool>();
        private float alphaAtFadeCompletion;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Application.isPlaying, Is.True, "Run these tests in Play Mode.");
            previousScene = SceneManager.GetActiveScene();
            previousTimeScale = Time.timeScale;
            previousDialogueBlocker = DialoguePanel.ExternalInputBlocked;
            previousPresenterBlocker = NarrativePresenter.ExternalInputBlocked;
            pauseOwners = PresentationPauseRegistry.OwnerCount;
            completed = cancelled = 0;
            fadeResults.Clear();
            alphaAtFadeCompletion = -1f;
            scene = SceneManager.GetSceneByPath(PresentationDebugSceneController.ScenePath);
            ownsScene = !scene.IsValid() || !scene.isLoaded;
            if (ownsScene)
            {
                scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                    new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
            }
            Assert.That(scene.isLoaded, Is.True);
            SceneManager.SetActiveScene(scene);
            Time.timeScale = 1f;
            DialoguePanel.ExternalInputBlocked = null;
            NarrativePresenter.ExternalInputBlocked = null;

            var configuration = Configuration();
            presenter = new NarrativePresenter(configuration);
            presenter.ConversationPlayback = (request, data, done) =>
            {
                bool handled = NarrativeConversationEventPlayback.Play(request, data, done, Tag, PanelCachePolicy.Reusable);
                conversation = data;
                panel = UIKit.GetPanel<DialoguePanel>();
                var fade = data.fadeStage;
                data.fadeStage = (seconds, callback) => fade(seconds, success =>
                {
                    fadeResults.Add(success);
                    alphaAtFadeCompletion = panel.Effects.OverlayAlpha;
                    callback(success);
                });
                return handled;
            };
            presenter.Cancelled += _ => cancelled++;
            controller = new NarrativeController(configuration, presenter);
            controller.EventCompleted += _ => completed++;
            Assert.That(controller.QueueEvent(EventId), Is.True);
            Assert.That(panel, Is.Not.Null);
            yield return Wait(() => panel.IsReady, "Dialogue entrance");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            presenter?.Dispose();
            presenter = null;
            UIKit.ClosePanelsByTag(Tag);
            DialoguePanel.ExternalInputBlocked = previousDialogueBlocker;
            NarrativePresenter.ExternalInputBlocked = previousPresenterBlocker;
            DialogueHistoryService.ClearFormal();
            yield return null;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (ownsScene && scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = previousTimeScale;
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners));
        }

        [UnityTest]
        public IEnumerator StopDuringTerminalFadeCancelsWithoutSettlement()
        {
            yield return BeginFade();
            DialoguePlayback.Stop();
            DialoguePlayback.Stop();
            yield return AssertCancelled();
        }

        [UnityTest]
        public IEnumerator CloseDuringTerminalFadeCancelsWithoutSettlement()
        {
            yield return BeginFade();
            UIKit.ClosePanel(panel);
            yield return AssertCancelled();
        }

        [UnityTest]
        public IEnumerator DisableDuringTerminalFadeCancelsWithoutSettlement()
        {
            yield return BeginFade();
            panel.gameObject.SetActive(false);
            yield return AssertCancelled();
        }

        [UnityTest]
        public IEnumerator CancelledFadeHandleCancelsWithoutSettlement()
        {
            yield return BeginFade();
            panel.StopEffects();
            yield return AssertCancelled();
        }

        [UnityTest]
        public IEnumerator LastLineRemainsReadableAndSuccessSettlesOnceAfterBlack()
        {
            yield return BeginFade();
            panel.Effects.Tick(1f);
            Assert.That(panel.Effects.OverlayAlpha, Is.EqualTo(1f).Within(.001f));
            Assert.That(completed, Is.Zero, "Settlement must wait for the fade observer and black-frame boundary.");
            yield return Wait(() => !controller.IsBusy, "Successful terminal fade");
            Assert.That(fadeResults, Is.EqualTo(new[] { true }));
            Assert.That(alphaAtFadeCompletion, Is.EqualTo(1f).Within(.001f));
            Assert.That(presenter.IsPresenting, Is.False);
            Assert.That(cancelled, Is.Zero, "Normal release must detach before stopping the retained stage.");
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(controller.State.HasHappenedEvent(EventId), Is.True);
            AssertReward(1);
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners));
            DialoguePlayback.Stop();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(fadeResults, Is.EqualTo(new[] { true }));
            Assert.That(cancelled, Is.Zero);
            Assert.That(completed, Is.EqualTo(1));
            AssertReward(1);
        }

        [UnityTest]
        public IEnumerator FadeStartupExceptionFailsOnceAndDoesNotPoisonLaterFade()
        {
            var rejected = new List<bool>();
            Assert.Throws<InvalidOperationException>(() => conversation.fadeStage(.3f, rejected.Add),
                "A dialogue still being read has no retained stage to fade.");
            Assert.That(rejected, Is.EqualTo(new[] { false }));
            // The observation wrapper above also records the deliberately rejected attempt.
            fadeResults.Clear();
            yield return BeginFade();
            panel.Effects.Tick(1f);
            yield return Wait(() => !controller.IsBusy, "Fade after rejected startup");
            DialoguePlayback.Stop();
            yield return null;
            Assert.That(rejected, Is.EqualTo(new[] { false }));
            Assert.That(fadeResults, Is.EqualTo(new[] { true }));
            Assert.That(cancelled, Is.Zero);
            Assert.That(completed, Is.EqualTo(1));
            AssertReward(1);
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners));
        }

        private IEnumerator BeginFade()
        {
            // Reveal without finishing the page, then prove the final line can be read before fade starts.
            if (panel.IsTyping) panel.Advance();
            yield return null;
            TMP_Text body = (TMP_Text)typeof(DialoguePanel)
                .GetField("dialogueText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            body.ForceMeshUpdate();
            Assert.That(body.text, Is.EqualTo(LastLine));
            Assert.That(body.maxVisibleCharacters, Is.GreaterThanOrEqualTo(body.textInfo.characterCount));
            Assert.That(body.gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.001f));
            Assert.That(panel.IsPlaying, Is.True);
            Assert.That(panel.Effects.IsOverlayPlaying, Is.False);
            Assert.That(panel.Effects.OverlayAlpha, Is.Zero.Within(.001f));
            Assert.That(controller.IsBusy, Is.True);
            AssertUncommitted();
            yield return null;
            Assert.That(fadeResults, Is.Empty);
            panel.Advance();
            Assert.That(panel.SeamlessChoicePending, Is.True);
            Assert.That(panel.IsPlaying, Is.False);
            yield return Wait(() => panel.Effects.IsOverlayPlaying, "Terminal fade start");
            Assert.That(panel.EffectOverlay.gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.Effects.OverlayAlpha, Is.LessThan(1f));
            Assert.That(fadeResults, Is.Empty);
            AssertUncommitted();
        }

        private IEnumerator AssertCancelled()
        {
            yield return Wait(() => !controller.IsBusy, "Terminal fade cancellation");
            Assert.That(presenter.IsPresenting, Is.False);
            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(fadeResults, Is.EqualTo(new[] { false }));
            AssertUncommitted();
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners));
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(fadeResults, Is.EqualTo(new[] { false }));
            Assert.That(controller.IsBusy, Is.False);
            AssertUncommitted();
        }

        private void AssertUncommitted()
        {
            Assert.That(completed, Is.Zero);
            Assert.That(controller.State.HasHappenedEvent(EventId), Is.False);
            AssertReward(0);
        }

        private void AssertReward(int expected)
        {
            Assert.That(controller.State.TryGetBar("fixture.reward", out int reward), Is.True);
            Assert.That(reward, Is.EqualTo(expected));
        }

        private static IEnumerator Wait(Func<bool> condition, string operation)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), operation + " timed out.");
                yield return null;
            }
        }

        private static NarrativeConfiguration Configuration()
        {
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "fixture.reward", min = 0, max = 10 });
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_TwoPersonTalk", atomSeq = "A_DIALOG" });
            config.events.Add(new NarrativeEventDefinition { eventId = EventId, onceOnly = true, presentationKey = EventId });
            config.results.Add(new NarrativeResultDefinition { eventId = EventId,
                resultType = NarrativeResultType.BarAdd, target = "fixture.reward", value = "1" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition
                { presentationKey = EventId, presentationClass = "conversation" });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { presentationKey = EventId, pageId = "terminal.dialogue", seq = 1 });
            config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition
                { pageId = "terminal.dialogue", lineId = "terminal.last-line", seq = 1, textKey = LastLine });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { presentationKey = EventId, pageId = "terminal.fade", seq = 2, stepType = NarrativeConversationStepType.FadeOut });
            return config;
        }
    }
}
#endif
