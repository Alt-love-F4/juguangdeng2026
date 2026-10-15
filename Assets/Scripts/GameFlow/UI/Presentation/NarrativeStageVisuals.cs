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
            [Min(0f)] public float fadeSeconds = .3f;
            public Entry Snapshot() => new Entry { key = key, texture = texture, tint = tint, placeholderColor = placeholderColor, placeholder = placeholder, fadeSeconds = fadeSeconds };
        }
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        public Entry Find(string key)
        {
            if (entries != null) foreach (var entry in entries)
                if (entry != null && string.Equals(entry.key, key, StringComparison.Ordinal)) return Validate(entry).Snapshot();
            return null;
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
            if (!entry.placeholder && entry.texture == null) throw new InvalidOperationException("Missing narrative background texture: " + entry.key);
            if (float.IsNaN(entry.fadeSeconds) || float.IsInfinity(entry.fadeSeconds) || entry.fadeSeconds < 0f)
                throw new InvalidOperationException("Invalid background fade duration: " + entry.key);
            return entry;
        }
        private static readonly System.Collections.Generic.HashSet<string> warnedPlaceholders = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        public static Entry Resolve(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            var library = Resources.Load<NarrativeStageVisuals>("Dialogue/NarrativeStageVisuals");
            var resolved = library != null ? library.Find(key) : null;
            if (resolved != null)
            {
                if (resolved.placeholder && warnedPlaceholders.Add(key)) Debug.LogWarning("[NCS][BackgroundPlaceholder] key=" + key + "; using explicitly configured neutral color. Replace registry entry with matching artwork.");
                return resolved;
            }
            var texture = Resources.Load<Texture>(key);
            if (texture == null) throw new InvalidOperationException("Missing narrative background resource: " + key);
            return new Entry { key = key, texture = texture };
        }
    }
}
