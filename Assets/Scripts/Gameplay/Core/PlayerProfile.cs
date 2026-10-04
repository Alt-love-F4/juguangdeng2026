using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Gameplay
{
    [Serializable]
    public sealed class PlayerProfile
    {
        public int Gold = 0;
        public int Reputation = 0;
        public int SkillPoints = 0;
        public int TotalOrdersCompleted = 0;
        public int TodayOrdersCompleted = 0;
        public int TodayReputationDelta = 0;
        public int TodayRevenue = 0;
        public Dictionary<string, int> IngredientStock = new();
        public Dictionary<string, bool> UpgradedDevices = new();
        public string LastDaySummary = "";

        public void ResetDaily()
        {
            TodayOrdersCompleted = 0;
            TodayReputationDelta = 0;
            TodayRevenue = 0;
        }

        public int GetStock(string id) => IngredientStock.GetValueOrDefault(id, 0);
        public void Consume(string id, int amount)
        {
            if (amount <= 0) return;
            IngredientStock[id] = Mathf.Max(0, GetStock(id) - amount);
        }
        public void Refill(string id, int amount)
        {
            var entry = GameplayConfigService.GetIngredient(id);
            if (entry == null) return;
            int current = GetStock(id);
            int max = entry.max_stock;
            IngredientStock[id] = Mathf.Min(current + amount, max);
        }

        public bool CanAfford(int cost) => Gold >= cost;
        public void Spend(int cost) => Gold = Mathf.Max(0, Gold - cost);
        public void Earn(int amount) => Gold = Mathf.Max(0, Gold + amount);
        public void AddReputation(int amount) => Reputation = Mathf.Max(0, Reputation + amount);

        public static PlayerProfile CreateDefault()
        {
            var p = new PlayerProfile { Gold = GameplayConfigService.GetParamInt("starting_gold", 80) };
            foreach (var ing in GameplayConfigService.AllIngredients)
                p.IngredientStock[ing.id] = ing.max_stock;
            return p;
        }
    }
}
