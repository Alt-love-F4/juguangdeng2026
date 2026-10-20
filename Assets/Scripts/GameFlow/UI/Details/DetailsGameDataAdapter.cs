using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    public static class DetailsGameDataAdapter
    {
        public static DetailsPanelData Create()
        {
            var playerRuntime = PlayerRuntime.Current ?? throw new InvalidOperationException("Player session is not ready.");
            var runtime = NarrativeRuntime.Instance;
            var configuration = runtime != null && runtime.IsReady ? runtime.Configuration : LubanNarrativeLoader.Load();
            var state = runtime != null && runtime.IsReady ? runtime.Controller.State : null;
            var display = Resources.Load<DetailsDisplayConfig>("DetailsDisplayConfig");
            var dayFlow = UnityEngine.Object.FindFirstObjectByType<DayFlowController>();
            bool firstSessionGuide = dayFlow != null && dayFlow.IsFirstSessionGuideActive;
            string Text(string key) => string.IsNullOrEmpty(key) ? string.Empty : runtime != null && runtime.IsReady
                ? runtime.ResolveText(key) : configuration.texts.Find(t => t.key == key)?.content ?? key;
            string DisplayText(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return string.Empty;
                var row = configuration.texts.Find(item => item.key == value);
                return row != null ? Text(value) : value;
            }
            Sprite Avatar(NarrativeObjectDefinition obj)
            {
                return LoadSprite(string.IsNullOrWhiteSpace(obj.avtarPath) ? obj.iconPath : obj.avtarPath);
            }
            var player = configuration.objects.Find(o => o.objType == NarrativeObjectType.Player);
            string playerId = player != null ? player.objId : "player";
            string BarName(NarrativeBarDefinition bar)
            {
                switch (bar.barKey)
                {
                    case "world.stability": return "世界稳定度";
                    case "faction.neutral": return "经营与中立倾向";
                    case "faction.rose": return "新玫瑰倾向";
                    case "faction.church": return "王庭与教会倾向";
                    case "faction.revolution": return "革命联合倾向";
                    default:
                        if (bar.barType == NarrativeBarType.Relation)
                        {
                            string relatedId = bar.ownerId == playerId ? bar.targetId : bar.ownerId;
                            var related = configuration.objects.Find(item => item.objId == relatedId);
                            string relatedName = related != null ? Text(related.nameText) : relatedId;
                            return relatedName + "好感";
                        }
                        string localized = Text(bar.nameText);
                        return string.IsNullOrWhiteSpace(localized) ? bar.barKey : localized;
                }
            }
            DetailsProgressData Progress(NarrativeBarDefinition bar, string fallbackDescription = null)
            {
                var extra = display != null ? display.progress.Find(p => p.barKey == bar.barKey) : null;
                string visibility = state?.GetObjectState("details.bar." + bar.barKey);
                if (visibility == "hidden" || (visibility != "visible" && extra != null && !extra.initiallyVisible)) return null;
                if (state == null || !state.TryGetBar(bar.barKey, out int value)) return null;
                string description = DisplayText(extra?.description);
                if (string.IsNullOrWhiteSpace(description)) description = fallbackDescription
                    ?? (bar.barType == NarrativeBarType.Relation
                        ? "你与对方之间的关系。"
                        : "王都局势正在发生的变化。");
                string lowerDescription = DisplayText(extra?.lowerDescription);
                string upperDescription = DisplayText(extra?.upperDescription);
                return new DetailsProgressData(bar.barKey, BarName(bar), value, bar.min, bar.max,
                    bar.min + " · " + (string.IsNullOrWhiteSpace(lowerDescription) ? "下限" : lowerDescription),
                    bar.max + " · " + (string.IsNullOrWhiteSpace(upperDescription) ? "上限" : upperDescription), description);
            }
            List<DetailsProgressData> CharacterBars(string characterId)
            {
                var list = new List<DetailsProgressData>();
                foreach (var bar in configuration.bars
                    .Where(item => item.ownerId == characterId
                        || item.barType == NarrativeBarType.Relation && item.ownerId == playerId && item.targetId == characterId)
                    .OrderBy(item => item.barType == NarrativeBarType.Relation
                        && (item.ownerId == characterId && item.targetId == playerId || item.ownerId == playerId && item.targetId == characterId) ? 0 : 1))
                {
                    var progress = Progress(bar, bar.barType == NarrativeBarType.Relation
                        ? "对方对你的好感，会随你的言行与选择而改变。"
                        : null);
                    if (progress != null) list.Add(progress);
                }
                return list;
            }
            var characters = new List<DetailsCharacterData>();
            foreach (var obj in configuration.objects.Where(o => o.objType == NarrativeObjectType.Npc))
            {
                var entry = display != null ? display.characters.Find(c => c.objectId == obj.objId) : null;
                string unlock = state?.GetObjectState("details.character." + obj.objId);
                bool known = state != null && state.TryGetCondition("known." + obj.objId, out bool knownValue) && knownValue;
                bool tutorialAdele = obj.objId == "adele" && firstSessionGuide;
                bool initiallyUnlocked = entry != null && entry.initiallyUnlocked;
                bool unlocked = unlock == "locked" ? false
                    : unlock == "unlocked" || known || initiallyUnlocked || tutorialAdele;
                var relationshipDefinitions = configuration.bars.Where(bar => bar.barType == NarrativeBarType.Relation
                    && (bar.ownerId == obj.objId && bar.targetId == playerId
                        || bar.ownerId == playerId && bar.targetId == obj.objId)).ToList();
                if (!unlocked && relationshipDefinitions.Count == 0) continue;
                var characterBars = CharacterBars(obj.objId);
                string relationshipSummary;
                // Tutorial display only: it never writes known.adele.
                if (tutorialAdele && !known)
                    relationshipSummary = "初来乍到：阿黛尔正带你熟悉店里的工作。";
                else if (known || unlocked)
                    relationshipSummary = "已相识。";
                else relationshipSummary = "尚未结识。";
                if (unlocked && state != null)
                {
                    foreach (var relation in relationshipDefinitions)
                    {
                        if (!state.TryGetBar(relation.barKey, out int value)) continue;
                        relationshipSummary += "\n好感：" + value.ToString("+0;-0;0")
                            + "（" + relation.min + " ~ " + relation.max + "）";
                    }
                }
                characters.Add(new DetailsCharacterData(obj.objId, Text(obj.nameText), Text(obj.descText), Avatar(obj),
                    unlocked, characterBars, relationshipSummary));
            }
            var attributes = new List<DetailsAttributeData>();
            foreach (var attribute in playerRuntime.Catalog.Attributes)
            {
                if (playerRuntime.TryReadAttribute(attribute.AttributeId, out int value) != PlayerReadStatus.Success)
                    throw new InvalidOperationException("Player attribute unavailable: " + attribute.AttributeId);
                int kind = Array.IndexOf(FirstDayLetterDraft.AttributeKeys, attribute.AttributeId);
                attributes.Add(new DetailsAttributeData((DetailsAttributeKind)kind, value, playerRuntime.Catalog.Text(attribute.DescriptionText)));
            }
            bool HasSpecialization(string id)
            {
                var status = playerRuntime.TryReadSpecialization(id, out bool value);
                if (status != PlayerReadStatus.Success) throw new InvalidOperationException("Specialization unavailable: " + id);
                return value;
            }
            var badges = new List<DetailsSpecializationData>();
            if (display != null) foreach (var specialization in display.specializations)
                if (HasSpecialization(specialization.id))
                    badges.Add(new DetailsSpecializationData(Text(specialization.displayName), Text(specialization.description), specialization.icon));
            for (int i = 0; i < FirstDayLetterDraft.SpecializationIds.Length; i++)
                if (HasSpecialization(FirstDayLetterDraft.SpecializationIds[i])
                    && (display == null || !display.specializations.Exists(item => item.id == FirstDayLetterDraft.SpecializationIds[i])))
                    badges.Add(new DetailsSpecializationData(FirstDayLetterDraft.SpecializationNames[i], "在北港学院研读相关学业，获得的知识专精。", null));
            Sprite avatar = player == null ? null : Avatar(player);
            if (display != null)
            {
                Sprite selected = playerRuntime.Avatar == AvatarKind.Female ? display.femaleAvatar : display.maleAvatar;
                if (selected != null) avatar = selected;
            }
            string background = playerRuntime.Background;
            if (string.IsNullOrWhiteSpace(background)) background = player == null ? "尚未记录主角背景。" : Text(player.descText);
            string playerName = playerRuntime.PlayerName;
            if (string.IsNullOrWhiteSpace(playerName)) playerName = player == null ? "咖啡馆馆长" : Text(player.nameText);
            var worldProgress = new List<DetailsProgressData>();
            var stabilityBar = configuration.bars.Find(bar => bar.barKey == "world.stability");
            if (stabilityBar != null)
            {
                var progress = Progress(stabilityBar,
                    "王都局势的安稳程度。数值越低，街头越动荡，各方势力也越可能铤而走险。");
                if (progress != null) worldProgress.Add(progress);
            }
            // Faction profiles appear only after the story has introduced them; nothing here writes facts.
            int knownFactions = 0;
            foreach (var faction in WorldFactionCatalog.All)
            {
                if (!WorldFactionCatalog.IsEncountered(faction, state)) continue;
                knownFactions++;
                var bar = configuration.bars.Find(item => item.barKey == faction.TendencyBarKey);
                int value = 0;
                bool hasValue = bar != null && state != null && state.TryGetBar(bar.barKey, out value);
                worldProgress.Add(new DetailsProgressData("faction.profile." + faction.Id, faction.Name, hasValue ? value : 0,
                    hasValue ? bar.min : 0, hasValue ? bar.max : 0,
                    hasValue ? bar.min + " · 疏远" : "", hasValue ? bar.max + " · 亲近" : "", faction.Summary,
                    WorldFactionCatalog.Emblem(faction), faction.Epithet + "　·　" + faction.Leader, faction.TendencyName, hasValue));
            }
            string WorldOverview()
            {
                if (state == null) return "暂时还没有关于王都的消息。";
                int day = state.TryGetBar("world.calendar", out int currentDay) ? currentDay : runtime?.Actions?.CurrentDay ?? 0;
                var lines = new List<string>();
                if (day > 0) lines.Add(FormatWorldDate(day) + " · 第 " + day + " 日");
                var status = new List<string>();
                if (state.TryGetBar("world.stability", out int stable)) status.Add("世界稳定度 " + stable + " / 10");
                if (state.TryGetCondition("church.detention.active", out bool detained)) status.Add(detained ? "你正被拘押" : "未被拘押");
                if (status.Count > 0) lines.Add(string.Join(" · ", status));
                var routes = new List<string>();
                foreach (var route in new[]
                {
                    new KeyValuePair<string, string>("route.neutral", "中立 / 经营"),
                    new KeyValuePair<string, string>("route.rose", "新玫瑰"),
                    new KeyValuePair<string, string>("route.church", "王庭与教会"),
                    new KeyValuePair<string, string>("route.revolution", "革命联合")
                })
                    if (state.TryGetCondition(route.Key, out bool active) && active) routes.Add(route.Value);
                lines.Add("当前倾向：" + (routes.Count == 0 ? "尚未表明立场" : string.Join("、", routes)));
                lines.Add("已知势力 " + knownFactions + " 个");
                return string.Join("\n", lines);
            }
            var world = new DetailsSectionData("世界概况", "世界概况", WorldOverview());
            return new DetailsPanelData(new DetailsProfileData(playerName, background,
                "你接手叔叔留下的咖啡馆，以咖啡与倾听接待来客。", avatar), attributes, badges,
                new DetailsSectionData("人物关系", "当前可查看 " + characters.Count(item => item.Unlocked) + " 人",
                    "相识状态由剧情记录驱动。悬停人物卡片查看介绍与实际关系数值，尚未解锁的人物不会泄露关系值。"),
                world, characters: characters, worldBars: worldProgress);
        }

        private static string FormatWorldDate(int day)
        {
            if (day <= 0) return "日期不可用";
            try
            {
                DateTime date = new DateTime(670, 10, 6).AddDays(day - 1);
                return date.Year + "年" + date.Month + "月" + date.Day + "日";
            }
            catch (ArgumentOutOfRangeException) { return "王国纪元 · 第 " + day + " 日"; }
        }

        private static Sprite LoadSprite(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            string key = path.Replace('\\', '/');
            int resources = key.IndexOf("Resources/", StringComparison.Ordinal);
            if (resources >= 0) key = key.Substring(resources + 10);
            int extension = key.LastIndexOf('.');
            if (extension >= 0) key = key.Substring(0, extension);
            return Resources.Load<Sprite>(key);
        }
    }
}
