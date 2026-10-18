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

        private sealed class PendingPresenter : INarrativePresenter
        {
            public Action Complete;
            public void Play(NarrativeEventRequest request, Action completed) => Complete = completed;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MapActionNarrativeRunsWithoutDayFlowButRespectsHostBlockers(bool openingMap)
        {
            var presenter = new PendingPresenter();
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "map-story" });
            config.actions.Add(new NarrativeActionDefinition { actionKey = "map-action",
                type = NarrativeActionType.Once, resultEvents = new System.Collections.Generic.List<string> { "map-story" } });
            runtime.Initialize(config, presenter);
            var host = Create("NarrativeHost", root.transform);
            var flow = host.AddComponent(UiType("Spotlight.GameFlow.GameFlowBootstrap"));
            var integration = host.AddComponent<NarrativeGameFlowIntegration>();
            typeof(NarrativeGameFlowIntegration).GetField("runtime", PrivateInstance).SetValue(integration, runtime);
            flow.GetType().GetField("narrativeIntegration", PrivateInstance).SetValue(flow, integration);
            var setSession = flow.GetType().GetMethod("SetSessionState", PrivateInstance);
            var stateType = setSession.GetParameters()[0].ParameterType;
            setSession.Invoke(flow, new[] { Enum.Parse(stateType, "Playing") });
            if (openingMap) flow.GetType().GetField("openingMap", PrivateInstance).SetValue(flow, true);
            else flow.GetType().GetField("mapOverlay", PrivateInstance).SetValue(flow,
                Create("MapOverlay", root.transform).AddComponent(UiType("Spotlight.GameFlow.MapSceneController")));
            var canPresent = flow.GetType().GetMethod("CanUseNarrativePresentation", PrivateInstance);
            Assert.That((bool)canPresent.Invoke(flow, null), Is.False, "An idle map must still suspend unrelated narration.");
            Assert.That(runtime.Actions.Execute("map-action"), Is.True);
            Assert.That(runtime.Actions.IsExecuting, Is.True);
            Assert.That(presenter.Complete, Is.Not.Null);
            Assert.That((bool)canPresent.Invoke(flow, null), Is.True, "Map result events must progress even through the fallback presenter without DayFlow.");
            var dayFlow = host.AddComponent(UiType("Spotlight.GameFlow.DayFlowController"));
            var dayState = dayFlow.GetType().GetProperty("State");
            dayState.GetSetMethod(true).Invoke(dayFlow, new[] { Enum.Parse(dayState.PropertyType, "CityMapLeisure") });
            flow.GetType().GetField("dayFlow", PrivateInstance).SetValue(flow, dayFlow);
            var mapControls = flow.GetType().GetProperty("MapControlsAvailable", PrivateInstance);
            Assert.That((bool)mapControls.GetValue(flow), Is.False, "An executing action must block map input even without DialoguePanel playback.");
            foreach (string fieldName in new[] { "paused", "loadingTransitioning" })
            {
                var field = flow.GetType().GetField(fieldName, PrivateInstance);
                field.SetValue(flow, true);
                Assert.That((bool)canPresent.Invoke(flow, null), Is.False, fieldName);
                field.SetValue(flow, false);
            }
            var hudType = UiType("Spotlight.GameFlow.GameplayHudPanel");
            var hudInstance = hudType.GetProperty("Instance");
            var previousHud = hudInstance.GetValue(null);
            var hud = Create("SharedHud", root.transform).AddComponent(hudType);
            var detailsField = hudType.GetField("details", PrivateInstance);
            hudInstance.GetSetMethod(true).Invoke(null, new object[] { hud });
            try
            {
                detailsField.SetValue(hud, Create("DetailsBlocker", root.transform).AddComponent(detailsField.FieldType));
                Assert.That((bool)canPresent.Invoke(flow, null), Is.False, "Details must continue to suspend presentation.");
                detailsField.SetValue(hud, null);
            }
            finally { hudInstance.GetSetMethod(true).Invoke(null, new[] { previousHud }); }
            setSession.Invoke(flow, new[] { Enum.Parse(stateType, "Menu") });
            Assert.That((bool)canPresent.Invoke(flow, null), Is.False, "An action cannot bypass the gameplay session gate.");
            setSession.Invoke(flow, new[] { Enum.Parse(stateType, "Playing") });
            presenter.Complete();
            Assert.That(runtime.Actions.IsExecuting, Is.False);
            Assert.That((bool)canPresent.Invoke(flow, null), Is.False, "Finishing the action must restore the idle map gate.");
            Assert.That((bool)mapControls.GetValue(flow), Is.True, "Map controls must recover after the result event completes.");
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

        [Test]
        public void NewActionDotDeclaresAndCreatesRendererBeforeGraphicLifecycle()
        {
            var required = UiType("Spotlight.GameFlow.MapActionNewContentDot").GetCustomAttributes(typeof(RequireComponent), false)
                .Cast<RequireComponent>().ToArray();
            Assert.That(required.Any(attribute => attribute.m_Type0 == typeof(CanvasRenderer)
                || attribute.m_Type1 == typeof(CanvasRenderer) || attribute.m_Type2 == typeof(CanvasRenderer)), Is.True,
                "The custom Graphic must own its renderer dependency, not rely on Graphic's lazy getter.");
            var dotRoot = Create("NewActionDot", root.transform);
            var dot = dotRoot.AddComponent(UiType("Spotlight.GameFlow.MapActionNewContentDot"));
            // Do not access Graphic.canvasRenderer before this assertion: that getter may add
            // a renderer lazily and would conceal the missing construction-time dependency.
            Assert.That(dotRoot.GetComponent<CanvasRenderer>(), Is.Not.Null);
            Assert.That(dotRoot.GetComponents<CanvasRenderer>(), Has.Length.EqualTo(1));
            Assert.That(dotRoot.GetComponent<Canvas>(), Is.Null);
        }

        [Test]
        public void MapBadgeBindingCreatesOneCompleteGraphicAndKeepsItsRenderer()
        {
            var config = new NarrativeConfiguration();
            config.actions.Add(new NarrativeActionDefinition { actionKey = "visible" });
            runtime.Initialize(config, new ImmediatePresenter()); runtime.Actions.Mount("cafe", "visible");
            var mapRoot = Create("MapBadgeBinding", root.transform);
            var map = mapRoot.AddComponent(UiType("Spotlight.GameFlow.MapSceneController")); // Parent stays inactive: no scene services or Awake.
            var location = Create("Location:cafe", mapRoot.transform);
            location.AddComponent(UiType("UnityEngine.UI.Image"));
            var button = location.AddComponent(UiType("UnityEngine.UI.Button"));
            Invoke(map, "BindNewContentDot", button, "cafe");
            var dotRoot = location.transform.Find("NewActionDot");
            Assert.That(dotRoot, Is.Not.Null);
            var renderer = dotRoot.GetComponent<CanvasRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Invoke(map, "BindNewContentDot", button, "cafe");
            Assert.That(location.GetComponentsInChildren(UiType("Spotlight.GameFlow.MapActionNewContentDot"), true), Has.Length.EqualTo(1));
            Assert.That(dotRoot.GetComponent<CanvasRenderer>(), Is.SameAs(renderer));
            Assert.That(dotRoot.GetComponent<Canvas>(), Is.Null);
        }

        [Test]
        public void NewActionDotSurvivesMaskClippingAndCanvasRebuildAfterVisibilityChanges()
        {
            root.AddComponent<Canvas>(); // Isolated test host only; production uses its existing map Canvas.
            var viewport = Create("Viewport", root.transform);
            ((RectTransform)viewport.transform).sizeDelta = new Vector2(100, 100);
            var mask = viewport.AddComponent(UiType("UnityEngine.UI.RectMask2D"));
            var dotRoot = Create("NewActionDot", viewport.transform);
            ((RectTransform)dotRoot.transform).sizeDelta = new Vector2(20, 20);
            var dot = dotRoot.AddComponent(UiType("Spotlight.GameFlow.MapActionNewContentDot"));
            var renderer = dotRoot.GetComponent<CanvasRenderer>();
            Assert.That(renderer, Is.Not.Null, "Renderer must exist before the first Canvas pass.");
            root.SetActive(true);
            Assert.That(Get<bool>(dot, "raycastTarget"), Is.False);
            var clip = mask.GetType().GetMethod("PerformClipping");
            Assert.That(clip, Is.Not.Null);
            Assert.DoesNotThrow(() =>
            {
                Canvas.ForceUpdateCanvases();
                clip.Invoke(mask, null);
                dotRoot.SetActive(false); Canvas.ForceUpdateCanvases();
                dotRoot.SetActive(true); Canvas.ForceUpdateCanvases();
                clip.Invoke(mask, null);
            });
            Assert.That(dotRoot.GetComponent<CanvasRenderer>(), Is.SameAs(renderer));
            Assert.That(dotRoot.GetComponents<CanvasRenderer>(), Has.Length.EqualTo(1));
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
        public void PopupRequiredSizeIncludesExecuteOverflowBeforeContentBinding()
        {
            CreateDetails(null);
            var template = (RectTransform)details.transform.Find("Viewport/ActionList/ActionCardTemplate");
            ConfigureAuthoredExecuteOverflow(template);
            typeof(MapActionDetailsView).GetField("content", PrivateInstance).SetValue(details, null);
            var required = (Vector2)typeof(MapActionDetailsView).GetMethod("GetRequiredPopupSize", PrivateInstance).Invoke(details, null);
            Assert.That(required.x, Is.GreaterThanOrEqualTo(937f));
            Assert.That(required.y, Is.GreaterThanOrEqualTo(626f));
            Assert.That(template.sizeDelta, Is.EqualTo(new Vector2(796, 414)), "Keep authored card geometry intact.");
        }

        [Test]
        public void ActivePageFitsEntireExecuteArtworkWhenPopupIsClamped()
        {
            var config = new NarrativeConfiguration(); config.actions.Add(new NarrativeActionDefinition { actionKey = "fit" });
            runtime.Initialize(config, new ImmediatePresenter()); runtime.Actions.Mount("cafe", "fit");
            CreateDetails(null);
            ConfigureAuthoredExecuteOverflow((RectTransform)details.transform.Find("Viewport/ActionList/ActionCardTemplate"));
            details.Show("cafe", "咖啡馆");
            ((RectTransform)details.transform).sizeDelta = new Vector2(820, 440);
            Invoke(details, "FitVisibleCard");
            var viewport = (RectTransform)details.transform.Find("Viewport");
            var execute = (RectTransform)details.transform.Find("Viewport/ActionList/Action_fit/Execute");
            var corners = new Vector3[4]; execute.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 local = viewport.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(viewport.rect.xMin - .01f, viewport.rect.xMax + .01f));
                Assert.That(local.y, Is.InRange(viewport.rect.yMin - .01f, viewport.rect.yMax + .01f));
            }
        }

        [Test]
        public void VisibleTextRegionDoesNotRequireWholeCardAndRejectsTextOutsideMask()
        {
            CreateDetails(null);
            var viewport = (RectTransform)details.transform.Find("Viewport");
            var area = (RectTransform)Create("ReadableTextArea", viewport).transform;
            area.anchorMin = area.anchorMax = new Vector2(0, 1); area.pivot = new Vector2(0, 1);
            area.sizeDelta = new Vector2(544, 64); area.anchoredPosition = new Vector2(20, -20);
            var method = typeof(MapActionDetailsView).GetMethod("TryGetVisibleTextPoint", PrivateInstance);
            object[] args = { area, null, Vector2.zero };
            Assert.That((bool)method.Invoke(details, args), Is.True, "Readable title counts regardless of larger decorative card bounds.");
            area.anchoredPosition = new Vector2(20, -2000);
            Assert.That((bool)method.Invoke(details, args), Is.False, "Fully masked text must not count.");
        }

        private static void ConfigureAuthoredExecuteOverflow(RectTransform template)
        {
            var execute = (RectTransform)template.Find("Execute");
            execute.anchorMin = execute.anchorMax = new Vector2(0, 1); execute.pivot = new Vector2(0, 1);
            execute.anchoredPosition = new Vector2(538, -332); execute.sizeDelta = new Vector2(367, 160);
        }

        [Test]
        public void ReplacingMandatoryActionHidesCafeEntryAndOpeningUnrenderedDetailsDoesNotMarkSeen()
        {
            var story = new NarrativeActionDefinition { actionKey = "generic.intro", type = NarrativeActionType.Once, mustExecute = true,
                replacesLocationActions = true, focusLocationOnComplete = true };
            var ordinary = new NarrativeActionDefinition { actionKey = "generic.activity", requirements = "ActionPoint(1)" };
            var config = new NarrativeConfiguration(); config.actions.Add(story); config.actions.Add(ordinary);
            runtime.Initialize(config, new ImmediatePresenter());
            runtime.Actions.Mount("cafe", ordinary.actionKey); runtime.Actions.Mount("cafe", story.actionKey);
            runtime.Actions.ActionPoints.Set(0);
            int navigation = 0; CreateDetails(() => navigation++); details.Show("cafe", "咖啡馆");
            var content = details.transform.Find("Viewport/ActionList");
            Assert.That(content.Find("EnterCafe"), Is.Null);
            Assert.That(content.Find("Action_generic.activity"), Is.Null);
            Assert.That(content.Find("Action_generic.intro"), Is.Not.Null);
            Assert.That(runtime.Actions.HasUnseenActions("cafe"), Is.True, "An inactive hierarchy has never actually displayed the card.");
            Invoke(details, "OnEnterCafe"); Assert.That(navigation, Is.Zero);
            Assert.That(runtime.Actions.Execute(story.actionKey), Is.True); details.Refresh();
            details.Show("cafe", "咖啡馆");
            Assert.That(content.Find("EnterCafe"), Is.Not.Null);
            Assert.That(content.Find("Action_generic.activity"), Is.Not.Null);
            Assert.That(runtime.Actions.PendingFocusPosition, Is.EqualTo("cafe"));
        }

        [Test]
        public void MandatoryZeroCostAndAPActionsBothBecomeTodos()
        {
            var story = new NarrativeActionDefinition { actionKey = "zero", mustExecute = true };
            var activity = new NarrativeActionDefinition { actionKey = "activity", requirements = "ActionPoint(1)" };
            var config = new NarrativeConfiguration(); config.actions.Add(story); config.actions.Add(activity);
            runtime.Initialize(config, new ImmediatePresenter());
            Assert.That(MapTaskPanel.ShouldListAction(story), Is.True);
            Assert.That(MapTaskPanel.ShouldListAction(activity), Is.True);
            var taskRoot = Create("TaskFilter", root.transform); var tasks = taskRoot.AddComponent<MapTaskPanel>();
            tasks.SetItems(new[] { new MapTaskItem { id = "zero" }, new MapTaskItem { id = "activity" } });
            Assert.That(tasks.ItemCount, Is.EqualTo(2));
        }

        [Test]
        public void TodosUseCurrentConditionsReadableLocationsAndPersistedCompletion()
        {
            var config = new NarrativeConfiguration();
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "known" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "gate", ownerType = "action", ownerId = "story",
                condType = "BOOL_EQ", target = "known", value = "true" });
            config.actions.Add(new NarrativeActionDefinition { actionKey = "story", nameText = "name", type = NarrativeActionType.Once, mustExecute = true });
            config.mapPositions.Add(new NarrativeMapPositionDefinition { positionKey = "market", nameText = "place", enabled = true, defaultAction = "story" });
            runtime.Initialize(config, new ImmediatePresenter(), key => key == "name" ? "免费剧情" : key == "place" ? "集市" : key);
            Assert.That(GameplayTodoProjection.Build(runtime), Is.Empty);
            runtime.Controller.State.SetCondition("known", true);
            var item = GameplayTodoProjection.Build(runtime).Single();
            Assert.That(item.location, Is.EqualTo("集市"));
            Assert.That(item.currentTask, Does.Contain("集市").And.Contain("免费剧情"));
            Assert.That(item.priority, Is.Zero);
            Assert.That(item.completed, Is.False);
            Assert.That(GameplayTodoProjection.Build(runtime, _ => false), Is.Empty);
            Assert.That(runtime.Actions.Execute("story"), Is.True);
            item = GameplayTodoProjection.Build(runtime).Single();
            Assert.That(item.completed, Is.True, "Completion remains visible using NCS persisted counters.");
            Assert.That(item.navigationLabel, Is.Null);
        }

        [Test]
        public void TodosKeepCostBlockedActivitiesAndMarkDailyCompletion()
        {
            var config = new NarrativeConfiguration();
            config.actions.Add(new NarrativeActionDefinition { actionKey = "daily", type = NarrativeActionType.Daily, requirements = "ActionPoint(1)" });
            config.mapPositions.Add(new NarrativeMapPositionDefinition { positionKey = "market", enabled = true, defaultAction = "daily" });
            runtime.Initialize(config, new ImmediatePresenter());
            runtime.Actions.ActionPoints.Set(0);
            Assert.That(GameplayTodoProjection.Build(runtime).Single().status, Does.Contain("行动点不足"));
            runtime.Actions.ActionPoints.Set(1);
            Assert.That(runtime.Actions.Execute("daily"), Is.True);
            Assert.That(GameplayTodoProjection.Build(runtime).Single().completed, Is.True);
            runtime.Actions.BeginDay(2);
            Assert.That(GameplayTodoProjection.Build(runtime).Single().completed, Is.False);
        }

        [Test]
        public void IdleTodosSuggestThirdFloorButUtilitiesDoNotPreventRest()
        {
            var config = new NarrativeConfiguration();
            runtime.Initialize(config, new ImmediatePresenter());
            var items = new System.Collections.Generic.List<MapTaskItem> {
                new MapTaskItem { id = "guide.cafe.recipes" }, new MapTaskItem { id = "guide.cafe.area.2" } };
            GameplayTodoProjection.AppendRestGuidance(items, runtime, true);
            var rest = items.Single(i => i.id == GameplayTodoProjection.RestTodoId);
            Assert.That(rest.currentTask, Does.Contain("三楼").And.Contain("点击床"));
            Assert.That(rest.navigationLabel, Is.Not.Empty);
            items.Remove(rest);
            items.Add(new MapTaskItem { id = "tutorial", preventsRest = true });
            GameplayTodoProjection.AppendRestGuidance(items, runtime, true);
            Assert.That(items.Any(i => i.id == GameplayTodoProjection.RestTodoId), Is.False);
            items.Clear();
            GameplayTodoProjection.AppendRestGuidance(items, runtime, false);
            Assert.That(items, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExecutableOrMandatoryActionsPreventSleepSuggestion(bool mandatory)
        {
            var config = new NarrativeConfiguration();
            config.actions.Add(new NarrativeActionDefinition { actionKey = "activity", mustExecute = mandatory,
                requirements = mandatory ? "ActionPoint(1)" : "" });
            config.mapPositions.Add(new NarrativeMapPositionDefinition { positionKey = "market", enabled = true, defaultAction = "activity" });
            runtime.Initialize(config, new ImmediatePresenter());
            if (mandatory) runtime.Actions.ActionPoints.Set(0);
            var items = new System.Collections.Generic.List<MapTaskItem>();
            GameplayTodoProjection.AppendRestGuidance(items, runtime, true);
            Assert.That(items, Is.Empty, "Do not infer idle from only the visible scene's utility list.");
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
