using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    public enum CafeVisitStatus { Reserved, PendingArrival, WaitingForSeat, SeatedBeforeServe, Served, Leaving, Ended }

    /// <summary>Adapter-owned input, copied on acceptance. Reservation alone never authorizes arrival.</summary>
    [Serializable]
    public sealed class CafeVisitIntent
    {
        public string visitInstanceId, characterId, visitKey, preferredSeatId, seatedVisualPath, endReason;
        public bool arrivalAuthorized, isEnded, usesDefaultConfiguration, hasPatienceOverride;
        public float arrivalDelaySeconds, patienceSeconds;
        public bool leaveAfterServed = true;
        public CafePersonIdentity identity;
    }

    [Serializable]
    public sealed class CafeVisitInstance
    {
        public string visitInstanceId, characterId, visitKey, preferredSeatId, seatedVisualPath;
        public string seatId, customerId, endReason;
        public CafePersonIdentity identity;
        public CafeVisitStatus status;
        public bool arrivalAuthorized, leaveAfterServed = true, usesDefaultConfiguration, budgetReserved;
        public float arrivalDelaySeconds, patienceRemaining;
        public List<string> emittedStages = new List<string>();
        public List<string> diagnostics = new List<string>();
    }

    [Serializable]
    public sealed class CafeVisitFeedback
    {
        public string visitInstanceId, characterId, stage, reason;
        public long sequence;
        public bool isClick;
    }
}
