using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Generated player configuration; no gameplay state or duplicated initial attributes.</summary>
    public sealed class PlayerCreationCatalog
    {
        private readonly cfg.Tables tables;
        public List<cfg.PlayerAttributeEntry> Attributes { get; }
        public List<cfg.CreationGroupEntry> Groups { get; }
        public List<cfg.CreationOptionEntry> Options { get; }
        public List<cfg.CreationBonusEntry> Bonuses { get; }
        public AvatarKind DefaultAvatar { get; }
        public string DefaultOriginId { get; }
        public int BaseStartingMoney { get; }
        public int ActionPointMax { get; }
        public string DefaultName => Text(tables.TbNcsObject.DataList.Single(x => x.ObjId == "player").NameText);
        public static PlayerCreationCatalog Load() => new PlayerCreationCatalog(new cfg.Tables(name =>
        {
            var asset = Resources.Load<TextAsset>("Luban/" + name);
            if (asset == null) throw new InvalidOperationException("Missing generated table: " + name);
            return JArray.Parse(asset.text);
        }));
        public PlayerCreationCatalog(cfg.Tables tables)
        {
            this.tables = tables;
            var config = tables.TbPlayerConfig.DataList.Single();
            if (config.PlayerId != "player" || config.BaseStartingMoney < 0 || config.ActionPointMax < 0
                || !Enum.TryParse(config.DefaultAvatar, out AvatarKind avatar)) throw new InvalidOperationException("Invalid PlayerConfig.");
            DefaultAvatar = avatar; DefaultOriginId = config.DefaultOriginId;
            BaseStartingMoney = config.BaseStartingMoney; ActionPointMax = config.ActionPointMax;
            Attributes = tables.TbPlayerAttribute.DataList.OrderBy(x => x.Seq).ToList();
            Groups = tables.TbCreationGroup.DataList.OrderBy(x => x.Seq).ToList();
            Options = tables.TbCreationOption.DataList.ToList(); Bonuses = tables.TbCreationBonus.DataList.ToList();
            Validate();
        }
        public string Text(string key)
        {
            var row = tables.TbText.DataList.FirstOrDefault(x => x.Key == key);
            if (row == null) throw new InvalidOperationException("Missing Text: " + key);
            return row.Text;
        }
        public List<cfg.CreationOptionEntry> ForGroup(string id) => Options.Where(x => x.GroupId == id).OrderBy(x => x.Seq).ToList();
        public string Effect(cfg.CreationOptionEntry option)
        {
            var parts = new List<string>();
            if (option.StartingMoney >= 0) parts.Add("初始资金 " + option.StartingMoney);
            if (!string.IsNullOrEmpty(option.OriginId)) parts.Add("身份“" + Text("player.origin." + option.OriginId) + "”");
            if (!string.IsNullOrEmpty(option.SpecializationConditionKey))
                parts.Add(Text(tables.TbNcsConditionKey.DataList.Single(x => x.Key == option.SpecializationConditionKey).NameText));
            foreach (var bonus in Bonuses.Where(x => x.OptionId == option.OptionId))
                parts.Add(Text(Attributes.Single(x => x.AttributeId == bonus.AttributeId).NameText) + (bonus.Delta >= 0 ? "+" : "") + bonus.Delta);
            return string.Join("，", parts);
        }
        public string Background(IEnumerable<string> ids)
        {
            var selected = ids.Select(id => Options.Find(x => x.OptionId == id)).Where(x => x != null).ToList();
            if (selected.Count != Groups.Count) return "";
            return string.Format(Text("player.background.template"), Groups.Select(g => Text(selected.Single(x => x.GroupId == g.GroupId).LabelText)).Cast<object>().ToArray());
        }
        public PlayerCreationPlan Prepare(FirstDayLetterDraft draft)
        {
            if (draft == null || !draft.IsComplete) throw new InvalidOperationException("Complete all required creation choices; deleted options must be selected again.");
            var selected = draft.OptionIds.Select(id => Options.Single(x => x.OptionId == id)).ToList();
            var values = new Dictionary<string, int>();
            foreach (var reward in Bonuses.Where(x => draft.OptionIds.Contains(x.OptionId)))
                values[reward.AttributeId] = checked((values.TryGetValue(reward.AttributeId, out int old) ? old : 0) + reward.Delta);
            var origin = selected.Single(x => x.GroupId == "origin");
            return new PlayerCreationPlan(draft.PlayerName, origin.OriginId, origin.StartingMoney < 0 ? BaseStartingMoney : origin.StartingMoney,
                selected.Select(x => x.OptionId), values, selected.FirstOrDefault(x => !string.IsNullOrEmpty(x.SpecializationConditionKey))?.SpecializationConditionKey ?? "");
        }
        private void Validate()
        {
            void Require(bool valid, string error) { if (!valid) throw new InvalidOperationException("Player configuration: " + error); }
            string[] required = { "origin", "temperament", "memory", "education", "trait" };
            Require(Groups.Count == 5 && required.All(id => Groups.Any(g => g.GroupId == id && g.Required)), "missing required group");
            Require(Groups.Select(g => g.Seq).Distinct().Count() == Groups.Count, "duplicate group sequence");
            Require(Attributes.Count == 4 && new[] { "language", "courage", "wisdom", "perception" }.All(id => Attributes.Any(a => a.AttributeId == id)), "missing formal attribute");
            foreach (var a in Attributes)
            {
                Require(tables.TbNcsBar.DataList.Any(b => b.OwnerId + "." + b.BarId == a.BarKey && (string.IsNullOrEmpty(b.SourceMode) || b.SourceMode == "ncs") && b.AdvanceMode != "derived"), "unknown/internal attribute Bar " + a.BarKey);
                Text(a.NameText); Text(a.DescriptionText);
            }
            foreach (var g in Groups)
            {
                var choices = ForGroup(g.GroupId);
                Require(choices.Count > 0 && choices.Select(x => x.Seq).Distinct().Count() == choices.Count, "empty group/duplicate sequence " + g.GroupId);
            }
            foreach (var o in Options)
            {
                Require(Groups.Any(g => g.GroupId == o.GroupId), "unknown group " + o.OptionId);
                Text(o.LabelText); Text(o.DescriptionText);
                Require(o.StartingMoney >= -1, "illegal money " + o.OptionId);
                Require(o.GroupId == "origin" || (string.IsNullOrEmpty(o.OriginId) && o.StartingMoney == -1), "ambiguous origin/money " + o.OptionId);
                Require(string.IsNullOrEmpty(o.SpecializationConditionKey) || (o.GroupId == "education" && tables.TbNcsConditionKey.DataList.Any(c => c.Key == o.SpecializationConditionKey)), "invalid specialization " + o.OptionId);
            }
            var origins = ForGroup("origin");
            Require(origins.Count == 3 && new[] { "waif", "commoner", "noble" }.All(id => origins.Count(o => o.OriginId == id) == 1), "invalid origins");
            int Money(string id) => origins.Single(o => o.OriginId == id).StartingMoney;
            Require(Money("waif") >= 0 && Money("waif") < Money("commoner") && Money("commoner") < Money("noble"), "origin money order");
            Require(origins.Any(o => o.OriginId == DefaultOriginId), "unknown default origin");
            Require(Bonuses.Select(b => b.OptionId + "/" + b.AttributeId).Distinct().Count() == Bonuses.Count, "duplicate bonus");
            foreach (var b in Bonuses) Require(Options.Any(o => o.OptionId == b.OptionId) && Attributes.Any(a => a.AttributeId == b.AttributeId), "unknown bonus reference");
        }
    }
}
