using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class MapLayoutTests
    {
        private GameObject prefab, shared;
        private const string Path = "Assets/Resources/Art/UIPrefab/MapPanel.prefab";

        [SetUp]
        public void SetUp()
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            Assert.That(prefab, Is.Not.Null);
            shared = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Art/UIPrefab/GameplayHudPanel.prefab");
            Assert.That(shared, Is.Not.Null);
        }

        [Test]
        public void FixedControlsStayOutsideZoomedMap()
        {
            var map = prefab.transform.Find("MapViewport/MapContent");
            foreach (var name in new[] { "Title", "Compass", "ZoomControl" })
            {
                var control = prefab.transform.Find("HUD/" + name);
                Assert.That(control, Is.Not.Null, name);
                Assert.That(control.IsChildOf(map), Is.False, name);
            }
            Assert.That(prefab.transform.Find("HUD/ArtworkHud"), Is.Null, "Gameplay chrome belongs to the shared prefab.");
            Assert.That(shared.transform.Find("ArtworkHud"), Is.Not.Null);
            Assert.That(shared.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(((RectTransform)shared.transform).anchorMax, Is.EqualTo(Vector2.one));
            var title = (RectTransform)prefab.transform.Find("HUD/Title");
            Assert.That(title.anchorMin, Is.EqualTo(Vector2.zero));
            var compass = (RectTransform)prefab.transform.Find("HUD/Compass");
            Assert.That(compass.anchorMin, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void BothSidebarsStartHiddenWithIdenticalRightDock()
        {
            var inventory = (RectTransform)shared.transform.Find("InventoryBar");
            var tasks = (RectTransform)shared.transform.Find("TaskPanel");
            foreach (var sidebar in new[] { inventory, tasks })
            {
                Assert.That(sidebar.gameObject.activeSelf, Is.False);
                Assert.That(sidebar.anchorMin, Is.EqualTo(new Vector2(1, .5f)));
                Assert.That(sidebar.pivot, Is.EqualTo(new Vector2(1, .5f)));
                Assert.That(sidebar.sizeDelta, Is.EqualTo(new Vector2(550, 1000)));
                Assert.That(sidebar.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            }
        }

        [Test]
        public void SidebarsScrollContentWithoutScrollingHeaders()
        {
            var inventory = shared.transform.Find("InventoryBar");
            var tasks = shared.transform.Find("TaskPanel");
            Assert.That(inventory.Find("InventoryTitle").IsChildOf(inventory.Find("Viewport")), Is.False);
            Assert.That(tasks.Find("TaskTitle").IsChildOf(tasks.Find("TaskBody/TaskViewport")), Is.False);
            foreach (var content in new[] { inventory.Find("Viewport/Items"), tasks.Find("TaskBody/TaskViewport/TaskContent") })
            {
                var rect = (RectTransform)content;
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0, 1)));
                Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(1, 1)));
            }
        }

        [Test]
        public void ZoomHandleUsesInsetTravelAreaAndRaycastSurface()
        {
            var control = prefab.transform.Find("HUD/ZoomControl");
            var handleArea = (RectTransform)control.Find("HandleArea");
            Assert.That(handleArea, Is.Not.Null);
            Assert.That(handleArea.offsetMin.x, Is.GreaterThan(0));
            var handle = handleArea.Find("Handle");
            var slider = control.GetComponents<Component>().First(component => component.GetType().Name == "Slider");
            Assert.That(slider.GetType().GetProperty("handleRect").GetValue(slider), Is.EqualTo(handle));
            var image = control.GetComponents<Component>().First(component => component.GetType().Name == "Image");
            Assert.That(image.GetType().GetProperty("raycastTarget").GetValue(image), Is.True);
        }

        [Test]
        public void PopupUsesIndependentCardArtworkInsteadOfStretchedActionSheet()
        {
            var popup = prefab.transform.Find("Widgets/LocationActionPopup");
            var image = popup.GetComponents<Component>().First(component => component.GetType().Name == "Image");
            Assert.That(((Color)image.GetType().GetProperty("color").GetValue(image)).a, Is.EqualTo(0));
            Assert.That(((RectTransform)popup).sizeDelta.x, Is.EqualTo(820));
            var themeType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("Spotlight.GameFlow.MapVisualTheme")).First(type => type != null);
            var theme = Resources.Load("MapVisualTheme", themeType);
            foreach (string field in new[] { "actionEmblem", "actionDivider", "execute" })
                Assert.That(themeType.GetField(field).GetValue(theme), Is.Not.Null, field);
            var execute = (Sprite)themeType.GetField("execute").GetValue(theme);
            Assert.That(execute.rect.width, Is.GreaterThan(300));
            Assert.That(execute.rect.height, Is.GreaterThan(100));
            var panel = (Sprite)themeType.GetField("panel").GetValue(theme);
            Assert.That(panel.border.sqrMagnitude, Is.GreaterThan(0));
        }
    }
}
