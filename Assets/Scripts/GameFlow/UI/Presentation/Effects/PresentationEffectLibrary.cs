using System;
using UnityEngine;

namespace Spotlight.GameFlow
{
    [Serializable]
    public sealed class PresentationEffectDefinition
    {
        public string fx;
        [Min(.01f)] public float duration;
        [Min(0f)] public float strength;
        public int priority;
        [Min(1f)] public float zoom = 1f;
        public PresentationEffectDefinition(string fx, float duration, float strength = 0f, int priority = 0, float zoom = 1f)
        { this.fx = fx; this.duration = duration; this.strength = strength; this.priority = priority; this.zoom = zoom; }
    }

    /// <summary>Artist/programmer-owned presets. Scripts only need stable IDs, not animation parameters.</summary>
    [CreateAssetMenu(menuName = "Spotlight/Presentation/Effect Library")]
    public sealed class PresentationEffectLibrary : ScriptableObject
    {
        [SerializeField] private PresentationEffectDefinition[] definitions = CreateDefaults();
        public PresentationEffectDefinition Find(string fx)
        {
            if (definitions == null) throw new InvalidOperationException("Presentation effect library is not configured.");
            foreach (PresentationEffectDefinition definition in definitions)
                if (definition != null && string.Equals(definition.fx, fx, StringComparison.Ordinal))
                {
                    if (float.IsNaN(definition.duration) || float.IsInfinity(definition.duration) || definition.duration <= 0f
                        || float.IsNaN(definition.strength) || float.IsInfinity(definition.strength) || definition.strength < 0f
                        || (fx == PresentationEffectIds.BackgroundPan && (float.IsNaN(definition.zoom) || float.IsInfinity(definition.zoom) || definition.zoom < 1f)))
                        throw new InvalidOperationException("Invalid presentation effect preset: " + fx);
                    return definition;
                }
            throw new ArgumentException("Unknown presentation effect ID: " + fx, nameof(fx));
        }
        public void EnsureNarrativePresets()
        {
            var list = new System.Collections.Generic.List<PresentationEffectDefinition>(definitions ?? Array.Empty<PresentationEffectDefinition>());
            foreach (var preset in CreateDefaults())
                if (!list.Exists(d => d != null && d.fx == preset.fx)) list.Add(preset);
            definitions = list.ToArray();
        }
        private static PresentationEffectDefinition[] CreateDefaults() => new[]
        {
            new PresentationEffectDefinition(PresentationEffectIds.EnterShadowWalk, .52f, 110f),
            new PresentationEffectDefinition(PresentationEffectIds.AccentShakeStartle, .28f, 18f),
            new PresentationEffectDefinition(PresentationEffectIds.ShakeLight, .24f, 7f, 1),
            new PresentationEffectDefinition(PresentationEffectIds.ShakeHeavy, .46f, 22f, 2),
            new PresentationEffectDefinition(PresentationEffectIds.FlashWhite, .16f, .9f),
            new PresentationEffectDefinition(PresentationEffectIds.TransitionBlack, .6f, 1f),
            new PresentationEffectDefinition(PresentationEffectIds.PortraitFadeOut, .22f),
            new PresentationEffectDefinition(PresentationEffectIds.PortraitFadeIn, .22f),
            new PresentationEffectDefinition(PresentationEffectIds.PortraitNod, .25f, 8f),
            new PresentationEffectDefinition(PresentationEffectIds.BackgroundPan, 1.6f, .02f, zoom: 1.05f)
        };
    }
}
