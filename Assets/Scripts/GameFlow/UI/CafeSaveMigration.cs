using System;
using Spotlight.FreeCafe;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Repairs observed legacy float rounding only; callers must still run CafeWorld.Restore validation.</summary>
    public static class CafeSaveMigration
    {
        public static CafeSnapshot Prepare(CafeSnapshot source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var candidate = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(source));
            if (candidate.entities == null) return candidate;
            foreach (var entity in candidate.entities)
            {
                if (entity?.contents == null) continue;
                foreach (var portion in entity.contents)
                {
                    if (portion == null) continue;
                    // Old float weighted averages produced 100.000022888... from valid 100-point inputs.
                    // Larger errors, negative scores and non-finite values remain invalid.
                    if (portion.productionScore > 100f && portion.productionScore <= 100.0001f)
                        portion.productionScore = 100f;
                }
            }
            return candidate;
        }
    }
}
