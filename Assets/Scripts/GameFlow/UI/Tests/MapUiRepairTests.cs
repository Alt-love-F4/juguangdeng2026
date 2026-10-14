#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>In-memory EditMode regressions; no scene loading, PlayMode or generated prefab required.</summary>
    public sealed class MapUiRepairTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject root;
        private GameObject runtimeObject;
        private NarrativeRuntime previousRuntime;
        private NarrativeRuntime runtime;
        private PropertyInfo instanceProperty;
        private MapActionDetailsView details;

        private sealed class ImmediatePresenter : INarrativePresenter
        {
            public void Play(NarrativeEventRequest request, Action completed) => completed?.Invoke();
        }

        [SetUp]
        public void SetUp()
        {
            instanceProperty = typeof(NarrativeRuntime).GetProperty("Instance");
            previousRuntime = NarrativeRuntime.Instance;
            runtimeObject = new GameObject("MapUiRepairRuntime");
            runtimeObject.SetActive(false); // Prevent Awake/Luban loading/DontDestroyOnLoad.
            runtime = runtimeObject.AddComponent<NarrativeRuntime>();
            runtime.Initialize(new NarrativeConfiguration(), new ImmediatePresenter());
            instanceProperty.GetSetMethod(true).Invoke(null, new object[] { runtime });
            root = new GameObject("MapUiRepairRoot", typeof(RectTransform));
            root.SetActive(false); // No TMP rendering, Update polling or coroutines.
            ((RectTransform)root.transform).sizeDelta = new Vector2(1600, 1200);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (root != null) Object.DestroyImmediate(root);
                if (runtimeObject != null) Object.DestroyImmediate(runtimeObject);
            }
            finally
            {
                if (instanceProperty != null)
                    instanceProperty.GetSetMethod(true).Invoke(null, new object[] { previousRuntime });
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(8)]
        public void InventoryGridHasAtLeastSixSlotsAndExcludesEmptyHint(int itemCount)
        {
            for (int i = 0; i < itemCount; i++)
                Assert.That(runtime.MapInventory.TryAdd("item" + i, i + 1), Is.True);
            var inventory = CreateInventory();
            var content = inventory.transform.Find("Viewport/Items");
            var grid = Widget(content, "UnityEngine.UI.GridLayoutGroup");
            Assert.That(Get<int>(grid, "constraintCount"), Is.EqualTo(3));
            Assert.That(Get<object>(grid, "constraint").ToString(), Is.EqualTo("FixedColumnCount"));
            var slots = content.Cast<Transform>().Where(t => t.name != "Empty").ToArray();
            Assert.That(slots.Length, Is.EqualTo(Math.Max(6, itemCount)));
            Assert.That(slots.Count(t => t.name.StartsWith("Item_", StringComparison.Ordinal)), Is.EqualTo(itemCount));
            for (int i = 0; i < itemCount; i++)
                Assert.That(inventory.FindSlot("item" + i), Is.Not.Null);
            var hint = content.Find("Empty");
            Assert.That(hint != null, Is.EqualTo(itemCount == 0));
            if (hint != null)
            {
                Assert.That(Get<bool>(Widget(hint, "UnityEngine.UI.LayoutElement"), "ignoreLayout"), Is.True);
                Assert.That(Get<string>(Widget(hint, "TMPro.TextMeshProUGUI"), "text"), Is.EqualTo("物品栏为空"));
                var cell = Get<Vector2>(grid, "cellSize");
                var gap = Get<Vector2>(grid, "spacing");
                Assert.That(((RectTransform)hint).anchoredPosition.y,
                    Is.EqualTo(-(2 * cell.y + gap.y + 24)).Within(.001f));
                Assert.That(Get<RectOffset>(grid, "padding").bottom, Is.EqualTo(84));
            }
            else Assert.That(Get<RectOffset>(grid, "padding").bottom, Is.Zero);
        }

        [Test]
        public void InventoryRefreshRemovesEmptyHintAndStaleSlotsWithoutDuplicates()
        {
            var inventory = CreateInventory();
            runtime.MapInventory.TryAdd("beans", 7);
            inventory.Refresh();
            var content = inventory.transform.Find("Viewport/Items");
            Assert.That(content.Cast<Transform>().Count(child => child.gameObject.activeSelf), Is.EqualTo(6));
            Assert.That(content.Find("Empty"), Is.Null);
            var slot = inventory.FindSlot("beans");
            inventory.Refresh();
            Assert.That(inventory.FindSlot("beans"), Is.SameAs(slot), "Unchanged signatures must not rebuild slots.");
            runtime.MapInventory.Restore(null);
            inventory.Refresh();
            Assert.That(inventory.FindSlot("beans"), Is.Null);
            Assert.That(content.Find("Item_beans"), Is.Null);
            Assert.That(content.Cast<Transform>().Count(child => child.gameObject.activeSelf), Is.EqualTo(6), "Six visible slots; editor-only hint and authored template stay hidden.");
            Assert.That(content.Find("Empty"), Is.Not.Null);
            Assert.That(content.Find("Empty").gameObject.activeSelf, Is.False);
        }

        [TestCase(550f)]
        [TestCase(90f)]
        [TestCase(65f)]
        public void InventoryCellsFitThreeColumnsEvenOnNarrowPanels(float width)
        {
            var inventory = CreateInventory();
            ((RectTransform)inventory.transform).sizeDelta = new Vector2(width, 700);
            Invoke(inventory, "UpdateGridSize");
            var viewport = (RectTransform)inventory.transform.Find("Viewport");
            var grid = Widget(inventory.transform.Find("Viewport/Items"), "UnityEngine.UI.GridLayoutGroup");
            var cell = Get<Vector2>(grid, "cellSize");
            var gap = Get<Vector2>(grid, "spacing");
            Assert.That(cell.x, Is.GreaterThanOrEqualTo(0));
            Assert.That(cell.x, Is.EqualTo(cell.y));
            Assert.That(cell.x * 3 + gap.x * 2, Is.EqualTo(Mathf.Max(1, viewport.rect.width)).Within(.001f));
        }

        [TestCase("大堂区")]
        [TestCase("cafe")]
        public void CafeLocationsExposeEnterCafeAndInvokeSuppliedNavigation(string position)
        {
            int calls = 0;
            CreateDetails(() => calls++);
            details.Show(position, "大堂区");
            var content = details.transform.Find("Viewport/ActionList");
            var enter = content.Find("EnterCafe");
            Assert.That(enter, Is.Not.Null, "The cafe detail must offer the scene entry.");
            Assert.That(content.Find("EmptyActionState"), Is.Null, "The cafe card replaces the empty action state.");
            Assert.That(Get<string>(Widget(enter.Find("Name"), "TMPro.TextMeshProUGUI"), "text"), Is.EqualTo("大堂区"));
            Assert.That(Get<string>(Widget(enter.Find("Execute/Label"), "TMPro.TextMeshProUGUI"), "text"), Is.EqualTo("进入咖啡馆"));
            Assert.That(enter.Find("Execute").gameObject.activeSelf, Is.True);
            var requirement = enter.Find("Requirements/Requirement") ?? enter.Find("Requirement");
            Assert.That(requirement == null || !requirement.gameObject.activeSelf, Is.True,
                "Scene navigation must not present NCS requirements.");
            var button = Widget(enter.Find("Execute"), "UnityEngine.UI.Button");
            Get<object>(button, "onClick").GetType().GetMethod("Invoke").Invoke(Get<object>(button, "onClick"), null);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void CafeEntryConsumesNoActionPointsItemsOrNarrativeActions()
        {
            var beans = new NarrativeActionDefinition { actionKey = "cafe_beans", nameText = "咖啡豆特饮", requirements = "Item(beans,2);ActionPoint(1)" };
            var config = new NarrativeConfiguration(); config.actions.Add(beans);
            runtime.Initialize(config, new ImmediatePresenter());
            runtime.MapInventory.TryAdd("beans", 5);
            runtime.Actions.ActionPoints.Set(3);
            runtime.Actions.Mount("cafe", beans.actionKey);
            int settled = 0;
            runtime.Actions.ActionFinished += (action, success) => settled++;
            int navigations = 0;
            CreateDetails(() => navigations++);
            details.Show("cafe", "咖啡馆");
            var entry = details.transform.Find("Viewport/ActionList/EnterCafe");
            Assert.That(entry, Is.Not.Null);
            var button = Widget(entry.Find("Execute"), "UnityEngine.UI.Button");
            var click = Get<object>(button, "onClick");
            click.GetType().GetMethod("Invoke").Invoke(click, null);
            Assert.That(navigations, Is.EqualTo(1));
            Assert.That(settled, Is.Zero, "Entering the cafe must not settle an NCS action.");
            Assert.That(runtime.Actions.ActionPoints.Current, Is.EqualTo(3), "Entering the cafe must not spend action points.");
            Assert.That(runtime.MapInventory.GetCount("beans"), Is.EqualTo(5), "Entering the cafe must not consume items.");
        }

        [Test]
        public void CafeDetailKeepsConfiguredActionsAlongsideTheEntry()
        {
            var action = new NarrativeActionDefinition { actionKey = "act_meet_b", nameText = "市场行动", requirements = "ActionPoint(1)" };
            var config = new NarrativeConfiguration(); config.actions.Add(action);
            runtime.Initialize(config, new ImmediatePresenter());
            runtime.Actions.ActionPoints.Set(2);
            runtime.Actions.Mount("cafe", action.actionKey);
            CreateDetails(null);
            details.Show("cafe", "咖啡馆");
            var content = details.transform.Find("Viewport/ActionList");
            Assert.That(content.Find("EnterCafe"), Is.Not.Null);
            Assert.That(content.Find("Action_act_meet_b"), Is.Not.Null, "Existing cafe actions stay visible.");
        }

        [Test]
        public void OtherLocationsDoNotExposeEnterCafe()
        {
            CreateDetails(null);
            details.Show("library", "图书馆");
            Assert.That(details.transform.Find("Viewport/ActionList/EnterCafe"), Is.Null);
            Assert.That(details.transform.Find("Viewport/ActionList/EmptyActionState"), Is.Not.Null);
        }

        [Test]
        public void EmptyCardUsesAuthoredLabelAndCannotExecute()
        {
            CreateDetails(null);
            details.Show("empty-test-location", "测试地点");
            var content = details.transform.Find("Viewport/ActionList");
            var empty = content.Find("EmptyActionState");
            Assert.That(empty, Is.Not.Null);
            Assert.That(Get<string>(Widget(empty.Find("Execute/Label"), "TMPro.TextMeshProUGUI"), "text"), Is.EqualTo("暂无行动"));
            Assert.That(empty.Find("Execute").gameObject.activeSelf, Is.False);
            Assert.That(Get<string>(Widget(content.Find("ActionCardTemplate/Execute/Label"), "TMPro.TextMeshProUGUI"), "text"), Is.EqualTo("Authored execute"));
            Assert.That(content.Find("LocationOverview"), Is.Null);
        }

        [Test]
        public void EmptyLocationSwitchAndReopenPreserveTemplateAndReplaceOnlyClone()
        {
            CreateDetails(null);
            var content = details.transform.Find("Viewport/ActionList");
            var template = content.Find("ActionCardTemplate");
            details.Show("empty-a", "地点甲");
            details.Show("empty-b", "地点乙");
            Assert.That(Get<string>(Widget(content.Find("EmptyActionState/Name"), "TMPro.TextMeshProUGUI"), "text"), Is.EqualTo("地点乙"));
            details.gameObject.SetActive(false);
            details.Show("empty-b", "地点乙");
            Assert.That(content.Find("ActionCardTemplate"), Is.SameAs(template));
            Assert.That(content.Cast<Transform>().Count(t => t.name == "EmptyActionState"), Is.EqualTo(1));
            Assert.That(Get<string>(Widget(template.Find("Name"), "TMPro.TextMeshProUGUI"), "text"), Is.EqualTo("Authored name"));
            Assert.That(template.Find("Execute").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void EmptyCardPreservesAuthoredArtworkColor()
        {
            CreateDetails(null);
            details.Show("empty-test", "测试地点");
            var content = details.transform.Find("Viewport/ActionList");
            var template = Widget(content.Find("ActionCardTemplate"), "UnityEngine.UI.Image");
            var clone = Widget(content.Find("EmptyActionState"), "UnityEngine.UI.Image");
            Assert.That(Get<Color>(clone, "color"), Is.EqualTo(Get<Color>(template, "color")));
            Assert.That(Get<Color>(clone, "color"), Is.EqualTo(new Color(.12f, .34f, .56f)));
        }

        [Test]
        public void ShakeStopsWhenTargetIsDestroyedBetweenFrames()
        {
            CreateDetails(null);
            var target = (RectTransform)Create("ShakeTarget", root.transform).transform;
            var routine = (System.Collections.IEnumerator)typeof(MapActionDetailsView).GetMethod("Shake", PrivateInstance).Invoke(details, new object[] { target });
            Object.DestroyImmediate(target.gameObject);
            Assert.That(routine.MoveNext(), Is.False);
        }

        [TestCase(23, 8, 2)]
        [TestCase(50, 3, 1)]
        [TestCase(0, 8, 0)]
        public void RepeatSegmentsUseMinimumResourceCountAndAggregateDuplicateCosts(int beans, int points, int expected)
        {
            runtime.MapInventory.TryAdd("beans", beans);
            runtime.Actions.ActionPoints.SetMax(8);
            runtime.Actions.ActionPoints.Set(points);
            var action = new NarrativeActionDefinition { actionKey = "repeat" };
            action.parsedRequirements.Add(Cost(NarrativeActionRequirementType.Item, "beans", 2));
            action.parsedRequirements.Add(Cost(NarrativeActionRequirementType.Item, "beans", 3));
            action.parsedRequirements.Add(Cost(NarrativeActionRequirementType.ActionPoint, null, 1));
            action.parsedRequirements.Add(Cost(NarrativeActionRequirementType.ActionPoint, null, 2));
            var card = CreateRepeatCard(action, out var segments);
            Invoke(details, "UpdateRepeatSegments", card, runtime.Actions);
            Assert.That((int)CardField("repeatCount").GetValue(card), Is.EqualTo(expected));
            Assert.That(segments.childCount, Is.Zero, "Missing authored repeat art must not be synthesized.");
            Invoke(details, "UpdateRepeatSegments", card, runtime.Actions);
            Assert.That((int)CardField("repeatCount").GetValue(card), Is.EqualTo(expected));
            runtime.Actions.ActionPoints.Set(0);
            Invoke(details, "UpdateRepeatSegments", card, runtime.Actions);
            Assert.That((int)CardField("repeatCount").GetValue(card), Is.Zero);
            Assert.That(segments.childCount, Is.Zero);
        }

        [Test]
        public void DeliveryRepeatCountNeverPromisesMoreThanOneSubmissionToday()
        {
            var action = new NarrativeActionDefinition { actionKey = "delivery", deliveryMode = NarrativeDeliveryMode.Cumulative, progressTarget = 5, requirements = "Item(beans,2)" };
            var config = new NarrativeConfiguration(); config.actions.Add(action);
            runtime.Initialize(config, new ImmediatePresenter());
            runtime.MapInventory.TryAdd("beans", 100);
            var card = CreateRepeatCard(action, out var segments);
            Invoke(details, "UpdateRepeatSegments", card, runtime.Actions);
            Assert.That((int)CardField("repeatCount").GetValue(card), Is.EqualTo(1));
            Assert.That(runtime.Actions.Execute("delivery"), Is.True);
            Invoke(details, "UpdateRepeatSegments", card, runtime.Actions);
            Assert.That((int)CardField("repeatCount").GetValue(card), Is.Zero);
            Assert.That(segments.childCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatWithoutPositiveResourceCostShowsUnlimitedInsteadOfArbitraryPartitions(bool nonResourceRequirements)
        {
            var action = new NarrativeActionDefinition { actionKey = "unlimited" };
            if (nonResourceRequirements)
            {
                action.parsedRequirements.Add(Cost(NarrativeActionRequirementType.Condition, "flag", 1));
                action.parsedRequirements.Add(Cost(NarrativeActionRequirementType.Item, "beans", 0));
                action.parsedRequirements.Add(Cost(NarrativeActionRequirementType.ActionPoint, null, 0));
            }
            var card = CreateRepeatCard(action, out var segments);
            Invoke(details, "UpdateRepeatSegments", card, runtime.Actions);
            Assert.That((int)CardField("repeatCount").GetValue(card), Is.EqualTo(int.MaxValue));
            Assert.That(segments.childCount, Is.Zero, "Unbounded counts do not synthesize replacement artwork.");
        }

        [TestCase(1600f, 1200f, 550f, 850f)]
        [TestCase(90f, 200f, 34f, 140f)]
        public void SidebarInitializationSizesInactivePanelAndDisablesInteraction(float width, float height, float expectedWidth, float expectedHeight)
        {
            ((RectTransform)root.transform).sizeDelta = new Vector2(width, height);
            var sidebar = Create("Sidebar", root.transform).AddComponent<MapSidebarView>();
            sidebar.Initialize();
            var rect = (RectTransform)sidebar.transform;
            AssertVector(rect.sizeDelta, new Vector2(expectedWidth, expectedHeight));
            AssertVector(rect.anchorMin, new Vector2(1, .5f));
            Assert.That(sidebar.gameObject.activeSelf, Is.False);
            Assert.That(sidebar.IsVisible, Is.False);
            var group = sidebar.GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.Zero);
            Assert.That(group.interactable, Is.False);
            Assert.That(group.blocksRaycasts, Is.False);
        }

        // Match the existing test asmdef: TMP/UGUI types are resolved rather than referenced.
        private MapActionInventoryView CreateInventory()
        {
            var go = Create("Inventory", root.transform);
            go.AddComponent(UiType("UnityEngine.UI.Image"));
            var view = go.AddComponent<MapActionInventoryView>();
            typeof(MapActionInventoryView).GetField("showEditorPreview", PrivateInstance).SetValue(view, false);
            typeof(MapActionInventoryView).GetMethod("Initialize").Invoke(view, new object[] { null });
            return view;
        }

        private void CreateDetails(Action cafe)
        {
            var go = Create("Details", root.transform);
            go.AddComponent(UiType("UnityEngine.UI.Image"));
            details = go.AddComponent<MapActionDetailsView>();
            typeof(MapActionDetailsView).GetField("showEditorPreview", PrivateInstance).SetValue(details, false);
            details.Initialize(null, () => true, null, cafe);
            var content = details.transform.Find("Viewport/ActionList");
            var template = Create("ActionCardTemplate", content);
            ((RectTransform)template.transform).sizeDelta = new Vector2(796, 414);
            var image = template.AddComponent(UiType("UnityEngine.UI.Image"));
            image.GetType().GetProperty("color").SetValue(image, new Color(.12f, .34f, .56f));
            AddText("Name", template.transform, "Authored name");
            AddText("Description", template.transform, "Authored description");
            var execute = Create("Execute", template.transform);
            execute.AddComponent(UiType("UnityEngine.UI.Image"));
            execute.AddComponent(UiType("UnityEngine.UI.Button"));
            AddText("Label", execute.transform, "Authored execute");
            var requirement = Create("Requirement", template.transform);
            requirement.AddComponent(UiType("UnityEngine.UI.Image"));
            AddText("Label", requirement.transform, "行动点\n× 1");
            template.SetActive(false);
        }

        private object CreateRepeatCard(NarrativeActionDefinition action, out RectTransform segments)
        {
            if (details == null) CreateDetails(null);
            var type = typeof(MapActionDetailsView).GetNestedType("Card", BindingFlags.NonPublic);
            var card = Activator.CreateInstance(type, true);
            segments = (RectTransform)Create("RepeatSegments", root.transform).transform;
            CardField("action").SetValue(card, action);
            CardField("repeats").SetValue(card, segments);
            return card;
        }

        private static void AddText(string name, Transform parent, string text)
        {
            var component = Create(name, parent).AddComponent(UiType("TMPro.TextMeshProUGUI"));
            component.GetType().GetProperty("text").SetValue(component, text);
        }

        private static NarrativeActionRequirement Cost(NarrativeActionRequirementType type, string key, int value)
            => new NarrativeActionRequirement { type = type, key = key, value = value };
        private static FieldInfo CardField(string name)
            => typeof(MapActionDetailsView).GetNestedType("Card", BindingFlags.NonPublic).GetField(name);
        private static GameObject Create(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
        private static Type UiType(string name)
            => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        private static Component Widget(Transform target, string type)
        {
            Assert.That(target, Is.Not.Null, type + " target");
            var component = target.GetComponent(UiType(type));
            Assert.That(component, Is.Not.Null, type);
            return component;
        }
        private static T Get<T>(object target, string property)
            => (T)target.GetType().GetProperty(property).GetValue(target);
        private static void Invoke(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method, PrivateInstance);
            Assert.That(info, Is.Not.Null, method);
            info.Invoke(target, args);
        }
        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(.001f));
        }
    }
}
#endif
