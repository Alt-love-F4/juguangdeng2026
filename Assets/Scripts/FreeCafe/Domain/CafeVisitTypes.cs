using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    public enum CafeVisitStatus { Reserved, PendingArrival, WaitingForSeat, SeatedBeforeServe, Served, Leaving, Ended }

    /// <summary>Adapter-owned input, copied on acceptance. Reservation alone never authorizes arrival.</summary>
    [Serializable]
    public sealed class CafeVisitIntent
    {
        public string visitInstanceId, characterId, visitKey, preferredSeatId, seatedVisualPath, endReason, orderProductId;
        public bool requireRealProduction;
        public bool arrivalAuthorized, isEnded, usesDefaultConfiguration, hasPatienceOverride;
        public float arrivalDelaySeconds, patienceSeconds;
        public bool leaveAfterServed = true;
        public CafePersonIdentity identity;
    }

    [Serializable]
    public sealed class CafeVisitInstance
    {
        public string visitInstanceId, characterId, visitKey, preferredSeatId, seatedVisualPath;
        public string seatId, customerId, endReason, orderId, orderProductId;
        public bool requireRealProduction;
        public CafeServiceReceipt serviceReceipt;
        public List<CafeProductionAttemptReceipt> productionAttempts = new List<CafeProductionAttemptReceipt>();
        public CafePersonIdentity identity;
        public CafeVisitStatus status;
        public bool arrivalAuthorized, leaveAfterServed = true, usesDefaultConfiguration, budgetReserved;
        public float arrivalDelaySeconds, patienceRemaining;
        public List<string> emittedStages = new List<string>();
        public List<string> diagnostics = new List<string>();
    }

    /// <summary>Final real-cup settlement, recorded before removing the request. No receipt for remakes or auto orders.</summary>
    [Serializable]
    public sealed class CafeServiceReceipt
    {
        public string visitInstanceId, characterId, orderId, productId, cupId, classifiedProductId;
        public bool realProduction, satisfied, correctType, latteArtSatisfied;
        public float score, minimumScore;
        // Null inline fields become default objects in Unity JSON. Do not erase partially corrupt receipts.
        internal static CafeServiceReceipt Normalize(CafeServiceReceipt value)
            => value != null && string.IsNullOrEmpty(value.visitInstanceId) && string.IsNullOrEmpty(value.characterId)
                && string.IsNullOrEmpty(value.orderId) && string.IsNullOrEmpty(value.productId) && string.IsNullOrEmpty(value.cupId)
                && string.IsNullOrEmpty(value.classifiedProductId) && !value.realProduction && !value.satisfied && !value.correctType
                && !value.latteArtSatisfied && value.score == 0 && value.minimumScore == 0 ? null : value;
    }

    /// <summary>One accepted, consumed real cup. Satisfied is the existing business result, not a narrative grade.</summary>
    [Serializable]
    public sealed class CafeProductionAttemptReceipt
    {
        public string visitInstanceId, orderId, cupId, productId, classifiedProductId;
        // Monotonic within the visit; retained attempts are never acknowledged or removed.
        public long attemptSequence;
        public float score, minimumScore;
        public bool correctType, latteArtSatisfied, satisfied, willRemake;

        internal CafeProductionAttemptReceipt Copy() => (CafeProductionAttemptReceipt)MemberwiseClone();
    }

    [Serializable]
    public sealed class CafeVisitFeedback
    {
        public string visitInstanceId, characterId, stage, reason, orderId, productId;
        public CafeServiceReceipt serviceReceipt;
        public long sequence;
        public bool isClick;
    }
}
