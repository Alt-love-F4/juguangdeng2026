using System;

namespace Spotlight.GameFlow
{
    public sealed partial class NarrativeChapterPreview
    {
        /// <summary>
        /// Plays the complete blocked raid text with real Inline snapshots in an isolated session.
        /// Does not produce clearance/detention facts, advance dates or open the formal map action.
        /// </summary>
        public void PlayChurchRaidConstruction(int language, int courage, int wisdom, int perception,
            Action completed = null)
        {
            constructionSeed = debug =>
            {
                debug.SetDate(15);
                debug.SetAbility("language", language);
                debug.SetAbility("courage", courage);
                debug.SetAbility("wisdom", wisdom);
                debug.SetAbility("perception", perception);
            };
            Play("church.1020.s1", perception, null, completed, true);
        }
    }
}
