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
        public void ScopedButtonsPreserveSilenceAndDisabledOverrides()
        {
            var root = new GameObject("scope", typeof(UIAudioScope));
            try
            {
                catalog.entries.Add(new AudioCue { key = "ui.common.confirm", enabled = false });
                Assert.That(UIAudioScope.Resolve(catalog, root.transform, AudioKeys.Confirm), Is.EqualTo("ui.common.confirm"));
                Assert.That(UIAudioScope.Resolve(catalog, root.transform, ""), Is.EqualTo(""));
                Assert.That(UIAudioScope.Resolve(catalog, root.transform, AudioKeys.Select), Is.EqualTo(AudioKeys.Select));
                Assert.That(UIAudioScope.Resolve(catalog, root.transform, AudioKeys.Grinder), Is.EqualTo(AudioKeys.Grinder));
            }
            finally { Object.DestroyImmediate(root); }
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
        public void LoopMultipliersApplyToCueAndExistingVoiceWithoutRestart()
        {
            AudioCue cue = catalog.Find(AudioKeys.Grinder);
            cue.volume = .8f; cue.pitch = .8f;
            var owner = new object();
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, true, volume: .5f, pitch: 1.5f);
            int id = backend.Active.Keys.Single();
            Assert.That(backend.Active[id].Volume, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(backend.Active[id].Pitch, Is.EqualTo(1.2f).Within(.0001f));
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, true, volume: 3f, pitch: 10f);
            Assert.That(backend.PlayCount, Is.EqualTo(1));
            Assert.That(backend.Active.Keys.Single(), Is.EqualTo(id));
            Assert.That(backend.Active[id].Volume, Is.EqualTo(1f));
            Assert.That(backend.Active[id].Pitch, Is.EqualTo(3f));
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, true);
            Assert.That(backend.Active[id].Volume, Is.EqualTo(.8f));
            Assert.That(backend.Active[id].Pitch, Is.EqualTo(.8f));
            cue.volume = .6f; cue.pitch = 1.4f;
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, true, volume: .5f, pitch: .5f);
            Assert.That(backend.PlayCount, Is.EqualTo(1));
            Assert.That(backend.Active[id].Volume, Is.EqualTo(.3f).Within(.0001f));
            Assert.That(backend.Active[id].Pitch, Is.EqualTo(.7f).Within(.0001f));
        }

        [Test]
        public void LoopStopUsesCapturedCueFadeOutButStopOwnerStillStopsImmediately()
        {
            AudioCue cue = catalog.Find(AudioKeys.Grinder);
            cue.fadeOut = .35f;
            var owner = new object();
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, true);
            cue.fadeOut = .9f;
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, false);
            Assert.That(backend.LastFadeDuration, Is.EqualTo(.35f));
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, true);
            GameAudio.StopOwner(owner);
            Assert.That(backend.Active, Is.Empty);
            Assert.That(backend.LastFadeDuration, Is.EqualTo(.35f));
        }

        [Test]
        public void UpdatingOneLoopDoesNotChangeAnotherOwnerOrReplacementBackend()
        {
            var first = new object(); var second = new object();
            GameAudio.SetLoop(first, "machine", AudioKeys.Grinder, true);
            int firstId = backend.Active.Keys.Single();
            GameAudio.SetLoop(second, "machine", AudioKeys.Grinder, true);
            int secondId = backend.Active.Keys.Single(id => id != firstId);
            GameAudio.SetLoop(first, "machine", AudioKeys.Grinder, true, volume: .2f, pitch: .5f);
            Assert.That(backend.Active[secondId].Volume, Is.EqualTo(1f));
            Assert.That(backend.Active[secondId].Pitch, Is.EqualTo(1f));
            var replacement = new FakeBackend();
            AudioKit.SetBackend(replacement);
            GameAudio.SetLoop(new object(), "other", AudioKeys.Grinder, true);
            GameAudio.SetLoop(first, "machine", AudioKeys.Grinder, true, volume: .1f, pitch: 2f);
            Assert.That(replacement.Active.Values.Single().Volume, Is.EqualTo(1f));
            Assert.That(replacement.Active.Values.Single().Pitch, Is.EqualTo(1f));
        }

        [Test]
        public void NativeLoopGlidesOnSameVoiceAndKeepsSettingsAndPauseLayers()
        {
            AudioCue cue = catalog.Find(AudioKeys.Grinder);
            cue.volume = .8f; cue.pitch = .8f;
            GameAudio.Shutdown(); AudioKit.ClearBackend(); GameAudio.Initialize(catalog);
            AudioKit.SetGlobalVolume(.5f);
            AudioKit.SetBusVolume(AudioBus.Sfx, .25f);
            var owner = new object();
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, true);
            var actual = AudioKit.GetBackend();
            var snapshots = new List<AudioVoiceSnapshot>();
            actual.GetActiveVoices(snapshots);
            int id = snapshots.Single().VoiceId;
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, true, volume: .5f, pitch: 1.5f);
            AdvanceNativeBackend(actual, .04f);
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots.Single().VoiceId, Is.EqualTo(id));
            Assert.That(snapshots[0].Volume, Is.EqualTo(.6f * .5f * .25f).Within(.0001f));
            Assert.That(snapshots[0].Pitch, Is.EqualTo(1f).Within(.0001f));
            // Repeating the same target must not reset interpolation.
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, true, volume: .5f, pitch: 1.5f);
            GameAudio.SetOwnerPaused(owner, true);
            AdvanceNativeBackend(actual, 1f);
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots[0].Pitch, Is.EqualTo(1f).Within(.0001f));
            actual.PauseAll();
            GameAudio.SetOwnerPaused(owner, false);
            AdvanceNativeBackend(actual, 1f);
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots[0].IsPaused, Is.True);
            Assert.That(snapshots[0].Pitch, Is.EqualTo(1f).Within(.0001f));
            actual.ResumeAll(); AdvanceNativeBackend(actual, .04f);
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots[0].Pitch, Is.EqualTo(1.2f).Within(.0001f));
            Assert.That(snapshots[0].Volume, Is.EqualTo(.4f * .5f * .25f).Within(.0001f));
            AudioKit.MuteAll(true);
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots[0].Volume, Is.Zero);
            AudioKit.MuteAll(false);
            GameAudio.StopOwner(owner);
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots, Is.Empty);
        }

        [Test]
        public void NativeLoopFadesRetainBusMuteAndGlobalPauseAndShutdownCleansOutgoingVoice()
        {
            catalog.Find(AudioKeys.Grinder).fadeIn = .2f;
            GameAudio.Shutdown(); AudioKit.ClearBackend(); GameAudio.Initialize(catalog);
            var owner = new object();
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, true);
            var actual = AudioKit.GetBackend();
            var snapshots = new List<AudioVoiceSnapshot>();
            AdvanceNativeBackend(actual, .1f);
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots.Single().Volume, Is.EqualTo(.5f).Within(.0001f));
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, false);
            actual.PauseAll(); AdvanceNativeBackend(actual, 1f);
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots.Count, Is.EqualTo(1));
            actual.SetBusVolume(AudioBus.Sfx, 0f);
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots[0].Volume, Is.Zero);
            actual.ResumeAll(); AdvanceNativeBackend(actual, .15f);
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots, Is.Empty);
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, true);
            GameAudio.SetLoop(owner, "machine", AudioKeys.Grinder, false);
            GameAudio.Shutdown();
            actual.GetActiveVoices(snapshots);
            Assert.That(snapshots, Is.Empty);
            Assert.That(AudioKit.HasBackend, Is.False);
        }

        private static void AdvanceNativeBackend(IAudioBackend actual, float seconds)
        {
            // Test the unscaled step deterministically without waiting for Editor frame timing.
            actual.GetType().GetMethod("Advance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(actual, new object[] { seconds });
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
        public void CafeDayMusicSelectsOnlyRequestedCandidatesAndDoesNotRerollDuringTheDay()
        {
            AddCafeMusicCandidates();
            Random.State previous = Random.state;
            try
            {
                for (int seed = 0; seed < 8; seed++)
                {
                    // Leaving the cafe baseline starts a fresh visit even on the same calendar day.
                    GameAudio.Music(AudioKeys.MenuMusic);
                    Random.InitState(seed);
                    int choice = Random.Range(0, 2);
                    float expectedNextRandom = Random.value;
                    Random.InitState(seed);
                    GameAudio.CafeDayMusic(1);
                    Assert.That(backend.LoadedClip, Is.SameAs(choice == 0 ? clip : secondClip));
                    int plays = backend.PlayCount;
                    GameAudio.CafeDayMusic(1);
                    GameAudio.Music(AudioKeys.CafeMusic); // SceneLoaded/Start must share the same choice.
                    Assert.That(backend.PlayCount, Is.EqualTo(plays));
                    Assert.That(Random.value, Is.EqualTo(expectedNextRandom), "Repeated observations must not consume random choices.");
                }
            }
            finally { Random.state = previous; }
        }

        [Test]
        public void NewCafeDayRerollsOnceWhileOwnedMusicKeepsPriorityAndRestoresTheSelectedBaseline()
        {
            AddCafeMusicCandidates();
            Random.State previous = Random.state;
            try
            {
                GameAudio.CafeDayMusic(1);
                var first = new object();
                var story = new object();
                GameAudio.OverrideMusic(first, AudioKeys.CafeMusic);
                GameAudio.OverrideMusic(story, AudioKeys.MenuMusic);
                int plays = backend.PlayCount;
                Random.InitState(42);
                int choice = Random.Range(0, 2);
                float expectedNextRandom = Random.value;
                Random.InitState(42);
                GameAudio.CafeDayMusic(2);
                GameAudio.CafeDayMusic(2);
                Assert.That(backend.PlayCount, Is.EqualTo(plays), "Daily changes cannot displace an owned story override.");
                Assert.That(Random.value, Is.EqualTo(expectedNextRandom), "A new business day chooses exactly once.");
                GameAudio.StopOwner(story);
                Assert.That(backend.LoadedClip, Is.SameAs(secondClip), "The previous owner still resumes before the baseline.");
                GameAudio.StopOwner(first);
                Assert.That(backend.LoadedClip, Is.SameAs(choice == 0 ? clip : secondClip));
            }
            finally { Random.state = previous; }
        }

        [Test]
        public void CafeDayMusicUsesAvailableCandidateAndFallsBackToLegacyCatalog()
        {
            GameAudio.CafeDayMusic(1);
            Assert.That(backend.LoadedClip, Is.SameAs(secondClip));
            GameAudio.Music(AudioKeys.MenuMusic);
            GameAudio.Shutdown();
            Object.DestroyImmediate(catalog);
            catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            catalog.warnMissing = false;
            catalog.entries.Add(new AudioCue { key = AudioKeys.CafeLeisureMusic, clip = clip, category = AudioCategory.Music, loop = true });
            catalog.entries.Add(new AudioCue { key = AudioKeys.CafeProgressMusic, enabled = false, clip = secondClip });
            GameAudio.Initialize(catalog);
            GameAudio.CafeDayMusic(2);
            Assert.That(backend.LoadedClip, Is.SameAs(clip));
        }

        private void AddCafeMusicCandidates()
        {
            // Add entries before any Find call builds the catalog's lazy index.
            catalog.entries.Add(new AudioCue { key = AudioKeys.CafeLeisureMusic, clip = clip, category = AudioCategory.Music, loop = true });
            catalog.entries.Add(new AudioCue { key = AudioKeys.CafeProgressMusic, clip = secondClip, category = AudioCategory.Music, loop = true });
        }

        [Test]
        public void CityMusicChoosesMapOrBaroqueOncePerVisitAndResumesAfterStory()
        {
            catalog.entries.Add(new AudioCue { key = AudioKeys.CityMusic, clip = clip, category = AudioCategory.Music, loop = true });
            catalog.entries.Add(new AudioCue { key = AudioKeys.CityBaroqueMusic, clip = secondClip, category = AudioCategory.Music, loop = true });
            Random.State previous = Random.state;
            try
            {
                for (int seed = 0; seed < 8; seed++)
                {
                    GameAudio.Music(AudioKeys.MenuMusic);
                    Random.InitState(seed);
                    int choice = Random.Range(0, 2);
                    float expectedNextRandom = Random.value;
                    Random.InitState(seed);
                    GameAudio.Music(AudioKeys.CityMusic);
                    AudioClip expected = choice == 0 ? clip : secondClip;
                    Assert.That(backend.LoadedClip, Is.SameAs(expected));
                    int plays = backend.PlayCount;
                    GameAudio.Music(AudioKeys.CityMusic);
                    Assert.That(backend.PlayCount, Is.EqualTo(plays));
                    var owner = new object();
                    GameAudio.OverrideMusic(owner, AudioKeys.MenuMusic);
                    GameAudio.Music(AudioKeys.CityMusic);
                    GameAudio.StopOwner(owner);
                    Assert.That(backend.LoadedClip, Is.SameAs(expected));
                    Assert.That(Random.value, Is.EqualTo(expectedNextRandom));
                }
            }
            finally { Random.state = previous; }
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

        private sealed class FakeBackend : IAudioBackend, IDynamicAudioLoopBackend
        {
            private int nextId;
            public readonly Dictionary<int, AudioPlayOptions> Active = new Dictionary<int, AudioPlayOptions>();
            public AudioClip LoadedClip;
            public int PlayCount;
            public bool DeferFades;
            public float LastFadeDuration;
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
            public bool SetLoopParameters(int voiceId, float volume, float pitch)
            {
                if (!Active.TryGetValue(voiceId, out AudioPlayOptions options)) return false;
                options.Volume = volume; options.Pitch = pitch;
                Active[voiceId] = options;
                return true;
            }
            public bool Stop(int voiceId) => Active.Remove(voiceId);
            public bool StopWithFade(int voiceId, float duration)
            {
                LastFadeDuration = duration;
                return DeferFades ? Active.ContainsKey(voiceId) : Stop(voiceId);
            }
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
