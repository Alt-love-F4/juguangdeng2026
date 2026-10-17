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
        public float ambientTemperatureCelsius = 20;
        public float warmTemperatureCelsius = 60;
        public float boilingTemperatureCelsius = 100;
        public float hotThresholdCelsius = 50;
        public float boilingThresholdCelsius = 95;
        public float heatingTargetCelsius = 100;
        // Obsolete cooling durations remain serialized for legacy catalog compatibility; no natural cooling.
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
        public float filterDrainMlPerSecond = 220;
        public float filterResidenceSeconds = .12f;
        public float brewReferenceGroundsGrams = 12;
        // Independent extensions outside the gold band; legacy symmetric fields stay serialized.
        public float brewGreenLeftSeconds = 3;
        public float brewGreenRightSeconds = 3;
        public float brewGreenLeftMl = 35;
        public float brewGreenRightMl = 35;
        public float kettleCapacityMl = 400;
        public float extractionFilterOffsetX = -.22f;
        public float extractionFilterOffsetY = .91f;
        // Position tolerance used when repairing an unlinked filter near its extractor.
        public float extractionAssemblySnapDistance = .35f;
    }

    /// <summary>道具实例状态。所有液体仍在 WorldEntity.contents，不建立第二份液体库存。</summary>
    [Serializable]
    public sealed class ProductionEntityState
    {
        public ProductionPhase phase;
        public float remaining;
        public float clickCooldown;
        public bool selfHeating; // Only the silver kettle can heat independently of a stove.
        public bool brewStarted;
        public bool brewPourPaused; // 松手/取消后冻结本批计时，下一滴有效热水恢复。
        public bool brewCompleted;
        public float brewGroundsGrams;
        public float brewInjectedHotWaterMl;
        public float brewRateSquareIntegral;
        public float brewElapsed;
        public float brewPourSeconds;
        public float flowSum;
        public float flowSquareSum;
        public int flowSamples;
        public float extractionScore = 100;
        public int additionSequence;
        public float spilledMl;
        public string heatingVesselId;
        public string extractionFilterId;
        public string extractionJugId;
        public bool extractionAssemblyInitialized;
        public bool filterSpent;
        public float filterDrainHold;
        public float filterDrainedMl;
        public float filterReferenceGroundsGrams;
        public float filterWaterPassedMl;
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
        public string miniGamePitcherId;
        public float miniGameFoamUsedMl;
        public string lastDelivery;
    }

    /// <summary>展示层只读快照；真实 2D 流体可使用此数据驱动，而不能反写库存。</summary>
    public readonly struct CafeLiquidComposition
    {
        public readonly float coffeeMl, milkMl, waterMl, matchaMl, cocoaMl, otherMl;
        // Subset of milkMl, not additional volume. Keep the existing milk total API.
        public readonly float milkFoamMl;
        public float TotalMl => coffeeMl + milkMl + waterMl + matchaMl + cocoaMl + otherMl;

        public CafeLiquidComposition(float coffee, float milk, float water, float matcha, float cocoa, float other, float milkFoam = 0)
        {
            coffeeMl = coffee; milkMl = milk; waterMl = water;
            matchaMl = matcha; cocoaMl = cocoa; otherMl = other;
            milkFoamMl = Math.Max(0, Math.Min(milk, milkFoam));
        }
    }

    public readonly struct CafeLiquidSnapshot
    {
        public readonly float volumeMl, capacityMl, extractionScore, temperatureCelsius;
        public readonly LiquidTemperature temperature;
        public readonly ProductionPhase phase;
        public readonly string primaryIngredient;
        public readonly CafeLiquidComposition composition;
        public CafeLiquidSnapshot(float volume, float capacity, LiquidTemperature temperature,
            ProductionPhase phase, string ingredient, float score, CafeLiquidComposition composition = default,
            float temperatureCelsius = -1)
        {
            volumeMl = volume; capacityMl = capacity; this.temperature = temperature;
            this.phase = phase; primaryIngredient = ingredient; extractionScore = score;
            this.composition = composition;
            this.temperatureCelsius = temperatureCelsius == -1
                ? temperature == LiquidTemperature.Boiling ? 100 : temperature == LiquidTemperature.Warm ? 60 : 20
                : temperatureCelsius;
        }
    }

    /// <summary>Authoritative, grounds-scaled precision extraction ranges for UI (seconds and ml).</summary>
    public readonly struct CafeExtractionSnapshot
    {
        public readonly bool available, inProgress, completed;
        public readonly float groundsGrams, idealSeconds, idealMl;
        public readonly float goldTimeMin, goldTimeMax, greenTimeMin, greenTimeMax;
        public readonly float goldMlMin, goldMlMax, greenMlMin, greenMlMax;
        public readonly float elapsedSeconds, injectedHotWaterMl, uniformityScore, qualityScore;
        public readonly string qualityLabel;

        public CafeExtractionSnapshot(bool available, bool inProgress, bool completed, float grounds,
            float idealTime, float idealVolume, float goldTimeRadius, float greenTimeLeft, float greenTimeRight,
            float goldVolumeRadius, float greenVolumeLeft, float greenVolumeRight,
            float elapsed, float injected, float uniformity, float score, string label)
        {
            this.available = available; this.inProgress = inProgress; this.completed = completed;
            groundsGrams = grounds; idealSeconds = idealTime; idealMl = idealVolume;
            goldTimeMin = idealTime - goldTimeRadius; goldTimeMax = idealTime + goldTimeRadius;
            greenTimeMin = goldTimeMin - greenTimeLeft; greenTimeMax = goldTimeMax + greenTimeRight;
            goldMlMin = idealVolume - goldVolumeRadius; goldMlMax = idealVolume + goldVolumeRadius;
            greenMlMin = goldMlMin - greenVolumeLeft; greenMlMax = goldMlMax + greenVolumeRight;
            elapsedSeconds = elapsed; injectedHotWaterMl = injected; uniformityScore = uniformity;
            qualityScore = score; qualityLabel = label;
        }
    }

    public readonly struct CafeMachineProgress
    {
        public readonly bool active;
        public readonly float remaining, duration;
        public float Fraction => duration > 0 ? Math.Max(0, Math.Min(1, 1 - remaining / duration)) : 0;
        public CafeMachineProgress(bool active, float remaining, float duration)
        { this.active=active; this.remaining=remaining; this.duration=duration; }
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
