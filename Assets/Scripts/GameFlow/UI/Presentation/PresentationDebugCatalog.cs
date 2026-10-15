using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>Discovers generated content without executing events or maintaining a second renderer.</summary>
    public static class PresentationDebugCatalog
    {
        public sealed class Entry
        {
            public string Id, Label;
        }

        public static List<Entry> List(NarrativeConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            var entries = new List<Entry>();
            foreach (var opening in configuration.openingPresentations.Where(x => x != null))
                entries.Add(new Entry { Id = "opening:" + opening.presentationId,
                    Label = "Opening | " + opening.presentationId + " | " + Text(configuration, opening.titleKey) });
            foreach (var presentation in configuration.presentations.index.Where(x => x != null))
                entries.Add(new Entry { Id = presentation.presentationClass.ToLowerInvariant() + ":" + presentation.presentationKey,
                    Label = presentation.presentationClass + " | " + presentation.presentationKey + " | " + presentation.logTag });
            foreach (var definition in configuration.events.Where(x => x != null))
            {
                var setup = configuration.setups.Find(x => x != null && x.setupKey == definition.setupKey);
                if (!NarrativePresenter.RequiresPresentation(definition, setup)) continue;
                entries.Add(new Entry { Id = "event:" + definition.eventId,
                    Label = "Event | " + definition.eventId + " | " + Text(configuration, definition.titleText) });
            }
            foreach (var setup in configuration.setups.Where(x => x != null))
                entries.Add(new Entry { Id = "setup:" + setup.setupKey, Label = "Setup | " + setup.setupKey + " | " + setup.tplName });
            return entries.GroupBy(x => x.Id, StringComparer.Ordinal).Select(x => x.First())
                .OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Continuous previews start from real events. Results/choices/checks are scheduled by the
        /// production controller, not a guessed filename/ID prefix or a debug-only story graph.
        /// Include intermediate entries for direct testing, plus unbound new content for authoring.
        /// </summary>
        public static List<Entry> ListSequences(NarrativeConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            var incoming = new HashSet<string>(StringComparer.Ordinal);
            foreach (var result in configuration.results)
            {
                if (result == null || result.resultType != NarrativeResultType.QueueEvent || string.IsNullOrWhiteSpace(result.target)) continue;
                incoming.Add(result.target.StartsWith("event:", StringComparison.OrdinalIgnoreCase)
                    ? result.target.Substring("event:".Length) : result.target);
            }
            foreach (var check in configuration.abilityChecks)
            {
                if (check == null) continue;
                if (!string.IsNullOrWhiteSpace(check.successEventId)) incoming.Add(check.successEventId);
                if (!string.IsNullOrWhiteSpace(check.failureEventId)) incoming.Add(check.failureEventId);
            }
            var entries = configuration.events.Where(item => item != null && !string.IsNullOrWhiteSpace(item.eventId))
                .OrderBy(item => incoming.Contains(item.eventId)).ThenBy(item => item.eventId, StringComparer.Ordinal)
                .Select(item => new Entry
                {
                    Id = "sequence:" + item.eventId,
                    Label = (incoming.Contains(item.eventId) ? "中途试播" : "入口候选") + " | " + item.eventId + " | " + Text(configuration, item.titleText)
                }).ToList();
            var bound = new HashSet<string>(configuration.events.Where(item => item != null)
                .Select(item => item.presentationKey), StringComparer.Ordinal);
            entries.AddRange(List(configuration).Where(item => item.Id.StartsWith("opening:", StringComparison.Ordinal)
                || (!item.Id.StartsWith("event:", StringComparison.Ordinal) && !item.Id.StartsWith("setup:", StringComparison.Ordinal)
                    && !bound.Contains(item.Id.Substring(item.Id.IndexOf(':') + 1)))));
            return entries.GroupBy(item => item.Id, StringComparer.Ordinal).Select(group => group.First()).ToList();
        }

        public static NarrativeEventDefinition Event(NarrativeConfiguration configuration, string id)
        {
            int split = id?.IndexOf(':') ?? -1;
            if (split < 1 || split == id.Length - 1) throw new ArgumentException("Invalid presentation id: " + id);
            string kind = id.Substring(0, split), key = id.Substring(split + 1);
            if (kind == "event")
                return configuration.events.Find(x => x != null && x.eventId == key)
                    ?? throw new InvalidOperationException("Missing event: " + key);
            if (kind == "setup")
            {
                if (!configuration.setups.Exists(x => x != null && x.setupKey == key))
                    throw new InvalidOperationException("Missing setup: " + key);
                return new NarrativeEventDefinition { eventId = id, setupKey = key };
            }
            var presentation = configuration.presentations.index.Find(x => x != null && x.presentationKey == key
                && string.Equals(x.presentationClass, kind, StringComparison.OrdinalIgnoreCase));
            if (presentation == null) throw new InvalidOperationException("Unknown presentation: " + id);
            // A bare presentation has no event-specific properties. Use event:<id> to preview those.
            return new NarrativeEventDefinition { eventId = id, presentationKey = key, presentationClass = presentation.presentationClass,
                titleText = configuration.events.Where(x => x != null && x.presentationKey == key)
                    .OrderBy(x => x.eventId, StringComparer.Ordinal).FirstOrDefault()?.titleText };
        }

        public static string Text(NarrativeConfiguration configuration, string key)
            => string.IsNullOrEmpty(key) ? string.Empty : configuration.texts.Find(x => x != null && x.key == key)?.content ?? key;
    }
}
