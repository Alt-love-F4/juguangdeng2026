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
                Sprite portrait = string.IsNullOrWhiteSpace(item.portraitPath) ? null : Resources.Load<Sprite>(item.portraitPath);
                var character = new DialogueCharacter(item.speakerKey, item.speakerName, portrait);
                bool isLeft = string.Equals(item.side, "left", StringComparison.OrdinalIgnoreCase);
                if (isLeft) left = character; else right = character;
                lines.Add(new DialogueLine(isLeft ? DialogueSpeaker.Left : DialogueSpeaker.Right, item.text,
                    replaceLeft: isLeft, leftCharacter: isLeft ? character : null,
                    replaceRight: !isLeft, rightCharacter: !isLeft ? character : null,
                    accentFxKey: item.accentFxKey, accentTargetKey: item.accentTargetKey));
            }
            DialoguePlayback.Play(new DialogueScript(lines, left, right, data.title), completed, completed);
            return true;
        }
    }
}
