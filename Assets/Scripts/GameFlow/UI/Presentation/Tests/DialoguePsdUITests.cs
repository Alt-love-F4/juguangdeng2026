#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class DialoguePsdUITests
    {
        private Scene scene;
        private DialoguePanel panel;
        private float timeScale;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeScale = Time.timeScale;
            scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (canvas.name == "DebugHelpOverlay") canvas.gameObject.SetActive(false);
            }
            panel = UIKit.OpenPanel<DialoguePanel>(UILevel.Pop, tag: "dialogue-psd-tests", cachePolicy: PanelCachePolicy.Transient);
            Assert.That(panel, Is.Not.Null);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UIKit.ClosePanelsByTag("dialogue-psd-tests");
            Time.timeScale = timeScale;
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        private static DialogueScript Script(string text, IReadOnlyList<DialogueOption> options = null)
        {
            // Null right actor explicitly replaces the compatibility default; center the PSD portrait as authored.
            return new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Left, text, replaceRight: true) },
                options: options);
        }

        private DialogueScript TwoCharacterScript(string text)
        {
            return new DialogueScript(
                new[] { new DialogueLine(DialogueSpeaker.Left, text) },
                new DialogueCharacter("left", "左侧角色", panel.LeftPortrait.sprite),
                new DialogueCharacter("right", "右侧角色", panel.RightPortrait.sprite));
        }
        private IEnumerator Ready()
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (!panel.IsReady) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
        }
        private TMP_Text Text(string name)
        {
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true)) if (text.name == name) return text;
            throw new InvalidOperationException(name);
        }
        [UnityTest]
        public IEnumerator PsdLayoutAndButtonsKeepAuthoredBounds()
        {
            panel.Play(Script("你知道的。")); yield return Ready();
            RectTransform artwork = panel.transform.Find("EffectStage/Artwork") as RectTransform;
            RectTransform box = artwork.Find("DialogueBox") as RectTransform;
            Assert.That(panel.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
            Assert.That(box.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(box.anchorMin.x, Is.EqualTo((2050.5f - 992.5f) / 4096f).Within(.001f));
            Assert.That(box.anchorMax.x, Is.EqualTo((2050.5f + 992.5f) / 4096f).Within(.001f));
            Assert.That(box.pivot.y, Is.Zero);
            Assert.That(artwork.Find("DialogueOptions/Viewport/Content").GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
            Assert.That(artwork.Find("DialogueOptions/Viewport/Content").GetComponent<ContentSizeFitter>(), Is.Not.Null);
            Assert.That(box.Find("Continue").GetComponent<Button>(), Is.Not.Null);
            Assert.That(box.Find("Skip").GetComponent<Button>(), Is.Not.Null);
            panel.Advance(); yield return null;
            Assert.That(panel.IsTyping, Is.False);
            yield return new WaitForEndOfFrame(); Capture("output/dialogue-psd-short.png");
        }

        [UnityTest]
        public IEnumerator TwoCharacterLayoutSeparatesPortraitSlotsAndKeepsBackgroundOptional()
        {
            panel.Play(TwoCharacterScript("两位角色应分别位于左右区域。"));
            yield return Ready();

            Assert.That(panel.CenterPortrait.enabled, Is.False);
            Assert.That(panel.transform.Find("EffectStage/Artwork/PsdCafeBackground").gameObject.activeSelf, Is.False);
            Assert.That(panel.transform.Find("EffectStage/Artwork/Background").GetComponent<RawImage>().enabled, Is.False);

            RectTransform left = panel.LeftPortrait.transform.parent as RectTransform;
            RectTransform right = panel.RightPortrait.transform.parent as RectTransform;
            Assert.That(left.anchorMax.x, Is.LessThan(right.anchorMin.x));
            Assert.That(left.anchorMax.x, Is.EqualTo(.40f).Within(.001f));
            Assert.That(right.anchorMin.x, Is.EqualTo(.60f).Within(.001f));
        }
        [UnityTest]
        public IEnumerator LongOptionsGrowWithoutFontShrinkingAndManyChoicesScroll()
        {
            var options = new List<DialogueOption>
            {
                new DialogueOption("short", "我不知道"),
                new DialogueOption("long", "这是一段需要自动换行的长选项。" + new string('长', 100)),
                new DialogueOption("locked", "你是说那件事吗？", enabled: false, requirementHint: "需要更多情报")
            };
            for (int i = 3; i < 12; i++) options.Add(new DialogueOption("extra" + i, "额外选项 " + i));
            panel.Play(Script("你知道的。", options)); yield return Ready(); panel.Advance(); yield return null;
            Assert.That(panel.IsChoosing, Is.True);
            DialogueOptionView[] views = panel.GetComponentsInChildren<DialogueOptionView>();
            Assert.That(views.Length, Is.EqualTo(options.Count));
            RectTransform shortRow = (RectTransform)views[0].transform, longRow = (RectTransform)views[1].transform;
            Assert.That(longRow.rect.height, Is.GreaterThan(shortRow.rect.height * 2));
            Assert.That(views[1].Label.enableAutoSizing, Is.False);
            Assert.That(views[1].Label.fontSize, Is.EqualTo(views[0].Label.fontSize));
            Assert.That(views[2].Button.interactable, Is.False);
            Assert.That(panel.SelectOption("locked"), Is.False);
            ScrollRect scroll = panel.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            yield return new WaitForEndOfFrame(); Capture("output/dialogue-psd-many-options.png");
            scroll.verticalNormalizedPosition = 0;
            yield return null;
            Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(0));
        }
        [UnityTest]
        public IEnumerator TwoChoicesMatchReferenceAndSelectionRepliesAtTimeScaleZero()
        {
            Time.timeScale = 0;
            panel.Play(Script("你知道的。", new[]
            {
                new DialogueOption("locked", "你是说那件事吗？", enabled: false, requirementHint: "需要更多情报"),
                new DialogueOption("reply", "我不知道", new DialogueLine(DialogueSpeaker.Left, "那么，就让我从头说起。"))
            }));
            yield return Ready(); panel.Advance(); yield return null;
            yield return new WaitForEndOfFrame(); Capture("output/dialogue-psd-options.png");
            Assert.That(panel.IsChoosing, Is.True);
            Assert.That(panel.SelectOption("reply"), Is.True);
            Assert.That(panel.SelectOption("reply"), Is.False);
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(panel.LineIndex, Is.EqualTo(1));
            Assert.That(Text("Dialogue").text, Is.EqualTo("那么，就让我从头说起。"));
            Assert.That(panel.IsChoosing, Is.False);
        }
        [UnityTest]
        public IEnumerator LongDialogueIsThreeLinesPerPageAndSkipNeverSelectsChoices()
        {
            panel.Play(Script(new string('长', 2000)));
            yield return Ready();
            TMP_Text text = Text("Dialogue");
            Assert.That(text.textInfo.pageCount, Is.GreaterThan(1));
            foreach (TMP_PageInfo page in text.textInfo.pageInfo)
                if (page.lastCharacterIndex > 0) Assert.That(page.lastCharacterIndex - page.firstCharacterIndex, Is.LessThan(100));
            panel.Advance(); yield return null;
            yield return new WaitForEndOfFrame(); Capture("output/dialogue-psd-long.png");
            int done = 0;
            panel.Play(Script("需要选择", new[] { new DialogueOption("a", "不要自动选择") }), () => done++);
            yield return Ready(); panel.Skip();
            Assert.That(panel.IsChoosing, Is.True);
            Assert.That(panel.SelectedOptionId, Is.Empty);
            Assert.That(done, Is.Zero);
            panel.Stop(); yield return new WaitForSecondsRealtime(.6f);
            Assert.That(done, Is.Zero);
        }
        private static void Capture(string path)
        {
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllBytes(Path.GetFullPath(path), image.EncodeToPNG());
            }
            finally { UnityEngine.Object.Destroy(image); }
        }
    }
}
#endif
