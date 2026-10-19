using System;
using System.Collections.Generic;
using Spotlight.GameFlow;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>咖啡馆领域与共享 DialoguePanel/NCS 的表现适配层。</summary>
    public static class CafeDialoguePlayback
    {
        public static Spotlight.GameFlow.DialoguePanel PlayCustomer(CafePersonIdentity person, string content,
            Action onFinished = null, Action onCancelled = null)
        {
            var script = CreateCustomerScript(person, content);
            return script == null ? null : DialoguePlayback.Play(script, onFinished, onCancelled);
        }

        /// <summary>Keep each authored speaker as a separate beat, including Adele's order replies.</summary>
        public static DialogueScript CreateCustomerScript(CafePersonIdentity person, string content)
        {
            if (person == null) return null;
            CharacterProfile profile = NarrativeCharacterResolver.Find(person.characterId);
            var customer = new DialogueCharacter(person.personId ?? person.characterId,
                string.IsNullOrWhiteSpace(person.displayName) ? profile?.displayName ?? "顾客" : person.displayName,
                CafeCustomerVisualVariants.ResolvePortrait(person, profile)
                    ?? Resources.Load<Sprite>("Characters/guest_a/neutral"));
            var adele = NarrativeCharacterResolver.CreateDialogueCharacter("adele");
            var player = NarrativeCharacterResolver.CreateDialogueCharacter("player");
            var lines = new List<DialogueLine>();
            foreach (var raw in (content ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string text = raw.Trim();
                int separator = text.IndexOf('：');
                string name = separator < 0 ? string.Empty : text.Substring(0, separator).Trim();
                bool isAdele = name == "阿黛尔" || name == "艾黛尔" || name == adele?.DisplayName;
                bool isPlayer = name == "我" || name == "你" || name == player?.DisplayName;
                if (separator >= 0) text = text.Substring(separator + 1).TrimStart();
                if (string.IsNullOrWhiteSpace(text)) continue;
                var speaker = isAdele ? adele : isPlayer ? player : customer;
                bool left = isAdele || isPlayer;
                lines.Add(new DialogueLine(left ? DialogueSpeaker.Left : DialogueSpeaker.Right, text,
                    replaceLeft: left, leftCharacter: left ? speaker : null,
                    replaceRight: !left, rightCharacter: left ? null : speaker));
            }
            if (lines.Count == 0) lines.Add(new DialogueLine(DialogueSpeaker.Right, "请看看我的订单。",
                replaceRight: true, rightCharacter: customer));
            return new DialogueScript(lines, rightCharacter: customer, title: "沸点咖啡馆");
        }
    }
}
