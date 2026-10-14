using System;
using System.Collections.Generic;
using System.Globalization;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        /// <summary>Evaluate configured ingredient amounts and standards without changing the cup or world.</summary>
        public CafeProductEvaluation EvaluateProductComposition(WorldEntity cup, ProductSpec product)
        {
            var evaluation = product?.evaluation;
            if (cup?.contents == null || cup.contents.Count == 0 || !ValidProductEvaluation(evaluation))
                return ProductCompositionResult(0, 0, 0);

            double amountSum = 0, standardSum = 0, concentrationSum = 0, concentrationVolume = 0;
            bool needsCoffee = false;
            foreach (var need in evaluation.amounts)
            {
                double actual = 0, coffeeVolume = 0, coffeeQuality = 0, groundsGrams = 0, waterMl = 0;
                bool coffee = need.ingredientId == "coffee" || !string.IsNullOrEmpty(need.coffeeKind);
                needsCoffee |= coffee;
                foreach (var portion in cup.contents)
                {
                    if (!ProductPortionMatches(portion, need)) continue;
                    string kind = CoffeeKind(portion);
                    actual += ProductPortionAmount(portion, need.idealMl > 0);
                    if (coffee && kind != null)
                    {
                        double volume = ProductPortionAmount(portion, true);
                        coffeeVolume += volume;
                        coffeeQuality += volume * (Finite(portion.productionScore) ? Clamp(portion.productionScore) : 0);
                        if (volume > 0)
                        {
                            float grams = CoffeeGroundsGrams(portion), water = CoffeeWaterMl(portion);
                            if (Finite(grams) && grams > 0) groundsGrams += grams;
                            if (Finite(water) && water > 0) waterMl += water;
                        }
                    }
                }

                double ideal = need.idealMl > 0 ? need.idealMl : need.idealGrams;
                // The tolerance band is free; outside it, equal excess/shortage has equal cost.
                // Normalize by the remaining distance to an empty serving, so missing is always zero.
                float amount = ProductAmountScore(actual, ideal, need.tolerance);
                float quality = coffee ? coffeeVolume > 0 ? (float)(coffeeQuality / coffeeVolume) : 0 : 100;
                if (coffee)
                {
                    float concentration = ProductAmountScore(waterMl > 0 ? groundsGrams / waterMl : 0,
                        evaluation.idealCoffeeStrength, evaluation.coffeeStrengthTolerance);
                    concentrationSum += concentration * coffeeVolume;
                    concentrationVolume += coffeeVolume;
                    quality = quality * (1 - evaluation.concentrationWeight) + concentration * evaluation.concentrationWeight;
                }
                float standard = actual <= 0 ? 0 : quality;
                if (need.minimumStandard > 0 && standard < need.minimumStandard)
                    standard *= standard / need.minimumStandard;
                amountSum += amount;
                standardSum += standard;
            }

            float amountScore = (float)(amountSum / evaluation.amounts.Length);
            float standardScore = (float)(standardSum / evaluation.amounts.Length);
            double weight = (double)evaluation.amountWeight + evaluation.standardWeight;
            float total = (float)((amountScore * (double)evaluation.amountWeight + standardScore * (double)evaluation.standardWeight) / weight);

            var extras = new HashSet<string>(StringComparer.Ordinal);
            foreach (var portion in cup.contents)
            {
                if (portion == null || string.IsNullOrEmpty(portion.specId)
                    || !(portion.milliliters > 0 || portion.grams > 0 || portion.units > 0)) continue;
                bool expected = portion.specId == "grounds" && needsCoffee;
                foreach (var need in evaluation.amounts)
                    if (SameIngredient(portion.specId, need.ingredientId)
                        || portion.specId == "cocoa.powder" && need.ingredientId == "cocoa.drink"
                        || portion.specId == "matcha.powder" && need.ingredientId == "matcha.drink")
                    { expected = true; break; }
                // A wrong coffee kind already scores as missing; do not penalize it a second time.
                if (!expected) extras.Add(ProductIngredientKey(portion.specId));
            }
            total *= Clamp(1 - extras.Count * evaluation.extraIngredientPenalty, 0, 1);
            if (evaluation.enforceCup && cup.specId != product.cupSpecId) total = 0;
            float concentrationScore = concentrationVolume > 0 ? (float)(concentrationSum / concentrationVolume) : 0;
            return ProductCompositionResult(amountScore, standardScore, total, concentrationScore,
                needsCoffee && evaluation.concentrationWeight > 0);
        }

        private double ProductPortionAmount(Portion portion, bool milliliters)
        {
            float recorded = milliliters ? portion.milliliters : portion.grams;
            if (!Finite(recorded) || recorded < 0) return 0;
            if (recorded > 0) return recorded;
            if (portion.units <= 0) return 0;
            var definition = IngredientDefinition(portion.specId) ?? IngredientDefinition(ProductIngredientKey(portion.specId));
            float perUnit = milliliters ? definition?.millilitersPerUnit ?? Prep.portionMl
                : definition?.gramsPerUnit ?? (portion.specId == "sugar" ? 5 : 12);
            return Finite(perUnit) && perUnit > 0 ? portion.units * (double)perUnit : 0;
        }

        private static float ProductAmountScore(double actual, double ideal, float tolerance)
        {
            double deviation = Math.Max(0, Math.Abs(actual - ideal) - ideal * tolerance);
            return actual > 0 ? Clamp((float)(100 * (1 - deviation / (ideal * (1 - tolerance))))) : 0;
        }

        private static string ProductIngredientKey(string id) => id == "hotwater" ? "water" : id == "hotmilk" ? "milk" : id;

        private static bool ValidProductEvaluation(ProductEvaluationSpec evaluation)
        {
            if (evaluation?.amounts == null || evaluation.amounts.Length == 0
                || !Finite(evaluation.amountWeight) || evaluation.amountWeight < 0
                || !Finite(evaluation.standardWeight) || evaluation.standardWeight < 0
                || (double)evaluation.amountWeight + evaluation.standardWeight <= 0
                || !Finite(evaluation.extraIngredientPenalty) || evaluation.extraIngredientPenalty < 0 || evaluation.extraIngredientPenalty > 1
                || !Finite(evaluation.concentrationWeight) || evaluation.concentrationWeight < 0 || evaluation.concentrationWeight > 1
                || !Finite(evaluation.idealCoffeeStrength) || evaluation.idealCoffeeStrength <= 0
                || !Finite(evaluation.coffeeStrengthTolerance) || evaluation.coffeeStrengthTolerance < 0 || evaluation.coffeeStrengthTolerance >= 1)
                return false;
            for (int i = 0; i < evaluation.amounts.Length; i++)
            {
                var need = evaluation.amounts[i];
                if (need == null || string.IsNullOrEmpty(need.ingredientId)
                    || !Finite(need.idealMl) || need.idealMl < 0 || !Finite(need.idealGrams) || need.idealGrams < 0
                    || (need.idealMl > 0) == (need.idealGrams > 0)
                    || !Finite(need.tolerance) || need.tolerance < 0 || need.tolerance >= 1
                    || !Finite(need.minimumStandard) || need.minimumStandard < 0 || need.minimumStandard > 100
                    || need.minimumTemperature < -1 || need.minimumTemperature > 2
                    || !Finite(need.classificationMinimumRatio) || need.classificationMinimumRatio < 0 || need.classificationMinimumRatio > 1
                    || !Finite(need.classificationMaximumRatio) || need.classificationMaximumRatio < need.classificationMinimumRatio || need.classificationMaximumRatio > 1
                    || !Finite(need.classificationMinimumGrams) || need.classificationMinimumGrams < 0
                    || !Finite(need.classificationMaximumGrams) || need.classificationMaximumGrams < 0
                    || need.classificationMaximumGrams > 0 && need.classificationMaximumGrams < need.classificationMinimumGrams
                    || !string.IsNullOrEmpty(need.coffeeKind) && (need.ingredientId != "coffee"
                        || need.coffeeKind != "cold_brew" && need.coffeeKind != "black" && need.coffeeKind != "essence"))
                    return false;
                for (int j = 0; j < i; j++)
                    if (ProductIngredientKey(need.ingredientId) == ProductIngredientKey(evaluation.amounts[j].ingredientId)
                        && (need.coffeeKind ?? "") == (evaluation.amounts[j].coffeeKind ?? ""))
                        return false;
            }
            return true;
        }

        private static CafeProductEvaluation ProductCompositionResult(float amount, float standard, float total,
            float concentration = 0, bool showConcentration = false)
        {
            total = Clamp(total);
            string grade = total >= 90 ? "精品" : total >= 60 ? "一般" : "糟糕";
            string description = grade + " " + total.ToString("0.0", CultureInfo.InvariantCulture) + "分 · 含量 "
                + amount.ToString("0", CultureInfo.InvariantCulture) + " / 水准 " + standard.ToString("0", CultureInfo.InvariantCulture);
            if (showConcentration) description += " / 浓度 " + concentration.ToString("0", CultureInfo.InvariantCulture);
            return new CafeProductEvaluation(amount, standard, total, description, concentration);
        }
    }
}
