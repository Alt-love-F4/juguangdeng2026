using System;
using System.Collections.Generic;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>One player-facing faction profile. Text follows 策划文件/文案/z-全设定集/势力与派别设定案.xlsx,
    /// trimmed to what a newcomer in the capital can learn without spoiling the four lines.</summary>
    public sealed class WorldFactionInfo
    {
        public string Id { get; }
        public string Name { get; }
        public string Epithet { get; }
        public string Leader { get; }
        public string Summary { get; }
        public string EmblemPath { get; }
        public Color Accent { get; }
        /// <summary>Existing NCS bar that measures the player's leaning toward this faction; may be shared.</summary>
        public string TendencyBarKey { get; }
        public string TendencyName { get; }
        /// <summary>Existing facts produced by real story roots. Any true fact means the player has met the faction.</summary>
        public IReadOnlyList<string> EncounterFacts { get; }
        public bool AlwaysKnown => EncounterFacts.Count == 0;

        public WorldFactionInfo(string id, string name, string epithet, string leader, string summary, string emblemPath,
            Color accent, string tendencyBarKey, string tendencyName, params string[] encounterFacts)
        {
            Id = id; Name = name; Epithet = epithet; Leader = leader; Summary = summary; EmblemPath = emblemPath;
            Accent = accent; TendencyBarKey = tendencyBarKey; TendencyName = tendencyName;
            EncounterFacts = Array.AsReadOnly(encounterFacts ?? Array.Empty<string>());
        }
    }

    /// <summary>Shared by the details world page and the latte bench. Reads existing facts only; never writes state.</summary>
    public static class WorldFactionCatalog
    {
        public const string Cafe = "cafe", Court = "court", Church = "church", Rose = "rose", Revolution = "revolution";
        private static readonly Dictionary<string, Sprite> Emblems = new Dictionary<string, Sprite>();

        public static readonly IReadOnlyList<WorldFactionInfo> All = Array.AsReadOnly(new[]
        {
            new WorldFactionInfo(Cafe, "沸点咖啡馆", "圣徒街 217 号", "代理店长：你",
                "坐落于王都圣诺特圣徒街 217 号，靠近旧城区的十字路口，由叔叔杜瓦尔·米尼耶年轻时创立。手艺高超、装修素雅、欢迎一切顾客，曾是圣徒街最受欢迎的咖啡馆。\n不同阶层的客人在此交谈，街巷里的消息与厅堂中的议论，也随之汇聚于一张张小桌之间。",
                "UI/Factions/cafe", new Color(.47f, .27f, .14f), "faction.neutral", "经营与中立倾向"),
            new WorldFactionInfo(Court, "奥尔狄亚王庭", "剑与王冠", "君主：克洛维·康多尔",
                "以康多尔皇室为核心，由皇室家臣、在编神佑者、政务官僚与直接听命于皇室的近卫队共同构成。维系它的，是对皇室的效忠，以及神佑者统治王国的传统。\n伴随神佑者力量的衰竭，王庭已无法再压倒其他势力。如何应对这一局面，是王庭内部长久争论的问题。",
                "UI/Factions/court", new Color(.13f, .18f, .33f), "faction.church", "王庭与教会倾向",
                "known.alienor", "story.church.1009.s2.completed", "story.revolution.1028.s1.completed",
                "story.church.1104.s1.completed", "story.church.1111.s1.completed", "route.church"),
            new WorldFactionInfo(Church, "圣谐教会", "善良 · 勇敢 · 智慧 · 觉知", "大主教：马米翁",
                "奥尔狄亚王国实际上的政治机构。教会供奉神佑者，将宗教信仰与日常治理结合；对多数民众而言，它既主持信仰事务，也管理着生活中的行政、司法与监察，国家军队亦由其实际掌控。\n教会设监察司、律法司、教务司与事务司，分掌监察、司法、神学与行政，逐级隶属，自成体系。",
                "UI/Factions/church", new Color(.29f, .29f, .29f), "faction.church", "王庭与教会倾向",
                "known.sade", "story.revolution.1014.s1.completed", "story.rose.1016.s1.completed",
                "story.church.1020.s1.completed", "story.church.1023.s1.completed", "story.church.1111.s1.completed", "route.church"),
            new WorldFactionInfo(Rose, "新玫瑰会", "玫瑰王的遗产", "所尊奉：「玫瑰王」贝特朗",
                "百年前，「玫瑰王」贝特朗·康多尔率玫瑰军发动政变，史称双王之乱。失败之后，这股力量转入隐秘，逐渐成为一个秘密政治团体。\n其成员分散在王庭、教会与军队之中，以各自的公开身份活动，延续着当年的改革主张与玫瑰军留下的政治遗产。",
                "UI/Factions/rose", new Color(.52f, .20f, .37f), "faction.rose", "新玫瑰倾向",
                "story.rose.1011.s1.completed", "story.rose.1016.s1.completed", "story.rose.1024.s1.completed",
                "story.rose.1024.s2.completed", "story.rose.1026.s2.completed", "story.revolution.1029.s1.completed",
                "story.rose.1103.s1.completed", "story.revolution.1107.s1.completed", "story.church.route.1112.s1.completed", "route.rose"),
            new WorldFactionInfo(Revolution, "革命联合", "挣断锁链的拳头", "领袖：勒苏",
                "奥尔狄亚第一个激进组织，主张彻底推翻神权制与君主制。它汇集了中下阶层中对现状不满的人，共同的立场是拒绝继续接受现存神权秩序的统治。\n与其说它是纪律严明的政党，不如说是一位富有号召力的领袖身边形成的联盟：成员处境各异，对革命之后应当建立怎样的社会，也还没有统一的答案。",
                "UI/Factions/revolution", new Color(.58f, .13f, .12f), "faction.revolution", "革命联合倾向",
                "known.lesou", "known.mary", "story.rose.1024.s1.completed", "story.rose.1024.s2.completed",
                "story.rose.1026.s2.completed", "story.revolution.1028.s1.completed", "story.revolution.1029.s1.completed",
                "story.church.1111.s1.completed", "route.revolution")
        });

        public static WorldFactionInfo Find(string id)
        {
            foreach (var faction in All) if (faction.Id == id) return faction;
            return null;
        }

        /// <summary>Current formal session state, or null when no narrative session exists.</summary>
        public static NarrativeState CurrentState
        {
            get
            {
                var runtime = NarrativeRuntime.Instance;
                return runtime != null && runtime.IsReady ? runtime.Controller.State : null;
            }
        }

        public static bool IsEncountered(WorldFactionInfo faction, NarrativeState state)
        {
            if (faction == null) return false;
            if (faction.AlwaysKnown) return true;
            if (state == null) return false;
            foreach (string fact in faction.EncounterFacts)
                if (state.TryGetCondition(fact, out bool value) && value) return true;
            return false;
        }

        public static bool IsEncountered(string id) => IsEncountered(Find(id), CurrentState);

        public static Sprite Emblem(WorldFactionInfo faction)
        {
            if (faction == null || string.IsNullOrEmpty(faction.EmblemPath)) return null;
            if (Emblems.TryGetValue(faction.EmblemPath, out Sprite sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>(faction.EmblemPath);
            Emblems[faction.EmblemPath] = sprite;
            return sprite;
        }
    }
}
