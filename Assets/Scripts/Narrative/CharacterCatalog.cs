using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Narrative
{
    /// <summary>
    /// 配表角色的运行时资源目录；由 Luban Object/Text 构建，也可在隔离测试中 Configure。
    /// 不保存关系、好感或场景位置等运行时状态。
    /// </summary>
    [CreateAssetMenu(menuName = "Spotlight/Characters/Character Catalog", fileName = "CharacterCatalog")]
    public sealed class CharacterCatalog : ScriptableObject
    {
        [SerializeField] private List<CharacterProfile> characters = new List<CharacterProfile>();
        public IReadOnlyList<CharacterProfile> Characters => characters;

        public void Configure(IEnumerable<CharacterProfile> profiles)
        {
            var next = new List<CharacterProfile>(profiles ?? throw new ArgumentNullException(nameof(profiles)));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var profile in next)
                if (profile == null || string.IsNullOrWhiteSpace(profile.characterId) || string.IsNullOrWhiteSpace(profile.displayName) || !ids.Add(profile.characterId))
                    throw new ArgumentException("角色需要唯一 ID 和显示名。", nameof(profiles));
            characters = next;
        }

        public void Validate()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var profile in characters)
                if (profile == null || string.IsNullOrWhiteSpace(profile.characterId) || string.IsNullOrWhiteSpace(profile.displayName) || !ids.Add(profile.characterId))
                    throw new InvalidOperationException("角色目录存在空角色、缺少名字或重复 ID。" );
        }

        /// <summary>运行时仅从 Luban Object/Text 创建目录；不读取手动资产作为权威数据。</summary>
        public static CharacterCatalog LoadFromTables()
        {
            var config = LubanNarrativeLoader.Load();
            var catalog = CreateInstance<CharacterCatalog>();
            catalog.hideFlags = HideFlags.HideAndDontSave;
            var profiles = new List<CharacterProfile>();
            foreach (var obj in config.objects)
            {
                if (obj.objType == NarrativeObjectType.World) continue;
                string Text(string key) => config.texts.Find(t => t.key == key)?.content ?? key;
                var portrait = LoadSprite(obj.portraitPath, obj.objId);
                var avatar = LoadSprite(string.IsNullOrWhiteSpace(obj.avtarPath) ? obj.iconPath : obj.avtarPath, obj.objId);
                var indoor = string.IsNullOrWhiteSpace(obj.portraitIndoorPath) ? portrait : LoadSprite(obj.portraitIndoorPath, obj.objId);
                profiles.Add(new CharacterProfile
                {
                    characterId = obj.objId, displayName = Text(obj.nameText), description = Text(obj.descText),
                    portrait = portrait, avatar = avatar, worldSprite = indoor, tags = obj.tags,
                    cafeCustomer = obj.objType == NarrativeObjectType.Npc && HasTag(obj.tags, "cafe_customer")
                });
            }
            catalog.Configure(profiles);
            return catalog;
        }

        public static bool HasTag(string tags, string tag)
        {
            foreach (var value in (tags ?? string.Empty).Split(','))
                if (string.Equals(value.Trim(), tag, StringComparison.Ordinal)) return true;
            return false;
        }

        public static Sprite LoadSprite(string path, string characterId)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Object " + characterId + " 引用了不存在的 Sprite: " + path);
            return sprite;
        }

        public CharacterProfile Find(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return null;
            for (int i = 0; i < characters.Count; i++)
                if (characters[i] != null && string.Equals(characters[i].characterId, characterId, StringComparison.Ordinal))
                    return characters[i];
            return null;
        }
    }

    [Serializable]
    public sealed class CharacterProfile
    {
        [Tooltip("稳定角色 ID；后续对应 NCS Object.objId。")]
        public string characterId;
        public string displayName;
        [TextArea(2, 5)] public string description;
        [Header("Dialogue")]
        public Sprite avatar;
        public Sprite portrait;
        public Sprite portraitHappy;
        public Sprite portraitAngry;
        [Header("Coffee shop")]
        public Sprite worldSprite;
        [Tooltip("用于分类和后续 NCS 标签映射。多个标签用逗号分隔。")]
        public string tags;
        public bool cafeCustomer;

        public Sprite PortraitFor(string emote)
        {
            if (string.Equals(emote, "happy", StringComparison.OrdinalIgnoreCase) && portraitHappy != null) return portraitHappy;
            if (string.Equals(emote, "angry", StringComparison.OrdinalIgnoreCase) && portraitAngry != null) return portraitAngry;
            return portrait;
        }
    }

    public interface ICharacterCatalogProvider
    {
        CharacterProfile FindCharacter(string characterId);
    }
}
