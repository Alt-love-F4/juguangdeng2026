using UnityEngine;
using YokiFrame;

namespace Spotlight.Audio
{
    public static class GameAudio
    {
        public static GameAudioService Service { get; private set; }
        public static AudioCatalog Catalog => Service?.Catalog;
        public static float VideoGain => (Catalog != null ? Catalog.videoVolume : 1f) * (AudioKit.IsMuted() ? 0f :
            AudioKit.GetGlobalVolume() * AudioKit.GetBusVolume(GameAudioService.Bus(Catalog != null ? Catalog.videoCategory : AudioCategory.Sfx)));

        public static void Initialize(AudioCatalog catalog)
        {
            if (Service != null && Service.Catalog == catalog) return;
            Service?.Dispose();
            Service = new GameAudioService(catalog);
        }

        public static void Play(string key, object owner = null, float volume = 1f)
        {
            Transform target = owner is Component component ? component.transform : (owner as GameObject)?.transform;
            if (target != null) key = UIAudioScope.Resolve(Catalog, target, key);
            Service?.Play(key, owner, volume);
        }
        public static void PlayNarrative(string key, object owner, float volume = 1f, bool voice = false)
            => Service?.PlayNarrative(key, owner, volume, voice);
        public static void SetLoop(object owner, string slot, string key, bool active) => Service?.SetLoop(owner, slot, key, active);
        public static void Music(string key) => Service?.SetMusic(key);
        public static void OverrideMusic(object owner, string key, float volume = 1f, float fadeIn = -1f, float fadeOut = -1f)
            => Service?.SetMusic(key, owner, volume, fadeIn, fadeOut, true);
        public static void StopOwner(object owner) => Service?.StopOwner(owner);

        public static void Shutdown()
        {
            Service?.Dispose();
            Service = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Shutdown();
    }
}
