using System;
using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public IEnumerator NewSessionWaitsForCompletedLetterBeforeFirstDayAndNextDayBypassesIt()
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
            yield return CompleteInteractiveLetter(panel);
            yield return WaitFor(() => panel.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.IntroLetter));
            panel.ConfirmSignature();
            yield return WaitFor(() => flow.State == DayFlowState.DateTransition);
            Assert.That(flow.BlocksGameplayInput, Is.True);
            Assert.That(flow.CurrentAvatarSelection, Is.Null);
            Assert.That(PlayerDetailsProfile.Current.HasSelectedAvatar, Is.True);
            Assert.That(PlayerDetailsProfile.Current.Avatar, Is.EqualTo(AvatarKind.Male));
            Assert.That(flow.State, Is.EqualTo(DayFlowState.DateTransition));
            Assert.That(flow.CurrentPresentation, Is.TypeOf<DateChangePanel>());
            flow.BeginNewDayFlow();
            Assert.That(flow.CurrentPresentation, Is.TypeOf<DateChangePanel>());
            flow.HideForMainMenu();
        }

        [UnityTest]
        public IEnumerator NewSessionSkipLetterUsesMaleAvatarWithoutSelectionWindow()
        {
            controllerObject = new GameObject("FirstDayLetterSkipFlowTest");
            var flow = controllerObject.AddComponent<DayFlowController>();
            flow.Initialize(null);
            flow.BeginNewSession();
            var panel = flow.CurrentPresentation as FirstDayLetterPanel;
            Assert.That(panel, Is.Not.Null);
            ((Button)Field("skipButton").GetValue(panel)).onClick.Invoke();
            panel.SkipLetter();
            Assert.That(flow.CurrentAvatarSelection, Is.Null);
            Assert.That(PlayerDetailsProfile.Current.Avatar, Is.EqualTo(AvatarKind.Male));
            Assert.That(PlayerDetailsProfile.Current.HasSelectedAvatar, Is.True);
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
            Assert.That(flow.CurrentDate, Is.EqualTo("王国纪元 670年10月7日"));
            flow.HideForMainMenu();
            yield return null;
        }

        [UnityTest]
        public IEnumerator InteractiveLetterPausesAtEveryBlankAndUsesInlineChoiceLinks()
        {
            var panel = Open();
            panel.Play(null);
            panel.OpenEnvelope();
            yield return WaitFor(() => panel.IsWaitingForInput);
            Assert.That(panel.CurrentChoiceGroup, Is.EqualTo(-1));
            Assert.That(((CanvasGroup)Field("nameEditor").GetValue(panel)).gameObject.activeSelf, Is.True);
            int visible = panel.VisibleCharacterCount;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(panel.VisibleCharacterCount, Is.EqualTo(visible));
            Assert.That(panel.SelectLetterOption(0, 0), Is.False, "Future blank cannot be filled early.");
            panel.ActivateLetterLink("choice:0:0");
            Assert.That(panel.Draft.Selection(0), Is.EqualTo(-1));
            panel.SetPlayerName("测试馆长");
            for (int group = 0; group < FirstDayLetterDraft.Labels.Length; group++)
            {
                yield return WaitFor(() => panel.CurrentChoiceGroup == group);
                var body = Body(panel);
                AssertInlineChoices(panel, group);
                Assert.That(((CanvasGroup)Field("nameEditor").GetValue(panel)).gameObject.activeSelf, Is.False);
                visible = panel.VisibleCharacterCount;
                string unread = UnreadSuffix(body);
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(panel.VisibleCharacterCount, Is.EqualTo(visible));
                Assert.That(panel.IsWaitingForInput, Is.True);
                panel.CancelLetterChoice();
                Assert.That(panel.CurrentChoiceGroup, Is.EqualTo(-1));
                AssertNoInlineChoices(body);
                Assert.That(UnreadSuffix(body), Is.EqualTo(unread), "Collapsing choices must not reveal later text.");
                visible = panel.VisibleCharacterCount;
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(panel.IsWaitingForInput, Is.True);
                Assert.That(panel.VisibleCharacterCount, Is.EqualTo(visible));
                panel.ActivateLetterLink($"choice:{group}:0");
                Assert.That(panel.Draft.Selection(group), Is.EqualTo(-1), "Collapsed choice links cannot select.");
                panel.ActivateLetterLink($"slot:{group}");
                AssertInlineChoices(panel, group);
                Assert.That(UnreadSuffix(body), Is.EqualTo(unread));
                panel.ActivateLetterLink($"slot:{group}");
                Assert.That(panel.CurrentChoiceGroup, Is.EqualTo(-1), "Clicking the same slot again collapses its inline choices.");
                Assert.That(panel.IsWaitingForInput, Is.True);
                AssertNoInlineChoices(body);
                Assert.That(UnreadSuffix(body), Is.EqualTo(unread));
                panel.ActivateLetterLink($"slot:{group}");
                AssertInlineChoices(panel, group);
                ClickChoiceEffect(panel, group, 0);
                Assert.That(panel.Draft.Selection(group), Is.Zero);
                Assert.That(panel.CurrentChoiceGroup, Is.EqualTo(-1));
                Assert.That(panel.IsWaitingForInput, Is.False);
                AssertNoInlineChoices(body);
                Assert.That(UnreadSuffix(body), Is.EqualTo(unread), "Selection must resume at the same unread suffix.");
                StringAssert.DoesNotContain(FirstDayLetterDraft.Effects[group][0], body.text);
                if (group == 0)
                {
                    // Revising to a different-length name preserves the exact unread suffix.
                    panel.ActivateLetterLink("name");
                    visible = panel.VisibleCharacterCount;
                    panel.SetPlayerName("米");
                    Assert.That(panel.VisibleCharacterCount, Is.EqualTo(visible - 3));
                    Assert.That(UnreadSuffix(body), Is.EqualTo(unread));
                }
            }
            yield return WaitFor(() => panel.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature);
            panel.ActivateLetterLink("slot:3");
            AssertInlineChoices(panel, 3);
            Assert.That(((Button)Field("signatureButton").GetValue(panel)).interactable, Is.False);
            panel.ConfirmSignature();
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.AwaitingSignature));
            panel.CancelLetterChoice();
            AssertNoInlineChoices(Body(panel));
            Assert.That(((Button)Field("signatureButton").GetValue(panel)).interactable, Is.True);
            panel.ActivateLetterLink("slot:3");
            ClickChoiceEffect(panel, 3, 2);
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.AwaitingSignature));
            Assert.That(panel.Draft.Selection(3), Is.EqualTo(2));
            Assert.That(RequireLink(Body(panel), "slot:3").GetLinkText(), Is.EqualTo("数学与物理科学"));
            StringAssert.DoesNotContain("神学", Body(panel).text);
            AssertNoInlineChoices(Body(panel));
            Assert.That(((Button)Field("signatureButton").GetValue(panel)).interactable, Is.True);
            panel.ActivateLetterLink("slot:3");
            panel.Stop();
            Assert.That(panel.CurrentChoiceGroup, Is.EqualTo(-1));
            AssertNoInlineChoices(Body(panel));
            Assert.That(panel.IsWaitingForInput, Is.False);
        }

        [UnityTest]
        public IEnumerator RevisingEarlierInlineChoicePreservesUnreadLaterText()
        {
            var panel = Open();
            panel.Play(null);
            panel.OpenEnvelope();
            yield return WaitFor(() => panel.IsWaitingForInput);
            panel.SetPlayerName("测试馆长");
            for (int group = 0; group < FirstDayLetterDraft.Labels.Length; group++)
            {
                int current = group;
                yield return WaitFor(() => panel.CurrentChoiceGroup == current);
                if (group == FirstDayLetterDraft.Labels.Length - 1)
                    Field("characterInterval").SetValue(panel, .5f);
                panel.ActivateLetterLink($"choice:{group}:0");
                Assert.That(panel.Draft.Selection(group), Is.Zero);
            }
            int visible = panel.VisibleCharacterCount;
            yield return WaitFor(() => panel.VisibleCharacterCount > visible);
            var body = Body(panel);
            string unread = UnreadSuffix(body);
            Assert.That(unread, Is.Not.Empty);
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Reading));
            panel.ActivateLetterLink("slot:3");
            AssertInlineChoices(panel, 3);
            Assert.That(UnreadSuffix(body), Is.EqualTo(unread));
            visible = panel.VisibleCharacterCount;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(panel.VisibleCharacterCount, Is.EqualTo(visible), "Reading pauses while revising an earlier word.");
            panel.CancelLetterChoice();
            Assert.That(panel.IsWaitingForInput, Is.False, "A filled slot must not become an unfilled wait.");
            AssertNoInlineChoices(body);
            Assert.That(UnreadSuffix(body), Is.EqualTo(unread));
            panel.ActivateLetterLink("slot:3");
            panel.ActivateLetterLink("choice:3:2");
            Assert.That(panel.Draft.Selection(3), Is.EqualTo(2));
            Assert.That(panel.CurrentChoiceGroup, Is.EqualTo(-1));
            AssertNoInlineChoices(body);
            Assert.That(UnreadSuffix(body), Is.EqualTo(unread), "A longer selected word must not replay or skip later text.");
            Assert.That(RequireLink(body, "slot:3").GetLinkText(), Is.EqualTo("数学与物理科学"));
            StringAssert.DoesNotContain("神学", body.text);
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Reading));
            visible = panel.VisibleCharacterCount;
            yield return WaitFor(() => panel.VisibleCharacterCount > visible);
        }

        private static TMP_Text Body(FirstDayLetterPanel panel) => (TMP_Text)Field("letterBody").GetValue(panel);

        private static TMP_LinkInfo RequireLink(TMP_Text body, string id)
        {
            body.ForceMeshUpdate();
            for (int i = 0; i < body.textInfo.linkCount; i++)
                if (body.textInfo.linkInfo[i].GetLinkID() == id) return body.textInfo.linkInfo[i];
            Assert.Fail($"Missing body TMP link: {id}");
            return default;
        }

        private static void AssertInlineChoices(FirstDayLetterPanel panel, int group)
        {
            Assert.That(panel.CurrentChoiceGroup, Is.EqualTo(group));
            var body = Body(panel);
            int count = 0;
            body.ForceMeshUpdate();
            for (int i = 0; i < body.textInfo.linkCount; i++)
                if (body.textInfo.linkInfo[i].GetLinkID().StartsWith("choice:")) count++;
            Assert.That(count, Is.EqualTo(FirstDayLetterDraft.Labels[group].Length), "Only the expanded group has choices.");
            for (int option = 0; option < FirstDayLetterDraft.Labels[group].Length; option++)
            {
                TMP_LinkInfo link = RequireLink(body, $"choice:{group}:{option}");
                Assert.That(link.GetLinkText(), Is.EqualTo(FirstDayLetterDraft.Labels[group][option]
                    + "【" + FirstDayLetterDraft.Effects[group][option] + "】"));
                Assert.That(link.linkTextfirstCharacterIndex + link.linkTextLength,
                    Is.LessThanOrEqualTo(panel.VisibleCharacterCount), "The effect itself must be clickable, not hidden by typewriter progress.");
            }
        }

        private static void AssertNoInlineChoices(TMP_Text body)
        {
            body.ForceMeshUpdate();
            for (int i = 0; i < body.textInfo.linkCount; i++)
                Assert.That(body.textInfo.linkInfo[i].GetLinkID().StartsWith("choice:"), Is.False);
            foreach (string[] group in FirstDayLetterDraft.Effects)
                foreach (string effect in group) StringAssert.DoesNotContain(effect, body.text);
        }

        private static string UnreadSuffix(TMP_Text body)
        {
            body.ForceMeshUpdate();
            var suffix = new StringBuilder();
            for (int i = Mathf.Min(body.maxVisibleCharacters, body.textInfo.characterCount); i < body.textInfo.characterCount; i++)
                suffix.Append(body.textInfo.characterInfo[i].character);
            return suffix.ToString();
        }

        private static void ClickChoiceEffect(FirstDayLetterPanel panel, int group, int option)
        {
            var body = Body(panel);
            TMP_LinkInfo link = RequireLink(body, $"choice:{group}:{option}");
            int effectIndex = link.linkTextfirstCharacterIndex + FirstDayLetterDraft.Labels[group][option].Length + 1;
            TMP_CharacterInfo character = body.textInfo.characterInfo[effectIndex];
            Assert.That(character.isVisible, Is.True);
            var raycaster = panel.GetComponent<GraphicRaycaster>();
            Vector3 world = body.transform.TransformPoint((character.bottomLeft + character.topRight) * .5f);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, world),
                pointerPressRaycast = new RaycastResult { module = raycaster }
            };
            int hit = TMP_TextUtilities.FindIntersectingLink(body, pointer.position, pointer.pressEventCamera);
            Assert.That(hit, Is.GreaterThanOrEqualTo(0), "Effect glyph must hit a TMP link.");
            Assert.That(body.textInfo.linkInfo[hit].GetLinkID(), Is.EqualTo($"choice:{group}:{option}"));
            body.GetComponent<FirstDayLetterLinks>().OnPointerClick(pointer);
        }

        private static IEnumerator CompleteInteractiveLetter(FirstDayLetterPanel panel)
        {
            yield return WaitFor(() => panel.IsWaitingForInput);
            panel.SetPlayerName("测试馆长");
            for (int i = 0; i < 5; i++)
            {
                int group = i;
                yield return WaitFor(() => panel.CurrentChoiceGroup == group);
                panel.ActivateLetterLink($"choice:{group}:0");
                Assert.That(panel.Draft.Selection(group), Is.Zero);
            }
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
