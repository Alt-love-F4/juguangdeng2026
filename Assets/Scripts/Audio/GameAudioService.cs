using System;
using System.Collections.Generic;
using UnityEngine;
using YokiFrame;

namespace Spotlight.Audio
{
    public sealed class GameAudioService : IDisposable
    {
        private sealed class Voice
        {
            public string key;
            public object owner;
            public string slot;
            public AudioVoiceHandle handle;
            public IAudioBackend backend;
            public float baseVolume;
            public float basePitch;
            public float fadeOut;
            public float end;
            public bool paused;
            public float pausedAt;
        }

        private sealed class MusicRequest
        {
            public object owner;
            public string key;
            public float volume;
            public float fadeIn;
            public float fadeOut;
            public bool resource;
        }

        private readonly AudioCatalog catalog;
        private readonly IAudioResourceLoader previousLoader;
        private readonly CatalogAudioLoader loader;
        private IAudioBackend ownedBackend;
        private readonly List<Voice> voices = new List<Voice>();
        private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        private readonly HashSet<string> warnings = new HashSet<string>();
        private readonly List<MusicRequest> musicRequests = new List<MusicRequest>();
        private readonly MusicRequest baseline = new MusicRequest { volume = 1f, fadeIn = -1f, fadeOut = -1f };
        private AudioVoiceHandle music;
        private string musicPath;
        private float musicVolume;
        private bool cafeMusicActive;
        private string cafeMusicKey;
        private int? cafeMusicDay;
        private bool disposed;
        public AudioCatalog Catalog => catalog;
        public event Action<string> CuePlayed;

#if UNITY_EDITOR
        // Project-owned diagnostics only; reading this must not create a backend or start sound.
        public void DescribeActiveVoices(List<string> result)
        {
            result.Clear();
            foreach (Voice voice in voices)
                result.Add(voice.key + " | owner=" + (voice.owner ?? "global") + " | slot=" + (voice.slot ?? "one-shot"));
            MusicRequest request = musicRequests.Count > 0 ? musicRequests[musicRequests.Count - 1] : baseline;
            if (music.IsValid && musicPath != null)
                result.Add("BGM: " + request.key + " | owner=" + (request.owner ?? "global"));
        }
#endif

        public GameAudioService(AudioCatalog catalog)
        {
            this.catalog = catalog;
            previousLoader = AudioKit.GetResourceLoader();
            loader = new CatalogAudioLoader(previousLoader);
            AudioKit.SetResourceLoader(loader);
        }

        public AudioVoiceHandle Play(string key, object owner = null, float volume = 1f)
            => PlayCue(key, owner, null, volume, false);

        private AudioVoiceHandle PlayCue(string key, object owner, string slot, float volume, bool forceLoop, AudioCategory? category = null, float pitch = 1f)
        {
            if (disposed || string.IsNullOrWhiteSpace(key)) return default;
            AudioCue cue = catalog != null ? catalog.Find(key) : null;
            if (cue == null || !cue.enabled || cue.clip == null && string.IsNullOrWhiteSpace(cue.resourcePath))
            {
                if (cue == null || cue.enabled) Warn(key);
                return default;
            }
            Tick();
            if (!forceLoop && lastPlayed.TryGetValue(key, out float previous) && Time.unscaledTime - previous < cue.cooldown) return default;
            int count = 0;
            foreach (Voice voice in voices) if (voice.key == key && voice.slot != "music-fade") count++;
            if (count >= Mathf.Max(1, cue.maxVoices)) return default;
            AudioPlayOptions options = AudioPlayOptions.Default;
            options.Bus = Bus(category ?? cue.category);
            options.Loop = category == AudioCategory.Voice ? false : forceLoop || cue.loop;
            options.Volume = Mathf.Clamp01(cue.volume * volume);
            options.Pitch = Mathf.Clamp(cue.pitch * pitch, .1f, 3f);
            options.FadeInDuration = cue.fadeIn;
            options.FadeOutDuration = cue.fadeOut;
            string path = cue.clip != null ? loader.Register(cue.clip) : cue.resourcePath;
            AudioVoiceHandle handle = Start(path, options, key);
            if (!handle.IsValid) return handle;
            lastPlayed[key] = Time.unscaledTime;
            voices.Add(new Voice { key = key, owner = owner, slot = slot, handle = handle,
                backend = AudioKit.GetBackend(), baseVolume = cue.volume, basePitch = cue.pitch, fadeOut = Mathf.Max(0f, cue.fadeOut),
                end = options.Loop ? float.PositiveInfinity : Time.unscaledTime + loader.Duration(path) / options.Pitch });
            return handle;
        }

        /// <summary>Volume and pitch multiply the cue defaults; repeated requests update the same voice over 80 ms.</summary>
        public void SetLoop(object owner, string slot, string key, bool active, float volume = 1f, float pitch = 1f)
        {
            if (owner == null || disposed) return;
            for (int index = voices.Count - 1; index >= 0; index--)
            {
                Voice voice = voices[index];
                if (!ReferenceEquals(voice.owner, owner) || voice.slot != slot) continue;
                if (active && voice.key == key)
                {
                    AudioCue cue = catalog != null ? catalog.Find(key) : null;
                    if (cue != null) { voice.baseVolume = cue.volume; voice.basePitch = cue.pitch; }
                    // Never send a stale local id to a replacement backend or take over an injected backend.
                    if (ReferenceEquals(voice.backend, AudioKit.GetBackend()) && voice.backend is IDynamicAudioLoopBackend dynamicBackend)
                        dynamicBackend.SetLoopParameters(voice.handle.VoiceId,
                            Mathf.Clamp01(voice.baseVolume * volume), Mathf.Clamp(voice.basePitch * pitch, .1f, 3f));
                    return;
                }
                AudioKit.StopWithFade(voice.handle, voice.fadeOut);
                voices.RemoveAt(index);
            }
            if (active) PlayCue(key, owner, slot, volume, true, pitch: pitch);
        }

        public AudioVoiceHandle PlayNarrative(string keyOrResource, object owner, float volume = 1f, bool voice = false)
        {
            if (string.IsNullOrWhiteSpace(keyOrResource) || disposed) return default;
            if (catalog != null && catalog.Find(keyOrResource) != null)
                return PlayCue(keyOrResource, owner, null, volume, false, voice ? AudioCategory.Voice : (AudioCategory?)null);
            AudioClip clip = Resources.Load<AudioClip>(keyOrResource);
            AudioPlayOptions options = AudioPlayOptions.Default;
            options.Bus = voice ? AudioBus.Voice : AudioBus.Sfx;
            options.Volume = Mathf.Clamp01(volume);
            string path = clip != null ? loader.Register(clip) : keyOrResource;
            AudioVoiceHandle handle = Start(path, options, keyOrResource);
            if (handle.IsValid) voices.Add(new Voice { owner = owner, key = keyOrResource, handle = handle,
                end = Time.unscaledTime + loader.Duration(path) });
            return handle;
        }

        public void SetCafeDayMusic(int day)
        {
            if (cafeMusicDay.HasValue && cafeMusicDay.Value != day) cafeMusicKey = null;
            cafeMusicDay = day;
            SetMusic(AudioKeys.CafeMusic);
        }

        private string ChooseCafeMusic()
        {
            AudioCue sunshine = catalog != null ? catalog.Find(AudioKeys.CafeSunshineMusic) : null;
            AudioCue progress = catalog != null ? catalog.Find(AudioKeys.CafeProgressMusic) : null;
            bool hasSunshine = HasMusicMaterial(sunshine), hasProgress = HasMusicMaterial(progress);
            if (hasSunshine && hasProgress)
                return UnityEngine.Random.Range(0, 2) == 0 ? AudioKeys.CafeSunshineMusic : AudioKeys.CafeProgressMusic;
            if (hasSunshine) return AudioKeys.CafeSunshineMusic;
            if (hasProgress) return AudioKeys.CafeProgressMusic;
            // Retain compatibility with small preview/test catalogs that only provide the legacy cue.
            return AudioKeys.CafeMusic;
        }

        private static bool HasMusicMaterial(AudioCue cue)
            => cue != null && cue.enabled && (cue.clip != null || !string.IsNullOrWhiteSpace(cue.resourcePath));

        public void SetMusic(string key, object owner = null, float volume = 1f, float fadeIn = -1f, float fadeOut = -1f, bool resource = false)
        {
            if (disposed) return;
            MusicRequest request = baseline;
            if (owner == null)
            {
                bool cafe = key == AudioKeys.CafeMusic;
                if (cafe)
                {
                    if (!cafeMusicActive || cafeMusicKey == null) cafeMusicKey = ChooseCafeMusic();
                    key = cafeMusicKey;
                }
                cafeMusicActive = cafe;
            }
            if (owner != null)
            {
                request = musicRequests.Find(item => ReferenceEquals(item.owner, owner));
                if (request == null) request = new MusicRequest { owner = owner };
                else musicRequests.Remove(request);
                musicRequests.Add(request);
            }
            request.key = key; request.volume = volume; request.fadeIn = fadeIn; request.fadeOut = fadeOut; request.resource = resource;
            RefreshMusic();
        }

        private void RefreshMusic()
        {
            MusicRequest request = musicRequests.Count > 0 ? musicRequests[musicRequests.Count - 1] : baseline;
            AudioCue cue = catalog != null ? catalog.Find(request.key) : null;
            string path = null;
            float volume = request.volume;
            if (!string.IsNullOrWhiteSpace(request.key))
            {
                if (cue != null)
                {
                    if (cue.enabled)
                    {
                        if (cue.clip == null && string.IsNullOrWhiteSpace(cue.resourcePath)) { Warn(request.key); return; }
                        path = cue.clip != null ? loader.Register(cue.clip) : cue.resourcePath;
                        volume *= cue.volume;
                    }
                }
                else if (request.resource)
                {
                    AudioClip clip = Resources.Load<AudioClip>(request.key);
                    path = clip != null ? loader.Register(clip) : request.key;
                }
                else { Warn(request.key); return; }
            }
            if (musicPath == path && Mathf.Approximately(musicVolume, volume)) return;
            AudioVoiceHandle next = default;
            if (path != null)
            {
                AudioPlayOptions options = AudioPlayOptions.Default;
                options.Bus = AudioBus.Music; options.Loop = true; options.Volume = Mathf.Clamp01(volume);
                options.FadeInDuration = request.fadeIn >= 0 ? request.fadeIn : Mathf.Max(cue != null ? cue.fadeIn : 0f, catalog != null ? catalog.musicTransitionSeconds : .5f);
                next = Start(path, options, request.key);
                if (!next.IsValid) return;
            }
            float fade = request.fadeOut >= 0 ? request.fadeOut : catalog != null ? catalog.musicTransitionSeconds : .5f;
            if (music.IsValid)
            {
                AudioKit.StopWithFade(music, fade);
                voices.Add(new Voice { key = musicPath, slot = "music-fade", handle = music, end = Time.unscaledTime + fade });
            }
            music = next; musicPath = path; musicVolume = volume;
        }

        public void StopOwner(object owner)
        {
            if (disposed || owner == null) return;
            for (int index = voices.Count - 1; index >= 0; index--)
                if (ReferenceEquals(voices[index].owner, owner)) { AudioKit.Stop(voices[index].handle); voices.RemoveAt(index); }
            if (musicRequests.RemoveAll(item => ReferenceEquals(item.owner, owner)) > 0) RefreshMusic();
        }

        public void SetOwnerPaused(object owner, bool paused)
        {
            if (disposed || owner == null) return;
            foreach (Voice voice in voices)
            {
                if (!ReferenceEquals(voice.owner, owner) || voice.paused == paused) continue;
                if (ownedBackend is UnscaledAudioBackend backend) backend.SetVoicePaused(voice.handle.VoiceId, paused);
                else continue;
                if (paused) voice.pausedAt = Time.unscaledTime;
                else voice.end += Time.unscaledTime - voice.pausedAt;
                voice.paused = paused;
            }
        }

        public void Tick()
        {
            if (disposed) return;
            for (int index = voices.Count - 1; index >= 0; index--)
            {
                Voice voice = voices[index];
                if (voice.owner is UnityEngine.Object owner && owner == null)
                { AudioKit.Stop(voice.handle); voices.RemoveAt(index); }
                else if (!voice.paused && Time.unscaledTime >= voice.end) { AudioKit.Stop(voice.handle); voices.RemoveAt(index); }
            }
            if (musicRequests.RemoveAll(item => item.owner is UnityEngine.Object owner && owner == null) > 0) RefreshMusic();
        }

        private AudioVoiceHandle Start(string path, AudioPlayOptions options, string key)
        {
            try
            {
                if (!AudioKit.HasBackend)
                {
                    ownedBackend = new UnscaledAudioBackend();
                    AudioKit.SetBackend(ownedBackend);
                }
                AudioVoiceHandle handle = AudioKit.Play(path, options);
                if (handle.IsValid) CuePlayed?.Invoke(key); else Warn(key);
                return handle;
            }
            catch (Exception exception)
            {
                if (warnings.Add(key)) Debug.LogWarning("[GameAudio] 无法播放 " + key + ": " + exception.Message);
                return default;
            }
        }

        private void Warn(string key)
        {
            if ((catalog == null || catalog.warnMissing) && warnings.Add(key))
                Debug.LogWarning("[GameAudio] 缺少音频配置或素材: " + key + "。请在 Tools > Audio > Game Audio 配置。");
        }

        public static string Bus(AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Music: return AudioBus.Music;
                case AudioCategory.Ambience: return AudioBus.Ambience;
                case AudioCategory.Voice: return AudioBus.Voice;
                case AudioCategory.UI: return AudioBus.UI;
                default: return AudioBus.Sfx;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Voice voice in voices) AudioKit.Stop(voice.handle);
            voices.Clear(); musicRequests.Clear(); AudioKit.Stop(music);
            if (ownedBackend != null && ReferenceEquals(AudioKit.GetBackend(), ownedBackend)) AudioKit.ClearBackend();
            if (ReferenceEquals(AudioKit.GetResourceLoader(), loader)) AudioKit.SetResourceLoader(previousLoader);
            CuePlayed = null;
        }
    }
}
