using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Spotlight.GameFlow;
using UnityEngine;

namespace Spotlight.Narrative.Editor
{
    public sealed class NarrativeIntegrationReport
    {
        public string Filter { get; private set; }
        public readonly List<NarrativeIntegrationEventReport> Events = new List<NarrativeIntegrationEventReport>();
        public readonly List<string> GlobalErrors = new List<string>();
        public readonly List<string> GlobalWarnings = new List<string>();

        public bool HasErrors => GlobalErrors.Count > 0 || Events.Any(item => item.HasErrors);
        public bool HasWarnings => GlobalWarnings.Count > 0 || Events.Any(item => item.Warnings.Count > 0);
        public int ErrorCount => GlobalErrors.Count + Events.Sum(item => item.Errors.Count);
        public int WarningCount => GlobalWarnings.Count + Events.Sum(item => item.Warnings.Count);

        public NarrativeIntegrationEventReport Find(string eventId)
            => Events.FirstOrDefault(item => string.Equals(item.EventId, eventId, StringComparison.Ordinal));

        public static NarrativeIntegrationReport Build(NarrativeConfiguration configuration, string filter = null)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            var report = new NarrativeIntegrationReport { Filter = string.IsNullOrWhiteSpace(filter) ? string.Empty : filter.Trim() };
            try { report.GlobalErrors.AddRange(NarrativeConfigurationValidator.Validate(configuration)); }
            catch (Exception error) { report.GlobalErrors.Add("Configuration validator threw: " + error.Message); }
            try { report.GlobalWarnings.AddRange(NarrativeConfigurationValidator.Warnings(configuration)); }
            catch (Exception error) { report.GlobalWarnings.Add("Configuration warning scan threw: " + error.Message); }

            IEnumerable<NarrativeEventDefinition> events = configuration.events.Where(item => item != null);
            if (!string.IsNullOrEmpty(report.Filter))
                events = events.Where(item => item.eventId != null && item.eventId.StartsWith(report.Filter, StringComparison.OrdinalIgnoreCase));
            foreach (NarrativeEventDefinition definition in events.OrderBy(item => item.eventId, StringComparer.Ordinal))
                report.Events.Add(BuildEventReport(configuration, definition));
            return report;
        }

        private static NarrativeIntegrationEventReport BuildEventReport(NarrativeConfiguration c, NarrativeEventDefinition definition)
        {
            var result = new NarrativeIntegrationEventReport
            {
                EventId = definition.eventId ?? string.Empty,
                Entry = definition.entry,
                Presentation = FormatPresentation(definition)
            };
            foreach (string blocker in (definition.eventProperties ?? new List<NarrativeEventProperty>())
                .Where(property => property != null && string.Equals(property.property, "blockedBy", StringComparison.OrdinalIgnoreCase))
                .SelectMany(property => (property.value ?? string.Empty).Split(new[] { ',', '+', ';' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.Ordinal))
                result.Errors.Add("Construction blocker: " + blocker);
            var eventDates = c.eventDates.Where(item => item != null && item.eventId == definition.eventId).OrderBy(item => item.rowIndex).ToList();
            var triggers = c.triggers.Where(item => item != null && item.eventId == definition.eventId).ToList();
            var actions = c.actions.Where(item => item != null && ((item.resultEvents ?? new List<string>()).Contains(definition.eventId)
                || (item.milestones ?? new List<NarrativeActionMilestoneDefinition>()).Any(milestone => milestone != null && milestone.eventId == definition.eventId))).ToList();
            var queueSources = c.results.Where(item => item != null && item.resultType == NarrativeResultType.QueueEvent
                && string.Equals(NormalizeEventTarget(item.target), definition.eventId, StringComparison.Ordinal)).ToList();

            foreach (NarrativeEventDateDefinition date in eventDates)
                result.Sources.Add("Date: day " + date.day + " / " + date.timeSlot + " / " + date.repeatRule);
            foreach (NarrativeTriggerDefinition trigger in triggers)
                result.Sources.Add("Trigger: " + trigger.triggerId + " / " + trigger.barKey + " @ " + trigger.threshold + " / " + trigger.crossType);
            foreach (NarrativeActionDefinition action in actions)
            {
                string positions = string.Join(", ", c.mapPositions.Where(position => position != null
                    && (position.defaultAction == action.actionKey || position.extendedActions.Contains(action.actionKey)))
                    .Select(position => position.positionKey).Distinct().ToList());
                result.Sources.Add("Action: " + action.actionKey + (string.IsNullOrEmpty(positions) ? string.Empty : " @ " + positions));
            }
            foreach (NarrativeResultDefinition source in queueSources)
                result.Sources.Add("QueueEvent: " + source.eventId + " -> " + definition.eventId);
            if (result.Sources.Count == 0)
            {
                if (definition.entry == NarrativeEntryPoint.Immediate)
                    result.Sources.Add("Immediate / continuation or code-owned entrance");
                else
                    result.Errors.Add("No formal source found for entry " + definition.entry + ". Add EventDate, Trigger, Action result, or QueueEvent.");
            }

            string ownerId = string.IsNullOrWhiteSpace(definition.conditionOwnerId) ? definition.eventId : definition.conditionOwnerId;
            var conditions = c.conditions.Where(item => item != null
                && string.Equals(item.ownerType ?? "event", "event", StringComparison.OrdinalIgnoreCase)
                && item.ownerId == ownerId).OrderBy(item => item.groupIndex).ThenBy(item => item.conditionId, StringComparer.Ordinal).ToList();
            foreach (NarrativeConditionDefinition condition in conditions)
            {
                string text = "G" + condition.groupIndex + ": " + condition.condType + "(" + condition.target;
                if (!string.IsNullOrWhiteSpace(condition.value)) text += ", " + condition.value;
                result.Conditions.Add(text + ")");
                if (condition.condType == "BOOL_EQ" && string.Equals(condition.value, "true", StringComparison.OrdinalIgnoreCase)
                    || condition.condType == "EVENT_HAPPENED") result.Prerequisites.Add(text + ")");
            }

            var pages = c.presentations?.conversationPages?.Where(item => item != null && item.presentationKey == definition.presentationKey)
                .OrderBy(item => item.seq).ToList() ?? new List<NarrativeConversationPageDefinition>();
            var lines = c.presentations?.conversationLines?.Where(item => item != null && pages.Any(page => page.pageId == item.pageId))
                .OrderBy(item => item.pageId, StringComparer.Ordinal).ThenBy(item => item.seq).ToList() ?? new List<NarrativeConversationLineDefinition>();
            result.PageCount = pages.Count;
            result.LineCount = lines.Count;
            result.ChoiceCount = c.choices.Count(item => item != null && pages.Any(page => page.optionGroupKey == item.groupKey));
            result.LetterStepCount = pages.Count(page => page.stepType == NarrativeConversationStepType.LetterReveal || page.stepType == NarrativeConversationStepType.LetterRead);
            // Reading can be the event's direct presentation, not only a nested letter step.
            // Resolve the index class exactly as the runtime does and count shared readings once.
            var presentation = c.presentations?.index?.FirstOrDefault(item => item != null && item.presentationKey == definition.presentationKey);
            string presentationClass = presentation?.presentationClass ?? definition.presentationClass;
            var readingKeys = new HashSet<string>(pages.Where(page => !string.IsNullOrWhiteSpace(page.readingKey)).Select(page => page.readingKey), StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(definition.presentationKey) && string.Equals(presentationClass, "Reading", StringComparison.OrdinalIgnoreCase))
                readingKeys.Add(definition.presentationKey);
            var readingPages = c.presentations?.readingPages?.Where(item => item != null && readingKeys.Contains(item.presentationKey))
                .ToList() ?? new List<NarrativeReadingPageDefinition>();
            var readingPageIds = new HashSet<string>(readingPages.Select(page => page.pageId), StringComparer.Ordinal);
            var readingLines = c.presentations?.readingLines?.Where(item => item != null && readingPageIds.Contains(item.pageId))
                .ToList() ?? new List<NarrativeReadingLineDefinition>();
            result.ReadingPageCount = readingPages.Count;
            result.ReadingLineCount = readingLines.Count;

            var setup = c.setups.FirstOrDefault(item => item != null && item.setupKey == definition.setupKey);
            if (!string.IsNullOrWhiteSpace(definition.setupKey) && setup == null)
                result.Errors.Add("Missing SetupParams: " + definition.setupKey);
            if (!string.IsNullOrWhiteSpace(definition.presentationKey) && pages.Count == 0
                && string.Equals(presentationClass, "Conversation", StringComparison.OrdinalIgnoreCase))
                result.Errors.Add("Conversation has no ConversationPage rows: " + definition.presentationKey);
            if (!string.IsNullOrWhiteSpace(definition.presentationKey) && string.Equals(presentationClass, "Reading", StringComparison.OrdinalIgnoreCase)
                && !readingPages.Any(page => page.presentationKey == definition.presentationKey))
                result.Errors.Add("Reading presentation has no ReadingPage rows: " + definition.presentationKey);

            var checkedBackgrounds = new HashSet<string>(StringComparer.Ordinal);
            foreach (NarrativeConversationPageDefinition page in pages)
            {
                if (page.stepType == NarrativeConversationStepType.PassiveCheck && !string.IsNullOrWhiteSpace(page.checkId))
                    result.Checks.Add(page.checkId);
                if (!string.IsNullOrWhiteSpace(page.readingKey))
                {
                    var reading = c.presentations?.readingPages?.Where(item => item != null && item.presentationKey == page.readingKey).ToList() ?? new List<NarrativeReadingPageDefinition>();
                    if (reading.Count == 0) result.Errors.Add("Missing reading presentation for page " + page.pageId + ": " + page.readingKey);
                }
                if (!string.IsNullOrWhiteSpace(page.bgOverride) && checkedBackgrounds.Add(page.bgOverride))
                    CheckBackground(page.bgOverride, result);
                if (!string.IsNullOrWhiteSpace(page.bgmOverride)) CheckAudio(page.bgmOverride, result);
            }
            if (!string.IsNullOrWhiteSpace(setup?.checkId)) result.Checks.Add(setup.checkId);
            foreach (NarrativeChoiceDefinition choice in c.choices.Where(item => item != null && pages.Any(page => page.optionGroupKey == item.groupKey)))
            {
                if (!string.IsNullOrWhiteSpace(choice.checkId)) result.Checks.Add(choice.checkId);
                RequireText(c, choice.textKey, "Choice " + choice.choiceId, result);
            }
            foreach (NarrativeConversationLineDefinition line in lines)
            {
                RequireText(c, line.textKey, "Line " + line.lineId, result);
                if (!string.IsNullOrWhiteSpace(line.speakerKey) && !c.objects.Any(item => item != null && item.objId == line.speakerKey))
                    result.Errors.Add("Line " + line.lineId + " references unknown speaker: " + line.speakerKey);
                if (!string.IsNullOrWhiteSpace(line.speakerNameKey))
                    RequireText(c, line.speakerNameKey, "Speaker name " + line.lineId, result);
            }
            foreach (NarrativeReadingPageDefinition page in readingPages)
            {
                RequireText(c, page.titleKey, "Reading page title " + page.pageId, result);
                if (!readingLines.Any(line => line.pageId == page.pageId))
                    result.Errors.Add("ReadingPage has no ReadingLine rows: " + page.pageId);
            }
            foreach (NarrativeReadingLineDefinition line in readingLines)
            {
                if (string.IsNullOrWhiteSpace(line.textKey)) result.Errors.Add("Reading line " + line.lineId + " has no text key.");
                else RequireText(c, line.textKey, "Reading line " + line.lineId, result);
                if (!string.IsNullOrWhiteSpace(line.speakerKey) && !c.objects.Any(item => item != null && item.objId == line.speakerKey))
                    result.Errors.Add("Reading line " + line.lineId + " references unknown speaker: " + line.speakerKey);
            }
            if (!string.IsNullOrWhiteSpace(definition.titleText)) RequireText(c, definition.titleText, "Event title", result);
            foreach (string key in definition.contentKeys ?? new List<string>()) RequireText(c, key, "Event content", result);

            List<string> uniqueChecks = result.Checks.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToList();
            result.Checks.Clear();
            result.Checks.AddRange(uniqueChecks);
            foreach (NarrativeResultDefinition effect in c.results.Where(item => item != null && item.eventId == definition.eventId).OrderBy(item => item.seq))
            {
                string value = string.IsNullOrWhiteSpace(effect.target) ? effect.resultType.ToString() : effect.resultType + " -> " + effect.target;
                result.ExpectedResults.Add(value);
                if (effect.resultType == NarrativeResultType.QueueEvent)
                {
                    string target = NormalizeEventTarget(effect.target);
                    if (!c.events.Any(item => item != null && item.eventId == target))
                        result.Errors.Add("QueueEvent result references unknown event: " + target);
                }
            }
            if (definition.onceOnly && result.ExpectedResults.Count == 0 && !IsCodeOwnedSignal(c, definition))
                result.Warnings.Add("onceOnly event has no result rows; confirm that this is intentional.");
            if (definition.entry == NarrativeEntryPoint.MapAction && !actions.Any() && !triggers.Any())
                result.Errors.Add("MapAction event has no action or trigger route.");
            if ((definition.entry == NarrativeEntryPoint.MorningBeforeMap || definition.entry == NarrativeEntryPoint.AfterBusiness || definition.entry == NarrativeEntryPoint.BeforeNight)
                && eventDates.Count == 0 && !queueSources.Any())
                result.Errors.Add("Formal phase event has no EventDate or QueueEvent route.");
            return result;
        }

        private static string FormatPresentation(NarrativeEventDefinition definition)
        {
            string value = string.IsNullOrWhiteSpace(definition.presentationClass) ? "NoClass" : definition.presentationClass;
            if (!string.IsNullOrWhiteSpace(definition.presentationKey)) value += "/" + definition.presentationKey;
            if (!string.IsNullOrWhiteSpace(definition.setupKey)) value += " setup=" + definition.setupKey;
            else if (!string.IsNullOrWhiteSpace(definition.setupType)) value += " setupType=" + definition.setupType;
            return value;
        }

        private static bool IsCodeOwnedSignal(NarrativeConfiguration configuration, NarrativeEventDefinition definition)
        {
            if (configuration == null || definition == null) return false;
            if (definition.eventId == "EV_FIRST_DAY_PERCEPTION_CHECK"
                || definition.eventId == "EV_FIRST_DAY_PERCEPTION_SUCCESS"
                || definition.eventId == "EV_FIRST_DAY_PERCEPTION_FAILURE") return true;
            if (string.IsNullOrWhiteSpace(definition.setupKey)) return false;
            NarrativeSetupParamsDefinition setup = configuration.setups.FirstOrDefault(item => item != null && item.setupKey == definition.setupKey);
            return setup != null && string.Equals(setup.tplName, "Tpl_NarrativeSignal", StringComparison.Ordinal);
        }

        private static void RequireText(NarrativeConfiguration c, string key, string label, NarrativeIntegrationEventReport result)
        {
            if (!string.IsNullOrWhiteSpace(key) && !c.texts.Any(item => item != null && item.key == key))
                result.Errors.Add(label + " references missing text: " + key);
        }

        private static void CheckBackground(string key, NarrativeIntegrationEventReport result)
        {
            var registry = Resources.Load<NarrativeStageVisuals>("Dialogue/NarrativeStageVisuals");
            if (registry != null)
            {
                try
                {
                    if (registry.Find(key) != null) return;
                }
                catch (Exception error)
                {
                    result.Errors.Add("Invalid background registry entry for " + key + ": " + error.Message);
                    return;
                }
            }
            if (Resources.Load<Texture>(key) != null || Resources.Load<Sprite>(key) != null) return;
            result.Errors.Add("Missing background resource: " + key);
        }

        private static void CheckAudio(string key, NarrativeIntegrationEventReport result)
        {
            if (Resources.Load<AudioClip>(key) == null)
                result.Warnings.Add("Missing optional BGM resource: " + key);
        }

        public static string NormalizeEventTarget(string target)
        {
            string value = (target ?? string.Empty).Trim();
            foreach (string prefix in new[] { "event:", "queue:" })
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return value.Substring(prefix.Length).Trim();
            return value;
        }

        public string ToMarkdown()
        {
            var text = new StringBuilder();
            text.AppendLine("# NCS Integration Report");
            text.AppendLine();
            text.AppendLine("- Filter: " + (string.IsNullOrEmpty(Filter) ? "(all)" : Filter));
            text.AppendLine("- Events: " + Events.Count.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("- Errors: " + ErrorCount.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("- Warnings: " + WarningCount.ToString(CultureInfo.InvariantCulture));
            text.AppendLine();
            AppendMessages(text, "Global errors", GlobalErrors);
            AppendMessages(text, "Global warnings", GlobalWarnings);
            foreach (NarrativeIntegrationEventReport item in Events)
            {
                text.AppendLine("## " + item.EventId + " [" + item.Status + "]");
                text.AppendLine();
                text.AppendLine("- Entry: " + item.Entry);
                text.AppendLine("- Sources: " + (item.Sources.Count == 0 ? "(none)" : string.Join("; ", item.Sources)));
                text.AppendLine("- Conditions: " + (item.Conditions.Count == 0 ? "(none)" : string.Join("; ", item.Conditions)));
                text.AppendLine("- Presentation: " + item.Presentation);
                text.AppendLine("- Pages/lines/options: " + item.PageCount + "/" + item.LineCount + "/" + item.ChoiceCount);
                text.AppendLine("- Letter/reading: " + item.LetterStepCount + "/" + item.ReadingPageCount + " pages / " + item.ReadingLineCount + " lines");
                text.AppendLine("- Checks: " + (item.Checks.Count == 0 ? "(none)" : string.Join(", ", item.Checks)));
                text.AppendLine("- Results: " + (item.ExpectedResults.Count == 0 ? "(none)" : string.Join("; ", item.ExpectedResults)));
                AppendMessages(text, "Errors", item.Errors);
                AppendMessages(text, "Warnings", item.Warnings);
                text.AppendLine();
            }
            return text.ToString();
        }

        private static void AppendMessages(StringBuilder text, string title, IEnumerable<string> messages)
        {
            List<string> values = messages?.Where(item => !string.IsNullOrWhiteSpace(item)).ToList() ?? new List<string>();
            if (values.Count == 0) return;
            text.AppendLine("### " + title);
            foreach (string value in values) text.AppendLine("- " + value.Replace("\r", " ").Replace("\n", " "));
            text.AppendLine();
        }
    }

    public sealed class NarrativeIntegrationEventReport
    {
        public string EventId;
        public NarrativeEntryPoint Entry;
        public string Presentation;
        public int PageCount;
        public int LineCount;
        public int ChoiceCount;
        public int LetterStepCount;
        public int ReadingPageCount;
        public int ReadingLineCount;
        public readonly List<string> Sources = new List<string>();
        public readonly List<string> Conditions = new List<string>();
        public readonly List<string> Prerequisites = new List<string>();
        public readonly List<string> Checks = new List<string>();
        public readonly List<string> ExpectedResults = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool HasErrors => Errors.Count > 0;
        public string Status => HasErrors ? "blocked" : Warnings.Count > 0 ? "review" : "static_pass";
    }
}
