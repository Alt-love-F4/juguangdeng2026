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
            var runtime = NarrativeRuntime.Instance;
            var configuration = runtime != null && runtime.IsReady ? runtime.Configuration : LubanNarrativeLoader.Load();
            var state = runtime != null && runtime.IsReady ? runtime.Controller.State : null;
            var display = Resources.Load<DetailsDisplayConfig>("DetailsDisplayConfig");
            string Text(string key) => string.IsNullOrEmpty(key) ? string.Empty : runtime != null && runtime.IsReady
                ? runtime.ResolveText(key) : configuration.texts.Find(t => t.key == key)?.content ?? key;
            Sprite Avatar(NarrativeObjectDefinition obj)
            {
                return LoadSprite(string.IsNullOrWhiteSpace(obj.avtarPath) ? obj.iconPath : obj.avtarPath);
            }
            List<DetailsProgressData> Bars(string owner)
            {
                var list = new List<DetailsProgressData>();
                foreach (var bar in configuration.bars.Where(b => b.ownerId == owner))
                {
                    var extra = display != null ? display.progress.Find(p => p.barKey == bar.barKey) : null;
                    string visibility = state?.GetObjectState("details.bar." + bar.barKey);
                    if (visibility == "hidden" || (visibility != "visible" && extra != null && !extra.initiallyVisible)) continue;
                    int value = state != null && state.TryGetBar(bar.barKey, out int current) ? current : bar.initValue;
                    list.Add(new DetailsProgressData(bar.barKey, Text(bar.nameText), value, bar.min, bar.max,
                        bar.min + " · " + (string.IsNullOrWhiteSpace(extra?.lowerDescription) ? "下限" : Text(extra.lowerDescription)),
                        bar.max + " · " + (string.IsNullOrWhiteSpace(extra?.upperDescription) ? "上限" : Text(extra.upperDescription)),
                        string.IsNullOrWhiteSpace(extra?.description) ? "尚未配置进度条描述。" : Text(extra.description)));
                }
                return list;
            }
            var player = configuration.objects.Find(o => o.objType == NarrativeObjectType.Player);
            var characters = new List<DetailsCharacterData>();
            foreach (var obj in configuration.objects.Where(o => o.objType == NarrativeObjectType.Npc))
            {
                var entry = display != null ? display.characters.Find(c => c.objectId == obj.objId) : null;
                string unlock = state?.GetObjectState("details.character." + obj.objId);
                bool unlocked = unlock == "unlocked" || (unlock != "locked" && entry != null && entry.initiallyUnlocked);
                characters.Add(new DetailsCharacterData(obj.objId, Text(obj.nameText), Text(obj.descText), Avatar(obj), unlocked, Bars(obj.objId)));
            }
            var attributes = new List<DetailsAttributeData>();
            string[] keys = { "language", "courage", "wisdom", "perception" };
            string[] descriptions = { "影响表达、说服与沟通。", "影响面对风险与压力时的行动。", "影响分析、推理与解决问题。", "影响观察环境、察觉细节与情绪。" };
            for (int i = 0; i < 4; i++)
            {
                int value = state != null && state.TryGetBar("player." + keys[i], out int current) ? current : PlayerDetailsProfile.Current.Attributes[i];
                attributes.Add(new DetailsAttributeData((DetailsAttributeKind)i, value, descriptions[i]));
            }
            var badges = new List<DetailsSpecializationData>();
            if (display != null) foreach (var specialization in display.specializations)
                if (PlayerDetailsProfile.Current.Specializations.Contains(specialization.id) || (state?.IsFlagSet("details.specialization." + specialization.id) ?? false))
                    badges.Add(new DetailsSpecializationData(Text(specialization.displayName), Text(specialization.description), specialization.icon));
            for (int i = 0; i < FirstDayLetterDraft.SpecializationIds.Length; i++)
                if (PlayerDetailsProfile.Current.Specializations.Contains(FirstDayLetterDraft.SpecializationIds[i])
                    && (display == null || !display.specializations.Exists(item => item.id == FirstDayLetterDraft.SpecializationIds[i])))
                    badges.Add(new DetailsSpecializationData(FirstDayLetterDraft.SpecializationNames[i], "在北港学院研读相关学业，获得的知识专精。", null));
            Sprite avatar = player == null ? null : Avatar(player);
            if (display != null)
            {
                Sprite selected = PlayerDetailsProfile.Current.Avatar == AvatarKind.Female ? display.femaleAvatar : display.maleAvatar;
                if (selected != null) avatar = selected;
            }
            string background = PlayerDetailsProfile.Current.Background;
            if (string.IsNullOrWhiteSpace(background)) background = player == null ? "尚未记录主角背景。" : Text(player.descText);
            string playerName = PlayerDetailsProfile.Current.PlayerName;
            if (string.IsNullOrWhiteSpace(playerName)) playerName = player == null ? "咖啡馆馆长" : Text(player.nameText);
            return new DetailsPanelData(new DetailsProfileData(playerName, background,
                "你接手叔叔留下的咖啡馆，以咖啡与倾听接待来客。", avatar), attributes, badges,
                new DetailsSectionData("人物关系", "尚未配置人物", ""), new DetailsSectionData("世界情况", "暂无世界进度", ""),
                characters: characters, worldBars: configuration.objects.Where(o => o.objType == NarrativeObjectType.World).SelectMany(o => Bars(o.objId)));
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
