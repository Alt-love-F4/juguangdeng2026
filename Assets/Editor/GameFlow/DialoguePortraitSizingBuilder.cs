using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Spotlight.GameFlow;
using UnityEditor;
using UnityEngine;

namespace Spotlight.EditorTools
{
    public static class DialoguePortraitSizingBuilder
    {
        public const string AssetPath = "Assets/Resources/Dialogue/DialoguePortraitSizing.asset";

        [MenuItem("Tools/Game Flow/Refresh Dialogue Portrait Sizing")]
        public static void Refresh()
        {
            var table = AssetDatabase.LoadAssetAtPath<DialoguePortraitSizing>(AssetPath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<DialoguePortraitSizing>();
                AssetDatabase.CreateAsset(table, AssetPath);
            }
            Undo.RecordObject(table, "Refresh dialogue portrait alpha bounds");
            var objects = new cfg.TbNcsObject(JArray.Parse(Resources.Load<TextAsset>("Luban/tbncsobject").text));
            var texts = new cfg.TbText(JArray.Parse(Resources.Load<TextAsset>("Luban/tbtext").text));
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var text in texts.DataList) names[text.Key] = text.Text;
            var bounds = new Dictionary<Sprite, Rect>();
            foreach (var person in objects.DataList)
            {
                if (string.Equals(person.ObjType, "world", StringComparison.OrdinalIgnoreCase)) continue;
                string name = names.TryGetValue(person.NameText ?? "", out var value) ? value : person.ObjId;
                Add(person.ObjId, name, person.PortraitPath);
                Add(person.ObjId + ":indoor", name + " / 室内", person.PortraitIndoorPath);
            }
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log("Dialogue portrait sizing: " + table.entries.Count + " entries. Existing height scales and offsets preserved.");

            void Add(string id, string name, string path)
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite == null) return;
                var entry = table.entries.Find(e => e != null && e.characterId == id && e.sprite == sprite);
                if (entry == null)
                {
                    entry = new DialoguePortraitSizing.Entry { characterId = id, sprite = sprite };
                    table.entries.Add(entry);
                }
                entry.displayName = name;
                if (!bounds.TryGetValue(sprite, out var rect))
                {
                    rect = AlphaBounds(sprite);
                    bounds.Add(sprite, rect);
                }
                entry.visibleRect = rect;
            }
        }

        private static Rect AlphaBounds(Sprite sprite)
        {
            // Decode source PNG independently: no Read/Write importer changes or GPU readback.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))))
                    throw new InvalidOperationException("Cannot decode portrait: " + sprite.name);
                var pixels = texture.GetPixels32();
                Rect spriteRect = sprite.rect;
                // Sprite rect is in imported texture pixels; source can have a higher resolution.
                float sx = (float)texture.width / sprite.texture.width;
                float sy = (float)texture.height / sprite.texture.height;
                int x0 = Mathf.RoundToInt(spriteRect.xMin * sx), y0 = Mathf.RoundToInt(spriteRect.yMin * sy);
                int w = Mathf.RoundToInt(spriteRect.width * sx), h = Mathf.RoundToInt(spriteRect.height * sy);
                int minX = w, minY = h, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (pixels[(y0 + y) * texture.width + x0 + x].a < 8) continue;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
                return maxY < minY ? new Rect(0, 0, 1, 1)
                    : new Rect((float)minX / w, (float)minY / h, (float)(maxX - minX + 1) / w, (float)(maxY - minY + 1) / h);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
