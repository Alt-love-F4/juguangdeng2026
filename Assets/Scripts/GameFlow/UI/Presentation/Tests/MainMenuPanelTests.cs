#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
            var fields = new SerializedObject(panel);
            string[] names = { "savesButton", "startButton", "settingsButton", "quitButton" };
            string[] labels = { "继续游戏", "开始游戏", "设置", "退出游戏" };
            for (int i = 0; i < names.Length; i++)
            {
                var button = fields.FindProperty(names[i]).objectReferenceValue as Button;
                Assert.That(button, Is.Not.Null, names[i]);
                Assert.That(button.name, Is.EqualTo(labels[i]));
                Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(button.targetGraphic, Is.Not.Null);
                Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                Assert.That(button.navigation.selectOnDown, Is.Not.Null);
                Assert.That(button.navigation.selectOnUp, Is.Not.Null);
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
}
#endif
