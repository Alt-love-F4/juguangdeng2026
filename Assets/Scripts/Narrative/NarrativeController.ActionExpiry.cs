using System;
using System.Linq;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativeController
    {
        /// <summary>Grants explicit expiry eligibility on the ordinary queue. Phase events wait
        /// for their declared entry; MapAction remains a player-only entrance.</summary>
        internal bool QueueExpiredActionEvent(string eventId)
        {
            if (IsEnded || presenterUnavailable || !events.TryGetValue(eventId ?? string.Empty, out var definition)
                || definition.entry == NarrativeEntryPoint.MapAction || EventRequiresServiceContext(definition)) return false;
            if (!EventCanStart(definition)) return false;
            var candidate = candidates.FirstOrDefault(c => c.eventId == eventId && c.source == NarrativeCandidateSource.Manual);
            if (candidate == null)
            {
                candidate = new NarrativeCandidateValue { eventId = eventId, entry = definition.entry,
                    source = NarrativeCandidateSource.Manual, day = GetDay(), sequence = ++candidateSequence,
                    priority = configuration.eventDates.Where(d => d != null && d.eventId == eventId).Select(d => d.priority).DefaultIfEmpty(0).Max() };
                candidates.Add(candidate);
            }
            // PromoteCandidates enforces declared phases, all ordinary event gates and stable priority.
            PromoteCandidates();
            ProcessQueue();
            return true;
        }
    }
}
