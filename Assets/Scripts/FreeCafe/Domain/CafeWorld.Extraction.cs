using System;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        public bool IsPrecisionExtractor(WorldEntity entity) => ProductionEnabled && entity != null
            && Spec(entity.specId).Has(Capability.Container) && Spec(entity.specId).Has(Capability.HotExtraction);

        private float ExtractionGrounds(WorldEntity entity)
        {
            float grams = 0;
            var filter = ExtractionFilter(entity);
            foreach (var p in filter != null ? filter.contents : entity.contents)
                if (p.specId == "grounds") grams += p.grams > 0 ? p.grams : p.units * 12;
            return grams;
        }

        public CafeExtractionSnapshot ExtractionSnapshot(string entityId) => ExtractionSnapshot(Find(entityId));
        public CafeExtractionSnapshot ExtractionSnapshot(WorldEntity entity)
        {
            entity = ExtractionJug(entity);
            if (entity == null) return default;
            var d = entity.production;
            var filter = ExtractionFilter(entity);
            float grounds = d.brewStarted ? d.brewGroundsGrams : filter != null && filter.production.filterReferenceGroundsGrams > 0
                ? filter.production.filterReferenceGroundsGrams : ExtractionGrounds(entity);
            float scale = grounds / Prep.brewReferenceGroundsGrams;
            float uniformity = ExtractionUniformity(d);
            return new CafeExtractionSnapshot(true, d.brewStarted && !d.brewCompleted, d.brewCompleted, grounds,
                Prep.brewIdealSeconds * scale, Prep.brewIdealMl * scale,
                Prep.brewGoldSeconds * scale, Prep.brewGreenLeftSeconds * scale, Prep.brewGreenRightSeconds * scale,
                Prep.brewGoldMl * scale, Prep.brewGreenLeftMl * scale, Prep.brewGreenRightMl * scale,
                d.brewElapsed, d.brewInjectedHotWaterMl, uniformity, d.brewCompleted ? d.extractionScore : 0,
                !d.brewCompleted ? "未完成精粹咖啡" : d.extractionScore >= 90 ? "优秀精粹咖啡液" : d.extractionScore >= 60 ? "良好精粹咖啡液" : "糟糕精粹咖啡液");
        }

        // Integrating rate squared over accepted pour duration avoids sampling/frame-count bias.
        private float ExtractionUniformity(ProductionEntityState d)
        {
            if (d.brewPourSeconds <= 0 || d.brewRateSquareIntegral <= 0) return 0;
            double mean = d.brewInjectedHotWaterMl / (double)d.brewPourSeconds;
            double variance = Math.Max(0, d.brewRateSquareIntegral / (double)d.brewPourSeconds - mean * mean);
            return Clamp((float)(100 * (1 - Math.Sqrt(variance) / Math.Max(.000001, mean))));
        }

        private void ResetExtraction(WorldEntity entity)
        {
            var d = entity.production;
            d.brewGroundsGrams = d.brewInjectedHotWaterMl = d.brewElapsed = 0;
            d.brewStarted = d.brewCompleted = d.brewPourPaused = false;
            d.brewPourSeconds = d.brewRateSquareIntegral = 0;
            d.flowSum = d.flowSquareSum = 0; d.flowSamples = 0;
            d.extractionScore = 0;
        }

        private void AcceptExtractionWater(WorldEntity entity, float ml, float seconds)
        {
            entity = ExtractionJug(entity);
            if (!IsPrecisionExtractor(entity) || ExtractionFilter(entity) == null || ml <= 0 || !Finite(ml) || !Finite(seconds) || seconds <= 0) return;
            var d = entity.production;
            if (d.brewCompleted)
            {
                // A qualified partial stop is not a one-shot lock. Continue the same batch
                // while its original filter still has powder; never reset dose or flow history.
                var filter=ExtractionFilter(entity);
                if(filter.production.filterSpent || ExtractionGrounds(entity)<=.000001f)return;
                d.brewCompleted=false;
            }
            if (!d.brewStarted)
            {
                float grounds = ExtractionGrounds(entity);
                if (grounds <= 0 || LiquidVolume(entity) > 0 || LiquidVolume(ExtractionFilter(entity)) > 0) return; // Prefilled mixtures stay ordinary.
                ResetExtraction(entity);
                d.brewGroundsGrams = grounds; d.brewStarted = true;
                var filter = ExtractionFilter(entity);
                if (filter.production.filterReferenceGroundsGrams <= 0)
                    filter.production.filterReferenceGroundsGrams = grounds;
            }
            d.brewPourPaused=false;
            double integral = d.brewRateSquareIntegral + (double)ml * ml / seconds;
            d.brewInjectedHotWaterMl = (float)Math.Min(float.MaxValue, (double)d.brewInjectedHotWaterMl + ml);
            d.brewPourSeconds = (float)Math.Min(float.MaxValue, (double)d.brewPourSeconds + seconds);
            d.brewRateSquareIntegral = (float)Math.Min(float.MaxValue, integral);
        }

        private void CompleteExtraction(WorldEntity entity)
        {
            var d = entity.production;
            if (!d.brewStarted || d.brewCompleted) return;
            var s = ExtractionSnapshot(entity);
            bool gold = InBand(s.elapsedSeconds, s.goldTimeMin, s.goldTimeMax)
                && InBand(s.injectedHotWaterMl, s.goldMlMin, s.goldMlMax);
            bool green = InBand(s.elapsedSeconds, s.greenTimeMin, s.greenTimeMax)
                && InBand(s.injectedHotWaterMl, s.greenMlMin, s.greenMlMax);
            d.extractionScore = gold && s.uniformityScore >= 90 ? 100 : green && s.uniformityScore >= 60 ? 70 : 30;
            d.brewCompleted = true;
            d.brewPourPaused = true;
            foreach (var p in entity.contents)
                if (p.specId == "coffee") { EnsureCoffeeProvenance(p); p.extracted = true; p.productionScore = d.extractionScore; p.coffeeKind = "essence"; }
        }

        private static bool InBand(float value, float min, float max) => value >= min - .0001f && value <= max + .0001f;
    }
}
