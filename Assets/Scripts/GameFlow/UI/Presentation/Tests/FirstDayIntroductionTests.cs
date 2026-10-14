using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class FirstDayIntroductionTests
    {
        private GameObject controllerObject;
        private GameObject endpointObject;
        private DayFlowController flow;
        private DayFlowTestEndpoint endpoint;
        private float oldTimeScale;
        private PlayerPresentationTestSession session;

        [SetUp]
        public void Setup()
        {
            oldTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            GameSaveParticipantRegistry.PendingRestore = null;
            session = new PlayerPresentationTestSession();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (flow != null) flow.HideForMainMenu();
            if (controllerObject != null) Object.Destroy(controllerObject);
            if (endpointObject != null) Object.Destroy(endpointObject);
            DialoguePlayback.Stop();
            Time.timeScale = oldTimeScale;
            GameSaveParticipantRegistry.PendingRestore = null;
            yield return null;
            session.Dispose();
            session = null;
        }

        [Test]
        public void ProductionOpeningLoadsLubanScriptAndPreviewRemainsSeparate()
        {
            DialogueScript script = new LubanOpeningDialogueSource().CreateScript();
            Assert.That(script.Count, Is.EqualTo(13));
            Assert.That(script.RightCharacter.DisplayName, Is.EqualTo("阿黛尔"));
            Assert.That(script.RightCharacter.Portrait, Is.Not.Null);
            Assert.That(script.LeftCharacter.DisplayName, Is.EqualTo("你"));
            Assert.That(script.LeftCharacter.Portrait, Is.Null);
            StringAssert.Contains("我是阿黛尔", script[1].Text);
            Assert.That(script[1].Gesture, Is.EqualTo(DialogueGesture.Nod));
            Assert.That(script[8].Gesture, Is.EqualTo(DialogueGesture.Nod));
            Assert.That(DialoguePreviewDialogue.Create().Count, Is.EqualTo(7));
            Assert.That(script.Title, Is.EqualTo("初来乍到 · 沸点咖啡馆"));
            Assert.That(new LubanOpeningDialogueSource().CreateScript().Count, Is.EqualTo(13));
            Assert.That(typeof(DialogueVisualsAsset).GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Length, Is.EqualTo(3));
            Assert.That((int)DayFlowState.FreeCafeBusiness, Is.EqualTo(3));
            Assert.That((int)DayFlowState.IntroLetter, Is.EqualTo(9));
            Assert.That((int)DayFlowState.FirstDayIntroduction, Is.EqualTo(10));
        }

        [Test]
        public void LubanOpeningSourceLoadsAllConfiguredRowsAndVisuals()
        {
            DialogueScript script = new LubanOpeningDialogueSource().CreateScript();
            Assert.That(script.Count, Is.EqualTo(13));
            Assert.That(script.Title, Is.EqualTo("初来乍到 · 沸点咖啡馆"));
            Assert.That(script.LeftCharacter.DisplayName, Is.EqualTo("你"));
            Assert.That(script.RightCharacter.DisplayName, Is.EqualTo("阿黛尔"));
            Assert.That(script[0].Speaker, Is.EqualTo(DialogueSpeaker.Narrator));
            Assert.That(script[12].Speaker, Is.EqualTo(DialogueSpeaker.Right));
        }

        [UnityTest]
        public IEnumerator LetterDatePreparationIntroductionThenBusinessAndNoReplay()
        {
            CreateFlow();
            flow.BeginNewSession();
            var letter = (FirstDayLetterPanel)flow.CurrentPresentation;
            session.BindLetter(letter);
            SetField(letter, "characterInterval", .005f);
            SetField(letter, "openSeconds", .01f);
            SetField(letter, "closeSeconds", .01f);
            letter.OpenEnvelope();
            yield return Until(() => letter.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature);
            letter.SetPlayerName("测试馆长");
            for (int group = 0; group < 5; group++) letter.SelectLetterOption(group, 0);
            letter.ConfirmSignature();
            yield return Until(() => flow.State == DayFlowState.DateTransition);
            AssertDefaultAvatar();
            Assert.That(flow.State, Is.EqualTo(DayFlowState.DateTransition));
            yield return ReachIntroduction();
            Assert.That(flow.CurrentDate, Is.EqualTo("王国纪元 670年10月6日"));
            Assert.That(flow.BlocksGameplayInput, Is.True);
            Assert.That(endpoint.starts, Is.Zero);
            Assert.That(flow.CurrentDialogue.LeftPortrait.transform.parent.GetComponent<Image>().enabled, Is.False);
            yield return FinishDialogue();
            Assert.That(endpoint.starts, Is.EqualTo(1));
            Assert.That(flow.BlocksGameplayInput, Is.False);
            StringAssert.Contains("\"firstDayIntroductionCompleted\":true", flow.CaptureJson());
            flow.BeginNewDayFlow();
            yield return ReachBusinessWithoutIntroduction();
            Assert.That(flow.CurrentDialogue, Is.Null);
        }

        [UnityTest]
        public IEnumerator InjectedSourceCanReplaceHardcodedStoryForFutureLubanAdapter()
        {
            CreateFlow();
            var source = new ShortSource();
            flow.SetFirstDayIntroductionSource(source);
            Assert.That(session.Submit(null), Is.True);
            flow.BeginNewSession(playLetter: false);
            AssertDefaultAvatar();
            yield return ReachIntroduction();
            Assert.That(source.Reads, Is.EqualTo(1));
            Assert.That(DialogueBinding<TMPro.TMP_Text>(flow.CurrentDialogue, "dialogueText").text, Is.EqualTo("测试数据源台词"));
            yield return FinishDialogue();
            Assert.That(endpoint.starts, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FirstDayCafeTutorialCannotBeCancelledAndBlocksSavingUntilCompleted()
        {
            CreateFlow(new ShortSource());
            Assert.That(session.Submit(null), Is.True);
            flow.BeginNewSession(playLetter: false);
            Assert.That(PlayerRuntime.Current.HasSelectedAvatar, Is.True);
            Assert.That(PlayerRuntime.Current.Avatar, Is.EqualTo(PlayerRuntime.Current.Catalog.DefaultAvatar));
            Assert.That(flow.State, Is.EqualTo(DayFlowState.CityMapLeisure));
            Assert.That(flow.IsFirstDayMapGuideActive, Is.True);
            Assert.That(flow.CanSave, Is.True);

            flow.StartCafeFromMap();
            Assert.That(flow.IsFirstDayMapGuideActive, Is.False);
            Assert.That(flow.IsCafeTutorialActive, Is.True);
            Assert.That(flow.CanSave, Is.False);
            yield return SubmitPage();
            yield return Until(() => flow.State == DayFlowState.FirstDayIntroduction);

            DialoguePanel dialogue = flow.CurrentDialogue;
            Assert.That(dialogue.AllowCancel, Is.False);
            dialogue.Cancel();
            Assert.That(dialogue.IsPlaying, Is.True);
            Assert.That(flow.IsCafeTutorialActive, Is.True);

            yield return FinishDialogue();
            Assert.That(flow.IsCafeTutorialActive, Is.True);
            Assert.That(flow.CanSave, Is.False);
            flow.CompleteFirstDayCafeTutorial();
            Assert.That(flow.IsCafeTutorialActive, Is.False);
            Assert.That(flow.CanSave, Is.True);
            Assert.That(endpoint.starts, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReturningToTitleSilencesPendingDialogueCallbacks()
        {
            CreateFlow(new ShortSource());
            Assert.That(session.Submit(null), Is.True);
            flow.BeginNewSession(playLetter: false);
            AssertDefaultAvatar();
            yield return ReachIntroduction();
            flow.HideForMainMenu();
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(endpoint.starts, Is.Zero);
            Assert.That(flow.CurrentDialogue, Is.Null);
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator IntroSaveReplaysButCompletedSaveAndSecondDayDoNot()
        {
            CreateFlow(new ShortSource());
            Assert.That(session.Submit(null), Is.True);
            flow.BeginNewSession(playLetter: false);
            AssertDefaultAvatar();
            yield return ReachIntroduction();
            string save = flow.CaptureJson();
            flow.HideForMainMenu();
            Restore(save);
            Assert.That(flow.CurrentDialogue.LineIndex, Is.Zero);
            yield return FinishDialogue();
            save = flow.CaptureJson();
            flow.HideForMainMenu();
            Restore(save);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.FreeCafeBusiness));
            Assert.That(flow.CurrentDialogue, Is.Null);
            Assert.That(endpoint.starts, Is.EqualTo(1));
            flow.HideForMainMenu();
            Restore("{\"day\":2,\"state\":2}");
            yield return SubmitPage();
            yield return Until(() => flow.State == DayFlowState.FreeCafeBusiness);
            Assert.That(flow.CurrentDialogue, Is.Null);
        }

        [UnityTest]
        public IEnumerator LegacyBusinessSaveDoesNotReplayIntroduction()
        {
            CreateFlow(new ShortSource());
            Restore("{\"day\":1,\"state\":3}");
            StringAssert.Contains("\"firstDayIntroductionCompleted\":true", flow.CaptureJson());
            flow.BeginNewDayFlow();
            yield return ReachBusinessWithoutIntroduction();
            Assert.That(flow.CurrentDialogue, Is.Null);
        }

        [UnityTest]
        public IEnumerator MissingLegacyDayFlowDataIsNotANewGame()
        {
            CreateFlow(new ShortSource());
            Assert.That(session.Submit(null), Is.True);
            flow.RestoreSavedDayFlow();
            yield return ReachBusinessWithoutIntroduction();
            Assert.That(flow.CurrentDialogue, Is.Null);
        }

        [UnityTest]
        public IEnumerator FailingInjectedSourceCanRetryUsingDefaultHardcodedScript()
        {
            CreateFlow(new ThrowingSource());
            Assert.That(session.Submit(null), Is.True);
            flow.BeginNewSession(playLetter: false);
            AssertDefaultAvatar();
            yield return EnterCafeTransition();
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException: Test source failure"));
            yield return SubmitPage();
            yield return Until(() => flow.State == DayFlowState.PreBusiness);
            Assert.That(endpoint.starts, Is.Zero);
            flow.SetFirstDayIntroductionSource(null);
            yield return ReachIntroduction();
            yield return FinishDialogue();
            Assert.That(endpoint.starts, Is.EqualTo(1));
        }

        private void AssertDefaultAvatar()
        {
            Assert.That(flow.State, Is.EqualTo(DayFlowState.DateTransition));
            Assert.That(flow.BlocksGameplayInput, Is.True);
            Assert.That(flow.CurrentAvatarSelection, Is.Null);
            Assert.That(PlayerRuntime.Current.HasSelectedAvatar, Is.True);
            Assert.That(PlayerRuntime.Current.Avatar, Is.EqualTo(PlayerRuntime.Current.Catalog.DefaultAvatar));
        }

        private void CreateFlow(IDialogueScriptSource source = null)
        {
            controllerObject = new GameObject("FirstDayIntroductionFlowTest");
            flow = controllerObject.AddComponent<DayFlowController>();
            flow.Initialize(null);
            session.Bind(flow);
            flow.SetFirstDayIntroductionSource(source);
            endpointObject = new GameObject("FirstDayIntroductionEndpoint");
            endpoint = endpointObject.AddComponent<DayFlowTestEndpoint>();
        }
        private sealed class ShortSource : IDialogueScriptSource
        {
            public int Reads;
            public DialogueScript CreateScript()
            {
                Reads++;
                return new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Right, "测试数据源台词") });
            }
        }
        private sealed class ThrowingSource : IDialogueScriptSource
        {
            public DialogueScript CreateScript() => throw new InvalidOperationException("Test source failure");
        }
        private static void SetField(object instance, string name, object value)
            => instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, value);
        private void Restore(string json)
        {
            // Establish isolated owners; production save migration is verified separately.
            if (!PlayerRuntime.Current.CreationSubmitted) Assert.That(session.Submit(null), Is.True);
            GameSaveParticipantRegistry.PendingRestore = new List<GameSaveParticipantData>
            { new GameSaveParticipantData { Id = DayFlowController.SaveId, Json = json } };
            flow.RestoreSavedDayFlow();
        }
        private IEnumerator ReachIntroduction()
        {
            yield return EnterCafeTransition();
            yield return SubmitPage();
            yield return Until(() => flow.State == DayFlowState.FirstDayIntroduction);
        }
        private IEnumerator ReachBusinessWithoutIntroduction()
        {
            yield return EnterCafeTransition();
            yield return SubmitPage();
            yield return Until(() => flow.State == DayFlowState.FreeCafeBusiness);
        }
        private IEnumerator EnterCafeTransition()
        {
            yield return Until(() => flow.State == DayFlowState.CityMapLeisure || flow.State == DayFlowState.PreBusiness);
            if (flow.State == DayFlowState.CityMapLeisure) flow.StartCafeFromMap();
            else yield return SubmitPage(); // The explicit error-retry page remains supported.
            yield return Until(() => flow.State == DayFlowState.BusinessTransition);
            Assert.That(flow.CurrentPresentation, Is.TypeOf<CafeOpeningPanel>());
        }
        private IEnumerator SubmitPage()
        {
            var button = ((UIPanel)flow.CurrentPresentation).GetComponentInChildren<Button>(true);
            yield return Until(() => button.interactable);
            button.onClick.Invoke();
        }
        private IEnumerator FinishDialogue()
        {
            DialoguePanel panel = flow.CurrentDialogue;
            SetField(panel, "charactersPerSecond", 100000f);
            float deadline = Time.realtimeSinceStartup + 8f;
            var button = DialogueBinding<Button>(panel, "continueButton");
            while (panel.IsPlaying)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Dialogue did not finish.");
                if (button.interactable) panel.Advance();
                yield return null;
            }
            yield return Until(() => flow.State == DayFlowState.FreeCafeBusiness);
        }
        private static T DialogueBinding<T>(DialoguePanel panel, string field) where T : UnityEngine.Object
        {
            Assert.That(panel, Is.Not.Null);
            var binding = (T)typeof(DialoguePanel).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
            Assert.That(binding, Is.Not.Null, "Missing serialized dialogue binding: " + field);
            return binding;
        }
        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "First-day flow timed out.");
                yield return null;
            }
        }
    }
}
