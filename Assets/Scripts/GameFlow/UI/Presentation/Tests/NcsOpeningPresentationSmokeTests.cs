#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NcsOpeningPresentationSmokeTests
    {
        private Scene debugScene;
        private PresentationDebugSceneController controller;
        private EventSystem[] previousSystems;

        [UnityTest]
        public IEnumerator SharedSceneShowsNcsContentAndRetainsDraftEntryPoints()
        {
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
            Assert.That(UnityEngine.Object.FindFirstObjectByType<GameFlowBootstrap>(), Is.Null);

            controller.PlayLetter();
            var letter = controller.CurrentPanel as FirstDayLetterPanel;
            Assert.That(letter, Is.Not.Null);
            StringAssert.Contains("欢迎回家", TextField(letter, "letterBody").text);
            Assert.That(TextField(letter, "signatureText").text, Is.EqualTo("—— 永远惦念你的叔叔"));
            Assert.That(TextField(letter, "letterBody").richText, Is.False);
            letter.Play(new FirstDayLetterContent("注入的信件 <不是标签>", "测试落款"), null);
            Assert.That(TextField(letter, "letterBody").text, Is.EqualTo("注入的信件 <不是标签>"));
            Assert.That(TextField(letter, "signatureText").text, Is.EqualTo("测试落款"));
            letter.Play(null);
            StringAssert.Contains("欢迎回家", TextField(letter, "letterBody").text);

            controller.PlayDialogue();
            yield return new WaitForSecondsRealtime(.8f);
            var dialogue = controller.CurrentPanel as DialoguePanel;
            Assert.That(dialogue, Is.Not.Null);
            Assert.That(TextField(dialogue, "titleText").text, Is.EqualTo("初来乍到 · 沸点咖啡馆"));
            StringAssert.Contains("你推开沸点咖啡馆", TextField(dialogue, "dialogueText").text);
            controller.PlayHardcodedDialogue();
            yield return new WaitForSecondsRealtime(.8f);
            dialogue = controller.CurrentPanel as DialoguePanel;
            Assert.That(TextField(dialogue, "titleText").text, Is.EqualTo("咖啡馆 · 不期而至的访客"));
            controller.PlayHardcodedLetter();
            Assert.That(controller.CurrentPanel, Is.TypeOf<FirstDayLetterPanel>());
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (controller != null) controller.StopCurrent();
            if (debugScene.IsValid() && debugScene.isLoaded) yield return SceneManager.UnloadSceneAsync(debugScene);
            if (previousSystems != null)
                foreach (EventSystem system in previousSystems) if (system != null) system.enabled = true;
        }

        private static TMP_Text TextField(object panel, string name) => (TMP_Text)panel.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
    }
}
#endif
