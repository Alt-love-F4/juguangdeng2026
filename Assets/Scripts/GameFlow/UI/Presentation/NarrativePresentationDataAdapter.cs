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
                lines.Add(new DialogueLine(speaker, text, gesture: gesture, autoAdvanceSeconds: line.autoAdvanceSeconds));
            }

            if (lines.Count == 0)
            {
                error = "NCS opening presentation has no lines: " + presentationId;
                return false;
            }

            if (!TryResolveText(configuration, resolver, presentation.leftNameKey, out string leftName)
                || !TryResolveText(configuration, resolver, presentation.rightNameKey, out string rightName)
                || !TryResolveText(configuration, resolver, presentation.titleKey, out string title))
            {
                error = "Missing NCS title/character name text for opening: " + presentationId;
                return false;
            }
            script = new DialogueScript(lines,
                new DialogueCharacter(leftName, visuals.LeftPortrait),
                new DialogueCharacter(rightName, visuals.RightPortrait), title, visuals.Background);
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
