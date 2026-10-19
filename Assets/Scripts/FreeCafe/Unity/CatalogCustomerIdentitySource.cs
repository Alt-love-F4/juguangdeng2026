using System;
using System.Collections.Generic;
using System.Globalization;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>普通客流只借用通用客人外观；每次到访有独立身份，不占用剧情角色。</summary>
    public sealed class CatalogCustomerIdentitySource : ICafeAvailableCustomerIdentitySource
    {
        private readonly System.Collections.Generic.List<CharacterProfile> profiles = new System.Collections.Generic.List<CharacterProfile>();
        private readonly Dictionary<string, int[]> variantIndicesByCharacter = new Dictionary<string, int[]>(StringComparer.Ordinal);
        public CatalogCustomerIdentitySource(CharacterCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            catalog.Validate();
            foreach (string id in new[] { "guest_a", "guest_b", "guest_c", "guest_d" })
            {
                var profile = catalog.Find(id);
                if (profile == null) continue;
                profiles.Add(profile);
                variantIndicesByCharacter[id] = CafeCustomerVisualVariants.DiscoverIndices(id);
            }
        }
        public CafePersonIdentity CreateIdentity(string visitId, int seatIndex)
            => CreateIdentity(visitId, seatIndex, new System.Collections.Generic.HashSet<string>());

        public CafePersonIdentity CreateIdentity(string visitId, int seatIndex, System.Collections.Generic.ISet<string> occupiedPersonIds)
        {
            if (profiles.Count == 0) return null;
            // The guest_* keys select reusable art, not named NCS actors. No story preset is required.
            var profile = profiles[(seatIndex % profiles.Count + profiles.Count) % profiles.Count];
            variantIndicesByCharacter.TryGetValue(profile.characterId, out var variants);
            int variantIndex = CafeCustomerVisualVariants.SelectVariantIndex(visitId, variants);
            return new CafePersonIdentity
            {
                personId = "visitor_" + visitId,
                characterId = profile.characterId,
                displayName = "普通客人",
                visualId = profile.characterId,
                role = CafePersonRole.Customer,
                portraitVariantPath = CafeCustomerVisualVariants.LoadablePortraitPath(profile.characterId, variantIndex),
                avatarVariantPath = CafeCustomerVisualVariants.LoadableAvatarPath(profile.characterId, variantIndex)
            };
        }
    }

    public static class CafeCustomerVisualVariants
    {
        public static int SelectVariantIndex(string visitId, IList<int> candidates)
        {
            if (candidates == null || candidates.Count == 0) return 0;
            uint hash = StableHash(visitId ?? string.Empty);
            return candidates[(int)(hash % (uint)candidates.Count)];
        }

        public static string PortraitVariantPath(string characterId, int variantIndex)
            => VariantPath(characterId, "portrait", variantIndex);

        public static string AvatarVariantPath(string characterId, int variantIndex)
            => VariantPath(characterId, "avatar", variantIndex);

        public static Sprite ResolvePortrait(CafePersonIdentity identity, CharacterProfile profile)
        {
            var variant = Load(identity?.portraitVariantPath);
            return variant != null ? variant : profile?.PortraitFor(null);
        }

        public static Sprite ResolveAvatar(CafePersonIdentity identity, CharacterProfile profile)
        {
            var variant = Load(identity?.avatarVariantPath);
            return variant != null ? variant : profile?.avatar;
        }

        internal static int[] DiscoverIndices(string characterId)
        {
            var found = new SortedSet<int>();
            string folder = "Characters/" + characterId + "/variants";
            foreach (var sprite in Resources.LoadAll<Sprite>(folder))
            {
                if (TryReadIndex(sprite == null ? null : sprite.name, "portrait", out int portraitIndex)) found.Add(portraitIndex);
                if (TryReadIndex(sprite == null ? null : sprite.name, "avatar", out int avatarIndex)) found.Add(avatarIndex);
            }
            var result = new int[found.Count];
            found.CopyTo(result);
            return result;
        }

        internal static string LoadablePortraitPath(string characterId, int variantIndex)
        {
            string path = PortraitVariantPath(characterId, variantIndex);
            return variantIndex > 0 && Load(path) != null ? path : null;
        }

        internal static string LoadableAvatarPath(string characterId, int variantIndex)
        {
            string path = AvatarVariantPath(characterId, variantIndex);
            return variantIndex > 0 && Load(path) != null ? path : null;
        }

        private static string VariantPath(string characterId, string kind, int variantIndex)
        {
            if (string.IsNullOrWhiteSpace(characterId) || variantIndex <= 0) return null;
            return "Characters/" + characterId + "/variants/" + kind + "_"
                + variantIndex.ToString("D2", CultureInfo.InvariantCulture);
        }

        private static bool TryReadIndex(string spriteName, string kind, out int index)
        {
            index = 0;
            string prefix = kind + "_";
            if (string.IsNullOrEmpty(spriteName) || !spriteName.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string suffix = spriteName.Substring(prefix.Length);
            return suffix.Length == 2 && int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index > 0;
        }

        private static Sprite Load(string path) => string.IsNullOrWhiteSpace(path) ? null : Resources.Load<Sprite>(path);

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < value.Length; i++)
                {
                    char character = value[i];
                    hash = (hash ^ (byte)character) * 16777619;
                    hash = (hash ^ (byte)(character >> 8)) * 16777619;
                }
                return hash;
            }
        }
    }
}
