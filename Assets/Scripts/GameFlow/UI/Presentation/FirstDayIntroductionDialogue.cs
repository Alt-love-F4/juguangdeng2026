using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>第一天进入咖啡馆的临时硬编码剧本，后续可替换为 Luban 数据源。</summary>
    public sealed class FirstDayIntroductionDialogue : IDialogueScriptSource
    {
        private const string VisualAssetPath = "Dialogue/FirstDayIntroductionVisuals";

        public static DialogueScript Create() => new FirstDayIntroductionDialogue().CreateScript();

        public DialogueScript CreateScript()
        {
            DialogueVisualsAsset visuals = Resources.Load<DialogueVisualsAsset>(VisualAssetPath);
            var lines = new DialogueLine[]
            {
                new DialogueLine(DialogueSpeaker.Narrator, "你推开沸点咖啡馆的门。吧台后的年轻女子放下抹布，先看了看你的行李，又看向你手里的信。"),
                new DialogueLine(DialogueSpeaker.Right, "你就是他一直惦记的那个孩子吧？我是阿黛尔，这里的店员。欢迎来到圣诺特。", gesture: DialogueGesture.Nod),
                new DialogueLine(DialogueSpeaker.Left, "我是来接手咖啡馆的。他在信里说，这里会是我的新起点。"),
                new DialogueLine(DialogueSpeaker.Right, "嗯，沸点咖啡馆。名字听起来很厉害，实际上连热水壶都比我们脾气大。别怕，我会陪你把它打理好。"),
                new DialogueLine(DialogueSpeaker.Right, "你叔叔总说，一杯咖啡不只用来提神。有些人进门，是想找个能喘口气的地方。"),
                new DialogueLine(DialogueSpeaker.Left, "他还说过什么？"),
                new DialogueLine(DialogueSpeaker.Right, "他说，等你来了，就把靠窗的位置留给你……他一直盼着亲口听你讲这些年的事。"),
                new DialogueLine(DialogueSpeaker.Narrator, "阿黛尔低头理了理围裙，停了一会儿，再抬起头时已经重新露出笑容。"),
                new DialogueLine(DialogueSpeaker.Right, "好啦，伤心的事我们慢慢说。今天先从第一杯咖啡开始，我可不会让新店主第一天就手忙脚乱。", gesture: DialogueGesture.Nod),
                new DialogueLine(DialogueSpeaker.Right, "客人来了，先看清他们的订单，再按配方准备原料、使用设备。做好的饮品记得交给对应的客人。"),
                new DialogueLine(DialogueSpeaker.Right, "不确定的时候就查看订单和配方，别急着乱试。先把一位客人照顾好，再接下一位。"),
                new DialogueLine(DialogueSpeaker.Left, "那就拜托你了，阿黛尔。"),
                new DialogueLine(DialogueSpeaker.Right, "当然。不过店主也要记得照顾好自己——这是你叔叔留给我们的规矩。准备好了？我们开门吧。", gesture: DialogueGesture.Nod)
            };
            if (visuals == null) throw new System.InvalidOperationException("Missing FirstDayIntroductionVisuals asset.");
            return new DialogueScript(lines, new DialogueCharacter("你", null),
                new DialogueCharacter("阿黛尔", visuals.RightPortrait),
                title: "初来乍到 · 沸点咖啡馆", background: visuals.Background);
        }
    }
}
