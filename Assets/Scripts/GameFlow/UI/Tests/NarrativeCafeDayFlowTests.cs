using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>Headless controller/gate tests. No new scene, Canvas, UI prefab or input system.</summary>
    public sealed class NarrativeCafeDayFlowTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject controllerObject, runtimeObject;
        private DayFlowController days;
        private NarrativeRuntime runtime, previousRuntime;
        private PropertyInfo runtimeInstance;
        private PendingPresenter presenter;

        private sealed class PendingPresenter : INarrativePresenter
        {
            public readonly List<string> Played = new List<string>();
            public Action Completion;
            public void Play(NarrativeEventRequest request, Action completed)
            { Played.Add(request.Event.eventId); Completion = completed; }
            public void Complete() { var callback = Completion; Completion = null; callback?.Invoke(); }
        }

        [SetUp]
        public void SetUp()
        {
            runtimeInstance = typeof(NarrativeRuntime).GetProperty("Instance");
            previousRuntime = NarrativeRuntime.Instance;
            runtimeObject = new GameObject("NarrativeCafeDayFlowRuntime");
            runtimeObject.SetActive(false);
            runtime = runtimeObject.AddComponent<NarrativeRuntime>();
            runtimeInstance.GetSetMethod(true).Invoke(null, new object[] { runtime });
            presenter = new PendingPresenter();
            InitializeRuntime(new NarrativeConfiguration());
            controllerObject = new GameObject("NarrativeCafeDayFlowController");
            controllerObject.SetActive(false);
            days = controllerObject.AddComponent<DayFlowController>();
            Set("running", true);
            Set("day", 2);
            SetState(DayFlowState.CityMapLeisure);
        }

        [TearDown]
        public void TearDown()
        {
            if (controllerObject != null) Object.DestroyImmediate(controllerObject);
            if (runtimeObject != null) Object.DestroyImmediate(runtimeObject);
            runtimeInstance.GetSetMethod(true).Invoke(null, new object[] { previousRuntime });
        }

        private void InitializeRuntime(NarrativeConfiguration config)
        {
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999,
                initValue = 2, advanceMode = NarrativeAdvanceMode.Monotonic });
            runtime.Initialize(config, presenter);
            runtime.Controller.SetOfficialDay(2);
            runtime.Controller.ProcessOfficialDay(2);
        }

        private void Set(string field, object value) => typeof(DayFlowController).GetField(field, Private).SetValue(days, value);
        private object Get(string field) => typeof(DayFlowController).GetField(field, Private).GetValue(days);
        private void SetState(DayFlowState state) => Set("<State>k__BackingField", state);
        private bool Gate() => (bool)typeof(DayFlowController).GetMethod("CanRequestDayEnd", Private).Invoke(days, null);
        private Action NightContinuation() => (Action)Delegate.CreateDelegate(typeof(Action), days,
            typeof(DayFlowController).GetMethod("RunBeforeNightAndCompleteDay", Private));

        private NarrativeConfiguration PhaseConfig()
        {
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "after", entry = NarrativeEntryPoint.AfterBusiness, onceOnly = true });
            config.events.Add(new NarrativeEventDefinition { eventId = "night", entry = NarrativeEntryPoint.BeforeNight, onceOnly = true });
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "after", day = 2, timeSlot = NarrativeTimeSlot.Noon, repeatRule = NarrativeRepeatRule.Once });
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "night", day = 2, timeSlot = NarrativeTimeSlot.Night, repeatRule = NarrativeRepeatRule.Once });
            return config;
        }

        [TestCase(DayFlowState.CityMapLeisure)]
        [TestCase(DayFlowState.FreeCafeBusiness)]
        [TestCase(DayFlowState.PreLeisure)]
        public void DormitoryCannotBypassMountedMandatoryAction(DayFlowState state)
        {
            var config = new NarrativeConfiguration();
            config.actions.Add(new NarrativeActionDefinition { actionKey = "mandatory", mustExecute = true });
            InitializeRuntime(config);
            runtime.Actions.Mount("market", "mandatory");
            SetState(state);
            Assert.That(runtime.Actions.HasBlockingActions(), Is.True);
            Assert.That(Gate(), Is.False);
            days.EndDayFromDormitory();
            days.EndDayFromMap();
            Assert.That(days.CurrentDay, Is.EqualTo(2));
            Assert.That(days.State, Is.EqualTo(state));
            Assert.That(presenter.Played, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BedDoesNotMarkUnfinishedTutorialCompleted(bool tutorialStarted)
        {
            Set("day", 1);
            Set("firstDayCafeTutorialStarted", tutorialStarted);
            SetState(DayFlowState.FreeCafeBusiness);
            Assert.That(days.IsFirstSessionGuideActive, Is.True);
            days.EndDayFromDormitory();
            Assert.That(days.CurrentDay, Is.EqualTo(1));
            Assert.That((bool)Get("firstDayCafeTutorialCompleted"), Is.False);
            Assert.That(days.IsFirstSessionGuideActive, Is.True);
            Assert.That((bool)Get("firstDayCafeTutorialStarted"), Is.EqualTo(tutorialStarted));
        }

        [Test]
        public void BedOnMapRunsBeforeNightInsteadOfAdvancingDateImmediately()
        {
            InitializeRuntime(PhaseConfig());
            days.EndDayFromDormitory();
            Assert.That(presenter.Played, Is.EqualTo(new[] { "night" }));
            Assert.That(days.CurrentDay, Is.EqualTo(2));
            Assert.That(days.State, Is.EqualTo(DayFlowState.NarrativeEntry));
            Assert.That(days.IsNarrativeEntryActive, Is.True);
            days.EndDayFromDormitory();
            days.EndDayFromMap();
            Assert.That(presenter.Played.Count, Is.EqualTo(1), "Repeated bed/map clicks cannot enqueue another night.");
        }

        [Test]
        public void SharedSettlementContinuationRunsAfterBusinessBeforeBeforeNight()
        {
            InitializeRuntime(PhaseConfig());
            SetState(DayFlowState.PreLeisure);
            typeof(DayFlowController).GetMethod("RunNarrativeEntry", Private).Invoke(days,
                new object[] { NarrativeEntryPoint.AfterBusiness, NightContinuation() });
            Assert.That(presenter.Played, Is.EqualTo(new[] { "after" }));
            Assert.That(days.CurrentDay, Is.EqualTo(2));
            presenter.Complete();
            Assert.That(presenter.Played, Is.EqualTo(new[] { "after", "night" }));
            Assert.That(days.CurrentDay, Is.EqualTo(2), "BeforeNight has not completed yet.");
        }

        [Test]
        public void NewMandatoryActionAfterBusinessStopsNightAndKeepsDay()
        {
            var config = PhaseConfig();
            config.actions.Add(new NarrativeActionDefinition { actionKey = "new-mandatory", mustExecute = true });
            InitializeRuntime(config);
            SetState(DayFlowState.PreLeisure);
            typeof(DayFlowController).GetMethod("RunNarrativeEntry", Private).Invoke(days,
                new object[] { NarrativeEntryPoint.AfterBusiness, NightContinuation() });
            runtime.Actions.Mount("market", "new-mandatory");
            presenter.Complete();
            Assert.That(presenter.Played, Is.EqualTo(new[] { "after" }));
            Assert.That(days.CurrentDay, Is.EqualTo(2));
            Assert.That(days.State, Is.EqualTo(DayFlowState.CityMapLeisure));
        }

        [Test]
        public void PendingSceneArrivalDoesNotDeclareCafeReadyAndNeverOpensDayEndGate()
        {
            SetState(DayFlowState.NarrativeEntry);
            var field = typeof(DayFlowController).GetField("cafeGameplaySession", Private);
            int callbacks = 0;
            var pending = (IDisposable)Activator.CreateInstance(field.FieldType,
                new object[] { (Action<NarrativeGameplayResult>)(_ => callbacks++), null, null });
            field.SetValue(days, pending);
            Assert.That(days.IsNarrativeCafeGameplayActive, Is.False);
            Assert.That(days.BlocksGameplayInput, Is.True, "A pending transition is not a ready cafe.");
            SetState(DayFlowState.FreeCafeBusiness);
            Assert.That(days.BlocksGameplayInput, Is.True, "A pending handoff also blocks an otherwise interactive cafe phase.");
            SetState(DayFlowState.CityMapLeisure);
            Assert.That(days.BlocksGameplayInput, Is.True, "An otherwise interactive map phase cannot declare the destination ready.");
            SetState(DayFlowState.NarrativeEntry);
            Assert.That(days.CanSave, Is.False);
            Assert.That(Gate(), Is.False);
            var world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction(); world.EnterProduction();
            object[] args = { world, "serve:espresso:60:1", null };
            Assert.That((bool)field.FieldType.GetMethod("Attach").Invoke(pending, args), Is.True, args[2] as string);
            Assert.That(days.IsNarrativeCafeGameplayActive, Is.True);
            Assert.That((bool)field.FieldType.GetMethod("Attach").Invoke(pending, args), Is.False,
                "A duplicate scene-arrival callback cannot leak a second observation.");
            Assert.That(days.BlocksGameplayInput, Is.False, "Only ready real cafe gameplay temporarily opens the DayFlow input gate.");
            Assert.That(Gate(), Is.False, "Interactive cafe gameplay must not open end-day or other story entrances.");
            pending.Dispose();
            Assert.That(days.BlocksGameplayInput, Is.True);
            Assert.That(callbacks, Is.Zero, "Presenter-owned disposal cannot publish a stale result.");
        }
    }
}
