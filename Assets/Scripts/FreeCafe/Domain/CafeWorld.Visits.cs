using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        public CafeVisitInstance CafeVisit(string id) => State.cafeVisits.Find(v => v.visitInstanceId == id);
        private CafeVisitInstance VisitForCustomer(string id) => State.cafeVisits.Find(v => v.customerId == id && v.status != CafeVisitStatus.Ended);
        private static bool AwaitingSeat(CafeVisitInstance v) => v.status <= CafeVisitStatus.WaitingForSeat;
        private void VisitDiagnostic(CafeVisitInstance v, string diagnostic)
        {
            if (!v.diagnostics.Contains(diagnostic)) v.diagnostics.Add(diagnostic);
        }
        private void EmitVisit(CafeVisitInstance v, string stage, bool click = false)
        {
            if (!click && v.emittedStages.Contains(stage)) return;
            if (!click) v.emittedStages.Add(stage);
            var request = State.requests.Find(r => r.id == v.orderId);
            State.cafeVisitFeedback.Add(new CafeVisitFeedback { visitInstanceId = v.visitInstanceId,
                characterId = v.characterId, stage = stage, reason = v.endReason,
                orderId = v.orderId, productId = request?.productId ?? v.serviceReceipt?.productId ?? v.orderProductId,
                serviceReceipt = stage == "service_completed" ? v.serviceReceipt : null,
                sequence = ++State.cafeVisitFeedbackSequence, isClick = click });
        }

        /// <summary>Call only after both save participants are ready. Missing entries are not cancellation.</summary>
        public ActionResult SyncCafeVisits(IEnumerable<CafeVisitIntent> intents)
        {
            if (intents == null) return ActionResult.No("来访快照尚未就绪");
            foreach (var input in intents)
            {
                if (input == null || string.IsNullOrWhiteSpace(input.visitInstanceId) || string.IsNullOrWhiteSpace(input.characterId)) continue;
                var v = CafeVisit(input.visitInstanceId);
                if (v == null)
                {
                    v = new CafeVisitInstance { visitInstanceId = input.visitInstanceId, characterId = input.characterId,
                        visitKey = input.visitKey, preferredSeatId = input.preferredSeatId, seatedVisualPath = input.seatedVisualPath,
                        arrivalAuthorized = input.arrivalAuthorized, leaveAfterServed = input.leaveAfterServed,
                        usesDefaultConfiguration = input.usesDefaultConfiguration,
                        orderProductId = input.orderProductId, requireRealProduction = input.requireRealProduction,
                        arrivalDelaySeconds = Finite(input.arrivalDelaySeconds) && input.arrivalDelaySeconds >= 0 ? input.arrivalDelaySeconds : 0,
                        patienceRemaining = input.hasPatienceOverride && Finite(input.patienceSeconds) && input.patienceSeconds > 0 ? input.patienceSeconds : Catalog.customerPatience };
                    var identity = input.identity;
                    if (identity != null) v.identity = new CafePersonIdentity { personId = identity.personId, characterId = input.characterId,
                        displayName = identity.displayName, narrativeCharacterId = identity.narrativeCharacterId,
                        visualId = identity.visualId, role = identity.role };
                    State.cafeVisits.Add(v);
                    if (input.isEnded) { FinishVisit(v, input.endReason ?? "ncs_despawn"); continue; }
                    if (!ValidCustomerIdentity(v.identity)) { FinishVisit(v, "configuration_invalid"); continue; }
                    if (!string.IsNullOrEmpty(v.orderProductId) && Product(v.orderProductId) == null
                        || v.requireRealProduction && string.IsNullOrWhiteSpace(v.orderProductId))
                    { VisitDiagnostic(v, "invalid_order_product"); FinishVisit(v, "configuration_invalid"); continue; }
                    if (!Finite(input.arrivalDelaySeconds) || input.arrivalDelaySeconds < 0) VisitDiagnostic(v, "invalid_arrival_delay");
                    if (input.hasPatienceOverride && (!Finite(input.patienceSeconds) || input.patienceSeconds <= 0)) VisitDiagnostic(v, "invalid_patience");
                    if (!string.IsNullOrEmpty(v.preferredSeatId) && SeatIndex(v.preferredSeatId) < 0)
                    { VisitDiagnostic(v, "invalid_seat"); v.preferredSeatId = null; v.seatedVisualPath = null; }
                    foreach (var other in State.cafeVisits)
                        if (other != v && other.status != CafeVisitStatus.Ended && !string.IsNullOrEmpty(v.preferredSeatId) && other.preferredSeatId == v.preferredSeatId)
                            VisitDiagnostic(v, "seat_conflict");
                    v.budgetReserved = !State.open || State.todayArrivals + ReservedVisitBudget() < State.todayCustomerBudget;
                    EmitVisit(v, "reserved");
                }
                if (v.status == CafeVisitStatus.Ended) continue;
                if (input.isEnded) { FinishVisit(v, input.endReason ?? "ncs_despawn"); continue; }
                // Replayed snapshots cannot revoke already committed authorization or reset a timer.
                v.arrivalAuthorized |= input.arrivalAuthorized;
            }
            ProcessVisitArrivals();
            Notify();
            return ActionResult.Yes("来访状态已同步");
        }

        private int SeatIndex(string id)
        {
            if (id != null && id.StartsWith("customer_", StringComparison.Ordinal) && int.TryParse(id.Substring(9), out int index)
                && index >= 0 && index < State.seats && id == "customer_" + index) return index;
            return -1;
        }
        private int ReservedVisitBudget()
        {
            int count = 0;
            foreach (var v in State.cafeVisits) if (AwaitingSeat(v) && v.budgetReserved) count++;
            return count;
        }
        private int AvailableSeat(CafeVisitInstance applicant = null)
        {
            int genericReservations = 0;
            foreach (var earlier in State.cafeVisits)
            {
                if (earlier == applicant) break;
                if (AwaitingSeat(earlier) && string.IsNullOrEmpty(earlier.preferredSeatId)) genericReservations++;
            }
            for (int i = 0; i < State.seats; i++)
            {
                string seat = "customer_" + i;
                if (applicant != null && !string.IsNullOrEmpty(applicant.preferredSeatId) && applicant.preferredSeatId != seat) continue;
                if (Find(seat) != null) continue;
                bool reserved = false;
                foreach (var v in State.cafeVisits)
                {
                    if (v == applicant) break;
                    if (v.status != CafeVisitStatus.Ended && (v.preferredSeatId == seat || v.seatId == seat)) { reserved = true; break; }
                }
                // Ordinary visitors must respect all reservations; generic applicants respect later dedicated seats too.
                if (!reserved && (applicant == null || string.IsNullOrEmpty(applicant.preferredSeatId)))
                    foreach (var v in State.cafeVisits)
                        if (v != applicant && v.status != CafeVisitStatus.Ended && v.preferredSeatId == seat) { reserved = true; break; }
                if (!reserved)
                {
                    if ((applicant == null || string.IsNullOrEmpty(applicant.preferredSeatId)) && genericReservations-- > 0) continue;
                    return i;
                }
            }
            return -1;
        }
        private void StartVisitDay()
        {
            State.businessElapsed = 0;
            int minimum = 0;
            foreach (var v in State.cafeVisits) if (AwaitingSeat(v)) { v.budgetReserved = true; minimum++; }
            State.cafeVisitBudgetMinimum = minimum;
            if (State.todayCustomerBudget < minimum)
            {
                State.todayCustomerBudget = minimum;
                foreach (var v in State.cafeVisits) if (AwaitingSeat(v)) VisitDiagnostic(v, "customer_budget_minimum_adjusted");
            }
            ProcessVisitArrivals();
        }
        private void ProcessVisitArrivals()
        {
            if (!State.open) return;
            foreach (var v in State.cafeVisits)
            {
                if (!AwaitingSeat(v)) continue;
                if (!v.arrivalAuthorized) { v.status = CafeVisitStatus.Reserved; continue; }
                if (State.businessElapsed < v.arrivalDelaySeconds) { v.status = CafeVisitStatus.PendingArrival; continue; }
                v.status = CafeVisitStatus.WaitingForSeat;
                if (!v.budgetReserved && State.todayArrivals + ReservedVisitBudget() < State.todayCustomerBudget) v.budgetReserved = true;
                if (!v.budgetReserved) { VisitDiagnostic(v, "budget_unavailable"); EmitVisit(v, "waiting"); continue; }
                bool duplicate = State.entities.Exists(e => e.person != null && (e.person.personId == v.identity.personId || e.person.characterId == v.characterId));
                int slot = duplicate ? -1 : AvailableSeat(v);
                if (slot < 0) { if (duplicate) VisitDiagnostic(v, "character_already_present"); EmitVisit(v, "waiting"); continue; }
                var eligible = new List<ProductSpec>();
                foreach (var p in Catalog.products) if (State.unlockedProducts.Contains(p.id)) eligible.Add(p);
                if (eligible.Count == 0 && string.IsNullOrEmpty(v.orderProductId)) continue;
                var product = string.IsNullOrEmpty(v.orderProductId) ? eligible[State.requestSequence++ % eligible.Count] : Product(v.orderProductId);
                if (product == null) { FinishVisit(v, "configuration_invalid"); continue; }
                v.seatId = v.customerId = "customer_" + slot;
                State.entities.Add(new WorldEntity { id = v.customerId, specId = "customer", area = AreaId.Cafe,
                    x = -6 + (slot % 4) * 3.5f, y = .5f - (slot / 4) * 2.5f, person = v.identity });
                var request = new CustomerRequest { id = Allocate("order_"), customerId = v.customerId, productId = product.id, patience = v.patienceRemaining };
                if (!v.requireRealProduction && State.automaticOrders && NextRandom() % 100 < (uint)(Catalog.economy.autoChance * 100)) request.automaticRemaining = Catalog.economy.autoSeconds;
                State.requests.Add(request);
                v.orderId = request.id;
                State.todayArrivals++;
                v.budgetReserved = false;
                v.status = CafeVisitStatus.SeatedBeforeServe;
                EmitVisit(v, "arrived");
            }
            UpdateFocusedRequest();
        }

        public ActionResult RequestCafeVisitClick(string visitInstanceId)
        {
            var v = CafeVisit(visitInstanceId);
            if (v == null || (v.status != CafeVisitStatus.SeatedBeforeServe && (v.status != CafeVisitStatus.Served || v.leaveAfterServed)))
                return ActionResult.No("该来访当前不可交互");
            EmitVisit(v, v.status == CafeVisitStatus.Served ? "served" : "before_served", true);
            Notify(); return ActionResult.Yes("来访交互已排队");
        }
        public ActionResult AcknowledgeCafeVisitFeedback(long sequence)
        {
            var feedback = State.cafeVisitFeedback.Find(f => f.sequence == sequence);
            if (feedback == null) return ActionResult.Yes("反馈已完成");
            State.cafeVisitFeedback.Remove(feedback);
            var v = CafeVisit(feedback.visitInstanceId);
            if (v != null && !feedback.isClick)
            {
                if (feedback.stage == "served" && v.status == CafeVisitStatus.Served && v.leaveAfterServed) BeginVisitLeaving(v, "served");
                else if (feedback.stage == "leaving" && v.status == CafeVisitStatus.Leaving) FinishVisit(v, v.endReason);
            }
            ProcessVisitArrivals(); Notify(); return ActionResult.Yes("反馈已完成");
        }
        public ActionResult EndCafeVisit(string visitInstanceId, string reason)
        {
            var v = CafeVisit(visitInstanceId);
            if (v == null) return ActionResult.No("来访不存在");
            FinishVisit(v, string.IsNullOrEmpty(reason) ? "ncs_despawn" : reason);
            ProcessVisitArrivals(); Notify(); return ActionResult.Yes("来访已结束");
        }
        private void RemoveVisitOrder(CafeVisitInstance v)
        {
            var request = State.requests.Find(r => r.customerId == v.customerId);
            if (request != null) { State.requests.Remove(request); State.todayFailed++; }
            UpdateFocusedRequest();
        }
        private void BeginVisitLeaving(CafeVisitInstance v, string reason)
        {
            if (v.status >= CafeVisitStatus.Leaving) return;
            RemoveVisitOrder(v); v.endReason = reason; v.status = CafeVisitStatus.Leaving; v.budgetReserved = false;
            State.cafeVisitFeedback.RemoveAll(f => f.visitInstanceId == v.visitInstanceId && f.isClick);
            EmitVisit(v, "leaving");
        }
        private void FinishVisit(CafeVisitInstance v, string reason)
        {
            if (v.status == CafeVisitStatus.Ended) return;
            BeginVisitLeaving(v, reason);
            v.endReason = reason;
            State.entities.Remove(Find(v.customerId));
            State.spokenCustomers.Remove(v.customerId);
            v.status = CafeVisitStatus.Ended; v.customerId = null; v.seatId = null; v.budgetReserved = false;
            EmitVisit(v, "ended");
        }
        private bool RequiresRealProduction(CustomerRequest request) => request != null && VisitForCustomer(request.customerId)?.requireRealProduction == true;

        // All final order paths use this after their one economic settlement. Remakes never call it.
        private void CompleteCustomerOrder(CustomerRequest request, bool served)
        {
            if (request == null || !State.requests.Remove(request)) return;
            State.spokenCustomers.Remove(request.customerId);
            var v = VisitForCustomer(request.customerId);
            if (v == null) State.entities.Remove(Find(request.customerId));
            else if (served)
            {
                v.patienceRemaining = Math.Max(0, request.patience);
                v.status = CafeVisitStatus.Served; EmitVisit(v, "served");
            }
            else BeginVisitLeaving(v, "order_failed");
            UpdateFocusedRequest();
        }
        private bool TickVisits(float seconds)
        {
            bool changed = false;
            foreach (var v in State.cafeVisits)
            {
                if (v.status == CafeVisitStatus.SeatedBeforeServe)
                {
                    var request = State.requests.Find(r => r.customerId == v.customerId);
                    if (request != null) v.patienceRemaining = Math.Max(0, request.patience);
                }
                else if (v.status == CafeVisitStatus.Served)
                {
                    // A stage handoff cannot race its presentation, even with a delayed adapter.
                    if (State.cafeVisitFeedback.Exists(f => f.visitInstanceId == v.visitInstanceId && !f.isClick && f.stage == "served")) continue;
                    v.patienceRemaining = Math.Max(0, v.patienceRemaining - seconds);
                    if (v.patienceRemaining == 0) { BeginVisitLeaving(v, "patience_expired"); changed = true; }
                }
            }
            return changed;
        }
    }
}
