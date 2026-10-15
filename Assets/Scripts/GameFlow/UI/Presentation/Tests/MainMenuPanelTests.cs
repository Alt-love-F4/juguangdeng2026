#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class MainMenuPanelTests
    {
        private const string PanelPath = "Assets/Resources/Art/UIPrefab/MainMenuPanel.prefab";

        [Test]
        public void FormalMenuRetainsAllFourFlowBindingsAndPsdArtwork()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            Assert.That(prefab, Is.Not.Null);
            var panel = prefab.GetComponent<MainMenuPanel>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(4));
            Assert.That(panel.AutoFocusOnShow, Is.False);
            var fields = new SerializedObject(panel);
            Assert.That(fields.FindProperty("mAutoFocusOnShow").boolValue, Is.False);
            string[] names = { "savesButton", "startButton", "settingsButton", "quitButton" };
            for (int i = 0; i < names.Length; i++)
            {
                var button = fields.FindProperty(names[i]).objectReferenceValue as Button;
                Assert.That(button, Is.Not.Null, names[i]);
                Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(button.targetGraphic, Is.Not.Null);
                Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            }
            int artworkCount = 0;
            foreach (Image image in prefab.GetComponentsInChildren<Image>(true))
                if (image.sprite != default)
                {
                    artworkCount++;
                    Assert.That(AssetDatabase.GetAssetPath(image.sprite), Does.StartWith("Assets/Art/PSD/主界面/"));
                }
            Assert.That(artworkCount, Is.EqualTo(12));
        }

        [Test]
        public void ContinueButtonUsesTheSameNativeFeedbackAsOtherMenuButtons()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            var fields = new SerializedObject(prefab.GetComponent<MainMenuPanel>());
            var saves = (Button)fields.FindProperty("savesButton").objectReferenceValue;
            var start = (Button)fields.FindProperty("startButton").objectReferenceValue;

            Assert.That(saves.GetType(), Is.EqualTo(typeof(Button)));
            Assert.That(saves.GetComponent<ContinueGameHoverOverlay>(), Is.Null);
            Assert.That(saves.targetGraphic, Is.SameAs(saves.GetComponent<Image>()));
            Assert.That(saves.transition, Is.EqualTo(Selectable.Transition.ColorTint));
            Assert.That(saves.colors, Is.EqualTo(start.colors));
            Assert.That(saves.navigation.mode, Is.EqualTo(start.navigation.mode));
            Assert.That(saves.GetComponent<ButtonClickFeedback>(), Is.Not.Null);
        }

        [Test]
        public void MenuLayoutIsFiniteAndCoversCanvasWithoutScreenMotion()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            foreach (RectTransform rect in prefab.GetComponentsInChildren<RectTransform>(true))
            {
                AssertFinite(rect.anchorMin, rect.name);
                AssertFinite(rect.anchorMax, rect.name);
                AssertFinite(rect.anchoredPosition, rect.name);
                AssertFinite(rect.sizeDelta, rect.name);
            }
            var fit = prefab.GetComponentInChildren<AspectRatioFitter>(true);
            Assert.That(fit.aspectRatio, Is.EqualTo(16f / 9f).Within(.001f));
            Assert.That(fit.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
            var fields = new SerializedObject(prefab.GetComponent<MainMenuPanel>());
            Assert.That(fields.FindProperty("showOffset").floatValue, Is.Zero);
            Assert.That(fields.FindProperty("hideOffset").floatValue, Is.Zero);
            Assert.That(fields.FindProperty("showStartScale").floatValue, Is.EqualTo(1f));
            Assert.That(fields.FindProperty("hideEndScale").floatValue, Is.EqualTo(1f));
        }

        private static void AssertFinite(Vector2 value, string context)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False, context);
        }
    }

    public sealed class MainMenuPointerTests
    {
        private const string TestTag = "main-menu-pointer-tests";
        private Scene scene;
        private MainMenuPanel panel;
        private EventSystem eventSystem;
        private EventSystem[] previousSystems;
        private float previousTimeScale;

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
                EventSystem found = root.GetComponentInChildren<EventSystem>();
                if (found != null) eventSystem = found;
            }
            Assert.That(eventSystem, Is.Not.Null);
            // Dispatch pointer events deterministically, without the real cursor
            // overwriting the test's hover state on the next frame.
            foreach (BaseInputModule module in eventSystem.GetComponents<BaseInputModule>())
                module.enabled = false;
            panel = UIKit.OpenPanel<MainMenuPanel>(UILevel.Pop, tag: TestTag,
                cachePolicy: PanelCachePolicy.Transient);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (panel.State == PanelState.Opening)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
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
        public IEnumerator SystemMenusRenderAboveGameplayWhileSettingsRemainAboveMenus()
        {
            Canvas root = panel.GetComponentInParent<Canvas>().rootCanvas;
            Assert.That(root.sortingOrder, Is.GreaterThan(2000));
            Assert.That(panel.Level, Is.EqualTo(UILevel.Pop));

            var pause = UIKit.OpenPanel<PauseMenuPanel>(UILevel.Pop, tag: TestTag,
                cachePolicy: PanelCachePolicy.Transient);
            while (pause.State == PanelState.Opening) yield return null;
            Assert.That(pause.GetComponentInParent<Canvas>().rootCanvas, Is.SameAs(root));
            Assert.That(pause.transform.GetSiblingIndex(), Is.GreaterThan(panel.transform.GetSiblingIndex()));

            var settings = UIKit.OpenPanel<SettingsPanel>(UILevel.Pop, tag: TestTag,
                cachePolicy: PanelCachePolicy.Transient);
            while (settings.State == PanelState.Opening) yield return null;
            Assert.That(settings.GetComponentInParent<Canvas>().rootCanvas, Is.SameAs(root));
            Assert.That(settings.transform.GetSiblingIndex(), Is.GreaterThan(pause.transform.GetSiblingIndex()));
        }

        [UnityTest]
        public IEnumerator OpeningAndRefocusingMenuNeverKeepsAButtonSelected()
        {
            Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
            Assert.That(panel.AutoFocusOnShow, Is.False);
            Button button = panel.GetComponentInChildren<Button>();
            eventSystem.SetSelectedGameObject(button.gameObject);
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(button.gameObject));
            typeof(GameFlowPanel).GetMethod("OnFocus", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(panel, null);
            Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
            yield return new WaitForSecondsRealtime(.15f);
            AssertVisual(button, false);
        }

        [UnityTest]
        public IEnumerator AllFourButtonsHighlightOnlyWhileHoveredIncludingAfterClick()
        {
            Button[] buttons = panel.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Length, Is.EqualTo(4));
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            foreach (Button button in buttons)
            {
                Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));
                AssertVisual(button, false);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.15f);
                AssertVisual(button, true);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                Assert.That(eventSystem.currentSelectedGameObject, Is.Null,
                    "A pointer click must not create persistent selection: " + button.name);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                yield return new WaitForSecondsRealtime(.15f);
                AssertVisual(button, true);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.15f);
                AssertVisual(button, false);
                Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
            }
        }

        private static void AssertVisual(Button button, bool hovered)
        {
            Color expected = (hovered ? button.colors.highlightedColor : button.colors.normalColor)
                * button.colors.colorMultiplier;
            Color actual = button.targetGraphic.canvasRenderer.GetColor();
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.01f), button.name);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.01f), button.name);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.01f), button.name);
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(.01f), button.name);
        }
    }
}
#endif
