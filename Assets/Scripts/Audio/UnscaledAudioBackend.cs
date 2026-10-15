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
        public string BackendName => "Spotlight.Unity.Unscaled";
        public AudioBackendCapabilities Capabilities => backend.Capabilities;
        public int Play(string path, AudioPlayOptions options) => backend.Play(path, options);
        public Task<int> PlayAsync(string path, AudioPlayOptions options, CancellationToken token) => backend.PlayAsync(path, options, token);
        public bool Stop(int voiceId) => backend.Stop(voiceId);
        public bool StopWithFade(int voiceId, float duration) => backend.StopWithFade(voiceId, duration);
        public void StopAll() => backend.StopAll();
        public void StopBus(string bus) => backend.StopBus(bus);
        public void PauseAll() => backend.PauseAll();
        public void ResumeAll() => backend.ResumeAll();
        public bool Preload(string path) => backend.Preload(path);
        public Task<bool> PreloadAsync(string path, CancellationToken token) => backend.PreloadAsync(path, token);
        public void Unload(string path) => backend.Unload(path);
        public void UnloadAll() => backend.UnloadAll();
        public void SetBusVolume(string bus, float volume) => backend.SetBusVolume(bus, volume);
        public float GetBusVolume(string bus) => backend.GetBusVolume(bus);
        public void Update(float deltaTime) => backend.Update(Time.unscaledDeltaTime);
        public void Dispose() => backend.Dispose();
#if UNITY_EDITOR
        public void GetActiveVoices(List<AudioVoiceSnapshot> result) => backend.GetActiveVoices(result);
#endif
    }
}
