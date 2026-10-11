using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    public static class NarrativeCharacterResolver
    {
        private static CharacterCatalog catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => catalog = null;

        public static void SetCatalog(CharacterCatalog value) => catalog = value;

        public static CharacterProfile Find(string key)
        {
            if (catalog == null) catalog = CharacterCatalog.LoadFromTables();
            return catalog != null ? catalog.Find(key) : null;
        }

        public static DialogueCharacter CreateDialogueCharacter(string key, string emote = null)
        {
            CharacterProfile profile = Find(key);
            if (profile == null) throw new System.ArgumentException("Unknown character: " + key, nameof(key));
            return new DialogueCharacter(key, profile.displayName, profile.PortraitFor(emote));
        }

        public static NarrativeCharacterPresentationData Resolve(string key, string displayName, string portraitPath)
        {
            CharacterProfile profile = Find(key);
            return new NarrativeCharacterPresentationData
            {
                key = key,
                name = profile != null && !string.IsNullOrWhiteSpace(profile.displayName) ? profile.displayName : displayName,
                portraitPath = portraitPath,
                portrait = profile != null ? profile.portrait : null,
                avatar = profile != null ? profile.avatar : null
            };
        }
    }
}
