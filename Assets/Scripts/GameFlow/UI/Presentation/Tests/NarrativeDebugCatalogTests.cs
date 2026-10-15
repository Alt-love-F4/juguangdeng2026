using System.Linq;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativeDebugCatalogTests
    {
        private CharacterCatalog characters;
        private NarrativeConfiguration configuration;
        private NarrativeDebugCatalog catalog;
        private string Resolve(string key) => configuration.texts.FirstOrDefault(t => t.key == key)?.content ?? key ?? "";

        [SetUp]
        public void SetUp()
        {
            configuration = LubanNarrativeLoader.Load();
            characters = CharacterCatalog.LoadFromTables();
            NarrativeCharacterResolver.SetCatalog(characters);
            catalog = new NarrativeDebugCatalog(configuration, Resolve);
        }
        [TearDown]
        public void TearDown()
        {
            NarrativeCharacterResolver.SetCatalog(null);
            if (characters != null) Object.DestroyImmediate(characters);
        }
        [Test]
        public void GeneratedEventsAreSearchableByIdAndChineseTitle()
        {
            var item = configuration.events.First(e => !string.IsNullOrEmpty(Resolve(e.titleText)) && Resolve(e.titleText).Any(c => c >= '\u4e00' && c <= '\u9fff'));
            Assert.That(catalog.Search(item.eventId).Any(e => e.Kind == "Event" && e.Key == item.eventId), Is.True);
            Assert.That(catalog.Search(Resolve(item.titleText)).Any(e => e.Kind == "Event" && e.Key == item.eventId), Is.True);
        }
        [Test]
        public void GeneratedConversationBodyAndSourceIdFindTheCorrectPage()
        {
            var line = configuration.presentations.conversationLines.First(l => !string.IsNullOrWhiteSpace(Resolve(l.textKey)));
            Assert.That(catalog.Search(Resolve(line.textKey)).Any(e => e.Kind == "Conversation" && e.PageId == line.pageId), Is.True);
            Assert.That(catalog.Search(line.lineId).Any(e => e.Kind == "Conversation" && e.PageId == line.pageId), Is.True);
        }
        [Test]
        public void SharedPresentationKeepsEveryEventReference()
        {
            var page = configuration.presentations.conversationPages.First();
            // Deliberately reuse a real generated presentation; no single-event reverse lookup is allowed.
            configuration.events.Add(new NarrativeEventDefinition { eventId = "catalog-test-event-a", presentationKey = page.presentationKey });
            configuration.events.Add(new NarrativeEventDefinition { eventId = "catalog-test-event-b", presentationKey = page.presentationKey });
            catalog = new NarrativeDebugCatalog(configuration, Resolve);
            var entry = catalog.Entries.First(e => e.Kind == "Conversation" && e.PageId == page.pageId);
            Assert.That(entry.EventIds, Does.Contain("catalog-test-event-a"));
            Assert.That(entry.EventIds, Does.Contain("catalog-test-event-b"));
            foreach (var actual in configuration.events.Where(e => e.presentationKey == page.presentationKey))
                Assert.That(entry.EventIds, Does.Contain(actual.eventId));
        }
        [Test]
        public void GeneratedOpeningBuildsAndLocatesSourceInsteadOfSnapshotIndex()
        {
            var entry = catalog.Entries.First(e => e.Kind == "Opening");
            var source = configuration.openingLines.Where(l => l.presentationId == entry.Key).OrderBy(l => l.seq).ToArray();
            var script = catalog.CreateScript(entry);
            Assert.That(script.Count, Is.EqualTo(source.Length));
            Assert.That(script[0].Text, Is.EqualTo(Resolve(source[0].textKey)));
            Assert.That(catalog.SourceAt(entry, 0), Does.Contain("sourceLineId=" + source[0].lineId));
            Assert.That(catalog.SourceAt(entry, 0), Does.Contain("seq=" + source[0].seq));
        }
        [Test]
        public void GeneratedConversationBuildsWithSourceIdentityAndDisabledPreviewChoices()
        {
            var entry = catalog.Entries.First(e => e.Kind == "Conversation" && configuration.presentations.conversationLines.Any(l => l.pageId == e.PageId));
            var source = configuration.presentations.conversationLines.Where(l => l.pageId == entry.PageId).OrderBy(l => l.seq).ToArray();
            var script = catalog.CreateScript(entry);
            Assert.That(script.Count, Is.EqualTo(source.Length));
            Assert.That(script[0].Source.SourceLineId, Is.EqualTo(source[0].lineId));
            Assert.That(script[0].Source.PageId, Is.EqualTo(entry.PageId));
            Assert.That(script[0].Source.Sequence, Is.EqualTo(source[0].seq));
            Assert.That(script.Options.All(o => !o.Enabled), Is.True);
            Assert.That(catalog.SourceAt(entry, 0), Does.Contain(source[0].lineId));
            Assert.That(catalog.SourceAt(entry, source.Length), Is.EqualTo("源行不可用"));
        }
    }
}
