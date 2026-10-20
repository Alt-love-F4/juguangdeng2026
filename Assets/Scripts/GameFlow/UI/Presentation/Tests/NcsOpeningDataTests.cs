using System;
using System.Linq;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NcsOpeningDataTests
    {
        private CharacterCatalog catalog;

        [SetUp]
        public void LoadFreshTableCharacters()
        {
            catalog = CharacterCatalog.LoadFromTables();
            NarrativeCharacterResolver.SetCatalog(catalog);
        }

        [TearDown]
        public void ReleaseTableCharacters()
        {
            NarrativeCharacterResolver.SetCatalog(null);
            if (catalog != null) Object.DestroyImmediate(catalog);
        }

        [Test]
        public void SelectedOpeningIsSortedAndPreservesSpeakerGestureAndTiming()
        {
            NarrativeConfiguration config = LubanNarrativeLoader.Load();
            config.openingPresentations.Add(new NarrativeOpeningPresentationDefinition
            {
                presentationId = "test-selected", titleKey = "test.title", leftNameKey = "test.left",
                rightNameKey = "test.right", visualsResource = "Dialogue/DialoguePreview"
            });
            config.texts.Add(new NarrativeTextDefinition { key = "test.title", content = "指定演出" });
            config.texts.Add(new NarrativeTextDefinition { key = "test.left", content = "左侧" });
            config.texts.Add(new NarrativeTextDefinition { key = "test.right", content = "右侧" });
            config.texts.Add(new NarrativeTextDefinition { key = "test.line.1", content = "第一句" });
            config.texts.Add(new NarrativeTextDefinition { key = "test.line.2", content = "第二句" });
            config.openingLines.Add(new NarrativeOpeningLineDefinition { lineId = "test-2", presentationId = "test-selected",
                seq = 20, speaker = "right", gesture = "shake", textKey = "test.line.2", autoAdvanceSeconds = 1.5f });
            config.openingLines.Add(new NarrativeOpeningLineDefinition { lineId = "test-1", presentationId = "test-selected",
                seq = 10, speaker = "left", gesture = "nod", textKey = "test.line.1" });

            DialogueScript script = new LubanOpeningDialogueSource("test-selected", config).CreateScript();
            Assert.That(script.Count, Is.EqualTo(2));
            Assert.That(script.Title, Is.EqualTo("指定演出"));
            Assert.That(script.LeftCharacter.CharacterKey, Is.EqualTo("player"));
            Assert.That(script.RightCharacter.CharacterKey, Is.EqualTo("adele"));
            Assert.That(script.LeftCharacter.DisplayName, Is.EqualTo(NarrativeCharacterResolver.Find("player").displayName));
            Assert.That(script.RightCharacter.DisplayName, Is.EqualTo(NarrativeCharacterResolver.Find("adele").displayName));
            Assert.That(script.LeftCharacter.Portrait, Is.SameAs(NarrativeCharacterResolver.Find("player").PortraitFor(null)));
            Assert.That(script.RightCharacter.Portrait, Is.SameAs(NarrativeCharacterResolver.Find("adele").PortraitFor(null)));
            Assert.That(script[0].Text, Is.EqualTo("第一句"));
            Assert.That(script[0].Gesture, Is.EqualTo(DialogueGesture.Nod));
            Assert.That(script[1].Speaker, Is.EqualTo(DialogueSpeaker.Right));
            Assert.That(script[1].Gesture, Is.EqualTo(DialogueGesture.Shake));
            Assert.That(script[1].AutoAdvanceSeconds, Is.EqualTo(1.5f));
            Assert.That(script.BackgroundVisual, Is.Null);
            Assert.That(script.Background, Is.SameAs(Resources.Load<DialogueVisualsAsset>("Dialogue/DialoguePreview").Background));
        }

        [TestCase("first_day_cafe")]
        [TestCase("cafe_tutorial.customer_arrives")]
        [TestCase("cafe_tutorial.workstation_open")]
        [TestCase("cafe_tutorial.demo_serve")]
        [TestCase("cafe_tutorial.result_refund")]
        public void SharedOpeningVisualsUseTheCafeSceneWithoutOverlayArtwork(string presentationId)
        {
            var config = LubanNarrativeLoader.Load();
            Assert.That(NarrativePresentationDataAdapter.TryCreateOpeningDialogue(config, presentationId,
                out var script, out var error), Is.True, error);
            Assert.That(script.BackgroundVisual, Is.Not.Null);
            Assert.That(script.BackgroundVisual.useSceneBackground, Is.True);
            Assert.That(script.BackgroundVisual.key, Is.EqualTo(NarrativeBackgroundKeys.CafeScene));
            Assert.That(script.Background, Is.Null);
        }

        [Test]
        public void TutorialCustomerUsesAuthoredFemaleTwoThirdsSilhouetteWithoutChangingSharedVisuals()
        {
            var config = LubanNarrativeLoader.Load();
            foreach (var presentation in config.openingPresentations.Where(item => item.presentationId.StartsWith("cafe_tutorial.")))
                Assert.That(presentation.visualsResource, Is.EqualTo("Dialogue/CafeTutorialVisuals"));
            var visuals = Resources.Load<DialogueVisualsAsset>("Dialogue/CafeTutorialVisuals");
            Assert.That(visuals, Is.Not.Null);
            Assert.That(visuals.BackgroundKey, Is.EqualTo(NarrativeBackgroundKeys.CafeScene));
            Assert.That(visuals.CenterPortrait, Is.Not.Null);
            Assert.That(visuals.CenterPortrait, Is.SameAs(Resources.Load<Sprite>("Characters/silhouettes/female/neutral_placeholder")));
            Assert.That(Resources.Load<DialogueVisualsAsset>("Dialogue/CafeSceneVisuals").CenterPortrait, Is.Null,
                "The generic cafe resource must not acquire a tutorial-only guest.");
            Assert.That(NarrativePresentationDataAdapter.TryCreateOpeningDialogue(config, "cafe_tutorial.customer_arrives",
                out var script, out var error), Is.True, error);
            Assert.That(script.CenterCharacter, Is.Not.Null);
            Assert.That(script.CenterCharacter.DisplayName, Is.EqualTo("B太太"));
            Assert.That(script.CenterCharacter.Portrait, Is.SameAs(visuals.CenterPortrait));
            Assert.That(script.CenterCharacter.Portrait, Is.Not.SameAs(script.RightCharacter.Portrait));
            Assert.That(NarrativePresentationDataAdapter.TryCreateOpeningDialogue(config, "first_day_cafe",
                out script, out error), Is.True, error);
            Assert.That(script.CenterCharacter, Is.Null, "The guest must not appear before arrival.");
        }

        [Test]
        public void MissingOpeningOrTextReturnsDiagnosticInsteadOfRawKey()
        {
            NarrativeConfiguration config = LubanNarrativeLoader.Load();
            Assert.That(NarrativePresentationDataAdapter.TryCreateOpeningDialogue(config, "missing", out var script, out var error), Is.False);
            Assert.That(script, Is.Null);
            StringAssert.Contains("missing", error);
            config.texts.RemoveAll(text => text.key == "opening.line.01");
            Assert.That(NarrativePresentationDataAdapter.TryCreateOpeningDialogue(config, "first_day_cafe", out script, out error), Is.False);
            StringAssert.Contains("opening.line.01", error);
        }

        [Test]
        public void OpeningStillValidatesLegacyNameKeysBeforeUsingTableCharacters()
        {
            NarrativeConfiguration config = LubanNarrativeLoader.Load();
            string key = config.openingPresentations.First(item => item.presentationId == "first_day_cafe").leftNameKey;
            config.texts.RemoveAll(text => text.key == key);
            Assert.That(NarrativePresentationDataAdapter.TryCreateOpeningDialogue(config, "first_day_cafe", out var script, out var error), Is.False);
            Assert.That(script, Is.Null);
            StringAssert.Contains("character name", error);
        }

        [Test]
        public void LetterReadsExactSelectedKeysAndMissingValueUsesCompleteFallback()
        {
            var config = new NarrativeConfiguration();
            config.texts.Add(new NarrativeTextDefinition { key = "custom.body", content = "<原文>\n新正文" });
            config.texts.Add(new NarrativeTextDefinition { key = "custom.signature", content = "新署名" });
            var fallback = new FirstDayLetterContent("草稿正文", "草稿署名");
            Assert.That(NarrativePresentationDataAdapter.TryResolveFirstDayLetter(config, "custom.body", "custom.signature",
                fallback, out var content, out var error), Is.True);
            Assert.That(content.Body, Is.EqualTo("<原文>\n新正文"));
            Assert.That(content.Signature, Is.EqualTo("新署名"));
            Assert.That(error, Is.Null);
            Assert.That(NarrativePresentationDataAdapter.TryResolveFirstDayLetter(config, "custom.body", "missing.signature",
                fallback, out content, out error), Is.False);
            Assert.That(content, Is.SameAs(fallback));
            StringAssert.Contains("missing.signature", error);
        }

        [Test]
        public void RuntimeResolverWinsAndExplicitConfigurationStillOverridesRuntime()
        {
            NarrativeConfiguration config = LubanNarrativeLoader.Load();
            var owner = new GameObject("NcsOpeningResolverTest");
            owner.SetActive(false); // Inject before Awake; do not create a live NCS stage or replace the scene session.
            try
            {
                var runtime = owner.AddComponent<NarrativeRuntime>();
                runtime.Initialize(config, new NoopPresenter(), key => "resolved:" + key);
                DialogueScript script = new LubanOpeningDialogueSource(runtime: runtime).CreateScript();
                Assert.That(script.Title, Is.EqualTo("resolved:opening.title"));
                Assert.That(script[0].Text, Is.EqualTo("resolved:opening.line.01"));
                Assert.That(NarrativePresentationDataAdapter.TryResolveFirstDayLetter(runtime, null, null, null,
                    out var content, out var error), Is.True);
                Assert.That(content.Body, Is.EqualTo("resolved:opening.letter.body"));
                Assert.That(content.Signature, Is.EqualTo("resolved:opening.letter.signature"));
                var explicitConfig = LubanNarrativeLoader.Load();
                string original = explicitConfig.texts.First(text => text.key == "opening.title").content;
                Assert.That(new LubanOpeningDialogueSource(configuration: explicitConfig, runtime: runtime).CreateScript().Title,
                    Is.EqualTo(original));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void PlayerNamePlaceholderIsResolvedForConfiguredAndRuntimeText()
        {
            var config = new NarrativeConfiguration();
            config.texts.Add(new NarrativeTextDefinition { key = "configured", content = "馆长：{playername} / ｛playername｝ / ｛playername｝" });
            var owner = new GameObject("NcsPlayerNameResolverTest");
            owner.SetActive(false);
            try
            {
                var runtime = owner.AddComponent<NarrativeRuntime>();
                string name = "阿黛尔";
                runtime.PlayerNameProvider = () => name;
                runtime.Initialize(config, new NoopPresenter(), key => key == "runtime" ? "外部：{playername}" : key);

                Assert.That(runtime.ResolveText("configured"), Is.EqualTo("馆长：阿黛尔 / 阿黛尔 / 阿黛尔"));
                Assert.That(runtime.ResolveText("runtime"), Is.EqualTo("外部：阿黛尔"));
                name = "";
                Assert.That(runtime.ResolveText("configured"), Is.EqualTo("馆长：{playername} / ｛playername｝ / ｛playername｝"));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void GeneratedDefaultsLoadWithoutEnablingNcsGameplayModule()
        {
            NarrativeConfiguration config = LubanNarrativeLoader.Load();
            Assert.That(NarrativePresentationDataAdapter.TryResolveFirstDayLetter(config, null, null, null, out var content, out var error), Is.True);
            StringAssert.Contains("欢迎回家", content.Body);
            StringAssert.Contains("叔叔", content.Signature);
            Assert.That(config.objects.Any(item => item.objId == "adele"), Is.True,
                "The formal opening requires Adele in the generated Object table.");
            DialogueScript script = new LubanOpeningDialogueSource(configuration: config).CreateScript();
            Assert.That(script.Count, Is.EqualTo(8), "Empty generated text rows are separators and are omitted consistently in game and debug playback.");
            Assert.That(script.LeftCharacter.CharacterKey, Is.EqualTo("player"));
            Assert.That(script.RightCharacter.CharacterKey, Is.EqualTo("adele"));
            Assert.That(script.RightCharacter.DisplayName, Is.EqualTo("阿黛尔·米尼耶"));
            Assert.That(script.RightCharacter.Portrait, Is.Not.Null,
                "The formal opening must load Adele's existing portrait from the Object table.");
            Assert.That(script.RightCharacter.Portrait,
                Is.SameAs(Resources.Load<Sprite>("Characters/adele/neutral")));
        }

        private sealed class NoopPresenter : INarrativePresenter
        {
            public void Play(NarrativeEventRequest request, Action completed) => completed?.Invoke();
        }
    }
}
