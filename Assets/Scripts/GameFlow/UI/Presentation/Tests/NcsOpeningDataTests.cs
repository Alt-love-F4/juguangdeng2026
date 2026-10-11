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
        public void GeneratedDefaultsLoadWithoutEnablingNcsGameplayModule()
        {
            NarrativeConfiguration config = LubanNarrativeLoader.Load();
            Assert.That(NarrativePresentationDataAdapter.TryResolveFirstDayLetter(config, null, null, null, out var content, out var error), Is.True);
            StringAssert.Contains("欢迎回家", content.Body);
            StringAssert.Contains("叔叔", content.Signature);
            Assert.That(new LubanOpeningDialogueSource(configuration: config).CreateScript().Count, Is.EqualTo(13));
            Assert.That(DialoguePreviewDialogue.Create().Count, Is.EqualTo(7));
        }

        private sealed class NoopPresenter : INarrativePresenter
        {
            public void Play(NarrativeEventRequest request, Action completed) => completed?.Invoke();
        }
    }
}
