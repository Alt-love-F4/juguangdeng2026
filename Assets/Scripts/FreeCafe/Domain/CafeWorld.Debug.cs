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
    }
}
