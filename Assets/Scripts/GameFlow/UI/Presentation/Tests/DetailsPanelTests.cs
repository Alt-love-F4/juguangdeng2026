#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class DetailsPanelTests
    {
        private Scene scene;
        private PresentationDebugSceneController controller;
        private EventSystem[] previousSystems;
        private EventSystem eventSystem;
        private float previousTime;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTime = Time.timeScale;
            previousSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (EventSystem system in previousSystems) system.enabled = false;
            scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<PresentationDebugSceneController>();
                if (found != default) controller = found;
                var system = root.GetComponentInChildren<EventSystem>();
                if (system != default) eventSystem = system;
            }
            Assert.That(controller, Is.Not.Null);
            Assert.That(eventSystem, Is.Not.Null);
            controller.ToggleHelp();
            controller.PlayDetails();
            yield return new WaitForSecondsRealtime(.35f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (controller != default) controller.StopCurrent();
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (EventSystem system in previousSystems) if (system != default) system.enabled = true;
            Time.timeScale = previousTime;
        }

        [UnityTest]
        public IEnumerator PlaceholderContentTabsAndScreenshots()
        {
            DetailsPanel panel = controller.CurrentPanel as DetailsPanel;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.IsModal, Is.True);
            Assert.That(panel.CurrentPage, Is.EqualTo(DetailsPanel.Page.MyInfo));
            Assert.That(panel.GetComponentsInChildren<DetailsHoverTarget>(true).Length, Is.EqualTo(10));
            for (int i = 0; i < 4; i++)
            {
                TMP_Text value = panel.transform.Find("Window/MyInfo/Attribute_" + i + "/Value").GetComponent<TMP_Text>();
                int score = int.Parse(value.text.Split('/')[0].Trim());
                Assert.That(score, Is.InRange(0, 20));
            }
            yield return new WaitForEndOfFrame();
            Capture("details-my-info");
            panel.transform.Find("Window/Tab_1").GetComponent<Button>().onClick.Invoke();
            Assert.That(panel.CurrentPage, Is.EqualTo(DetailsPanel.Page.Relationships));
            AssertPages(panel, "Relationships");
            yield return new WaitForEndOfFrame();
            Capture("details-relationships");
            panel.transform.Find("Window/Tab_2").GetComponent<Button>().onClick.Invoke();
            Assert.That(panel.CurrentPage, Is.EqualTo(DetailsPanel.Page.World));
            AssertPages(panel, "World");
            yield return new WaitForEndOfFrame();
            Capture("details-world");
            panel.ShowMyInfo();
            AssertPages(panel, "MyInfo");
            Assert.That(UnityEngine.Object.FindFirstObjectByType<GameFlowBootstrap>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator HoverDescriptionsHideOnExitTabSwitchAndClose()
        {
            DetailsPanel panel = (DetailsPanel)controller.CurrentPanel;
            GameObject attribute = panel.transform.Find("Window/MyInfo/Attribute_0").gameObject;
            var pointer = new PointerEventData(eventSystem) { position = ScreenPoint(attribute.transform) };
            ExecuteEvents.Execute(attribute, pointer, ExecuteEvents.pointerEnterHandler);
            Assert.That(panel.TooltipVisible, Is.True);
            Assert.That(panel.transform.Find("Window/Tooltip/Title").GetComponent<TMP_Text>().text, Is.EqualTo("语言"));
            yield return new WaitForEndOfFrame();
            Capture("details-attribute-tooltip");
            ExecuteEvents.Execute(attribute, pointer, ExecuteEvents.pointerExitHandler);
            Assert.That(panel.TooltipVisible, Is.False);
            GameObject badge = panel.transform.Find("Window/MyInfo/Specializations/Content/Specialization_5").gameObject;
            pointer.position = ScreenPoint(badge.transform);
            ExecuteEvents.Execute(badge, pointer, ExecuteEvents.pointerEnterHandler);
            Assert.That(panel.TooltipVisible, Is.True);
            Assert.That(panel.transform.Find("Window/Tooltip/Title").GetComponent<TMP_Text>().text, Is.EqualTo("灵感手记"));
            yield return new WaitForEndOfFrame();
            Capture("details-specialization-tooltip");
            panel.ShowRelationships();
            Assert.That(panel.TooltipVisible, Is.False);
            panel.ShowMyInfo();
            ExecuteEvents.Execute(attribute, pointer, ExecuteEvents.pointerEnterHandler);
            Assert.That(panel.TooltipVisible, Is.True);
            panel.RequestClose();
            Assert.That(panel.TooltipVisible, Is.False);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(controller.CurrentPanel, Is.Null);
        }

        [UnityTest]
        public IEnumerator WindowBlocksOutsideDismissalAndBothCloseButtonsWork()
        {
            DetailsPanel panel = (DetailsPanel)controller.CurrentPanel;
            RectTransform window = panel.transform.Find("Window") as RectTransform;
            // An empty interior point hits the window shield, not the full-screen outside button.
            Vector2 interior = RectTransformUtility.WorldToScreenPoint(null,
                window.TransformPoint(new Vector3(0, window.rect.yMax - 8f, 0)));
            GameObject hit = FirstHit(interior);
            Assert.That(hit, Is.Not.Null);
            Assert.That(hit.name, Is.Not.EqualTo("OutsideClose"));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit), Is.Null);
            panel.transform.Find("Window/CloseButton").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(controller.CurrentPanel, Is.Null);
            controller.PlayDetails();
            yield return new WaitForSecondsRealtime(.35f);
            Vector2 outside = new Vector2(Screen.width * .03f, Screen.height * .5f);
            hit = FirstHit(outside);
            Assert.That(hit.name, Is.EqualTo("OutsideClose"));
            var pointer = new PointerEventData(eventSystem) { position = outside, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(hit, pointer, ExecuteEvents.pointerClickHandler);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(controller.CurrentPanel, Is.Null);
        }

        [UnityTest]
        public IEnumerator PausedPreviewClosesAndReopensOnDefaultTab()
        {
            DetailsPanel panel = (DetailsPanel)controller.CurrentPanel;
            panel.ShowWorld();
            Time.timeScale = 0f;
            panel.RequestClose();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(controller.CurrentPanel, Is.Null);
            controller.ReplayLast();
            yield return new WaitForSecondsRealtime(.35f);
            panel = controller.CurrentPanel as DetailsPanel;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.CurrentPage, Is.EqualTo(DetailsPanel.Page.MyInfo));
            Assert.That(panel.TooltipVisible, Is.False);
            Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.01f));
        }

        [UnityTest]
        public IEnumerator DataSnapshotNormalizesAttributesAndCopiesCollections()
        {
            var source = new List<DetailsSpecializationData> { new DetailsSpecializationData("测试专精", "测试描述") };
            var data = new DetailsPanelData(attributes: new[]
            {
                new DetailsAttributeData(DetailsAttributeKind.Perception, 99, "知觉说明"),
                new DetailsAttributeData(DetailsAttributeKind.Language, -10, "语言说明")
            }, specializations: source);
            source.Clear();
            Assert.That(data.Attributes.Count, Is.EqualTo(4));
            Assert.That(data.Attributes[0].Kind, Is.EqualTo(DetailsAttributeKind.Language));
            Assert.That(data.Attributes[0].Value, Is.Zero);
            Assert.That(data.Attributes[1].Value, Is.Zero);
            Assert.That(data.Attributes[3].Value, Is.EqualTo(20));
            Assert.That(data.Specializations.Count, Is.EqualTo(1));
            Assert.That(data.Profile.Name, Is.Empty);
            Assert.Throws<ArgumentException>(() => new DetailsPanelData(attributes: new[]
            {
                new DetailsAttributeData(DetailsAttributeKind.Language, 1),
                new DetailsAttributeData(DetailsAttributeKind.Language, 2)
            }));
            Assert.Throws<ArgumentException>(() => new DetailsPanelData(specializations: new DetailsSpecializationData[] { null }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DetailsAttributeData((DetailsAttributeKind)99, 1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator UIKitOpenConsumesOwnDataClass()
        {
            controller.StopCurrent();
            var data = new DetailsPanelData(
                new DetailsProfileData("测试馆长", "自定义背景", "自定义介绍"),
                relationships: new DetailsSectionData("测试关系", "已提供关系", "关系内容"),
                world: new DetailsSectionData("测试世界", "已提供世界", "世界内容"));
            DetailsPanel panel = UIKit.OpenPanel<DetailsPanel>(UILevel.Pop, data: data,
                tag: PresentationDebugSceneController.DebugTag, cachePolicy: PanelCachePolicy.Transient);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(panel.CurrentData, Is.SameAs(data));
            Assert.That(panel, Is.InstanceOf<IDetailsPanel>());
            Assert.That(panel.transform.Find("Window/MyInfo/ProfileCard/Name").GetComponent<TMP_Text>().text, Is.EqualTo("测试馆长"));
            Assert.That(panel.transform.Find("Window/Relationships/Description").GetComponent<TMP_Text>().text, Is.EqualTo("关系内容"));
            Assert.That(panel.transform.Find("Window/World/Description").GetComponent<TMP_Text>().text, Is.EqualTo("世界内容"));
            Assert.That(panel.transform.Find("Window/Footer").GetComponent<TMP_Text>().text, Does.Not.Contain("占位"));
        }

        [UnityTest]
        public IEnumerator RefreshPreservesComponentReferencesAndUpdatesAvatarValuesAndHover()
        {
            DetailsPanel panel = (DetailsPanel)controller.CurrentPanel;
            TMP_Text name = panel.transform.Find("Window/MyInfo/ProfileCard/Name").GetComponent<TMP_Text>();
            TMP_Text value = panel.transform.Find("Window/MyInfo/Attribute_0/Value").GetComponent<TMP_Text>();
            GameObject attribute = panel.transform.Find("Window/MyInfo/Attribute_0").gameObject;
            Sprite icon = Resources.Load<Sprite>("Art/UI/Icons/ic_star");
            Assert.That(icon, Is.Not.Null);
            var data = new DetailsPanelData(new DetailsProfileData("新馆长", "新背景", "新介绍", icon),
                new[] { new DetailsAttributeData(DetailsAttributeKind.Language, 20, "新的语言说明") },
                new[] { new DetailsSpecializationData("新专精", "新的专精说明", icon) });
            panel.ShowWorld();
            IDetailsPanel api = panel;
            api.Refresh(data);
            Assert.That(((IPanel)panel).Data, Is.SameAs(data));
            Assert.That(api.CurrentPage, Is.EqualTo(DetailsPanel.Page.World), "Refresh should not force a tab change.");
            Assert.That(panel.transform.Find("Window/MyInfo/ProfileCard/Name").GetComponent<TMP_Text>(), Is.SameAs(name));
            Assert.That(panel.transform.Find("Window/MyInfo/Attribute_0/Value").GetComponent<TMP_Text>(), Is.SameAs(value));
            Assert.That(name.text, Is.EqualTo("新馆长"));
            Assert.That(value.text, Is.EqualTo("20 / 20"));
            Assert.That(panel.transform.Find("Window/MyInfo/Attribute_0/Track/Fill").GetComponent<RectTransform>().anchorMax.x, Is.EqualTo(1f));
            Image avatar = panel.transform.Find("Window/MyInfo/ProfileCard/AvatarFrame/AvatarImage").GetComponent<Image>();
            Assert.That(avatar.sprite, Is.SameAs(icon));
            Assert.That(avatar.gameObject.activeSelf, Is.True);
            Assert.That(panel.transform.Find("Window/MyInfo/ProfileCard/AvatarFrame/AvatarSilhouette").gameObject.activeSelf, Is.False);
            panel.ShowMyInfo();
            ExecuteEvents.Execute(attribute, new PointerEventData(eventSystem) { position = ScreenPoint(attribute.transform) }, ExecuteEvents.pointerEnterHandler);
            Assert.That(panel.transform.Find("Window/Tooltip/Body").GetComponent<TMP_Text>().text, Is.EqualTo("新的语言说明"));
            panel.Refresh(new DetailsPanelData());
            Assert.That(panel.TooltipVisible, Is.False);
            Assert.That(avatar.sprite, Is.Null);
            Assert.That(avatar.gameObject.activeSelf, Is.False);
            Assert.That(value.text, Is.EqualTo("0 / 20"));
            Assert.Throws<ArgumentNullException>(() => panel.Refresh(null));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpecializationListGrowsScrollsAndClearsWithoutStaleBadges()
        {
            DetailsPanel panel = (DetailsPanel)controller.CurrentPanel;
            var list = new List<DetailsSpecializationData>();
            for (int i = 0; i < 10; i++) list.Add(new DetailsSpecializationData("专精" + i, "说明" + i));
            panel.Refresh(new DetailsPanelData(specializations: list));
            yield return null;
            Canvas.ForceUpdateCanvases();
            Transform content = panel.transform.Find("Window/MyInfo/Specializations/Content");
            Assert.That(content.GetComponentsInChildren<DetailsSpecializationItem>().Length, Is.EqualTo(10));
            ScrollRect scroll = panel.transform.Find("Window/MyInfo/Specializations").GetComponent<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            GameObject last = content.Find("Specialization_9").gameObject;
            ExecuteEvents.Execute(last, new PointerEventData(eventSystem) { position = ScreenPoint(last.transform) }, ExecuteEvents.pointerEnterHandler);
            Assert.That(panel.transform.Find("Window/Tooltip/Body").GetComponent<TMP_Text>().text, Is.EqualTo("说明9"));
            panel.Refresh(new DetailsPanelData());
            Assert.That(panel.TooltipVisible, Is.False);
            Assert.That(content.GetComponentsInChildren<DetailsSpecializationItem>().Length, Is.Zero);
            Assert.That(panel.transform.Find("Window/MyInfo/Specializations/Empty").gameObject.activeSelf, Is.True);
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.01f));
            panel.Refresh(DetailsPanelData.CreatePlaceholder());
            yield return null;
            Assert.That(content.GetComponentsInChildren<DetailsSpecializationItem>().Length, Is.EqualTo(6));
            Assert.That(panel.transform.Find("Window/MyInfo/Specializations/Empty").gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ReusablePanelClearsSnapshotAndDefaultsOnNextOpen()
        {
            controller.StopCurrent();
            var data = new DetailsPanelData(new DetailsProfileData("缓存测试", "缓存背景"));
            DetailsPanel first = UIKit.OpenPanel<DetailsPanel>(UILevel.Pop, data: data,
                tag: PresentationDebugSceneController.DebugTag, cachePolicy: PanelCachePolicy.Reusable);
            yield return new WaitForSecondsRealtime(.35f);
            first.ShowWorld();
            UIKit.ClosePanel(first);
            Assert.That(first.CurrentData, Is.Null);
            DetailsPanel second = UIKit.OpenPanel<DetailsPanel>(UILevel.Pop,
                tag: PresentationDebugSceneController.DebugTag, cachePolicy: PanelCachePolicy.Transient);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.CurrentData.IsPlaceholder, Is.True);
            Assert.That(second.CurrentData.Profile.Name, Does.Not.Contain("缓存测试"));
            Assert.That(second.CurrentPage, Is.EqualTo(DetailsPanel.Page.MyInfo));
            Assert.That(second.TooltipVisible, Is.False);
        }

        private GameObject FirstHit(Vector2 position)
        {
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }
        private static Vector2 ScreenPoint(Transform transform) => RectTransformUtility.WorldToScreenPoint(null, transform.position);
        private static void AssertPages(DetailsPanel panel, string selected)
        {
            foreach (string name in new[] { "MyInfo", "Relationships", "World" })
                Assert.That(panel.transform.Find("Window/" + name).gameObject.activeSelf, Is.EqualTo(name == selected));
        }
        private static void Capture(string name)
        {
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            if (image == default) throw new InvalidOperationException("Game view capture unavailable.");
            try
            {
                string directory = Path.GetFullPath("output/details-panel");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
            }
            finally { UnityEngine.Object.Destroy(image); }
        }
    }
}
#endif
