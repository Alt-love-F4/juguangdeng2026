using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using Spotlight.Narrative;

namespace Spotlight.GameFlow.Tests
{
    public sealed class PresentationDebugDialogueSourceTests
    {
        [Test]
        public void GeneratedTablesListAllDialoguesAndEveryEntryCreatesWithoutLegacyCharacters()
        {
            NarrativeCharacterResolver.SetCatalog(null);
            var configuration = LubanNarrativeLoader.Load();
            var entries = PresentationDebugDialogueSource.List(configuration);
            var expected = configuration.openingPresentations.Select(item => "opening:" + item.presentationId)
                .Concat(configuration.presentations.index
                    .Where(item => string.Equals(item.presentationClass, "Conversation", StringComparison.OrdinalIgnoreCase))
                    .Select(item => "conversation:" + item.presentationKey)).ToArray();
            CollectionAssert.AreEquivalent(expected, entries.Select(item => item.Id));
            Assert.That(entries.Select(item => item.Id).Distinct().Count(), Is.EqualTo(entries.Count));
            foreach (var entry in entries)
            {
                Assert.That(entry.Label, Is.Not.Empty, entry.Id);
                var pages = PresentationDebugDialogueSource.Create(configuration, entry.Id);
                Assert.That(pages.Count, Is.GreaterThan(0), entry.Id);
                Assert.That(pages.All(page => page.Count > 0), Is.True, entry.Id);
            }
        }

        [Test]
        public void ConversationSnapshotsSortPagesLinesAndChoicesWithoutMutatingOrBindingResults()
        {
            var configuration = new NarrativeConfiguration();
            configuration.objects.Add(new NarrativeObjectDefinition { objId = "fixture.speaker", nameText = "speaker.name" });
            void Text(string key, string text) => configuration.texts.Add(new NarrativeTextDefinition { key = key, content = text });
            Text("speaker.name", "自定义角色"); Text("one", "第一句"); Text("two", "第二句");
            Text("three", "下一页"); Text("choice.one", "选项一"); Text("choice.two", "选项二");
            configuration.presentations.index.Add(new NarrativePresentationIndexDefinition
                { presentationKey = "fixture", presentationClass = "Conversation", logTag = "fixture" });
            configuration.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = "later", presentationKey = "fixture", seq = 20 });
            configuration.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = "first", presentationKey = "fixture", seq = 10, optionGroupKey = "choices", pageCondKeys = "not_satisfied" });
            configuration.presentations.conversationLines.Add(new NarrativeConversationLineDefinition
                { lineId = "second", pageId = "first", seq = 20, textKey = "two" });
            configuration.presentations.conversationLines.Add(new NarrativeConversationLineDefinition
                { lineId = "third", pageId = "later", seq = 1, textKey = "three" });
            configuration.presentations.conversationLines.Add(new NarrativeConversationLineDefinition
            {
                lineId = "first", pageId = "first", seq = 10, textKey = "one", speakerKey = "fixture.speaker", side = "left",
                emote = "happy", sfxKey = "fixture.sfx", voiceKey = "fixture.voice", accentFxKey = "fixture.accent", accentTargetKey = "fixture.speaker"
            });
            configuration.choices.Add(new NarrativeChoiceDefinition
                { choiceId = "second", groupKey = "choices", seq = 20, textKey = "choice.two", resultOwnerId = "must_not_execute" });
            configuration.choices.Add(new NarrativeChoiceDefinition
                { choiceId = "first", groupKey = "choices", seq = 10, textKey = "choice.one", resultOwnerId = "must_not_execute" });
            string before = JsonConvert.SerializeObject(configuration);
            var pages = PresentationDebugDialogueSource.Create(configuration, "conversation:fixture");
            Assert.That(pages.Count, Is.EqualTo(2));
            Assert.That(pages[0][0].Text, Is.EqualTo("第一句"));
            Assert.That(pages[0][1].Text, Is.EqualTo("第二句"));
            Assert.That(pages[1][0].Text, Is.EqualTo("下一页"));
            Assert.That(pages[0][0].LeftCharacter.CharacterKey, Is.EqualTo("fixture.speaker"));
            Assert.That(pages[0][0].LeftCharacter.DisplayName, Is.EqualTo("自定义角色"));
            Assert.That(pages[0][0].SfxKey, Is.EqualTo("fixture.sfx"));
            Assert.That(pages[0][0].VoiceKey, Is.EqualTo("fixture.voice"));
            Assert.That(pages[0][0].AccentFxKey, Is.EqualTo("fixture.accent"));
            Assert.That(pages[0][0].AccentTargetKey, Is.EqualTo("fixture.speaker"));
            CollectionAssert.AreEqual(new[] { "first", "second" }, pages[0].Options.Select(option => option.OptionId));
            foreach (var option in pages[0].Options)
            {
                Assert.That(option.Enabled, Is.True);
                Assert.That(option.ResultKey, Is.Null.Or.Empty);
                Assert.That(option.Response, Is.Null);
            }
            Assert.That(JsonConvert.SerializeObject(configuration), Is.EqualTo(before));
        }

        [TestCase("opening:missing")]
        [TestCase("conversation:missing")]
        [TestCase("unsupported:missing")]
        public void UnknownEntryReportsTheRequestedId(string id)
        {
            var error = Assert.Throws<InvalidOperationException>(() => PresentationDebugDialogueSource.Create(new NarrativeConfiguration(), id));
            StringAssert.Contains(id, error.Message);
        }
    }
}
