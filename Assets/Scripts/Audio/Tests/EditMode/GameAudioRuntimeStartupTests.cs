using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.Audio.Tests
{
    public sealed class GameAudioRuntimeStartupTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void StartupAppliesCurrentSceneAndRepeatedSceneLoadedDoesNotRestartMusic(bool sceneLoadedFirst)
        {
            Scene scene = SceneManager.GetActiveScene();
            string previousName = scene.name;
            scene.name = "CoffeeShop";
            var disabledListeners = new List<AudioListener>();
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.enabled) { disabledListeners.Add(listener); listener.enabled = false; }
            var host = new GameObject("audio-runtime-test");
            var button = new GameObject("button", typeof(RectTransform), typeof(Button));
            var camera = new GameObject("listener-test");
            var catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            var clip = AudioClip.Create("startup-music", 4410, 1, 44100, false);
            var backend = new StartupBackend();
            GameAudioRuntime runtime = host.AddComponent<GameAudioRuntime>();
            try
            {
                // EditMode does not drive ordinary MonoBehaviour lifecycle. Simulate entering
                // Play with the scene already present, without delivering sceneLoaded.
                Invoke(runtime, "Awake");
                GameAudio.Shutdown();
                AudioKit.Reset();
                AudioKit.SetBackend(backend);
                catalog.warnMissing = false;
                catalog.entries.Add(new AudioCue { key = AudioKeys.CafeMusic, clip = clip, loop = true, category = AudioCategory.Music });
                GameAudio.Initialize(catalog);
                AudioListener fallback = host.GetComponent<AudioListener>();
                Assert.That(fallback.enabled, Is.False);
                if (sceneLoadedFirst) Invoke(runtime, "SceneLoaded", scene, LoadSceneMode.Single);
                Invoke(runtime, "Start");
                Assert.That(backend.PlayCount, Is.EqualTo(1));
                Assert.That(backend.LastOptions.Loop, Is.True);
                Assert.That(backend.LastOptions.Bus, Is.EqualTo(AudioBus.Music));
                Assert.That(fallback.enabled, Is.True);
                Assert.That(button.GetComponents<UIButtonAudio>().Length, Is.EqualTo(1));

                Invoke(runtime, "SceneLoaded", scene, LoadSceneMode.Single);
                Invoke(runtime, "Start");
                Assert.That(backend.PlayCount, Is.EqualTo(1));
                Assert.That(button.GetComponents<UIButtonAudio>().Length, Is.EqualTo(1));

                AudioListener primary = camera.AddComponent<AudioListener>();
                Invoke(runtime, "RefreshListener");
                Assert.That(fallback.enabled, Is.False);
                primary.enabled = false;
                Invoke(runtime, "RefreshListener");
                Assert.That(fallback.enabled, Is.True, "A disabled scene listener must not suppress the fallback.");
            }
            finally
            {
                Invoke(runtime, "OnDestroy");
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(button);
                Object.DestroyImmediate(camera);
                GameAudio.Shutdown();
                AudioKit.Reset();
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(clip);
                foreach (AudioListener listener in disabledListeners) if (listener != null) listener.enabled = true;
                scene.name = previousName;
            }
        }

        private static void Invoke(GameAudioRuntime runtime, string method, params object[] arguments)
            => typeof(GameAudioRuntime).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runtime, arguments);

        private sealed class StartupBackend : IAudioBackend
        {
            public int PlayCount;
            public AudioPlayOptions LastOptions;
            public string BackendName => "Test.Startup";
            public AudioBackendCapabilities Capabilities => AudioBackendCapabilities.All;
            public int Play(string path, AudioPlayOptions options) { LastOptions = options; return ++PlayCount; }
            public Task<int> PlayAsync(string path, AudioPlayOptions options, CancellationToken token) => Task.FromResult(Play(path, options));
            public bool Stop(int voiceId) => true;
            public bool StopWithFade(int voiceId, float duration) => true;
            public void StopAll() { }
            public void StopBus(string bus) { }
            public void PauseAll() { }
            public void ResumeAll() { }
            public bool Preload(string path) => true;
            public Task<bool> PreloadAsync(string path, CancellationToken token) => Task.FromResult(true);
            public void Unload(string path) { }
            public void UnloadAll() { }
            public void SetBusVolume(string bus, float volume) { }
            public float GetBusVolume(string bus) => 1f;
            public void Update(float deltaTime) { }
            public void Dispose() { }
            public void GetActiveVoices(List<AudioVoiceSnapshot> result) => result.Clear();
        }
    }
}
