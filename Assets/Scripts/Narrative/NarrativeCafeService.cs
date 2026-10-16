using System;

namespace Spotlight.Narrative
{
    /// <summary>Exact order selected by a visit hook, never inferred from a reused customer seat.</summary>
    [Serializable]
    public sealed class NarrativeCafeVisitContext
    {
        public string visitInstanceId, characterId, orderId, productId;
        // Unity's inline serializer expands null class fields into all-default objects.
        // Only the exact empty shell is absence; partial/nonempty identities remain invalid data.
        internal NarrativeCafeVisitContext Copy() => IsEmpty ? null : (NarrativeCafeVisitContext)MemberwiseClone();
        private bool IsEmpty => string.IsNullOrEmpty(visitInstanceId) && string.IsNullOrEmpty(characterId)
            && string.IsNullOrEmpty(orderId) && string.IsNullOrEmpty(productId);
    }

    /// <summary>Immutable-by-copy final production fact. Satisfied is the business verdict, not a second evaluation.</summary>
    [Serializable]
    public sealed class NarrativeCafeServiceReceipt
    {
        public string visitInstanceId, characterId, orderId, productId, cupId, classifiedProductId;
        public bool realProduction, satisfied, correctType, latteArtSatisfied;
        public float score, minimumScore;
        internal NarrativeCafeServiceReceipt Copy() => IsEmpty ? null : (NarrativeCafeServiceReceipt)MemberwiseClone();
        private bool IsEmpty => string.IsNullOrEmpty(visitInstanceId) && string.IsNullOrEmpty(characterId)
            && string.IsNullOrEmpty(orderId) && string.IsNullOrEmpty(productId) && string.IsNullOrEmpty(cupId)
            && string.IsNullOrEmpty(classifiedProductId) && !realProduction && !satisfied && !correctType
            && !latteArtSatisfied && score == 0 && minimumScore == 0;
    }
}
