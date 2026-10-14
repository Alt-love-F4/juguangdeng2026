using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Spotlight.GameFlow;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Spotlight.FreeCafe.Tests
{
    // Integration of the authored CoffeeShop, Excel/Luban events and the scene-owned bridge.
    // Immediate presentation is only a deterministic NCS adapter; these are not UI tests.
    public sealed class FreeCafeVisitPlayModeTests
    {
        private FreeCafeRuntime app;
        private NarrativeRuntime narrative;
        private bool addedNarrative;
        private IDisposable pause;
        private CafeNarrativeValueProvider cafeValues;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            PlayerCafeOwner.EndSession();
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop");
            yield return null;
            yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            Assert.That(app, Is.Not.Null);
            app.areaManager.panSmoothTime = 0;
            app.Enter(AreaId.Cafe);
            narrative = NarrativeRuntime.Instance;
            addedNarrative = narrative == null;
            if (addedNarrative)
            {
                // Reuse the authored runtime object; no test Scene, Canvas or input objects.
                var scene = app.gameObject.scene;
                narrative = app.gameObject.AddComponent<NarrativeRuntime>();
                // NarrativeRuntime.Awake may mark this existing root DontDestroyOnLoad.
                SceneManager.MoveGameObjectToScene(app.transform.root.gameObject, scene);
            }
            narrative.Initialize(LubanNarrativeLoader.Load(), new ImmediateNarrativePresenter());
            cafeValues = new CafeNarrativeValueProvider(app.World);
            narrative.Controller.BindValueProvider(cafeValues);
            narrative.Controller.MoneyAccount = cafeValues;
            narrative.Controller.BindValueProvider(new DelegateNarrativeValueProvider("actions", key =>
                key == "current" ? narrative.Actions.ActionPoints.Current : key == "max" ? narrative.Actions.ActionPoints.Max : (int?)null));
            narrative.Controller.BindValueProvider(new DelegateNarrativeValueProvider("dayflow", key =>
                key == "day" ? app.World.State.day : (int?)null));
            narrative.Controller.CompleteValueRestore(false);
            yield return null;
            Assert.That(narrative.IsReady, Is.True);
            Assert.That(app.World.State.open, Is.False);
            Assert.That(app.World.State.requests, Is.Empty);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            pause?.Dispose();
            pause = null;
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            if (narrative != null)
            {
                if (addedNarrative) Object.Destroy(narrative);
                else narrative.ResetSession();
            }
            cafeValues?.Dispose();
            cafeValues = null;
            PlayerCafeOwner.EndSession();
            yield return null;
        }

        private void Queue(string eventId)
        {
            Assert.That(narrative.QueueEvent(eventId), Is.True, "Formal Excel event: " + eventId);
        }

        private CafeVisitInstance Visit => app.World.State.cafeVisits.Single(v => v.visitKey == "demo_cafe_visit");
        private CustomerRequest Order => app.World.State.requests.SingleOrDefault(r => r.customerId == Visit.customerId);

        private IEnumerator OpenDemo()
        {
            Queue("EV_DEMO_CAFE_RESERVE");
            yield return WaitFor(() => app.World.State.cafeVisits.Any(v => v.visitKey == "demo_cafe_visit"),
                "Formal reserve event did not publish/reconcile a committed visit.");
            Assert.That(Visit.status, Is.EqualTo(CafeVisitStatus.Reserved));
            Assert.That(Visit.arrivalAuthorized, Is.False);
            Assert.That(app.World.State.requests, Is.Empty, "Reservation must not create an order while closed.");
            Queue("EV_DEMO_CAFE_ARRIVE");
            // Request immediately: TryStartBusiness itself must reconcile the latest NCS commit.
            app.RequestBusinessStart();
            yield return WaitFor(() => app.World.State.open && Visit.status == CafeVisitStatus.SeatedBeforeServe,
                "Authorized demo visitor was not seated on opening.");
            yield return WaitFor(() => app.World.State.cafeVisitFeedback.Count == 0, "Arrival feedback did not drain.");
        }

        private static IEnumerator WaitFor(Func<bool> condition, string failure)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, failure);
        }

        [UnityTest]
        public IEnumerator ExcelReservationAndArrivalWinOpeningSeatAndCreateOneRealOrder()
        {
            yield return OpenDemo();
            Assert.That(Visit.characterId, Is.EqualTo("adele"));
            Assert.That(Visit.customerId, Is.EqualTo("customer_0"));
            Assert.That(Visit.leaveAfterServed, Is.False);
            Assert.That(Order, Is.Not.Null);
            Assert.That(app.World.Product(Order.productId), Is.Not.Null);
            Assert.That(app.World.Find(Visit.customerId).person.narrativeCharacterId, Is.EqualTo("adele"));
            Assert.That(app.View(Visit.customerId).GetComponent<FreeCafePersonView>().Identity.narrativeCharacterId,
                Is.EqualTo("adele"), "The authored scene must render the special identity.");
            Assert.That(app.World.State.requests[0], Is.SameAs(Order), "Special visitor must order before ordinary opening customers.");
            Assert.That(app.World.State.todayArrivals, Is.EqualTo(app.World.State.requests.Count));
            Assert.That(app.World.State.todayArrivals, Is.EqualTo(Math.Min(app.World.State.seats, app.World.State.todayCustomerBudget)));
            int arrivals = app.World.State.todayArrivals;
            string orderId = Order.id;
            yield return null;
            yield return null;
            Assert.That(Order.id, Is.EqualTo(orderId));
            Assert.That(app.World.State.todayArrivals, Is.EqualTo(arrivals), "Repeated bridge pumps must not duplicate arrival.");
        }

        [UnityTest]
        public IEnumerator PresentationPauseFreezesBusinessElapsedAndBothPatienceClocksThenResumes()
        {
            yield return OpenDemo();
            pause = PresentationPauseRegistry.Acquire("FreeCafeVisit integration test");
            float elapsed = app.World.State.businessElapsed;
            float patience = Order.patience;
            float visitPatience = Visit.patienceRemaining;
            Assert.That(app.BusinessSimulationPaused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1), "Test the simulation gate independently of Unity timeScale.");
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(app.World.State.businessElapsed, Is.EqualTo(elapsed));
            Assert.That(Order.patience, Is.EqualTo(patience));
            Assert.That(Visit.patienceRemaining, Is.EqualTo(visitPatience));
            pause.Dispose();
            pause = null;
            yield return WaitFor(() => app.World.State.businessElapsed > elapsed, "Business did not resume after presentation pause.");
            Assert.That(Order.patience, Is.LessThan(patience));
            Assert.That(Visit.patienceRemaining, Is.LessThan(visitPatience));
        }

        [UnityTest]
        public IEnumerator PaidAutomaticOrdersSettleSpecialCustomerOnceAndKeepServedVisitorInScene()
        {
            Assert.That(app.World.DebugAddMoney(app.World.Catalog.economy.hireCost).success, Is.True);
            // This isolated scene has no session Update to flush external-value batches.
            // Flush at each completed owner-operation boundary, as GameFlowBootstrap does.
            narrative.Controller.FlushValueChanges();
            int beforeHire = app.World.State.money;
            Assert.That(app.World.Hire("orders").success, Is.True);
            Assert.That(app.World.State.money, Is.EqualTo(beforeHire - app.World.Catalog.economy.hireCost));
            narrative.Controller.FlushValueChanges();
            Assert.That(narrative.Controller.IsSafeBoundary, Is.True, "Hire must settle before the reserve event.");
            // The saved RNG is deterministic: opening budget consumes draw 1, special auto-order draw 2.
            // Seed 3 yields 11/100 for draw 2, within the formal catalog's 20% chance.
            app.World.State.randomState = 3;
            yield return OpenDemo();
            var request = Order;
            Assert.That(request.automaticRemaining, Is.GreaterThan(0), "The real hired-worker lottery must authorize this order.");
            string customer = Visit.customerId;
            int money = app.World.State.money;
            int revenue = app.World.State.todayRevenue;
            int served = app.World.State.todayServed;
            int lifetimeServed = app.World.State.served;
            int expectedPrice = app.World.State.requests.Where(r => r.automaticRemaining >= 0 && r.automaticRemaining <= request.automaticRemaining)
                .Sum(r => app.World.Product(r.productId).price);
            int expectedServed = app.World.State.requests.Count(r => r.automaticRemaining >= 0 && r.automaticRemaining <= request.automaticRemaining);
            // Advance the domain's real timers, without fabricating a cup, zeroing timers or awarding money.
            app.World.Tick(request.automaticRemaining + .01f);
            narrative.Controller.FlushValueChanges();
            yield return WaitFor(() => app.World.State.cafeVisitFeedback.Count == 0, "Served feedback did not drain.");
            Assert.That(Visit.status, Is.EqualTo(CafeVisitStatus.Served));
            Assert.That(Order, Is.Null);
            Assert.That(app.World.State.money, Is.EqualTo(money + expectedPrice));
            Assert.That(app.World.State.todayRevenue, Is.EqualTo(revenue + expectedPrice));
            Assert.That(app.World.State.todayServed, Is.EqualTo(served + expectedServed));
            Assert.That(app.World.State.served, Is.EqualTo(lifetimeServed + expectedServed));
            Assert.That(app.World.Find(customer), Is.Not.Null);
            Assert.That(app.View(customer), Is.Not.Null);
            int settledRevenue = app.World.State.todayRevenue;
            int settledServed = app.World.State.todayServed;
            app.RestoreJson(app.CaptureJson());
            yield return null;
            yield return null;
            Assert.That(Visit.status, Is.EqualTo(CafeVisitStatus.Served));
            Assert.That(Order, Is.Null);
            Assert.That(app.World.State.todayRevenue, Is.EqualTo(settledRevenue));
            Assert.That(app.World.State.todayServed, Is.EqualTo(settledServed));
            Assert.That(app.View(customer), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ClosingFeedbackEndsNarrativeVisitAndRestoredCafeCannotResurrectIt()
        {
            yield return OpenDemo();
            string id = Visit.visitInstanceId;
            int failures = app.World.State.todayFailed;
            int outstanding = app.World.State.requests.Count;
            app.EndBusiness();
            yield return WaitFor(() => Visit.status == CafeVisitStatus.Ended && app.World.State.cafeVisitFeedback.Count == 0,
                "Leaving/ended feedback did not reach terminal state.");
            var committed = narrative.GetCommittedCafeVisits().Visits.Single(v => v.VisitInstanceId == id);
            Assert.That(committed.IsEnded, Is.True);
            Assert.That(committed.EndReason, Is.EqualTo("business_closed"));
            Assert.That(app.World.State.todayFailed, Is.EqualTo(failures + outstanding));
            Assert.That(app.World.State.requests, Is.Empty);
            string json = app.CaptureJson();
            long sequence = app.World.State.cafeVisitFeedbackSequence;
            app.RestoreJson(json);
            yield return null;
            yield return null;
            Assert.That(Visit.status, Is.EqualTo(CafeVisitStatus.Ended));
            Assert.That(Visit.customerId, Is.Null.Or.Empty);
            Assert.That(app.World.State.cafeVisitFeedbackSequence, Is.EqualTo(sequence));
            Assert.That(app.World.State.todayFailed, Is.EqualTo(failures + outstanding));
            Assert.That(app.World.State.entities.Any(e => e.person != null && e.person.narrativeCharacterId == "adele"), Is.False);
        }
    }
}
