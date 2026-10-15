using System;
using System.Collections.Generic;
using Spotlight.Narrative;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    public static class NarrativeConversationEventPlayback
    {
        public static DialogueScript CreateScript(NarrativeConversationPresentationData data, string passiveHint = null)
        {
            if (data == null || data.lines.Count == 0 && data.options.Count == 0)
                throw new ArgumentException("Conversation has no lines or options.", nameof(data));
            var lines = new List<DialogueLine>(data.lines.Count);

            foreach (NarrativeConversationPresentationLine item in data.lines)
            {
                var source = new DialogueSourceLocation("Conversation", data.eventId, data.presentationKey,
                    item.lineId, item.seq, item.pageId);
                if (string.IsNullOrWhiteSpace(item.speakerKey))
                {
                    lines.Add(new DialogueLine(DialogueSpeaker.Narrator, item.text, passiveHint: lines.Count == 0 ? passiveHint : null,
                        accentFxKey: item.accentFxKey, accentTargetKey: item.accentTargetKey,
                        sfxKey: item.sfxKey, voiceKey: item.voiceKey, source: source));
                    continue;
                }
                CharacterProfile profile = NarrativeCharacterResolver.Find(item.speakerKey);
                Sprite portrait = profile != null ? profile.PortraitFor(item.emote)
                    : item.portrait != null ? item.portrait : string.IsNullOrWhiteSpace(item.portraitPath) ? null : Resources.Load<Sprite>(item.portraitPath);
                var character = new DialogueCharacter(item.speakerKey,
                    profile != null ? profile.displayName : item.speakerName, portrait,
                    portraitSpecified: !string.IsNullOrWhiteSpace(item.emote));
                bool isLeft = string.Equals(item.side, "left", StringComparison.OrdinalIgnoreCase);
                bool isCenter = string.Equals(item.side, "center", StringComparison.OrdinalIgnoreCase);
                lines.Add(new DialogueLine(isLeft ? DialogueSpeaker.Left : isCenter ? DialogueSpeaker.Center : DialogueSpeaker.Right, item.text,
                    replaceLeft: isLeft, leftCharacter: isLeft ? character : null,
                    replaceRight: !isLeft && !isCenter, rightCharacter: !isLeft && !isCenter ? character : null,
                    replaceCenter: isCenter, centerCharacter: isCenter ? character : null,
                    accentFxKey: item.accentFxKey, accentTargetKey: item.accentTargetKey,
                    passiveHint: lines.Count == 0 ? passiveHint : null, sfxKey: item.sfxKey, voiceKey: item.voiceKey, source: source));
            }
            if (lines.Count == 0)
                lines.Add(new DialogueLine(DialogueSpeaker.Narrator, string.Empty, passiveHint: passiveHint));
            var options = new List<DialogueOption>();
            foreach (var option in data.options)
                options.Add(new DialogueOption(option.id, option.text, enabled: option.enabled, requirementHint: option.requirement));
            // Only the first line introduces its speaker. Later characters must not appear early.
            return new DialogueScript(lines, new DialogueCharacter("", null), new DialogueCharacter("", null), data.title,
                string.IsNullOrEmpty(data.backgroundPath) ? null : Resources.Load<Texture>(data.backgroundPath), options,
                continuityOwner: data.continuityOwner, continues: data.continues, musicKey: data.bgmOverride);
        }

        public static bool Play(NarrativeEventRequest request, NarrativeConversationPresentationData data, Action completed)
            => Play(request, data, completed, "character-dialogue", PanelCachePolicy.Reusable);

        public static bool Play(NarrativeEventRequest request, NarrativeConversationPresentationData data, Action completed,
            string panelTag, PanelCachePolicy cachePolicy, bool isPreview = false)
        {
            if (data == null || data.lines.Count == 0 && data.options.Count == 0) return false;
            bool notified = false;
            void Complete() { if (notified) return; notified = true; completed?.Invoke(); }
            void Cancel() { if (notified) return; notified = true; data.cancelled?.Invoke(); }
            DialogueScript script = CreateScript(data, request.PassiveHint);
            if (isPreview) script = script.WithPreviewContext();
            DialoguePanel panel = DialoguePlayback.Play(script, Complete, Cancel,
                panelTag, cachePolicy);
            data.releaseStage = () => DialoguePlayback.ReleaseStage(data.continuityOwner);
            // Formal first-session guidance owns progression; ESC belongs to the pause menu.
            // Preview playback has no running guide and retains its normal cancellation behavior.
            var dayFlow = UnityEngine.Object.FindFirstObjectByType<DayFlowController>();
            panel.AllowCancel = dayFlow == null || !dayFlow.IsFirstSessionGuideActive;
            panel.OptionSelectionRequested = data.selectChoice;
            return true;
        }
    }
}
