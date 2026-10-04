using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Gameplay
{
    public enum OrderState { Pending, InProgress, Ready, Served, Failed }

    [Serializable]
    public sealed class CustomerOrder
    {
        public int OrderId;
        public string RecipeId;
        public string CustomerName;
        public string NpcId;
        public int CustomerId;
        public OrderState State = OrderState.Pending;
        public RecipeEntry Recipe => GameplayConfigService.GetRecipe(RecipeId);
    }

    public sealed class OrderManager
    {
        private readonly List<CustomerOrder> orders = new();
        private int nextOrderId = 1;
        private readonly PlayerProfile profile;

        public IReadOnlyList<CustomerOrder> Orders => orders;
        public int PendingCount => orders.FindAll(o => o.State == OrderState.Pending).Count;

        public event Action OnOrdersChanged;

        public OrderManager(PlayerProfile profile) => this.profile = profile;

        public int GetTodayCustomerCount()
        {
            int baseC = GameplayConfigService.GetParamInt("base_customers", 12);
            int perOrders = GameplayConfigService.GetParamInt("customers_per_orders", 30);
            int maxC = GameplayConfigService.GetParamInt("max_customers", 24);
            int bonus = profile.TotalOrdersCompleted / Mathf.Max(1, perOrders);
            int rand = UnityEngine.Random.Range(0, 3);
            return Mathf.Min(baseC + bonus + rand, maxC);
        }

        public void GenerateDailyOrders(int reputation)
        {
            orders.Clear();
            nextOrderId = 1;
            int count = GetTodayCustomerCount();
            var unlocked = new List<RecipeEntry>();
            foreach (var r in GameplayConfigService.AllRecipes)
                if (r.unlock_rep <= reputation)
                    unlocked.Add(r);
            if (unlocked.Count == 0) return;

            var visitors = new List<NpcEntry>();
            foreach (var npc in GameplayConfigService.AllNpcs) if (npc.unlock_rep <= reputation) visitors.Add(npc);
            if (visitors.Count == 0) throw new InvalidOperationException("没有已解锁 NPC 配置");
            for (int i = 0; i < count; i++)
            {
                var recipe = unlocked[UnityEngine.Random.Range(0, unlocked.Count)];
                var visitor = visitors[i % visitors.Count];
                orders.Add(new CustomerOrder
                {
                    OrderId = nextOrderId++,
                    RecipeId = recipe.id,
                    CustomerName = visitor.name, NpcId = visitor.id, CustomerId = visitor.social_id
                });
            }
            OnOrdersChanged?.Invoke();
        }

        public CustomerOrder NextPending => orders.Find(o => o.State == OrderState.Pending);
        public void Restore(CustomerOrder[] saved)
        {
            orders.Clear();
            if (saved != null) foreach (var order in saved) if (order != null && order.Recipe != null) orders.Add(order);
            nextOrderId = 1; foreach (var order in orders) nextOrderId = Mathf.Max(nextOrderId, order.OrderId + 1);
        }

        public CustomerOrder TakeNextPending()
        {
            var order = orders.Find(o => o.State == OrderState.Pending);
            if (order != null) { order.State = OrderState.InProgress; OnOrdersChanged?.Invoke(); }
            return order;
        }

        public void MarkReady(CustomerOrder order)
        {
            if (order.State == OrderState.InProgress) { order.State = OrderState.Ready; OnOrdersChanged?.Invoke(); }
        }

        public void MarkServed(CustomerOrder order)
        {
            if (order.State == OrderState.Ready) { order.State = OrderState.Served; OnOrdersChanged?.Invoke(); }
        }

        public void MarkFailed(CustomerOrder order)
        {
            if (order.State == OrderState.InProgress || order.State == OrderState.Ready)
            { order.State = OrderState.Failed; OnOrdersChanged?.Invoke(); }
        }

        public int CompletedToday => orders.FindAll(o => o.State == OrderState.Served).Count;
    }
}
