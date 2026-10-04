using System;
using UnityEngine;

namespace Spotlight.Gameplay
{
    public enum DayPhase { NotStarted, Running, Ended }

    public sealed class DayCycleController
    {
        private readonly PlayerProfile profile;
        private readonly OrderManager orderManager;

        public DayPhase Phase { get; private set; } = DayPhase.NotStarted;
        public float TimeRemaining { get; private set; }
        public float TotalTime { get; private set; }
        public int DayNumber { get; private set; }

        public event Action OnDayStarted;
        public event Action OnDayEnded;
        public event Action<float> OnTimeTick;

        public DayCycleController(PlayerProfile profile, OrderManager orderManager)
        {
            this.profile = profile;
            this.orderManager = orderManager;
            TotalTime = GameplayConfigService.GetParamFloat("daily_duration_sec", 300f);
            TimeRemaining = TotalTime;
        }

        public void StartDay()
        {
            if (Phase == DayPhase.Running) return;
            DayNumber++;
            profile.ResetDaily();
            orderManager.GenerateDailyOrders(profile.Reputation);
            Phase = DayPhase.Running;
            TimeRemaining = TotalTime;
            OnDayStarted?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            if (Phase != DayPhase.Running) return;
            TimeRemaining -= deltaTime;
            OnTimeTick?.Invoke(TimeRemaining);
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                EndDay();
            }
        }

        public void Restore(DayPhase phase, float remaining, int number)
        { Phase = phase; TimeRemaining = Mathf.Clamp(remaining, 0, TotalTime); DayNumber = Mathf.Max(0, number); }

        public void EndDay()
        {
            if (Phase != DayPhase.Running) return;
            Phase = DayPhase.Ended;
            int completed = orderManager.CompletedToday;
            profile.TodayOrdersCompleted = completed;

            int repBase = GameplayConfigService.GetParamInt("reputation_per_10orders", 10);
            int target = 10;
            int repDelta = completed >= target
                ? repBase + (completed - target)
                : Mathf.Max(0, repBase - (target - completed));
            int previousReputation = profile.Reputation;
            profile.AddReputation(repDelta);
            profile.TodayReputationDelta = repDelta;
            profile.TotalOrdersCompleted += completed;

            int skillPer = GameplayConfigService.GetParamInt("skillpoint_per_10rep", 1);
            if (skillPer > 0)
                profile.SkillPoints += (profile.Reputation / 10 - previousReputation / 10) * skillPer;
            profile.LastDaySummary = $"第 {DayNumber} 天：完成 {completed} 单，营业收入 {profile.TodayRevenue}，声望 +{repDelta}";

            OnDayEnded?.Invoke();
        }
    }
}
