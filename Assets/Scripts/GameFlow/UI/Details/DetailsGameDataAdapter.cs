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
                    if (state == null || !state.TryGetBar(bar.barKey, out int value))
                        throw new InvalidOperationException("Details value unavailable: " + bar.barKey);
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
