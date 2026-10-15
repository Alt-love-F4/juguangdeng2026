using System;
using System.Collections.Generic;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    public static class NarrativeConversationEventPlayback
    {
        public static bool Play(NarrativeEventRequest request, NarrativeConversationPresentationData data, Action completed)
        {
            if (data == null || data.lines.Count == 0) return false;
            var lines = new List<DialogueLine>(data.lines.Count);
            DialogueCharacter left = null, right = null;
            foreach (NarrativeConversationPresentationLine item in data.lines)
            {
                if (string.IsNullOrWhiteSpace(item.speakerKey))
                {
                    lines.Add(new DialogueLine(DialogueSpeaker.Narrator, item.text, passiveHint: lines.Count == 0 ? request.PassiveHint : null));
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
                    passiveHint: lines.Count == 0 ? request.PassiveHint : null));
            }
            // 显式空角色避免纯旁白套用旧 Prefab 的默认人物。
            bool notified = false;
            void Complete() { if (notified) return; notified = true; completed?.Invoke(); }
            var options = new List<DialogueOption>();
            foreach (var option in data.options)
                options.Add(new DialogueOption(option.id, option.text, enabled: option.enabled, requirementHint: option.requirement));
            DialoguePanel panel = DialoguePlayback.Play(new DialogueScript(lines, left ?? new DialogueCharacter("", null),
                right ?? new DialogueCharacter("", null), data.title,
                string.IsNullOrEmpty(data.backgroundPath) ? null : Resources.Load<Texture>(data.backgroundPath), options), Complete);
            panel.AllowCancel = false;
            panel.OptionSelectionRequested = data.selectChoice;
            return true;
        }
    }
}
