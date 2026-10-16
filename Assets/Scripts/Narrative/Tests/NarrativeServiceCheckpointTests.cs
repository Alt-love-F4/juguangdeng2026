using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeServiceCheckpointTests
    {
        private sealed class Presenter : INarrativePresenter, INarrativePresentationCheckpoints, INarrativePresentationCancellation
        {
            public event Action<NarrativeEventRequest, string, NarrativeCheckpointWait> CheckpointReached;
            public event Action<string> Cancelled;
            public readonly List<NarrativeEventRequest> requests = new List<NarrativeEventRequest>();
            public Action complete;
            public NarrativeEventRequest Current => requests.Last();
            public void Play(NarrativeEventRequest request, Action done) { requests.Add(request); complete = done; }
            public void Wait() => CheckpointReached?.Invoke(Current, "after-service", NarrativeCheckpointWait.Service);
            public void Cancel() => Cancelled?.Invoke("host_unavailable");
        }
        private NarrativeConfiguration config;
        private Presenter presenter;
        private NarrativeController controller;
        private string visitId;
        [SetUp] public void Setup()
        {
            config = new NarrativeConfiguration();
            config.objects.Add(new NarrativeObjectDefinition { objId = "reporter", objType = NarrativeObjectType.Npc });
            config.cafeVisits.Add(new NarrativeCafeVisitDefinition { visitKey = "reporter-visit", characterId = "reporter", orderProductId = "espresso", requireRealProduction = true, leaveAfterServed = false });
            config.events.Add(new NarrativeEventDefinition { eventId = "spawn" });
            config.results.Add(new NarrativeResultDefinition { eventId = "spawn", resultType = NarrativeResultType.SpawnNpc, target = "obj:reporter", value = "scene:cafe" });
            config.events.Add(new NarrativeEventDefinition { eventId = "report", onceOnly = true, presentationKey = "report-pages" });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
            { pageId = "wait", presentationKey = "report-pages", seq = 1, stepType = NarrativeConversationStepType.WaitService });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
            { pageId = "after-service", presentationKey = "report-pages", seq = 2 });
            config.cafeVisitHooks.Add(new NarrativeCafeVisitHookDefinition { visitKey = "reporter-visit", stage = "arrived", trigger = "auto", eventId = "report" });
            presenter = new Presenter(); controller = new NarrativeController(config, presenter);
            controller.QueueEvent("spawn"); presenter.complete();
            visitId = controller.GetCommittedCafeVisits().Visits[0].VisitInstanceId;
        }
        private NarrativeCafeVisitFeedback Fact(string stage) => new NarrativeCafeVisitFeedback
        { visitInstanceId = visitId, characterId = "reporter", orderId = "order-exact", productId = "espresso", stage = stage, sequence = 1 };
        private void Wait()
        {
            controller.SubmitCafeVisitFeedback(Fact("arrived"));
            Assert.That(presenter.Current.CafeVisit.orderId, Is.EqualTo("order-exact"));
            presenter.Wait();
            Assert.That(controller.HasPendingServiceCheckpoint);
            Assert.That(controller.IsCafeVisitFeedbackComplete(Fact("arrived")), "WaitService must release simulation's arrived handshake");
            Assert.That(controller.IsBusy, Is.False);
        }
        private NarrativeCafeVisitFeedback Receipt(bool satisfied)
        {
            var fact = Fact("service_completed");
            fact.serviceReceipt = new NarrativeCafeServiceReceipt { visitInstanceId = visitId, characterId = "reporter", orderId = fact.orderId,
                productId = fact.productId, cupId = "cup-real", realProduction = true, satisfied = satisfied, score = satisfied ? 73 : 25,
                minimumScore = 65, correctType = true, latteArtSatisfied = true };
            return fact;
        }
        [Test] public void FinalBusinessReceiptResumesSameRequestOnceAndCopiesVerdict()
        {
            Wait(); string request = controller.CaptureSnapshot().checkpoint.root.requestId;
            var fact = Receipt(true); Assert.That(controller.SubmitCafeVisitFeedback(fact));
            Assert.That(presenter.Current.RequestId, Is.EqualTo(request)); Assert.That(presenter.Current.ResumePageId, Is.EqualTo("after-service"));
            Assert.That(controller.ReadPresentationCondition("service:passed"));
            fact.serviceReceipt.satisfied = false;
            Assert.That(controller.ReadPresentationCondition("service:passed"));
            Assert.That(controller.SubmitCafeVisitFeedback(Receipt(true))); Assert.That(presenter.requests.Count, Is.EqualTo(3));
            presenter.complete(); presenter.complete();
            Assert.That(controller.State.HasHappenedEvent("report"));
            controller.SubmitCafeVisitFeedback(Receipt(true)); Assert.That(presenter.requests.Count, Is.EqualTo(3));
        }
        [Test] public void FailedFinalCupResumesButWrongIdentityAutoAndDepartureNeverConfirm()
        {
            Wait(); var wrong = Receipt(true); wrong.serviceReceipt.orderId = "other-order";
            Assert.That(controller.SubmitCafeVisitFeedback(wrong), Is.False);
            wrong = Receipt(true); wrong.serviceReceipt.realProduction = false;
            Assert.That(controller.SubmitCafeVisitFeedback(wrong), Is.False);
            Assert.That(controller.ResumeServiceCheckpoint(), Is.False);
            Assert.That(controller.TryReadServiceResultGuard("not:service:passed", out _), Is.False);
            Assert.That(controller.SubmitCafeVisitFeedback(Receipt(false)));
            Assert.That(controller.ReadPresentationCondition("service:passed"), Is.False);
            Assert.That(controller.ReadPresentationCondition("not:service:passed"));
        }
        [Test] public void WaitRestoreAndCancelledContinuationRetainSameReceiptForExplicitRetry()
        {
            Wait(); var saved = JsonUtility.FromJson<NarrativeSnapshot>(JsonUtility.ToJson(controller.CaptureSnapshot()));
            var p = new Presenter(); var restored = new NarrativeController(config, p);
            restored.RestoreSnapshot(saved); restored.ResumePendingEvents();
            Assert.That(restored.HasPendingServiceCheckpoint); Assert.That(p.requests, Is.Empty);
            restored.SubmitCafeVisitFeedback(Receipt(false)); Assert.That(p.Current.ResumePageId, Is.EqualTo("after-service"));
            var stale = p.complete; p.Cancel();
            restored.CheckEndingsAtSafeBoundary(); Assert.That(p.requests.Count, Is.EqualTo(1), "cancel must not auto-loop");
            var copy = JsonUtility.FromJson<NarrativeSnapshot>(JsonUtility.ToJson(restored.CaptureSnapshot()));
            Assert.That(copy.checkpoint.serviceReceipt.satisfied, Is.False);
            restored.RestoreSnapshot(copy); Assert.That(restored.ResumeServiceCheckpoint());
            Assert.That(restored.ReadPresentationCondition("not:service:passed"));
            stale(); Assert.That(restored.State.HasHappenedEvent("report"), Is.False);
            p.complete(); Assert.That(restored.State.HasHappenedEvent("report"));
        }
        [Test] public void UnservedDepartureCancelsRootWithoutOnceMarkerAndUnblocksNight()
        {
            Wait(); controller.SubmitCafeVisitFeedback(Fact("leaving"));
            Assert.That(controller.HasPendingServiceCheckpoint, Is.False);
            Assert.That(controller.CaptureSnapshot().checkpoint, Is.Null);
            Assert.That(controller.State.HasHappenedEvent("report"), Is.False);
            Assert.That(controller.State.IsFlagSet("event.once.report"), Is.False);
            Assert.That(controller.ProcessEntry(NarrativeEntryPoint.BeforeNight));
        }
        [Test] public void ResultPresentationGuardUsesOnlyCachedVerdictAndNoUnknownNegationReward()
        {
            config.conditions.Add(new NarrativeConditionDefinition { ownerType = "result", ownerId = "pass", condType = "PRESENTATION", target = "service:passed", value = "true" });
            config.conditions.Add(new NarrativeConditionDefinition { ownerType = "result", ownerId = "fail", condType = "PRESENTATION", target = "not:service:passed", value = "true" });
            presenter = new Presenter(); controller = new NarrativeController(config, presenter);
            controller.QueueEvent("spawn"); presenter.complete(); visitId = controller.GetCommittedCafeVisits().Visits[0].VisitInstanceId;
            Wait(); Assert.That(controller.EvaluateConditions("result", "pass"), Is.False);
            Assert.That(controller.EvaluateConditions("result", "fail"), Is.False);
            controller.SubmitCafeVisitFeedback(Receipt(false));
            Assert.That(controller.EvaluateConditions("result", "pass"), Is.False);
            Assert.That(controller.EvaluateConditions("result", "fail"));
        }
        [Test] public void MissingContextIsRejectedAtAdmissionAndUnservedEndedVisitRejectsLateReceipt()
        {
            Assert.That(controller.QueueEvent("report"), Is.False);
            Assert.That(controller.ExplainEvent("report").Rejection, Does.Contain("WaitService"));
            Wait(); controller.SubmitCafeVisitFeedback(Fact("ended"));
            Assert.That(controller.SubmitCafeVisitFeedback(Receipt(true)), Is.False);
            Assert.That(controller.TryReadServiceResultGuard("not:service:passed", out _), Is.False);
            Assert.That(controller.State.HasHappenedEvent("report"), Is.False);
        }
        [Test] public void WaitServiceLoaderRequiresRealProductHookAndFollowingPage()
        {
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config), Is.Empty);
            Assert.That(LubanNarrativeLoader.ParseConversationStepType("WaitService"), Is.EqualTo(NarrativeConversationStepType.WaitService));
            config.cafeVisitHooks.Clear();
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config).Any(e => e.Contains("real-production product")));
            config.presentations.conversationPages.RemoveAt(1);
            Assert.That(LubanNarrativeLoader.ValidateConversationSteps(config).Any(e => e.Contains("following page")));
        }
        [Test] public void ReceiptWaitsBehindActivePresentationOnExistingSafeQueue()
        {
            config.events.Add(new NarrativeEventDefinition { eventId = "other" });
            // Controllers index configuration at construction; add the unrelated event before rebuilding.
            presenter = new Presenter(); controller = new NarrativeController(config, presenter);
            controller.QueueEvent("spawn"); presenter.complete(); visitId = controller.GetCommittedCafeVisits().Visits[0].VisitInstanceId;
            Wait(); Assert.That(controller.QueueEvent("other"));
            var otherComplete = presenter.complete;
            controller.SubmitCafeVisitFeedback(Receipt(true));
            Assert.That(presenter.Current.Event.eventId, Is.EqualTo("other"));
            Assert.That(controller.TryReadServiceResultGuard("service:passed", out _), Is.False, "another root cannot borrow this receipt");
            otherComplete(); Assert.That(presenter.Current.Event.eventId, Is.EqualTo("report"));
            Assert.That(controller.ReadPresentationCondition("service:passed"));
        }
        [Test] public void UnityInlineEmptyReceiptShellsNormalizeAtVisitFeedbackAndCheckpointBoundaries()
        {
            Wait(); var saved = controller.CaptureSnapshot();
            saved.checkpoint.serviceReceipt = new NarrativeCafeServiceReceipt();
            foreach (var visit in saved.cafeVisits) visit.serviceReceipt = new NarrativeCafeServiceReceipt();
            var feedback = Fact("before_served"); feedback.isClick = true; feedback.serviceReceipt = new NarrativeCafeServiceReceipt();
            saved.cafeFeedback.Add(new NarrativeCafeFeedbackSave { fact = feedback });
            var p = new Presenter(); var restored = new NarrativeController(config, p);
            restored.RestoreSnapshot(saved);
            var normalized = restored.CaptureSnapshot();
            Assert.That(normalized.checkpoint.serviceReceipt, Is.Null);
            Assert.That(normalized.cafeVisits.All(v => v.serviceReceipt == null));
            Assert.That(normalized.cafeFeedback.All(f => f.fact.serviceReceipt == null));
            Assert.That(restored.SubmitCafeVisitFeedback(Receipt(true)));
            Assert.That(p.Current.ResumePageId, Is.EqualTo("after-service"));
            Assert.That(restored.ReadPresentationCondition("service:passed"));
        }
        [Test] public void UnityInlineNormalizationPreservesEveryNondefaultReceiptFieldAndPartialContext()
        {
            Wait();
            foreach (var field in typeof(NarrativeCafeServiceReceipt).GetFields())
            {
                var invalid = new NarrativeCafeServiceReceipt();
                field.SetValue(invalid, field.FieldType == typeof(string) ? (object)"invalid" : field.FieldType == typeof(bool) ? true : 1f);
                var saved = controller.CaptureSnapshot(); saved.checkpoint.serviceReceipt = invalid;
                foreach (var visit in saved.cafeVisits) visit.serviceReceipt = invalid;
                var p = new Presenter(); var restored = new NarrativeController(config, p);
                restored.RestoreSnapshot(saved);
                var captured = restored.CaptureSnapshot();
                Assert.That(captured.checkpoint.serviceReceipt, Is.Not.Null, "Nondefault checkpoint field erased: " + field.Name);
                Assert.That(captured.cafeVisits[0].serviceReceipt, Is.Not.Null, "Nondefault visit field erased: " + field.Name);
                Assert.That(restored.ResumeServiceCheckpoint(), Is.False);
            }
            var contextSaved = controller.CaptureSnapshot();
            contextSaved.checkpoint.serviceContext = new NarrativeCafeVisitContext();
            var contextRestored = new NarrativeController(config, new Presenter()); contextRestored.RestoreSnapshot(contextSaved);
            Assert.That(contextRestored.HasPendingServiceCheckpoint, Is.False);
            // Empty request context is absence, while a partial invalid identity remains visible for admission rejection.
            var pending = new NarrativeEventRequest(config.events[0], null, 0, cafeVisit: new NarrativeCafeVisitContext());
            Assert.That(pending.CafeVisit, Is.Null);
            pending = new NarrativeEventRequest(config.events[0], null, 0, cafeVisit: new NarrativeCafeVisitContext { orderId = "partial" });
            Assert.That(pending.CafeVisit, Is.Not.Null);
        }
        [Test] public void ActiveRootSnapshotAndCachedReceiptRestoreWithoutBorrowingAnotherVisit()
        {
            controller.SubmitCafeVisitFeedback(Fact("arrived"));
            var saved = JsonUtility.FromJson<NarrativeSnapshot>(JsonUtility.ToJson(controller.CaptureSnapshot()));
            var p = new Presenter(); var restored = new NarrativeController(config, p);
            restored.RestoreSnapshot(saved); restored.ResumePendingEvents();
            Assert.That(p.Current.CafeVisit.visitInstanceId, Is.EqualTo(visitId)); p.Wait();
            restored.SubmitCafeVisitFeedback(Receipt(true));
            var active = JsonUtility.FromJson<NarrativeSnapshot>(JsonUtility.ToJson(restored.CaptureSnapshot()));
            restored.RestoreSnapshot(active); Assert.That(restored.ResumeServiceCheckpoint());
            Assert.That(restored.ReadPresentationCondition("service:passed"));
        }
    }
}
