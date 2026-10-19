using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Spotlight.Narrative.Editor.Tests
{
    /// <summary>In-memory EditMode coverage: no runtime, scenes, gameplay callbacks or generated config writes.</summary>
    public sealed class NarrativeGraphPageNavigationTests
    {
        private const string EventId = "event:neutral.reader";

        private static NarrativeConfiguration Config()
        {
            var c = new NarrativeConfiguration();
            c.events.Add(new NarrativeEventDefinition { eventId = "neutral.reader", presentationKey = "story" });
            c.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready", initialValue = false });
            // Deliberately insert out of seq order. Mutually exclusive pages must both remain browsable.
            c.presentations.conversationPages.Add(new NarrativeConversationPageDefinition {
                pageId = "page.no", presentationKey = "story", seq = 20, pageCondKeys = "not:ready",
                stepType = NarrativeConversationStepType.End });
            c.presentations.conversationPages.Add(new NarrativeConversationPageDefinition {
                pageId = "page.yes", presentationKey = "story", seq = 10, pageCondKeys = "ready",
                pageBarConds = "wisdom>=3", optionGroupKey = "options", nextPageId = "page.work", bgOverride = "scene.cafe" });
            c.presentations.conversationPages.Add(new NarrativeConversationPageDefinition {
                pageId = "page.work", presentationKey = "story", seq = 30,
                stepType = NarrativeConversationStepType.Gameplay, gameplayKey = "taste:blackcoffee:90:1" });
            c.presentations.conversationLines.Add(new NarrativeConversationLineDefinition {
                lineId = "line.yes", pageId = "page.yes", seq = 1, textKey = "body", speakerNameKey = "name", emote = "happy" });
            c.texts.Add(new NarrativeTextDefinition { key = "body", content = "<b>雨后的咖啡馆</b>\n炉火依旧温暖。" });
            c.texts.Add(new NarrativeTextDefinition { key = "name", content = "店长" });
            c.texts.Add(new NarrativeTextDefinition { key = "option", content = "邀请她留下喝茶" });
            c.choices.Add(new NarrativeChoiceDefinition {
                choiceId = "choice.stay", groupKey = "options", seq = 1, textKey = "option", resultOwnerId = "stay.results", checkId = "stay.check" });
            c.conditions.Add(ChoiceCondition("choice.ready", 1, "ready", "true"));
            c.conditions.Add(ChoiceCondition("choice.notLocked", 1, "locked", "false"));
            c.conditions.Add(ChoiceCondition("choice.other", 2, "other", "true"));
            return c;
        }

        private static NarrativeConditionDefinition ChoiceCondition(string id, int group, string target, string value)
            => new NarrativeConditionDefinition { conditionId = id, ownerType = "choice", ownerId = "choice.stay",
                groupIndex = group, condType = "BOOL_EQ", target = target, value = value };

        [Test]
        public void ReaderFieldsSeparateProseFromDiagnosticsAndKeepLegacyTextExactly()
        {
            var block = NarrativeRelationshipGraph.Build(Config()).Content(EventId)[0];
            var legacy = new StringBuilder().AppendLine("Page page.yes").AppendLine("步骤：Dialogue　seq=10")
                .AppendLine("显示条件（AND）：ready").AppendLine("显式下一页：page.work").AppendLine("背景：scene.cafe")
                .AppendLine().AppendLine("店长 [happy]：<b>雨后的咖啡馆</b>\n炉火依旧温暖。")
                .AppendLine("  ↳ line.yes").AppendLine().AppendLine("选项 1：邀请她留下喝茶")
                .AppendLine("  choice.stay; check=stay.check")
                .AppendLine("  可选条件：BOOL_EQ(ready, true)")
                .AppendLine("  可选条件：BOOL_EQ(locked, false)")
                .AppendLine("  可选条件：BOOL_EQ(other, true)").ToString();
            Assert.That(block.Id, Is.EqualTo("page.yes"));
            Assert.That(block.Caption, Is.EqualTo("10. page.yes"));
            Assert.That(block.Text, Is.EqualTo(legacy));
            Assert.That(block.BodyText, Does.Contain("店长：雨后的咖啡馆").And.Contain("炉火依旧温暖。").And.Contain("邀请她留下喝茶"));
            foreach (string technical in new[] { "<b>", "line.yes", "page.yes", "choice.stay", "scene.cafe", "BOOL_EQ", "happy" })
                Assert.That(block.BodyText, Does.Not.Contain(technical));
            Assert.That(block.Diagnostics, Does.Contain("page.yes").And.Contain("line.yes").And.Contain("wisdom>=3").And.Contain("scene.cafe"));
            Assert.That(block.Summary, Is.EqualTo("店长：雨后的咖啡馆"));
            Assert.That(block.NextPageId, Is.EqualTo("page.work"));
        }

        [Test]
        public void EndAndGameplayRemainDescriptiveAndDoNotInferNextPageOrCompletion()
        {
            var c = Config();
            var graph = NarrativeRelationshipGraph.Build(c);
            var blocks = graph.Content(EventId);
            Assert.That(blocks[1].KindLabel, Is.EqualTo("分支终点（End） · 条件页"));
            Assert.That(blocks[1].BodyText, Is.Empty);
            Assert.That(blocks[1].Summary, Is.EqualTo(blocks[1].KindLabel));
            Assert.That(blocks[1].Diagnostics, Does.Contain("本分支终点").And.Contain("不执行完成回调"));
            Assert.That(blocks[1].Text, Does.Contain("本分支终点"));
            Assert.That(blocks[1].NextPageId, Is.Empty, "The following Gameplay page is not an inferred successor.");
            Assert.That(blocks[2].KindLabel, Is.EqualTo("玩法交互（Gameplay）"));
            Assert.That(blocks[2].BodyText, Is.Empty);
            Assert.That(blocks[2].Diagnostics, Does.Contain("等待真实玩法：taste:blackcoffee:90:1"));
            Assert.That(blocks[2].NextPageId, Is.Empty);
            Assert.That(c.results, Is.Empty);
            Assert.That(c.conditionKeys.Single().initialValue, Is.False);
        }

        [Test]
        public void ExplicitNextPagePreservesEvenMissingTargetsWithoutRepairingOrInventingLinks()
        {
            var c = Config();
            c.presentations.conversationPages.Single(p => p.pageId == "page.yes").nextPageId = "missing.target";
            var graph = NarrativeRelationshipGraph.Build(c);
            int edgeCount = graph.Edges.Count;
            var blocks = graph.Content(EventId);
            Assert.That(blocks[0].NextPageId, Is.EqualTo("missing.target"));
            Assert.That(NarrativeRelationshipGraph.FindPageIndex(blocks, exactPageId: blocks[0].NextPageId), Is.EqualTo(-1));
            Assert.That(blocks.Select(b => b.Id), Is.EqualTo(new[] { "page.yes", "page.no", "page.work" }));
            Assert.That(graph.Edges.Count, Is.EqualTo(edgeCount));
            Assert.That(graph.Edges.Any(e => e.Kind == StoryLinkKind.Continuation), Is.False);
        }

        [Test]
        public void MutuallyExclusivePagesAreNotFilteredEvaluatedOrReorderedInSource()
        {
            var c = Config();
            var sourceOrder = c.presentations.conversationPages.Select(p => p.pageId).ToArray();
            var sourceConditions = c.presentations.conversationPages.Select(p => p.pageCondKeys).ToArray();
            var graph = NarrativeRelationshipGraph.Build(c);
            var blocks = graph.Content(EventId);
            Assert.That(blocks[0].Diagnostics, Does.Contain("显示条件（AND）：ready"));
            Assert.That(blocks[1].Diagnostics, Does.Contain("显示条件（AND）：not:ready"));
            Assert.That(NarrativeRelationshipGraph.FindPageIndex(blocks, "page.yes"), Is.EqualTo(0));
            Assert.That(NarrativeRelationshipGraph.FindPageIndex(blocks, "page.no"), Is.EqualTo(1));
            Assert.That(NarrativeRelationshipGraph.FindMatchingPages(blocks, " "), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(graph.Content(EventId), Is.SameAs(blocks));
            Assert.That(c.presentations.conversationPages.Select(p => p.pageId), Is.EqualTo(sourceOrder));
            Assert.That(c.presentations.conversationPages.Select(p => p.pageCondKeys), Is.EqualTo(sourceConditions));
            Assert.That(c.conditionKeys.Single().initialValue, Is.False);
        }

        [Test]
        public void ChoiceDiagnosticsKeepConditionGroupEvidenceWithoutSelectingAChoice()
        {
            var c = Config();
            var block = NarrativeRelationshipGraph.Build(c).Content(EventId)[0];
            Assert.That(block.Text, Does.Contain("邀请她留下喝茶"));
            Assert.That(block.Diagnostics, Does.Contain("choice.stay").And.Contain("stay.check").And.Contain("stay.results")
                .And.Contain("组内 AND，组间 OR").And.Contain("组 1:").And.Contain("组 2:")
                .And.Contain("BOOL_EQ(ready, true)").And.Contain("BOOL_EQ(locked, false)").And.Contain("BOOL_EQ(other, true)"));
            Assert.That(c.choices.Count, Is.EqualTo(1));
            Assert.That(c.conditions.Count, Is.EqualTo(3));
            Assert.That(c.results, Is.Empty);
        }

        [TestCase(NarrativeConversationStepType.Dialogue, "对话（Dialogue）")]
        [TestCase(NarrativeConversationStepType.PassiveCheck, "被动检定（PassiveCheck）")]
        [TestCase(NarrativeConversationStepType.LetterReveal, "信件揭示（LetterReveal）")]
        [TestCase(NarrativeConversationStepType.LetterRead, "信件阅读（LetterRead）")]
        [TestCase(NarrativeConversationStepType.End, "分支终点（End）")]
        [TestCase(NarrativeConversationStepType.Gameplay, "玩法交互（Gameplay）")]
        public void EveryConfiguredStepHasAReadableKindLabel(NarrativeConversationStepType kind, string label)
        {
            var c = Config();
            c.presentations.conversationPages.Single(p => p.pageId == "page.yes").stepType = kind;
            var block = NarrativeRelationshipGraph.Build(c).Content(EventId)[0];
            Assert.That(block.KindLabel, Is.EqualTo(label + " · 条件页 · 选项"));
            Assert.That(block.NextPageId, Is.EqualTo("page.work"), "Preserve the field even on End; this is not traversal.");
        }

        [Test]
        public void PageMarkersUseOnlyOwnConditionsAndExistingChoicesNotGuessedReachability()
        {
            var c = Config();
            var page = c.presentations.conversationPages.Single(p => p.pageId == "page.yes");
            page.pageCondKeys = " ";
            page.pageBarConds = "wisdom>=3";
            page.optionGroupKey = "missing.options";
            var block = NarrativeRelationshipGraph.Build(c).Content(EventId)[0];
            Assert.That(block.KindLabel, Is.EqualTo("对话（Dialogue） · 条件页"));
            page.pageBarConds = " ";
            page.optionGroupKey = "options";
            block = NarrativeRelationshipGraph.Build(c).Content(EventId)[0];
            Assert.That(block.KindLabel, Is.EqualTo("对话（Dialogue） · 选项"), "Choice conditions do not become page conditions.");
            page.optionGroupKey = "missing.options";
            block = NarrativeRelationshipGraph.Build(c).Content(EventId)[0];
            Assert.That(block.KindLabel, Is.EqualTo("对话（Dialogue）"));
        }

        [Test]
        public void ExactIdLookupIsOrdinalAndNeverParsesNumericPageNumbers()
        {
            IReadOnlyList<StoryContentBlock> blocks = new[] {
                new StoryContentBlock { Id = "Page.A" }, null,
                new StoryContentBlock { Id = "2" }, new StoryContentBlock { Id = "Page.A" } };
            Assert.That(NarrativeRelationshipGraph.FindPageIndex(blocks, "Page.A"), Is.EqualTo(0));
            Assert.That(NarrativeRelationshipGraph.FindPageIndex(blocks, "page.a"), Is.EqualTo(-1));
            Assert.That(NarrativeRelationshipGraph.FindPageIndex(blocks, " Page.A "), Is.EqualTo(-1));
            Assert.That(NarrativeRelationshipGraph.FindPageIndex(blocks, "2"), Is.EqualTo(2));
            Assert.That(NarrativeRelationshipGraph.FindPageIndex(blocks, "1"), Is.EqualTo(-1));
            Assert.That(NarrativeRelationshipGraph.FindPageIndex(blocks, null), Is.EqualTo(-1));
            Assert.That(NarrativeRelationshipGraph.FindPageIndex(blocks, " "), Is.EqualTo(-1));
            Assert.That(NarrativeRelationshipGraph.FindPageIndex(null, "Page.A"), Is.EqualTo(-1));
        }

        [Test]
        public void SearchSupportsChineseBodyCaptionLegacyChoiceAndExactIdInIndexOrder()
        {
            IReadOnlyList<StoryContentBlock> blocks = new[] {
                new StoryContentBlock { Id = "exact.only", BodyText = "咖啡馆与炉火", Text = "咖啡馆" },
                new StoryContentBlock { Caption = "昨日的咖啡馆" },
                new StoryContentBlock { Text = "选项：邀请她留下喝茶" },
                new StoryContentBlock { BodyText = "Cafe Gameplay" }, null };
            Assert.That(NarrativeRelationshipGraph.FindMatchingPages(blocks, "咖啡馆"), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(NarrativeRelationshipGraph.FindMatchingPages(blocks, "炉火"), Is.EqualTo(new[] { 0 }));
            Assert.That(NarrativeRelationshipGraph.FindMatchingPages(blocks, "留下喝茶"), Is.EqualTo(new[] { 2 }));
            Assert.That(NarrativeRelationshipGraph.FindMatchingPages(blocks, "exact.only"), Is.EqualTo(new[] { 0 }));
            Assert.That(NarrativeRelationshipGraph.FindMatchingPages(blocks, " cafe "), Is.EqualTo(new[] { 3 }));
            Assert.That(NarrativeRelationshipGraph.FindMatchingPages(blocks, "不存在"), Is.Empty);
            Assert.That(blocks[0].BodyText, Is.EqualTo("咖啡馆与炉火"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t\r\n")]
        public void BlankSearchReturnsAllPagesInOrderAndNullInputIsSafe(string query)
        {
            var blocks = NarrativeRelationshipGraph.Build(Config()).Content(EventId);
            Assert.That(NarrativeRelationshipGraph.FindMatchingPages(blocks, query), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(NarrativeRelationshipGraph.FindMatchingPages(null, query), Is.Empty);
            Assert.That(NarrativeRelationshipGraph.FindMatchingPages(Array.Empty<StoryContentBlock>(), query), Is.Empty);
        }

        [Test]
        public void ReadingFieldsAndNodeSearchIncludeNestedAndStandalonePageIds()
        {
            var c = Config();
            c.events.Add(new NarrativeEventDefinition { eventId = "reading.event", presentationKey = "letter" });
            c.presentations.conversationPages.Single(p => p.pageId == "page.yes").readingKey = "letter";
            c.presentations.readingPages.Add(new NarrativeReadingPageDefinition {
                pageId = "letter.page", presentationKey = "letter", seq = 1, titleKey = "letter.title",
                pageCondKeys = "not:ready", pageBarConds = "wisdom>=2", imageKey = "letter.image" });
            c.presentations.readingLines.Add(new NarrativeReadingLineDefinition {
                lineId = "letter.line", pageId = "letter.page", textKey = "letter.body", seq = 1 });
            c.texts.Add(new NarrativeTextDefinition { key = "letter.title", content = "给未来的信" });
            c.texts.Add(new NarrativeTextDefinition { key = "letter.body", content = "愿你记住这个雨夜。" });
            var graph = NarrativeRelationshipGraph.Build(c);
            var nested = graph.Content(EventId)[0];
            var reading = graph.Content("event:reading.event").Single();
            Assert.That(nested.BodyText, Does.Contain("给未来的信").And.Contain("愿你记住这个雨夜。"));
            Assert.That(reading.KindLabel, Is.EqualTo("阅读 · 条件页"));
            Assert.That(reading.Summary, Is.EqualTo("给未来的信"));
            Assert.That(reading.BodyText, Does.Not.Contain("letter.page").And.Not.Contain("letter.line"));
            Assert.That(reading.Diagnostics, Does.Contain("letter.page").And.Contain("letter.line")
                .And.Contain("not:ready").And.Contain("wisdom>=2").And.Contain("letter.image"));
            Assert.That(reading.NextPageId, Is.Empty);
            Assert.That(graph.Find(EventId).SearchText, Does.Contain("page.yes").And.Contain("page.no")
                .And.Contain("page.work").And.Contain("letter.page").And.Contain("雨后的咖啡馆"));
            Assert.That(graph.Find("event:reading.event").SearchText, Does.Contain("letter.page"));
        }

        [Test]
        public void OpeningNoticeFallbackAndEndingAlsoProvideReadingFields()
        {
            var c = new NarrativeConfiguration();
            c.openingPresentations.Add(new NarrativeOpeningPresentationDefinition { presentationId = "intro" });
            c.openingLines.Add(new NarrativeOpeningLineDefinition { presentationId = "intro", lineId = "intro.line", speaker = "left", textKey = "欢迎回来。" });
            c.events.Add(new NarrativeEventDefinition { eventId = "notice", presentationKey = "notice" });
            c.presentations.notices.Add(new NarrativeNoticeDefinition { presentationKey = "notice", titleKey = "今晚的消息", textKey = "店里即将打烊。", barConds = "ready" });
            c.events.Add(new NarrativeEventDefinition { eventId = "fallback", contentKeys = new List<string> { "一段独白。" }, setupKey = "setup.only" });
            c.endings.Add(new NarrativeEndingDefinition { endingId = "ending", enabled = true, messageText = "新的旅程开始了。" });
            var graph = NarrativeRelationshipGraph.Build(c);
            var opening = graph.Content("opening:intro").Single();
            Assert.That(opening.BodyText, Is.EqualTo("欢迎回来。"));
            Assert.That(opening.Diagnostics, Does.Contain("intro.line").And.Contain("speaker=left"));
            var notice = graph.Content("event:notice").Single();
            Assert.That(notice.BodyText, Does.Contain("今晚的消息").And.Contain("店里即将打烊。"));
            Assert.That(notice.Diagnostics, Does.Contain("barConds=ready"));
            var fallback = graph.Content("event:fallback").Single();
            Assert.That(fallback.BodyText, Is.EqualTo("一段独白。"));
            Assert.That(fallback.Diagnostics, Is.EqualTo("SetupParams: setup.only"));
            Assert.That(fallback.Text, Is.EqualTo("一段独白。\n\nSetupParams: setup.only"));
            Assert.That(graph.Content("ending:ending").Single().BodyText, Is.EqualTo("新的旅程开始了。"));
            Assert.That(graph.Content("event:missing"), Is.Empty);
        }

        [Test]
        public void SummaryIsShortAndPlainBodyPreservesLiteralComparisons()
        {
            var c = new NarrativeConfiguration();
            c.events.Add(new NarrativeEventDefinition { eventId = "long", contentKeys = new List<string> { "long.body" } });
            c.texts.Add(new NarrativeTextDefinition { key = "long.body", content = "<b>" + new string('文', 60) + "</b><br>1 < 2，3 > 2，<未知标记>" });
            var block = NarrativeRelationshipGraph.Build(c).Content("event:long").Single();
            Assert.That(block.Summary, Is.EqualTo(new string('文', 48) + "…"));
            Assert.That(block.BodyText, Does.Contain("\n1 < 2，3 > 2，<未知标记>"));
            Assert.That(block.BodyText, Does.Not.Contain("<b>").And.Not.Contain("<br>"));
        }
    }
}
