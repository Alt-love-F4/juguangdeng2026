#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class SettingsDisplaySelectorTests
    {
        private const string PrefabPath = "Assets/Resources/Art/UIPrefab/SettingsPanel.prefab";

        [Test]
        public void BothSelectorsUseTheReferencePaletteAndNativeIndependentTemplates()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var fields = new SerializedObject(prefab.GetComponent<SettingsPanel>());
            var mode = (TMP_Dropdown)fields.FindProperty("windowModeDropdown").objectReferenceValue;
            var resolution = (TMP_Dropdown)fields.FindProperty("resolutionDropdown").objectReferenceValue;
            Assert.That(mode, Is.Not.Null);
            Assert.That(resolution, Is.Not.Null);
            Assert.That(mode.template, Is.Not.SameAs(resolution.template));
            Assert.That(mode.options.ConvertAll(option => option.text), Is.EqualTo(new[] { "无边框全屏", "窗口" }));
            foreach (TMP_Dropdown dropdown in new[] { mode, resolution })
            {
                Assert.That(dropdown.template.gameObject.activeSelf, Is.False);
                Assert.That(dropdown.captionText, Is.Not.Null);
                Assert.That(dropdown.itemText, Is.Not.Null);
                Assert.That(dropdown.navigation.mode, Is.EqualTo(Navigation.Mode.None));
                Assert.That(dropdown.captionText.color, Is.EqualTo(Color.white));
                Assert.That(dropdown.itemText.alignment, Is.EqualTo(TextAlignmentOptions.Center));
                Toggle item = dropdown.template.GetComponentInChildren<Toggle>(true);
                Assert.That(item, Is.Not.Null);
                Assert.That(item.colors.normalColor, Is.EqualTo(new Color(.24f, .21f, .19f)));
                Assert.That(item.colors.highlightedColor, Is.EqualTo(new Color(.55f, .19f, .15f)));
                Assert.That(item.graphic.name, Is.EqualTo("SelectionRule"));
                Assert.That(item.graphic.rectTransform.anchorMin.x, Is.Zero);
                Assert.That(item.graphic.rectTransform.anchorMax.x, Is.EqualTo(1f));
                Assert.That(item.graphic.rectTransform.sizeDelta.y, Is.EqualTo(4f));
                Assert.That(item.graphic.raycastTarget, Is.False);
                ScrollRect scroll = dropdown.template.GetComponent<ScrollRect>();
                Assert.That(scroll.viewport.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(scroll.viewport.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(scroll.verticalScrollbar, Is.Null);
            }
            var oldModeButton = (Button)fields.FindProperty("windowModeButton").objectReferenceValue;
            Assert.That(oldModeButton.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void WindowModeIndicesMatchTheirLabelsRatherThanTheOldCycleOrder()
        {
            var modes = (FullScreenMode[])typeof(SettingsPanel)
                .GetField("WindowModes", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.That(modes, Is.EqualTo(new[]
            {
                FullScreenMode.FullScreenWindow, FullScreenMode.Windowed
            }));
        }
    }

    public sealed class SettingsDisplaySelectorPlayModeTests
    {
        private const string TestTag = "settings-display-selector-tests";
        private Scene scene;
        private EventSystem[] previousSystems;
        private float previousTimeScale;
        private SettingsPanel panel;
        private TMP_Dropdown mode;
        private TMP_Dropdown resolution;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            previousSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (EventSystem system in previousSystems) system.enabled = false;
            scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var controller = root.GetComponentInChildren<PresentationDebugSceneController>();
                if (controller != null) controller.ToggleHelp();
                var system = root.GetComponentInChildren<EventSystem>();
                if (system != null)
                    foreach (BaseInputModule module in system.GetComponents<BaseInputModule>()) module.enabled = false;
            }
            // No bootstrap or flow data: selector preview never changes the real screen,
            // progresses DayFlow, or writes the player's settings.
            panel = UIKit.OpenPanel<SettingsPanel>(UILevel.Common, tag: TestTag,
                cachePolicy: PanelCachePolicy.Transient);
            yield return new WaitForSecondsRealtime(.35f);
            var fields = new SerializedObject(panel);
            mode = (TMP_Dropdown)fields.FindProperty("windowModeDropdown").objectReferenceValue;
            resolution = (TMP_Dropdown)fields.FindProperty("resolutionDropdown").objectReferenceValue;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UIKit.ClosePanelsByTag(TestTag);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (previousSystems != null)
                foreach (EventSystem system in previousSystems) if (system != null) system.enabled = true;
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator NativeListsShowAllOptionsAndUpdateTheirCaptions()
        {
            mode.Show();
            yield return new WaitForSecondsRealtime(.12f);
            Transform list = mode.transform.Find("Dropdown List");
            Assert.That(list, Is.Not.Null);
            Toggle[] items = list.GetComponentsInChildren<Toggle>();
            Assert.That(items.Length, Is.EqualTo(3));
            yield return new WaitForEndOfFrame();
            Capture("window-mode-options");
            items[1].isOn = true;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(mode.value, Is.EqualTo(1));
            Assert.That(mode.captionText.text, Is.EqualTo("窗口"));
            resolution.Show();
            yield return new WaitForSecondsRealtime(.12f);
            list = resolution.transform.Find("Dropdown List");
            Assert.That(list, Is.Not.Null);
            items = list.GetComponentsInChildren<Toggle>();
            Assert.That(items.Length, Is.EqualTo(resolution.options.Count));
            yield return new WaitForEndOfFrame();
            Capture("resolution-options");
            items[1].isOn = true;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(resolution.value, Is.EqualTo(1));
            Assert.That(resolution.captionText.text, Is.EqualTo(resolution.options[1].text));
        }

        [UnityTest]
        public IEnumerator HidingSettingsDismissesTheNativeListAndBlocker()
        {
            mode.Show();
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(mode.transform.Find("Dropdown List"), Is.Not.Null);
            panel.Hide();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(mode.transform.Find("Dropdown List"), Is.Null);
            Assert.That(resolution.transform.Find("Dropdown List"), Is.Null);
        }

        private static void Capture(string name)
        {
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            if (image == null) throw new InvalidOperationException("Game view capture unavailable.");
            try
            {
                string directory = Path.GetFullPath("output/settings-selectors");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
            }
            finally { Object.Destroy(image); }
        }
    }
}
#endif
