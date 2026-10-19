using System;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativeController
    {
        private readonly Dictionary<string, bool> gameplayResults = new Dictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>Only the presenter publishes a completed real host attempt. No persistent/global flags.</summary>
        public void RecordGameplayResultForPresentation(string pageId, bool passed)
        {
            if (!activePresentation || activeRequest.Event == null) throw new InvalidOperationException("Gameplay results require an active root.");
            var page = configuration.presentations.conversationPages.Find(p => p != null && p.pageId == pageId);
            if (page == null || page.stepType != NarrativeConversationStepType.Gameplay || page.presentationKey != activeRequest.Event.presentationKey)
                throw new InvalidOperationException("Gameplay result does not belong to active root: " + pageId);
            // A page may deliberately be revisited by a bounded graph; the most recent real attempt wins.
            gameplayResults[pageId] = passed;
        }
    }
}
