using Spotlight.Audio;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>Read-only sound inputs. Never infer flow from the refillable source's volume delta.</summary>
    public static class FreeCafeSoundDynamics
    {
        private static readonly CafeAudioDynamics Defaults = new CafeAudioDynamics();
        private static CafeAudioDynamics Settings => GameAudio.Catalog != null && GameAudio.Catalog.cafeDynamics != null
            ? GameAudio.Catalog.cafeDynamics : Defaults;

        public static float DragDistanceForRustle => Mathf.Max(.05f, Settings.dragDistanceForRustle);
        public static float DragRustleCooldown => Mathf.Max(.1f, Settings.dragRustleCooldown);

        public static void Pour(float normalizedFlow, float receiverFill, out float volume, out float pitch)
        {
            var settings = Settings;
            float flow = Mathf.Clamp01(normalizedFlow);
            volume = Mathf.Lerp(settings.pourVolume.x, settings.pourVolume.y, flow);
            pitch = Mathf.Lerp(settings.pourPitch.x, settings.pourPitch.y, flow)
                + Mathf.Clamp01(receiverFill) * settings.receiverFillPitch;
        }

        public static void Heating(float progress, float normalizedTemperature, out float volume, out float pitch)
        {
            var settings = Settings;
            float intensity = Mathf.Max(Mathf.Clamp01(progress), Mathf.Clamp01(normalizedTemperature));
            volume = Mathf.Lerp(settings.heatingVolume.x, settings.heatingVolume.y, intensity);
            pitch = Mathf.Lerp(settings.heatingPitch.x, settings.heatingPitch.y, intensity);
        }

        public static float Outflow(CafeWorld world, WorldEntity source, float requestedMl)
            => source == null ? 0 : world.IsInfiniteLiquidContainer(source) ? Mathf.Max(0, requestedMl)
                : Mathf.Min(Mathf.Max(0, requestedMl), world.LiquidVolume(source));

        public static WorldEntity PourReceiver(CafeWorld world, WorldEntity target)
            => target == null ? null : world.ExtractionFilter(target) ?? target;

        public static float Fill(CafeWorld world, WorldEntity receiver)
        {
            float capacity = world.LiquidCapacity(receiver);
            return capacity > 0 ? Mathf.Clamp01(world.LiquidVolume(receiver) / capacity) : 0;
        }

        /// <summary>Same attachment, self-heating and docking tolerances as FreeCafeHeatingVisual.</summary>
        public static WorldEntity StoveVessel(CafeWorld world, WorldEntity stove)
        {
            if (!world.ProductionEnabled || stove == null || stove.specId != "machine.stove") return null;
            var vessel = world.Find(stove.production?.heatingVesselId);
            if (!world.IsContainer(vessel) || vessel.production == null || vessel.area != stove.area ||
                vessel.production.selfHeating || vessel.production.phase != ProductionPhase.Processing) return null;
            float dockY = stove.y + world.Spec(stove.specId).height * .45f + world.Spec(vessel.specId).height * .5f;
            return Mathf.Abs(vessel.x - stove.x) <= .8f && Mathf.Abs(vessel.y - dockY) <= .7f ? vessel : null;
        }

        public static bool Finished(ProductionEntityState priorState, ProductionPhase priorPhase, ProductionEntityState current)
            => ReferenceEquals(priorState, current) && priorPhase == ProductionPhase.Processing && current.remaining <= .0001f &&
                (current.phase == ProductionPhase.Ready || current.phase == ProductionPhase.Room ||
                 current.phase == ProductionPhase.Boiling || current.phase == ProductionPhase.Warm);

        public static string PickupKey(string specId) => specId == "beans" ? AudioKeys.BeansPickup
            : specId == "cup.small" || specId == "cup.large" ? AudioKeys.CupPickup : AudioKeys.Pickup;

        public static string AdditionKey(CafeWorld world, string specId)
        {
            if (specId == "beans") return AudioKeys.BeansAdd;
            if (specId == "grounds" || specId == "sugar" || specId == "matcha.powder" || specId == "cocoa.powder")
                return AudioKeys.PowderAdd;
            return world.IngredientDefinition(specId)?.liquid == true ? AudioKeys.LiquidAdd : AudioKeys.Ingredient;
        }
    }
}
