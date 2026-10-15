using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine.TestTools;

namespace Spotlight.GameFlow.Tests
{
    // Fake playback isolates the shared presenter contract from bridges, panels and scenes.
    public sealed class NarrativePresenterReadingTests
    {
        private sealed class PageCall
        {
            public NarrativeEventRequest request;
            public string title, image, speaker, portrait;
            public string[] text;
            public bool dialogue;
            public Action complete;
        }

        private static NarrativeConfiguration Configuration()
        {
            var config = new NarrativeConfiguration();
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_Narration", atomSeq = "A_NARRATE" });
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_TwoPersonTalk", atomSeq = "A_DIALOG" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "reading", presentationClass = "reading" });
            return config;
        }

        private static NarrativeEventRequest Request(string key = "reading")
            => new NarrativeEventRequest(new NarrativeEventDefinition
            {
                eventId = "event", presentationKey = key, titleText = "event-title",
                contentKeys = new List<string> { "fallback-one", "fallback-two" }
            }, null, 2, new[] { "selected" }, requestId: "request", passiveHint: "hint");

        private static void AddPage(NarrativeConfiguration config, string id, int seq, string keys = null)
        {
            config.presentations.readingPages.Add(new NarrativeReadingPageDefinition
            {
                pageId = id, presentationKey = "reading", seq = seq, titleKey = id + "-title",
                imageKey = id + "-image", pageCondKeys = keys
            });
            config.presentations.readingLines.Add(new NarrativeReadingLineDefinition
            {
                pageId = id, seq = 1, textKey = id + "-line", mode = "narration"
            });
        }

        private static void Capture(NarrativePresenter presenter, List<PageCall> calls)
        {
            presenter.ReadingPlayback = (request, title, pages, image, dialogue, speaker, portrait, complete) =>
            {
                var text = new string[pages.Count];
                for (int i = 0; i < text.Length; i++) text[i] = pages[i];
                calls.Add(new PageCall { request = request, title = title, text = text, image = image,
                    dialogue = dialogue, speaker = speaker, portrait = portrait, complete = complete });
                return true;
            };
        }

        [UnityTest]
        public IEnumerator ReadingOrdersPagesAndLinesAndWaitsForEachLocalCompletion()
        {
            var config = Configuration();
            AddPage(config, "later", 20);
            AddPage(config, "hidden", 15, "deny");
            AddPage(config, "first", 10, " allow | second, third ");
            config.presentations.readingLines.Add(new NarrativeReadingLineDefinition
            {
                pageId = "first", seq = 0, textKey = "speech", mode = "dialogue", speakerKey = "speaker", voiceKey = "voice"
            });
            config.objects.Add(new NarrativeObjectDefinition { objId = "speaker", nameText = "name", portraitPath = "", iconPath = "icon" });
            var calls = new List<PageCall>();
            var checkedKeys = new List<string>();
            bool completed = false;
            using (var presenter = new NarrativePresenter(config, key => "resolved:" + key))
            {
                presenter.PageCondition = key => { checkedKeys.Add(key); return key != "deny"; };
                presenter.CharacterResolver = (key, name, portrait) => key == "speaker"
                    ? new NarrativeCharacterPresentationData { name = name + "!", portraitPath = portrait + "!" } : null;
                Capture(presenter, calls);
                var incoming = Request();
                presenter.Play(incoming, () => completed = true);
                yield return null;
                Assert.That(calls.Count, Is.EqualTo(1));
                Assert.That(calls[0].title, Is.EqualTo("resolved:first-title"));
                Assert.That(calls[0].image, Is.EqualTo("first-image"));
                Assert.That(calls[0].text, Is.EqualTo(new[] { "resolved:speech\nresolved:first-line" }));
                Assert.That(calls[0].dialogue, Is.True);
                Assert.That(calls[0].speaker, Is.EqualTo("resolved:name!"));
                Assert.That(calls[0].portrait, Is.EqualTo("icon!"));
                Assert.That(calls[0].request.RequestId, Is.EqualTo(incoming.RequestId));
                Assert.That(calls[0].request.SelectedChoices, Is.SameAs(incoming.SelectedChoices));
                Assert.That(calls[0].request.Depth, Is.EqualTo(2));
                Assert.That(calls[0].request.PassiveHint, Is.EqualTo("hint"));
                Assert.That(calls[0].request.ReadingLines.Count, Is.EqualTo(2));
                Assert.That(calls[0].request.ReadingLines[0].voiceKey, Is.EqualTo("voice"));
                Assert.That(calls[0].request.ReadingLines[0].portraitPath, Is.EqualTo("icon!"));
                Assert.That(completed, Is.False);
                calls[0].complete();
                yield return null;
                yield return null;
                Assert.That(calls.Count, Is.EqualTo(2));
                Assert.That(calls[1].title, Is.EqualTo("resolved:later-title"));
                Assert.That(calls[1].image, Is.EqualTo("later-image"));
                calls[0].complete(); // A duplicate old page callback cannot complete the new page.
                yield return null;
                Assert.That(completed, Is.False);
                calls[1].complete(); // The API also uses this callback for single-page cancel.
                yield return null;
                yield return null;
                Assert.That(completed, Is.True);
                Assert.That(checkedKeys, Is.EqualTo(new[] { "allow", "second", "third", "deny" }));
            }
        }

        [UnityTest]
        public IEnumerator DisposeInvalidatesLatePageCallbackAndNeverStartsNextPage()
        {
            var config = Configuration();
            AddPage(config, "first", 1);
            AddPage(config, "second", 2);
            var calls = new List<PageCall>();
            bool completed = false;
            using (var presenter = new NarrativePresenter(config))
            {
                Capture(presenter, calls);
                presenter.Play(Request(), () => completed = true);
                yield return null;
                Assert.That(calls.Count, Is.EqualTo(1));
                presenter.Dispose();
                calls[0].complete();
                yield return null;
                yield return null;
                Assert.That(calls.Count, Is.EqualTo(1));
                Assert.That(completed, Is.False);
                Assert.That(presenter.IsPresenting, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator DisposeInsidePlaybackCannotOpenNextPageEvenWithSynchronousCompletion()
        {
            var config = Configuration();
            AddPage(config, "first", 1);
            AddPage(config, "second", 2);
            int calls = 0;
            bool completed = false;
            using (var presenter = new NarrativePresenter(config))
            {
                presenter.ReadingPlayback = (request, title, pages, image, dialogue, speaker, portrait, complete) =>
                {
                    calls++;
                    presenter.Dispose();
                    complete();
                    return true;
                };
                presenter.Play(Request(), () => completed = true);
                yield return null;
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(completed, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ReadingWithoutConfiguredPagesPreservesContentKeyFallback()
        {
            var calls = new List<PageCall>();
            bool completed = false;
            using (var presenter = new NarrativePresenter(Configuration(), key => "resolved:" + key))
            {
                Capture(presenter, calls);
                presenter.Play(Request(), () => completed = true);
                yield return null;
                Assert.That(calls.Count, Is.EqualTo(1));
                Assert.That(calls[0].text, Is.EqualTo(new[] { "resolved:fallback-one", "resolved:fallback-two" }));
                calls[0].complete();
                yield return null;
                yield return null;
                Assert.That(completed, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator PageVisibilityFailsExplicitlyForMissingReaderAndUnsupportedBars()
        {
            using (var presenter = new NarrativePresenter(Configuration()))
            {
                var method = typeof(NarrativePresenter).GetMethod("PageVisible", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                var missing = Assert.Throws<TargetInvocationException>(() => method.Invoke(presenter, new object[] { "page", "condition", null }));
                Assert.That(missing.InnerException, Is.InstanceOf<InvalidOperationException>());
                StringAssert.Contains("Page condition reader unavailable: page", missing.InnerException.Message);
                presenter.PageCondition = _ => false;
                var unsupported = Assert.Throws<TargetInvocationException>(() => method.Invoke(presenter, new object[] { "page", "hidden", "bar > 1" }));
                Assert.That(unsupported.InnerException, Is.InstanceOf<InvalidOperationException>());
                StringAssert.Contains("Unsupported presentation page bar conditions: page", unsupported.InnerException.Message);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConversationUsesTheSamePageConditionsAsReading()
        {
            var config = Configuration();
            config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "conversation", presentationClass = "conversation" });
            foreach (var id in new[] { "hidden", "visible" })
            {
                config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                {
                    pageId = id, presentationKey = "conversation", seq = id == "hidden" ? 1 : 2, pageCondKeys = id
                });
                config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition { pageId = id, textKey = id, seq = 1 });
            }
            int calls = 0;
            bool completed = false;
            using (var presenter = new NarrativePresenter(config))
            {
                presenter.PageCondition = key => key == "visible";
                presenter.ConversationPlayback = (request, data, complete) =>
                {
                    calls++;
                    Assert.That(data.lines[0].text, Is.EqualTo("visible"));
                    complete();
                    return true;
                };
                presenter.Play(Request("conversation"), () => completed = true);
                yield return null;
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(completed, Is.True);
            }
        }
    }
}
