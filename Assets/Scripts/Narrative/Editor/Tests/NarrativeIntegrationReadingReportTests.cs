using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Editor.Tests
{
    public sealed class NarrativeIntegrationReadingReportTests
    {
        private static NarrativeConfiguration Config(string declaredClass = "Reading")
        {
            var c = new NarrativeConfiguration();
            c.events.Add(new NarrativeEventDefinition { eventId = "direct", eventKind = NarrativeEventKind.World, presentationClass = declaredClass, presentationKey = "reading" });
            c.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "reading", presentationClass = "Reading" });
            c.presentations.readingPages.Add(new NarrativeReadingPageDefinition { pageId = "r1", presentationKey = "reading", seq = 1, titleKey = "title" });
            c.presentations.readingLines.Add(new NarrativeReadingLineDefinition { lineId = "l1", pageId = "r1", seq = 1, mode = "narration", textKey = "body1" });
            c.presentations.readingLines.Add(new NarrativeReadingLineDefinition { lineId = "l2", pageId = "r1", seq = 2, mode = "narration", textKey = "body2" });
            foreach (string key in new[] { "title", "body1", "body2" }) c.texts.Add(new NarrativeTextDefinition { key = key, content = key });
            return c;
        }
        [TestCase("Reading")] [TestCase(null)] [TestCase("Conversation")]
        public void DirectReadingCountsPagesLinesUsingIndexClass(string declaredClass)
        {
            var c = Config(declaredClass);
            c.presentations.readingPages.Add(new NarrativeReadingPageDefinition { pageId = "r2", presentationKey = "reading", seq = 2 });
            c.presentations.readingLines.Add(new NarrativeReadingLineDefinition { lineId = "l3", pageId = "r2", seq = 1, mode = "narration", textKey = "body1" });
            var report = NarrativeIntegrationReport.Build(c).Find("direct");
            Assert.That(report.ReadingPageCount, Is.EqualTo(2)); Assert.That(report.ReadingLineCount, Is.EqualTo(3));
            Assert.That(report.PageCount, Is.Zero); Assert.That(report.LetterStepCount, Is.Zero); Assert.That(report.Errors, Is.Empty);
        }
        [Test] public void DirectReadingChecksAllTitleAndLineTextReferences()
        {
            var c = Config(); c.texts.RemoveAll(text => text.key == "title" || text.key == "body2");
            var report = NarrativeIntegrationReport.Build(c).Find("direct");
            Assert.That(report.Errors.Count, Is.EqualTo(2));
            Assert.That(report.Errors.Any(e => e.Contains("Reading page title r1") && e.Contains("title")));
            Assert.That(report.Errors.Any(e => e.Contains("Reading line l2") && e.Contains("body2")));
            Assert.That(report.ReadingLineCount, Is.EqualTo(2));
        }
        [Test] public void NestedLetterReadingIsCountedOnceAndAlsoChecksText()
        {
            var c = Config(); c.events[0].presentationClass = "Conversation"; c.events[0].presentationKey = "conversation";
            c.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = "conversation", presentationClass = "Conversation" });
            c.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "reveal", presentationKey = "conversation", seq = 1, stepType = NarrativeConversationStepType.LetterReveal, readingKey = "reading" });
            c.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "read", presentationKey = "conversation", seq = 2, stepType = NarrativeConversationStepType.LetterRead, readingKey = "reading" });
            c.texts.RemoveAll(text => text.key == "body2");
            var report = NarrativeIntegrationReport.Build(c).Find("direct");
            Assert.That(report.LetterStepCount, Is.EqualTo(2)); Assert.That(report.ReadingPageCount, Is.EqualTo(1)); Assert.That(report.ReadingLineCount, Is.EqualTo(2));
            Assert.That(report.Errors.Count(e => e.Contains("Reading line l2") && e.Contains("body2")), Is.EqualTo(1));
        }
        [Test] public void TrulyMissingReadingPagesAndConstructionBlockersRemainErrors()
        {
            var c = Config(); c.presentations.readingPages.Clear();
            c.events[0].eventProperties.Add(new NarrativeEventProperty { property = "blockedBy", value = "source.choice.unresolved" });
            var report = NarrativeIntegrationReport.Build(c).Find("direct");
            Assert.That(report.ReadingPageCount, Is.Zero); Assert.That(report.ReadingLineCount, Is.Zero);
            Assert.That(report.Errors.Any(e => e.Contains("Reading presentation has no ReadingPage rows")));
            Assert.That(report.Errors.Contains("Construction blocker: source.choice.unresolved")); Assert.That(report.Status, Is.EqualTo("blocked"));
        }
        [Test] public void EmptyReadingPagesBlankTextAndUnknownSpeakersAreNotSuppressed()
        {
            var c = Config(); c.presentations.readingPages.Add(new NarrativeReadingPageDefinition { pageId = "empty", presentationKey = "reading", seq = 2 });
            c.presentations.readingLines[0].textKey = ""; c.presentations.readingLines[1].speakerKey = "unknown";
            var report = NarrativeIntegrationReport.Build(c).Find("direct");
            Assert.That(report.Errors.Any(e => e.Contains("ReadingPage has no ReadingLine rows: empty")));
            Assert.That(report.Errors.Any(e => e.Contains("Reading line l1 has no text key")));
            Assert.That(report.Errors.Any(e => e.Contains("Reading line l2 references unknown speaker: unknown")));
        }
        [Test] public void UnrelatedReadingPagesAndLinesAreNotCountedOrValidatedForThisRoot()
        {
            var c = Config(); c.presentations.readingPages.Add(new NarrativeReadingPageDefinition { pageId = "other", presentationKey = "other.reading", seq = 1, titleKey = "missing" });
            c.presentations.readingLines.Add(new NarrativeReadingLineDefinition { lineId = "other.line", pageId = "other", seq = 1, textKey = "also.missing" });
            var report = NarrativeIntegrationReport.Build(c).Find("direct");
            Assert.That(report.ReadingPageCount, Is.EqualTo(1)); Assert.That(report.ReadingLineCount, Is.EqualTo(2)); Assert.That(report.Errors, Is.Empty);
        }
    }
}
