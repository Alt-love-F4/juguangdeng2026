using System.Collections;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.TestTools;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class PresentationDebugSessionTests
    {
        [UnityTest]
        public IEnumerator ConversationUsesFormalBridgeAndDisposesWithoutRuntimeOrPauseLeak()
        {
            Assert.That(NarrativeRuntime.Instance, Is.Null);
            UIPanel panel = null;
            bool complete = false;
            var config = LubanNarrativeLoader.Load();
            using (var session = new PresentationDebugSession(config, PresentationDebugSceneController.DebugTag,
                true, true, 3, "预览馆长", value => panel = value, _ => { }))
            {
                session.Play("conversation:DEMO_CAFE_CHAT", () => complete = true);
                yield return null;
                Assert.That(panel, Is.InstanceOf<DialoguePanel>());
                Assert.That(panel.Tag, Is.EqualTo(PresentationDebugSceneController.DebugTag));
                Assert.That(((DialoguePanel)panel).IsPlaying, Is.True);
                Assert.That(NarrativeRuntime.Instance, Is.Null);
                Assert.That(complete, Is.False);
            }
            UIKit.ClosePanelsByTag(PresentationDebugSceneController.DebugTag);
            yield return null;
            Assert.That(PresentationPauseRegistry.IsPaused, Is.False);
            Assert.That(complete, Is.False, "Disposing a preview cannot settle or finish an event.");
        }

        [UnityTest]
        public IEnumerator ReadingIsDiscoveredAndOpensProductionPanel()
        {
            const string id = "reading:oct07.s2.letter";
            UIPanel panel = null;
            using (var session = new PresentationDebugSession(LubanNarrativeLoader.Load(),
                    PresentationDebugSceneController.DebugTag, true, true, 3, "馆长", value => panel = value, _ => { }))
            {
                session.Play(id, () => { });
                yield return null;
                Assert.That(panel, Is.Not.Null, id);
                Assert.That(panel.Tag, Is.EqualTo(PresentationDebugSceneController.DebugTag));
                Assert.That(panel, Is.InstanceOf<ReadingPanel>(), id);
                Assert.That(NarrativeRuntime.Instance, Is.Null);
            }
            UIKit.ClosePanelsByTag(PresentationDebugSceneController.DebugTag);
            yield return null;
            Assert.That(PresentationPauseRegistry.IsPaused, Is.False, id);
        }
    }
}
