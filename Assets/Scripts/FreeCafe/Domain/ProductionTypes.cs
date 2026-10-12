using System;

namespace Spotlight.FreeCafe
{
    public enum LiquidTemperature { Room, Warm, Boiling }
    public enum ProductionPhase { Empty, Loaded, Processing, Ready, Boiling, Warm, Room }

    [Serializable]
    public sealed class ProductionSettings
    {
        public bool requireLatteArt; // 真实小游戏接入后可启用，不让占位阻断基础出品
        public float grindSeconds = 12;
        public float grindClickAdvance = 1;
        public float grindClickCooldown = .12f;
        public float heatSeconds = 4;
        public float boilingSeconds = 5;
        public float warmSeconds = 10;
        public float portionMl = 200;
        public float pourRateMl = 28;
        public float brewIdealSeconds = 7.5f;
        public float brewGoldSeconds = 1;
        public float brewGreenSeconds = 3;
        public float brewIdealMl = 200;
        public float brewGoldMl = 15;
        public float brewGreenMl = 35;
        public float kettleCapacityMl = 400;
    }

    /// <summary>道具实例状态。所有液体仍在 WorldEntity.contents，不建立第二份液体库存。</summary>
    [Serializable]
    public sealed class ProductionEntityState
    {
        public ProductionPhase phase;
        public float remaining;
        public float clickCooldown;
        public bool brewStarted;
        public float brewElapsed;
        public float brewPourSeconds;
        public float flowSum;
        public float flowSquareSum;
        public int flowSamples;
        public float extractionScore = 100;
        public int additionSequence;
        public float spilledMl;
        public string heatingVesselId;
    }

    [Serializable]
    public sealed class ProductionSession
    {
        public bool enabled;
        public string activeCupId;
        public AreaId returnArea;
        public string miniGameToken;
        public string miniGameCupId;
        public string miniGameKind;
        public string lastDelivery;
    }

    /// <summary>展示层只读快照；真实 2D 流体可使用此数据驱动，而不能反写库存。</summary>
    public readonly struct CafeLiquidComposition
    {
        public readonly float coffeeMl, milkMl, waterMl, matchaMl, cocoaMl, otherMl;
        public float TotalMl => coffeeMl + milkMl + waterMl + matchaMl + cocoaMl + otherMl;

        public CafeLiquidComposition(float coffee, float milk, float water, float matcha, float cocoa, float other)
        {
            coffeeMl = coffee; milkMl = milk; waterMl = water;
            matchaMl = matcha; cocoaMl = cocoa; otherMl = other;
        }
    }

    public readonly struct CafeLiquidSnapshot
    {
        public readonly float volumeMl, capacityMl, extractionScore;
        public readonly LiquidTemperature temperature;
        public readonly ProductionPhase phase;
        public readonly string primaryIngredient;
        public readonly CafeLiquidComposition composition;
        public CafeLiquidSnapshot(float volume, float capacity, LiquidTemperature temperature,
            ProductionPhase phase, string ingredient, float score, CafeLiquidComposition composition = default)
        {
            volumeMl = volume; capacityMl = capacity; this.temperature = temperature;
            this.phase = phase; primaryIngredient = ingredient; extractionScore = score;
            this.composition = composition;
        }
    }

    public readonly struct ProductionQuality
    {
        public readonly float extraction, liquid, order, sweetness, concentration, total;
        public readonly string grade;
        public ProductionQuality(float extraction, float liquid, float order, float sweetness, float concentration)
        {
            this.extraction = extraction; this.liquid = liquid; this.order = order;
            this.sweetness = sweetness; this.concentration = concentration;
            total = (extraction + liquid + order + sweetness + concentration) / 5;
            grade = total < 20 ? "难以下咽" : total < 40 ? "差强人意" : total < 60 ? "平淡普通" : total < 80 ? "醇香可口" : "匠心佳作";
        }
    }
}
