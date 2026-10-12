#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NoticePanelTests
    {
        private const string Tag = "notice-regression";
        private NoticePanel panel;
        private float previousTimeScale;

        [SetUp]
        public void SetUp() => previousTimeScale = Time.timeScale;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = previousTimeScale;
            UIKit.ClosePanelsByTag(Tag);
        }

        private NoticePanel Open()
        {
            panel = UIKit.OpenPanel<NoticePanel>(UILevel.Pop, tag: Tag, cachePolicy: PanelCachePolicy.Reusable);
            return panel;
        }

        [UnityTest]
        public IEnumerator ConfirmClosesOnceAndCachedPanelReopensWhilePaused()
        {
            int finished = 0;
            int cancelled = 0;
            Open().Play(new NoticeData("新闻正文", "开门营业", "星期三"), () => finished++, () => cancelled++);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(panel.IsPlaying, Is.True);
            Assert.That(panel.IsModal, Is.True);
            Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.01f));
            panel.Confirm();
            panel.Confirm();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(finished, Is.EqualTo(1));
            Assert.That(panel.IsPlaying, Is.False);
            Assert.That(panel.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            var first = panel;
            Time.timeScale = 0;
            Open().Play(new NoticeData("再次打开", "新的通知"), () => finished++, () => cancelled++);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(panel, Is.SameAs(first));
            Assert.That(panel.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(.01f));
            panel.Cancel();
            panel.Cancel();
            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(panel.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        }

        [UnityTest]
        public IEnumerator LongCopyPaginatesAndConfirmationButtonIsRaycastable()
        {
            string body = string.Empty;
            for (int i = 0; i < 35; i++) body += "沸点咖啡馆今日开门营业，欢迎来喝一杯拿铁。\n";
            int finished = 0;
            Open().Play(new NoticeData(body, "长新闻", hasProgress: true, progressValue: .62f), () => finished++);
            yield return new WaitForSecondsRealtime(.4f);
            var content = panel.transform.Find("NewspaperLayout/DynamicContent");
            TMP_Text text = content.Find("Body").GetComponent<TMP_Text>();
            Assert.That(text.textInfo.pageCount, Is.GreaterThan(1));
            Assert.That(content.Find("ArticleSurface").GetSiblingIndex(), Is.LessThan(text.transform.GetSiblingIndex()));
            Assert.That(content.Find("DateSurface").GetSiblingIndex(), Is.LessThan(content.Find("Time").GetSiblingIndex()));
            var confirm = content.Find("Confirm").GetComponent<Button>();
            Assert.That(confirm.interactable, Is.True);
            Assert.That(confirm.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(content.Find("ProgressBar").GetComponent<Slider>().fillRect, Is.Not.Null);
            panel.Confirm();
            Assert.That(text.pageToDisplay, Is.EqualTo(2));
            Assert.That(finished, Is.Zero);
            Assert.That(panel.IsPlaying, Is.True);
            for (int i = 2; i < text.textInfo.pageCount; i++) { yield return null; panel.Confirm(); }
            yield return null;
            panel.Confirm();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(finished, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ImageStaysInPhotographAndInvisibleNoticeDoesNotBlockInput()
        {
            var image = new Texture2D(32, 16);
            try
            {
                Open().Play(new NoticeData("配图新闻", image: image));
                yield return new WaitForSecondsRealtime(.4f);
                var slot = panel.transform.Find("NewspaperLayout/DynamicContent/PhotographSlot") as RectTransform;
                var photo = slot.GetComponentInChildren<RawImage>();
                Assert.That(photo.texture, Is.SameAs(image));
                Assert.That(photo.rectTransform.rect.width, Is.LessThanOrEqualTo(slot.rect.width + .1f));
                Assert.That(photo.rectTransform.rect.height, Is.LessThanOrEqualTo(slot.rect.height + .1f));
                panel.Cancel();
                int finished = 0;
                Open().Play(new NoticeData("", visible: false), () => finished++);
                Assert.That(finished, Is.EqualTo(1));
                Assert.That(panel.IsPlaying, Is.False);
                Assert.That(panel.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            }
            finally { Object.Destroy(image); }
        }
    }
}
#endif
