using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// Read-only dialogue snapshots for the presentation debug menu. Never creates a runtime,
    /// evaluates page/choice conditions, or binds narrative results and event callbacks.
    /// </summary>
    public static class PresentationDebugDialogueSource
    {
        public sealed class Entry
        {
            public string Id, Label;
        }

        private const string OpeningPrefix = "opening:";
        private const string ConversationPrefix = "conversation:";
        private const string PreviewRequirementHint = "仅预览，不结算；忽略选项条件，不执行 Result/Event。";

        public static List<Entry> List(NarrativeConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            var entries = new List<Entry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var opening in configuration.openingPresentations ?? Enumerable.Empty<NarrativeOpeningPresentationDefinition>())
            {
                if (opening == null || string.IsNullOrWhiteSpace(opening.presentationId)) continue;
                int count = configuration.openingLines?.Count(line => line != null
                    && string.Equals(line.presentationId, opening.presentationId, StringComparison.Ordinal)) ?? 0;
                string id = OpeningPrefix + opening.presentationId;
                if (count == 0 || !ids.Add(id)) continue;
                entries.Add(new Entry
                {
                    Id = id,
                    Label = "Opening | " + opening.presentationId + " | "
                        + (FindText(configuration, opening.titleKey) ?? opening.titleKey ?? opening.presentationId)
                        + " | 1 页 / " + count + " 句"
                });
            }
            foreach (var conversation in configuration.presentations?.index ?? Enumerable.Empty<NarrativePresentationIndexDefinition>())
            {
                if (!IsConversation(conversation) || string.IsNullOrWhiteSpace(conversation.presentationKey)) continue;
                var pages = GetPages(configuration, conversation.presentationKey);
                var pageIds = new HashSet<string>(pages.Select(page => page.pageId), StringComparer.Ordinal);
                int count = configuration.presentations.conversationLines.Count(line => line != null && pageIds.Contains(line.pageId));
                string id = ConversationPrefix + conversation.presentationKey;
                if (count == 0 || !ids.Add(id)) continue;
                entries.Add(new Entry
                {
                    Id = id,
                    Label = "Conversation | " + conversation.presentationKey + " | " + ConversationTitle(configuration, conversation)
                        + " | " + pages.Count + " 页 / " + count + " 句"
                });
            }
            return entries.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToList();
        }

        public static List<DialogueScript> Create(NarrativeConfiguration configuration, string id)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A debug dialogue id is required.", nameof(id));
            if (id.StartsWith(OpeningPrefix, StringComparison.Ordinal))
            {
                string key = id.Substring(OpeningPrefix.Length);
                // Do not let an empty debug id silently select the formal adapter's default opening.
                if (string.IsNullOrWhiteSpace(key)) throw Error(id, "Missing opening presentation id.");
                // The shared opening adapter owns validation and separator handling for both game
                // and debug callers. Do not silently repair content only in the preview path.
                if (!NarrativePresentationDataAdapter.TryCreateOpeningDialogue(configuration, key, out var script, out string error))
                    throw Error(id, error);
                return new List<DialogueScript> { script };
            }
            if (!id.StartsWith(ConversationPrefix, StringComparison.Ordinal))
                throw Error(id, "Unknown dialogue source; expected opening:<id> or conversation:<key>.");

            string presentationKey = id.Substring(ConversationPrefix.Length);
            var presentation = configuration.presentations?.index.FirstOrDefault(item => IsConversation(item)
                && string.Equals(item.presentationKey, presentationKey, StringComparison.Ordinal));
            if (presentation == null || string.IsNullOrWhiteSpace(presentationKey))
                throw Error(id, "Unknown Conversation presentation: " + presentationKey);
            var pages = GetPages(configuration, presentationKey);
            if (pages.Count == 0) throw Error(id, "Conversation has no pages.");

            string title = ConversationTitle(configuration, presentation);
            var scripts = new List<DialogueScript>(pages.Count);
            foreach (var page in pages)
            {
                string context = id + "/page:" + page.pageId;
                if (string.IsNullOrWhiteSpace(page.pageId)) throw Error(context, "Page id is empty.");
                var sourceLines = configuration.presentations.conversationLines
                    .Where(line => line != null && string.Equals(line.pageId, page.pageId, StringComparison.Ordinal))
                    .OrderBy(line => line.seq).ToList();
                if (sourceLines.Count == 0) throw Error(context, "Conversation page has no lines.");

                var lines = new List<DialogueLine>(sourceLines.Count);
                foreach (var line in sourceLines)
                {
                    string lineContext = context + "/line:" + line.lineId;
                    string text = RequireText(configuration, line.textKey, lineContext);
                    if (string.IsNullOrWhiteSpace(line.speakerKey))
                    {
                        lines.Add(new DialogueLine(DialogueSpeaker.Narrator, text,
                            accentFxKey: line.accentFxKey, accentTargetKey: line.accentTargetKey,
                            sfxKey: line.sfxKey, voiceKey: line.voiceKey));
                        continue;
                    }
                    var speaker = configuration.objects?.FirstOrDefault(item => item != null
                        && string.Equals(item.objId, line.speakerKey, StringComparison.Ordinal));
                    if (speaker == null) throw Error(lineContext, "Missing speaker Object: " + line.speakerKey);
                    string displayName = RequireText(configuration, speaker.nameText, lineContext + "/speaker:" + line.speakerKey);
                    string portraitPath = string.IsNullOrWhiteSpace(speaker.portraitPath) ? speaker.iconPath : speaker.portraitPath;
                    // Object currently supplies one dialogue portrait, not emotion-specific paths.
                    // Use the formal PortraitFor(emote) convention without reading or changing the global catalog.
                    var profile = new CharacterProfile
                    {
                        portrait = string.IsNullOrWhiteSpace(portraitPath) ? null : Resources.Load<Sprite>(portraitPath)
                    };
                    var character = new DialogueCharacter(line.speakerKey, displayName, profile.PortraitFor(line.emote));
                    bool isLeft = string.Equals(line.side, "left", StringComparison.OrdinalIgnoreCase);
                    lines.Add(new DialogueLine(isLeft ? DialogueSpeaker.Left : DialogueSpeaker.Right, text,
                        replaceLeft: isLeft, leftCharacter: isLeft ? character : null,
                        replaceRight: !isLeft, rightCharacter: !isLeft ? character : null,
                        accentFxKey: line.accentFxKey, accentTargetKey: line.accentTargetKey,
                        sfxKey: line.sfxKey, voiceKey: line.voiceKey));
                }

                var options = new List<DialogueOption>();
                if (!string.IsNullOrWhiteSpace(page.optionGroupKey))
                {
                    var optionIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var choice in (configuration.choices ?? Enumerable.Empty<NarrativeChoiceDefinition>())
                                 .Where(item => item != null && string.Equals(item.groupKey, page.optionGroupKey, StringComparison.Ordinal))
                                 .OrderBy(item => item.seq))
                    {
                        string choiceContext = context + "/choice:" + choice.choiceId;
                        if (string.IsNullOrWhiteSpace(choice.choiceId) || !optionIds.Add(choice.choiceId))
                            throw Error(choiceContext, "Choice id is empty or duplicated in group: " + page.optionGroupKey);
                        options.Add(new DialogueOption(choice.choiceId, RequireText(configuration, choice.textKey, choiceContext),
                            enabled: true, requirementHint: PreviewRequirementHint));
                    }
                }
                // Explicit blanks prevent DialoguePanel from falling back to legacy prefab characters.
                // Each spoken line replaces its side on demand, so later speakers do not appear early.
                scripts.Add(new DialogueScript(lines, new DialogueCharacter("", null), new DialogueCharacter("", null),
                    title, string.IsNullOrWhiteSpace(page.bgOverride) ? null : Resources.Load<Texture>(page.bgOverride), options));
            }
            return scripts;
        }

        private static bool IsConversation(NarrativePresentationIndexDefinition item)
        {
            return item != null && string.Equals(item.presentationClass, "Conversation", StringComparison.OrdinalIgnoreCase);
        }

        private static List<NarrativeConversationPageDefinition> GetPages(NarrativeConfiguration configuration, string key)
        {
            return (configuration.presentations?.conversationPages ?? Enumerable.Empty<NarrativeConversationPageDefinition>())
                .Where(page => page != null && string.Equals(page.presentationKey, key, StringComparison.Ordinal))
                .OrderBy(page => page.seq).ToList();
        }

        private static string ConversationTitle(NarrativeConfiguration configuration, NarrativePresentationIndexDefinition presentation)
        {
            // A presentation can be shared by events; choose a title deterministically, without executing the event.
            foreach (var definition in (configuration.events ?? Enumerable.Empty<NarrativeEventDefinition>())
                         .Where(item => item != null && string.Equals(item.presentationKey, presentation.presentationKey, StringComparison.Ordinal))
                         .OrderBy(item => item.eventId, StringComparer.Ordinal))
            {
                string title = FindText(configuration, definition.titleText);
                if (!string.IsNullOrWhiteSpace(title)) return title;
            }
            return string.IsNullOrWhiteSpace(presentation.logTag) ? presentation.presentationKey : presentation.logTag;
        }

        private static string FindText(NarrativeConfiguration configuration, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            string text = configuration.texts?.FirstOrDefault(item => item != null
                && string.Equals(item.key, key, StringComparison.Ordinal))?.content;
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static string RequireText(NarrativeConfiguration configuration, string key, string context)
        {
            return FindText(configuration, key)
                ?? throw Error(context, "Missing or empty textKey: " + (string.IsNullOrWhiteSpace(key) ? "<empty>" : key));
        }

        private static InvalidOperationException Error(string id, string message)
        {
            return new InvalidOperationException("Debug dialogue [" + id + "]: " + message);
        }
    }
}
