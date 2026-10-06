using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class DialoguePresentationTests
    {
        private const string Tag = "dialogue-presentation-tests";
        private float previousTimeScale;

        [SetUp]
        public void SetUp() { previousTimeScale = Time.timeScale; Time.timeScale = 1f; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UIKit.ClosePanelsByTag(Tag);
            DialoguePlayback.Stop();
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        [Test]
        public void ScriptRejectsInvalidDataAndSnapshotsTheCallerList()
        {
            Assert.Throws<ArgumentNullException>(() => new DialogueScript(null));
            Assert.Throws<ArgumentException>(() => new DialogueScript(Array.Empty<DialogueLine>()));
            Assert.Throws<ArgumentException>(() => new DialogueScript(new DialogueLine[] { null }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DialogueLine(DialogueSpeaker.Left, "x", autoAdvanceSeconds: float.NaN));
            var lines = new List<DialogueLine>();
            for (int i = 0; i < 10000; i++) lines.Add(new DialogueLine(DialogueSpeaker.Left, "台词 " + i));
            var script = new DialogueScript(lines);
            lines.Clear();
            Assert.That(script.Count, Is.EqualTo(10000));
            Assert.That(script[9999].Text, Is.EqualTo("台词 9999"));
        }

        [Test]
        public void PrefabAndPreviewHaveRealCharacterSpritesAndFont()
        {
            GameObject prefab = Resources.Load<GameObject>("Art/UIPrefab/DialoguePanel");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<DialoguePanel>(), Is.InstanceOf<IDialoguePresentation>());
            Assert.That(prefab.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(700));
            Assert.That(prefab.transform.Find("LeftPortrait").GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(prefab.transform.Find("RightPortrait").GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(prefab.transform.Find("DialogueBox/Dialogue").GetComponent<TMP_Text>().font, Is.Not.Null);
            DialogueScript sample = Resources.Load<DialogueScriptAsset>("Dialogue/DialoguePreview").CreateScript();
            Assert.That(sample.Count, Is.GreaterThan(2));
            Assert.That(sample.LeftCharacter.Portrait, Is.Not.Null);
            Assert.That(sample.RightCharacter.Portrait, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator RevealAndAdvanceAreSeparateAndDuplicateSubmitIsIgnored()
        {
            DialoguePanel panel = Open();
            panel.Play(new DialogueScript(new[]
            {
                new DialogueLine(DialogueSpeaker.Left, new string('长', 80)),
                new DialogueLine(DialogueSpeaker.Right, "第二句")
            }));
            panel.Advance();
            Assert.That(panel.LineIndex, Is.Zero, "Entrance submissions must not advance.");
            yield return Ready(panel);
            panel.Advance();
            Assert.That(panel.IsTyping, Is.False);
            Assert.That(panel.LineIndex, Is.Zero);
            panel.Advance();
            Assert.That(panel.LineIndex, Is.Zero, "Keyboard and button may submit in the same frame.");
            yield return null;
            panel.Advance();
            Assert.That(panel.LineIndex, Is.EqualTo(1));
            Assert.That(panel.transform.Find("DialogueBox/Speaker").GetComponent<TMP_Text>().text, Is.EqualTo("印刷工"));
        }

        [UnityTest]
        public IEnumerator LongLinePagesBeforeAdvancingToNextLine()
        {
            DialoguePanel panel = Open();
            panel.Play(new DialogueScript(new[]
            {
                new DialogueLine(DialogueSpeaker.Narrator, new string('长', 2000)),
                new DialogueLine(DialogueSpeaker.Left, "最后一句")
            }));
            yield return Ready(panel);
            panel.Advance();
            yield return null;
            panel.Advance();
            Assert.That(panel.LineIndex, Is.Zero);
            Assert.That(panel.PageNumber, Is.EqualTo(2));
            Assert.That(panel.IsTyping, Is.True);
        }

        [UnityTest]
        public IEnumerator FinishesOnceUsingUnscaledTime()
        {
            DialoguePanel panel = Open();
            int completions = 0;
            Time.timeScale = 0f;
            panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Left, "") }), () => completions++);
            yield return Ready(panel);
            panel.Advance();
            panel.Advance();
            yield return Until(() => !panel.IsPlaying);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator StopCancelsOldCallbackAndReusablePanelResetsCharacters()
        {
            DialoguePanel panel = Open();
            int oldCompletions = 0;
            panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Left, "", replaceRight: true) }), () => oldCompletions++);
            yield return Ready(panel);
            Assert.That(panel.transform.Find("RightPortrait").GetComponent<Image>().enabled, Is.False);
            panel.Advance();
            panel.Stop();
            panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Right, "新演出") }));
            yield return Ready(panel);
            Assert.That(panel.transform.Find("RightPortrait").GetComponent<Image>().enabled, Is.True);
            Assert.That(panel.LineIndex, Is.Zero);
            Assert.That(oldCompletions, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CancelCallsOnlyCancellationAndReleasesInputBlock()
        {
            DialoguePanel panel = Open();
            int completed = 0, cancelled = 0;
            panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Left, "测试") }), () => completed++, () => cancelled++);
            panel.Cancel();
            panel.Cancel();
            yield return null;
            Assert.That(completed, Is.Zero);
            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator AutomaticAdvanceAndCharacterReplacementWork()
        {
            DialoguePanel panel = Open();
            panel.Play(new DialogueScript(new[]
            {
                new DialogueLine(DialogueSpeaker.Narrator, "", autoAdvanceSeconds: .05f),
                new DialogueLine(DialogueSpeaker.Left, "新角色", replaceLeft: true,
                    leftCharacter: new DialogueCharacter("新访客", null))
            }));
            yield return Until(() => panel.LineIndex == 1);
            Assert.That(panel.transform.Find("DialogueBox/Speaker").GetComponent<TMP_Text>().text, Is.EqualTo("新访客"));
            Assert.That(panel.transform.Find("LeftPortrait").GetComponent<Image>().enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator ClosingDuringEntranceSilencesCallbackAndInputBlock()
        {
            DialoguePanel panel = Open();
            int completed = 0;
            panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Left, "旧演出") }), () => completed++);
            UIKit.ClosePanel(panel);
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(completed, Is.Zero);
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator PreviewCapturesTheActualPrefab()
        {
            DialoguePanel panel = Open();
            panel.Play(Resources.Load<DialogueScriptAsset>("Dialogue/DialoguePreview").CreateScript());
            yield return Ready(panel);
            yield return new WaitForSecondsRealtime(.25f);
            panel.Advance();
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("output/dialogue-preview.png"));
            yield return null;
        }
        private static DialoguePanel Open() => UIKit.OpenPanel<DialoguePanel>(UILevel.Pop, tag: Tag, cachePolicy: PanelCachePolicy.Reusable);
        private static IEnumerator Ready(DialoguePanel panel) => Until(() => panel.transform.Find("DialogueBox/Continue").GetComponent<Button>().interactable);
        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Dialogue timed out.");
                yield return null;
            }
        }
    }
}
