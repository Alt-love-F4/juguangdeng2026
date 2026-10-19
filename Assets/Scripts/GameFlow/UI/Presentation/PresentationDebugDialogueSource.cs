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

        public sealed class CharacterGalleryEntry
        {
            public string CharacterId, DisplayName, PortraitPath, AvatarPath, ScenePortraitPath;
            public bool IsNpc, IsOpeningCenter, UsesAvatarFallback;
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

        public static List<CharacterGalleryEntry> ListCharacterGallery(NarrativeConfiguration configuration, string id)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A debug dialogue id is required.", nameof(id));

            if (id.StartsWith(OpeningPrefix, StringComparison.Ordinal))
            {
                string key = id.Substring(OpeningPrefix.Length);
                var opening = configuration.openingPresentations?.FirstOrDefault(item => item != null
                    && string.Equals(item.presentationId, key, StringComparison.Ordinal));
                if (opening == null || string.IsNullOrWhiteSpace(key)) throw Error(id, "Unknown opening presentation.");
                var people = new List<CharacterGalleryEntry>
                {
                    GalleryObject(configuration, "player", id),
                    GalleryObject(configuration, "adele", id)
                };
                if (!string.IsNullOrWhiteSpace(opening.centerNameKey))
                {
                    people.Add(new CharacterGalleryEntry
                    {
                        CharacterId = "opening:" + key + ":center",
                        DisplayName = FindText(configuration, opening.centerNameKey) ?? opening.centerNameKey,
                        IsOpeningCenter = true
                    });
                }
                return people;
            }

            if (!id.StartsWith(ConversationPrefix, StringComparison.Ordinal))
                throw Error(id, "Unknown dialogue source; expected opening:<id> or conversation:<key>.");

            string presentationKey = id.Substring(ConversationPrefix.Length);
            var presentation = configuration.presentations?.index.FirstOrDefault(item => IsConversation(item)
                && string.Equals(item.presentationKey, presentationKey, StringComparison.Ordinal));
            if (presentation == null || string.IsNullOrWhiteSpace(presentationKey))
                throw Error(id, "Unknown Conversation presentation: " + presentationKey);

            var characterIds = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pages = GetPages(configuration, presentationKey);
            var pageIds = new HashSet<string>(pages.Select(page => page.pageId), StringComparer.Ordinal);
            foreach (var line in (configuration.presentations?.conversationLines ?? Enumerable.Empty<NarrativeConversationLineDefinition>())
                         .Where(item => item != null && pageIds.Contains(item.pageId)).OrderBy(item =>
                             pages.FindIndex(page => string.Equals(page.pageId, item.pageId, StringComparison.Ordinal)))
                         .ThenBy(item => item.seq))
                AddCharacterId(line.speakerKey, characterIds, seen);

            var eventIds = new HashSet<string>((configuration.events ?? Enumerable.Empty<NarrativeEventDefinition>())
                .Where(item => item != null && string.Equals(item.presentationKey, presentationKey, StringComparison.Ordinal))
                .Select(item => item.eventId), StringComparer.Ordinal);
            foreach (var result in (configuration.results ?? Enumerable.Empty<NarrativeResultDefinition>())
                         .Where(item => item != null && item.resultType == NarrativeResultType.SpawnNpc && eventIds.Contains(item.eventId))
                         .OrderBy(item => item.eventId, StringComparer.Ordinal).ThenBy(item => item.seq))
                AddCharacterId(StripObjectPrefix(result.target), characterIds, seen);

            var entries = new List<CharacterGalleryEntry>(characterIds.Count);
            foreach (string characterId in characterIds)
            {
                CharacterGalleryEntry entry = GalleryObject(configuration, characterId, id);
                if (entry != null) entries.Add(entry);
            }
            if (entries.Count == 0) throw Error(id, "No character speakers or spawned NPCs were found.");
            return entries;
        }

        public static DialogueScript CreateCharacterGallery(NarrativeConfiguration configuration, string id)
        {
            List<CharacterGalleryEntry> people = ListCharacterGallery(configuration, id);
            DialogueVisualsAsset openingVisuals = null;
            if (id.StartsWith(OpeningPrefix, StringComparison.Ordinal))
            {
                string key = id.Substring(OpeningPrefix.Length);
                var opening = configuration.openingPresentations.First(item => item != null && item.presentationId == key);
                openingVisuals = string.IsNullOrWhiteSpace(opening.visualsResource)
                    ? null : Resources.Load<DialogueVisualsAsset>(opening.visualsResource);
                if (openingVisuals == null) throw Error(id, "Missing opening visuals resource: " + opening.visualsResource);
            }

            string title = GalleryTitle(configuration, id);
            var lines = new List<DialogueLine>(people.Count);
            for (int i = 0; i < people.Count; i++)
            {
                CharacterGalleryEntry person = people[i];
                Sprite portrait = person.IsOpeningCenter
                    ? openingVisuals.CenterPortrait
                    : openingVisuals != null && string.Equals(person.CharacterId, "player", StringComparison.Ordinal)
                        ? openingVisuals.LeftPortrait
                        : openingVisuals != null && string.Equals(person.CharacterId, "adele", StringComparison.Ordinal)
                            ? openingVisuals.RightPortrait
                            : LoadGallerySprite(string.IsNullOrWhiteSpace(person.PortraitPath) ? person.AvatarPath : person.PortraitPath, id, person.CharacterId);
                Sprite scenePortrait = person.IsNpc && !string.IsNullOrWhiteSpace(person.ScenePortraitPath)
                    ? LoadGallerySprite(person.ScenePortraitPath, id, person.CharacterId)
                    : null;
                bool showScenePortrait = scenePortrait != null && !ReferenceEquals(scenePortrait, portrait);
                var left = new DialogueCharacter(person.CharacterId, person.DisplayName, portrait);
                var right = showScenePortrait
                    ? new DialogueCharacter(person.CharacterId + "#scene-preview", person.DisplayName + " · 场景", scenePortrait)
                    : null;

                string artDescription = person.UsesAvatarFallback
                    ? "当前未配置对话立绘，左侧显示头像素材。"
                    : portrait == null ? "尚未配置可用立绘。" : "左侧显示对话立绘。";
                if (person.IsNpc)
                    artDescription += showScenePortrait ? "右侧显示 NPC 在对话场景中的形象。"
                        : person.ScenePortraitPath == person.PortraitPath ? "场景形象与对话立绘共用素材。"
                        : "未配置独立 NPC 场景形象。";
                artDescription += "点击继续查看下一位。";
                lines.Add(new DialogueLine(DialogueSpeaker.Left,
                    (i + 1) + " / " + people.Count + "　" + person.DisplayName + "\n" + artDescription,
                    speakerNameOverride: person.DisplayName, replaceLeft: true, leftCharacter: left,
                    replaceRight: true, rightCharacter: right));
            }

            return new DialogueScript(lines, new DialogueCharacter("", null), new DialogueCharacter("", null),
                title: title);
        }

        private static CharacterGalleryEntry GalleryObject(NarrativeConfiguration configuration, string id, string context)
        {
            NarrativeObjectDefinition character = configuration.objects?.FirstOrDefault(item => item != null
                && string.Equals(item.objId, id, StringComparison.Ordinal));
            if (character == null) throw Error(context, "Missing speaker Object: " + id);
            if (character.objType == NarrativeObjectType.World) return null;
            string name = FindText(configuration, character.nameText);
            return new CharacterGalleryEntry
            {
                CharacterId = character.objId,
                DisplayName = string.IsNullOrWhiteSpace(name) ? character.objId : name,
                PortraitPath = string.IsNullOrWhiteSpace(character.portraitPath) ? character.iconPath : character.portraitPath,
                AvatarPath = string.IsNullOrWhiteSpace(character.avtarPath) ? character.iconPath : character.avtarPath,
                ScenePortraitPath = character.portraitIndoorPath,
                IsNpc = character.objType == NarrativeObjectType.Npc,
                UsesAvatarFallback = string.IsNullOrWhiteSpace(character.portraitPath)
                    && (!string.IsNullOrWhiteSpace(character.avtarPath) || !string.IsNullOrWhiteSpace(character.iconPath))
            };
        }

        private static void AddCharacterId(string id, List<string> result, HashSet<string> seen)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (seen.Add(id)) result.Add(id);
        }

        private static string StripObjectPrefix(string value)
        {
            const string prefix = "obj:";
            return !string.IsNullOrWhiteSpace(value) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? value.Substring(prefix.Length).Trim() : value;
        }

        private static Sprite LoadGallerySprite(string path, string presentationId, string characterId)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null) throw Error(presentationId, "Missing character Sprite for " + characterId + ": " + path);
            return sprite;
        }

        private static string GalleryTitle(NarrativeConfiguration configuration, string id)
        {
            if (id.StartsWith(OpeningPrefix, StringComparison.Ordinal))
            {
                var opening = configuration.openingPresentations.First(item => item != null
                    && item.presentationId == id.Substring(OpeningPrefix.Length));
                return "人物巡览 · " + (FindText(configuration, opening.titleKey) ?? opening.presentationId);
            }
            string key = id.Substring(ConversationPrefix.Length);
            var presentation = configuration.presentations.index.First(item => IsConversation(item)
                && item.presentationKey == key);
            return "人物巡览 · " + ConversationTitle(configuration, presentation);
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
