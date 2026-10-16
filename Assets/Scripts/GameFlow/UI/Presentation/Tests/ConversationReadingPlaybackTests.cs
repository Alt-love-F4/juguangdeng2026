using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.TestTools;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class ConversationReadingPlaybackTests
    {
        private object owner;
        private int pauseOwners;
        private Func<bool> previousInputBlocker;
        [SetUp] public void Setup()
        {
            owner = new object();
            pauseOwners = PresentationPauseRegistry.OwnerCount;
            previousInputBlocker = DialoguePanel.ExternalInputBlocked;
            DialoguePanel.ExternalInputBlocked = null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            ConversationReadingPlayback.ReleaseStage(owner);
            DialoguePanel.ExternalInputBlocked = previousInputBlocker;
            yield return null;
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners));
        }
        private NarrativeConversationReadingPresentationData Data()
        {
            var data = new NarrativeConversationReadingPresentationData
            { continuityOwner = owner, eventId = "root", readingKey = "card", readingPageId = "card-page", allowCancel = true };
            for (int i = 1; i <= 3; i++) data.lines.Add(new NarrativeConversationReadingPresentationLine
            { lineId = "line-" + i, seq = i, textKey = "text-" + i, text = "Card row " + i, mode = "narration" });
            return data;
        }
        [Test] public void ScriptCopiesThreeSeparateRowsAndDoesNotInventTitle()
        {
            var data = Data();
            data.lines[1].text = "French\nChinese\nFrench";
            var script = ConversationReadingPlayback.CreateScript(data);
            data.lines[0].text = "mutated"; data.lines.Clear(); data.title = "mutated";
            Assert.That(script.Count, Is.EqualTo(1));
            Assert.That(script.Title, Is.Empty);
            Assert.That(script[0].Title, Is.Empty);
            Assert.That(script[0].Lines.Count, Is.EqualTo(3));
            Assert.That(script[0].Lines[0].Text, Is.EqualTo("Card row 1"));
            Assert.That(script[0].Lines[1].Text, Is.EqualTo("French\nChinese\nFrench"));
            Assert.That(script[0].Lines[2].textKey, Is.EqualTo("text-3"));
        }
        [UnityTest] public IEnumerator NaturalCompletionSeparatesStopAndOnlyCompletesOnceAfterThreeRows()
        {
            var data = Data(); int done = 0, cancelled = 0, stopped = 0;
            data.cancelled = () => cancelled++; data.playbackStopped = () => stopped++;
            Assert.That(ConversationReadingPlayback.Play(default, data, () => done++));
            var panel = UIKit.GetPanel<ReadingPanel>();
            yield return Wait(() => Field<bool>(panel, "ready"));
            for (int line = 0; line < 3; line++)
            {
                Assert.That(panel.LineIndex, Is.EqualTo(line));
                Assert.That(done, Is.Zero);
                yield return null;
                if (panel.IsTyping) { panel.Advance(); yield return null; }
                panel.Advance();
                if (line < 2) { Assert.That(panel.LineIndex, Is.EqualTo(line + 1)); yield return null; }
            }
            yield return Wait(() => done == 1);
            Assert.That(stopped, Is.EqualTo(1));
            Assert.That(cancelled, Is.Zero);
            data.releaseStage(); data.releaseStage();
            yield return null;
            Assert.That(done, Is.EqualTo(1));
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners));
        }
        [UnityTest] public IEnumerator CancelAndOwnerReleaseNeverCompleteAndOldReleaseCannotCloseNextOwner()
        {
            var data = Data(); int done = 0, cancelled = 0, stopped = 0;
            data.cancelled = () => cancelled++; data.playbackStopped = () => stopped++;
            Assert.That(ConversationReadingPlayback.Play(default, data, () => done++));
            var panel = UIKit.GetPanel<ReadingPanel>();
            yield return null;
            panel.Cancel();
            Assert.That(done, Is.Zero); Assert.That(cancelled, Is.EqualTo(1)); Assert.That(stopped, Is.EqualTo(1));
            Action staleRelease = data.releaseStage;
            owner = new object();
            var next = Data();
            Assert.That(ConversationReadingPlayback.Play(default, next, () => done++));
            staleRelease();
            Assert.That(panel.IsPlaying, Is.True);
            ConversationReadingPlayback.ReleaseStage(owner);
            yield return null;
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(done, Is.Zero); Assert.That(cancelled, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator StopAloneIsNotCompletionAndReleaseIsSilent()
        {
            var data = Data(); int done = 0, cancelled = 0, stopped = 0;
            data.cancelled = () => cancelled++; data.playbackStopped = () => stopped++;
            Assert.That(ConversationReadingPlayback.Play(default, data, () => done++));
            var panel = UIKit.GetPanel<ReadingPanel>();
            panel.Stop();
            Assert.That(stopped, Is.EqualTo(1)); Assert.That(done, Is.Zero); Assert.That(cancelled, Is.Zero);
            data.releaseStage();
            yield return null;
            Assert.That(stopped, Is.EqualTo(1)); Assert.That(done, Is.Zero); Assert.That(cancelled, Is.Zero);
        }
        [UnityTest] public IEnumerator FormalCancelAndSuspendedInputAreBlockedPreviewCanCancel()
        {
            var data = Data(); data.allowCancel = false;
            int done = 0, cancelled = 0;
            data.cancelled = () => cancelled++;
            Assert.That(ConversationReadingPlayback.Play(default, data, () => done++));
            var panel = UIKit.GetPanel<ReadingPanel>();
            yield return Wait(() => Field<bool>(panel, "ready"));
            panel.Cancel(); Assert.That(panel.IsPlaying); Assert.That(cancelled, Is.Zero);
            data.setSuspended(true);
            yield return null;
            panel.Advance(); panel.Back(); panel.Cancel();
            Assert.That(panel.LineIndex, Is.Zero);
            data.setSuspended(false);
            panel.Advance(); Assert.That(panel.LineIndex, Is.Zero, "Resume consumes this frame's submit.");
            data.releaseStage();
            Assert.That(ConversationReadingPlayback.PlayPreview(default, Data(), () => done++));
            yield return null;
            panel.Cancel();
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(done, Is.Zero);
        }
        [UnityTest] public IEnumerator AllowedCancelIsBlockedWhileSuspendedAndResumeConsumesSameFrameAdvance()
        {
            var data = Data(); data.allowCancel = true;
            int done = 0, cancelled = 0, stopped = 0;
            data.cancelled = () => cancelled++; data.playbackStopped = () => stopped++;
            Assert.That(ConversationReadingPlayback.Play(default, data, () => done++));
            var panel = UIKit.GetPanel<ReadingPanel>();
            var speedField = typeof(ReadingPanel).GetField("charactersPerSecond", BindingFlags.Instance | BindingFlags.NonPublic);
            float previousSpeed = (float)speedField.GetValue(panel);
            speedField.SetValue(panel, .1f);
            try
            {
                yield return Wait(() => Field<bool>(panel, "ready"));
                Assert.That(panel.AllowCancel, Is.True);
                Assert.That(panel.IsTyping, Is.True);
                data.setSuspended(true);
                float revealed = Field<float>(panel, "revealed");
                var text = Field<TMPro.TMP_Text>(panel, "readingText");
                int visibleCharacters = text.maxVisibleCharacters;
                for (int frame = 0; frame < 3; frame++)
                {
                    yield return null;
                    Assert.That(panel.IsTyping, Is.True);
                    Assert.That(Field<float>(panel, "revealed"), Is.EqualTo(revealed));
                    Assert.That(text.maxVisibleCharacters, Is.EqualTo(visibleCharacters));
                }
                panel.Cancel();
                Assert.That(panel.IsPlaying, Is.True);
                Assert.That(done, Is.Zero); Assert.That(cancelled, Is.Zero); Assert.That(stopped, Is.Zero);
                data.setSuspended(false);
                panel.Advance();
                Assert.That(panel.LineIndex, Is.Zero, "Resume consumes this frame's submit.");
                Assert.That(Field<float>(panel, "revealed"), Is.EqualTo(revealed));
                Assert.That(text.maxVisibleCharacters, Is.EqualTo(visibleCharacters));
                yield return null;
                panel.Cancel();
                Assert.That(panel.IsPlaying, Is.False);
                Assert.That(done, Is.Zero); Assert.That(cancelled, Is.EqualTo(1)); Assert.That(stopped, Is.EqualTo(1));
            }
            finally { speedField.SetValue(panel, previousSpeed); }
        }
        [UnityTest] public IEnumerator ReleasedSessionCallbacksStaySilentAndCannotCloseNextOwnerPanel()
        {
            var data = Data(); int done = 0, cancelled = 0, stopped = 0;
            data.cancelled = () => cancelled++; data.playbackStopped = () => stopped++;
            Assert.That(ConversationReadingPlayback.Play(default, data, () => done++));
            var panel = UIKit.GetPanel<ReadingPanel>();
            var activeField = typeof(ConversationReadingPlayback).GetField("active", BindingFlags.Static | BindingFlags.NonPublic);
            object oldSession = activeField.GetValue(null);
            string[] methodNames = { "Complete", "Cancel", "Stopped" };
            var staleCallbacks = new Action[methodNames.Length];
            for (int i = 0; i < methodNames.Length; i++)
                staleCallbacks[i] = (Action)Delegate.CreateDelegate(typeof(Action), oldSession,
                    oldSession.GetType().GetMethod(methodNames[i], BindingFlags.Instance | BindingFlags.Public));
            data.releaseStage();
            foreach (Action callback in staleCallbacks)
            {
                callback();
                Assert.That(done, Is.Zero); Assert.That(cancelled, Is.Zero); Assert.That(stopped, Is.Zero);
                Assert.That(panel.IsPlaying, Is.False);
                Assert.That(activeField.GetValue(null), Is.Null);
            }
            owner = new object();
            var next = Data(); int nextDone = 0, nextCancelled = 0, nextStopped = 0;
            next.cancelled = () => nextCancelled++; next.playbackStopped = () => nextStopped++;
            Assert.That(ConversationReadingPlayback.Play(default, next, () => nextDone++));
            Assert.That(UIKit.GetPanel<ReadingPanel>(), Is.SameAs(panel));
            object nextSession = activeField.GetValue(null);
            yield return Wait(() => Field<bool>(panel, "ready"));
            var nextState = panel.State;
            foreach (Action callback in staleCallbacks)
            {
                callback();
                Assert.That(done, Is.Zero); Assert.That(cancelled, Is.Zero); Assert.That(stopped, Is.Zero);
                Assert.That(nextDone, Is.Zero); Assert.That(nextCancelled, Is.Zero); Assert.That(nextStopped, Is.Zero);
                Assert.That(activeField.GetValue(null), Is.SameAs(nextSession));
                Assert.That(panel.IsPlaying, Is.True);
                Assert.That(panel.State, Is.EqualTo(nextState));
            }
            yield return null;
            Assert.That(panel.IsPlaying, Is.True);
            Assert.That(panel.State, Is.EqualTo(nextState));
            Assert.That(done, Is.Zero); Assert.That(cancelled, Is.Zero); Assert.That(stopped, Is.Zero);
            Assert.That(nextDone, Is.Zero); Assert.That(nextCancelled, Is.Zero); Assert.That(nextStopped, Is.Zero);
        }
        private static T Field<T>(ReadingPanel panel, string name)
            => (T)typeof(ReadingPanel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
        private static IEnumerator Wait(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!condition()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
        }
    }
}
