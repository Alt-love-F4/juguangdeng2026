#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
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
            Assert.That(buttons.Length, Is.EqualTo(27));
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
