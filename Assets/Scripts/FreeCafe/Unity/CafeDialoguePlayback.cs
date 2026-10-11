using System;
using Spotlight.Narrative;

namespace Spotlight.FreeCafe
{
    /// <summary>咖啡馆领域与共享 DialoguePanel/NCS 的表现适配层。</summary>
    public static class CafeDialoguePlayback
    {
        public static Spotlight.GameFlow.DialoguePanel PlayCustomer(CafePersonIdentity person, string content,
            Action onFinished = null, Action onCancelled = null)
        {
            if (person == null || string.IsNullOrWhiteSpace(person.characterId)) return null;
            CharacterProfile character = Spotlight.GameFlow.NarrativeCharacterResolver.Find(person.characterId);
            if (character == null) return null;
            var speaker = new Spotlight.GameFlow.DialogueCharacter(character.characterId, character.displayName, character.PortraitFor(null));
            var playerProfile = Spotlight.GameFlow.NarrativeCharacterResolver.Find("player");
            var player = playerProfile == null ? null : Spotlight.GameFlow.NarrativeCharacterResolver.CreateDialogueCharacter("player");
            var text = content ?? string.Empty;
            int separator = text.IndexOf('：');
            if (separator >= 0) text = text.Substring(separator + 1);
            var line = new Spotlight.GameFlow.DialogueLine(Spotlight.GameFlow.DialogueSpeaker.Right, text,
                replaceRight: true, rightCharacter: speaker);
            return Spotlight.GameFlow.DialoguePlayback.Play(
                new Spotlight.GameFlow.DialogueScript(new[] { line }, player, speaker, "沸点咖啡馆"), onFinished, onCancelled);
        }
    }
}
