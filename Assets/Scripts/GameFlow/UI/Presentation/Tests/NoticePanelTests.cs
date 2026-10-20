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
        public IEnumerator LongCopyScrollsAndReopeningResetsToTop()
        {
            string body = string.Empty;
            for (int i = 0; i < 35; i++) body += "沸点咖啡馆今日开门营业，欢迎来喝一杯拿铁。\n";
            int finished = 0;
            Open().Play(new NoticeData(body, "长新闻", hasProgress: true, progressValue: .62f), () => finished++);
            yield return new WaitForSecondsRealtime(.4f);
            var content = panel.transform.Find("NewspaperLayout/DynamicContent");
            var scroll = content.Find("ArticleSurface").GetComponent<ScrollRect>();
            TMP_Text text = scroll.content.GetComponent<TMP_Text>();
            Assert.That(text.text, Is.EqualTo(body));
            Assert.That(content.Find("Title").GetComponent<TMP_Text>().text, Is.EqualTo("长新闻"));
            Assert.That(text.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.01f));
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            foreach (var image in panel.transform.Find("NewspaperLayout/NewspaperArtwork").GetComponentsInChildren<Image>(true))
                if (image.name.StartsWith("都来喝拿铁")) Assert.That(image.gameObject.activeSelf, Is.False);
            Assert.That(content.Find("DateSurface").GetSiblingIndex(), Is.LessThan(content.Find("Time").GetSiblingIndex()));
            var confirm = content.Find("Confirm").GetComponent<Button>();
            Assert.That(confirm.interactable, Is.True);
            Assert.That(confirm.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(content.Find("ProgressBar").GetComponent<Slider>().fillRect, Is.Not.Null);
            scroll.verticalNormalizedPosition = 0f;
            yield return null;
            Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(0f));
            panel.Cancel();
            Open().Play(new NoticeData(body, "再次阅读"), () => finished++);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.01f));
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
