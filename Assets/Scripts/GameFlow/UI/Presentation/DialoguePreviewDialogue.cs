using System;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>F8 演示用硬编码剧本，与第一天正式剧本分开。</summary>
    public sealed class DialoguePreviewDialogue : IDialogueScriptSource
    {
        public static DialogueScript Create() => new DialoguePreviewDialogue().CreateScript();

        /// <summary>
        /// 共享调试场景的选项演出样例。它只展示 DialoguePanel 的选项、禁用态、选择反馈和回应台词，
        /// 不调用 NCS 选择结算，也不修改存档。
        /// </summary>
        public static DialogueScript CreateOptions()
        {
            var visuals = Resources.Load<DialogueVisualsAsset>("Dialogue/DialoguePreview");
            if (visuals == null) throw new InvalidOperationException("Missing DialoguePreview visuals asset.");
            return new DialogueScript(new[]
            {
                new DialogueLine(DialogueSpeaker.Right, "夜色落下来，印刷工把那份名单推到你面前。", gesture: DialogueGesture.Nod),
                new DialogueLine(DialogueSpeaker.Center, "我也带来了消息。先把门关好，我们慢慢说。", gesture: DialogueGesture.Nod),
                new DialogueLine(DialogueSpeaker.Left, "咖啡还热着。名单上的这些人，你们都认识吗？"),
                new DialogueLine(DialogueSpeaker.Right, "有几个是印刷厂的老工人，剩下的名字我也是第一次见。"),
                new DialogueLine(DialogueSpeaker.Center, "我下午去过码头，有人正在打听他们的住处。"),
                new DialogueLine(DialogueSpeaker.Left, "那就不能把这张纸留在桌上。先收起来。"),
                new DialogueLine(DialogueSpeaker.Right, "我想在天亮之前提醒他们，但需要一个可靠的帮手。"),
                new DialogueLine(DialogueSpeaker.Center, "我们三个人得一起想想：你准备怎么处理？")
            }, NarrativeCharacterResolver.CreatePreviewCharacter("player", visuals.LeftPortrait),
                NarrativeCharacterResolver.CreatePreviewCharacter("adele", visuals.RightPortrait),
                centerCharacter: NarrativeCharacterResolver.CreatePreviewCharacter("npc_a", visuals.CenterPortrait),
                title: "咖啡馆 · 三人选项演出测试", background: visuals.Background,
                optionPrompt: "你要如何回应？",
                options: new[]
                {
                    new DialogueOption("support", "支持他的计划",
                        new DialogueLine(DialogueSpeaker.Left, "好，我会帮你把这件事安排下去。"),
                        resultKey: "debug_support"),
                    new DialogueOption("caution", "提醒他保持谨慎",
                        new DialogueLine(DialogueSpeaker.Center, "你说得对，我们三个人更应该小心，不能因为着急就留下破绽。"),
                        resultKey: "debug_caution"),
                    new DialogueOption("locked", "询问只有线人知道的情报",
                        new DialogueLine(DialogueSpeaker.Narrator, "你现在还没有足够的情报。"),
                        enabled: false, requirementHint: "需要更多情报", resultKey: "debug_locked")
                });
        }

        public DialogueScript CreateScript()
        {
            var visuals = Resources.Load<DialogueVisualsAsset>("Dialogue/DialoguePreview");
            if (visuals == null) throw new InvalidOperationException("Missing DialoguePreview visuals asset.");
            return new DialogueScript(new[]
            {
                new DialogueLine(DialogueSpeaker.Narrator, "傍晚的最后一缕光穿过玻璃。门铃响起，一位带着油墨气味的客人走进咖啡馆。"),
                new DialogueLine(DialogueSpeaker.Left, "欢迎光临。今天想喝点什么？"),
                new DialogueLine(DialogueSpeaker.Right, "一杯热咖啡，不加糖。还有……我想在这里等一个人。"),
                new DialogueLine(DialogueSpeaker.Left, "当然。靠窗的位置还空着，你可以慢慢等。", gesture: DialogueGesture.Nod),
                new DialogueLine(DialogueSpeaker.Right, "谢谢。有些话，只有在这样安静的地方才说得出口。"),
                new DialogueLine(DialogueSpeaker.Narrator,
                    "城里的印刷机已经停了三天，街上却比往常更加热闹。有人在寻找一封没有署名的信，也有人只想找个地方坐下来。\n\n" +
                    "我原本以为今天只是普通的一天，直到他把那张折了四次的纸放在桌上。纸边沾着油墨，字迹却非常清楚。那不是一张订单，而是一份名单。\n\n" +
                    "我没有问名单上是谁。他也没有解释。门外又传来一阵脚步声，我们同时看向窗边。\n\n" +
                    "有些故事，从一杯咖啡开始，却不会在咖啡凉下来时结束。"),
                new DialogueLine(DialogueSpeaker.Left, "那就先喝一口吧。无论外面发生什么，这杯咖啡都还是热的。")
            }, NarrativeCharacterResolver.CreatePreviewCharacter("player", visuals.LeftPortrait),
                NarrativeCharacterResolver.CreatePreviewCharacter("adele", visuals.RightPortrait),
                title: "咖啡馆 · 不期而至的访客", background: visuals.Background);
        }
    }
}
