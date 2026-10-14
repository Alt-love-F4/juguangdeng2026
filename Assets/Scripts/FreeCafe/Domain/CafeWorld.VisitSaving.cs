using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        private ActionResult ValidateVisitSnapshot(CafeSnapshot candidate, Dictionary<string, WorldEntity> entities)
        {
            candidate.cafeVisits = candidate.cafeVisits ?? new List<CafeVisitInstance>();
            candidate.cafeVisitFeedback = candidate.cafeVisitFeedback ?? new List<CafeVisitFeedback>();
            if (!Finite(candidate.businessElapsed) || candidate.businessElapsed < 0 || candidate.cafeVisitFeedbackSequence < 0
                || candidate.cafeVisitBudgetMinimum < 0 || candidate.cafeVisitBudgetMinimum > candidate.cafeVisits.Count)
                return ActionResult.No("存档来访时钟或预算无效");
            var ids = new HashSet<string>();
            var seats = new HashSet<string>();
            int reserved = 0;
            foreach (var v in candidate.cafeVisits)
            {
                if (v == null || string.IsNullOrWhiteSpace(v.visitInstanceId) || !ids.Add(v.visitInstanceId)
                    || string.IsNullOrWhiteSpace(v.characterId) || !Enum.IsDefined(typeof(CafeVisitStatus), v.status)
                    || !Finite(v.patienceRemaining) || v.patienceRemaining < 0 || !Finite(v.arrivalDelaySeconds) || v.arrivalDelaySeconds < 0)
                    return ActionResult.No("存档来访实例无效");
                v.emittedStages = v.emittedStages ?? new List<string>();
                v.diagnostics = v.diagnostics ?? new List<string>();
                if (AwaitingSeat(v))
                {
                    if (!string.IsNullOrEmpty(v.customerId) || !string.IsNullOrEmpty(v.seatId)) return ActionResult.No("等候来访不能占有人物");
                    if (v.budgetReserved) reserved++;
                }
                else if (v.status != CafeVisitStatus.Ended && !string.IsNullOrEmpty(v.customerId))
                {
                    if (v.seatId != v.customerId || !v.seatId.StartsWith("customer_", StringComparison.Ordinal)
                        || !int.TryParse(v.seatId.Substring(9), out int seatIndex) || seatIndex < 0 || seatIndex >= candidate.seats
                        || v.seatId != "customer_" + seatIndex || !seats.Add(v.seatId) || !entities.TryGetValue(v.customerId, out var entity)
                        || !ValidCustomerIdentity(v.identity) || entity.person == null || entity.person.personId != v.identity.personId)
                        return ActionResult.No("存档来访座位归属无效");
                }
                if ((v.status == CafeVisitStatus.SeatedBeforeServe || v.status == CafeVisitStatus.Served) && string.IsNullOrEmpty(v.customerId))
                    return ActionResult.No("在店来访缺少人物");
                if (v.status != CafeVisitStatus.Ended && !ValidCustomerIdentity(v.identity)) return ActionResult.No("来访人物无效");
                var order = candidate.requests.Find(r => r.customerId == v.customerId);
                if ((v.status == CafeVisitStatus.SeatedBeforeServe) != (order != null)) return ActionResult.No("来访阶段与订单不一致");
                if (v.status == CafeVisitStatus.Ended && (!string.IsNullOrEmpty(v.customerId) || !string.IsNullOrEmpty(v.seatId) || v.budgetReserved))
                    return ActionResult.No("已结束来访残留座位");
            }
            if (candidate.open && candidate.todayArrivals + reserved > candidate.todayCustomerBudget) return ActionResult.No("存档来访预留超过预算");
            var sequences = new HashSet<long>();
            var stages = new HashSet<string>();
            foreach (var f in candidate.cafeVisitFeedback)
            {
                var v = f == null ? null : candidate.cafeVisits.Find(item => item.visitInstanceId == f.visitInstanceId);
                if (v == null || f.characterId != v.characterId || f.sequence <= 0 || f.sequence > candidate.cafeVisitFeedbackSequence
                    || !sequences.Add(f.sequence) || string.IsNullOrEmpty(f.stage)
                    || (!f.isClick && (!v.emittedStages.Contains(f.stage) || !stages.Add(f.visitInstanceId + "\n" + f.stage))))
                    return ActionResult.No("存档来访反馈无效");
            }
            return ActionResult.Yes("来访状态有效");
        }
    }
}
