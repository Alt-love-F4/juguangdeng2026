#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Spotlight.Narrative;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class PresentationDebugSceneTests
    {
        private Scene debugScene;
        private PresentationDebugSceneController controller;
        private float oldTime;
        private EventSystem[] previousSystems;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldTime = Time.timeScale;
            previousSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (EventSystem system in previousSystems) system.enabled = false;
            debugScene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            foreach (GameObject root in debugScene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<PresentationDebugSceneController>();
                if (found != null) controller = found;
            }
            Assert.That(controller, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (controller != null) controller.StopCurrent();
            if (debugScene.IsValid() && debugScene.isLoaded) yield return SceneManager.UnloadSceneAsync(debugScene);
            foreach (EventSystem system in previousSystems) if (system != null) system.enabled = true;
            Time.timeScale = oldTime;
        }

        [UnityTest]
        public IEnumerator OpeningProloguePreviewUsesSharedSceneAndRemainsIndependent()
        {
            controller.PlayOpeningPrologue();
            yield return null;
            var panel = controller.CurrentPanel as OpeningProloguePanel;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.BeatCount, Is.EqualTo(8));
            Assert.That(panel.IsPlaying, Is.True);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<GameFlowBootstrap>(), Is.Null);
            controller.StopCurrent();
            yield return null;
            Assert.That(controller.CurrentPanel, Is.Null);
        }

        [UnityTest]
        public IEnumerator CafeDemoConversationsPauseUntilAdvancedAndClosed()
        {
            controller.StopCurrent();
            Assert.That(PresentationPauseRegistry.IsPaused, Is.False, "Fixture must start without another presentation owner.");
            float scale = Time.timeScale;
            var configuration = Spotlight.Narrative.LubanNarrativeLoader.Load();
            foreach (string key in new[] { "DEMO_CAFE_BEFORE", "DEMO_CAFE_SERVED", "DEMO_CAFE_CHAT" })
            {
                var data = new NarrativeConversationPresentationData();
                var pages = configuration.presentations.conversationPages.FindAll(page => page.presentationKey == key);
                pages.Sort((a, b) => a.seq.CompareTo(b.seq));
                foreach (var page in pages)
                {
                    var lines = configuration.presentations.conversationLines.FindAll(line => line.pageId == page.pageId);
                    lines.Sort((a, b) => a.seq.CompareTo(b.seq));
                    foreach (var line in lines)
                    {
                        var text = configuration.texts.Find(item => item.key == line.textKey);
                        Assert.That(text, Is.Not.Null, key + ": missing text " + line.textKey);
                        Assert.That(text.content, Is.Not.Empty, key);
                        data.lines.Add(new NarrativeConversationPresentationLine
                        {
                            speakerKey = line.speakerKey, side = line.side, emote = line.emote,
                            text = text.content, accentFxKey = line.accentFxKey, accentTargetKey = line.accentTargetKey
                        });
                    }
                }
                Assert.That(data.lines.Count, Is.GreaterThan(0), key + " must contain real configured dialogue.");
                bool completed = false;
                DialoguePanel panel = null;
                try
                {
                    Assert.That(NarrativeConversationEventPlayback.Play(default, data, () => completed = true), Is.True, key);
                    panel = UIKit.GetPanel<DialoguePanel>();
                    Assert.That(panel, Is.Not.Null, key);
                    Assert.That(panel.IsPlaying, Is.True, key);
                    Assert.That(PresentationPauseRegistry.IsPaused, Is.True, key);
                    float deadline = Time.realtimeSinceStartup + 12f;
                    while (!completed)
                    {
                        Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), key + " failed to advance and close.");
                        Assert.That(Time.timeScale, Is.EqualTo(scale), key + " must not change timeScale.");
                        Assert.That(PresentationPauseRegistry.IsPaused, Is.True, key + " released before completion.");
                        if (panel.IsReady) panel.Advance();
                        yield return null;
                    }
                    Assert.That(panel.IsPlaying, Is.False, key);
                    Assert.That(PresentationPauseRegistry.IsPaused, Is.False, key + " leaked a pause owner.");
                    Assert.That(Time.timeScale, Is.EqualTo(scale));
                }
                finally
                {
                    if (panel != null) { panel.Stop(); UIKit.ClosePanel(panel); }
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SidebarBindingsAndLayeringAreCorrect()
        {
            Canvas room = controller.GetComponent<Canvas>();
            Assert.That(room.sortingOrder, Is.LessThan(700), "Debug backdrop must never cover the presentation.");
            Canvas help = null;
            foreach (GameObject root in debugScene.GetRootGameObjects())
                if (root.name == "DebugHelpOverlay") help = root.GetComponent<Canvas>();
            Assert.That(help, Is.Not.Null);
            Assert.That(help.sortingOrder, Is.GreaterThan(800));
            Button[] buttons = help.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Length, Is.EqualTo(33));
            foreach (Button button in buttons)
            {
                Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
                Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(controller));
            }
            controller.PlayPreBusiness();
            yield return null;
            buttons[2].onClick.Invoke();
            yield return null;
            Assert.That(controller.CurrentPanel, Is.InstanceOf<DialoguePanel>());
            Assert.That(UnityEngine.Object.FindFirstObjectByType<GameFlowBootstrap>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator SharedSceneChoiceButtonShowsOptionsAndReplaysCleanly()
        {
            Button launcher = null;
            foreach (GameObject root in debugScene.GetRootGameObjects())
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                    if (button.name == "Preview_15") launcher = button;
            Assert.That(launcher, Is.Not.Null);
            Assert.That(launcher.onClick.GetPersistentMethodName(0), Is.EqualTo("PlayDialogueOptions"));
            launcher.onClick.Invoke();
            var panel = controller.CurrentPanel as DialoguePanel;
            Assert.That(panel, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!panel.IsChoosing)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                if (panel.IsReady) panel.Advance();
                yield return null;
            }
            Assert.That(panel.LeftPortrait.enabled && panel.CenterPortrait.enabled && panel.RightPortrait.enabled, Is.True);
            foreach (Image portrait in new[] { panel.LeftPortrait, panel.CenterPortrait, panel.RightPortrait })
                Assert.That(portrait.transform.localScale.x, Is.GreaterThan(0f));
            panel.Effects.SetFocus(DialogueSpeaker.Center, immediate: true);
            panel.Effects.Tick(0f);
            Assert.That(panel.CenterPortrait.color.r, Is.GreaterThan(panel.LeftPortrait.color.r));
            Assert.That(panel.CenterPortrait.color.r, Is.GreaterThan(panel.RightPortrait.color.r));
            StringAssert.Contains(NarrativeCharacterResolver.Find("thibaut").displayName, string.Join(" ",
                Array.ConvertAll(panel.GetComponentsInChildren<TMP_Text>(), item => item.text)));
            Transform options = panel.GetComponentInChildren<ScrollRect>().transform;
            Assert.That(options.gameObject.activeInHierarchy, Is.True);
            Button[] choices = options.GetComponentsInChildren<Button>();
            Assert.That(choices.Length, Is.EqualTo(3));
            Assert.That(choices[2].interactable, Is.False);
            StringAssert.Contains("需要更多情报", string.Join(" ",
                Array.ConvertAll(choices[2].GetComponentsInChildren<TMP_Text>(), item => item.text)));
            panel.Advance();
            Assert.That(panel.IsChoosing, Is.True, "Dialogue submit must not select an option.");
            Assert.That(panel.SelectedOptionId, Is.Empty);
            yield return new WaitForEndOfFrame();
            Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
            if (capture != null)
            {
                Directory.CreateDirectory(Path.GetFullPath("output"));
                File.WriteAllBytes(Path.GetFullPath("output/presentation-debug-options.png"), capture.EncodeToPNG());
                UnityEngine.Object.Destroy(capture);
            }
            controller.ToggleTimePause();
            choices[1].onClick.Invoke();
            Assert.That(panel.SelectedOptionId, Is.EqualTo("caution"));
            Assert.That(panel.SelectOption("support"), Is.False, "Only accept one choice.");
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(panel.IsChoosing, Is.False);
            Assert.That(panel.LineIndex, Is.EqualTo(DialoguePreviewDialogue.CreateOptions().Count));
            controller.ToggleTimePause();
            controller.ReplayLast();
            var replay = controller.CurrentPanel as DialoguePanel;
            Assert.That(replay, Is.Not.Null);
            Assert.That(replay.SelectedOptionId, Is.Empty);
            Assert.That(replay.IsChoosing, Is.False);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<GameFlowBootstrap>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator ThirdPortraitEffectsReplacementAndTwoPersonReplayKeepOriginalOrientation()
        {
            controller.PlayDialogueOptions();
            var panel = controller.CurrentPanel as DialoguePanel;
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!panel.IsReady) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            Assert.That(panel.CenterPortrait.enabled, Is.True);
            var centerFx = panel.PlayEffect(PresentationEffectIds.AccentShakeStartle, "thibaut");
            var screenFx = panel.PlayEffect(PresentationEffectIds.ShakeLight);
            Assert.That(centerFx.Status, Is.EqualTo(PresentationEffectStatus.Playing));
            Assert.That(screenFx.Status, Is.EqualTo(PresentationEffectStatus.Playing));
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(centerFx.IsCompleted, Is.True);
            Assert.That(screenFx.IsCompleted, Is.True);
            yield return new WaitForEndOfFrame();
            CaptureScreenshot("output/presentation-three-portraits.png");

            DialogueScript sample = DialoguePreviewDialogue.CreateOptions();
            var replacement = NarrativeCharacterResolver.CreateDialogueCharacter("player");
            panel.Play(new DialogueScript(new[]
            {
                new DialogueLine(DialogueSpeaker.Center, ""),
                new DialogueLine(DialogueSpeaker.Center, "新线人已经到场", replaceCenter: true, centerCharacter: replacement),
                new DialogueLine(DialogueSpeaker.Left, "中间角色离开", replaceCenter: true)
            }, sample.LeftCharacter, sample.RightCharacter, centerCharacter: sample.CenterCharacter));
            while (!panel.IsReady) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            panel.Advance();
            yield return null;
            while (!panel.IsReady) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            Assert.That(panel.CenterCharacter, Is.SameAs(replacement));
            Assert.That(panel.CenterPortrait.sprite, Is.SameAs(replacement.Portrait));
            Assert.That(panel.CenterPortrait.transform.localScale.x, Is.GreaterThan(0f));
            panel.Advance();
            yield return null;
            panel.Advance();
            yield return null;
            while (!panel.IsReady) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            Assert.That(panel.CenterCharacter, Is.Null);
            Assert.That(panel.CenterPortrait.enabled, Is.False);
            panel.Play(DialoguePreviewDialogue.Create());
            while (!panel.IsReady) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            Assert.That(panel.CenterPortrait.enabled, Is.False);
            Assert.That(panel.RightPortrait.transform.localScale.x, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator AllSamplesOpenAndStopWithoutDayFlow()
        {
            foreach (PresentationDebugSceneController.Preview preview in Enum.GetValues(typeof(PresentationDebugSceneController.Preview)))
            {
                controller.PlayPreview(preview);
                Assert.That(controller.CurrentPanel, Is.Not.Null, preview.ToString());
                Assert.That(controller.CurrentPanel.Tag, Is.EqualTo(PresentationDebugSceneController.DebugTag));
                yield return null;
                controller.StopCurrent();
                yield return null;
                Assert.That(controller.CurrentPanel, Is.Null);
                Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator DateCompletesAutomaticallyAndOnlyItsOwnCallbackUpdatesStatus()
        {
            controller.PlayDateChange();
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.That(controller.CurrentPanel, Is.Null);
            controller.PlayDateChange();
            yield return null;
            controller.PlayLetter();
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.That(controller.CurrentPanel, Is.InstanceOf<FirstDayLetterPanel>());
        }

        [UnityTest]
        public IEnumerator PauseHelpReplayAndSceneExitRestoreState()
        {
            controller.ToggleTimePause();
            Assert.That(Time.timeScale, Is.Zero);
            controller.PlayPreBusiness();
            yield return new WaitForSecondsRealtime(.5f);
            var first = controller.CurrentPanel;
            controller.ReplayLast();
            Assert.That(controller.CurrentPanel, Is.Not.SameAs(first));
            controller.ToggleHelp();
            Assert.That(controller.HelpVisible, Is.False);
            controller.ResetHelp();
            Assert.That(controller.HelpVisible, Is.True);
            controller.gameObject.SetActive(false);
            yield return null;
            Assert.That(controller.CurrentPanel, Is.Null);
            Assert.That(Time.timeScale, Is.EqualTo(oldTime));
        }

        [UnityTest]
        public IEnumerator QWERShortcutEndpointsLaunchTheExpectedPanels()
        {
            Action[] launchers = { controller.PlayDateChange, controller.PlayLetter, controller.PlayDialogue, controller.PlayPreBusiness };
            Type[] types = { typeof(DateChangePanel), typeof(FirstDayLetterPanel), typeof(DialoguePanel), typeof(PreBusinessPanel) };
            for (int i = 0; i < launchers.Length; i++)
            {
                launchers[i]();
                yield return null;
                Assert.That(controller.CurrentPanel, Is.Not.Null);
                Assert.That(controller.CurrentPanel.GetType(), Is.EqualTo(types[i]));
                controller.StopCurrent();
                yield return null;
            }
            Assert.That(controller.CurrentPanel, Is.Null);
        }

        // Deliberately small smoke check: same shared scene, no full narrative/regression run.
        [UnityTest]
        public IEnumerator RemainingContentPanelsSmoke()
        {
            controller.PlayReading();
            yield return new WaitForSecondsRealtime(.65f);
            ReadingPanel reading = controller.CurrentPanel as ReadingPanel;
            Assert.That(reading, Is.Not.Null);
            Assert.That(reading.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.01f));
            yield return new WaitForEndOfFrame();
            CaptureScreenshot("output/presentation-reading-smoke.png");
            if (reading.IsTyping) { reading.Advance(); yield return null; }
            reading.Advance();
            Assert.That(reading.LineIndex, Is.EqualTo(1));
            yield return null;
            reading.Back();
            Assert.That(reading.LineIndex, Is.EqualTo(0));
            yield return null;
            reading.Cancel();
            yield return null;
            Assert.That(controller.CurrentPanel, Is.Null);

            controller.PlayNotice();
            yield return new WaitForSecondsRealtime(.4f);
            NoticePanel notice = controller.CurrentPanel as NoticePanel;
            Assert.That(notice, Is.Not.Null);
            Assert.That(notice.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.01f));
            Assert.That(notice.GetComponentInChildren<Slider>().value, Is.EqualTo(.62f).Within(.01f));
            yield return new WaitForEndOfFrame();
            CaptureScreenshot("output/presentation-notice-smoke.png");
            notice.Confirm();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(controller.CurrentPanel, Is.Null);

            controller.PlayCutScene();
            yield return new WaitForSecondsRealtime(.4f);
            CutScenePanel cutScene = controller.CurrentPanel as CutScenePanel;
            Assert.That(cutScene, Is.Not.Null);
            Assert.That(cutScene.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.01f));
            yield return new WaitForEndOfFrame();
            CaptureScreenshot("output/presentation-cutscene-smoke.png");
            cutScene.UpdateSkipHold(true, .3f);
            Assert.That(cutScene.SkipProgress, Is.GreaterThan(0f));
            cutScene.UpdateSkipHold(false, 0f);
            Assert.That(cutScene.SkipProgress, Is.Zero);
            cutScene.UpdateSkipHold(true, CutScenePresentationSettings.SkipHoldSeconds + .1f);
            Assert.That(cutScene.WasSkipped, Is.True);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(controller.CurrentPanel, Is.Null);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<GameFlowBootstrap>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator CaptureActualSharedSceneAndDialogue()
        {
            yield return new WaitForEndOfFrame();
            CaptureScreenshot("output/presentation-debug-room.png");
            yield return null;
            controller.PlayDialogue();
            yield return new WaitForSecondsRealtime(.7f);
            yield return new WaitForEndOfFrame();
            CaptureScreenshot("output/presentation-debug-dialogue.png");
            yield return null;
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
#endif
