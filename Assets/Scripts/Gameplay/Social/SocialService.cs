using System;
using System.Collections.Generic;

namespace Spotlight.Gameplay.Social
{
    /// <summary>
    /// 社交系统扩展接口。当前实现返回默认值，后续可替换为复杂判定系统。
    /// </summary>
    public interface ISocialService
    {
        /// <summary>饮品质量（0~1）影响好感度，返回额外好感度增量</summary>
        int EvaluateDrinkQuality(int customerId, float qualityMultiplier);
        /// <summary>拉花形状影响好感度，返回额外好感度增量</summary>
        int EvaluateLatteArt(int customerId, string latteArtShape);
        /// <summary>订单完成时更新声望/好感度，返回实际应得金币倍率</summary>
        float OnOrderServed(int customerId, float qualityMultiplier, string latteArtShape, int basePrice);
        /// <summary>查询当前玩家声望是否解锁剧情/特殊事件</summary>
        bool CheckStoryTrigger(int reputation, int gold, int totalOrders);
    }

    /// <summary>默认实现：所有判定返回中性值，便于上层游戏循环先跑通。</summary>
    public sealed class NullSocialService : ISocialService
    {
        public static readonly NullSocialService Instance = new();
        public int EvaluateDrinkQuality(int customerId, float qualityMultiplier) => 0;
        public int EvaluateLatteArt(int customerId, string latteArtShape) => 0;
        public float OnOrderServed(int customerId, float qualityMultiplier, string latteArtShape, int basePrice) => 1f;
        public bool CheckStoryTrigger(int reputation, int gold, int totalOrders) => false;
    }

    public static class SocialServiceRegistry
    {
        private static ISocialService current = NullSocialService.Instance;
        public static ISocialService Current
        {
            get => current;
            set => current = value ?? NullSocialService.Instance;
        }
    }

    // ==================== NCS 叙事系统预留接口 ====================
    // 叙事架构是游戏最精华的部分，当前仅留接口，后续设计实现

    /// <summary>
    /// NCS 叙事上下文接口：叙事系统通过此接口与模拟经营系统交互。
    /// 当前为预留接口，后续叙事架构实现时填充具体逻辑。
    /// </summary>
    public interface INarrativeContext
    {
        /// <summary>当前叙事章节 ID</summary>
        string CurrentChapterId { get; }

        /// <summary>当前叙事节点 ID</summary>
        string CurrentNodeId { get; }

        /// <summary>叙事修正系数：影响订单价格/顾客行为等</summary>
        float NarrativePriceModifier { get; }

        /// <summary>检查特定叙事条件是否满足</summary>
        bool CheckCondition(string conditionId);

        /// <summary>触发叙事事件（由经营系统调用）</summary>
        void TriggerEvent(string eventId, Dictionary<string, object> parameters = null);
    }

    /// <summary>
    /// Null 叙事上下文：无叙事时的默认实现，所有操作无效果。
    /// </summary>
    public sealed class NullNarrativeContext : INarrativeContext
    {
        public static readonly NullNarrativeContext Instance = new();

        public string CurrentChapterId => string.Empty;
        public string CurrentNodeId => string.Empty;
        public float NarrativePriceModifier => 1.0f;

        public bool CheckCondition(string conditionId) => false;
        public void TriggerEvent(string eventId, Dictionary<string, object> parameters = null) { }

        private NullNarrativeContext() { }
    }

    /// <summary>
    /// 叙事服务注册表：经营系统通过此注册表访问叙事上下文。
    /// 默认使用无叙事（Null）实现，后续替换为真实叙事系统。
    /// </summary>
    public static class NarrativeServiceRegistry
    {
        private static INarrativeContext _current;

        /// <summary>当前叙事上下文，未设置时使用 Null 实现</summary>
        public static INarrativeContext Current
        {
            get => _current ?? NullNarrativeContext.Instance;
            set => _current = value;
        }

        /// <summary>重置为 Null 实现（用于测试或清除叙事状态）</summary>
        public static void Reset() => _current = null;
    }

    /// <summary>
    /// 叙事事件类型：定义经营系统可能触发的叙事事件
    /// </summary>
    public static class NarrativeEvents
    {
        public const string OrderCompleted = "order_completed";
        public const string PerfectOrder = "perfect_order";      // 满分订单
        public const string FailedOrder = "failed_order";
        public const string DayEnded = "day_ended";
        public const string CustomerServed = "customer_served";
        public const string IngredientDepleted = "ingredient_depleted";
        public const string DeviceUpgraded = "device_upgraded";
    }

    /// <summary>
    /// 叙事条件类型：定义经营系统可查询的叙事条件
    /// </summary>
    public static class NarrativeConditions
    {
        public const string IsFirstDay = "is_first_day";
        public const string HasRegularCustomer = "has_regular_customer";
        public const string ReachedReputation = "reached_reputation";
        public const string CompletedAllRecipes = "completed_all_recipes";
    }
}
