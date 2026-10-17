using System;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        public ActionResult DebugAddMoney(int amount)
        {
            if (amount <= 0) return ActionResult.No("金币增量必须大于 0");
            State.money = Math.Min(int.MaxValue, State.money + amount);
            Notify();
            return ActionResult.Yes("金币 +" + amount);
        }

        public ActionResult DebugAddReputation(int amount)
        {
            if (amount <= 0) return ActionResult.No("声望增量必须大于 0");
            State.reputation = Math.Min(int.MaxValue, State.reputation + amount);
            UnlockContent();
            Notify();
            return ActionResult.Yes("声望 +" + amount);
        }

        public ActionResult DebugRefillFree()
        {
            int changed = 0;
            foreach (var entity in State.entities)
            {
                var spec = Spec(entity.specId);
                if (spec == null || !spec.Has(Capability.Source)) continue;
                if (entity.stock != spec.initialStock) changed++;
                entity.stock = spec.initialStock;
            }

            if (changed == 0) return ActionResult.No("所有来源库存已经是满的");
            Notify();
            return ActionResult.Yes("免费补满 " + changed + " 个来源");
        }

        public ActionResult DebugSetTime(float seconds)
        {
            if (!Finite(seconds) || seconds < 0) return ActionResult.No("时间必须是非负有限数值");
            State.timeRemaining = Math.Min(Catalog.daySeconds, seconds);
            Notify();
            return ActionResult.Yes("营业剩余时间设为 " + State.timeRemaining.ToString("0.0") + " 秒");
        }

        public ActionResult DebugSpawnCustomers(int count)
        {
            if (count <= 0 || count > 20) return ActionResult.No("一次只能生成 1~20 位顾客");
            if (!State.open) return ActionResult.No("请先开始营业，再生成顾客");
            State.todayCustomerBudget = Math.Min(Catalog.economy.maxCustomers, State.todayCustomerBudget + count);
            int before = State.todayArrivals;
            for (int i = 0; i < count; i++) SpawnCustomer();
            int created = State.todayArrivals - before;
            if (created == 0) return ActionResult.No("没有空卡座，无法生成顾客");
            Notify();
            return ActionResult.Yes("生成顾客 " + created + " 位");
        }

        public ActionResult DebugEndBusiness()
        {
            if (!State.open) return ActionResult.No("当前没有营业");
            return EndDay();
        }

        public ActionResult DebugSummonCustomer(CafePersonIdentity identity, int seatIndex = -1,
            bool allowDuplicate = true, bool replaceExisting = false)
        {
            if (!ValidCustomerIdentity(identity)) return ActionResult.No("顾客身份无效");
            if (!State.open) return ActionResult.No("请先开始营业，再召唤指定顾客");
            if (seatIndex < -1 || seatIndex >= State.seats) return ActionResult.No("指定座位无效");
            if (!Array.Exists(Catalog.products, product => State.unlockedProducts.Contains(product.id)))
                return ActionResult.No("尚无已解锁餐品，无法生成订单");

            int slot = seatIndex >= 0 ? seatIndex : AvailableSeat();
            if (slot < 0) return ActionResult.No("没有空座，请选择指定位置并开启挤位");
            string targetId = "customer_" + slot;
            var target = Find(targetId);
            bool force = seatIndex >= 0 && replaceExisting;
            if (!force && AvailableSeat(null, slot) < 0)
                return ActionResult.No("指定位置已有顾客或剧情预留，请开启“挤掉原客人”");

            var displacedVisits = new System.Collections.Generic.List<CafeVisitInstance>();
            foreach (var visit in State.cafeVisits)
                if (force && visit.status != CafeVisitStatus.Ended
                    && (visit.customerId == targetId || visit.preferredSeatId == targetId && AwaitingSeat(visit)))
                    displacedVisits.Add(visit);
            if (!allowDuplicate)
            {
                foreach (var entity in State.entities)
                    if (entity != target && SameDebugCustomer(entity.person, identity))
                        return ActionResult.No(identity.displayName + " 已经在店里；请开启“允许重复召唤”");
                foreach (var visit in State.cafeVisits)
                    if (visit.status != CafeVisitStatus.Ended && !displacedVisits.Contains(visit)
                        && SameDebugCustomer(visit.identity, identity))
                        return ActionResult.No(identity.displayName + " 已有剧情来访预约；请开启“允许重复召唤”");
            }

            int reserved = ReservedVisitBudget();
            foreach (var visit in displacedVisits)
                if (AwaitingSeat(visit) && visit.budgetReserved) reserved--;
            bool reuseArrival = target != null;
            int requiredBudget = State.todayArrivals + reserved + (reuseArrival ? 0 : 1);
            if (requiredBudget > Math.Max(Catalog.economy.maxCustomers, State.cafeVisitBudgetMinimum))
                return ActionResult.No("今日客流已达上限，请下一营业日再召唤");

            foreach (var visit in displacedVisits) FinishVisit(visit, "gm_replaced");
            State.requests.RemoveAll(request => request.customerId == targetId);
            State.spokenCustomers.Remove(targetId);
            if (target != null) State.entities.Remove(target);
            if (reuseArrival) State.todayArrivals--;
            State.todayCustomerBudget = Math.Max(State.todayCustomerBudget, requiredBudget);
            SpawnCustomer(new CafePersonIdentity
            {
                personId = identity.personId + ":gm:" + Allocate("person_"), characterId = identity.characterId,
                narrativeCharacterId = identity.narrativeCharacterId, displayName = identity.displayName,
                visualId = identity.visualId, role = CafePersonRole.Customer
            }, slot, seatIndex < 0);
            UpdateFocusedRequest();
            Notify();
            return ActionResult.Yes("已召唤 " + identity.displayName + " 到位置 " + (slot + 1)
                + (target != null ? "，已替换 " + (target.person?.displayName ?? "原客人") : ""));
        }

        private static bool SameDebugCustomer(CafePersonIdentity candidate, CafePersonIdentity identity) => candidate != null
            && (candidate.personId == identity.personId
                || candidate.personId != null && candidate.personId.StartsWith(identity.personId + ":gm:", StringComparison.Ordinal)
                || !string.IsNullOrEmpty(identity.characterId) && candidate.characterId == identity.characterId);
    }
}
