using System;
using System.Text;

namespace Spotlight.GameFlow
{
    /// <summary>开场信的硬编码文案与建角选项；编辑中的选择不直接修改存档。</summary>
    public sealed class FirstDayLetterDraft
    {
        public const string Signature = "—— 永远惦念你的叔叔";
        public const string Ink = "000000";
        public const string ChoiceInk = "B3000F";
        public const string HoverInk = "D18C00";
        public static readonly string[] AttributeKeys = { "language", "courage", "wisdom", "perception" };
        public static readonly string[] SpecializationIds = { "letter.religion", "letter.literature", "letter.science", "letter.politics" };
        public static readonly string[] SpecializationNames = { "宗教知识专精", "文学知识专精", "科学知识专精", "政治知识专精" };
        public static readonly string[][] Labels =
        {
            new[] { "贫寒", "平民", "富贵" },
            new[] { "善良", "赤诚" },
            new[] { "勇敢", "明睿" },
            new[] { "神学", "文学", "数学与物理科学", "历史与官房学" },
            new[] { "能言善辩", "坚定无畏", "聪颖睿智", "敏锐细腻" }
        };
        public static readonly string[][] Effects =
        {
            new[] { "初始资金最少，初始身份“流浪儿”", "初始资金一般，初始身份“平民”", "初始资金最多，初始身份“贵族”" },
            new[] { "语言+2", "知觉+2" },
            new[] { "勇气+2", "智慧+2" },
            new[] { "获得宗教知识专精，知觉+2，语言+1", "获得文学知识专精，语言+2，勇气+1", "获得科学知识专精，智慧+2，知觉+1", "获得政治知识专精，勇气+2，智慧+1" },
            new[] { "语言+2", "勇气+2", "智慧+2", "知觉+2" }
        };
        public string PlayerName { get; private set; } = "";
        private readonly int[] selections = { -1, -1, -1, -1, -1 };
        public bool IsComplete => PlayerName.Length > 0 && Array.TrueForAll(selections, value => value >= 0);
        public int Selection(int group) => selections[group];
        public void SetName(string value)
        {
            var result = new StringBuilder();
            foreach (char c in (value ?? "").Trim())
                if (!char.IsControl(c) && c != '<' && c != '>' && result.Length < 16) result.Append(c);
            PlayerName = result.ToString().Trim();
        }
        public bool Select(int group, int option)
        {
            if (group < 0 || group >= Labels.Length || option < 0 || option >= Labels[group].Length) return false;
            selections[group] = option;
            return true;
        }
        public int[] CalculateBonuses()
        {
            var values = new int[4];
            if (selections[1] >= 0) values[selections[1] == 0 ? 0 : 3] += 2;
            if (selections[2] >= 0) values[selections[2] == 0 ? 1 : 2] += 2;
            switch (selections[3])
            {
                case 0: values[3] += 2; values[0]++; break;
                case 1: values[0] += 2; values[1]++; break;
                case 2: values[2] += 2; values[3]++; break;
                case 3: values[1] += 2; values[2]++; break;
            }
            if (selections[4] >= 0) values[selections[4]] += 2;
            return values;
        }
        public string Origin => selections[0] < 0 ? "" : new[] { "流浪儿", "平民", "贵族" }[selections[0]];
        public string Specialization => selections[3] < 0 ? "" : SpecializationIds[selections[3]];
        public string Background => IsComplete ? $"出身{Labels[0][selections[0]]}，曾修习{Labels[3][selections[3]]}；{Labels[1][selections[1]]}、{Labels[2][selections[2]]}，如今已是{Labels[4][selections[4]]}的人。" : "";

        public string BuildBody(string hoveredLink = null)
        {
            string Link(string id, string label, bool selected = false)
            {
                string text = selected ? "<b><u>" + label + "</u></b>" : label;
                return $"<link=\"{id}\"><color=#{(hoveredLink == id ? HoverInk : ChoiceInk)}>{text}</color></link>";
            }
            string Word(int group) => selections[group] < 0 ? "______" : $"<u>{Labels[group][selections[group]]}</u>";
            string Options(int group)
            {
                var result = new StringBuilder("（选项：");
                for (int i = 0; i < Labels[group].Length; i++)
                {
                    if (i > 0) result.Append("；");
                    result.Append(Link($"choice:{group}:{i}", Labels[group][i] + "【" + Effects[group][i] + "】", selections[group] == i));
                }
                return result.Append("）").ToString();
            }
            return "我亲爱的" + Link("name", PlayerName.Length == 0 ? "______（点击输入姓名）" : PlayerName) + "：\n\n" +
                "我是你的叔叔，杜瓦尔·米尼耶。许久未见，不知你近来可好。\n" +
                "月寒日暖，冬去春来，我们在北境留下的珍贵的回忆不知你还记得多少，你幼时出身" + Word(0) + Options(0) + "，\n" +
                "但父母忙碌，你便常常找到我作你的玩伴。662年，我遭逢海难，身患胃疾，那时年幼的你虽不是我的儿女，你" + Word(1) + Options(1) + "的心却驱使着你主动提出照顾我，陪我度过了那段艰难的时光。我永远不会忘记那时你" + Word(2) + Options(2) + "的身影。\n\n" +
                "从你父母来信中得知，你已长大成人，在北港的学院里研读了" + Word(3) + Options(3) + "，\n" +
                "并顺利毕业，成为了一个" + Word(4) + Options(4) + "的人。\n\n" +
                "然而我却又听闻，由于时运不济，你离开学院后只能在家乡暂时做些市井活计谋生，叔叔我闻此不由得伤心落泪。我相信，今日的你已经成长得可以在王都生活下去。我在王都圣诺特有一个咖啡馆，但由于我已离开王都多年，只能委托他人代为打理，生意平平。如果你不嫌弃，我愿意将那间咖啡馆交付给你，由你担任店长。可惜我自己身体抱恙，时日无多，不能亲到王都亲自见你。那间咖啡馆也是我年轻时打拼过的地方，希望你能振兴那间咖啡馆，在王都过上安定的生活。\n" +
                "咖啡馆位于圣穆街217号，我的女儿阿黛尔已经前往咖啡馆，她会和你一起经营，帮助你早日上手咖啡馆的各项事务。\n\n" +
                "吾主庇佑，荣光永存。";
        }
    }
}
