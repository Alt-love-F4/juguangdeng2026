using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>One-page-per-object preview of configured character portraits, independent of narrative playback.</summary>
    public static class PresentationDebugCharacterGallery
    {
        public const string Id = "characters:all";
        private const string Title = "全人物立绘巡览";

        public static DialogueScript Create()
        {
            TextAsset objectsAsset = Resources.Load<TextAsset>("Luban/tbncsobject");
            TextAsset textsAsset = Resources.Load<TextAsset>("Luban/tbtext");
            if (objectsAsset == null) throw new InvalidOperationException("Missing Resources/Luban/tbncsobject.json.");
            if (textsAsset == null) throw new InvalidOperationException("Missing Resources/Luban/tbtext.json.");
            var objects = new cfg.TbNcsObject(JArray.Parse(objectsAsset.text));
            var texts = new cfg.TbText(JArray.Parse(textsAsset.text));
            return Create(objects, texts);
        }

        /// <summary>Fixture-friendly builder; uses only these generated tables and loads sprite resources on demand.</summary>
        public static DialogueScript Create(cfg.TbNcsObject objects, cfg.TbText texts)
        {
            if (objects == null) throw new ArgumentNullException(nameof(objects));
            if (texts == null) throw new ArgumentNullException(nameof(texts));

            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (cfg.TextEntry text in texts.DataList)
                if (text != null && !string.IsNullOrEmpty(text.Key) && !names.ContainsKey(text.Key))
                    names.Add(text.Key, text.Text ?? string.Empty);

            var roster = objects.DataList
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ObjId)
                    && !string.Equals(item.ObjType, "world", StringComparison.OrdinalIgnoreCase))
                .GroupBy(item => item.ObjId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(item => item.ObjId, StringComparer.Ordinal)
                .ToList();
            if (roster.Count == 0) throw new InvalidOperationException("Character gallery has no configured character image paths.");

            var spriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            var lines = new List<DialogueLine>(roster.Count);
            DialogueCharacter blank = new DialogueCharacter(string.Empty, string.Empty, null);
            for (int i = 0; i < roster.Count; i++)
            {
                cfg.NcsObjectEntry item = roster[i];
                string displayName = names.TryGetValue(item.NameText ?? string.Empty, out string resolvedName)
                    && !string.IsNullOrWhiteSpace(resolvedName) ? resolvedName
                    : string.IsNullOrWhiteSpace(item.NameText) ? item.ObjId : item.NameText;
                LoadFirst(spriteCache, item.PortraitPath, item.IconPath, item.AvtarPath, out Sprite portrait, out string triedPath);
                string indoorPath = string.IsNullOrWhiteSpace(item.PortraitIndoorPath) ? null : item.PortraitIndoorPath;
                Sprite indoor = Load(spriteCache, indoorPath);

                var person = new DialogueCharacter(item.ObjId, displayName, portrait);
                DialogueCharacter indoorCharacter = indoor != null && indoor != portrait
                    ? new DialogueCharacter(item.ObjId + ":indoor", displayName, indoor)
                    : null;
                string diagnostic = portrait == null
                    ? "；立绘缺失" + (string.IsNullOrEmpty(triedPath) ? "（未配置资源路径）" : "（资源未找到：" + triedPath + "）")
                    : string.Empty;
                if (!string.IsNullOrEmpty(indoorPath) && indoor == null)
                    diagnostic += "；室内立绘缺失（资源未找到：" + indoorPath + "）";
                string lineText = (i + 1) + "/" + roster.Count + "  " + displayName + "（" + item.ObjId + "）" + diagnostic + "\n点击下一页继续";
                lines.Add(new DialogueLine(DialogueSpeaker.Left, lineText, speakerNameOverride: displayName,
                    replaceLeft: true, leftCharacter: person,
                    replaceRight: true, rightCharacter: indoorCharacter,
                    replaceCenter: true, centerCharacter: null));
            }

            return new DialogueScript(lines, blank, blank, title: Title, isPreview: true, instantPortraitPreview: true);
        }

        private static void LoadFirst(Dictionary<string, Sprite> cache, string first, string second, string third,
            out Sprite sprite, out string lastTriedPath)
        {
            lastTriedPath = null;
            foreach (string path in new[] { first, second, third })
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                lastTriedPath = path;
                sprite = Load(cache, path);
                if (sprite != null) return;
            }
            sprite = null;
        }

        private static Sprite Load(Dictionary<string, Sprite> cache, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (!cache.TryGetValue(path, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>(path);
                cache.Add(path, sprite);
            }
            return sprite;
        }
    }
}
