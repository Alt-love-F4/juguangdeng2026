using System;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Shared room sample: real portraits, hard-coded commands, no NCS initialization or event settlement.</summary>
    public static class PresentationEffectsPreview
    {
        public static DialogueScript Create()
        {
            DialogueVisualsAsset visuals = Resources.Load<DialogueVisualsAsset>("Dialogue/DialoguePreview");
            if (visuals == null) throw new InvalidOperationException("Missing DialoguePreview visuals asset.");
            return new DialogueScript(new[]
            {
                new DialogueLine(DialogueSpeaker.Left,
                    "演出特效试验台。\n1 黑影入场 · 2 客人受惊 · 3 轻震屏 · 4 重震屏 · 5 白闪\n6 黑场切换 · 7 左右换位 · 8 镜头推近 · 9 镜头恢复 · 0 角色退场\n效果可同时播放；F9 暂停游戏时间不影响演出。H 可以收起右侧说明。"),
                new DialogueLine(DialogueSpeaker.Left, "我说话，但受惊的是右侧客人。", accentFxKey: PresentationEffectIds.AccentShakeStartle, accentTargetKey: "adele"),
                new DialogueLine(DialogueSpeaker.Right, "现在轮到我说话。所有立绘都保持原图朝向，入场和换位不会再镜像翻转。"),
                new DialogueLine(DialogueSpeaker.Narrator, "第三名角色替换右侧：先退场，再黑影入场。", replaceRight: true,
                    rightCharacter: NarrativeCharacterResolver.CreateDialogueCharacter("thibaut")),
                new DialogueLine(DialogueSpeaker.Left, "新访客出现后仍然可以调用角色ID特效。", accentFxKey: PresentationEffectIds.AccentShakeStartle, accentTargetKey: "thibaut")
            }, NarrativeCharacterResolver.CreateDialogueCharacter("eugene"),
                NarrativeCharacterResolver.CreateDialogueCharacter("adele"),
                title: "演出特效 · 独立表现层", background: visuals.Background);
        }
    }
}
