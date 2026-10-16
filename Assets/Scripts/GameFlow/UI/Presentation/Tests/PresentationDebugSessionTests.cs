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
        public IEnumerator ConversationReadingUsesOrdinaryPanelAndCancelsWithoutCompleting()
        {
            Assert.That(NarrativeRuntime.Instance, Is.Null);
            int owners = PresentationPauseRegistry.OwnerCount;
            UIPanel panel = null;
            bool complete = false, cancelled = false;
            var config = ConversationReadingStepTests.Configuration();
            config.presentations.conversationPages.RemoveAll(page => page.pageId != "card-step");
            config.presentations.conversationLines.Clear();
            using (var session = new PresentationDebugSession(config, PresentationDebugSceneController.DebugTag,
                true, true, 3, "馆长", value => panel = value, _ => cancelled = true))
            {
                session.Play("conversation:conversation", () => complete = true);
                float deadline = Time.realtimeSinceStartup + 5f;
                while (!(panel is ReadingPanel))
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                    yield return null;
                }
                Assert.That(((ReadingPanel)panel).IsPlaying, Is.True);
                Assert.That(panel.Tag, Is.EqualTo(PresentationDebugSceneController.DebugTag));
                Assert.That(NarrativeRuntime.Instance, Is.Null);
                ((ReadingPanel)panel).Cancel();
                while (session.IsPresenting)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                    yield return null;
                }
                Assert.That(cancelled, Is.True);
                Assert.That(complete, Is.False);
            }
            yield return null;
            Assert.That(UIKit.GetPanel<ReadingPanel>()?.IsPlaying ?? false, Is.False);
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(owners));
        }

        [UnityTest]
        public IEnumerator ReadingAndNoticeAreDiscoveredAndOpenProductionPanels()
        {
            foreach (string id in new[] { "reading:TEST_READING", "notice:TEST_NOTICE" })
            {
                UIPanel panel = null;
                using (var session = new PresentationDebugSession(LubanNarrativeLoader.Load(),
                    PresentationDebugSceneController.DebugTag, true, true, 3, "馆长", value => panel = value, _ => { }))
                {
                    session.Play(id, () => { });
                    yield return null;
                    Assert.That(panel, Is.Not.Null, id);
                    Assert.That(panel.Tag, Is.EqualTo(PresentationDebugSceneController.DebugTag));
                    Assert.That(id.StartsWith("reading:") ? panel is ReadingPanel : panel is NoticePanel, Is.True, id);
                    Assert.That(NarrativeRuntime.Instance, Is.Null);
                }
                UIKit.ClosePanelsByTag(PresentationDebugSceneController.DebugTag);
                yield return null;
                Assert.That(PresentationPauseRegistry.IsPaused, Is.False, id);
            }
        }
    }
}
