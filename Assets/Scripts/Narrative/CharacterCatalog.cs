using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Narrative
{
    /// <summary>
    /// 手动配置的角色资源目录。字段刻意使用稳定 characterId，未来可由 NCS Object 表生成同样的运行时数据。
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
