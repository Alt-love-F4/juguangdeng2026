#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.Narrative.Tests
{
    /// <summary>Lifecycle tests with a controllable host, not a replacement coffee system.
    /// Loads the shared debug scene; never builds a temporary Scene/Canvas/InputSystem.</summary>
    public sealed class NarrativeGameplayPresenterTests
    {
        private Scene sharedScene, previousScene;
        private NarrativePresenter presenter;
        private NarrativeRuntime runtime;
        private IDisposable foreignPause;
        private sealed class Handle : IDisposable
        {
            public int Disposals;
            public void Dispose() { Disposals++; }
        }
        [UnitySetUp] public IEnumerator SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            sharedScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PresentationDebugScene.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            Assert.That(sharedScene.IsValid() && sharedScene.isLoaded);
            SceneManager.SetActiveScene(sharedScene);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            presenter?.Dispose(); presenter = null;
            if (runtime != null) UnityEngine.Object.Destroy(runtime.gameObject);
            runtime = null;
            foreignPause?.Dispose(); foreignPause = null;
            yield return null;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (sharedScene.IsValid() && sharedScene.isLoaded) yield return SceneManager.UnloadSceneAsync(sharedScene);
        }
        private static NarrativeConfiguration Config()
        {
            var c = new NarrativeConfiguration();
            c.events.Add(new NarrativeEventDefinition { eventId = "root", presentationKey = "story", blocksSave = true });
            c.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_TwoPersonTalk", atomSeq = "A_DIALOG" });
            c.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "story", presentationClass = "Conversation" });
            c.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "before", presentationKey = "story", seq = 1 });
            c.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "brew", presentationKey = "story", seq = 2, stepType = NarrativeConversationStepType.Gameplay, gameplayKey = "serve:black_coffee:60:1" });
            c.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "after", presentationKey = "story", seq = 3 });
            c.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "end", presentationKey = "story", seq = 4, stepType = NarrativeConversationStepType.End });
            c.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "wrongLongBranch", presentationKey = "story", seq = 5 });
            foreach (string page in new[] { "before", "after", "wrongLongBranch" })
                c.presentations.conversationLines.Add(new NarrativeConversationLineDefinition { lineId = page + ".line", pageId = page, seq = 1, textKey = page });
            return c;
        }
        [UnityTest] public IEnumerator GameplayYieldsOnlyItsPauseAndVisualsThenReacquiresBeforeNextPage()
        {
            int baseline = PresentationPauseRegistry.OwnerCount;
            foreignPause = PresentationPauseRegistry.Acquire("unrelated-test-owner");
            var seen = new List<string>(); int released = 0; Action<NarrativeGameplayResult> finish = null; var handle = new Handle();
            presenter = new NarrativePresenter(Config());
            presenter.ConversationPlayback = (request, data, done) =>
            {
                seen.Add(data.lines[0].pageId);
                Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(baseline + 2));
                data.releaseStage = () => released++;
                done(); return true;
            };
            presenter.GameplayPlayback = (request, data, done) =>
            {
                Assert.That(presenter.IsAwaitingGameplay); Assert.That(released, Is.EqualTo(1));
                Assert.That(data.pageId, Is.EqualTo("brew")); Assert.That(data.eventId, Is.EqualTo("root")); Assert.That(data.continuityOwner, Is.Not.Null);
                Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(baseline + 1), "Only the presenter lease should be released.");
                finish = done; return handle;
            };
            var c = new NarrativeController(Config(), presenter); presenter.PageCondition = c.ReadPresentationCondition;
            Assert.That(c.QueueEvent("root")); yield return null;
            Assert.That(finish, Is.Not.Null); Assert.That(c.IsBusy && c.BlocksSave); Assert.That(c.State.HasHappenedEvent("root"), Is.False);
            finish(new NarrativeGameplayResult { passed = true });
            for (int i = 0; i < 8 && c.IsBusy; i++) yield return null;
            Assert.That(c.State.HasHappenedEvent("root")); Assert.That(seen, Is.EqualTo(new[] { "before", "after" }));
            Assert.That(presenter.IsAwaitingGameplay, Is.False); Assert.That(handle.Disposals, Is.EqualTo(1));
            Assert.That(released, Is.EqualTo(2)); Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(baseline + 1));
        }
        [UnityTest] public IEnumerator CancelDisposeAndLateCallbacksNeverCompleteAnotherRequest()
        {
            var callbacks = new List<Action<NarrativeGameplayResult>>(); var handles = new List<Handle>();
            presenter = new NarrativePresenter(Config());
            presenter.ConversationPlayback = (_, data, done) => { data.releaseStage = () => { }; done(); return true; };
            presenter.GameplayPlayback = (_, data, done) => { callbacks.Add(done); var h = new Handle(); handles.Add(h); return h; };
            var c = new NarrativeController(Config(), presenter); presenter.PageCondition = c.ReadPresentationCondition;
            Assert.That(c.QueueEvent("root")); yield return null; Assert.That(callbacks.Count, Is.EqualTo(1));
            presenter.DebugStopPresentation(); callbacks[0](new NarrativeGameplayResult { passed = true });
            Assert.That(handles[0].Disposals, Is.EqualTo(1)); Assert.That(c.State.HasHappenedEvent("root"), Is.False);
            Assert.That(c.QueueEvent("root")); yield return null; Assert.That(callbacks.Count, Is.EqualTo(2));
            callbacks[0](new NarrativeGameplayResult { passed = true }); yield return null;
            Assert.That(c.IsBusy); Assert.That(presenter.IsAwaitingGameplay);
            callbacks[1](new NarrativeGameplayResult { cancelled = true });
            for (int i = 0; i < 8 && c.IsBusy; i++) yield return null;
            Assert.That(c.State.HasHappenedEvent("root"), Is.False); Assert.That(handles[1].Disposals, Is.EqualTo(1));
            Assert.That(c.QueueEvent("root")); yield return null;
            presenter.Dispose(); callbacks[2](new NarrativeGameplayResult { passed = true });
            Assert.That(handles[2].Disposals, Is.EqualTo(1)); Assert.That(c.State.HasHappenedEvent("root"), Is.False);
        }
        [UnityTest] public IEnumerator SynchronousHostCallbackAcceptsOnlyFirstOutcomeAndDisposesOnce()
        {
            bool? observed = null; var handle = new Handle();
            presenter = new NarrativePresenter(Config());
            presenter.ConversationPlayback = (_, data, done) => { data.releaseStage = () => { }; done(); return true; };
            presenter.GameplayPlayback = (_, data, done) =>
            {
                done(new NarrativeGameplayResult { passed = false });
                done(new NarrativeGameplayResult { passed = true });
                return handle;
            };
            presenter.GameplayCompleted += (_, passed) => observed = passed;
            var c = new NarrativeController(Config(), presenter); presenter.PageCondition = c.ReadPresentationCondition;
            Assert.That(c.QueueEvent("root"));
            for (int i = 0; i < 8 && c.IsBusy; i++) yield return null;
            Assert.That(c.State.HasHappenedEvent("root")); Assert.That(observed, Is.False);
            Assert.That(handle.Disposals, Is.EqualTo(1)); Assert.That(presenter.IsAwaitingGameplay, Is.False);
        }
        [UnityTest] public IEnumerator DirectReadingCancelNeverSettlesTrainingOrNewsAndRejectsStaleCallbacks()
        {
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "reading.root", presentationKey = "reading", presentationClass = "Reading", blocksSave = true });
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_Narration", atomSeq = "A_NARRATE" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "reading", presentationClass = "Reading" });
            config.presentations.readingPages.Add(new NarrativeReadingPageDefinition { pageId = "read", presentationKey = "reading", seq = 1 });
            config.presentations.readingLines.Add(new NarrativeReadingLineDefinition { lineId = "line", pageId = "read", seq = 1, textKey = "body", mode = "narration" });
            config.bars.Add(new NarrativeBarDefinition { barKey = "player.language", min = 0, max = 20, initValue = 3 });
            config.bars.Add(new NarrativeBarDefinition { barKey = "training.language", min = 0, max = 9 });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "news.read" });
            config.results.Add(new NarrativeResultDefinition { resultId = "training", eventId = "reading.root", resultType = NarrativeResultType.TrainAttribute, target = "player.language", value = "2" });
            config.results.Add(new NarrativeResultDefinition { resultId = "news", eventId = "reading.root", resultType = NarrativeResultType.SetFlag, target = "news.read", value = "true", seq = 1 });
            var cancels = new List<Action>(); var completions = new List<Action>();
            presenter = new NarrativePresenter(config);
            presenter.ReadingPlayback = (request, title, pages, image, dialogue, speaker, portrait, done) =>
            {
                Assert.That(request.PresentationCancelled, Is.Not.Null);
                cancels.Add(request.PresentationCancelled); completions.Add(done); return true;
            };
            var c = new NarrativeController(config, presenter); presenter.PageCondition = c.ReadPresentationCondition;
            Assert.That(c.QueueEvent("reading.root")); yield return null;
            Assert.That(cancels.Count, Is.EqualTo(1)); cancels[0](); completions[0]();
            yield return null;
            Assert.That(c.IsBusy, Is.False); Assert.That(c.State.HasHappenedEvent("reading.root"), Is.False);
            Assert.That(c.State.GetCondition("news.read"), Is.False);
            Assert.That(c.State.TryGetBar("training.language", out int cancelledXp) && cancelledXp == 0);
            Assert.That(c.QueueEvent("reading.root")); yield return null;
            Assert.That(cancels.Count, Is.EqualTo(2)); cancels[0](); completions[0](); yield return null;
            Assert.That(c.IsBusy); Assert.That(c.State.GetCondition("news.read"), Is.False);
            completions[1]();
            for (int frame = 0; frame < 8 && c.IsBusy; frame++) yield return null;
            Assert.That(c.State.HasHappenedEvent("reading.root")); Assert.That(c.State.GetCondition("news.read"));
            Assert.That(c.State.TryGetBar("training.language", out int earnedXp) && earnedXp == 2);
        }
        [UnityTest] public IEnumerator RuntimeInputGateYieldsWhileFullBusyAndSaveGatesRemainBlocked()
        {
            Action<NarrativeGameplayResult> finish = null; var handle = new Handle();
            var go = new UnityEngine.GameObject("NarrativeGameplayRuntimeTestHost"); go.SetActive(false);
            runtime = go.AddComponent<NarrativeRuntime>();
            runtime.GameplayPlayback = (_, data, done) => { finish = done; return handle; };
            runtime.ConversationPlayback = (_, data, done) => { data.releaseStage = () => { }; done(); return true; };
            runtime.Initialize(Config()); presenter = runtime.Presenter;
            Assert.That(runtime.Controller.QueueEvent("root")); yield return null;
            Assert.That(runtime.IsAwaitingGameplay); Assert.That(runtime.BlocksGameplayInput, Is.False);
            Assert.That(runtime.IsPresenting); Assert.That(runtime.Controller.IsBusy); Assert.That(runtime.BlocksSave);
            finish(new NarrativeGameplayResult { passed = false });
            for (int i = 0; i < 8 && runtime.Controller.IsBusy; i++) yield return null;
            Assert.That(runtime.Controller.State.HasHappenedEvent("root")); Assert.That(runtime.IsAwaitingGameplay, Is.False);
        }
    }
}
#endif
