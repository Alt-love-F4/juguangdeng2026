using System;
using UnityEngine;
using YokiFrame;

namespace Spotlight.Audio
{
    /// <summary>Optional project backend capability. Values are absolute cue gains, before settings buses.</summary>
    public interface IDynamicAudioLoopBackend
    {
        bool SetLoopParameters(int voiceId, float volume, float pitch);
    }

    // UnityAudioKitBackend is sealed and has no live voice setters. Own only 2D loop sources;
    // keep its loading ownership semantics and leave one-shots/3D playback with the package.
    internal sealed class DynamicAudioLoop : IDisposable
    {
        private const float ParameterSeconds = .08f;
        private readonly IAudioResourceLoader loader;
        private readonly AudioClip clip;
        private readonly AudioSource source;
        private readonly string path;
        private readonly string bus;
        private readonly float fadeIn;
        private float volume, pitch, startVolume, startPitch, targetVolume, targetPitch;
        private float parameterElapsed = ParameterSeconds;
        private float fadeElapsed, fadeOut = -1f, fadeOutStart;
        private float envelope;
        private float masterGain = 1f, busGain = 1f;
        private bool paused, disposed;

        public DynamicAudioLoop(string path, AudioClip clip, IAudioResourceLoader loader, AudioPlayOptions options, float masterGain, float busGain)
        {
            this.masterGain = masterGain;
            this.busGain = busGain;
            this.path = path;
            this.clip = clip;
            this.loader = loader;
            bus = options.Bus;
            volume = targetVolume = options.Volume;
            pitch = targetPitch = options.Pitch;
            fadeIn = options.FadeInDuration;
            envelope = fadeIn > 0f ? 0f : 1f;
            var host = new GameObject("GameAudioLoop");
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(host);
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            source.loop = true;
            source.pitch = pitch;
            ApplyVolume();
            source.Play();
        }

        public bool SetParameters(float nextVolume, float nextPitch)
        {
            if (disposed || fadeOut >= 0f) return false;
            nextVolume = Mathf.Clamp01(nextVolume);
            nextPitch = Mathf.Clamp(nextPitch, .1f, 3f);
            if (Mathf.Approximately(targetVolume, nextVolume) && Mathf.Approximately(targetPitch, nextPitch)) return true;
            startVolume = volume; startPitch = pitch;
            targetVolume = nextVolume; targetPitch = nextPitch;
            parameterElapsed = 0f;
            return true;
        }

        public void SetGains(float master, float busVolume)
        {
            masterGain = master; busGain = busVolume;
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            if (source != null) source.volume = Mathf.Clamp01(volume * masterGain * busGain * envelope);
        }

        public void SetPaused(bool value)
        {
            if (paused == value || source == null) return;
            paused = value;
            if (paused) source.Pause(); else source.UnPause();
        }

        public void StopWithFade(float duration)
        {
            fadeOutStart = envelope;
            fadeElapsed = 0f;
            fadeOut = duration;
        }

        // Returns true when the source/fade is finished. The owner performs cleanup after iteration.
        public bool Update(float deltaTime)
        {
            if (source == null) return true;
            if (paused) return false;
            parameterElapsed += deltaTime;
            float progress = Mathf.Clamp01(parameterElapsed / ParameterSeconds);
            volume = Mathf.Lerp(startVolume, targetVolume, progress);
            pitch = Mathf.Lerp(startPitch, targetPitch, progress);
            source.pitch = pitch;
            fadeElapsed += deltaTime;
            if (fadeOut >= 0f)
            {
                if (fadeElapsed >= fadeOut) return true;
                envelope = fadeOutStart * (1f - fadeElapsed / fadeOut);
            }
            else envelope = fadeIn > 0f ? Mathf.Clamp01(fadeElapsed / fadeIn) : 1f;
            ApplyVolume();
            return false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (source != null)
            {
                source.Stop();
                source.clip = null;
                if (Application.isPlaying) UnityEngine.Object.Destroy(source.gameObject);
                else UnityEngine.Object.DestroyImmediate(source.gameObject);
            }
            loader.Release(clip);
        }

#if UNITY_EDITOR
        public AudioVoiceSnapshot Snapshot(int id, string backendName) => new AudioVoiceSnapshot
        {
            VoiceId = id, BackendName = backendName, Path = path, Bus = bus, Loop = true,
            IsPlaying = source != null && source.isPlaying, IsPaused = paused,
            Volume = source != null ? source.volume : 0f, Pitch = pitch,
            Duration = clip != null ? clip.length : 0f, Elapsed = source != null ? source.time : 0f
        };
#endif
    }
}
