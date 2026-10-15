using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Spotlight.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class LetterEventPlaybackTests
    {
        private const string Tag = "generic-letter-tests";
        private object adapterOwner;
        private Func<bool> previousInputBlocker;
        private int pauseOwners;

        [SetUp]
        public void Setup()
        {
            previousInputBlocker = DialoguePanel.ExternalInputBlocked;
            pauseOwners = PresentationPauseRegistry.OwnerCount;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (adapterOwner != null) LetterEventPlayback.ReleaseStage(adapterOwner);
            adapterOwner = null;
            UIKit.ClosePanelsByTag(Tag);
            DialoguePanel.ExternalInputBlocked = previousInputBlocker;
            DialogueHistoryService.ClearFormal();
            yield return null;
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners));
        }

        [Test]
        public void SnapshotCopiesResolvedTextAndStableSourceWithoutKeepingMutableRows()
        {
            var data = Data(new object());
            data.lines.Add(new NarrativeLetterPresentationLine { eventId = "event", presentationKey = "reading",
                pageId = "page", lineId = "line", seq = 24, textKey = "key", text = "01 02 03" });
            var snapshot = new LetterReadingSnapshot(data);
            data.lines[0].text = "mutated"; data.lines.Clear(); data.title = "mutated";
            Assert.That(snapshot.Lines.Count, Is.EqualTo(1));
            Assert.That(snapshot.Body, Is.EqualTo("01 02 03"));
            Assert.That(snapshot.Title, Is.EqualTo("致新任店长："));
            var source = snapshot.Lines[0].Source;
            Assert.That(source.SourceType, Is.EqualTo("Reading"));
            Assert.That(source.EventId, Is.EqualTo("event"));
            Assert.That(source.PresentationKey, Is.EqualTo("reading"));
            Assert.That(source.PageId, Is.EqualTo("page"));
            Assert.That(source.SourceLineId, Is.EqualTo("line"));
            Assert.That(source.Sequence, Is.EqualTo(24));
            Assert.That(snapshot.AllowCancel, Is.False);
            Assert.That(new LetterReadingSnapshot(data, true).AllowCancel, Is.True);
        }

        [Test]
        public void ActualConfiguredCipherPreservesSevenRowsAndAll231TwoDigitTokens()
        {
            NarrativeConfiguration config = LubanNarrativeLoader.Load();
            var page = config.presentations.readingPages.Single(p => p.presentationKey == "oct07.s2.letter");
            var data = Data(new object());
            foreach (var line in config.presentations.readingLines.Where(l => l.pageId == page.pageId).OrderBy(l => l.seq))
                data.lines.Add(new NarrativeLetterPresentationLine { lineId = line.lineId, pageId = line.pageId, seq = line.seq,
                    textKey = line.textKey, text = config.texts.Single(t => t.key == line.textKey).content });
            var snapshot = new LetterReadingSnapshot(data);
            Assert.That(snapshot.Lines.Count, Is.EqualTo(7));
            Assert.That(snapshot.Body.Split('\n').Length, Is.EqualTo(7));
            Assert.That(Regex.Matches(snapshot.Body, @"\b\d{2}\b").Count, Is.EqualTo(231));
            string layout = LetterReadingSnapshot.LayoutText(snapshot.Body);
            Assert.That(Regex.Matches(layout, @"<nobr>\d{2}</nobr>").Count, Is.EqualTo(231));
            Assert.That(layout.Replace("<nobr>", "").Replace("</nobr>", ""), Is.EqualTo(snapshot.Body));
            StringAssert.DoesNotContain("<link", LetterReadingSnapshot.LayoutText("<link=\"name\">01</link>"));
        }

        [Test]
        public void ExistingPrefabExportsOriginalSpriteBindingsWithoutReplacementAssets()
        {
            var prefab = Resources.Load<GameObject>("Art/UIPrefab/FirstDayLetterPanel");
            var panel = prefab.GetComponent<FirstDayLetterPanel>();
            var defaults = panel.GetLetterVisualDefaults();
            Assert.That(defaults.EnvelopeClosed, Is.SameAs(Field<Sprite>(panel, "envelopeClosedSprite")));
            Assert.That(defaults.EnvelopeOpen, Is.SameAs(Field<Sprite>(panel, "envelopeOpenSprite")));
            Assert.That(defaults.Paper, Is.SameAs(prefab.transform.Find("LetterPaper/Page").GetComponent<Image>().sprite));
            Assert.That(defaults.EnvelopeClosed != null && defaults.EnvelopeOpen != null && defaults.Paper != null);
        }

        [UnityTest]
        public IEnumerator RevealCompletesOnceKeepsCenteredEnvelopeAndCannotInterceptDialogue()
        {
            var panel = Open();
            var owner = new object();
            int done = 0;
            var data = Data(owner); data.revealOnly = true;
            panel.PlayLetter(new LetterReadingSnapshot(data), () => done++);
            Assert.That(done, Is.EqualTo(1));
            Assert.That(panel.OwnsLetter(owner), Is.True);
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(panel.IsModal, Is.False);
            Assert.That(panel.GetComponent<GraphicRaycaster>().enabled, Is.False);
            Assert.That(Field<CanvasGroup>(panel, "rootGroup").blocksRaycasts, Is.False);
            Assert.That(Field<RectTransform>(panel, "envelope").anchoredPosition, Is.EqualTo(Vector2.zero));
            foreach (string path in new[] { "Blackout", "NameEditor", "EnvelopeStage/Chapter", "EnvelopeStage/Place",
                "EnvelopeStage/ClickHint", "EnvelopeStage/Envelope/Address" })
                Assert.That(panel.transform.Find(path).gameObject.activeInHierarchy, Is.False, path);
            panel.OpenEnvelope(); panel.SkipLetter(); panel.ConfirmSignature();
            yield return null;
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Envelope));
            Assert.That(done, Is.EqualTo(1));
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners));
        }

        [UnityTest]
        public IEnumerator SameOwnerRevealBecomesModalReadAndCannotInvokeOpeningSubmissionOrSkip()
        {
            var panel = Open();
            var data = Data(new object()); data.revealOnly = true;
            panel.PlayLetter(new LetterReadingSnapshot(data), null);
            RectTransform envelope = Field<RectTransform>(panel, "envelope");
            Vector2 position = envelope.anchoredPosition, size = envelope.sizeDelta;
            data.revealOnly = false; data.lines.Add(Line("01 02 03"));
            int submissions = 0, completions = 0;
            panel.SubmitDefault = () => { submissions++; return true; };
            panel.SubmitDraft = _ => { submissions++; return true; };
            panel.PlayLetter(new LetterReadingSnapshot(data), () => completions++);
            Assert.That(Field<RectTransform>(panel, "envelope"), Is.SameAs(envelope));
            Assert.That(envelope.anchoredPosition, Is.EqualTo(position));
            Assert.That(envelope.sizeDelta, Is.EqualTo(size));
            Assert.That(panel.IsModal, Is.True);
            Assert.That(panel.GetComponent<GraphicRaycaster>().enabled, Is.True);
            Assert.That(DialoguePanel.IsExternalInputBlocked, Is.True);
            Assert.That(panel.Draft, Is.Null);
            panel.SkipLetter(); panel.ConfirmSignature(); panel.SetPlayerName("不得写入"); panel.SelectLetterOption(0, 0);
            yield return null; panel.OpenEnvelope();
            yield return Wait(() => panel.Stage == FirstDayLetterPanel.LetterStage.Reading);
            Assert.That(Field<TMP_Text>(panel, "letterBody").maxVisibleCharacters, Is.EqualTo(int.MaxValue));
            Assert.That(submissions, Is.Zero); Assert.That(completions, Is.Zero);
            Assert.That(Field<Button>(panel, "signatureButton").gameObject.activeSelf, Is.False);
            Assert.That(Field<Button>(panel, "skipButton").gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator LongLetterNeedsBottomBeforeReadDoneAndCompletesOnlyOnAFreshFrame()
        {
            var panel = Open();
            var data = Data(new object());
            data.lines.Add(Line(string.Join("\n", Enumerable.Repeat("01 02 03 04 05 06 07 08 09 10", 100))));
            int done = 0, cancelled = 0;
            panel.PlayLetter(new LetterReadingSnapshot(data), () => done++, () => cancelled++);
            yield return null; panel.OpenEnvelope();
            yield return Wait(() => panel.Stage == FirstDayLetterPanel.LetterStage.Reading);
            yield return null;
            Assert.That(panel.CanFinishLetter, Is.False);
            Assert.That(Field<Button>(panel, "genericDone").gameObject.activeSelf, Is.False);
            panel.FinishReadingLetter(); Assert.That(done, Is.Zero);
            var scroll = Field<ScrollRect>(panel, "letterScroll");
            scroll.verticalNormalizedPosition = 0; scroll.onValueChanged.Invoke(Vector2.zero);
            Assert.That(panel.CanFinishLetter, Is.True);
            int frame = Time.frameCount; int completionFrame = -1;
            // Replace only the completion action for the frame boundary assertion.
            typeof(FirstDayLetterPanel).GetField("genericCompleted", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(panel, (Action)(() => { done++; completionFrame = Time.frameCount; }));
            panel.FinishReadingLetter(); panel.FinishReadingLetter();
            Assert.That(done, Is.Zero);
            yield return Wait(() => done == 1);
            Assert.That(completionFrame, Is.GreaterThan(frame));
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(cancelled, Is.Zero);
            Assert.That(DialoguePanel.ExternalInputBlocked, Is.SameAs(previousInputBlocker));
        }

        [UnityTest]
        public IEnumerator StopDuringOpeningCancelsOnceAndKillsDelayedCompletionAndLeases()
        {
            var panel = Open();
            var data = Data(new object()); data.lines.Add(Line("01 02 03"));
            int done = 0, cancelled = 0;
            panel.PlayLetter(new LetterReadingSnapshot(data), () => done++, () => cancelled++);
            yield return null; panel.OpenEnvelope(); panel.Stop(); panel.Stop();
            Vector2 stopped = Field<RectTransform>(panel, "paper").anchoredPosition;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(done, Is.Zero); Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Stopped));
            Assert.That(Field<RectTransform>(panel, "paper").anchoredPosition, Is.EqualTo(stopped));
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(pauseOwners));
        }

        [UnityTest]
        public IEnumerator FormalCannotCancelButPreviewCanAndSuspensionBlocksOpening()
        {
            var panel = Open();
            var data = Data(new object()); data.lines.Add(Line("01 02"));
            int cancelled = 0;
            panel.PlayLetter(new LetterReadingSnapshot(data), null, () => cancelled++);
            panel.CancelLetter(); Assert.That(cancelled, Is.Zero);
            panel.SetSuspended(true);
            yield return null; panel.OpenEnvelope();
            Assert.That(panel.Stage, Is.EqualTo(FirstDayLetterPanel.LetterStage.Envelope));
            Assert.That(Field<CanvasGroup>(panel, "rootGroup").alpha, Is.Zero);
            panel.SetSuspended(false);
            panel.Stop();
            Assert.That(cancelled, Is.EqualTo(1));
            panel.PlayLetter(new LetterReadingSnapshot(data, true), null, () => cancelled++);
            panel.CancelLetter(); Assert.That(cancelled, Is.EqualTo(1));
            yield return null; yield return null;
            Assert.That(cancelled, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator FormalHistoryOnlyCapturesShownPaperAndPreviewNeverChangesIt()
        {
            var panel = Open();
            var data = Data(new object());
            data.lines.Add(Line("VISIBLE 01 02"));
            data.lines.Add(Line(string.Join("\n", Enumerable.Repeat("03 04 05", 80))));
            data.lines.Add(Line("UNSEEN-LAST 25 26"));
            var history = DialogueHistoryService.Formal;
            int count = history.Entries.Count;
            panel.PlayLetter(new LetterReadingSnapshot(data), null);
            Assert.That(history.Entries.Count, Is.EqualTo(count));
            yield return null; panel.OpenEnvelope();
            yield return Wait(() => panel.Stage == FirstDayLetterPanel.LetterStage.Reading);
            Assert.That(history.Entries.Count, Is.GreaterThan(count));
            Assert.That(history.Entries.Skip(count).Any(e => e.Text.Contains("UNSEEN-LAST")), Is.False);
            panel.Stop();
            count = history.Entries.Count;
            panel.PlayLetter(new LetterReadingSnapshot(data, true), null);
            yield return null; panel.OpenEnvelope();
            yield return Wait(() => panel.Stage == FirstDayLetterPanel.LetterStage.Reading);
            var scroll = Field<ScrollRect>(panel, "letterScroll");
            scroll.verticalNormalizedPosition = 0; scroll.onValueChanged.Invoke(Vector2.zero);
            Assert.That(history.Entries.Count, Is.EqualTo(count));
        }

        [UnityTest]
        public IEnumerator GenericReuseRestoresOriginalCreationNameChoicesSignatureAndSpriteLayout()
        {
            var panel = Open();
            var paper = Field<RectTransform>(panel, "paper");
            Vector2 originalSize = paper.sizeDelta, originalAnchor = paper.anchorMin;
            float originalFont = Field<TMP_Text>(panel, "letterBody").fontSize;
            var data = Data(new object()); data.revealOnly = true;
            panel.PlayLetter(new LetterReadingSnapshot(data), null); panel.Stop();
            panel.Play((Action)null);
            Assert.That(panel.IsGenericLetter, Is.False);
            Assert.That(panel.Draft, Is.Not.Null);
            Assert.That(paper.sizeDelta, Is.EqualTo(originalSize));
            Assert.That(paper.anchorMin, Is.EqualTo(originalAnchor));
            Assert.That(Field<TMP_Text>(panel, "letterBody").fontSize, Is.EqualTo(originalFont));
            Assert.That(panel.transform.Find("Blackout").gameObject.activeSelf, Is.True);
            Assert.That(panel.transform.Find("EnvelopeStage/Chapter").gameObject.activeSelf, Is.True);
            Assert.That(Field<CanvasGroup>(panel, "envelopeGroup").blocksRaycasts, Is.True);
            Assert.That(Field<CanvasGroup>(panel, "paperGroup").blocksRaycasts, Is.True);
            panel.OpenEnvelope();
            yield return Wait(() => panel.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature);
            panel.SetPlayerName("回归店长");
            for (int i = 0; i < 5; i++) Assert.That(panel.SelectLetterOption(i, 0), Is.True);
            int submitted = 0, done = 0;
            panel.SubmitDraft = draft => { Assert.That(draft.PlayerName, Is.EqualTo("回归店长")); submitted++; return true; };
            typeof(FirstDayLetterPanel).GetField("finished", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(panel, (Action)(() => done++));
            panel.ConfirmSignature();
            yield return Wait(() => done == 1);
            Assert.That(submitted, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AcknowledgedRevealStillNotifiesOwnerWhenItsRetainedPanelIsStopped()
        {
            adapterOwner = new object();
            var data = Data(adapterOwner); data.revealOnly = true;
            int completed = 0, cancelled = 0; data.cancelled = () => cancelled++;
            Assert.That(LetterEventPlayback.Play(default, data, () => completed++), Is.True);
            var panel = UIKit.GetPanel<FirstDayLetterPanel>();
            Assert.That(completed, Is.EqualTo(1));
            panel.Stop(); panel.Stop();
            yield return null;
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(cancelled, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AdapterRejectsOtherOwnerAndReleaseIsScopedIdempotentAndNeverCompletesRead()
        {
            adapterOwner = new object();
            var data = Data(adapterOwner); data.revealOnly = true;
            int done = 0, cancelled = 0;
            Assert.That(LetterEventPlayback.Play(default, data, () => done++), Is.True);
            var panel = UIKit.GetPanel<FirstDayLetterPanel>();
            Assert.That(data.releaseStage, Is.Not.Null); Assert.That(data.setSuspended, Is.Not.Null);
            LetterEventPlayback.ReleaseStage(new object());
            Assert.That(panel.OwnsLetter(adapterOwner), Is.True);
            var other = Data(new object()); other.revealOnly = true;
            Assert.That(LetterEventPlayback.Play(default, other, null), Is.False);
            data.revealOnly = false; data.lines.Add(Line("01 02")); data.cancelled = () => cancelled++;
            Assert.That(LetterEventPlayback.PlayPreview(default, data, () => done++), Is.True);
            Assert.That(UIKit.GetPanel<FirstDayLetterPanel>(), Is.SameAs(panel));
            Assert.That(panel.LetterSnapshot.IsPreview, Is.True);
            data.releaseStage(); data.releaseStage();
            yield return null;
            Assert.That(done, Is.EqualTo(1)); Assert.That(cancelled, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PaperRefitsOnResizeAndWrapsLongEnglishWithoutSplittingNumericTokens()
        {
            var panel = Open();
            var data = Data(new object());
            data.lines.Add(Line(new string('W', 140) + "\n" + string.Join(" ", Enumerable.Repeat("01 02 03 04 05 06 07 08 09 10", 60))));
            panel.PlayLetter(new LetterReadingSnapshot(data, true), null);
            yield return null; panel.OpenEnvelope();
            yield return Wait(() => panel.Stage == FirstDayLetterPanel.LetterStage.Reading);
            var root = (RectTransform)panel.transform;
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(640, 1080);
            yield return null; yield return null;
            var paper = Field<RectTransform>(panel, "paper");
            Assert.That(paper.rect.width, Is.LessThanOrEqualTo(root.rect.width * .8f + 1));
            Assert.That(paper.rect.height, Is.LessThanOrEqualTo(root.rect.height * .8f + 1));
            var body = Field<TMP_Text>(panel, "letterBody"); body.ForceMeshUpdate();
            var info = body.textInfo;
            Assert.That(info.lineCount, Is.GreaterThan(3));
            int tokens = 0;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (c.isVisible) Assert.That(c.xAdvance, Is.LessThanOrEqualTo(body.rectTransform.rect.xMax + 1));
                if (i + 1 < info.characterCount && char.IsDigit(c.character) && char.IsDigit(info.characterInfo[i + 1].character))
                {
                    Assert.That(c.lineNumber, Is.EqualTo(info.characterInfo[i + 1].lineNumber)); tokens++; i++;
                }
            }
            Assert.That(tokens, Is.EqualTo(600));
            Assert.That(panel.CanFinishLetter, Is.False);
        }

        [UnityTest]
        public IEnumerator EnterAfterScrollCompletesOnFreshFrameAndDoesNotSubmitTwice()
        {
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            try
            {
                var panel = Open();
                var data = Data(new object()); data.lines.Add(Line(string.Join("\n", Enumerable.Repeat("01 02 03 04 05 06 07 08 09 10", 60))));
                int done = 0, completionFrame = -1;
                panel.PlayLetter(new LetterReadingSnapshot(data, true), () => { done++; completionFrame = Time.frameCount; });
                yield return null; panel.OpenEnvelope();
                yield return Wait(() => panel.Stage == FirstDayLetterPanel.LetterStage.Reading);
                yield return null;
                var scroll = Field<ScrollRect>(panel, "letterScroll");
                scroll.OnScroll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { scrollDelta = new Vector2(0, -1000) });
                yield return null;
                Assert.That(panel.CanFinishLetter, Is.True);
                int inputFrame = Time.frameCount;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Enter));
                yield return Wait(() => done != 0);
                Assert.That(done, Is.EqualTo(1)); Assert.That(completionFrame, Is.GreaterThan(inputFrame));
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null; yield return null;
                Assert.That(done, Is.EqualTo(1));
            }
            finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard); }
        }

        private static FirstDayLetterPanel Open()
        {
            var panel = UIKit.OpenPanel<FirstDayLetterPanel>(UILevel.Pop, tag: Tag, cachePolicy: PanelCachePolicy.Transient);
            typeof(FirstDayLetterPanel).GetField("openSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(panel, .01f);
            typeof(FirstDayLetterPanel).GetField("closeSeconds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(panel, .01f);
            typeof(FirstDayLetterPanel).GetField("characterInterval", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(panel, .005f);
            return panel;
        }
        private static T Field<T>(FirstDayLetterPanel panel, string name)
            => (T)typeof(FirstDayLetterPanel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
        private static NarrativeLetterPresentationData Data(object owner) => new NarrativeLetterPresentationData
        { continuityOwner = owner, eventId = "test.letter", presentationKey = "test.presentation", pageId = "test.step",
            readingKey = "test.reading", readingPageId = "test.page", titleKey = "test.title", title = "致新任店长：" };
        private static NarrativeLetterPresentationLine Line(string text) => new NarrativeLetterPresentationLine
        { lineId = "test.line", pageId = "test.page", seq = 1, text = text };
        private static IEnumerator Wait(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (!condition()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
        }
    }
}
