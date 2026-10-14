using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>Scene tuning for the reference 12 g recipe. Targets scale with the loaded grounds.</summary>
    [DisallowMultipleComponent]
    public sealed class FreeCafeExtractionConfig : MonoBehaviour
    {
        [Min(.1f)] public float referenceGroundsGrams = 12f;
        [Header("Time — seconds at reference dose")]
        [Min(.1f)] public float perfectTimeSeconds = 7.5f;
        [Min(.01f)] public float goldTimeRadiusSeconds = 1f;
        [Min(0f)] public float greenTimeLeftSeconds = 3f;
        [Min(0f)] public float greenTimeRightSeconds = 3f;
        [Header("Water — ml at reference dose")]
        [Min(.1f)] public float perfectWaterMl = 200f;
        [Min(.01f)] public float goldWaterRadiusMl = 15f;
        [Min(0f)] public float greenWaterLeftMl = 35f;
        [Min(0f)] public float greenWaterRightMl = 35f;
        [Header("Gravity filter")]
        [Min(1f)] public float drainageMlPerSecond = 220f;
        [Min(.01f)] public float waterResidenceSeconds = .12f;
        public void Apply(ProductionSettings settings)
        {
            settings.filterDrainMlPerSecond = drainageMlPerSecond;
            settings.filterResidenceSeconds = waterResidenceSeconds;
            settings.brewReferenceGroundsGrams=referenceGroundsGrams;
            settings.brewIdealSeconds=perfectTimeSeconds;settings.brewGoldSeconds=goldTimeRadiusSeconds;
            settings.brewGreenLeftSeconds=greenTimeLeftSeconds;settings.brewGreenRightSeconds=greenTimeRightSeconds;
            settings.brewIdealMl=perfectWaterMl;settings.brewGoldMl=goldWaterRadiusMl;
            settings.brewGreenLeftMl=greenWaterLeftMl;settings.brewGreenRightMl=greenWaterRightMl;
        }
    }
}
