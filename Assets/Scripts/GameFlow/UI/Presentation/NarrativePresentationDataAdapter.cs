using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// NCS -> 游戏内演出数据的单向适配层。
    /// NCS 负责表结构和文本值，DialoguePanel/FirstDayLetterPanel 只负责表现。
    /// </summary>
    public static class NarrativePresentationDataAdapter
    {
        public const string DefaultOpeningPresentationId = "first_day_cafe";
        public const string DefaultFirstDayLetterBodyKey = "opening.letter.body";
        public const string DefaultFirstDayLetterSignatureKey = "opening.letter.signature";

        public static bool TryCreateOpeningDialogue(
            NarrativeRuntime runtime,
            string presentationId,
            out DialogueScript script,
            out string error)
        {
            NarrativeConfiguration configuration = runtime != null && runtime.IsReady ? runtime.Configuration : null;
            if (configuration == null)
            {
                script = null;
                error = "NCS runtime is not ready.";
                return false;
            }
            return TryCreateOpeningDialogue(configuration, runtime.ResolveText, presentationId, out script, out error);
        }

        public static bool TryCreateOpeningDialogue(
            NarrativeConfiguration configuration,
            string presentationId,
            out DialogueScript script,
            out string error)
        {
            return TryCreateOpeningDialogue(configuration, null, presentationId, out script, out error);
        }

        public static bool TryCreateOpeningDialogue(
            NarrativeConfiguration configuration,
            Func<string, string> resolver,
            string presentationId,
            out DialogueScript script,
            out string error)
        {
            script = null;
            error = null;
            if (configuration == null)
            {
                error = "NCS configuration is null.";
                return false;
            }

            presentationId = string.IsNullOrWhiteSpace(presentationId)
                ? DefaultOpeningPresentationId : presentationId;
            NarrativeOpeningPresentationDefinition presentation = configuration.openingPresentations?.FirstOrDefault(
                item => item != null && string.Equals(item.presentationId, presentationId, StringComparison.Ordinal));
            if (presentation == null)
            {
                error = "Missing NCS opening presentation: " + presentationId;
                return false;
            }

            DialogueVisualsAsset visuals = string.IsNullOrWhiteSpace(presentation.visualsResource)
                ? null : Resources.Load<DialogueVisualsAsset>(presentation.visualsResource);
            if (visuals == null)
            {
                error = "Missing opening visuals resource: " + presentation.visualsResource;
                return false;
            }

            if (!TryResolveText(configuration, resolver, presentation.leftNameKey, out string leftName)
                || !TryResolveText(configuration, resolver, presentation.rightNameKey, out string rightName)
                || !TryResolveText(configuration, resolver, presentation.titleKey, out string title))
            {
                error = "Missing NCS title/character name text for opening: " + presentationId;
                return false;
            }
            DialogueCharacter left = CreateOpeningCharacter(configuration, presentation.leftNameKey, leftName, "player");
            DialogueCharacter right = CreateOpeningCharacter(configuration, presentation.rightNameKey, rightName, "adele");
            if (left == null || right == null)
            {
                error = "Missing opening character configuration: " + presentationId;
                return false;
            }
            bool ambient = presentationId.StartsWith("cafe.ambient.", StringComparison.Ordinal);
            bool hasTemplateTokens = false;
            List<DialogueLine> lines = new List<DialogueLine>();
            IEnumerable<NarrativeOpeningLineDefinition> openingLines = configuration.openingLines ?? new List<NarrativeOpeningLineDefinition>();
            foreach (NarrativeOpeningLineDefinition line in openingLines
                         .Where(item => item != null && string.Equals(item.presentationId, presentationId, StringComparison.Ordinal))
                         .OrderBy(item => item.seq))
            {
                if (!TryParseSpeaker(line.speaker, out DialogueSpeaker speaker))
                {
                    error = "Invalid NCS opening speaker: " + line.speaker;
                    return false;
                }
                if (!TryParseGesture(line.gesture, out DialogueGesture gesture))
                {
                    error = "Invalid NCS opening gesture: " + line.gesture;
                    return false;
                }
                // Authored blank text rows are separators, not playable beats. Apply the same policy
                // in game and in the debug room; a missing key remains an error below.
                NarrativeTextDefinition sourceText = configuration.texts?.FirstOrDefault(item => item != null && item.key == line.textKey);
                if (sourceText != null && string.IsNullOrWhiteSpace(sourceText.content)) continue;
                if (!TryResolveText(configuration, resolver, line.textKey, out string text))
                {
                    error = "Missing NCS opening text key: " + line.textKey;
                    return false;
                }
                if (float.IsNaN(line.autoAdvanceSeconds) || float.IsInfinity(line.autoAdvanceSeconds) || line.autoAdvanceSeconds < 0f)
                {
                    error = "Invalid NCS opening autoAdvanceSeconds: " + line.lineId;
                    return false;
                }
                var source = new DialogueSourceLocation("OpeningPresentation", null, presentationId, line.lineId, line.seq);
                if (ambient)
                {
                    // Mirror CafeDialoguePlayback's separate speaker beats without depending on
                    // FreeCafe.Unity (which already depends on this assembly).
                    string[] beats = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    string[] authoredBeats = sourceText?.content?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int beatIndex = 0; beatIndex < beats.Length; beatIndex++)
                    {
                        string beat = beats[beatIndex].Trim();
                        int separator = beat.IndexOf('：');
                        string name = separator < 0 ? null : beat.Substring(0, separator).Trim();
                        // Prefer the authored token to a runtime-resolved prefix: a player's chosen
                        // name can equal an NPC's name and must not change the actor's identity.
                        string authored = authoredBeats != null && beatIndex < authoredBeats.Length ? authoredBeats[beatIndex].Trim() : null;
                        if (authored != null && authored.IndexOf('：') >= 0
                            && NarrativeTextSubstitution.PlayerName(authored.Substring(0, authored.IndexOf('：')), "player") == "player")
                            name = "{playername}";
                        if (name == null)
                        {
                            lines.Add(new DialogueLine(DialogueSpeaker.Narrator, beat, gesture: gesture,
                                autoAdvanceSeconds: line.autoAdvanceSeconds, source: source));
                            continue;
                        }
                        DialogueCharacter actor = ResolveAmbientCharacter(configuration, resolver, name, leftName);
                        if (actor == null)
                        {
                            error = "Unknown or ambiguous NCS cafe speaker: " + name + " (" + line.lineId + ")";
                            return false;
                        }
                        beat = NarrativeTextSubstitution.PlayerName(beat.Substring(separator + 1).TrimStart(), leftName);
                        if (string.IsNullOrWhiteSpace(beat)) continue;
                        hasTemplateTokens |= beat.Contains("{饮品名}") || beat.Contains("{桌号}")
                            || beat.IndexOf("drinkname", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool isLeft = actor.CharacterKey == "player" || actor.CharacterKey == "adele";
                        lines.Add(new DialogueLine(isLeft ? DialogueSpeaker.Left : DialogueSpeaker.Right, beat,
                            gesture: gesture, autoAdvanceSeconds: line.autoAdvanceSeconds,
                            replaceLeft: isLeft, leftCharacter: isLeft ? actor : null,
                            replaceRight: !isLeft, rightCharacter: isLeft ? null : actor, source: source));
                    }
                }
                else
                    lines.Add(new DialogueLine(speaker, text, gesture: gesture, autoAdvanceSeconds: line.autoAdvanceSeconds, source: source));
            }

            if (lines.Count == 0)
            {
                error = "NCS opening presentation has no lines: " + presentationId;
                return false;
            }

            DialogueCharacter center = null;
            if (!string.IsNullOrEmpty(presentation.centerNameKey))
            {
                if (!TryResolveText(configuration, resolver, presentation.centerNameKey, out string centerName))
                {
                    error = "Missing NCS center name: " + presentation.centerNameKey;
                    return false;
                }
                // Opening schema has a fixed center slot but no character ID; use an explicit scoped legacy identity.
                center = new DialogueCharacter("opening:" + presentationId + ":center", centerName, visuals.CenterPortrait);
            }
            NarrativeStageVisuals.Entry background = !string.IsNullOrWhiteSpace(visuals.BackgroundKey)
                ? NarrativeStageVisuals.Resolve(visuals.BackgroundKey, presentationId)
                : string.Equals(presentation.visualsResource, "Dialogue/FirstDayIntroductionVisuals", StringComparison.Ordinal)
                    ? NarrativeStageVisuals.Resolve("cafe.day", presentationId) : null;
            script = new DialogueScript(lines,
                ambient ? new DialogueCharacter("", null) : left,
                ambient ? new DialogueCharacter("", null) : right,
                hasTemplateTokens ? title + "（模板预览）" : title, visuals.Background,
                centerCharacter: center, backgroundVisual: background);
            return true;
        }

        public static bool TryResolveFirstDayLetter(
            NarrativeRuntime runtime,
            string bodyKey,
            string signatureKey,
            FirstDayLetterContent fallback,
            out FirstDayLetterContent content,
            out string error)
        {
            NarrativeConfiguration configuration = runtime != null && runtime.IsReady ? runtime.Configuration : null;
            if (configuration == null)
            {
                content = fallback;
                error = "NCS runtime is not ready.";
                return false;
            }
            return TryResolveFirstDayLetter(configuration, runtime.ResolveText, bodyKey, signatureKey, fallback, out content, out error);
        }

        public static bool TryResolveFirstDayLetter(
            NarrativeConfiguration configuration,
            string bodyKey,
            string signatureKey,
            FirstDayLetterContent fallback,
            out FirstDayLetterContent content,
            out string error)
        {
            return TryResolveFirstDayLetter(configuration, null, bodyKey, signatureKey, fallback, out content, out error);
        }

        public static bool TryResolveFirstDayLetter(
            NarrativeConfiguration configuration,
            Func<string, string> resolver,
            string bodyKey,
            string signatureKey,
            FirstDayLetterContent fallback,
            out FirstDayLetterContent content,
            out string error)
        {
            bodyKey = string.IsNullOrWhiteSpace(bodyKey) ? DefaultFirstDayLetterBodyKey : bodyKey;
            signatureKey = string.IsNullOrWhiteSpace(signatureKey) ? DefaultFirstDayLetterSignatureKey : signatureKey;
            bool bodyFound = TryResolveText(configuration, resolver, bodyKey, out string body);
            bool signatureFound = TryResolveText(configuration, resolver, signatureKey, out string signature);
            if (!bodyFound || !signatureFound)
            {
                // Do not combine an NCS body with an unrelated draft signature (or display a raw key).
                content = fallback;
                error = "Missing NCS first-day letter value(s): " +
                    (bodyFound ? string.Empty : bodyKey) +
                    (!bodyFound && !signatureFound ? ", " : string.Empty) +
                    (signatureFound ? string.Empty : signatureKey);
                return false;
            }
            content = new FirstDayLetterContent(body, signature);
            error = null;
            return true;
        }

        private static DialogueCharacter CreateOpeningCharacter(NarrativeConfiguration configuration, string nameKey, string name, string legacyKey)
        {
            // Opening has name keys, not actor IDs. Object.nameText is the canonical association;
            // legacy slots retain their player/Adele identity when a presentation uses local aliases.
            string key = configuration.objects?.FirstOrDefault(item => item != null && item.nameText == nameKey)?.objId ?? legacyKey;
            CharacterProfile profile = NarrativeCharacterResolver.Find(key);
            return profile == null ? null : new DialogueCharacter(key, name, profile.PortraitFor(null));
        }

        private static DialogueCharacter ResolveAmbientCharacter(NarrativeConfiguration configuration,
            Func<string, string> resolver, string name, string playerName)
        {
            string key = null;
            if (NarrativeTextSubstitution.PlayerName(name, "player") == "player" || name == "我" || name == "你") key = "player";
            else if (name == "阿黛尔" || name == "艾黛尔") key = "adele";
            else if (name == "顾客" || name == "老顾客" || name == "{customerName}") key = "guest_a";
            else
            {
                var matches = configuration.objects.Where(item => item != null && !string.IsNullOrWhiteSpace(item.nameText)
                    && TryResolveText(configuration, resolver, item.nameText, out string fullName)
                    && (name == fullName || name == fullName.Split('·')[0])).ToArray();
                // Never turn an unknown or ambiguous named NPC into an arbitrary generic customer.
                if (matches.Length == 1) key = matches[0].objId;
            }
            CharacterProfile profile = string.IsNullOrEmpty(key) ? null : NarrativeCharacterResolver.Find(key);
            if (profile == null) return null;
            return new DialogueCharacter(key, key == "player" ? playerName : name, profile.PortraitFor(null));
        }

        private static bool TryResolveText(NarrativeConfiguration configuration, Func<string, string> resolver, string key, out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (resolver != null)
            {
                string resolved = resolver(key);
                if (!string.IsNullOrEmpty(resolved) && !string.Equals(resolved, key, StringComparison.Ordinal))
                {
                    value = resolved;
                    return true;
                }
            }
            NarrativeTextDefinition text = configuration?.texts?.FirstOrDefault(item =>
                item != null && string.Equals(item.key, key, StringComparison.Ordinal));
            if (text == null || string.IsNullOrEmpty(text.content)) return false;
            value = text.content;
            return true;
        }

        private static bool TryParseSpeaker(string value, out DialogueSpeaker speaker)
        {
            return Enum.TryParse(value ?? string.Empty, true, out speaker) && Enum.IsDefined(typeof(DialogueSpeaker), speaker);
        }

        private static bool TryParseGesture(string value, out DialogueGesture gesture)
        {
            if (string.IsNullOrWhiteSpace(value)) { gesture = DialogueGesture.None; return true; }
            return Enum.TryParse(value, true, out gesture) && Enum.IsDefined(typeof(DialogueGesture), gesture);
        }
    }
}
