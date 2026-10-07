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
            Assert.That(buttons.Length, Is.EqualTo(13));
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
        public IEnumerator AllElevenSamplesOpenAndStopWithoutDayFlow()
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
        public IEnumerator QWERKeyboardShortcutsLaunchTheExpectedPanels()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>("PresentationDebugTestKeyboard");
            var background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            try
            {
                Key[] keys = { Key.Q, Key.W, Key.E, Key.R };
                Type[] types = { typeof(DateChangePanel), typeof(FirstDayLetterPanel), typeof(DialoguePanel), typeof(PreBusinessPanel) };
                for (int i = 0; i < keys.Length; i++)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[i]));
                    yield return null;
                    yield return null;
                    Assert.That(controller.CurrentPanel.GetType(), Is.EqualTo(types[i]));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                Assert.That(controller.CurrentPanel, Is.Null);
            }
            finally
            {
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator CaptureActualSharedSceneAndDialogue()
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("output/presentation-debug-room.png"));
            yield return null;
            controller.PlayDialogue();
            yield return new WaitForSecondsRealtime(.7f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("output/presentation-debug-dialogue.png"));
            yield return null;
        }
    }
}
#endif
