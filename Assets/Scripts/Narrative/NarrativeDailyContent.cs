using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativeController
    {
        // Daily content is authored in the existing Event/Condition/Action tables. No second
        // runtime, random state, or save participant is created for the content pool.
        private static string PoolProperty(NarrativeEventDefinition definition, string name)
            => definition?.eventProperties?.FirstOrDefault(p => p != null && p.property == name)?.value;

        public bool UsesDailyPool(NarrativeActionDefinition action)
            => action?.resultEvents != null && action.resultEvents.Count > 0
                && action.resultEvents.All(id => events.TryGetValue(id, out var e)
                    && !string.IsNullOrWhiteSpace(PoolProperty(e, "dailyPool")));

        /// <summary>Preflight only; never spends AP, marks a visit, or advances rotation.</summary>
        public bool TrySelectActionEvents(NarrativeActionDefinition action, out List<string> sequence)
            => TrySelectActionEvents(action, out sequence, ignoreTransientBusy: false);

        /// <summary>Stable map projection only; execution keeps all transient entry/playback guards.</summary>
        internal bool TrySelectActionEvents(NarrativeActionDefinition action, out List<string> sequence, bool ignoreTransientBusy)
        {
            sequence = new List<string>();
            if (!UsesDailyPool(action))
            {
                if (action?.resultEvents != null) sequence.AddRange(action.resultEvents);
                return true;
            }
            var configured = action.resultEvents.Select(id => events[id]).ToList();
            var poolKeys = configured.Select(e => PoolProperty(e, "dailyPool")).Distinct().ToArray();
            if (poolKeys.Length != 1) return false;
            bool Available(NarrativeEventDefinition e) => ExplainMapActionEvent(e.eventId, ignoreTransientBusy).CanRequest;
            var introductions = configured.Where(e => PoolProperty(e, "dailyKind") == "visit"
                && !state.HasHappenedEvent(e.eventId)).ToList();
            // Never skip an unavailable first visit and jump directly into an ordinary line.
            if (introductions.Any(e => !Available(e))) return false;
            var candidates = configured.Where(e => PoolProperty(e, "dailyKind") != "visit" && Available(e)).ToList();
            if (candidates.Count == 0) return false;
            var news = candidates.Where(e => PoolProperty(e, "dailyKind") == "news").ToList();
            var choices = news.Count > 0 ? news : candidates;
            var selected = choices.OrderBy(e => state.GetCounter("daily.playcount." + e.eventId))
                .ThenBy(e => configured.IndexOf(e)).First();
            sequence.AddRange(introductions.Select(e => e.eventId));
            sequence.Add(selected.eventId);
            return true;
        }

        internal void RefreshDailyAvailability(int day)
        {
            bool Fact(string key) => state.TryGetCondition(key, out bool value) && value;
            void SetIfDeclared(string key, bool value)
            {
                if (state.TryGetCondition(key, out bool previous) && previous != value) state.SetCondition(key, value);
            }
            SetIfDeclared("availability.adele", !IsEnded && !Fact("story.adele.away"));
            SetIfDeclared("availability.sylvain", !IsEnded && !(day >= 11 && day <= 13) && !Fact("story.sylvain.unavailable"));
            // Safe migration only for encounters shared by every completed path of these roots.
            if (state.HasHappenedEvent("arrival.complete")) SetIfDeclared("known.adele", true);
            if (Fact("story.neutral.1008.s1.completed")) SetIfDeclared("known.colette", true);
            if (Fact("story.rose.1011.s1.completed"))
            {
                SetIfDeclared("known.sparrow", true);
                // The source compiler records known.sylvain only on the actual long encounter.
            }
            if (Fact("story.revolution.1014.s1.completed")) SetIfDeclared("known.sorel", true);
        }
        internal void RecordDailyPoolCompletion(string eventId)
        {
            if (events.TryGetValue(eventId ?? string.Empty, out var e)
                && !string.IsNullOrWhiteSpace(PoolProperty(e, "dailyPool")))
            {
                string key = "daily.playcount." + eventId;
                int count = state.GetCounter(key);
                if (count < int.MaxValue) state.SetCounter(key, count + 1);
            }
        }
    }
}

