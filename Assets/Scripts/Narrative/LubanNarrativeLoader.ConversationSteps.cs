using System;
using System.Collections.Generic;
using System.Linq;

namespace Spotlight.Narrative
{
    public static partial class LubanNarrativeLoader
    {
        public static NarrativeConversationStepType ParseConversationStepType(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return NarrativeConversationStepType.Dialogue;
            foreach (NarrativeConversationStepType type in Enum.GetValues(typeof(NarrativeConversationStepType)))
                if (string.Equals(value.Trim(), type.ToString(), StringComparison.OrdinalIgnoreCase)) return type;
            throw new FormatException("Unknown ConversationPage stepType: " + value);
        }
        public static NarrativeAbilityCheckMode ParseAbilityCheckMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return NarrativeAbilityCheckMode.Branch;
            if (string.Equals(value.Trim(), "Branch", StringComparison.OrdinalIgnoreCase)) return NarrativeAbilityCheckMode.Branch;
            if (string.Equals(value.Trim(), "Inline", StringComparison.OrdinalIgnoreCase)) return NarrativeAbilityCheckMode.Inline;
            throw new FormatException("Unknown AbilityCheck mode: " + value);
        }
        public static List<string> ValidateAbilityCheckMode(NarrativeConfiguration c, NarrativeAbilityCheckDefinition check)
        {
            var errors = new List<string>();
            if (check == null) return errors;
            if (check.mode == NarrativeAbilityCheckMode.Inline)
            {
                if (new[] { check.successEventId, check.failureEventId }.Any(v => !string.IsNullOrWhiteSpace(v)))
                    errors.Add("AbilityCheck " + check.checkId + ": Inline forbids branch events.");
                if (c.setups.Any(x => x != null && x.checkId == check.checkId))
                    errors.Add("AbilityCheck " + check.checkId + ": Inline cannot be referenced by SetupParams.");
                if (c.presentations.conversationPages.Any(x => x != null && x.stepType == NarrativeConversationStepType.PassiveCheck && x.checkId == check.checkId)
                    && (c.choices.Any(x => x != null && x.checkId == check.checkId)))
                    errors.Add("AbilityCheck " + check.checkId + ": Inline cannot be used by both a Choice and a PassiveCheck page.");
            }
            else if (check.mode == NarrativeAbilityCheckMode.Branch)
            {
                if (string.IsNullOrWhiteSpace(check.successEventId) || !c.events.Any(e => e != null && e.eventId == check.successEventId))
                    errors.Add("AbilityCheck " + check.checkId + ": unknown successEventId " + check.successEventId);
                if (string.IsNullOrWhiteSpace(check.failureEventId) || !c.events.Any(e => e != null && e.eventId == check.failureEventId))
                    errors.Add("AbilityCheck " + check.checkId + ": unknown failureEventId " + check.failureEventId);
            }
            else errors.Add("AbilityCheck " + check.checkId + ": invalid mode.");
            return errors;
        }
        public static List<string> ValidateConversationSteps(NarrativeConfiguration c)
        {
            var errors = new List<string>();
            var pages = c.presentations.conversationPages;
            foreach (var group in pages.GroupBy(p => p.presentationKey))
                if (group.Select(p => p.seq).Distinct().Count() != group.Count())
                    errors.Add("Conversation " + group.Key + ": duplicate step sequence.");
            foreach (var page in pages)
            {
                string label = "ConversationPage " + page.pageId + ": ";
                var lines = c.presentations.conversationLines.FindAll(x => x != null && x.pageId == page.pageId);
                if (page.seq < 1) errors.Add(label + "sequence must be positive.");
                if (!string.IsNullOrWhiteSpace(page.nextPageId))
                {
                    var next = pages.Find(p => p != null && p.pageId == page.nextPageId);
                    if (next == null || next.presentationKey != page.presentationKey) errors.Add(label + "nextPageId must stay inside the same presentation.");
                    if (page.stepType == NarrativeConversationStepType.End) errors.Add(label + "End cannot have nextPageId.");
                }
                if (!string.IsNullOrWhiteSpace(page.pageCondKeys))
                    foreach (string guard in page.pageCondKeys.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string guardError = NarrativePresentationGuards.Validate(c, guard);
                        if (guardError != null) errors.Add(label + guardError);
                    }
                if (page.stepType == NarrativeConversationStepType.End || page.stepType == NarrativeConversationStepType.Gameplay)
                {
                    if (lines.Count > 0 || !string.IsNullOrWhiteSpace(page.optionGroupKey)) errors.Add(label + "End/Gameplay cannot contain dialogue or choices.");
                }
                if (page.stepType == NarrativeConversationStepType.Gameplay)
                {
                    if (string.IsNullOrWhiteSpace(page.gameplayKey)) errors.Add(label + "Gameplay requires gameplayKey.");
                }
                else if (!string.IsNullOrWhiteSpace(page.gameplayKey)) errors.Add(label + "gameplayKey requires Gameplay.");
                bool letter = page.stepType == NarrativeConversationStepType.LetterReveal || page.stepType == NarrativeConversationStepType.LetterRead;
                if (!Enum.IsDefined(typeof(NarrativeConversationStepType), page.stepType)) errors.Add(label + "invalid stepType.");
                if (page.stepType == NarrativeConversationStepType.PassiveCheck)
                {
                    var check = c.abilityChecks.Find(x => x != null && x.checkId == page.checkId);
                    if (check == null || check.mode != NarrativeAbilityCheckMode.Inline) errors.Add(label + "requires an Inline checkId.");
                    if (!string.IsNullOrWhiteSpace(page.optionGroupKey)) errors.Add(label + "PassiveCheck cannot offer choices.");
                    if (lines.Count == 0) errors.Add(label + "PassiveCheck requires success content.");
                }
                else if (!string.IsNullOrWhiteSpace(page.checkId)) errors.Add(label + "checkId requires PassiveCheck.");
                if (!letter)
                {
                    if (!string.IsNullOrWhiteSpace(page.readingKey)) errors.Add(label + "readingKey requires a letter step.");
                    continue;
                }
                if (lines.Count > 0 || !string.IsNullOrWhiteSpace(page.optionGroupKey)) errors.Add(label + "letter steps cannot contain dialogue or choices.");
                var index = c.presentations.index.Find(x => x != null && x.presentationKey == page.readingKey);
                if (index == null || !string.Equals(index.presentationClass, "Reading", StringComparison.OrdinalIgnoreCase)) errors.Add(label + "readingKey must reference a Reading presentation.");
                var readingPages = c.presentations.readingPages.FindAll(x => x != null && x.presentationKey == page.readingKey);
                if (readingPages.Count != 1) { errors.Add(label + "letter requires exactly one reading page."); continue; }
                var readingPage = readingPages[0];
                if (!string.IsNullOrWhiteSpace(readingPage.titleKey) && !c.texts.Any(x => x.key == readingPage.titleKey)) errors.Add(label + "unknown reading title text.");
                var readingLines = c.presentations.readingLines.FindAll(x => x != null && x.pageId == readingPage.pageId);
                if (readingLines.Count == 0) errors.Add(label + "letter requires reading lines.");
                if (readingLines.Any(x => x.seq < 1) || readingLines.Select(x => x.seq).Distinct().Count() != readingLines.Count) errors.Add(label + "invalid reading line sequence.");
                foreach (var line in readingLines)
                    if (!c.texts.Any(x => x.key == line.textKey)) errors.Add(label + "unknown reading text " + line.textKey);
            }
            return errors;
        }
    }
}
