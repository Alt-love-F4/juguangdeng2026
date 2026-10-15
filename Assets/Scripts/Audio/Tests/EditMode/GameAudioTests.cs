using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.Audio.Tests
{
    public sealed class GameAudioTests
    {
        private AudioCatalog catalog;
        private AudioClip clip;
        private AudioClip secondClip;
        private FakeBackend backend;

        [SetUp]
        public void SetUp()
        {
            GameAudio.Shutdown();
            AudioKit.Reset();
            backend = new FakeBackend();
            AudioKit.SetBackend(backend);
            catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            catalog.warnMissing = false;
            clip = AudioClip.Create("audio-test", 4410, 1, 44100, false);
            secondClip = AudioClip.Create("music-test", 4410, 1, 44100, false);
            catalog.entries.Add(new AudioCue { key = AudioKeys.Confirm, clip = clip, category = AudioCategory.UI, volume = .4f, cooldown = .2f });
            catalog.entries.Add(new AudioCue { key = AudioKeys.Grinder, clip = clip, category = AudioCategory.Sfx, loop = true });
            catalog.entries.Add(new AudioCue { key = AudioKeys.MenuMusic, clip = clip, category = AudioCategory.Music, loop = true });
            catalog.entries.Add(new AudioCue { key = AudioKeys.CafeMusic, clip = secondClip, category = AudioCategory.Music, loop = true });
            GameAudio.Initialize(catalog);
        }

        [TearDown]
        public void TearDown()
        {
            GameAudio.Shutdown();
            AudioKit.Reset();
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(secondClip);
        }

        [Test]
        public void OwnerPauseIsolatedFromOtherVoiceAndGlobalPause()
        {
            GameAudio.Shutdown();
            AudioKit.ClearBackend();
            GameAudio.Initialize(catalog);
            var first = new object(); var second = new object();
            GameAudio.SetLoop(first, "loop", AudioKeys.Grinder, true);
            GameAudio.SetLoop(second, "loop", AudioKeys.Grinder, true);
            var actual = AudioKit.GetBackend();
            var snapshots = new List<AudioVoiceSnapshot>();
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots.Count, Is.EqualTo(2));
            Assert.That(snapshots[0].VoiceId, Is.Not.EqualTo(snapshots[1].VoiceId));
            GameAudio.SetOwnerPaused(first, true);
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots.Count(item => item.IsPaused), Is.EqualTo(1));
            actual.PauseAll();
            GameAudio.SetOwnerPaused(first, false);
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots.All(item => item.IsPaused), Is.True);
            GameAudio.SetOwnerPaused(first, true);
            actual.ResumeAll();
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots.Count(item => item.IsPaused), Is.EqualTo(1));
            actual.SetBusVolume(AudioBus.Sfx, .25f);
            Assert.That(actual.GetBusVolume(AudioBus.Sfx), Is.EqualTo(.25f));
            actual.StopBus(AudioBus.Sfx);
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots, Is.Empty);
        }

        [Test]
        public void InitializingOrReadingDiagnosticsDoesNotCreateDefaultBackend()
        {
            GameAudio.Shutdown();
            AudioKit.ClearBackend();
            GameAudio.Initialize(catalog);
            GameAudio.Service.DescribeActiveVoices(new List<string>());
            Assert.That(AudioKit.HasBackend, Is.False);
        }

        [Test]
        public void ExplicitBackendIsPreservedAndCatalogClipsLoadThroughAudioKit()
        {
            Assert.That(AudioKit.GetBackend(), Is.SameAs(backend));
            GameAudio.Play(AudioKeys.Confirm);
            Assert.That(backend.LoadedClip, Is.SameAs(clip));
            Assert.That(backend.Active.Values.Single().Bus, Is.EqualTo(AudioBus.UI));
            Assert.That(backend.Active.Values.Single().Volume, Is.EqualTo(.4f));
        }

        [Test]
        public void OneShotCooldownAndDisabledEntriesDoNotStartDuplicateVoices()
        {
            GameAudio.Play(AudioKeys.Confirm);
            GameAudio.Play(AudioKeys.Confirm);
            Assert.That(backend.Active.Count, Is.EqualTo(1));
            catalog.Find(AudioKeys.Confirm).enabled = false;
            GameAudio.Play(AudioKeys.Confirm);
            Assert.That(backend.PlayCount, Is.EqualTo(1));
        }

        [Test]
        public void LoopIsIdempotentAndClosingOneOwnerDoesNotStopAnother()
        {
            var firstOwner = new object();
            var secondOwner = new object();
            GameAudio.SetLoop(firstOwner, "machine", AudioKeys.Grinder, true);
            GameAudio.SetLoop(firstOwner, "machine", AudioKeys.Grinder, true);
            GameAudio.SetLoop(secondOwner, "machine", AudioKeys.Grinder, true);
            Assert.That(backend.Active.Count, Is.EqualTo(2));
            GameAudio.StopOwner(firstOwner);
            Assert.That(backend.Active.Count, Is.EqualTo(1));
            GameAudio.SetLoop(secondOwner, "machine", AudioKeys.Grinder, false);
            Assert.That(backend.Active, Is.Empty);
        }

        [Test]
        public void TemporaryMusicOverrideRestoresBaselineAndIdenticalRequestsDoNotRestart()
        {
            var owner = new object();
            GameAudio.Music(AudioKeys.MenuMusic);
            GameAudio.Music(AudioKeys.MenuMusic);
            Assert.That(backend.PlayCount, Is.EqualTo(1));
            GameAudio.OverrideMusic(owner, AudioKeys.CafeMusic);
            Assert.That(backend.PlayCount, Is.EqualTo(2));
            GameAudio.StopOwner(owner);
            Assert.That(backend.PlayCount, Is.EqualTo(3));
            Assert.That(backend.LoadedClip, Is.SameAs(clip));
        }

        [Test]
        public void EmptyNarrativeMusicIsAnOwnedSilenceRatherThanDeletingBaseline()
        {
            var owner = new object();
            GameAudio.Music(AudioKeys.MenuMusic);
            GameAudio.OverrideMusic(owner, "");
            Assert.That(backend.Active, Is.Empty);
            GameAudio.StopOwner(owner);
            Assert.That(backend.Active.Count, Is.EqualTo(1));
        }

        [Test]
        public void DestroyedOwnersAreCleanedWithoutStoppingUnrelatedVoices()
        {
            var host = new GameObject("audio-owner");
            GameAudio.SetLoop(host, "machine", AudioKeys.Grinder, true);
            Object.DestroyImmediate(host);
            GameAudio.Service.Tick();
            Assert.That(backend.Active, Is.Empty);
        }

        [Test]
        public void CatalogReportsDuplicateAndMissingMaterials()
        {
            catalog.entries.Add(new AudioCue { key = "missing" });
            catalog.entries.Add(new AudioCue { key = "missing" });
            string[] messages = catalog.Validate().ToArray();
            Assert.That(messages.Any(message => message.Contains("缺少素材: missing")), Is.True);
            Assert.That(messages.Any(message => message.Contains("重复 Key: missing")), Is.True);
        }

        [Test]
        public void ReinstallingButtonFeedbackOnlyBindsOneClickListener()
        {
            var host = new GameObject("audio-button", typeof(RectTransform), typeof(Button));
            try
            {
                Button button = host.GetComponent<Button>();
                UIButtonAudio.Configure(button);
                UIButtonAudio.Configure(button);
                catalog.Find(AudioKeys.Confirm).cooldown = 0f;
                button.onClick.Invoke();
                Assert.That(backend.PlayCount, Is.EqualTo(1));
                button.onClick.RemoveAllListeners();
                UIButtonAudio.Configure(button);
                button.onClick.Invoke();
                Assert.That(backend.PlayCount, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void RenewedMusicOwnerTakesPriorityAndOtherOwnerCanStillResume()
        {
            var first = new object();
            var second = new object();
            GameAudio.Music(AudioKeys.MenuMusic);
            GameAudio.OverrideMusic(first, AudioKeys.MenuMusic);
            GameAudio.OverrideMusic(second, AudioKeys.CafeMusic);
            Assert.That(backend.LoadedClip, Is.SameAs(secondClip));
            GameAudio.OverrideMusic(first, AudioKeys.MenuMusic);
            Assert.That(backend.LoadedClip, Is.SameAs(clip));
            GameAudio.StopOwner(first);
            Assert.That(backend.LoadedClip, Is.SameAs(secondClip));
        }

        [Test]
        public void ShutdownStopsOutgoingMusicFadesButPreservesInjectedBackend()
        {
            backend.DeferFades = true;
            GameAudio.Music(AudioKeys.MenuMusic);
            GameAudio.Music(AudioKeys.CafeMusic);
            Assert.That(backend.Active.Count, Is.EqualTo(2));
            GameAudio.Shutdown();
            Assert.That(backend.Active, Is.Empty);
            Assert.That(AudioKit.GetBackend(), Is.SameAs(backend));
        }

        [Test]
        public void NarrativeVoiceAlwaysUsesVoiceBusAndStopsOnlyItsOwner()
        {
            var owner = new object();
            GameAudio.Play(AudioKeys.Confirm);
            GameAudio.PlayNarrative(AudioKeys.Grinder, owner, voice: true);
            Assert.That(backend.Active.Values.Any(options => options.Bus == AudioBus.Voice && !options.Loop), Is.True);
            GameAudio.StopOwner(owner);
            Assert.That(backend.Active.Values.Single().Bus, Is.EqualTo(AudioBus.UI));
        }

        [Test]
        public void DisabledButtonsDoNotProduceConfirmation()
        {
            var host = new GameObject("audio-disabled-button", typeof(RectTransform), typeof(Button));
            try
            {
                Button button = host.GetComponent<Button>();
                UIButtonAudio.Configure(button);
                button.interactable = false;
                button.onClick.Invoke();
                Assert.That(backend.PlayCount, Is.Zero);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void SliderRestorationIsSilentAndUserPreviewPlaysOnceAtEnd()
        {
            catalog.entries.Add(new AudioCue { key = AudioKeys.VolumePreview, clip = clip, cooldown = 0 });
            var host = new GameObject("audio-slider", typeof(RectTransform), typeof(Slider));
            try
            {
                Slider slider = host.GetComponent<Slider>();
                AudioSliderPreview preview = host.AddComponent<AudioSliderPreview>();
                // Regular MonoBehaviour lifecycle is not invoked by EditMode AddComponent.
                typeof(AudioSliderPreview).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(preview, null);
                slider.SetValueWithoutNotify(.8f);
                preview.OnDeselect(null);
                Assert.That(backend.PlayCount, Is.Zero);
                slider.value = .6f;
                Assert.That(backend.PlayCount, Is.Zero);
                preview.OnPointerUp(null);
                preview.OnDeselect(null);
                Assert.That(backend.PlayCount, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void ShippedCatalogContainsEveryProjectCueWithUniqueKeys()
        {
            AudioCatalog shipped = Resources.Load<AudioCatalog>(AudioCatalog.ResourceName);
            Assert.That(shipped, Is.Not.Null);
            foreach (var field in typeof(AudioKeys).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                string key = (string)field.GetRawConstantValue();
                Assert.That(shipped.Find(key), Is.Not.Null, key);
            }
            Assert.That(shipped.entries.Select(cue => cue.key).Distinct().Count(), Is.EqualTo(shipped.entries.Count));
        }

        private sealed class FakeBackend : IAudioBackend
        {
            private int nextId;
            public readonly Dictionary<int, AudioPlayOptions> Active = new Dictionary<int, AudioPlayOptions>();
            public AudioClip LoadedClip;
            public int PlayCount;
            public bool DeferFades;
            public string BackendName => "Test.Audio";
            public AudioBackendCapabilities Capabilities => AudioBackendCapabilities.All;
            public int Play(string path, AudioPlayOptions options)
            {
                LoadedClip = AudioKit.GetResourceLoader().Load<AudioClip>(path);
                PlayCount++;
                Active[++nextId] = options;
                return nextId;
            }
            public Task<int> PlayAsync(string path, AudioPlayOptions options, CancellationToken token) => Task.FromResult(Play(path, options));
            public bool Stop(int voiceId) => Active.Remove(voiceId);
            public bool StopWithFade(int voiceId, float duration) => DeferFades ? Active.ContainsKey(voiceId) : Stop(voiceId);
            public void StopAll() => Active.Clear();
            public void StopBus(string bus)
            {
                foreach (int identifier in Active.Where(pair => pair.Value.Bus == bus).Select(pair => pair.Key).ToArray()) Active.Remove(identifier);
            }
            public void PauseAll() { }
            public void ResumeAll() { }
            public bool Preload(string path) => true;
            public Task<bool> PreloadAsync(string path, CancellationToken token) => Task.FromResult(true);
            public void Unload(string path) { }
            public void UnloadAll() { }
            public void SetBusVolume(string bus, float volume) { }
            public float GetBusVolume(string bus) => 1f;
            public void Update(float deltaTime) { }
            public void Dispose() => Active.Clear();
            public void GetActiveVoices(List<AudioVoiceSnapshot> result) => result.Clear();
        }
    }
}
