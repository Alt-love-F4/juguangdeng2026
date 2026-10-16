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
                if (new[] { check.successEventId, check.failureEventId, check.successResultOwnerId, check.failureResultOwnerId }.Any(v => !string.IsNullOrWhiteSpace(v)))
                    errors.Add("AbilityCheck " + check.checkId + ": Inline forbids branch events and result owners.");
                if (c.choices.Any(x => x != null && x.checkId == check.checkId) || c.setups.Any(x => x != null && x.checkId == check.checkId))
                    errors.Add("AbilityCheck " + check.checkId + ": Inline is only supported by a PassiveCheck conversation step.");
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
            foreach (var line in c.presentations.conversationLines)
                foreach (string key in new[] { line.stageLeftKey, line.stageRightKey, line.stageCenterKey })
                    if (!string.IsNullOrWhiteSpace(key) && key != "-" && !c.objects.Any(o => o.objId == key))
                        errors.Add("ConversationLine " + line.lineId + ": unknown stage Object " + key);
            foreach (var group in pages.GroupBy(p => p.presentationKey))
                if (group.Select(p => p.seq).Distinct().Count() != group.Count())
                    errors.Add("Conversation " + group.Key + ": duplicate step sequence.");
            foreach (var page in pages)
            {
                string label = "ConversationPage " + page.pageId + ": ";
                var lines = c.presentations.conversationLines.FindAll(x => x != null && x.pageId == page.pageId);
                if (page.seq < 1) errors.Add(label + "sequence must be positive.");
                bool letter = page.stepType == NarrativeConversationStepType.LetterReveal || page.stepType == NarrativeConversationStepType.LetterRead;
                if (!Enum.IsDefined(typeof(NarrativeConversationStepType), page.stepType)) errors.Add(label + "invalid stepType.");
                if (page.stepType == NarrativeConversationStepType.WaitBusiness || page.stepType == NarrativeConversationStepType.WaitNight
                    || page.stepType == NarrativeConversationStepType.WaitService)
                {
                    if (lines.Count > 0 || !string.IsNullOrWhiteSpace(page.optionGroupKey)) errors.Add(label + "checkpoint cannot contain dialogue or choices.");
                    if (!string.IsNullOrWhiteSpace(page.checkId) || !string.IsNullOrWhiteSpace(page.readingKey)) errors.Add(label + "checkpoint cannot reference a check or reading.");
                    if (!pages.Any(p => p.presentationKey == page.presentationKey && p.seq > page.seq && !string.IsNullOrWhiteSpace(p.pageId))) errors.Add(label + "checkpoint requires a following page.");
                    if (page.stepType == NarrativeConversationStepType.WaitService)
                    {
                        var owners = c.events.Where(e => e != null && e.presentationKey == page.presentationKey).ToArray();
                        if (owners.Length == 0 || owners.Any(e => !c.cafeVisitHooks.Any(h => h != null && h.eventId == e.eventId
                            && (h.stage == "arrived" && h.trigger == "auto" || h.stage == "before_served" && h.trigger == "click")
                            && c.cafeVisits.Any(v => v != null && v.visitKey == h.visitKey && v.requireRealProduction
                                && !string.IsNullOrWhiteSpace(v.orderProductId)))))
                            errors.Add(label + "WaitService requires an arrived/auto or before_served/click hook with an explicit real-production product.");
                    }
                    continue;
                }
                if (page.stepType == NarrativeConversationStepType.LetterHide)
                {
                    if (lines.Count > 0 || !string.IsNullOrWhiteSpace(page.optionGroupKey)
                        || !string.IsNullOrWhiteSpace(page.checkId) || !string.IsNullOrWhiteSpace(page.readingKey))
                        errors.Add(label + "LetterHide cannot contain dialogue, choices, a check or reading.");
                    continue;
                }
                if (page.stepType == NarrativeConversationStepType.FadeOut)
                {
                    if (lines.Count > 0 || !string.IsNullOrWhiteSpace(page.optionGroupKey)) errors.Add(label + "FadeOut cannot contain dialogue or choices.");
                    if (!string.IsNullOrWhiteSpace(page.checkId) || !string.IsNullOrWhiteSpace(page.readingKey)) errors.Add(label + "FadeOut cannot reference a check or reading.");
                    if (pages.Any(p => p.presentationKey == page.presentationKey && p.seq > page.seq)) errors.Add(label + "FadeOut must be the final step.");
                    continue;
                }
                if (page.stepType == NarrativeConversationStepType.PassiveCheck)
                {
                    var check = c.abilityChecks.Find(x => x != null && x.checkId == page.checkId);
                    if (check == null || check.mode != NarrativeAbilityCheckMode.Inline) errors.Add(label + "requires an Inline checkId.");
                    if (!string.IsNullOrWhiteSpace(page.optionGroupKey)) errors.Add(label + "PassiveCheck cannot offer choices.");
                    if (lines.Count == 0) errors.Add(label + "PassiveCheck requires success content.");
                }
                else if (!string.IsNullOrWhiteSpace(page.checkId)) errors.Add(label + "checkId requires PassiveCheck.");
                if (page.stepType == NarrativeConversationStepType.Reading)
                {
                    ValidateOrdinaryReadingStep(c, page, lines.Count, errors);
                    continue;
                }
                if (!letter)
                {
                    if (!string.IsNullOrWhiteSpace(page.readingKey)) errors.Add(label + "readingKey requires a letter or Reading step.");
                    continue;
                }
                if (lines.Count > 0 || !string.IsNullOrWhiteSpace(page.optionGroupKey)) errors.Add(label + "letter steps cannot contain dialogue or choices.");
                // A closed prop needs no authored letter body. LetterRead remains strict.
                if (page.stepType == NarrativeConversationStepType.LetterReveal && string.IsNullOrWhiteSpace(page.readingKey)) continue;
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

        private static void ValidateOrdinaryReadingStep(NarrativeConfiguration c, NarrativeConversationPageDefinition step,
            int dialogueLineCount, List<string> errors)
        {
            string label = "ConversationPage " + step.pageId + ": ";
            if (dialogueLineCount > 0 || !string.IsNullOrWhiteSpace(step.optionGroupKey))
                errors.Add(label + "Reading cannot contain dialogue or choices.");
            var index = c.presentations.index.Find(x => x != null && x.presentationKey == step.readingKey);
            if (string.IsNullOrWhiteSpace(step.readingKey) || index == null
                || !string.Equals(index.presentationClass, "Reading", StringComparison.OrdinalIgnoreCase))
                errors.Add(label + "readingKey must reference a Reading presentation.");
            var pages = c.presentations.readingPages.FindAll(x => x != null && x.presentationKey == step.readingKey);
            if (pages.Count != 1) { errors.Add(label + "Reading requires exactly one reading page."); return; }
            var page = pages[0];
            if (!string.IsNullOrWhiteSpace(page.titleKey) && !c.texts.Any(x => x.key == page.titleKey))
                errors.Add(label + "unknown reading title text.");
            var lines = c.presentations.readingLines.FindAll(x => x != null && x.pageId == page.pageId);
            if (lines.Count == 0) errors.Add(label + "Reading requires reading lines.");
            if (lines.Any(x => x.seq < 1) || lines.Select(x => x.seq).Distinct().Count() != lines.Count)
                errors.Add(label + "invalid reading line sequence.");
            foreach (var line in lines)
                if (!c.texts.Any(x => x.key == line.textKey)) errors.Add(label + "unknown reading text " + line.textKey);
        }
    }
}
