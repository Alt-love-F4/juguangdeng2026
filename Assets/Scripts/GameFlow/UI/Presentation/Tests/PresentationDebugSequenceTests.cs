#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class PresentationDebugSequenceTests
    {
        private Scene scene;
        private PresentationDebugSession session;
        private float originalScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScale = Time.timeScale;
            scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            session?.Dispose();
            session = null;
            UIKit.ClosePanelsByTag(PresentationDebugSceneController.DebugTag);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = originalScale;
            yield return null;
            Assert.That(PresentationPauseRegistry.IsPaused, Is.False);
        }

        [UnityTest]
        public IEnumerator FirstArrivalContinuouslyFollowsChoicesRetryAndAbilityBranchWithoutLiveRuntime()
        {
            var configuration = LubanNarrativeLoader.Load();
            string before = JsonConvert.SerializeObject(configuration, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            var started = new List<string>();
            UIPanel current = null;
            bool completed = false;
            string failure = null;
            session = new PresentationDebugSession(configuration, PresentationDebugSceneController.DebugTag,
                false, false, 3, "预览馆长", panel => current = panel, reason => failure = reason, followEventChain: true);
            session.EventStarted += started.Add;
            session.Play("sequence:arrival.start", () => completed = true);
            Assert.That(completed, Is.False);
            Assert.That(session.IsPresenting, Is.True);
            float deadline = Time.realtimeSinceStartup + 35f;
            int refusals = 0;
            while (!completed && failure == null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Stalled at " + session.CurrentEventId);
                Assert.That(NarrativeRuntime.Instance, Is.Null);
                if (current is DialoguePanel dialogue && dialogue.IsPlaying)
                {
                    if (dialogue.IsChoosing && dialogue.IsReady && string.IsNullOrEmpty(dialogue.SelectedOptionId))
                    {
                        string choice = session.CurrentEventId == "arrival.start" ? "MA70.10.06-S1-1.1"
                            : session.CurrentEventId == "arrival.refuse" ? "MA70.10.06-S1-3.1"
                            : refusals++ < 2 ? "MA70.10.06-S1-2.1" : "MA70.10.06-S1-2.2";
                        Assert.That(dialogue.SelectOption(choice), Is.True, session.CurrentEventId + " / " + choice);
                    }
                    else if (!dialogue.IsChoosing && dialogue.IsReady) dialogue.Advance();
                }
                yield return null;
            }
            Assert.That(failure, Is.Null);
            CollectionAssert.AreEqual(new[] { "arrival.start", "arrival.reply.1", "arrival.agent", "arrival.check", "arrival.insight",
                "arrival.request", "arrival.decision", "arrival.refuse", "arrival.decision", "arrival.refuse",
                "arrival.decision", "arrival.accept", "arrival.complete" }, started);
            Assert.That(session.IsPresenting, Is.False);
            Assert.That(JsonConvert.SerializeObject(configuration, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), Is.EqualTo(before), "Preview must not mutate generated content.");
            Assert.That(NarrativeRuntime.Instance, Is.Null);
        }

        [UnityTest]
        public IEnumerator FormalContentPlaysAllPagesInsideExplicitTemplateBeforeCompletion()
        {
            var configuration = new NarrativeConfiguration();
            configuration.templates.Add(new NarrativeTemplateDefinition
                { tplName = "new.template", atomSeq = "A_WAIT,A_DIALOG,A_WAIT", interruptType = "progressive" });
            configuration.setups.Add(new NarrativeSetupParamsDefinition
                { setupKey = "new.setup", tplName = "new.template", slotValues = "duration=0.15" });
            configuration.presentations.index.Add(new NarrativePresentationIndexDefinition
                { presentationKey = "new.content", presentationClass = "Conversation" });
            configuration.events.Add(new NarrativeEventDefinition
                { eventId = "new.entry", presentationKey = "new.content", setupKey = "new.setup" });
            for (int index = 2; index >= 1; index--)
            {
                configuration.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                    { pageId = "page." + index, presentationKey = "new.content", seq = index });
                configuration.presentations.conversationLines.Add(new NarrativeConversationLineDefinition
                    { pageId = "page." + index, textKey = "text." + index, seq = 1 });
            }
            var played = new List<string>();
            bool completed = false;
            Action next = null;
            using (var presenter = new NarrativePresenter(configuration))
            {
                presenter.ConversationPlayback = (request, data, done) =>
                {
                    played.Add(data.lines[0].text);
                    next = done;
                    return true;
                };
                float start = Time.realtimeSinceStartup;
                presenter.Play(new NarrativeEventRequest(configuration.events[0], null, 0), () => completed = true);
                Assert.That(played, Is.Empty, "Leading template atom must run before the dialogue bridge.");
                float deadline = start + 5f;
                while (next == null) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
                Assert.That(Time.realtimeSinceStartup - start, Is.GreaterThanOrEqualTo(.10f));
                next(); next = null;
                while (next == null) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
                CollectionAssert.AreEqual(new[] { "text.1", "text.2" }, played);
                next();
                yield return null;
                Assert.That(completed, Is.False, "Completing the last page must not skip trailing template atoms.");
                while (!completed) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            }
        }

        [UnityTest]
        public IEnumerator NewDataDrivenChainPlaysEveryReadingPageAndWaitsForResultEffects()
        {
            var configuration = new NarrativeConfiguration();
            configuration.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_Narration", atomSeq = "A_NARRATE" });
            configuration.templates.Add(new NarrativeTemplateDefinition { tplName = "wait", atomSeq = "A_WAIT" });
            configuration.setups.Add(new NarrativeSetupParamsDefinition { setupKey = "tail", tplName = "wait", slotValues = "duration=0.2" });
            configuration.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "preview.flag" });
            configuration.events.Add(new NarrativeEventDefinition { eventId = "future.signal" });
            configuration.events.Add(new NarrativeEventDefinition { eventId = "future.reading", presentationKey = "future.book" });
            configuration.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "future.book", presentationClass = "Reading" });
            configuration.results.Add(new NarrativeResultDefinition { eventId = "future.signal", resultType = NarrativeResultType.SetFlag, target = "preview.flag", value = "true", seq = 1 });
            configuration.results.Add(new NarrativeResultDefinition { eventId = "future.signal", resultType = NarrativeResultType.QueueEvent, target = "event:future.reading", seq = 2 });
            configuration.results.Add(new NarrativeResultDefinition { eventId = "future.reading", resultType = NarrativeResultType.PlaySetup, target = "setup:tail", seq = 1 });
            for (int index = 2; index >= 1; index--)
            {
                configuration.presentations.readingPages.Add(new NarrativeReadingPageDefinition
                    { pageId = "future.page." + index, presentationKey = "future.book", seq = index, titleKey = "page." + index, pageCondKeys = "preview.flag" });
                configuration.presentations.readingLines.Add(new NarrativeReadingLineDefinition
                    { pageId = "future.page." + index, textKey = "page " + index, seq = 1 });
            }
            var panels = new List<UIPanel>();
            bool complete = false;
            string failure = null;
            session = new PresentationDebugSession(configuration, PresentationDebugSceneController.DebugTag,
                false, false, 3, "馆长", panels.Add, reason => failure = reason, followEventChain: true);
            session.Play("sequence:future.signal", () => complete = true);
            float deadline = Time.realtimeSinceStartup + 8f;
            bool observedDrain = false;
            while (!complete && failure == null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), session.CurrentEventId);
                if (panels.LastOrDefault() is DialoguePanel dialogue && dialogue != null && dialogue.IsPlaying) dialogue.Advance();
                else if (panels.Count == 2) { observedDrain = true; Assert.That(session.IsPresenting, Is.True); }
                yield return null;
            }
            Assert.That(failure, Is.Null);
            Assert.That(complete, Is.True);
            Assert.That(panels.Count, Is.EqualTo(2), "Both conditionally visible pages must play after the local flag result.");
            Assert.That(observedDrain, Is.True, "Completion must wait for the result-driven effect, not just the last reading page.");
            Assert.That(configuration.conditionKeys[0].initialValue, Is.False);
            Assert.That(NarrativeRuntime.Instance, Is.Null);
        }

        [UnityTest]
        public IEnumerator BackgroundImagesAndLiveSceneSwitchCleanlyAcrossContinuousPages()
        {
            var owner = new object();
            DialoguePanel panel = null;
            foreach (string key in new[] { "street.day", NarrativeBackgroundKeys.CafeScene, "street.night", NarrativeBackgroundKeys.CafeScene })
            {
                var script = new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Narrator, "背景切换") },
                    continuityOwner: owner, continues: true, backgroundVisual: NarrativeStageVisuals.Resolve(key));
                panel = DialoguePlayback.Play(script, null, null,
                    PresentationDebugSceneController.DebugTag, PanelCachePolicy.Transient);
                bool sceneBackground = key == NarrativeBackgroundKeys.CafeScene;
                Assert.That(panel.BackgroundLayer.enabled, Is.EqualTo(!sceneBackground), key);
                if (sceneBackground)
                {
                    Assert.That(panel.BackgroundLayer.texture, Is.Null);
                    foreach (var layer in panel.BackgroundLayer.GetComponentsInChildren<UnityEngine.UI.RawImage>(true))
                        Assert.That(layer.enabled, Is.False, layer.name);
                }
                float deadline = Time.realtimeSinceStartup + 5f;
                while (panel.IsPlaying)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                    if (panel.IsReady) panel.Advance();
                    yield return null;
                }
                Assert.That(panel.SeamlessChoicePending, Is.True);
            }
            DialoguePlayback.ReleaseStage(owner);
            yield return null;
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator StopDuringContinuousSequenceSuppressesPendingCallbacks()
        {
            UIPanel current = null;
            bool completed = false;
            var started = new List<string>();
            session = new PresentationDebugSession(LubanNarrativeLoader.Load(), PresentationDebugSceneController.DebugTag,
                true, true, 3, "馆长", panel => current = panel, _ => { }, followEventChain: true);
            session.EventStarted += started.Add;
            session.Play("sequence:arrival.start", () => completed = true);
            yield return null;
            Assert.That(current, Is.InstanceOf<DialoguePanel>());
            session.Dispose();
            UIKit.ClosePanelsByTag(PresentationDebugSceneController.DebugTag);
            yield return null;
            Assert.That(completed, Is.False);
            CollectionAssert.AreEqual(new[] { "arrival.start" }, started);
            Assert.That(session.IsPresenting, Is.False);
        }
    }
}
#endif
