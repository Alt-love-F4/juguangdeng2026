using System;

namespace Spotlight.FreeCafe
{
    [Serializable] public sealed class IngredientRuleSpec
    {
        public string id;
        public string label;
        public bool liquid;
        public bool soluble;
        public float gramsPerUnit = 12;
        public float millilitersPerUnit = 200;
        public string coffeeKind;
    }
    [Serializable] public sealed class CompositionRuleSpec
    {
        public string id;
        public string label;
        public string catalystId;
        public string inputId;
        public string inputCoffeeKind;
        public string outputId;
        public string outputCoffeeKind;
        public int minimumTemperature;
        public int maximumTemperature = 2;
        public bool precisionOnly;
        public bool consumeCatalyst;
    }
    [Serializable] public sealed class DrinkNameRuleSpec
    {
        public string id;
        public string label;
        public string coffeeKind;
        public string[] requiredIngredients;
        public string[] allowedIngredients;
        public int priority;
    }
    /// <summary>Concentration display and legacy provenance defaults; values are grams of grounds per ml of brew water.</summary>
    [Serializable] public sealed class CoffeeCompositionSettings
    {
        public float defaultCoffeeStrength = .06f;
        public float strongCoffeeStrength = .075f;
        public float mediumCoffeeStrength = .045f;
    }
    [Serializable] public sealed class ProductAmountSpec
    {
        public string ingredientId;
        public string coffeeKind;
        public float idealMl;
        public float idealGrams;
        public float tolerance = .2f;
        public float minimumStandard;
        // Classification is intentionally wider than the recommended serving tolerance.
        // A zero lower bound still requires a positive amount; ratios use total liquid ml.
        public float classificationMinimumRatio;
        public float classificationMaximumRatio = 1;
        public float classificationMinimumGrams;
        public float classificationMaximumGrams; // 0 means no upper limit.
        public int minimumTemperature = -1; // -1 unrestricted; otherwise Room/Warm/Boiling = 0/1/2.
    }
    [Serializable] public sealed class ProductEvaluationSpec
    {
        public ProductAmountSpec[] amounts;
        public float amountWeight = .65f;
        public float standardWeight = .35f;
        public float extraIngredientPenalty = .15f;
        public bool enforceCup;
        public bool classify = true;
        public int classificationPriority;
        public float concentrationWeight; // 0 keeps the original production-quality standard.
        public float idealCoffeeStrength = .06f;
        public float coffeeStrengthTolerance = .2f;
    }
    public readonly struct CafeProductEvaluation
    {
        public readonly float amountScore, standardScore, total, concentrationScore;
        public readonly string description;
        public CafeProductEvaluation(float amount,float standard,float total,string description)
            : this(amount, standard, total, description, 0) { }
        public CafeProductEvaluation(float amount,float standard,float total,string description,float concentration)
        { amountScore=amount;standardScore=standard;this.total=total;this.description=description;concentrationScore=concentration; }
    }
    /// <summary>Read-only classification snapshot. Unknown/empty containers have no productId and zero scores.</summary>
    public readonly struct CafeCoffeeClassification
    {
        public readonly string productId, label, description;
        public readonly bool known;
        public readonly float amountScore, standardScore, total, concentrationScore;
        public CafeCoffeeClassification(string productId, string label, bool known, CafeProductEvaluation evaluation)
        {
            this.productId=productId;this.label=label;this.known=known;
            amountScore=evaluation.amountScore;standardScore=evaluation.standardScore;total=evaluation.total;
            concentrationScore=evaluation.concentrationScore;description=evaluation.description;
        }
    }
}
