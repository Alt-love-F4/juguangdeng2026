using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class FirstDayLetterTests
    {
        private const string TestTag = "first-day-letter-tests";
        private GameObject controllerObject;
        private float oldTimeScale;

        [SetUp]
        public void Setup()
        {
            oldTimeScale = Time.timeScale;
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (controllerObject != null) Object.Destroy(controllerObject);
            UIKit.ClosePanelsByTag(TestTag);
            GameSaveParticipantRegistry.PendingRestore = null;
            Time.timeScale = oldTimeScale;
            yield return null;
        }

        [Test]
        public void PrefabContainsInterfaceAndBoundEditableLetter()
        {
            GameObject prefab = Resources.Load<GameObject>("Art/UIPrefab/FirstDayLetterPanel");
            Assert.That(prefab, Is.Not.Null);
            var panel = prefab.GetComponent<FirstDayLetterPanel>();
            Assert.That(panel, Is.InstanceOf<IDayFlowPresentation>());
            Assert.That(prefab.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(600));
            Assert.That(prefab.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<ScrollRect>(true), Is.Not.Null);
            foreach (string name in new[] { "rootGroup", "envelopeGroup", "envelope", "envelopeButton",
                "hintGroup", "paperGroup", "paper", "letterBody", "signatureText", "signatureButton",
                "skipButton", "readingHint", "letterScroll" })
                Assert.That(Field(name).GetValue(panel), Is.Not.Null, name);
            StringAssert.Contains("咖啡馆", (string)Field("letter").GetValue(panel));
        }

        [UnityTest]
        public IEnumerator WaitsForEnvelopeClickAndSignatureCannotFinishEarly()
        {
            var panel = Open();
            int completions = 0;
            panel.Play(() => completions++);
            panel.ConfirmSignature();
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Envelope));
            Assert.That(panel.VisibleCharacterCount, Is.Zero);
            Assert.That(completions, Is.Zero);
            Assert.That(panel.GetComponent<Canvas>().overrideSorting, Is.True);
        }

        [UnityTest]
        public IEnumerator SkipImmediatelyFinishesOnceFromAnyReadingStage()
        {
            var panel = Open();
            int completions = 0;
            panel.Play(() => completions++);
            Button skipButton = (Button)Field("skipButton").GetValue(panel);
            Assert.That(skipButton.isActiveAndEnabled, Is.True);
            Assert.That(skipButton.interactable, Is.True);
            skipButton.onClick.Invoke();
            panel.SkipLetter(); // duplicate input must not invoke the callback twice
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Finished));
            Assert.That(completions, Is.EqualTo(1));

            Field("characterInterval").SetValue(panel, .5f);
            panel.Play(() => completions++);
            panel.OpenEnvelope();
            yield return WaitFor(() => panel.Stage == FirstDayLetterPanel.LetterStage.Reading);
            panel.SkipLetter();
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Finished));
            Assert.That(completions, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator SkipDuringOpeningStopsMotionAndCannotResumeOrCompleteAgain()
        {
            var panel = Open();
            int completions = 0;
            panel.Play(() => completions++);
            panel.OpenEnvelope();
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Opening));
            panel.SkipLetter();
            RectTransform paper = (RectTransform)Field("paper").GetValue(panel);
            Vector2 skippedPosition = paper.anchoredPosition;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(paper.anchoredPosition, Is.EqualTo(skippedPosition));
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Finished));
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(((CanvasGroup)Field("rootGroup").GetValue(panel)).blocksRaycasts, Is.False);
            Assert.That(((Button)Field("skipButton").GetValue(panel)).interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator SkipDuringSignatureWaitAndFadeFinishesOnlyOnce()
        {
            var panel = Open();
            int completions = 0;
            panel.Play(() => completions++);
            panel.OpenEnvelope();
            yield return WaitFor(() => panel.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature);
            panel.SkipLetter();
            panel.ConfirmSignature();
            Assert.That(completions, Is.EqualTo(1));

            panel.Play(() => completions++);
            panel.OpenEnvelope();
            yield return WaitFor(() => panel.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature);
            panel.ConfirmSignature();
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Closing));
            panel.SkipLetter();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(completions, Is.EqualTo(2));
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Finished));
        }

        [UnityTest]
        public IEnumerator SkipWorksWhenPausedAndReplayReenablesButton()
        {
            var panel = Open();
            int completions = 0;
            panel.Play(() => completions++);
            Time.timeScale = 0;
            panel.SkipLetter();
            Assert.That(completions, Is.EqualTo(1));
            panel.Play(() => completions++);
            var skip = (Button)Field("skipButton").GetValue(panel);
            Assert.That(skip.interactable, Is.True);
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Envelope));
            skip.onClick.Invoke();
            yield return null;
            Assert.That(completions, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator SkipAfterStopOrCloseNeverInvokesNormalCompletion()
        {
            var panel = Open();
            int completions = 0;
            panel.Play(() => completions++);
            panel.Stop();
            panel.SkipLetter();
            Assert.That(completions, Is.Zero);
            panel.Play(() => completions++);
            UIKit.ClosePanel(panel);
            panel.SkipLetter();
            yield return null;
            Assert.That(completions, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PaperRisesFromBelowThenTypesBeforeFreshSignatureClick()
        {
            var panel = Open();
            Field("characterInterval").SetValue(panel, .05f);
            int completions = 0;
            panel.Play(() => completions++);
            RectTransform paper = (RectTransform)Field("paper").GetValue(panel);
            Vector2 restingPosition = paper.anchoredPosition;
            Button envelopeButton = (Button)Field("envelopeButton").GetValue(panel);
            envelopeButton.onClick.Invoke();
            panel.OpenEnvelope(); // second click must not restart the animation
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Opening));
            Assert.That(paper.anchoredPosition.y, Is.LessThan(restingPosition.y));
            Assert.That(panel.VisibleCharacterCount, Is.Zero);
            yield return WaitFor(() => panel.Stage == FirstDayLetterPanel.LetterStage.Reading);
            Assert.That(paper.anchoredPosition.y, Is.EqualTo(restingPosition.y).Within(.01f));
            panel.ConfirmSignature();
            Assert.That(completions, Is.Zero);
            yield return WaitFor(() => panel.VisibleCharacterCount > 0);
            yield return WaitFor(() => panel.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature);
            Assert.That(completions, Is.Zero);
            Button signatureButton = (Button)Field("signatureButton").GetValue(panel);
            Assert.That(signatureButton.isActiveAndEnabled, Is.True);
            signatureButton.onClick.Invoke();
            panel.ConfirmSignature();
            yield return WaitFor(() => !panel.IsPlaying);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Finished));
        }

        [UnityTest]
        public IEnumerator LetterWorksWhenScaledTimeIsPaused()
        {
            var panel = Open();
            Time.timeScale = 0;
            panel.Play(null);
            panel.OpenEnvelope();
            yield return WaitFor(() => panel.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature);
            panel.ConfirmSignature();
            yield return WaitFor(() => !panel.IsPlaying);
        }

        [UnityTest]
        public IEnumerator StopDuringOpeningDoesNotCompleteOrResumeTyping()
        {
            var panel = Open();
            int completions = 0;
            panel.Play(() => completions++);
            panel.OpenEnvelope();
            panel.Stop();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Stopped));
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(completions, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CloseDuringReadingCancelsCallback()
        {
            var panel = Open();
            // Keep the reading stage observable even when the editor has a slow frame.
            Field("characterInterval").SetValue(panel, .5f);
            int completions = 0;
            panel.Play(() => completions++);
            panel.OpenEnvelope();
            yield return WaitFor(() => panel.Stage == FirstDayLetterPanel.LetterStage.Reading);
            UIKit.ClosePanel(panel);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(completions, Is.Zero);
        }

        [UnityTest]
        public IEnumerator StopDuringClosingDoesNotAdvanceFlow()
        {
            var panel = Open();
            int completions = 0;
            panel.Play(() => completions++);
            panel.OpenEnvelope();
            yield return WaitFor(() => panel.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature);
            panel.ConfirmSignature();
            panel.Stop();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(completions, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReplayResetsPaperAndReplacesOldCallback()
        {
            var panel = Open();
            int oldCompletions = 0, newCompletions = 0;
            panel.Play(() => oldCompletions++);
            panel.OpenEnvelope();
            panel.Play(() => newCompletions++);
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Envelope));
            Assert.That(panel.VisibleCharacterCount, Is.Zero);
            panel.OpenEnvelope();
            yield return WaitFor(() => panel.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature);
            panel.ConfirmSignature();
            yield return WaitFor(() => !panel.IsPlaying);
            Assert.That(oldCompletions, Is.Zero);
            Assert.That(newCompletions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NewSessionWaitsForLetterBeforeFirstDayAndNextDayBypassesIt()
        {
            controllerObject = new GameObject("FirstDayLetterFlowTest");
            var flow = controllerObject.AddComponent<DayFlowController>();
            flow.Initialize(null);
            flow.BeginNewSession();
            var panel = flow.CurrentPresentation as FirstDayLetterPanel;
            Assert.That(panel, Is.Not.Null);
            SpeedUp(panel);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.IntroLetter));
            Assert.That(flow.CurrentDate, Is.EqualTo("王国纪元 670年10月6日"));
            Assert.That(flow.BlocksGameplayInput, Is.True);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.IntroLetter));
            panel.OpenEnvelope();
            yield return WaitFor(() => panel.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.IntroLetter));
            panel.ConfirmSignature();
            yield return WaitFor(() => flow.State == DayFlowState.DateTransition);
            Assert.That(flow.CurrentPresentation, Is.TypeOf<DateChangePanel>());
            flow.BeginNewDayFlow();
            Assert.That(flow.CurrentPresentation, Is.TypeOf<DateChangePanel>());
            flow.HideForMainMenu();
        }

        [UnityTest]
        public IEnumerator NewSessionSkipLetterImmediatelyEntersFirstDayDateFlow()
        {
            controllerObject = new GameObject("FirstDayLetterSkipFlowTest");
            var flow = controllerObject.AddComponent<DayFlowController>();
            flow.Initialize(null);
            flow.BeginNewSession();
            var panel = flow.CurrentPresentation as FirstDayLetterPanel;
            Assert.That(panel, Is.Not.Null);
            ((Button)Field("skipButton").GetValue(panel)).onClick.Invoke();
            panel.SkipLetter();
            yield return WaitFor(() => flow.State == DayFlowState.DateTransition);
            Assert.That(flow.CurrentPresentation, Is.TypeOf<DateChangePanel>());
            Assert.That(flow.CurrentDate, Is.EqualTo("王国纪元 670年10月6日"));
            Assert.That(flow.CurrentDialogue, Is.Null);
            StringAssert.Contains("\"firstDayIntroductionCompleted\":false", flow.CaptureJson());
            Assert.That(flow.BlocksGameplayInput, Is.True);
            flow.HideForMainMenu();
        }

        [UnityTest]
        public IEnumerator IntroSaveRestoresLetterButLaterPhaseNeverReplaysIntro()
        {
            controllerObject = new GameObject("FirstDayLetterRestoreTest");
            var flow = controllerObject.AddComponent<DayFlowController>();
            flow.Initialize(null);
            flow.BeginNewSession();
            string saved = flow.CaptureJson();
            flow.HideForMainMenu();
            flow.RestoreJson(saved);
            GameSaveParticipantRegistry.PendingRestore = new System.Collections.Generic.List<GameSaveParticipantData>
            {
                new GameSaveParticipantData { Id = DayFlowController.SaveId, Json = saved }
            };
            flow.RestoreSavedDayFlow();
            Assert.That(flow.CurrentPresentation, Is.TypeOf<FirstDayLetterPanel>());
            flow.HideForMainMenu();
            GameSaveParticipantRegistry.PendingRestore = new System.Collections.Generic.List<GameSaveParticipantData>
            {
                new GameSaveParticipantData { Id = DayFlowController.SaveId,
                    Json = "{\"day\":2,\"previousDate\":\"王国纪元 670年10月6日\",\"state\":1}" }
            };
            flow.RestoreSavedDayFlow();
            Assert.That(flow.CurrentPresentation, Is.TypeOf<PreBusinessPanel>());
            Assert.That(flow.CurrentDate, Is.EqualTo("王国纪元 670年10月7日"));
            flow.HideForMainMenu();
            yield return null;
        }

        private static FirstDayLetterPanel Open()
        {
            var panel = UIKit.OpenPanel<FirstDayLetterPanel>(UILevel.Pop, tag: TestTag,
                cachePolicy: PanelCachePolicy.Transient);
            SpeedUp(panel);
            return panel;
        }
        private static FieldInfo Field(string name)
            => typeof(FirstDayLetterPanel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        private static void SpeedUp(FirstDayLetterPanel panel)
        {
            Field("letter").SetValue(panel, "孩子，咖啡馆交给你。欢迎回家。");
            Field("characterInterval").SetValue(panel, .005f);
            Field("openSeconds").SetValue(panel, .08f);
            Field("closeSeconds").SetValue(panel, .08f);
        }
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Letter presentation timed out.");
                yield return null;
            }
        }
    }
}
