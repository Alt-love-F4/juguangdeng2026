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
            ((DetailsPanel)controller.CurrentPanel).Refresh(DetailsPanelData.CreatePlaceholder());
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
            Assert.That(panel.GetComponentsInChildren<DetailsHoverTarget>(true).Length, Is.GreaterThanOrEqualTo(10));
            for (int i = 0; i < 4; i++)
            {
                TMP_Text value = panel.transform.Find("Window/MyInfo/Attributes/Attribute_" + i + "/Value").GetComponent<TMP_Text>();
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
            GameObject attribute = panel.transform.Find("Window/MyInfo/Attributes/Attribute_0").gameObject;
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
                window.TransformPoint(new Vector3(300, 230, 0)));
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
            Assert.That(panel.transform.Find("Window/MyInfo/PlayerName").GetComponent<TMP_Text>().text, Is.EqualTo("测试馆长"));
            Assert.That(panel.transform.Find("Window/Relationships/Description").GetComponent<TMP_Text>().text, Is.EqualTo("关系内容"));
            Assert.That(panel.transform.Find("Window/World/Description").GetComponent<TMP_Text>().text, Is.EqualTo("世界内容"));
            Assert.That(panel.transform.Find("Window/Footer").GetComponent<TMP_Text>().text, Does.Not.Contain("占位"));
        }

        [UnityTest]
        public IEnumerator RefreshPreservesComponentReferencesAndUpdatesAvatarValuesAndHover()
        {
            DetailsPanel panel = (DetailsPanel)controller.CurrentPanel;
            TMP_Text name = panel.transform.Find("Window/MyInfo/PlayerName").GetComponent<TMP_Text>();
            TMP_Text value = panel.transform.Find("Window/MyInfo/Attributes/Attribute_0/Value").GetComponent<TMP_Text>();
            GameObject attribute = panel.transform.Find("Window/MyInfo/Attributes/Attribute_0").gameObject;
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
            Assert.That(panel.transform.Find("Window/MyInfo/PlayerName").GetComponent<TMP_Text>(), Is.SameAs(name));
            Assert.That(panel.transform.Find("Window/MyInfo/Attributes/Attribute_0/Value").GetComponent<TMP_Text>(), Is.SameAs(value));
            Assert.That(name.text, Is.EqualTo("新馆长"));
            Assert.That(value.text, Is.EqualTo("20 / 20"));
            Image avatar = panel.transform.Find("Window/MyInfo/Avatar").GetComponent<Image>();
            Assert.That(avatar.sprite, Is.SameAs(icon));
            Assert.That(avatar.gameObject.activeSelf, Is.True);
            Assert.That(panel.transform.Find("Window/MyInfo/AvatarPlaceholder").gameObject.activeSelf, Is.False);
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
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(0f).Within(.01f));
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

        [UnityTest]
        public IEnumerator CharacterSelectionKeepsGridVisibleAndPagingUsesReservedCells()
        {
            DetailsPanel panel = (DetailsPanel)controller.CurrentPanel;
            var roster = new List<DetailsCharacterData>();
            for (int i = 0; i < 40; i++)
                roster.Add(new DetailsCharacterData("npc" + i, "人物" + i, i == 4 ? new string('详', 400) : "介绍" + i, unlocked: i != 1,
                    bars: new[] { new DetailsProgressData("familiarity", "熟悉程度", 5, 0, 10) }));
            panel.Refresh(new DetailsPanelData(characters: roster));
            panel.ShowRelationships();
            yield return null;
            Transform view = panel.transform.Find("Window/Relationships/RelationshipsView");
            Transform grid = view.Find("CharacterGrid");
            Assert.That(grid.GetComponentsInChildren<Button>().Length, Is.EqualTo(31));
            Assert.That(panel.ShowCharacter("npc1"), Is.False);
            grid.Find("Character_npc4").GetComponent<Button>().onClick.Invoke();
            Assert.That(view.Find("Name").GetComponent<TMP_Text>().text, Is.EqualTo("人物4"));
            Assert.That(grid.gameObject.activeInHierarchy, Is.True);
            Canvas.ForceUpdateCanvases();
            var introductionScroll = view.Find("IntroductionScroll").GetComponent<ScrollRect>();
            Assert.That(introductionScroll.content.rect.height, Is.GreaterThan(introductionScroll.viewport.rect.height));
            Assert.That(view.Find("FamiliarityFill").GetComponent<Image>().fillAmount, Is.EqualTo(.5f));
            panel.ChangeRelationshipPage(1);
            yield return null;
            Assert.That(panel.RelationshipPageIndex, Is.EqualTo(1));
            Assert.That(grid.Find("Character_npc31"), Is.Not.Null);
            Assert.That(grid.Find("Character_npc0"), Is.Null);
            Assert.That(view.Find("Name").GetComponent<TMP_Text>().text, Is.EqualTo("人物4"));
            yield return new WaitForEndOfFrame();
            Capture("details-relationships-selected");
        }

        [UnityTest]
        public IEnumerator WorldSelectorsScrollAndSelectedProgressRefreshesWithoutStaleData()
        {
            DetailsPanel panel = (DetailsPanel)controller.CurrentPanel;
            var bars = new List<DetailsProgressData>();
            for (int i = 0; i < 8; i++) bars.Add(new DetailsProgressData("world" + i, "世界指标" + i, 15, 10, 20,
                "下限说明" + i, "上限说明" + i, "指标描述" + i));
            panel.Refresh(new DetailsPanelData(worldBars: bars));
            Assert.That(panel.SelectWorldProgress("world6"), Is.True);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Transform view = panel.transform.Find("Window/World/WorldView");
            var scroll = view.Find("Selectors").GetComponent<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(panel.SelectedWorldProgressKey, Is.EqualTo("world6"));
            Assert.That(view.Find("Description").GetComponent<TMP_Text>().text, Is.EqualTo("指标描述6"));
            Assert.That(view.Find("Fill").GetComponent<Image>().fillAmount, Is.EqualTo(.5f));
            Assert.That(view.Find("Lower").GetComponent<TMP_Text>().text, Is.EqualTo("10"));
            Assert.That(view.Find("UpperDescription").GetComponent<TMP_Text>().text, Is.EqualTo("上限说明6"));
            Assert.That(panel.SelectWorldProgress("missing"), Is.False);
            yield return new WaitForEndOfFrame();
            Capture("details-world-selected");
            panel.Refresh(new DetailsPanelData());
            Assert.That(panel.SelectedWorldProgressKey, Is.Null);
            Assert.That(view.Find("Fill").GetComponent<Image>().fillAmount, Is.Zero);
            Assert.That(view.Find("Value").GetComponent<TMP_Text>().text, Is.Empty);
        }

        [UnityTest]
        public IEnumerator FormalLetterArtRetainsOpenReadSignSkipAndReplay()
        {
            controller.PlayHardcodedLetter();
            var letter = (FirstDayLetterPanel)controller.CurrentPanel;
            int completions = 0;
            var content = new FirstDayLetterContent("一封来自叔叔的信。", "叔叔");
            letter.Play(content, () => completions++);
            yield return new WaitForSecondsRealtime(.1f);
            var art = letter.transform.Find("EnvelopeStage/Envelope/FormalArt").GetComponent<Image>();
            Assert.That(art.sprite.name, Is.EqualTo("信封出现"));
            yield return new WaitForEndOfFrame();
            Capture("letter-formal-envelope");
            letter.OpenEnvelope();
            Assert.That(art.sprite.name, Is.EqualTo("信封自动打开"));
            float deadline = Time.realtimeSinceStartup + 5;
            while (letter.Stage != FirstDayLetterPanel.LetterStage.AwaitingSignature)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            Capture("letter-formal-paper");
            letter.ConfirmSignature();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(completions, Is.EqualTo(1));
            letter.Play(content, () => completions++);
            Assert.That(art.sprite.name, Is.EqualTo("信封出现"), "Replay must restore the closed envelope.");
            letter.SkipLetter();
            Assert.That(completions, Is.EqualTo(2));
        }

        private sealed class SnapshotSource : IDetailsPanelDataSource
        {
            public DetailsPanelData Snapshot;
            public int Reads;
            public DetailsPanelData ReadDetails() { Reads++; return Snapshot; }
        }

        [UnityTest]
        public IEnumerator InjectedSourceAndDisplayEventsReleaseOnCachedClose()
        {
            controller.StopCurrent();
            DetailsPanel panel = UIKit.OpenPanel<DetailsPanel>(UILevel.Pop,
                tag: PresentationDebugSceneController.DebugTag, cachePolicy: PanelCachePolicy.Reusable);
            yield return new WaitForSecondsRealtime(.35f);
            IDetailsPanel api = panel;
            int refreshes = 0, closes = 0, pages = 0;
            api.DataRefreshed += snapshot => { refreshes++; Assert.That(api.CurrentData, Is.SameAs(snapshot)); };
            api.PageChanged += page => { pages++; Assert.That(api.CurrentPage, Is.EqualTo(page)); };
            api.Closed += () => { closes++; Assert.That(api.CurrentData, Is.Null); };
            var source = new SnapshotSource { Snapshot = new DetailsPanelData(new DetailsProfileData("接口馆长", "背景")) };
            api.BindDataSource(source, false);
            Assert.That(source.Reads, Is.Zero);
            Assert.That(api.RefreshFromSource(), Is.True);
            Assert.That(refreshes, Is.EqualTo(1));
            api.SelectPage(DetailsPanel.Page.World);
            api.SelectPage(DetailsPanel.Page.World);
            Assert.That(pages, Is.EqualTo(1), "Repeated selection must not emit duplicate events.");
            source.Snapshot = null;
            Assert.That(api.RefreshFromSource(), Is.False);
            Assert.That(refreshes, Is.EqualTo(1));
            Assert.That(api.CurrentData.Profile.Name, Is.EqualTo("接口馆长"));
            UIKit.ClosePanel(panel);
            Assert.That(closes, Is.EqualTo(1));
            api.Refresh(new DetailsPanelData(new DetailsProfileData("关闭后刷新")));
            Assert.That(api.CurrentData, Is.Null, "A stale reference cannot repopulate a closed cached panel.");
            DetailsPanel reopened = UIKit.OpenPanel<DetailsPanel>(UILevel.Pop,
                tag: PresentationDebugSceneController.DebugTag, cachePolicy: PanelCachePolicy.Transient);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(reopened, Is.SameAs(panel));
            Assert.That(reopened.RefreshFromSource(), Is.False);
            reopened.Refresh(new DetailsPanelData());
            reopened.ShowWorld();
            Assert.That(refreshes, Is.EqualTo(1), "Old subscribers must not survive cached reuse.");
            Assert.That(pages, Is.EqualTo(1));
            UIKit.ClosePanel(reopened);
            Assert.That(closes, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SelectionInterfacesNavigateAndClicksNotifyWithoutDuplicateEvents()
        {
            DetailsPanel panel = (DetailsPanel)controller.CurrentPanel;
            IDetailsPanel api = panel;
            var roster = new List<DetailsCharacterData>();
            for (int i = 0; i < 40; i++) roster.Add(new DetailsCharacterData("npc_" + i, "人物" + i, "介绍", unlocked: i != 0));
            api.Refresh(new DetailsPanelData(characters: roster, worldBars: new[] {
                new DetailsProgressData("first", "稳定", 2, 0, 10),
                new DetailsProgressData("second", "日期", 5, 0, 10) }));
            int characters = 0, worlds = 0, pages = 0;
            api.CharacterSelected += id => { characters++; Assert.That(api.SelectedCharacterId, Is.EqualTo(id)); };
            api.WorldProgressSelected += key => { worlds++; Assert.That(api.SelectedWorldProgressKey, Is.EqualTo(key)); };
            api.RelationshipPageChanged += index => { pages++; Assert.That(api.RelationshipPageIndex, Is.EqualTo(index)); };
            Assert.That(api.RelationshipPageCount, Is.EqualTo(2));
            Assert.That(api.ShowCharacter("npc_35"), Is.True);
            Assert.That(api.RelationshipPageIndex, Is.EqualTo(1), "External character selection reveals its page.");
            Assert.That(characters, Is.EqualTo(1));
            Assert.That(pages, Is.EqualTo(1));
            Assert.That(api.ShowCharacter("npc_35"), Is.True);
            Assert.That(characters, Is.EqualTo(1));
            Assert.That(api.ShowCharacter("npc_0"), Is.False);
            Assert.That(api.SetRelationshipPage(-1), Is.False);
            Assert.That(api.SetRelationshipPage(2), Is.False);
            Assert.That(api.RelationshipPageIndex, Is.EqualTo(1));
            panel.transform.Find("Window/Relationships/RelationshipsView/CharacterGrid/Character_npc_34").GetComponent<Button>().onClick.Invoke();
            Assert.That(api.SelectedCharacterId, Is.EqualTo("npc_34"));
            Assert.That(characters, Is.EqualTo(2));
            api.ReturnToRelationships();
            Assert.That(api.SelectedCharacterId, Is.Null);
            Assert.That(characters, Is.EqualTo(3));
            panel.transform.Find("Window/World/WorldView/Selectors/Content/Selector_second").GetComponent<Button>().onClick.Invoke();
            Assert.That(api.SelectedWorldProgressKey, Is.EqualTo("second"));
            Assert.That(worlds, Is.EqualTo(1));
            Assert.That(api.SelectWorldProgress("missing"), Is.False);
            api.SelectWorldProgress("second");
            Assert.That(worlds, Is.EqualTo(1));
            api.Refresh(new DetailsPanelData());
            Assert.That(api.SelectedWorldProgressKey, Is.Null);
            Assert.That(api.RelationshipPageIndex, Is.Zero);
            Assert.That(worlds, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisplayTransitionsAndDescriptionScrollWorkWhilePaused()
        {
            DetailsPanel panel = (DetailsPanel)controller.CurrentPanel;
            IDetailsPanel api = panel;
            var skills = new List<DetailsSpecializationData>();
            for (int i = 0; i < 15; i++) skills.Add(new DetailsSpecializationData("专精" + i, "专精说明" + i));
            api.Refresh(new DetailsPanelData(attributes: new[] { new DetailsAttributeData(DetailsAttributeKind.Wisdom, 8, "智慧说明") }, specializations: skills));
            yield return null;
            Assert.That(api.ScrollTo(DetailsScrollRegion.Specializations, 0), Is.True);
            var scroll = panel.transform.Find("Window/MyInfo/Specializations").GetComponent<ScrollRect>();
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0).Within(.01f));
            Assert.That(api.ScrollTo(DetailsScrollRegion.Specializations, float.NaN), Is.False);
            Assert.That(api.ScrollTo((DetailsScrollRegion)99, .5f), Is.False);
            Time.timeScale = 0;
            api.SelectPage(DetailsPanel.Page.World);
            api.SelectPage(DetailsPanel.Page.Relationships);
            api.SelectPage(DetailsPanel.Page.MyInfo);
            Assert.That(api.ShowAttributeDescription(DetailsAttributeKind.Wisdom), Is.True);
            Assert.That(api.TooltipVisible, Is.True);
            Assert.That(panel.transform.Find("Window/Tooltip/Body").GetComponent<TMP_Text>().text, Is.EqualTo("智慧说明"));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(panel.transform.Find("Window/MyInfo").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1).Within(.01f));
            Assert.That(panel.transform.Find("Window/Tooltip").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1).Within(.01f));
            Assert.That(api.ShowSpecializationDescription(14), Is.True);
            Assert.That(panel.transform.Find("Window/Tooltip/Body").GetComponent<TMP_Text>().text, Is.EqualTo("专精说明14"));
            Assert.That(api.ShowSpecializationDescription(15), Is.False);
            api.HideDescription();
            Assert.That(api.TooltipVisible, Is.False);
            api.SelectPage(DetailsPanel.Page.World);
            api.RequestClose();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(controller.CurrentPanel, Is.Null);
        }

        [UnityTest]
        public IEnumerator SharedFrameStaysVisibleWhileOnlyContentTransitions()
        {
            DetailsPanel panel = (DetailsPanel)controller.CurrentPanel;
            Transform window = panel.transform.Find("Window");
            Transform frame = window.Find("SharedFrame");
            Transform chrome = window.Find("SharedChrome");
            Assert.That(frame, Is.Not.Null);
            Assert.That(chrome, Is.Not.Null);
            Assert.That(frame.Find("MapBackground").gameObject.activeSelf, Is.False);
            Image paper = frame.Find("PaperSurface").GetComponent<Image>();
            Image backplate = frame.Find("Backplate").GetComponent<Image>();
            int paperId = paper.GetInstanceID();
            int backplateId = backplate.GetInstanceID();
            Vector2 selectedPosition = ((RectTransform)chrome.Find("SelectedTab")).anchoredPosition;
            foreach (string pageName in new[] { "MyInfo", "Relationships", "World" })
            {
                Transform page = window.Find(pageName);
                Assert.That(page.Find("PageHint_TMP"), Is.Null);
                Assert.That(page.Find("我的信息_TMP"), Is.Null);
                Assert.That(page.Find("组 3"), Is.Null);
                Assert.That(page.Find("退出按键"), Is.Null);
            }
            Time.timeScale = 0;
            foreach (DetailsPanel.Page page in new[] { DetailsPanel.Page.Relationships, DetailsPanel.Page.World, DetailsPanel.Page.MyInfo })
            {
                // Exercise the real tab route, preserving click hitboxes and their serialized listeners.
                window.Find("Tab_" + (int)page).GetComponent<Button>().onClick.Invoke();
                Assert.That(panel.CurrentPage, Is.EqualTo(page));
                Assert.That(frame.gameObject.activeInHierarchy, Is.True);
                Assert.That(chrome.gameObject.activeInHierarchy, Is.True);
                Assert.That(paper.enabled, Is.True);
                Assert.That(paper.GetInstanceID(), Is.EqualTo(paperId));
                Assert.That(backplate.GetInstanceID(), Is.EqualTo(backplateId));
                Assert.That(frame.GetComponent<CanvasGroup>(), Is.Null);
                Assert.That(chrome.GetComponent<CanvasGroup>(), Is.Null);
                Assert.That(frame.GetComponentInParent<CanvasGroup>().alpha, Is.EqualTo(1).Within(.01f));
                Assert.That(window.Find(page.ToString()).GetComponent<CanvasGroup>().alpha, Is.EqualTo(0).Within(.01f));
                Assert.That(((RectTransform)chrome.Find("SelectedTab")).anchoredPosition.y, Is.EqualTo(selectedPosition.y));
                for (int i = 0; i < 3; i++) Assert.That(chrome.Find("IdleTab_" + i).GetComponent<Image>().enabled, Is.EqualTo(i != (int)page));
                Assert.That(frame.Find("MapBackground").gameObject.activeInHierarchy, Is.False);
                yield return new WaitForSecondsRealtime(.16f);
                Assert.That(window.Find(page.ToString()).GetComponent<CanvasGroup>().alpha, Is.EqualTo(1).Within(.01f));
                yield return new WaitForEndOfFrame();
                Capture("details-shared-" + page.ToString().ToLowerInvariant());
            }
            Assert.That(FirstHit(ScreenPoint(window.Find("CloseButton"))).name, Is.EqualTo("CloseButton"));
        }

        [UnityTest]
        public IEnumerator ReferenceCompositionScreenshots()
        {
            DetailsPanel panel = (DetailsPanel)controller.CurrentPanel;
            var attributes = new[] {
                new DetailsAttributeData(DetailsAttributeKind.Language, 0, "影响表达与沟通"),
                new DetailsAttributeData(DetailsAttributeKind.Courage, 0, "影响面对风险的行动"),
                new DetailsAttributeData(DetailsAttributeKind.Wisdom, 0, "影响分析、推理与解决问题"),
                new DetailsAttributeData(DetailsAttributeKind.Perception, 0, "影响察觉细节") };
            panel.Refresh(new DetailsPanelData(
                new DetailsProfileData("主角id", introduction: "一段简短的介绍：我坐在咖啡馆的角落，观察着一切\n你接受叔叔的咖啡馆，习惯了倾听"), attributes,
                characters: new[] { new DetailsCharacterData("reference", "人物姓名", "尚未结识此人物，无法获得相关信息", unlocked: false) },
                worldBars: new[] { new DetailsProgressData("stability", "王国稳定", 10, 0, 10, description: "点击后展示相关描述：王国稳定各种表现和信息"),
                    new DetailsProgressData("date", "日期", 10, 0, 10, description: "日期与世界进度") }));
            panel.ShowMyInfo();
            yield return new WaitForSecondsRealtime(.16f);
            yield return new WaitForEndOfFrame(); Capture("details-reference-my-info");
            panel.ShowRelationships();
            yield return new WaitForSecondsRealtime(.16f);
            yield return new WaitForEndOfFrame(); Capture("details-reference-relationships");
            panel.ShowWorld();
            yield return new WaitForSecondsRealtime(.16f);
            yield return new WaitForEndOfFrame(); Capture("details-reference-world");
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
