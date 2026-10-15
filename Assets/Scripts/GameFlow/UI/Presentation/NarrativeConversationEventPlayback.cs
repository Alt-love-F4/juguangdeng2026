using System;
using System.Collections.Generic;
using Spotlight.Narrative;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    public static class NarrativeConversationEventPlayback
    {
        public static bool Play(NarrativeEventRequest request, NarrativeConversationPresentationData data, Action completed)
            => Play(request, data, completed, "character-dialogue", PanelCachePolicy.Reusable);

        public static bool Play(NarrativeEventRequest request, NarrativeConversationPresentationData data, Action completed,
            string panelTag, PanelCachePolicy cachePolicy)
        {
            if (data == null || data.lines.Count == 0 && data.options.Count == 0) return false;
            var lines = new List<DialogueLine>(data.lines.Count);
            DialogueCharacter left = null, right = null;
            foreach (NarrativeConversationPresentationLine item in data.lines)
            {
                if (string.IsNullOrWhiteSpace(item.speakerKey))
                {
                    lines.Add(new DialogueLine(DialogueSpeaker.Narrator, item.text, passiveHint: lines.Count == 0 ? request.PassiveHint : null,
                        accentFxKey: item.accentFxKey, accentTargetKey: item.accentTargetKey,
                        sfxKey: item.sfxKey, voiceKey: item.voiceKey));
                    continue;
                }
                CharacterProfile profile = NarrativeCharacterResolver.Find(item.speakerKey);
                Sprite portrait = profile != null ? profile.PortraitFor(item.emote)
                    : item.portrait != null ? item.portrait : string.IsNullOrWhiteSpace(item.portraitPath) ? null : Resources.Load<Sprite>(item.portraitPath);
                var character = new DialogueCharacter(item.speakerKey,
                    profile != null ? profile.displayName : item.speakerName, portrait);
                bool isLeft = string.Equals(item.side, "left", StringComparison.OrdinalIgnoreCase);
                if (isLeft) left = character; else right = character;
                lines.Add(new DialogueLine(isLeft ? DialogueSpeaker.Left : DialogueSpeaker.Right, item.text,
                    replaceLeft: isLeft, leftCharacter: isLeft ? character : null,
                    replaceRight: !isLeft, rightCharacter: !isLeft ? character : null,
                    accentFxKey: item.accentFxKey, accentTargetKey: item.accentTargetKey,
                    passiveHint: lines.Count == 0 ? request.PassiveHint : null, sfxKey: item.sfxKey, voiceKey: item.voiceKey));
            }
            if (lines.Count == 0) lines.Add(new DialogueLine(DialogueSpeaker.Narrator, string.Empty));
            // 显式空角色避免纯旁白套用旧 Prefab 的默认人物。
            if (!string.IsNullOrWhiteSpace(request.PassiveHint) && lines.Count > 0)
            {
                DialogueLine first = lines[0];
                lines[0] = new DialogueLine(first.Speaker, first.Text, first.SpeakerNameOverride, first.Gesture,
                    first.AutoAdvanceSeconds, first.ReplaceLeft, first.LeftCharacter, first.ReplaceRight, first.RightCharacter,
                    first.AccentFxKey, first.AccentTargetKey, first.ReplaceCenter, first.CenterCharacter, request.PassiveHint,
                    sfxKey: first.SfxKey, voiceKey: first.VoiceKey);
            }
            bool notified = false;
            void Complete() { if (notified) return; notified = true; completed?.Invoke(); }
            void Cancel() { if (notified) return; notified = true; data.cancelled?.Invoke(); }
            var options = new List<DialogueOption>();
            foreach (var option in data.options)
                options.Add(new DialogueOption(option.id, option.text, enabled: option.enabled, requirementHint: option.requirement));
            DialoguePanel panel = DialoguePlayback.Play(new DialogueScript(lines, left ?? new DialogueCharacter("", null),
                right ?? new DialogueCharacter("", null), data.title,
                string.IsNullOrEmpty(data.backgroundPath) ? null : Resources.Load<Texture>(data.backgroundPath), options), Complete, Cancel,
                panelTag, cachePolicy);
            panel.AllowCancel = true;
            if (!string.IsNullOrWhiteSpace(data.bgmOverride)) Spotlight.Audio.GameAudio.OverrideMusic(panel, data.bgmOverride);
            panel.OptionSelectionRequested = data.selectChoice;
            return true;
        }
    }
}
