#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using NUnit.Framework;
using Spotlight.Narrative;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>
    /// Generated church page seeks through the shared scene and real dialogue panel.
    /// Formal owners are ready, provider-bound, and deliberately different from preview fixtures.
    /// Disk checks only read the existing GameSaveService directory and known settings preference;
    /// this fixture never writes, deletes, or restores user save files or PlayerPrefs.
    /// </summary>
    public sealed class ChurchLongPagePreviewTests
    {
        private const string Chapter = "church.1104.s1";
        private const string SettingsPreference = "Spotlight.GameFlow.GameSettings";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly JsonSerializerSettings ConfigurationSettings = new JsonSerializerSettings
        { Converters = { new Vector2SnapshotConverter() } };
        private PlayerPresentationTestSession session;
        private CafeNarrativeValueProvider cafeProvider;
        private NarrativeConfiguration configuration;
        private NarrativeChapterPreview preview;
        private PresentationDebugSceneController sceneController;
        private Scene scene, previousScene;
        private bool ownsScene, ownsScenePanel;
        private float previousTimeScale;
        private Type previousPanelType;
        private Func<bool> previousDialogueBlocker, previousPresenterBlocker;
        private int pauseOwners, completions;
        private DialogueHistoryService.Entry[] originalHistory;
        private long originalHistoryPlayback;
        private NarrativeController Controller => session.Narrative.Controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Application.isPlaying, Is.True, "Run with the shared presentation scene in Play Mode.");
            previousScene = SceneManager.GetActiveScene();
            previousTimeScale = Time.timeScale;
            previousPanelType = GameplayPanelHooks.PanelType;
            previousDialogueBlocker = DialoguePanel.ExternalInputBlocked;
            previousPresenterBlocker = NarrativePresenter.ExternalInputBlocked;
            pauseOwners = PresentationPauseRegistry.OwnerCount;
            completions = 0;
            ownsScenePanel = false;
            originalHistory = null;
            scene = SceneManager.GetSceneByPath(PresentationDebugSceneController.ScenePath);
            ownsScene = !scene.IsValid() || !scene.isLoaded;
            if (ownsScene)
            {
                scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                    new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
            }
            Assert.That(scene.isLoaded, Is.True);
            SceneManager.SetActiveScene(scene);
            sceneController = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PresentationDebugSceneController>(true)).Single();
            Assert.That(sceneController.CurrentPanel, Is.Null, "Do not replace another scene preview.");
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False, "Do not replace another dialogue.");
            Time.timeScale = 1f;
            GameplayPanelHooks.PanelType = null;
            DialoguePanel.ExternalInputBlocked = null;
            NarrativePresenter.ExternalInputBlocked = null;

            // Keep a nonempty formal history, so clearing it cannot accidentally pass an empty-list assertion.
            // Restore the exact original in-memory entries and sequence in TearDown, including on failure.
            originalHistory = DialogueHistoryService.Formal.Entries.ToArray();
            originalHistoryPlayback = Field<long>(DialogueHistoryService.Formal, "nextPlayback");
            DialogueHistoryService.Formal.Choice("church page preview isolation sentinel");

            session = new PlayerPresentationTestSession();
            configuration = LubanNarrativeLoader.Load();
            session.Narrative.ResetSession(configuration);
            Controller.BeginValueRestore();
            session.World.State.money = 731;
            session.Narrative.Actions.ActionPoints.SetMax(7);
            session.Narrative.Actions.ActionPoints.Set(3);
            cafeProvider = new CafeNarrativeValueProvider(session.World);
            Controller.BindValueProvider(cafeProvider);
            Controller.MoneyAccount = cafeProvider;
            Controller.BindValueProvider(new DelegateNarrativeValueProvider("dayflow", key => key == "day" ? 30 : (int?)null));
            Controller.BindValueProvider(new DelegateNarrativeValueProvider("actions", key => key == "current"
                ? session.Narrative.Actions.ActionPoints.Current : key == "max" ? session.Narrative.Actions.ActionPoints.Max : (int?)null));
            session.Narrative.AbilityReader = _ => (true, 11);
            Controller.CompleteValueRestore(false);
            Assert.That(session.Narrative.IsReady, Is.True);
            Assert.That(Controller.IsBusy, Is.False);
            Assert.That(Controller.CanCaptureSnapshot, Is.True, "The formal comparison must use a genuinely ready session.");
            Assert.That(Controller.State.TryGetBar("cafe.money", out int money), Is.True);
            Assert.That(money, Is.EqualTo(731));
            Assert.That(configuration.events.Single(e => e.eventId == Chapter).eventProperties.Any(p => p.property == "blockedBy"), Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            preview?.Dispose();
            preview = null;
            if (ownsScenePanel && sceneController != null) sceneController.StopCurrent();
            cafeProvider?.Dispose();
            cafeProvider = null;
            session?.Dispose();
            session = null;
            yield return null;
            if (originalHistory != null)
            {
                var entries = Field<List<DialogueHistoryService.Entry>>(DialogueHistoryService.Formal, "entries");
                entries.Clear();
                entries.AddRange(originalHistory);
                typeof(DialogueHistoryService).GetField("nextPlayback", Private)
                    .SetValue(DialogueHistoryService.Formal, originalHistoryPlayback);
                originalHistory = null;
            }
            GameplayPanelHooks.PanelType = previousPanelType;
            DialoguePanel.ExternalInputBlocked = previousDialogueBlocker;
            NarrativePresenter.ExternalInputBlocked = previousPresenterBlocker;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (ownsScene && scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = previousTimeScale;
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners), "Page preview teardown leaked a pause owner.");
        }

        [UnityTest]
        public IEnumerator TourTargetsOpenAtTheirFirstSourceLineWithBothActorsAndIsolatedFormalOwners()
        {
            foreach (var sample in new[]
            {
                new PageSample("far.market", "p080", "free", "far"),
                new PageSample("far.old.intro", "p163", "one", "far"),
                new PageSample("far.old.perception", "p169", "one", "far", perception: 3),
                new PageSample("far.street.wisdom.pass", "p226", "quoted", "far", wisdom: 4),
                new PageSample("far.street.wisdom.fail", "p234", "one", "far", wisdom: 3),
                new PageSample("near.street", "p255", "free", "near"),
                new PageSample("near.market", "p364", "quoted", "near")
            })
                yield return PlayAndStop(sample);
        }

        [UnityTest]
        public IEnumerator NightTargetsKeepAlienorAliasAndNeverResurrectRetiredAdele()
        {
            foreach (var sample in new[]
            {
                new PageSample("night.question", "p432", "one", "far", "wait", false, rightPresent: false),
                new PageSample("night.nonrevolution", "p441", "free", "near", "ask", false, rightPresent: false),
                new PageSample("night.revolution", "p454", "quoted", "far", "hurry", true, rightPresent: false)
            })
                yield return PlayAndStop(sample);
        }

        [UnityTest]
        public IEnumerator DecisionPagesPermitNullOnlyForChoicesNotYetEncountered()
        {
            // Prior-phase decisions are still explicit; the current page's own choice is not preselected.
            foreach (var sample in new[]
            {
                new PageSample("morning.invite", "p034", null, null),
                new PageSample("tour.depart", "p071", "one", null),
                new PageSample("night.private", "p426", "one", "near", revolution: false, rightPresent: false)
            })
                yield return PlayAndStop(sample);
        }

        [UnityTest]
        public IEnumerator HiddenOrUnresolvedTargetsRejectBeforeOpeningUIAndLeaveFormalSessionUntouched()
        {
            foreach (var sample in new[]
            {
                new PageSample("far.market", "", "one", "near"),
                new PageSample("near.street", "", "one", "far"),
                new PageSample("far.old.perception", "", "one", "far", perception: 2),
                new PageSample("far.street.wisdom.pass", "", "one", "far", wisdom: 3),
                new PageSample("far.street.wisdom.fail", "", "one", "far", wisdom: 4),
                new PageSample("night.nonrevolution", "", "one", "near", "wait", true),
                new PageSample("night.revolution", "", "one", "near", "wait", false),
                new PageSample("night.nonrevolution", "", "one", "near", "wait"),
                new PageSample("night.private", "", "one", "near"),
                new PageSample("fee.one", "", null, null),
                new PageSample("far.market", "", null, "far"),
                new PageSample("far.market", "", "one", null),
                new PageSample("night.question", "", "one", "near", revolution: false),
                new PageSample("night.question", "", "one", null, "wait", false),
                new PageSample("night.question", "", null, "near", "wait", false)
            })
                yield return Reject(sample, typeof(InvalidOperationException));
        }

        [UnityTest]
        public IEnumerator InvalidPageAndChoiceInputsRejectCleanlyIncludingCheckpointAndFadeTargets()
        {
            foreach (var sample in new[]
            {
                new PageSample("missing.page", "", "one", "far"),
                new PageSample("night.fade", "", "one", "near", "wait", false),
                new PageSample("checkpoint.business", "", "one", "far"),
                new PageSample("checkpoint.night", "", "one", "far"),
                new PageSample("far.market", "", "invalid", "far"),
                new PageSample("far.market", "", "one", "invalid"),
                new PageSample("night.question", "", "one", "near", "invalid", false)
            })
                yield return Reject(sample, typeof(ArgumentException));
        }

        [UnityTest]
        public IEnumerator ReadyBusyFormalRuntimeRefusesWithoutClearingTheSharedScenesOwnPanel()
        {
            // Holding the real formal presenter without a dialogue panel isolates the Controller.IsBusy guard.
            session.Narrative.ConversationPlayback = (_, __, ___) => true;
            Assert.That(session.Narrative.QueueEvent("EV_TEST_CONVERSATION"), Is.True);
            yield return WaitFor(() => session.Narrative.IsPresenting, "Formal holding presentation");
            Assert.That(Controller.IsBusy, Is.True);
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False, "This must not pass only because another dialogue is playing.");
            ownsScenePanel = true;
            sceneController.PlayPreBusiness();
            yield return null;
            var ownPanel = sceneController.CurrentPanel;
            Assert.That(ownPanel, Is.TypeOf<PreBusinessPanel>());
            Assert.That(ownPanel.gameObject.activeInHierarchy, Is.True);
            var baseline = CaptureIsolation();
            int owners = PresentationPauseRegistry.OwnerCount;
            var hosts = PresenterHostIds();
            preview = new NarrativeChapterPreview();
            Assert.Throws<InvalidOperationException>(() => Start(new PageSample("far.market", "p080", "one", "far")));
            preview.Dispose();
            preview = null;
            yield return null;
            Assert.That(scene.isLoaded, Is.True);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            Assert.That(sceneController.CurrentPanel, Is.SameAs(ownPanel), "A rejected preview must not call scene StopCurrent.");
            Assert.That(ownPanel.gameObject.activeInHierarchy, Is.True);
            Assert.That(Controller.IsBusy, Is.True);
            Assert.That(session.Narrative.IsPresenting, Is.True);
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(owners));
            CollectionAssert.AreEqual(hosts, PresenterHostIds(), "Rejection must not create an extra presenter UI host.");
            AssertIsolation(baseline);
            Assert.That(completions, Is.Zero);
        }

        private IEnumerator PlayAndStop(PageSample sample)
        {
            var baseline = CaptureIsolation();
            int owners = PresentationPauseRegistry.OwnerCount;
            preview = new NarrativeChapterPreview();
            Start(sample);
            float deadline = Time.realtimeSinceStartup + 10f;
            DialoguePanel panel;
            while ((panel = DialoguePanel.Active) == null || !panel.IsPlaying)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Target dialogue did not start: " + sample.Page);
                yield return null;
            }
            AssertFirstFrame(panel, sample);
            while (!panel.IsReady || panel.GetComponent<CanvasGroup>().alpha < .99f)
            {
                AssertFirstFrame(panel, sample);
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Target dialogue did not become visible: " + sample.Page);
                yield return null;
            }
            AssertFirstFrame(panel, sample);
            Assert.That(preview.IsPlaying, Is.True);
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.GreaterThan(owners));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Presentation pause must not change simulation timeScale.");
            panel.SetPreviewReading(0f, false);
            yield return null;
            TMP_Text body = Field<TMP_Text>(panel, "dialogueText");
            body.ForceMeshUpdate();
            Assert.That(body.gameObject.activeInHierarchy, Is.True);
            Assert.That(body.text, Is.Not.Empty);
            Assert.That(body.textInfo.characterCount, Is.GreaterThan(0));
            Assert.That(body.maxVisibleCharacters, Is.GreaterThanOrEqualTo(body.textInfo.pageInfo[0].lastCharacterIndex + 1));
            Assert.That(panel.IsChoosing, Is.False, "Silent pre-roll must not expose an earlier decision UI.");
            AssertIsolation(baseline);

            // Stop through the real UI owner, then dispose repeatedly: none of these can settle a chapter.
            DialoguePlayback.Stop();
            yield return WaitFor(() => !preview.IsPlaying, "Page preview cancellation");
            preview.Dispose();
            preview.Dispose();
            preview = null;
            yield return null;
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
            Assert.That(completions, Is.Zero, "Stopping a seek is not chapter completion.");
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(owners));
            AssertIsolation(baseline);
        }

        private IEnumerator Reject(PageSample sample, Type expectedException)
        {
            var baseline = CaptureIsolation();
            int owners = PresentationPauseRegistry.OwnerCount;
            var hosts = PresenterHostIds();
            preview = new NarrativeChapterPreview();
            Exception error = Assert.Catch(() => Start(sample), "Expected a clean rejection: " + sample.Page);
            Assert.That(error, Is.InstanceOf(expectedException), sample.Page);
            Assert.That(preview.IsPlaying, Is.False);
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False, "No fallback page may flash for a rejected seek.");
            preview.Dispose();
            preview = null;
            yield return null;
            Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(owners));
            CollectionAssert.AreEqual(hosts, PresenterHostIds());
            Assert.That(completions, Is.Zero);
            AssertIsolation(baseline);
        }

        private void Start(PageSample sample)
            => preview.PlayChurchLongPageConstruction(Chapter + "." + sample.Page, sample.Fee, sample.Route, sample.Night,
                sample.Perception, sample.Wisdom, sample.Revolution, () => completions++);

        private void AssertFirstFrame(DialoguePanel panel, PageSample sample)
        {
            Assert.That(panel.CurrentSource, Is.Not.Null);
            Assert.That(panel.CurrentSource.EventId, Is.EqualTo(Chapter));
            Assert.That(panel.CurrentSource.PageId, Is.EqualTo(Chapter + "." + sample.Page));
            Assert.That(panel.CurrentSource.SourceLineId, Is.EqualTo(Chapter + "." + sample.Line));
            Assert.That(panel.LineIndex, Is.Zero, "Pre-roll must never visibly replay earlier source lines.");
            AssertPortrait(panel.LeftCharacter, panel.LeftPortrait, "alienor");
            if (sample.RightPresent) AssertPortrait(panel.RightCharacter, panel.RightPortrait, "adele");
            else
            {
                Assert.That(panel.RightCharacter == null || string.IsNullOrEmpty(panel.RightCharacter.CharacterKey), Is.True,
                    "Adèle must stay retired after source line p426, including p432's first frame.");
                Assert.That(panel.RightPortrait.enabled, Is.False);
            }
            Assert.That(panel.CenterPortrait.enabled, Is.False);
            string alias = configuration.texts.Single(text => text.key == Chapter + ".alias.alienor.name").content;
            var line = configuration.presentations.conversationLines.Single(item => item.lineId == Chapter + "." + sample.Line);
            // p071 explicitly rebuilds the canonical non-speaking actor. Her alias is introduced
            // when she next speaks at p073; seeking must not look ahead to that later source line.
            string expectedStageName = line.stageLeftKey == "alienor" && line.speakerKey != "alienor"
                ? NarrativeCharacterResolver.Find("alienor").displayName : alias;
            Assert.That(panel.LeftCharacter.DisplayName, Is.EqualTo(expectedStageName));
            if (line.speakerKey == "alienor") Assert.That(Field<TMP_Text>(panel, "speakerText").text, Is.EqualTo(alias));

            var page = configuration.presentations.conversationPages.Single(item => item.pageId == Chapter + "." + sample.Page);
            var script = Field<DialogueScript>(panel, "script");
            Assert.That(script.IsPreview, Is.True);
            Assert.That(script.BackgroundVisual, Is.Not.Null);
            Assert.That(script.BackgroundVisual.key, Is.EqualTo(page.bgOverride), "Check stable art identity, not only a shared placeholder color.");
            var expected = NarrativeStageVisuals.Resolve(page.bgOverride);
            Assert.That(panel.BackgroundLayer.enabled, Is.True);
            Assert.That(panel.BackgroundLayer.texture == expected.texture, Is.True,
                "Use Unity texture identity (including fake-null), after checking the exact registry key.");
            Color tint = expected.placeholder ? expected.placeholderColor : expected.tint;
            Assert.That(panel.BackgroundLayer.color.r, Is.EqualTo(tint.r).Within(.001f));
            Assert.That(panel.BackgroundLayer.color.g, Is.EqualTo(tint.g).Within(.001f));
            Assert.That(panel.BackgroundLayer.color.b, Is.EqualTo(tint.b).Within(.001f));
        }

        private static void AssertPortrait(DialogueCharacter character, Image image, string key)
        {
            Assert.That(character, Is.Not.Null);
            Assert.That(character.CharacterKey, Is.EqualTo(key), "Display aliases must never replace stable actor identity.");
            Assert.That(character.Portrait, Is.Not.Null);
            Assert.That(image.enabled, Is.True);
            Assert.That(image.sprite, Is.SameAs(character.Portrait));
            Assert.That(image.transform.localScale.x, Is.GreaterThan(0f), "Original portrait orientation must be preserved.");
        }

        private IsolationSnapshot CaptureIsolation()
            => new IsolationSnapshot
            {
                Runtime = NarrativeRuntime.Instance,
                Player = PlayerRuntime.Current,
                ConfigurationJson = JsonConvert.SerializeObject(configuration, ConfigurationSettings),
                GeneratedConfigurationJson = JsonConvert.SerializeObject(LubanNarrativeLoader.Load(), ConfigurationSettings),
                NarrativeJson = JsonUtility.ToJson(session.Narrative.CaptureSnapshot()),
                ActionJson = session.Narrative.Actions.CaptureJson(),
                PlayerJson = JsonUtility.ToJson(PlayerRuntime.Current.CaptureState()),
                WorldJson = JsonUtility.ToJson(session.World.State),
                ActionPoints = session.Narrative.Actions.ActionPoints.Current,
                ActionPointMax = session.Narrative.Actions.ActionPoints.Max,
                Money = session.World.State.money,
                History = HistoryFingerprint(),
                HasSettings = PlayerPrefs.HasKey(SettingsPreference),
                Settings = PlayerPrefs.GetString(SettingsPreference, string.Empty),
                Saves = ExistingSaveFingerprint()
            };

        private void AssertIsolation(IsolationSnapshot before)
        {
            Assert.That(NarrativeRuntime.Instance, Is.SameAs(before.Runtime));
            Assert.That(PlayerRuntime.Current, Is.SameAs(before.Player));
            Assert.That(session.Narrative.Configuration, Is.SameAs(configuration));
            Assert.That(JsonUtility.ToJson(session.Narrative.CaptureSnapshot()), Is.EqualTo(before.NarrativeJson), "Full formal runtime state changed.");
            Assert.That(session.Narrative.Actions.CaptureJson(), Is.EqualTo(before.ActionJson));
            Assert.That(JsonUtility.ToJson(PlayerRuntime.Current.CaptureState()), Is.EqualTo(before.PlayerJson));
            Assert.That(JsonUtility.ToJson(session.World.State), Is.EqualTo(before.WorldJson));
            Assert.That(session.Narrative.Actions.ActionPoints.Current, Is.EqualTo(before.ActionPoints));
            Assert.That(session.Narrative.Actions.ActionPoints.Max, Is.EqualTo(before.ActionPointMax));
            Assert.That(session.World.State.money, Is.EqualTo(before.Money));
            Assert.That(cafeProvider.TryRead(out int balance), Is.True);
            Assert.That(balance, Is.EqualTo(before.Money));
            Assert.That(HistoryFingerprint(), Is.EqualTo(before.History), "Preview reveal/stop must not append to or clear formal history.");
            Assert.That(JsonConvert.SerializeObject(configuration, ConfigurationSettings), Is.EqualTo(before.ConfigurationJson), "Formal configuration was mutated.");
            Assert.That(JsonConvert.SerializeObject(LubanNarrativeLoader.Load(), ConfigurationSettings), Is.EqualTo(before.GeneratedConfigurationJson), "Fresh generated configuration must stay blocked and unchanged.");
            Assert.That(PlayerPrefs.HasKey(SettingsPreference), Is.EqualTo(before.HasSettings));
            Assert.That(PlayerPrefs.GetString(SettingsPreference, string.Empty), Is.EqualTo(before.Settings));
            CollectionAssert.AreEqual(before.Saves, ExistingSaveFingerprint(), "Preview must not create, touch, or overwrite actual save files.");
        }

        private static string HistoryFingerprint()
            => Field<long>(DialogueHistoryService.Formal, "nextPlayback") + ":" +
                string.Join("|", DialogueHistoryService.Formal.Entries.Select(entry =>
                    entry.Speaker.Length + ":" + entry.Speaker + entry.Text.Length + ":" + entry.Text));

        private static string[] ExistingSaveFingerprint()
        {
            // This is the default root verified in GameSaveService's constructor. Never instantiate storage:
            // even a test-created directory or sentinel would modify the user's disk.
            string root = Path.Combine(Application.persistentDataPath, "Spotlight", "Saves");
            if (!Directory.Exists(root)) return new[] { "<save-directory-absent>" };
            using (var hash = SHA256.Create())
                return new[] { "<save-directory-present>" }.Concat(Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal).Select(path =>
                    {
                        var info = new FileInfo(path);
                        using (var stream = File.OpenRead(path))
                            return path.Substring(root.Length) + ":" + info.Length + ":" + info.LastWriteTimeUtc.Ticks
                                + ":" + Convert.ToBase64String(hash.ComputeHash(stream));
                    })).ToArray();
        }

        private static int[] PresenterHostIds()
            => Resources.FindObjectsOfTypeAll<NarrativePresentationUI>().Select(host => host.GetInstanceID()).OrderBy(id => id).ToArray();

        private static T Field<T>(object target, string name)
            => (T)target.GetType().GetField(name, Private).GetValue(target);

        private static IEnumerator WaitFor(Func<bool> condition, string operation)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), operation + " timed out.");
                yield return null;
            }
        }

        private sealed class PageSample
        {
            internal readonly string Page, Line, Fee, Route, Night;
            internal readonly bool? Revolution;
            internal readonly int Perception, Wisdom;
            internal readonly bool RightPresent;
            internal PageSample(string page, string line, string fee, string route, string night = null,
                bool? revolution = null, int perception = 2, int wisdom = 3, bool rightPresent = true)
            {
                Page = page; Line = line; Fee = fee; Route = route; Night = night;
                Revolution = revolution; Perception = perception; Wisdom = wisdom; RightPresent = rightPresent;
            }
        }

        // Serialize authored coordinates, not Unity's computed normalized property (which recurses).
        private sealed class Vector2SnapshotConverter : JsonConverter
        {
            public override bool CanConvert(Type type) => type == typeof(Vector2);
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var vector = (Vector2)value;
                writer.WriteStartObject();
                writer.WritePropertyName("x"); writer.WriteValue(vector.x);
                writer.WritePropertyName("y"); writer.WriteValue(vector.y);
                writer.WriteEndObject();
            }
            public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
                => throw new NotSupportedException("Snapshot comparison only.");
            public override bool CanRead => false;
        }

        private sealed class IsolationSnapshot
        {
            internal NarrativeRuntime Runtime;
            internal PlayerRuntime Player;
            internal string ConfigurationJson, GeneratedConfigurationJson, NarrativeJson, ActionJson, PlayerJson, WorldJson, History, Settings;
            internal int ActionPoints, ActionPointMax, Money;
            internal bool HasSettings;
            internal string[] Saves;
        }
    }
}
#endif
