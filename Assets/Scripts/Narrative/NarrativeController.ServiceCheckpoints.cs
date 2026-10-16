using System;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativeController
    {
        public bool HasPendingServiceCheckpoint => checkpoint != null && !checkpointResuming && checkpoint.wait == NarrativeCheckpointWait.Service;

        private bool EventRequiresServiceContext(NarrativeEventDefinition definition)
            => definition != null && configuration.presentations.conversationPages.Exists(p => p != null
                && p.presentationKey == definition.presentationKey && p.stepType == NarrativeConversationStepType.WaitService);

        private bool ValidServiceContext(NarrativeCafeVisitContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.orderId) || string.IsNullOrWhiteSpace(context.productId)) return false;
            var visit = cafeVisits.Find(v => v.id == context.visitInstanceId && v.characterId == context.characterId);
            return visit != null && visit.requireRealProduction && visit.orderId == context.orderId
                && (!visit.ended && visit.stage != "leaving" || MatchesService(context, visit.serviceReceipt))
                && visit.orderProductId == context.productId;
        }

        private static bool MatchesService(NarrativeCafeVisitContext context, NarrativeCafeServiceReceipt receipt)
            => context != null && receipt != null && receipt.realProduction && !string.IsNullOrWhiteSpace(receipt.cupId)
                && !float.IsNaN(receipt.score) && !float.IsInfinity(receipt.score)
                && !float.IsNaN(receipt.minimumScore) && !float.IsInfinity(receipt.minimumScore)
                && context.visitInstanceId == receipt.visitInstanceId && context.characterId == receipt.characterId
                && context.orderId == receipt.orderId && context.productId == receipt.productId;

        private void BindServiceCheckpoint()
        {
            var visit = cafeVisits.Find(v => v.id == checkpoint.serviceContext.visitInstanceId);
            // Keep the very same business verdict across retries/restores; never re-evaluate a vanished cup.
            if (checkpoint.serviceReceipt == null && MatchesService(checkpoint.serviceContext, visit?.serviceReceipt))
                checkpoint.serviceReceipt = visit.serviceReceipt.Copy();
            checkpoint.confirmed = MatchesService(checkpoint.serviceContext, checkpoint.serviceReceipt);
        }

        private void ReleaseServiceHook(string eventId)
        {
            // The visit's arrived outbox pauses simulation until all hooks settle. This retained root now owns
            // its own checkpoint; release that handshake without committing its results or once marker.
            CompleteCafeHook(eventId);
        }

        /// <summary>Only a validated final production fact can settle this checkpoint. No money is changed here.</summary>
        private bool AcceptServiceReceipt(NarrativeCafeVisitFeedback fact)
        {
            var receipt = fact.serviceReceipt;
            var visit = cafeVisits.Find(v => v.id == fact.visitInstanceId && v.characterId == fact.characterId);
            if (visit == null || (visit.ended || visit.stage == "leaving") && visit.serviceReceipt == null || receipt == null || !receipt.realProduction || string.IsNullOrWhiteSpace(receipt.cupId)
                || receipt.visitInstanceId != visit.id || receipt.characterId != visit.characterId
                || string.IsNullOrWhiteSpace(visit.orderId) || receipt.orderId != visit.orderId
                || receipt.orderId != fact.orderId || receipt.productId != fact.productId
                || !string.IsNullOrEmpty(visit.orderProductId) && receipt.productId != visit.orderProductId
                || float.IsNaN(receipt.score) || float.IsInfinity(receipt.score)
                || float.IsNaN(receipt.minimumScore) || float.IsInfinity(receipt.minimumScore)) return false;
            // A duplicate may acknowledge the original fact but may never replace its verdict.
            if (visit.serviceReceipt == null) { visit.serviceReceipt = receipt.Copy(); cafeDirty = true; }
            else if (visit.serviceReceipt.orderId != receipt.orderId || visit.serviceReceipt.cupId != receipt.cupId
                || visit.serviceReceipt.satisfied != receipt.satisfied || visit.serviceReceipt.score != receipt.score
                || visit.serviceReceipt.minimumScore != receipt.minimumScore || visit.serviceReceipt.correctType != receipt.correctType
                || visit.serviceReceipt.latteArtSatisfied != receipt.latteArtSatisfied
                || visit.serviceReceipt.classifiedProductId != receipt.classifiedProductId) return false;
            if (checkpoint?.wait == NarrativeCheckpointWait.Service && MatchesService(checkpoint.serviceContext, visit.serviceReceipt))
            {
                checkpoint.serviceReceipt ??= visit.serviceReceipt.Copy();
                checkpoint.confirmed = MatchesService(checkpoint.serviceContext, checkpoint.serviceReceipt);
            }
            return true;
        }

        private void PumpServiceCheckpoint()
        {
            if (!HasPendingServiceCheckpoint || checkpoint.serviceResumeBlocked || !checkpoint.confirmed || activeEntry.HasValue
                || IsEnded || presenterUnavailable || !IsSafeBoundary || IsBusy || !CheckpointCanResume()) return;
            EnqueueCheckpoint();
            ProcessQueue();
        }

        private void CancelUnservedServiceCheckpoint(string visitId, string reason)
        {
            if (!HasPendingServiceCheckpoint || checkpoint.serviceContext?.visitInstanceId != visitId || checkpoint.confirmed) return;
            string eventId = checkpoint.root.eventId;
            candidates.RemoveAll(c => c.eventId == eventId && c.source == NarrativeCandidateSource.Manual);
            pendingEvents.Remove(eventId);
            checkpoint = null;
            checkpointResuming = false;
            try { EventFailed?.Invoke(eventId, "service_not_delivered: " + reason); }
            catch (Exception error) { UnityEngine.Debug.LogException(error); }
        }

        /// <summary>Explicit retry after presentation/host cancellation; cannot bypass an absent receipt.</summary>
        public bool ResumeServiceCheckpoint()
        {
            if (!HasPendingServiceCheckpoint || !checkpoint.confirmed || activeEntry.HasValue || IsEnded || presenterUnavailable
                || !IsSafeBoundary || IsBusy || !CheckpointCanResume()) return false;
            checkpoint.serviceResumeBlocked = false;
            EnqueueCheckpoint();
            ProcessQueue();
            return true;
        }

        /// <summary>Read-only tri-state guard: false return means unknown, including under not:.</summary>
        public bool TryReadServiceResultGuard(string key, out bool value)
        {
            value = false;
            bool negate = false;
            while (key != null && key.StartsWith("not:", StringComparison.Ordinal)) { negate = !negate; key = key.Substring(4); }
            if (key != "service:passed" || !activePresentation || !IsCheckpointRoot(activeRequest)
                || !MatchesService(checkpoint.serviceContext, checkpoint.serviceReceipt)) return false;
            value = checkpoint.serviceReceipt.satisfied != negate;
            return true;
        }
    }
}
