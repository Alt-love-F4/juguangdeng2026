using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        public void AddConstituent(Portion portion, string ingredientId, float grams, float ml)
        {
            if (portion == null || string.IsNullOrEmpty(ingredientId) || !Finite(grams) || !Finite(ml) || grams < 0 || ml < 0)
                throw new ArgumentException("Invalid constituent amount");
            if (grams == 0 && ml == 0) return;
            ingredientId = CanonicalIngredient(ingredientId);
            portion.composition = portion.composition ?? new List<ConstituentAmount>();
            var same = portion.composition.Find(p => p.ingredientId == ingredientId);
            if (same == null)
                portion.composition.Add(new ConstituentAmount { ingredientId = ingredientId, grams = grams, milliliters = ml });
            else
            {
                double mass = (double)same.grams + grams, volume = (double)same.milliliters + ml;
                if (mass > float.MaxValue || volume > float.MaxValue) throw new ArgumentException("Constituent amount overflow");
                same.grams = (float)mass; same.milliliters = (float)volume;
            }
        }

        public float CoffeeGroundsGrams(Portion portion) => ConstituentAmountOf(portion, "grounds", false);
        public float CoffeeWaterMl(Portion portion) => ConstituentAmountOf(portion, "water", true);

        private float ConstituentAmountOf(Portion portion, string ingredientId, bool liquid)
        {
            if (portion == null) return 0;
            double sum = 0;
            if (portion.composition != null) foreach (var constituent in portion.composition)
                if (constituent != null && CanonicalIngredient(constituent.ingredientId) == ingredientId)
                    sum += liquid ? constituent.milliliters : constituent.grams;
            // Read-only legacy fallback; explicit provenance never gains fictitious powder.
            if (sum == 0 && portion.specId == "coffee" && (portion.composition == null || portion.composition.Count == 0))
                return liquid ? portion.milliliters : portion.grams > 0 ? portion.grams
                    : portion.milliliters * (Catalog.compositionSettings?.defaultCoffeeStrength ?? .06f);
            return (float)Math.Min(float.MaxValue, sum);
        }

        public void EnsureCoffeeProvenance(Portion portion)
        {
            if (portion == null || portion.specId != "coffee" || portion.milliliters <= 0) return;
            bool legacy = portion.composition == null || portion.composition.Count == 0;
            if (legacy)
            {
                float grams = portion.grams > 0 ? portion.grams : portion.milliliters * (Catalog.compositionSettings?.defaultCoffeeStrength ?? .06f);
                AddConstituent(portion, "grounds", grams, 0);
                AddConstituent(portion, "water", 0, portion.milliliters);
                portion.grams = 0;
            }
            else if (CoffeeWaterMl(portion) <= 0)
                AddConstituent(portion, "water", 0, portion.milliliters);
        }

        public void MergeComposition(Portion target, Portion incoming, float fraction = 1)
        {
            if (!Finite(fraction) || fraction < 0 || fraction > 1) throw new ArgumentException("Invalid provenance fraction");
            if (target == null || incoming == null || incoming.composition == null || fraction == 0) return;
            var source = target == incoming ? new List<ConstituentAmount>(incoming.composition) : incoming.composition;
            foreach (var constituent in source)
                if (constituent != null) AddConstituent(target, constituent.ingredientId, constituent.grams * fraction, constituent.milliliters * fraction);
        }

        public void CopyComposition(Portion source, Portion target, float fraction = 1)
        {
            if (target == null || source == target) return;
            target.composition = new List<ConstituentAmount>();
            MergeComposition(target, source, fraction);
        }

        public void ScaleComposition(Portion portion, float fraction)
        {
            if (!Finite(fraction) || fraction < 0 || fraction > 1) throw new ArgumentException("Invalid provenance fraction");
            if (portion?.composition == null) return;
            foreach (var constituent in portion.composition)
            { constituent.grams *= fraction; constituent.milliliters *= fraction; }
            portion.composition.RemoveAll(p => p.grams == 0 && p.milliliters == 0);
        }

        private Portion CopyPortion(Portion source)
        {
            if (source == null) return null;
            var copy = new Portion { specId = source.specId, units = source.units, quality = source.quality,
                milliliters = source.milliliters, grams = source.grams, extracted = source.extracted,
                temperature = source.temperature, productionScore = source.productionScore,
                coffeeKind = source.coffeeKind, addedSequence = source.addedSequence, latteArtSessionToken = source.latteArtSessionToken };
            SetTemperature(copy, TemperatureCelsius(source));
            CopyComposition(source, copy);
            return copy;
        }

        private Portion IngredientPortion(WorldEntity item)
        {
            if (item.portionPayload != null && item.portionPayload.specId == item.specId && item.portionPayload.units > 0)
                return CopyPortion(item.portionPayload);
            var portion = new Portion { specId = item.specId, units = item.units, quality = item.quality,
                milliliters = IsLiquid(item.specId) ? DoseMl(item.specId) * item.units : 0,
                grams = IsLiquid(item.specId) ? 0 : item.units * DoseGrams(item.specId) };
            SetTemperature(portion, item.specId == "hotwater" || item.specId == "hotmilk"
                ? Prep.boilingTemperatureCelsius : Prep.ambientTemperatureCelsius);
            return portion;
        }

        private void ScalePortionAmounts(Portion portion, float fraction)
        {
            portion.milliliters *= fraction;
            portion.grams *= fraction;
            ScaleComposition(portion, fraction);
        }

        /// <summary>Legacy ordinary mixing: free grounds become internal sources, not a second inventory.</summary>
        public void AbsorbGroundsIntoCoffee(WorldEntity entity)
        {
            if (entity == null || IsExtractionFilter(entity) || ExtractionFilter(entity) != null) return;
            float volume = 0, mass = 0;
            foreach (var portion in entity.contents)
            {
                if (portion.specId == "coffee" && CoffeeKind(portion) != "essence") volume += portion.milliliters;
                if (portion.specId == "grounds") mass += portion.grams > 0 ? portion.grams : portion.units * DoseGrams("grounds");
            }
            if (volume <= 0 || mass <= 0) return;
            foreach (var portion in entity.contents)
                if (portion.specId == "coffee" && CoffeeKind(portion) != "essence" && portion.milliliters > 0)
                {
                    if (portion.composition == null || portion.composition.Count == 0)
                        AddConstituent(portion, "water", 0, portion.milliliters);
                    AddConstituent(portion, "grounds", mass * portion.milliliters / volume, 0);
                    portion.grams = 0;
                }
            entity.contents.RemoveAll(p => p.specId == "grounds");
        }
    }
}
