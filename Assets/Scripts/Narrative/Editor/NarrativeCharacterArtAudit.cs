using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Spotlight.Narrative.Editor
{
    /// <summary>Read-only view of the same generated Object paths used by CharacterCatalog.LoadFromTables.</summary>
    public sealed class NarrativeCharacterArtAudit
    {
        public sealed class ResourceStatus
        {
            public string Path, AssetPath, Error, Provenance;
            public Sprite Sprite;
            public bool Placeholder;
            public bool Available => Sprite != null;
        }

        public sealed class CharacterStatus
        {
            public string Id, Name;
            public string Tags;
            public NarrativeObjectType ObjectType;
            public bool ObjectExists, CafeCustomer;
            public bool NoPortrait;
            public ResourceStatus Portrait, Avatar, Indoor;
            public int ConversationLines, ReadingLines, OnstageConversationLines, OffstageConversationLines;
            public string[] FallbackEmotes = Array.Empty<string>();
            public string[] UsageExamples = Array.Empty<string>();
            public int LineCount => ConversationLines + ReadingLines;
            public bool CanAppearOnstage => OnstageConversationLines > 0;
        }

        public sealed class GuestVariantCounts
        {
            public GuestVariantPool[] Pools = Array.Empty<GuestVariantPool>();
            public int Portraits => Pools.Sum(pool => pool.Portraits);
            public int Avatars => Pools.Sum(pool => pool.Avatars);
            public int Total => Portraits + Avatars;
        }

        public sealed class GuestVariantPool
        {
            public string GuestId;
            public int Portraits, Avatars;
            public int Total => Portraits + Avatars;
        }

        [Serializable] private sealed class ProvenanceManifest { public ProvenanceEntry[] characters; }
        [Serializable] private sealed class ProvenanceEntry
        {
            public string id, source, portraitPath, avtarPath, portraitIndoorPath;
            public bool portraitPlaceholder, seatedPlaceholder;
        }

        private readonly NarrativeConfiguration configuration;
        private readonly Dictionary<string, ProvenanceEntry> provenance;
        private readonly Dictionary<string, CharacterStatus> cache = new Dictionary<string, CharacterStatus>(StringComparer.Ordinal);

        public static NarrativeCharacterArtAudit Load()
        {
            const string path = "Tools/Characters/character-art-manifest.json";
            // This inventory describes authored/placeholder provenance; it is never a runtime configuration source.
            var entries = File.Exists(path)
                ? JsonUtility.FromJson<ProvenanceManifest>("{\"characters\":" + File.ReadAllText(path) + "}").characters
                : Array.Empty<ProvenanceEntry>();
            return new NarrativeCharacterArtAudit(LubanNarrativeLoader.Load(), entries);
        }

        public NarrativeCharacterArtAudit(NarrativeConfiguration configuration) : this(configuration, Array.Empty<ProvenanceEntry>()) { }
        private NarrativeCharacterArtAudit(NarrativeConfiguration configuration, IEnumerable<ProvenanceEntry> entries)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            provenance = (entries ?? Array.Empty<ProvenanceEntry>()).ToDictionary(e => e.id, StringComparer.Ordinal);
        }

        public CharacterStatus[] Inspect(NarrativeArtPlaceholderBuilder.Requirement requirement)
        {
            // Only explicit manifest evidence links a requirement to Object identities. Never merge by display name.
            return (requirement.objectEvidence ?? Array.Empty<NarrativeArtPlaceholderBuilder.ObjectEvidence>())
                .Where(e => !string.IsNullOrWhiteSpace(e?.objId)).Select(e => e.objId).Distinct(StringComparer.Ordinal)
                .Select(Inspect).ToArray();
        }

        public CharacterStatus Inspect(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Object ID is required.", nameof(id));
            if (cache.TryGetValue(id, out var cached)) return cached;
            var row = configuration.objects.Find(o => o.objId == id);
            var status = new CharacterStatus { Id = id, ObjectExists = row != null,
                Name = row == null ? id : configuration.texts.Find(t => t.key == row.nameText)?.content ?? row.nameText,
                ObjectType = row != null ? row.objType : NarrativeObjectType.Npc,
                Tags = row?.tags ?? string.Empty,
                NoPortrait = row != null && CharacterCatalog.HasTag(row.tags, "no_portrait"),
                CafeCustomer = row != null && row.objType == NarrativeObjectType.Npc && CharacterCatalog.HasTag(row.tags, "cafe_customer") };
            provenance.TryGetValue(id, out var record);
            status.Portrait = Read(row?.portraitPath, id, record, false);
            status.Avatar = Read(string.IsNullOrWhiteSpace(row?.avtarPath) ? row?.iconPath : row.avtarPath, id, record, false);
            // Same compatibility fallback as CharacterCatalog; do not infer seated art from the file name alone.
            status.Indoor = Read(string.IsNullOrWhiteSpace(row?.portraitIndoorPath) ? row?.portraitPath : row.portraitIndoorPath, id, record, true);
            var conversations = configuration.presentations.conversationLines.Where(l => l.speakerKey == id).ToArray();
            var readings = configuration.presentations.readingLines.Where(l => l.speakerKey == id).ToArray();
            status.ConversationLines = conversations.Length;
            status.OffstageConversationLines = conversations.Count(l => string.Equals(l.side, "none", StringComparison.OrdinalIgnoreCase));
            status.OnstageConversationLines = status.ConversationLines - status.OffstageConversationLines;
            status.ReadingLines = readings.Length;
            // LoadFromTables currently provides only the base portrait. A named PNG sitting in a folder is not a wired expression.
            status.FallbackEmotes = conversations.Select(l => l.emote).Where(e => !IsBaseEmote(e))
                .Distinct(StringComparer.Ordinal).OrderBy(e => e, StringComparer.Ordinal).ToArray();
            status.UsageExamples = conversations.Select(l => "Conversation / " + l.lineId + " [" + (string.Equals(l.side, "none", StringComparison.OrdinalIgnoreCase) ? "none · 不引入立绘" : (string.IsNullOrWhiteSpace(l.side) ? "right" : l.side)) + "]")
                .Concat(readings.Select(l => "Reading / " + l.lineId)).Take(8).ToArray();
            cache.Add(id, status);
            return status;
        }

        public CharacterStatus[] InspectAllObjects()
        {
            return configuration.objects.Where(o => o != null && !string.IsNullOrWhiteSpace(o.objId))
                .Select(o => Inspect(o.objId)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        }

        public CharacterStatus[] InspectAllNpcs()
        {
            return configuration.objects.Where(o => o != null && o.objType == NarrativeObjectType.Npc
                    && !string.IsNullOrWhiteSpace(o.objId))
                .Select(o => Inspect(o.objId)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        }

        public string[] MissingOnstageNpcPortraitIds()
        {
            return InspectAllNpcs().Where(c => !c.NoPortrait && c.CanAppearOnstage && !c.Portrait.Available)
                .Select(c => c.Id).ToArray();
        }

        public static GuestVariantCounts CountGuestVariants(string resourcesRoot = "Assets/Resources/Characters")
        {
            var result = new GuestVariantCounts
            {
                Pools = new[] { "guest_a", "guest_b", "guest_c", "guest_d" }
                    .Select(id => CountGuestPool(resourcesRoot, id)).ToArray()
            };
            return result;
        }

        private static GuestVariantPool CountGuestPool(string resourcesRoot, string guestId)
        {
            var result = new GuestVariantPool { GuestId = guestId };
            string variantsDirectory = Path.Combine(resourcesRoot, guestId, "variants");
            if (!Directory.Exists(variantsDirectory)) return result;
            foreach (string path in Directory.GetFiles(variantsDirectory, "*.png", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(path);
                if (name.StartsWith("portrait_", StringComparison.OrdinalIgnoreCase)) result.Portraits++;
                else if (name.StartsWith("avatar_", StringComparison.OrdinalIgnoreCase)) result.Avatars++;
            }
            return result;
        }

        private static bool IsBaseEmote(string emote) => string.IsNullOrWhiteSpace(emote)
            || string.Equals(emote, "neutral", StringComparison.OrdinalIgnoreCase)
            || string.Equals(emote, "normal", StringComparison.OrdinalIgnoreCase)
            || emote == "平静" || emote == "常态";

        private static ResourceStatus Read(string path, string id, ProvenanceEntry record, bool indoor)
        {
            var result = new ResourceStatus { Path = path ?? "" };
            if (string.IsNullOrWhiteSpace(path)) { result.Error = "Object 路径为空"; return result; }
            try { result.Sprite = CharacterCatalog.LoadSprite(path, id); }
            catch (InvalidOperationException ex) { result.Error = ex.Message; }
            result.AssetPath = result.Sprite == null ? "" : AssetDatabase.GetAssetPath(result.Sprite);
            bool portraitOrAvatar = record != null && (path == record.portraitPath || path == record.avtarPath);
            bool seated = record != null && path == record.portraitIndoorPath;
            result.Placeholder = (portraitOrAvatar && record.portraitPlaceholder) || (seated && record.seatedPlaceholder)
                || path.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0;
            result.Provenance = result.Placeholder ? "占位素材"
                : portraitOrAvatar && !string.IsNullOrEmpty(record.source) ? "已有原画导出（非美术验收）"
                : indoor && path == record?.portraitPath ? "沿用基础立绘" : "已加载（美术状态待核）";
            return result;
        }

        public static string Summary(CharacterStatus[] characters)
        {
            if (characters.Length == 0) return "无明确角色映射";
            var required = characters.Where(c => !c.NoPortrait).ToArray();
            if (required.Length == 0) return "全部标记 no_portrait";
            int loaded = required.Count(c => c.Portrait.Available);
            if (loaded == 0) return "基础未配置";
            if (loaded != required.Length) return required.Any(c => !c.Portrait.Available && c.CanAppearOnstage)
                ? "部分剧情 ID 缺图" : "部分 ID 未配置（未登场）";
            return required.Any(c => c.Portrait.Placeholder) ? "基础占位已配置" : "基础已配置";
        }
    }
}
