using NUnit.Framework;
using Spotlight.Audio;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeResultAudioTests
    {
        [TestCase(0f, 0, "cafe.result_bad")]
        [TestCase(39.99f, 0, "cafe.result_bad")]
        [TestCase(40f, 1, "cafe.result_good")]
        [TestCase(59.99f, 1, "cafe.result_good")]
        [TestCase(60f, 2, "cafe.result_great")]
        [TestCase(79.99f, 2, "cafe.result_great")]
        [TestCase(80f, 3, "cafe.result_perfect")]
        [TestCase(100f, 3, "cafe.result_perfect")]
        public void DeliveryKeyUsesReceiptGradeBoundariesRegardlessOfCustomerFeedback(
            float score, int grade, string expectedKey)
        {
            var quality = new ProductionQuality(score, score, score, score, score);
            var feedbacks = new[]
            {
                CafeFeedbackKind.DeliverySatisfied,
                CafeFeedbackKind.DeliveryRemake,
                CafeFeedbackKind.DeliveryFailed
            };
            foreach (CafeFeedbackKind feedback in feedbacks)
            {
                var receipt = new CafeDeliveryReceipt("coffee", "customer", quality, 10, 1, feedback, "delivery");
                Assert.That(receipt.quality.total, Is.EqualTo(score).Within(.001f));
                Assert.That(receipt.Grade, Is.EqualTo(grade), feedback.ToString());
                Assert.That(FreeCafeAudioPresenter.DeliveryKey(receipt), Is.EqualTo(expectedKey), feedback.ToString());
            }
        }

        [TestCase(AudioKeys.CoffeeResultBad, "cafe.result_bad")]
        [TestCase(AudioKeys.CoffeeResultGood, "cafe.result_good")]
        [TestCase(AudioKeys.CoffeeResultGreat, "cafe.result_great")]
        [TestCase(AudioKeys.CoffeeResultPerfect, "cafe.result_perfect")]
        public void CoffeeResultKeysKeepTheirPublicIdentifiers(string key, string expectedKey)
            => Assert.That(key, Is.EqualTo(expectedKey));

        [TestCase(AudioKeys.CoffeeResultBad, "咖啡结果-音效bad")]
        [TestCase(AudioKeys.CoffeeResultGood, "咖啡结果-音效good")]
        [TestCase(AudioKeys.CoffeeResultGreat, "咖啡结果-音效great")]
        [TestCase(AudioKeys.CoffeeResultPerfect, "咖啡结果-音效perfect")]
        public void ResourceCatalogContainsFinalNonLoopingUiResultClips(string key, string clipName)
        {
            AudioCue cue = LoadCue(key);
            Assert.That(cue.clip, Is.Not.Null, key);
            Assert.That(cue.clip.name, Is.EqualTo(clipName), key);
            Assert.That(cue.loop, Is.False, key);
            Assert.That(cue.provisional, Is.False, key);
            Assert.That(cue.category, Is.EqualTo(AudioCategory.UI), key);
        }

        [TestCase(AudioKeys.CityMusic, "map")]
        [TestCase(AudioKeys.MenuMusic, "Dorian-C#-一号主菜单(1)")]
        [TestCase("music.track.win！", "win！")]
        public void ResourceCatalogMusicRetainsExpectedClipNames(string key, string clipName)
        {
            AudioCue cue = LoadCue(key);
            Assert.That(cue.clip, Is.Not.Null, key);
            Assert.That(cue.clip.name, Is.EqualTo(clipName), key);
        }

        private static AudioCue LoadCue(string key)
        {
            AudioCatalog catalog = Resources.Load<AudioCatalog>(AudioCatalog.ResourceName);
            Assert.That(catalog, Is.Not.Null, "The actual Resources audio catalog must be available.");
            AudioCue cue = catalog.Find(key);
            Assert.That(cue, Is.Not.Null, key);
            return cue;
        }
    }
}
