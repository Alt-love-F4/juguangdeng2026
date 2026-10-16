using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        /// <summary>Detached facts for one exact visit/order; reading cannot mutate retained scores.</summary>
        public IReadOnlyList<CafeProductionAttemptReceipt> ProductionAttempts(string visitInstanceId, string orderId)
        {
            var result = new List<CafeProductionAttemptReceipt>();
            var visit = CafeVisit(visitInstanceId);
            if (visit == null || string.IsNullOrWhiteSpace(orderId) || visit.orderId != orderId) return result.AsReadOnly();
            if (visit.productionAttempts != null)
                foreach (var attempt in visit.productionAttempts) result.Add(attempt.Copy());
            return result.AsReadOnly();
        }

        private void RecordProductionAttempt(CustomerRequest request, string cupId, string classifiedProductId,
            float score, float minimumScore, bool correctType, bool latteArtSatisfied, bool satisfied, bool willRemake)
        {
            var visit = VisitForCustomer(request.customerId);
            if (visit == null || visit.orderId != request.id) return;
            if (visit.productionAttempts == null) visit.productionAttempts = new List<CafeProductionAttemptReceipt>();
            visit.productionAttempts.Add(new CafeProductionAttemptReceipt { visitInstanceId = visit.visitInstanceId,
                orderId = request.id, cupId = cupId, productId = request.productId, classifiedProductId = classifiedProductId,
                attemptSequence = (long)visit.productionAttempts.Count + 1, score = score, minimumScore = minimumScore,
                correctType = correctType, latteArtSatisfied = latteArtSatisfied, satisfied = satisfied, willRemake = willRemake });
        }

        private bool ValidProductionAttempts(CafeVisitInstance visit, CustomerRequest order)
        {
            // Old saves have no list. Unlike inline optional objects, an item in this list is always a real fact.
            if (visit.productionAttempts == null) visit.productionAttempts = new List<CafeProductionAttemptReceipt>();
            string productId = null;
            int retainedRemakes = 0;
            for (int i = 0; i < visit.productionAttempts.Count; i++)
            {
                var attempt = visit.productionAttempts[i];
                if (attempt == null || attempt.visitInstanceId != visit.visitInstanceId
                    || string.IsNullOrWhiteSpace(attempt.orderId) || attempt.orderId != visit.orderId
                    || string.IsNullOrWhiteSpace(attempt.cupId) || string.IsNullOrWhiteSpace(attempt.productId)
                    || Product(attempt.productId) == null || attempt.attemptSequence != (long)i + 1
                    || (!string.IsNullOrEmpty(attempt.classifiedProductId) && Product(attempt.classifiedProductId) == null)
                    || (!string.IsNullOrEmpty(visit.orderProductId) && attempt.productId != visit.orderProductId)
                    || (productId != null && attempt.productId != productId)
                    || (order != null && attempt.productId != order.productId)
                    || !Finite(attempt.score) || attempt.score < 0 || attempt.score > 100 || !Finite(attempt.minimumScore)
                    || attempt.satisfied != (attempt.correctType && attempt.score >= attempt.minimumScore && attempt.latteArtSatisfied)
                    || (attempt.willRemake && attempt.satisfied)
                    || (!attempt.willRemake && (i != visit.productionAttempts.Count - 1 || order != null || visit.serviceReceipt == null))) return false;
                productId = attempt.productId;
                if (attempt.willRemake) retainedRemakes++;
                if (i > 0 && !visit.productionAttempts[i - 1].willRemake) return false;
            }
            // Legacy remakes may predate receipts, but retained facts cannot exceed the active order's count.
            if (order != null && order.remakes < retainedRemakes) return false;
            if (visit.productionAttempts.Count > 0 && visit.serviceReceipt != null)
            {
                var last = visit.productionAttempts[visit.productionAttempts.Count - 1];
                var final = visit.serviceReceipt;
                if (last.willRemake || last.orderId != final.orderId || last.cupId != final.cupId
                    || last.productId != final.productId || !string.Equals(last.classifiedProductId ?? "", final.classifiedProductId ?? "", StringComparison.Ordinal)
                    || last.score != final.score || last.minimumScore != final.minimumScore || last.correctType != final.correctType
                    || last.latteArtSatisfied != final.latteArtSatisfied || last.satisfied != final.satisfied) return false;
            }
            return true;
        }
    }
}
