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
    /// <summary>Real endpoint fixture only; does not construct a scene, Canvas or coffee UI.</summary>
    public sealed class NarrativeCafeHostTestEndpoint : MonoBehaviour, IDayFlowGameplayEndpoint
    {
        public CafeWorld World;
        public bool RefuseClose;
        public int EndCalls;
        public bool DayFlowBusinessActive => World != null && World.State.open;
        public bool DayFlowBusinessFinished => World != null && !World.State.open;
        public void StartDayFlowBusiness() => Assert.Fail("Detention/host closure must never start business.");
        public void EndDayFlowBusiness() { EndCalls++; if (!RefuseClose) World.EndDay(); }
    }

    public sealed class NarrativeCafeHostDetentionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Active = "church.detention.active";
        private GameObject root, runtimeObject;
        private NarrativeRuntime runtime, previousRuntime;
        private DayFlowController days;
        private CafeWorld world;
        private PendingPresenter presenter;
        private NarrativeCafeHostTestEndpoint endpoint;
        private PropertyInfo runtimeInstance;

        private sealed class PendingPresenter : INarrativePresenter
        {
            public readonly List<string> Played = new List<string>();
            public Action Completion;
            public void Play(NarrativeEventRequest request, Action completed) { Played.Add(request.Event.eventId); Completion = completed; }
            public void Complete() { var callback = Completion; Completion = null; callback?.Invoke(); }
        }

        [SetUp]
        public void SetUp()
        {
            PlayerCafeOwner.EndSession();
            runtimeInstance = typeof(NarrativeRuntime).GetProperty("Instance");
            previousRuntime = NarrativeRuntime.Instance;
            runtimeObject = new GameObject("HostDetentionRuntime"); runtimeObject.SetActive(false);
            runtime = runtimeObject.AddComponent<NarrativeRuntime>();
            runtimeInstance.GetSetMethod(true).Invoke(null, new object[] { runtime });
            presenter = new PendingPresenter();
            root = new GameObject("HostDetentionController"); root.SetActive(false);
            days = root.AddComponent<DayFlowController>();
            endpoint = root.AddComponent<NarrativeCafeHostTestEndpoint>();
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction(); world.EnterProduction();
            endpoint.World = world;
            PlayerCafeOwner.Bind(world, _ => { });
            Set("running", true); Set("day", 15); Set("morningCompletedDay", 15);
            Set("endpointOwner", endpoint); Set("gameplayEndpoint", endpoint);
            SetState(DayFlowState.FreeCafeBusiness);
            Initialize(new NarrativeConfiguration(), 15);
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
                PlayerCafeOwner.EndSession();
                runtimeInstance.GetSetMethod(true).Invoke(null, new object[] { previousRuntime });
            }
        }

        private void Initialize(NarrativeConfiguration config, int day)
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = Active, onlySetTrue = false });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "world.map.active", initialValue = true });
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999, initValue = day,
                advanceMode = NarrativeAdvanceMode.Monotonic });
            runtime.Initialize(config, presenter);
            runtime.Controller.SetOfficialDay(day); runtime.Controller.ProcessOfficialDay(day);
        }
        private void Set(string field, object value) => typeof(DayFlowController).GetField(field, Private).SetValue(days, value);
        private void SetState(DayFlowState state) => Set("<State>k__BackingField", state);
        private bool CanAdvance() => (bool)typeof(DayFlowController).GetMethod("CanCompleteDetainedDay", Private).Invoke(days, null);
        private bool Close(out string error)
        {
            object[] args = { null };
            bool ok = (bool)typeof(DayFlowController).GetMethod("TryEndBoundCafeBusiness", Private).Invoke(days, args);
            error = args[0] as string;
            return ok;
        }

        [Test]
        public void HostEndBusinessUsesActualEndpointOnceAndNeverAdvancesDayOrPaysMoney()
        {
            world.StartDay();
            int money = world.State.money, calendar = world.State.day;
            Assert.That(Close(out string error), Is.True, error);
            Assert.That(world.State.open, Is.False);
            Assert.That(endpoint.EndCalls, Is.EqualTo(1));
            Assert.That(world.State.money, Is.EqualTo(money));
            Assert.That(world.State.day, Is.EqualTo(calendar));
            Assert.That(days.CurrentDay, Is.EqualTo(15));
            Assert.That(Close(out error), Is.True, error);
            Assert.That(endpoint.EndCalls, Is.EqualTo(1), "Idempotent already-closed success must not settle twice.");
        }

        [Test]
        public void AlreadyClosedHostDoesNotNeedToStartBusinessOrCallEndpoint()
        {
            string before = JsonUtility.ToJson(world.State);
            Assert.That(Close(out string error), Is.True, error);
            Assert.That(endpoint.EndCalls, Is.Zero);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }

        [Test]
        public void HostCannotFakeSuccessWhenEndpointLeavesActualWorldOpen()
        {
            world.StartDay(); endpoint.RefuseClose = true;
            Assert.That(Close(out string error), Is.False);
            Assert.That(error, Does.Contain("did not close"));
            Assert.That(world.State.open, Is.True);
            Assert.That(days.CurrentDay, Is.EqualTo(15));
        }

        [Test]
        public void HostRequiresAuthoritativeBoundWorld()
        {
            PlayerCafeOwner.EndSession();
            Assert.That(Close(out string error), Is.False);
            Assert.That(error, Does.Contain("bound"));
            Assert.That(endpoint.EndCalls, Is.Zero);
        }

        [Test]
        public void CaptureFactBlocksBusinessMapAndBedWithoutAdvancingAnActiveRoot()
        {
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "capture", entry = NarrativeEntryPoint.MapAction, onceOnly = true });
            Initialize(config, 15);
            Assert.That(runtime.Controller.QueueMapActionEvent("capture"), Is.True);
            runtime.Controller.State.SetCondition(Active, true);
            Assert.That(days.IsDetained, Is.True);
            Assert.That(days.BlocksGameplayInput, Is.True);
            Assert.That(days.CanFinishBusiness, Is.False);
            Assert.That(CanAdvance(), Is.False, "An active narrative root must settle first.");
            days.EndDayFromDormitory(); days.EndDayFromMap(); days.StartCafeFromMap();
            Assert.That(days.CurrentDay, Is.EqualTo(15));
            typeof(DayFlowController).GetMethod("SetMapAvailability", Private).Invoke(days, new object[] { true });
            Assert.That(runtime.Controller.State.TryGetCondition("world.map.active", out bool map), Is.True);
            Assert.That(map, Is.False);
            presenter.Complete();
            Assert.That(CanAdvance(), Is.True, "Once root/action is safe, the existing one-day transition may run.");
        }

        [TestCase(16)]
        [TestCase(17)]
        public void EachDetentionDayMustFinishItsOwnMorningBeforeNaturalTransition(int day)
        {
            Set("day", day); SetState(DayFlowState.NarrativeEntry);
            runtime.Controller.State.SetCondition(Active, true);
            Assert.That(CanAdvance(), Is.False, "Yesterday's morning is not today's morning.");
            Set("morningCompletedDay", day);
            Assert.That(CanAdvance(), Is.True);
            Set("detentionAdvanceDay", day);
            Assert.That(CanAdvance(), Is.False, "A repeated callback cannot advance the same day twice.");
            Assert.That(days.CurrentDay, Is.EqualTo(day));
        }

        [Test]
        public void MorningReleaseClearsCanonicalFactAndContinuesNormallyWithoutFakePayment()
        {
            const int releaseDay = 18;
            Set("day", releaseDay); Set("morningCompletedDay", 17); SetState(DayFlowState.DateTransition);
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "release", entry = NarrativeEntryPoint.MorningBeforeMap, onceOnly = true });
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "release", day = releaseDay,
                timeSlot = NarrativeTimeSlot.Morning, repeatRule = NarrativeRepeatRule.Once });
            Initialize(config, releaseDay);
            runtime.Controller.State.SetCondition(Active, true);
            int continuations = 0, money = world.State.money;
            typeof(DayFlowController).GetMethod("RunNarrativeEntry", Private).Invoke(days,
                new object[] { NarrativeEntryPoint.MorningBeforeMap, (Action)(() => continuations++) });
            Assert.That(presenter.Played, Is.EqualTo(new[] { "release" }));
            Assert.That(continuations, Is.Zero);
            Assert.That(CanAdvance(), Is.False);
            runtime.Controller.State.SetCondition(Active, false); // Source release result owns this write.
            presenter.Complete();
            Assert.That(continuations, Is.EqualTo(1));
            Assert.That(days.IsDetained, Is.False);
            Assert.That(CanAdvance(), Is.False);
            Assert.That(days.CurrentDay, Is.EqualTo(releaseDay));
            Assert.That(world.State.money, Is.EqualTo(money));
            Assert.That(endpoint.EndCalls, Is.Zero);
        }

        [Test]
        public void DetentionWaitsForUnrelatedPauseOwnerAndPausedTime()
        {
            runtime.Controller.State.SetCondition(Active, true);
            float previousTime = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                Assert.That(CanAdvance(), Is.False);
                Time.timeScale = 1;
                // Settings is deliberately not a direct test-assembly reference; inspect its
                // existing public gate instead of adding an unrelated asmdef dependency.
                var registry = Type.GetType("Spotlight.GameFlow.PresentationPauseRegistry, Spotlight.GameFlow.Settings", true);
                using ((IDisposable)registry.GetMethod("Acquire").Invoke(null, new object[] { "unrelated-detention-test-owner" }))
                    Assert.That(CanAdvance(), Is.False, "Natural day transition must not resume or override another owner.");
                Assert.That(CanAdvance(), Is.True);
            }
            finally { Time.timeScale = previousTime; }
        }

        [Test]
        public void HostResultAndDisposedPendingCallbacksAreIdempotent()
        {
            var field = typeof(DayFlowController).GetField("cafeGameplaySession", Private);
            int callbacks = 0;
            NarrativeGameplayResult result = null;
            var pending = (IDisposable)Activator.CreateInstance(field.FieldType,
                new object[] { (Action<NarrativeGameplayResult>)(value => { callbacks++; result = value; }), null, null });
            field.FieldType.GetMethod("CompleteHostCommand").Invoke(pending, null);
            field.FieldType.GetMethod("CompleteHostCommand").Invoke(pending, null);
            pending.Dispose();
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(result.passed, Is.True);
            Assert.That(result.cancelled, Is.False);
            Assert.That(days.CurrentDay, Is.EqualTo(15));
        }

        [Test]
        public void DetentionNeverDependsOnRequestedOrHostReadyShadowFlags()
        {
            Assert.That(days.IsDetained, Is.False);
            runtime.Controller.State.SetCondition(Active, true);
            Assert.That(days.IsDetained, Is.True);
            runtime.Controller.State.SetCondition(Active, false);
            Assert.That(days.IsDetained, Is.False);
        }
    }
}
