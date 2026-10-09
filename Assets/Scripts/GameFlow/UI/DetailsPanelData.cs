using System;
using System.Collections.Generic;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    public enum DetailsAttributeKind { Language, Courage, Wisdom, Perception }

    /// <summary>Immutable display snapshot; contains no model, table, narrative or save dependencies.</summary>
    public sealed class DetailsPanelData : IUIData
    {
        public DetailsProfileData Profile { get; }
        public IReadOnlyList<DetailsAttributeData> Attributes { get; }
        public IReadOnlyList<DetailsSpecializationData> Specializations { get; }
        public DetailsSectionData Relationships { get; }
        public DetailsSectionData World { get; }
        public bool IsPlaceholder { get; }
        public IReadOnlyList<DetailsCharacterData> Characters { get; }
        public IReadOnlyList<DetailsProgressData> WorldBars { get; }

        public DetailsPanelData(DetailsProfileData profile = null,
            IEnumerable<DetailsAttributeData> attributes = null,
            IEnumerable<DetailsSpecializationData> specializations = null,
            DetailsSectionData relationships = null, DetailsSectionData world = null, bool isPlaceholder = false,
            IEnumerable<DetailsCharacterData> characters = null, IEnumerable<DetailsProgressData> worldBars = null)
        {
            Profile = profile ?? new DetailsProfileData();
            var ordered = new DetailsAttributeData[4];
            if (attributes != null)
            {
                foreach (DetailsAttributeData attribute in attributes)
                {
                    if (attribute == null) throw new ArgumentException("Attributes must not contain null.", nameof(attributes));
                    int index = (int)attribute.Kind;
                    if (ordered[index] != null) throw new ArgumentException("Duplicate attribute kind: " + attribute.Kind, nameof(attributes));
                    ordered[index] = attribute;
                }
            }
            for (int i = 0; i < ordered.Length; i++)
                if (ordered[i] == null) ordered[i] = new DetailsAttributeData((DetailsAttributeKind)i, 0);
            Attributes = Array.AsReadOnly(ordered);
            var badges = specializations == null ? new List<DetailsSpecializationData>() : new List<DetailsSpecializationData>(specializations);
            if (badges.Exists(item => item == null)) throw new ArgumentException("Specializations must not contain null.", nameof(specializations));
            Specializations = badges.AsReadOnly();
            Relationships = relationships ?? new DetailsSectionData("人物关系", "暂无人物关系", "尚未提供人物关系展示数据。");
            World = world ?? new DetailsSectionData("世界情况", "暂无世界情况", "尚未提供世界情况展示数据。");
            IsPlaceholder = isPlaceholder;
            Characters = CopyItems(characters, nameof(characters));
            WorldBars = CopyItems(worldBars, nameof(worldBars));
        }

        internal static IReadOnlyList<T> CopyItems<T>(IEnumerable<T> source, string parameter) where T : class
        {
            var items = source == null ? new List<T>() : new List<T>(source);
            if (items.Exists(item => item == null)) throw new ArgumentException("Items must not contain null.", parameter);
            return items.AsReadOnly();
        }

        /// <summary>Explicit preview fallback. Real callers supply their own display snapshot.</summary>
        public static DetailsPanelData CreatePlaceholder(Sprite badgeIcon = null)
        {
            return new DetailsPanelData(
                new DetailsProfileData("主角 · 咖啡馆馆长",
                    "【背景占位】你因叔叔的一封来信来到这座城市，接手了他留下的咖啡馆。\n关于故乡、过往经历与来到这里的原因，未来会根据开场选择展示。",
                    "【馆长介绍占位】以咖啡与倾听接待来客，经营这间属于你的咖啡馆。"),
                new[]
                {
                    new DetailsAttributeData(DetailsAttributeKind.Language, 8, "影响表达、说服与沟通，让你更容易理解来客并赢得信任。（占位描述）"),
                    new DetailsAttributeData(DetailsAttributeKind.Courage, 6, "影响面对风险与压力时的行动，让你敢于尝试、坚持与作出决定。（占位描述）"),
                    new DetailsAttributeData(DetailsAttributeKind.Wisdom, 10, "影响分析、推理与解决问题，帮助你从信息中找到可靠的答案。（占位描述）"),
                    new DetailsAttributeData(DetailsAttributeKind.Perception, 7, "影响观察环境与察觉细节，帮助你发现线索和他人的情绪变化。（占位描述）")
                },
                new[]
                {
                    new DetailsSpecializationData("咖啡调制", "熟悉咖啡的香气与风味，能为不同来客调制合适的饮品。（占位专精，不代表已解锁）", badgeIcon),
                    new DetailsSpecializationData("善于倾听", "耐心倾听话语背后的心事，让来客愿意分享自己的故事。（占位专精，不代表已解锁）", badgeIcon),
                    new DetailsSpecializationData("街头见闻", "了解城市的街巷与传闻，容易在日常交谈中获得消息。（占位专精，不代表已解锁）", badgeIcon),
                    new DetailsSpecializationData("细致观察", "善于发现不起眼的细节，从细微变化中捕捉线索。（占位专精，不代表已解锁）", badgeIcon),
                    new DetailsSpecializationData("沉着应对", "在忙碌或突发情况中保持冷静，稳妥安排手边的事情。（占位专精，不代表已解锁）", badgeIcon),
                    new DetailsSpecializationData("灵感手记", "记录突如其来的想法，尝试从不同角度理解问题。（占位专精，不代表已解锁）", badgeIcon)
                },
                new DetailsSectionData("人物关系 / 占位页", "关系网络尚未接入", "今后将在这里展示人物档案、关系变化与交往记录。\n当前仅为页面占位，不读取角色或关系数据。"),
                new DetailsSectionData("世界情况 / 占位页", "世界信息尚未接入", "今后将在这里展示地区情况、势力动向与世界事件。\n当前仅为页面占位，不读取世界状态。"), true);
        }
    }

    public sealed class DetailsCharacterData
    {
        public string Id { get; }
        public string Name { get; }
        public string Introduction { get; }
        public Sprite Avatar { get; }
        public bool Unlocked { get; }
        public IReadOnlyList<DetailsProgressData> Bars { get; }

        public DetailsCharacterData(string id, string name, string introduction = null, Sprite avatar = null,
            bool unlocked = false, IEnumerable<DetailsProgressData> bars = null)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Introduction = introduction ?? string.Empty;
            Avatar = avatar;
            Unlocked = unlocked;
            Bars = DetailsPanelData.CopyItems(bars, nameof(bars));
        }
    }

    public sealed class DetailsProgressData
    {
        public string Key { get; }
        public string Name { get; }
        public float Value { get; }
        public float Min { get; }
        public float Max { get; }
        public string LowerDescription { get; }
        public string UpperDescription { get; }
        public string Description { get; }

        public DetailsProgressData(string key, string name, float value, float min = 0f, float max = 100f,
            string lowerDescription = null, string upperDescription = null, string description = null)
        {
            if (float.IsNaN(min) || float.IsInfinity(min)) throw new ArgumentOutOfRangeException(nameof(min));
            if (float.IsNaN(max) || float.IsInfinity(max) || max < min) throw new ArgumentOutOfRangeException(nameof(max));
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            Key = key ?? string.Empty;
            Name = name ?? string.Empty;
            Min = min;
            Max = max;
            Value = Mathf.Clamp(value, min, max);
            LowerDescription = lowerDescription ?? string.Empty;
            UpperDescription = upperDescription ?? string.Empty;
            Description = description ?? string.Empty;
        }
    }

    public sealed class DetailsProfileData
    {
        public string Name { get; }
        public string Background { get; }
        public string Introduction { get; }
        public Sprite Avatar { get; }
        public DetailsProfileData(string name = null, string background = null, string introduction = null, Sprite avatar = null)
        {
            Name = name ?? string.Empty;
            Background = background ?? string.Empty;
            Introduction = introduction ?? string.Empty;
            Avatar = avatar;
        }
    }

    public sealed class DetailsAttributeData
    {
        public DetailsAttributeKind Kind { get; }
        public int Value { get; }
        public string Description { get; }
        public string Name => Kind switch
        {
            DetailsAttributeKind.Language => "语言",
            DetailsAttributeKind.Courage => "勇气",
            DetailsAttributeKind.Wisdom => "智慧",
            _ => "知觉"
        };
        public DetailsAttributeData(DetailsAttributeKind kind, int value, string description = null)
        {
            if ((int)kind < 0 || (int)kind > 3) throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind;
            Value = Mathf.Clamp(value, 0, 20);
            Description = description ?? string.Empty;
        }
    }

    public sealed class DetailsSpecializationData
    {
        public string Name { get; }
        public string Description { get; }
        public Sprite Icon { get; }
        public DetailsSpecializationData(string name, string description = null, Sprite icon = null)
        {
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Icon = icon;
        }
    }

    /// <summary>Text-only contract for the two unfinished pages, not a relationship/world model.</summary>
    public sealed class DetailsSectionData
    {
        public string Title { get; }
        public string Heading { get; }
        public string Description { get; }
        public DetailsSectionData(string title, string heading = null, string description = null)
        {
            Title = title ?? string.Empty;
            Heading = heading ?? string.Empty;
            Description = description ?? string.Empty;
        }
    }
}
