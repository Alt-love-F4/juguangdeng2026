using System;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        /// <summary>Reads authoritative Celsius without changing legacy data or inventory.</summary>
        public float TemperatureCelsius(Portion portion)
        {
            if (portion == null) return Prep.ambientTemperatureCelsius;
            if (portion.temperatureCelsius != -1) return portion.temperatureCelsius;
            return portion.temperature == LiquidTemperature.Boiling ? Prep.boilingTemperatureCelsius
                : portion.temperature == LiquidTemperature.Warm ? Prep.warmTemperatureCelsius : Prep.ambientTemperatureCelsius;
        }

        /// <summary>Actual ml plus free grams, approximating equal density/heat capacity.
        /// Internal composition/provenance grams are not extra free mass and are never counted twice.</summary>
        public float GetThermalWeight(Portion portion) => portion == null ? 0
            : Math.Max(0, portion.milliliters) + Math.Max(0, portion.grams);

        public float TemperatureCelsius(WorldEntity entity)
        {
            if (entity == null) return Prep.ambientTemperatureCelsius;
            if (entity.contents.Count == 0)
                return entity.portionPayload != null ? TemperatureCelsius(entity.portionPayload)
                    : entity.specId == "hotwater" || entity.specId == "hotmilk" ? Prep.boilingTemperatureCelsius : Prep.ambientTemperatureCelsius;
            double energy = 0, weight = 0;
            foreach (var portion in entity.contents)
            {
                double mass = GetThermalWeight(portion);
                if (!ProductionEnabled && mass <= 0 && IsLiquid(portion.specId))
                    mass = Math.Max(0, portion.units) * LiquidCapacity(entity) / Math.Max(1, Spec(entity.specId).capacity);
                energy += mass * TemperatureCelsius(portion); weight += mass;
            }
            return weight > 0 ? (float)(energy / weight) : Prep.ambientTemperatureCelsius;
        }

        public LiquidTemperature TemperatureBand(float temperatureCelsius) => temperatureCelsius >= Prep.boilingThresholdCelsius
            ? LiquidTemperature.Boiling : temperatureCelsius >= Prep.hotThresholdCelsius ? LiquidTemperature.Warm : LiquidTemperature.Room;

        /// <summary>Writes exact Celsius and mirrors the legacy band. Invalid temperatures never mutate a portion.</summary>
        public void SetTemperature(Portion portion, float temperatureCelsius)
        {
            if (portion == null) throw new ArgumentNullException(nameof(portion));
            if (!Finite(temperatureCelsius) || temperatureCelsius < 0 || temperatureCelsius > 100)
                throw new ArgumentOutOfRangeException(nameof(temperatureCelsius), "Temperature must be finite and between 0 and 100 Celsius.");
            portion.temperatureCelsius = temperatureCelsius;
            portion.temperature = TemperatureBand(temperatureCelsius);
        }

        private void SetContainerTemperature(WorldEntity entity, float temperatureCelsius)
        {
            foreach (var portion in entity.contents) SetTemperature(portion, temperatureCelsius);
        }

        // Capture before any ingredient compaction, reaction, or inventory mutation.
        private float MixedTemperature(WorldEntity target, WorldEntity source, float acceptedFraction)
        {
            double energy = 0, weight = 0;
            if (target != null) foreach (var portion in target.contents)
            {
                double mass = GetThermalWeight(portion);
                energy += mass * TemperatureCelsius(portion); weight += mass;
            }
            if (source != null) foreach (var portion in source.contents)
            {
                double mass = GetThermalWeight(portion) * acceptedFraction;
                energy += mass * TemperatureCelsius(portion); weight += mass;
            }
            return weight > 0 ? (float)(energy / weight) : Prep.ambientTemperatureCelsius;
        }

        private float MixedTemperature(WorldEntity target, Portion incoming)
        {
            double prior = 0;
            foreach (var portion in target.contents) prior += GetThermalWeight(portion);
            double added = GetThermalWeight(incoming);
            return prior + added > 0
                ? (float)((TemperatureCelsius(target) * prior + TemperatureCelsius(incoming) * added) / (prior + added))
                : Prep.ambientTemperatureCelsius;
        }
    }
}
