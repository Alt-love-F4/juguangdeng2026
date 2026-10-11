using UnityEngine;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>独立于 NCS 的手动角色演出入口，可由按钮或业务协调器调用。</summary>
    public sealed class CharacterDialoguePreview : MonoBehaviour
    {
        // Retained for serialized scene compatibility; runtime previews use the unified table catalog.
        public CharacterCatalog catalog;
        public string leftCharacterId = "eugene";
        public string rightCharacterId = "adele";
        [TextArea] public string leftText = "今天也来一杯咖啡吧。";
        [TextArea] public string rightText = "欢迎，慢慢坐。";

        [ContextMenu("Play Character Dialogue")]
        public void Play()
        {
            if (!Application.isPlaying) { Debug.LogWarning("请进入 Play Mode 后播放人物预览。", this); return; }
            var a = NarrativeCharacterResolver.Find(leftCharacterId);
            var b = NarrativeCharacterResolver.Find(rightCharacterId);
            if (a == null || b == null) { Debug.LogError("人物 ID 不在角色目录中。", this); return; }
            DialoguePlayback.Play(new DialogueScript(new[]
            {
                new DialogueLine(DialogueSpeaker.Left, leftText),
                new DialogueLine(DialogueSpeaker.Right, rightText)
            }, NarrativeCharacterResolver.CreateDialogueCharacter(leftCharacterId),
               NarrativeCharacterResolver.CreateDialogueCharacter(rightCharacterId), "角色配置预览"));
        }
    }
}
