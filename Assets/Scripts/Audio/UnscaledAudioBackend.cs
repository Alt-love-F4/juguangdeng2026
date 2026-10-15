using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YokiFrame;
using YokiFrame.Unity;

namespace Spotlight.Audio
{
    internal sealed class UnscaledAudioBackend : IAudioBackend
    {
        private readonly UnityAudioKitBackend backend = new UnityAudioKitBackend();
        private sealed class Voice
        {
            public UnityAudioKitBackend backend;
            public int localId;
            public string bus;
            public bool paused;
            public float fadeRemaining = -1f;
        }
        private readonly Dictionary<int, Voice> isolated = new Dictionary<int, Voice>();
        private readonly Dictionary<string, float> volumes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly List<int> expired = new List<int>();
        private bool globallyPaused, disposed;
        private int generation;
        private int nextVoice;
        public void SetVoicePaused(int id, bool paused)
        {
            if (!isolated.TryGetValue(id, out var voice)) return;
            voice.paused = paused;
            ApplyPause(voice);
        }
        public string BackendName => "Spotlight.Unity.Unscaled";
        public AudioBackendCapabilities Capabilities => backend.Capabilities;
        public int Play(string path, AudioPlayOptions options)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UnscaledAudioBackend));
            var voice = new UnityAudioKitBackend();
            try { return Register(voice, voice.Play(path, options), options); }
            catch { voice.Dispose(); throw; }
        }
        private int Register(UnityAudioKitBackend backend, int localId, AudioPlayOptions options)
        {
            if (localId <= 0) { backend.Dispose(); return localId; }
            foreach (var volume in volumes) backend.SetBusVolume(volume.Key, volume.Value);
            var voice = new Voice { backend = backend, localId = localId, bus = options.Bus };
            ApplyPause(voice);
            do { nextVoice = nextVoice == int.MaxValue ? 1 : nextVoice + 1; } while (isolated.ContainsKey(nextVoice));
            isolated.Add(nextVoice, voice);
            return nextVoice;
        }
        public async Task<int> PlayAsync(string path, AudioPlayOptions options, CancellationToken token)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UnscaledAudioBackend));
            int version = generation;
            var voice = new UnityAudioKitBackend();
            try
            {
                int localId = await voice.PlayAsync(path, options, token);
                token.ThrowIfCancellationRequested();
                if (disposed || version != generation) { voice.Dispose(); return 0; }
                return Register(voice, localId, options);
            }
            catch { voice.Dispose(); throw; }
        }
        private void ApplyPause(Voice voice)
        {
            if (globallyPaused || voice.paused) voice.backend.PauseAll(); else voice.backend.ResumeAll();
        }
        public bool Stop(int voiceId)
        {
            if (!isolated.TryGetValue(voiceId, out var voice)) return false;
            bool stopped = voice.backend.Stop(voice.localId);
            voice.backend.Dispose(); isolated.Remove(voiceId); return stopped;
        }
        public bool StopWithFade(int voiceId, float duration)
        {
            if (!isolated.TryGetValue(voiceId, out var voice)) return false;
            if (duration <= 0f) return Stop(voiceId);
            if (!voice.backend.StopWithFade(voice.localId, duration)) return false;
            voice.fadeRemaining = duration;
            return true;
        }
        public void StopAll() { generation++; foreach (var voice in isolated.Values) voice.backend.Dispose(); isolated.Clear(); backend.StopAll(); }
        public void StopBus(string bus)
        {
            expired.Clear();
            foreach (var pair in isolated) if (string.Equals(pair.Value.bus, bus, StringComparison.OrdinalIgnoreCase)) expired.Add(pair.Key);
            foreach (int id in expired) Stop(id);
            backend.StopBus(bus);
        }
        public void PauseAll() { globallyPaused = true; foreach (var voice in isolated.Values) ApplyPause(voice); }
        public void ResumeAll() { globallyPaused = false; foreach (var voice in isolated.Values) ApplyPause(voice); }
        public bool Preload(string path) => backend.Preload(path);
        public Task<bool> PreloadAsync(string path, CancellationToken token) => backend.PreloadAsync(path, token);
        public void Unload(string path)
        {
            backend.Unload(path);
            foreach (var voice in isolated.Values) voice.backend.Unload(path);
        }
        public void UnloadAll()
        {
            backend.UnloadAll();
            foreach (var voice in isolated.Values) voice.backend.UnloadAll();
        }
        public void SetBusVolume(string bus, float volume)
        {
            backend.SetBusVolume(bus, volume);
            volumes[bus] = backend.GetBusVolume(bus);
            foreach (var voice in isolated.Values) voice.backend.SetBusVolume(bus, volume);
        }
        public float GetBusVolume(string bus) => backend.GetBusVolume(bus);
        public void Update(float deltaTime)
        {
            expired.Clear();
            foreach (var pair in isolated)
            {
                var voice = pair.Value;
                if (globallyPaused || voice.paused) continue;
                voice.backend.Update(Time.unscaledDeltaTime);
                if (voice.fadeRemaining < 0f) continue;
                voice.fadeRemaining -= Time.unscaledDeltaTime;
                if (voice.fadeRemaining <= 0f) expired.Add(pair.Key);
            }
            foreach (int id in expired) Stop(id);
            backend.Update(Time.unscaledDeltaTime);
        }
        public void Dispose() { if (disposed) return; disposed = true; StopAll(); backend.Dispose(); }
#if UNITY_EDITOR
        public void GetActiveVoices(List<AudioVoiceSnapshot> result)
        {
            result.Clear();
            var snapshots = new List<AudioVoiceSnapshot>();
            foreach (var pair in isolated)
            {
                pair.Value.backend.GetActiveVoices(snapshots);
                foreach (var snapshot in snapshots)
                {
                    snapshot.VoiceId = pair.Key; snapshot.BackendName = BackendName;
                    result.Add(snapshot);
                }
            }
        }
#endif
    }
}
