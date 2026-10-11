#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>Asset-independent EditMode coverage for the fixed map HUD.</summary>
    public sealed class MapActionPointsViewTests
    {
        private GameObject root;
        private GameObject runtimeObject;
        private NarrativeRuntime previousRuntime;
        private NarrativeActionController actions;
        private MapActionPointsView view;
        private Component label;
        private Component date;
        private Component fill;
        private Component[] segments;
        private PropertyInfo instanceProperty;

        private sealed class ImmediatePresenter : INarrativePresenter
        {
            public void Play(NarrativeEventRequest request, Action completed) => completed?.Invoke();
        }

        [SetUp]
        public void SetUp()
        {
            instanceProperty = typeof(NarrativeRuntime).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            Assert.That(instanceProperty, Is.Not.Null);
            previousRuntime = NarrativeRuntime.Instance;

            // Keep Runtime inactive: Awake would load Luban data and call DontDestroyOnLoad.
            // Inject only the in-memory state read by the HUD, preserving any existing singleton.
            runtimeObject = new GameObject("MapHudTestRuntime");
            runtimeObject.SetActive(false);
            var runtime = runtimeObject.AddComponent<NarrativeRuntime>();
            var controller = new NarrativeController(new NarrativeConfiguration(), new ImmediatePresenter());
            actions = new NarrativeActionController(controller, Array.Empty<NarrativeActionDefinition>(), 8);
            Field(typeof(NarrativeRuntime), "actionController").SetValue(runtime, actions);
            instanceProperty.GetSetMethod(true).Invoke(null, new object[] { runtime });

            root = new GameObject("MapHudTestRoot", typeof(RectTransform));
            view = root.AddComponent<MapActionPointsView>();
            label = CreateText("ActionPoints");
            date = CreateText("Date");
            fill = CreateImage("LegacyFill");
            segments = CreateSegments(4);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (runtimeObject != null) Object.DestroyImmediate(runtimeObject);
            if (instanceProperty != null)
                instanceProperty.GetSetMethod(true).Invoke(null, new object[] { previousRuntime });
        }

        [TestCase(8, 0, 0f, 0f, 0f, 0f, 0f)]
        [TestCase(8, 3, .375f, 1f, .5f, 0f, 0f)]
        [TestCase(8, 8, 1f, 1f, 1f, 1f, 1f)]
        [TestCase(3, 2, 2f / 3f, 1f, 1f, 2f / 3f, 0f)]
        [TestCase(0, 0, 0f, 0f, 0f, 0f, 0f)]
        public void InitializeUsesActualMaximumForLegacyAndSegmentedFill(
            int maximum, int current, float ratio, float first, float second, float third, float fourth)
        {
            actions.ActionPoints.SetMax(maximum);
            actions.ActionPoints.Set(current);

            Initialize(label, fill, date, segments);

            Assert.That(Fill(fill), Is.EqualTo(ratio).Within(.00001f));
            AssertSegments(first, second, third, fourth);
            Assert.That(Text(label), Is.EqualTo($"行动点 {current} / {maximum}    |    第 1 天"));
        }

        [Test]
        public void RefreshRecalculatesRatioWhenMaximumChangesWithoutPointConsumption()
        {
            actions.ActionPoints.Set(3);
            Initialize(label, fill, date, segments);
            Assert.That(Fill(fill), Is.EqualTo(.375f).Within(.00001f));

            actions.ActionPoints.SetMax(12);
            view.Refresh();

            Assert.That(Fill(fill), Is.EqualTo(.25f).Within(.00001f));
            AssertSegments(1f, 0f, 0f, 0f);
            Assert.That(Text(label), Does.Contain("3 / 12"));
        }

        [Test]
        public void SegmentDistributionUsesProvidedSegmentCountNotFive()
        {
            segments = CreateSegments(3);
            actions.ActionPoints.Set(4);

            Initialize(null, null, null, segments);

            AssertSegments(1f, .5f, 0f);
        }

        [TestCase(1, "10  06")]
        [TestCase(26, "10  31")]
        [TestCase(27, "11  01")]
        [TestCase(56, "11  30")]
        [TestCase(57, "12  01")]
        [TestCase(88, "01  01")]
        public void DateUsesCalendarArithmeticAcrossMonthAndYearBoundaries(int day, string expected)
        {
            actions.BeginDay(day);

            Initialize(label, fill, date, segments);

            Assert.That(Text(date), Is.EqualTo(expected));
            Assert.That(Text(label), Does.Contain($"第 {day} 天"));
        }

        [Test]
        public void HudUpdateRefreshesConsumedPointsAndNewDayWhileInventoryIsHidden()
        {
            var inventoryObject = new GameObject("HiddenInventory", typeof(RectTransform));
            inventoryObject.transform.SetParent(root.transform, false);
            inventoryObject.AddComponent<MapActionInventoryView>();
            inventoryObject.SetActive(false);
            Initialize(label, fill, date, segments);
            AssertSegments(1f, 1f, 1f, 1f);

            Assert.That(actions.ActionPoints.TryConsume(5), Is.True);
            TickHud();

            Assert.That(inventoryObject.activeInHierarchy, Is.False);
            Assert.That(view.isActiveAndEnabled, Is.True);
            Assert.That(Fill(fill), Is.EqualTo(.375f).Within(.00001f));
            AssertSegments(1f, .5f, 0f, 0f);
            Assert.That(Text(label), Does.Contain("3 / 8"));

            actions.BeginDay(27);
            TickHud();

            Assert.That(inventoryObject.activeInHierarchy, Is.False);
            Assert.That(Fill(fill), Is.EqualTo(1f));
            AssertSegments(1f, 1f, 1f, 1f);
            Assert.That(Text(date), Is.EqualTo("11  01"));
            Assert.That(Text(label), Does.Contain("第 27 天"));
        }

        [Test]
        public void RefreshToleratesMissingOptionalWidgetsAndNullSegmentEntries()
        {
            actions.ActionPoints.Set(3);
            segments[1] = null;

            Assert.DoesNotThrow(() => Initialize(null, null, null, segments));
            Assert.That(Fill(segments[0]), Is.EqualTo(1f));
            Assert.That(Fill(segments[2]), Is.Zero);
            Assert.That(Fill(segments[3]), Is.Zero);
            Assert.DoesNotThrow(() => Initialize(null, null, null, null));
        }

        private void TickHud()
        {
            // EditMode does not run MonoBehaviour.Update. Invoke its real polling entry point
            // with the throttle expired, without advancing frames or entering Play Mode.
            Field(typeof(MapActionPointsView), "nextRefresh").SetValue(view, float.NegativeInfinity);
            MethodInfo update = typeof(MapActionPointsView).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null);
            update.Invoke(view, null);
        }

        // The existing Editor-only GameFlow tests assembly intentionally has no TMP/UGUI
        // references. Resolve widget types from the production Initialize signature instead
        // of changing its assembly definition or depending on generated prefab assets.
        private void Initialize(Component text, Component legacyFill, Component dateText, Component[] pointImages)
        {
            MethodInfo initialize = typeof(MapActionPointsView).GetMethod("Initialize");
            Assert.That(initialize, Is.Not.Null);
            Type elementType = initialize.GetParameters()[3].ParameterType.GetElementType();
            Array images = pointImages == null ? null : Array.CreateInstance(elementType, pointImages.Length);
            if (images != null)
                for (int i = 0; i < pointImages.Length; i++) images.SetValue(pointImages[i], i);
            initialize.Invoke(view, new object[] { text, legacyFill, dateText, images });
        }

        private Component CreateText(string name) => CreateWidget(name, 0, true);
        private Component CreateImage(string name) => CreateWidget(name, 1, false);

        private Component CreateWidget(string name, int parameter, bool inactive)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            // Text assignment needs no font rendering; avoid TMP asset initialization.
            if (inactive) go.SetActive(false);
            Type type = typeof(MapActionPointsView).GetMethod("Initialize").GetParameters()[parameter].ParameterType;
            return go.AddComponent(type);
        }

        private Component[] CreateSegments(int count)
        {
            var result = new Component[count];
            for (int i = 0; i < count; i++) result[i] = CreateImage("Segment" + i);
            return result;
        }

        private static float Fill(Component image) => (float)image.GetType().GetProperty("fillAmount").GetValue(image);
        private static string Text(Component text) => (string)text.GetType().GetProperty("text").GetValue(text);

        private void AssertSegments(params float[] expected)
        {
            Assert.That(segments.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
                Assert.That(Fill(segments[i]), Is.EqualTo(expected[i]).Within(.00001f), "Segment " + i);
        }

        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, type.Name + "." + name);
            return field;
        }
    }
}
#endif
