using System;

namespace Spotlight.GameFlow
{
    public sealed partial class NarrativeChapterPreview
    {
        /// <summary>Isolated day9 content and closed envelope prop; no faction or formal prerequisite settlement.</summary>
        public void PlayChurchGuestConstruction(int perception, Action completed = null)
        {
            constructionSeed = debug =>
            {
                debug.SetDate(9);
                debug.SetAbility("perception", perception);
            };
            Play("church.1014.s2", perception, null, completed, true);
        }
    }
}
