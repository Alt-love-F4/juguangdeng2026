using System;
using System.Collections.Generic;
using System.Globalization;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativeController
    {
        // These dictionaries are transaction-local simulations, never a second attribute store.
        // Expand before ordinary preflight, so an invalid later reward cannot partially train anyone.
        private List<NarrativeResultDefinition> ExpandTrainingResults(List<NarrativeResultDefinition> batch)
        {
            if (!batch.Exists(r => r != null && r.resultType == NarrativeResultType.TrainAttribute)) return batch;
            var expanded = new List<NarrativeResultDefinition>();
            var simulated = new Dictionary<string, int>(StringComparer.Ordinal);
            int Read(string key)
            {
                if (simulated.TryGetValue(key, out int cached)) return cached;
                if (!state.TryGetBar(key, out int value)) throw new InvalidOperationException("Training bar is unavailable: " + key);
                return value;
            }
            foreach (var result in batch)
            {
                if (result.resultType != NarrativeResultType.TrainAttribute)
                {
                    expanded.Add(result);
                    if (result.resultType == NarrativeResultType.BarAdd || result.resultType == NarrativeResultType.BarSet)
                    {
                        string key = StripPrefix(result.target, "bar:");
                        var bar = configuration.bars.Find(b => b != null && b.barKey == key);
                        if (bar != null && state.IsWritableBar(key) && int.TryParse(result.value, out int amount))
                        {
                            int before = Read(key);
                            int simulatedNext = bar.Clamp(result.resultType == NarrativeResultType.BarSet ? amount : (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, (long)before + amount)));
                            simulated[key] = bar.advanceMode == NarrativeAdvanceMode.Monotonic ? Math.Max(before, simulatedNext) : simulatedNext;
                        }
                    }
                    continue;
                }
                string error = LubanNarrativeLoader.ValidateTrainingResult(configuration, result);
                if (error != null) throw new InvalidOperationException("Result " + result.resultId + ": " + error);
                string attribute = StripPrefix(result.target, "player:", "player.");
                string playerKey = "player." + attribute, trainingKey = "training." + attribute;
                int current = Read(playerKey), remainder = Read(trainingKey);
                if (current < 0 || current > 20 || remainder < 0 || remainder > 9) throw new InvalidOperationException("Training state out of range: " + attribute);
                long total = (long)remainder + int.Parse(result.value, CultureInfo.InvariantCulture);
                int next = (int)Math.Min(20, current + total / 10);
                int rest = next >= 20 ? 0 : (int)(total % 10);
                simulated[playerKey] = next; simulated[trainingKey] = rest;
                expanded.Add(TrainingWrite(result, playerKey, next));
                expanded.Add(TrainingWrite(result, trainingKey, rest));
            }
            return expanded;
        }
        private static NarrativeResultDefinition TrainingWrite(NarrativeResultDefinition source, string target, int value) => new NarrativeResultDefinition
        {
            resultId = source.resultId, eventId = source.eventId, seq = source.seq, resultType = NarrativeResultType.BarSet,
            target = target, value = value.ToString(CultureInfo.InvariantCulture), note = source.note
        };
    }
}
