using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Audio
{
    public enum AudioCategory { UI, Sfx, Music, Ambience, Voice }

    [Serializable]
    public sealed class AudioCue
    {
        public string key;
        public string description;
        public bool enabled = true;
        public AudioClip clip;
        public string resourcePath;
        public AudioCategory category;
        public bool loop;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(.1f, 3f)] public float pitch = 1f;
        [Min(0f)] public float fadeIn;
        [Min(0f)] public float fadeOut = .15f;
        [Min(0f)] public float cooldown = .08f;
        [Min(1)] public int maxVoices = 3;
        public bool provisional;
        [TextArea] public string materialNote;
    }

    [CreateAssetMenu(menuName = "Spotlight/Audio Catalog", fileName = "GameAudioCatalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        public const string ResourceName = "GameAudioCatalog";
        public List<AudioCue> entries = new List<AudioCue>();
        [Range(0f, 1f)] public float videoVolume = 1f;
        public AudioCategory videoCategory = AudioCategory.Sfx;
        [Min(0f)] public float musicTransitionSeconds = .5f;
        public bool warnMissing = true;
        private Dictionary<string, AudioCue> index;

        public AudioCue Find(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (index == null)
            {
                index = new Dictionary<string, AudioCue>(StringComparer.Ordinal);
                foreach (AudioCue entry in entries)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.key) && !index.ContainsKey(entry.key))
                        index.Add(entry.key, entry);
            }
            return index.TryGetValue(key, out AudioCue cue) ? cue : null;
        }

        public IEnumerable<string> Validate()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (AudioCue cue in entries)
            {
                if (cue == null || string.IsNullOrWhiteSpace(cue.key)) { yield return "存在空声音 Key"; continue; }
                if (!keys.Add(cue.key)) yield return "重复 Key: " + cue.key;
                if (cue.enabled && cue.clip == null && string.IsNullOrWhiteSpace(cue.resourcePath))
                    yield return "缺少素材: " + cue.key + " — " + cue.description;
                if (cue.provisional) yield return "临时替代素材: " + cue.key + " — " + cue.materialNote;
            }
        }

        private void OnValidate() => index = null;
    }
}
