using System;
using System.Globalization;

namespace Spotlight.Narrative
{
    public static partial class LubanNarrativeLoader
    {
        public static string ValidateTrainingResult(NarrativeConfiguration c, NarrativeResultDefinition result)
        {
            if (result == null || result.resultType != NarrativeResultType.TrainAttribute) return null;
            string attribute = result.target ?? "";
            if (attribute.StartsWith("player.", StringComparison.Ordinal) || attribute.StartsWith("player:", StringComparison.Ordinal)) attribute = attribute.Substring(7);
            if (attribute != "language" && attribute != "courage" && attribute != "wisdom" && attribute != "perception") return "TRAIN_ATTRIBUTE requires a player attribute target.";
            if (!int.TryParse(result.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int xp) || xp <= 0) return "TRAIN_ATTRIBUTE reward must be positive integer XP.";
            var player = c.bars.Find(b => b != null && b.barKey == "player." + attribute);
            var training = c.bars.Find(b => b != null && b.barKey == "training." + attribute);
            if (player == null || player.sourceMode != NarrativeSourceMode.Ncs || player.advanceMode == NarrativeAdvanceMode.Derived || player.min < 0 || player.max != 20)
                return "TRAIN_ATTRIBUTE requires writable centralized player." + attribute + " with max=20.";
            if (training == null || training.sourceMode != NarrativeSourceMode.Ncs || training.advanceMode != NarrativeAdvanceMode.EventDriven || training.min != 0 || training.max != 9)
                return "TRAIN_ATTRIBUTE requires persistent NCS-owned training." + attribute + " EventDriven bar with range 0..9.";
            return null;
        }
    }
}
