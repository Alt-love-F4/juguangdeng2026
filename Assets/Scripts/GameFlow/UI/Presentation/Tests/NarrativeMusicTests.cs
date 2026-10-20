using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using Spotlight.Audio;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativeMusicTests
    {
        [Test]
        public void EveryFormalEndingHasAnEnabledDistinctAssignmentWithALongLoop()
        {
            var catalog = Resources.Load<AudioCatalog>(AudioCatalog.ResourceName);
            var endings = LubanNarrativeLoader.Load().endings.Where(e => e.enabled).ToArray();
            Assert.That(endings.Length, Is.EqualTo(16));
            foreach (var ending in endings)
            {
                var cue = catalog.Find(NarrativeMusic.EndingPrefix + ending.endingId);
                Assert.That(cue, Is.Not.Null, ending.endingId);
                Assert.That(cue.enabled && cue.loop, Is.True, ending.endingId);
                Assert.That(cue.category, Is.EqualTo(AudioCategory.Music));
                Assert.That(cue.clip, Is.Not.Null);
                Assert.That(cue.clip.length, Is.GreaterThan(60f), "Avoid short victory jingles on ending reading panels.");
                Assert.That(cue.fadeIn, Is.GreaterThanOrEqualTo(1f));
            }
        }

        [TestCase("rose.route.1115.s1.ending.ideal_world", "恐怖？")]
        [TestCase("rose.route.ending.unforgiven.ending", "也许悲伤")]
        [TestCase("revolution.end01", "avemaria弥撒")]
        [TestCase("revolution.end02", "Eb 希望")]
        public void EndingMusicReflectsTheStoryRatherThanTreatingEveryEndingAsVictory(string ending, string track)
        {
            var cue = Resources.Load<AudioCatalog>(AudioCatalog.ResourceName).Find(NarrativeMusic.EndingPrefix + ending);
            Assert.That(cue.clip.name, Is.EqualTo(track));
        }

        [TestCase("complete")]
        [TestCase("cancel")]
        [TestCase("dispose")]
        public void EventMusicSpansPagesAndRestoresBaselineOnEveryExit(string exit)
        {
            GameAudio.Shutdown();
            GameAudio.Initialize(Resources.Load<AudioCatalog>(AudioCatalog.ResourceName));
            GameAudio.Music(AudioKeys.MenuMusic);
            const string key = "oct07.s1.conversation";
            var config = new NarrativeConfiguration();
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "talk", atomSeq = "A_DIALOG" });
            var definition = new NarrativeEventDefinition { eventId = "music.test", presentationClass = "conversation", presentationKey = key, setupType = "talk" };
            config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = key, presentationClass = "conversation" });
            foreach (string page in new[] { "first", "second" })
            {
                config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { presentationKey = key, pageId = page, seq = page == "first" ? 1 : 2 });
                config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition { pageId = page, lineId = page, textKey = page });
            }
            var presenter = new NarrativePresenter(config);
            Action next = null;
            int pageCount = 0;
            presenter.ConversationPlayback = (request, data, done) => { next = done; pageCount++; return true; };
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(NarrativePresenter).GetField("request", flags).SetValue(presenter, new NarrativeEventRequest(definition, null, 0));
            bool completed = false;
            typeof(NarrativePresenter).GetField("completion", flags).SetValue(presenter, (Action)(() => completed = true));
            var root = (IEnumerator)typeof(NarrativePresenter).GetMethod("PlayEvent", flags).Invoke(presenter, null);
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            void Pump()
            {
                for (int guard = 0; stack.Count > 0 && guard < 100; guard++)
                {
                    var current = stack.Peek();
                    if (!current.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current.Current is IEnumerator child) stack.Push(child); else return;
                }
            }
            string CurrentMusic()
            {
                var voices = new List<string>();
                GameAudio.Service.DescribeActiveVoices(voices);
                return voices.LastOrDefault(v => v.StartsWith("BGM: "));
            }
            try
            {
                Pump();
                Assert.That(pageCount, Is.EqualTo(1));
                Assert.That(CurrentMusic(), Does.StartWith("BGM: " + NarrativeMusic.ConversationPrefix + key + " |"));
                next(); Pump();
                Assert.That(pageCount, Is.EqualTo(2));
                Assert.That(CurrentMusic(), Does.StartWith("BGM: " + NarrativeMusic.ConversationPrefix + key + " |"));
                if (exit == "complete") { next(); Pump(); Assert.That(completed, Is.True); }
                else if (exit == "cancel") presenter.InterruptForHost();
                else presenter.Dispose();
                Assert.That(CurrentMusic(), Does.StartWith("BGM: " + AudioKeys.MenuMusic + " |"));
            }
            finally
            {
                presenter.Dispose();
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                GameAudio.Shutdown();
            }
        }

        [Test]
        public void CuratedLongConversationAssignmentsReferenceRealContentAndLeaveShortDailyScenesAlone()
        {
            var catalog = Resources.Load<AudioCatalog>(AudioCatalog.ResourceName);
            var config = LubanNarrativeLoader.Load();
            var cues = catalog.entries.Where(e => e.key.StartsWith(NarrativeMusic.ConversationPrefix)).ToArray();
            Assert.That(cues.Length, Is.GreaterThanOrEqualTo(50));
            foreach (var cue in cues)
            {
                string key = cue.key.Substring(NarrativeMusic.ConversationPrefix.Length);
                Assert.That(config.presentations.conversationPages.Any(p => p.presentationKey == key), Is.True, key);
                Assert.That(cue.clip, Is.Not.Null, key);
                Assert.That(cue.enabled && cue.loop, Is.True, key);
                Assert.That(cue.volume, Is.InRange(.05f, .4f), key);
                Assert.That(cue.category, Is.EqualTo(AudioCategory.Music));
                Assert.That(key.StartsWith("daily."), Is.False);
            }
        }
    }
}
