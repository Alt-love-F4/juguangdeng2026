using System;
using System.Linq;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    public sealed partial class NarrativeChapterPreview
    {
        // Text-only preview owns a fresh Load() result. Formal configuration and runtime are never changed.
        private static void PrepareNeutralTextPreview(NarrativeConfiguration config, NarrativeEventDefinition root)
        {
            var resultIds = config.results.Where(r => r.eventId == root.eventId).Select(r => r.resultId).ToArray();
            config.results.RemoveAll(r => r.eventId == root.eventId);
            config.conditions.RemoveAll(c => c.ownerType == "event" && c.ownerId == root.eventId
                || c.ownerType == "result" && resultIds.Contains(c.ownerId));
            config.eventDates.RemoveAll(d => d.eventId == root.eventId);
            config.presentations.conversationPages.RemoveAll(p => p.presentationKey == root.presentationKey
                && (p.stepType == NarrativeConversationStepType.WaitBusiness || p.stepType == NarrativeConversationStepType.WaitNight
                    || p.stepType == NarrativeConversationStepType.WaitService));
            root.entry = NarrativeEntryPoint.Immediate;
        }

        private bool ReadNeutralPreviewCondition(string key, bool construction)
        {
            if (construction && EventId == "neutral.1018.s1")
            {
                if (key.StartsWith("not:", StringComparison.Ordinal))
                    return !ReadNeutralPreviewCondition(key.Substring(4), true);
                if (key == "service:passed")
                    return constructionCondition?.Invoke(key) ?? throw new InvalidOperationException("Missing explicit quality preview sample.");
            }
            return ReadConstructionPageCondition(key, construction);
        }
    }
}
