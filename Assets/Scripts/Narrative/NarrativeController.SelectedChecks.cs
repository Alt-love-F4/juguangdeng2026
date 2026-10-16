using System;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativeController
    {
        /// <summary>
        /// selectedCheck:choiceId=inlineCheckId;choiceId=inlineCheckId
        /// Validates every mapping, then reads only the selected branch's cached snapshot.
        /// Missing or ambiguous selection is a configuration error, never a failed check.
        /// </summary>
        private bool ReadSelectedCheckCondition(string mapping)
        {
            if (!activePresentation || activeRequest.Event == null)
                throw new InvalidOperationException("Selected check requires an active narrative presentation.");

            string selectedCheck = null;
            int selections = 0;
            var seenChoices = new HashSet<string>(StringComparer.Ordinal);
            foreach (string pair in mapping.Split(';'))
            {
                int separator = pair.IndexOf('=');
                if (separator <= 0 || separator == pair.Length - 1 || pair.LastIndexOf('=') != separator)
                    throw new InvalidOperationException("Invalid selected check mapping: " + pair);
                string choiceId = pair.Substring(0, separator);
                string checkId = pair.Substring(separator + 1);
                var choice = configuration.choices.Find(c => c != null && c.choiceId == choiceId);
                var check = configuration.abilityChecks.Find(c => c != null && c.checkId == checkId);
                if (!seenChoices.Add(choiceId) || choice == null
                    || !configuration.presentations.conversationPages.Exists(p => p != null
                        && p.presentationKey == activeRequest.Event.presentationKey && p.optionGroupKey == choice.groupKey)
                    || check == null || check.mode != NarrativeAbilityCheckMode.Inline
                    || !string.IsNullOrWhiteSpace(check.successEventId) || !string.IsNullOrWhiteSpace(check.failureEventId)
                    || !string.IsNullOrWhiteSpace(check.successResultOwnerId) || !string.IsNullOrWhiteSpace(check.failureResultOwnerId))
                    throw new InvalidOperationException("Invalid selected check reference: " + pair);
                if (selectedChoices.Contains(choiceId))
                {
                    selections++;
                    selectedCheck = checkId;
                }
            }
            if (selections != 1)
                throw new InvalidOperationException("Selected check requires exactly one selected mapped choice; found " + selections + ".");
            return EvaluateInlineAbilityCheckForPresentation(selectedCheck).passed;
        }
    }
}
