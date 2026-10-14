using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;
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
        private static readonly FirstDayLetterContent TestContent = new FirstDayLetterContent("孩子，咖啡馆交给你。欢迎回家。", FirstDayLetterDraft.Signature);
        private GameObject controllerObject;
        private float oldTimeScale;
        private PlayerPresentationTestSession session;

        [SetUp]
        public void Setup()
        {
            oldTimeScale = Time.timeScale;
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            session = new PlayerPresentationTestSession();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (controllerObject != null) Object.Destroy(controllerObject);
            UIKit.ClosePanelsByTag(TestTag);
            GameSaveParticipantRegistry.PendingRestore = null;
            Time.timeScale = oldTimeScale;
            yield return null; // Let panel/controller destruction cancel all callbacks before releasing the test session.
            session.Dispose();
            session = null;
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
            Assert.That(prefab.transform.Find("LetterPaper/LetterScroll/Viewport/Body").GetComponent<FirstDayLetterLinks>(), Is.Not.Null);
            Assert.That(Field("nameInput").GetValue(panel), Is.Not.Null);
            StringAssert.Contains("咖啡馆", new FirstDayLetterDraft().BuildBody());
        }

        [UnityTest]
        public IEnumerator WaitsForEnvelopeClickAndSignatureCannotFinishEarly()
        {
            var panel = Open();
            int completions = 0;
            panel.Play(TestContent, () => completions++);
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
            panel.Play(TestContent, () => completions++);
            Button skipButton = (Button)Field("skipButton").GetValue(panel);
            Assert.That(skipButton.isActiveAndEnabled, Is.True);
            Assert.That(skipButton.interactable, Is.True);
            skipButton.onClick.Invoke();
            panel.SkipLetter(); // duplicate input must not invoke the callback twice
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Finished));
            Assert.That(completions, Is.EqualTo(1));

            Field("characterInterval").SetValue(panel, .5f);
            panel.Play(TestContent, () => completions++);
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
            panel.Play(TestContent, () => completions++);
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
            panel.Play(TestContent, () => completions++);
            panel.OpenEnvelope();
            yield return WaitFor(() => panel.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature);
            panel.SkipLetter();
            panel.ConfirmSignature();
            Assert.That(completions, Is.EqualTo(1));

            panel.Play(TestContent, () => completions++);
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
            panel.Play(TestContent, () => completions++);
            Time.timeScale = 0;
            panel.SkipLetter();
            Assert.That(completions, Is.EqualTo(1));
            panel.Play(TestContent, () => completions++);
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
            panel.Play(TestContent, () => completions++);
            panel.Stop();
            panel.SkipLetter();
            Assert.That(completions, Is.Zero);
            panel.Play(TestContent, () => completions++);
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
            panel.Play(TestContent, () => completions++);
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
            panel.Play(TestContent, null);
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
            panel.Play(TestContent, () => completions++);
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
            panel.Play(TestContent, () => completions++);
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
            panel.Play(TestContent, () => completions++);
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
            panel.Play(TestContent, () => oldCompletions++);
            panel.OpenEnvelope();
            panel.Play(TestContent, () => newCompletions++);
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
        public IEnumerator NewSessionWaitsForLetterThenSelectsMaleAvatarAndEntersMap()
        {
            controllerObject = new GameObject("FirstDayLetterFlowTest");
            var flow = controllerObject.AddComponent<DayFlowController>();
            flow.Initialize(null);
            session.Bind(flow);
            flow.BeginNewSession();
            var panel = flow.CurrentPresentation as FirstDayLetterPanel;
            session.BindLetter(panel);
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
            panel.SetPlayerName("测试馆长");
            for (int i = 0; i < 5; i++) panel.SelectLetterOption(i, 0);
            panel.ConfirmSignature();
            yield return WaitFor(() => flow.State == DayFlowState.DateTransition);
            Assert.That(flow.BlocksGameplayInput, Is.True);
            Assert.That(PlayerRuntime.Current.HasSelectedAvatar, Is.True);
            Assert.That(flow.CurrentAvatarSelection, Is.Null);
            Assert.That(PlayerRuntime.Current.Avatar, Is.EqualTo(AvatarKind.Male));
            Assert.That(PlayerRuntime.Current.PlayerName, Is.EqualTo("测试馆长"));
            Assert.That(PlayerRuntime.Current.TryReadSpecialization("letter.religion", out bool specialized), Is.EqualTo(PlayerReadStatus.Success));
            Assert.That(specialized, Is.True);
            Assert.That(session.World.State.money, Is.EqualTo(250));
            Assert.That(PlayerRuntime.Current.CreationSubmitted, Is.True);
            Assert.That(PlayerRuntime.Current.TryReadAttribute("language", out int language), Is.EqualTo(PlayerReadStatus.Success));
            Assert.That(language, Is.EqualTo(5));
            Assert.That(session.Submit(panel.Draft), Is.True, "Replay is acknowledged without granting rewards again.");
            Assert.That(session.World.State.money, Is.EqualTo(250));
            PlayerRuntime.Current.TryReadAttribute("language", out language);
            Assert.That(language, Is.EqualTo(5));
            Assert.That(flow.CurrentPresentation, Is.TypeOf<DateChangePanel>());
            yield return WaitFor(() => flow.State == DayFlowState.CityMapLeisure);
            Assert.That(flow.CurrentPresentation, Is.Null);
            Assert.That(flow.CurrentAvatarSelection, Is.Null);
            Assert.That(flow.BlocksGameplayInput, Is.False);
            flow.HideForMainMenu();
        }

        [UnityTest]
        public IEnumerator NewSessionSkipLetterUsesMaleAvatarWithoutSelectionWindow()
        {
            controllerObject = new GameObject("FirstDayLetterSkipFlowTest");
            var flow = controllerObject.AddComponent<DayFlowController>();
            flow.Initialize(null);
            session.Bind(flow);
            flow.BeginNewSession();
            var panel = flow.CurrentPresentation as FirstDayLetterPanel;
            session.BindLetter(panel);
            Assert.That(panel, Is.Not.Null);
            ((Button)Field("skipButton").GetValue(panel)).onClick.Invoke();
            panel.SkipLetter();
            Assert.That(flow.CurrentAvatarSelection, Is.Null);
            Assert.That(session.World.State.money, Is.EqualTo(500));
            Assert.That(PlayerRuntime.Current.CreationSubmitted, Is.True);
            Assert.That(PlayerRuntime.Current.TryReadAttribute("language", out int language), Is.EqualTo(PlayerReadStatus.Success));
            Assert.That(language, Is.Zero);
            PlayerRuntime.Current.TryReadSpecialization("letter.religion", out bool specialized);
            Assert.That(specialized, Is.False);
            Assert.That(PlayerRuntime.Current.Avatar, Is.EqualTo(AvatarKind.Male));
            Assert.That(PlayerRuntime.Current.HasSelectedAvatar, Is.True);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.DateTransition));
            Assert.That(flow.CurrentPresentation, Is.TypeOf<DateChangePanel>());
            Assert.That(flow.CurrentDate, Is.EqualTo("王国纪元 670年10月6日"));
            Assert.That(flow.CurrentDialogue, Is.Null);
            StringAssert.Contains("\"firstDayIntroductionCompleted\":false", flow.CaptureJson());
            Assert.That(flow.BlocksGameplayInput, Is.True);
            flow.HideForMainMenu();
            yield break;
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
            Assert.That(flow.State, Is.EqualTo(DayFlowState.CityMapLeisure));
            Assert.That(flow.CurrentPresentation, Is.Null);
            Assert.That(flow.BlocksGameplayInput, Is.False);
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
            Field("characterInterval").SetValue(panel, .005f);
            Field("openSeconds").SetValue(panel, .08f);
            Field("closeSeconds").SetValue(panel, .08f);
        }
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Letter presentation timed out.");
                yield return null;
            }
        }
    }
}
