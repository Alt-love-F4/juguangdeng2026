using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Spotlight.Narrative
{
    [Serializable]
    public sealed class NarrativeCafeVisitDefinition
    {
        public string visitKey, characterId, preferredSeatId, seatedVisualPath, orderProductId;
        public bool requireRealProduction;
        public float arrivalDelaySeconds;
        public float? patienceSeconds;
        public bool leaveAfterServed = true;
    }

    [Serializable]
    public sealed class NarrativeCafeVisitHookDefinition
    {
        public string visitKey, stage, trigger = "auto", eventId;
        public int seq;
    }

    [Serializable]
    public sealed class NarrativeCafeVisitFeedback
    {
        public string visitInstanceId, characterId, stage, reason, orderId, productId;
        public NarrativeCafeServiceReceipt serviceReceipt;
        public long sequence;
        public bool isClick;
        internal NarrativeCafeVisitFeedback Copy()
        {
            var copy = (NarrativeCafeVisitFeedback)MemberwiseClone();
            copy.serviceReceipt = serviceReceipt?.Copy();
            return copy;
        }
    }

    // Save DTOs are mutable serialization data; committed API objects below never expose them.
    [Serializable]
    public sealed class NarrativeCafeVisitSave
    {
        public string id, characterId, visitKey, preferredSeatId, seatedVisualPath, endReason, stage = "reserved";
        public string orderProductId, orderId;
        public bool requireRealProduction;
        public NarrativeCafeServiceReceipt serviceReceipt;
        public float arrivalDelaySeconds, patienceSeconds;
        public bool hasPatience, leaveAfterServed = true, arrivalAuthorized, ended, defaults;
        public long order;
        public List<string> acceptedFacts = new List<string>();
        public List<string> completedFacts = new List<string>();
        internal NarrativeCafeVisitSave Copy()
        {
            var copy = (NarrativeCafeVisitSave)MemberwiseClone();
            copy.serviceReceipt = serviceReceipt?.Copy();
            copy.acceptedFacts = new List<string>(acceptedFacts ?? new List<string>());
            copy.completedFacts = new List<string>(completedFacts ?? new List<string>());
            return copy;
        }
    }

    [Serializable]
    public sealed class NarrativeCafeFeedbackSave
    {
        public NarrativeCafeVisitFeedback fact;
        public bool accepted;
        public List<string> hooks = new List<string>();
        public int nextHook;
        public string waitingEvent;
        internal NarrativeCafeFeedbackSave Copy() => new NarrativeCafeFeedbackSave
        { fact = fact.Copy(), accepted = accepted, hooks = new List<string>(hooks), nextHook = nextHook, waitingEvent = waitingEvent };
    }

    public sealed class NarrativeCafeVisit
    {
        public string VisitInstanceId { get; }
        public string CharacterId { get; }
        public string VisitKey { get; }
        public string OrderProductId { get; }
        public string OrderId { get; }
        public bool RequireRealProduction { get; }
        public string PreferredSeatId { get; }
        public string SeatedVisualPath { get; }
        public string EndReason { get; }
        public string Stage { get; }
        public float ArrivalDelaySeconds { get; }
        public float? PatienceSeconds { get; }
        public bool LeaveAfterServed { get; }
        public bool ArrivalAuthorized { get; }
        public bool IsEnded { get; }
        public bool UsesDefaultConfiguration { get; }
        public long CommitOrder { get; }
        internal NarrativeCafeVisit(NarrativeCafeVisitSave v)
        {
            VisitInstanceId = v.id; CharacterId = v.characterId; VisitKey = v.visitKey;
            OrderProductId = v.orderProductId; OrderId = v.orderId; RequireRealProduction = v.requireRealProduction;
            PreferredSeatId = v.preferredSeatId; SeatedVisualPath = v.seatedVisualPath;
            EndReason = v.endReason; Stage = v.stage; ArrivalDelaySeconds = v.arrivalDelaySeconds;
            PatienceSeconds = v.hasPatience ? v.patienceSeconds : (float?)null;
            LeaveAfterServed = v.leaveAfterServed; ArrivalAuthorized = v.arrivalAuthorized;
            IsEnded = v.ended; UsesDefaultConfiguration = v.defaults; CommitOrder = v.order;
        }
    }

    public sealed class NarrativeCafeVisitSnapshot
    {
        public long Version { get; }
        public bool IsReady { get; }
        public IReadOnlyList<NarrativeCafeVisit> Visits { get; }
        internal NarrativeCafeVisitSnapshot(long version, bool ready, List<NarrativeCafeVisit> visits)
        { Version = version; IsReady = ready; Visits = new ReadOnlyCollection<NarrativeCafeVisit>(visits); }
    }
}
