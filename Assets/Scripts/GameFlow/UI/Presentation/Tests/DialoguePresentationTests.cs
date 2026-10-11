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
            Assert.That(FindImage(prefab, "LeftPortrait").sprite, Is.Not.Null);
            Assert.That(FindImage(prefab, "RightPortrait").sprite, Is.Not.Null);
            Assert.That(FindText(prefab, "Dialogue").font, Is.Not.Null);
            DialogueScript sample = DialoguePreviewDialogue.Create();
            Assert.That(sample.Count, Is.GreaterThan(2));
            Assert.That(sample.LeftCharacter.Portrait, Is.Not.Null);
            Assert.That(sample.RightCharacter.Portrait, Is.Not.Null);
        }

        [Test]
        public void OptionPreviewContainsEnabledDisabledChoicesWithoutNcsResultExecution()
        {
            DialogueScript script = DialoguePreviewDialogue.CreateOptions();
            Assert.That(script.CenterCharacter, Is.Not.Null);
            Assert.That(script.CenterCharacter.Portrait, Is.Not.Null);
            Assert.That(script[1].Speaker, Is.EqualTo(DialogueSpeaker.Center));
            Assert.That((int)DialogueSpeaker.Right, Is.EqualTo(2), "Preserve existing serialized enum values.");
            Assert.That(script.Options.Count, Is.EqualTo(3));
            Assert.That(script.Options[0].Enabled, Is.True);
            Assert.That(script.Options[1].ResultKey, Is.EqualTo("debug_caution"));
            Assert.That(script.Options[2].Enabled, Is.False);
            Assert.That(script.Options[2].RequirementHint, Is.EqualTo("需要更多情报"));
            var callerOptions = new List<DialogueOption> { script.Options[0] };
            var snapshot = new DialogueScript(new[] { script[0] }, options: callerOptions);
            callerOptions.Clear();
            Assert.That(snapshot.Options.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => new DialogueScript(new[] { script[0] },
                options: new[] { script.Options[0], script.Options[0] }));
        }

        [UnityTest]
        public IEnumerator OptionsAppearAfterTheLastLineAndSelectionStartsResponse()
        {
            DialoguePanel panel = Open();
            panel.Play(DialoguePreviewDialogue.CreateOptions());
            yield return Ready(panel);

            panel.Advance();
            yield return null;
            panel.Advance();
            yield return null;
            panel.Advance();
            yield return null;

            Assert.That(panel.IsChoosing, Is.True);
            Assert.That(panel.SelectOption("locked"), Is.False);
            Assert.That(panel.SelectOption("support"), Is.True);
            yield return new WaitForSecondsRealtime(.55f);
            Assert.That(panel.SelectedOptionId, Is.EqualTo("support"));
            Assert.That(panel.IsChoosing, Is.False);
            Assert.That(panel.LineIndex, Is.EqualTo(2));
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
            Assert.That(FindText(panel.gameObject, "Speaker").text, Is.EqualTo("印刷工"));
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
            Assert.That(panel.RightPortrait.enabled, Is.False);
            panel.Advance();
            panel.Stop();
            panel.Play(new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Right, "新演出") }));
            yield return Ready(panel);
            Assert.That(panel.RightPortrait.enabled, Is.True);
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
            Assert.That(FindText(panel.gameObject, "Speaker").text, Is.EqualTo("新访客"));
            Assert.That(panel.LeftPortrait.enabled, Is.False);
            Assert.That(FindImage(panel.gameObject, "LeftPortraitFrame").enabled, Is.False);
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
            panel.Play(DialoguePreviewDialogue.Create());
            yield return Ready(panel);
            yield return new WaitForSecondsRealtime(.25f);
            panel.Advance();
            yield return new WaitForEndOfFrame();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetFullPath("output"));
            CaptureScreenshot("output/dialogue-preview.png");
            yield return null;
        }
        private static Image FindImage(GameObject go, string name)
        {
            foreach (Image item in go.GetComponentsInChildren<Image>(true)) if (item.name == name) return item;
            throw new InvalidOperationException("Missing image: " + name);
        }
        private static TMP_Text FindText(GameObject go, string name)
        {
            foreach (TMP_Text item in go.GetComponentsInChildren<TMP_Text>(true)) if (item.name == name) return item;
            throw new InvalidOperationException("Missing text: " + name);
        }
        private static DialoguePanel Open() => UIKit.OpenPanel<DialoguePanel>(UILevel.Pop, tag: Tag, cachePolicy: PanelCachePolicy.Reusable);
        private static IEnumerator Ready(DialoguePanel panel) => Until(() => panel.IsReady);
        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Dialogue timed out.");
                yield return null;
            }
        }
        private static void CaptureScreenshot(string path)
        {
            // Save synchronously to avoid async screenshot writers racing repeated PlayMode runs.
            Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
            if (capture == null) throw new InvalidOperationException("Game view capture unavailable.");
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(path), capture.EncodeToPNG());
            }
            finally { UnityEngine.Object.Destroy(capture); }
        }
    }
}
