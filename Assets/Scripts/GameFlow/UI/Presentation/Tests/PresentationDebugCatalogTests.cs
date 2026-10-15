using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using Spotlight.Narrative;

namespace Spotlight.GameFlow.Tests
{
    public sealed class PresentationDebugCatalogTests
    {
        [Test]
        public void GeneratedCatalogIncludesEveryOpeningPresentationEventAndSetup()
        {
            var config = LubanNarrativeLoader.Load();
            string before = JsonConvert.SerializeObject(config, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            var entries = PresentationDebugCatalog.List(config);
            Assert.That(entries.Select(x => x.Id).Distinct().Count(), Is.EqualTo(entries.Count));
            foreach (var opening in config.openingPresentations)
            {
                Assert.That(entries.Any(x => x.Id == "opening:" + opening.presentationId), Is.True);
                Assert.That(NarrativePresentationDataAdapter.TryCreateOpeningDialogue(config,
                    opening.presentationId, out var script, out var error), Is.True, opening.presentationId + ": " + error);
                Assert.That(script.Count, Is.GreaterThan(0));
            }
            foreach (var item in config.presentations.index)
            {
                string id = item.presentationClass.ToLowerInvariant() + ":" + item.presentationKey;
                Assert.That(entries.Any(x => x.Id == id), Is.True);
                var definition = PresentationDebugCatalog.Event(config, id);
                Assert.That(definition.presentationKey, Is.EqualTo(item.presentationKey));
                Assert.That(definition.presentationClass, Is.EqualTo(item.presentationClass));
            }
            foreach (var setup in config.setups)
                Assert.That(PresentationDebugCatalog.Event(config, "setup:" + setup.setupKey).setupKey, Is.EqualTo(setup.setupKey));
            foreach (var definition in config.events)
            {
                var setup = config.setups.Find(x => x.setupKey == definition.setupKey);
                if (NarrativePresenter.RequiresPresentation(definition, setup))
                    Assert.That(entries.Any(x => x.Id == "event:" + definition.eventId), Is.True);
            }
            Assert.That(JsonConvert.SerializeObject(config, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }), Is.EqualTo(before));
        }

        [Test]
        public void NewlyAddedPresentationAndSetupAppearWithoutSceneBindings()
        {
            var config = new NarrativeConfiguration();
            config.presentations.index.Add(new NarrativePresentationIndexDefinition
                { presentationKey = "future.reading", presentationClass = "Reading" });
            config.setups.Add(new NarrativeSetupParamsDefinition { setupKey = "future.fx", tplName = "future.template" });
            var ids = PresentationDebugCatalog.List(config).Select(x => x.Id).ToArray();
            CollectionAssert.AreEquivalent(new[] { "reading:future.reading", "setup:future.fx" }, ids);
        }

        [Test]
        public void ContinuousMenuDiscoversNewEventEntriesAndHidesBoundFragments()
        {
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "future.entry", presentationKey = "future.talk" });
            config.events.Add(new NarrativeEventDefinition { eventId = "future.reply", presentationKey = "future.reply" });
            config.results.Add(new NarrativeResultDefinition { eventId = "future.entry", resultType = NarrativeResultType.QueueEvent, target = "event:future.reply" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "future.talk", presentationClass = "Conversation" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "unbound.reading", presentationClass = "Reading" });
            config.setups.Add(new NarrativeSetupParamsDefinition { setupKey = "fx.only" });
            var entries = PresentationDebugCatalog.ListSequences(config);
            CollectionAssert.AreEquivalent(new[] { "sequence:future.entry", "sequence:future.reply", "reading:unbound.reading" }, entries.Select(item => item.Id));
            StringAssert.StartsWith("入口候选", entries[0].Label);
            StringAssert.StartsWith("中途试播", entries[1].Label);
            // No scene bindings, hardcoded story prefixes or parallel chain definition is needed.
            config.events.Add(new NarrativeEventDefinition { eventId = "another.author.entry" });
            Assert.That(PresentationDebugCatalog.ListSequences(config).Any(item => item.Id == "sequence:another.author.entry"), Is.True);
        }

        [TestCase("conversation:missing")]
        [TestCase("setup:missing")]
        [TestCase("event:missing")]
        public void UnknownIdsFailExplicitly(string id)
        {
            StringAssert.Contains("missing", Assert.Throws<InvalidOperationException>(() =>
                PresentationDebugCatalog.Event(new NarrativeConfiguration(), id)).Message);
        }
    }
}
