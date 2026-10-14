using System;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        /// <summary>
        /// Classifies the actual contents using the same product evaluation rows used by orders.
        /// Serving ideals and quality scores do not decide the kind. This method does not mutate the world.
        /// </summary>
        public CafeCoffeeClassification ClassifyCoffee(WorldEntity entity)
        {
            bool hasContents = false;
            if (entity?.contents != null)
                foreach (var portion in entity.contents)
                    if (ProductPortionPresent(portion)) { hasContents = true; break; }
            if (!hasContents)
                return new CafeCoffeeClassification(null, "空容器", false, default);

            ProductSpec best = null;
            if (Catalog.products != null)
                foreach (var product in Catalog.products)
                {
                    if (product == null || string.IsNullOrEmpty(product.id) || string.IsNullOrEmpty(product.label)
                        || product.evaluation == null || !product.evaluation.classify
                        || !ClassificationIngredientsMatch(entity, product.evaluation)) continue;
                    // Equal priority retains the first declared row, never the highest serving score.
                    if (best == null || product.evaluation.classificationPriority > best.evaluation.classificationPriority)
                        best = product;
                }
            return best == null
                ? new CafeCoffeeClassification(null, "迷之饮品", false, default)
                : new CafeCoffeeClassification(best.id, best.label, true, EvaluateProductComposition(entity, best));
        }

        private bool ClassificationIngredientsMatch(WorldEntity entity, ProductEvaluationSpec evaluation)
        {
            if (!ValidProductEvaluation(evaluation)) return false;
            foreach (var portion in entity.contents)
            {
                if (!ProductPortionPresent(portion)) continue;
                bool matched = false;
                foreach (var need in evaluation.amounts)
                    if (ProductPortionMatches(portion, need)
                        && ClassificationPortionAmount(portion, need.idealMl > 0) > 0)
                    { matched = true; break; }
                // Only actual listed components are allowed; raw powder/beans/sugar are not hidden.
                // Reacted powder is already represented inside the coffee/liquid provenance ledger.
                if (!matched) return false;
            }
            foreach (var need in evaluation.amounts)
            {
                double amount = 0;
                foreach (var portion in entity.contents)
                    if (ProductPortionMatches(portion, need))
                        amount += ClassificationPortionAmount(portion, need.idealMl > 0);
                // Category depends only on ingredient presence, never ml/g targets or ratios.
                if (amount <= 0) return false;
            }
            return true;
        }

        private bool ProductPortionPresent(Portion portion)
        {
            if (portion == null || string.IsNullOrEmpty(portion.specId)) return false;
            return ClassificationPortionAmount(portion, true) > 0 || ClassificationPortionAmount(portion, false) > 0;
        }

        private static double ClassificationPortionAmount(Portion portion, bool milliliters)
        {
            float amount = milliliters ? portion.milliliters : portion.grams;
            return Finite(amount) && amount > 0 ? amount : 0;
        }

        private bool ProductPortionMatches(Portion portion, ProductAmountSpec need)
        {
            return portion != null && need != null && SameIngredient(portion.specId, need.ingredientId)
                && (string.IsNullOrEmpty(need.coffeeKind) || CoffeeKind(portion) == need.coffeeKind)
                && (need.minimumTemperature < 0 || (int)TemperatureBand(TemperatureCelsius(portion)) >= need.minimumTemperature);
        }
    }
}
