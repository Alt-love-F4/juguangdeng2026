using System;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Stable table keys reference original art without moving or duplicating source textures.</summary>
    [CreateAssetMenu(menuName = "Spotlight/Presentation/Narrative Stage Visuals")]
    public sealed class NarrativeStageVisuals : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string key;
            public Texture texture;
            public Color tint = Color.white;
            public Color placeholderColor = new Color(.23f, .25f, .28f, 1f);
            public bool placeholder;
            public bool solidColor;
            public bool useSceneBackground;
            [Min(0f)] public float fadeSeconds = .3f;
            public Entry Snapshot() => new Entry { key = key, texture = texture, tint = tint, placeholderColor = placeholderColor, placeholder = placeholder, solidColor = solidColor, useSceneBackground = useSceneBackground, fadeSeconds = fadeSeconds };
        }
        [Serializable] public sealed class Alias
        {
            public string pattern, targetKey;
        }
        [Serializable] public sealed class ContextOverride
        {
            public string contextPrefix, sourceKey, targetKey;
        }
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField] private Alias[] aliases = Array.Empty<Alias>();
        [SerializeField] private ContextOverride[] contextOverrides = Array.Empty<ContextOverride>();

        /// <summary>Explicit entries win over aliases; scoped migration rules only replace their declared old key.</summary>
        public Entry Find(string key, string contextKey = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (!string.IsNullOrWhiteSpace(contextKey) && contextOverrides != null)
                foreach (var rule in contextOverrides)
                    if (rule != null && string.Equals(key, rule.sourceKey, StringComparison.Ordinal)
                        && !string.IsNullOrWhiteSpace(rule.contextPrefix)
                        && (string.Equals(contextKey, rule.contextPrefix, StringComparison.Ordinal)
                            || contextKey.StartsWith(rule.contextPrefix + ".", StringComparison.Ordinal)))
                        return AliasSnapshot(key, rule.targetKey);
            var exact = FindExact(key);
            if (exact != null) return exact;
            if (aliases != null) foreach (var alias in aliases)
                if (alias != null && Matches(key, alias.pattern)) return AliasSnapshot(key, alias.targetKey);
            return null;
        }

        private Entry FindExact(string key)
        {
            if (entries != null) foreach (var entry in entries)
                if (entry != null && string.Equals(entry.key, key, StringComparison.Ordinal)) return Validate(entry).Snapshot();
            return null;
        }
        private Entry AliasSnapshot(string requestedKey, string targetKey)
        {
            var result = FindExact(targetKey);
            if (result == null) throw new InvalidOperationException("Missing narrative background alias target: " + targetKey);
            result.key = requestedKey;
            return result;
        }
        private static bool Matches(string key, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;
            int keyIndex = 0, patternIndex = 0, starIndex = -1, retryIndex = 0;
            while (keyIndex < key.Length)
            {
                if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                {
                    starIndex = patternIndex++;
                    retryIndex = keyIndex;
                }
                else if (patternIndex < pattern.Length && pattern[patternIndex] == key[keyIndex])
                {
                    patternIndex++;
                    keyIndex++;
                }
                else if (starIndex >= 0)
                {
                    patternIndex = starIndex + 1;
                    keyIndex = ++retryIndex;
                }
                else return false;
            }
            while (patternIndex < pattern.Length && pattern[patternIndex] == '*') patternIndex++;
            return patternIndex == pattern.Length;
        }
        // Builder/Inspector authoring boundary; updates a known key without replacing other artists' entries.
        public void SetEntry(Entry value)
        {
            Validate(value);
            var list = new System.Collections.Generic.List<Entry>(entries ?? Array.Empty<Entry>());
            int index = list.FindIndex(e => e != null && string.Equals(e.key, value.key, StringComparison.Ordinal));
            if (index < 0) list.Add(value.Snapshot()); else list[index] = value.Snapshot();
            entries = list.ToArray();
        }
        private static Entry Validate(Entry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key)) throw new ArgumentException("Background registry requires a stable key.");
            if (entry.useSceneBackground && (entry.texture != null || entry.placeholder || entry.solidColor))
                throw new InvalidOperationException("Scene background must not also configure artwork or a solid color: " + entry.key);
            if (!entry.placeholder && !entry.solidColor && !entry.useSceneBackground && entry.texture == null) throw new InvalidOperationException("Missing narrative background texture: " + entry.key);
            if (float.IsNaN(entry.fadeSeconds) || float.IsInfinity(entry.fadeSeconds) || entry.fadeSeconds < 0f)
                throw new InvalidOperationException("Invalid background fade duration: " + entry.key);
            return entry;
        }
        private static readonly System.Collections.Generic.HashSet<string> warnedPlaceholders = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        private sealed class BackgroundResources
        {
            private NarrativeStageVisuals library;
            // The cache outlives scene transitions, and Resources.UnloadUnusedAssets does not see this
            // weak-table reference: a cafe page loaded mid-conversation would otherwise destroy the
            // registry and make every later page's key "missing". Reload a destroyed instance.
            public NarrativeStageVisuals Library
                => library != null ? library : library = Resources.Load<NarrativeStageVisuals>("Dialogue/NarrativeStageVisuals");
            public readonly System.Collections.Generic.Dictionary<string, Texture> textures = new System.Collections.Generic.Dictionary<string, Texture>(StringComparer.Ordinal);
        }
        // Owner lifetime is the narrative batch, not the entire game session. Context overrides
        // are still resolved per page; only resource lookups are reused, never the authored rules.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, BackgroundResources> resourcesByOwner
            = new System.Runtime.CompilerServices.ConditionalWeakTable<object, BackgroundResources>();
        public static Entry Resolve(string key, string contextKey = null, object continuityOwner = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            var scope = continuityOwner == null ? null : resourcesByOwner.GetValue(continuityOwner, _ => new BackgroundResources());
            var library = scope != null ? scope.Library : Resources.Load<NarrativeStageVisuals>("Dialogue/NarrativeStageVisuals");
            var resolved = library != null ? library.Find(key, contextKey) : null;
            if (resolved != null)
            {
                if (resolved.placeholder && warnedPlaceholders.Add(key)) Debug.LogWarning("[NCS][BackgroundPlaceholder] key=" + key + (resolved.texture != null ? "; using a labelled local requirement card, NOT final artwork." : "; using explicitly configured neutral color.") + " Replace the registry entry only after art approval.");
                return resolved;
            }
            Texture texture;
            if (scope == null || !scope.textures.TryGetValue(key, out texture) || texture == null)
            {
                texture = Resources.Load<Texture>(key);
                if (texture != null && scope != null) scope.textures[key] = texture;
            }
            if (texture == null) throw new InvalidOperationException("Missing narrative background resource: " + key);
            return new Entry { key = key, texture = texture };
        }
    }
}
