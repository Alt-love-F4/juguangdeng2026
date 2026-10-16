using System;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class PresentationPauseTests
    {
        [Test]
        public void OwnersStackAndDisposeIndependentlyWithoutChangingTimeScale()
        {
            int baseline = PresentationPauseRegistry.OwnerCount;
            float scale = Time.timeScale;
            IDisposable first = PresentationPauseRegistry.Acquire("conversation");
            IDisposable second = PresentationPauseRegistry.Acquire("reading");
            try
            {
                Assert.That(PresentationPauseRegistry.IsPaused, Is.True);
                Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(baseline + 2));
                first.Dispose();
                first.Dispose();
                Assert.That(PresentationPauseRegistry.IsPaused, Is.True);
                Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(baseline + 1));
                Assert.That(Time.timeScale, Is.EqualTo(scale));
            }
            finally { first.Dispose(); second.Dispose(); }
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(baseline));
        }

        [Test]
        public void ExceptionalOwnerScopeReleasesOnlyItsOwnToken()
        {
            int baseline = PresentationPauseRegistry.OwnerCount;
            using (PresentationPauseRegistry.Acquire("parent"))
            {
                Assert.Throws<InvalidOperationException>(() =>
                {
                    using (PresentationPauseRegistry.Acquire("failing child"))
                        throw new InvalidOperationException();
                });
                Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(baseline + 1));
            }
            Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(baseline));
        }

        [TestCase("entry", "MorningBeforeMap")]
        [TestCase("ENTRY", "AfterBusiness")]
        [TestCase("blocksSave", "true")]
        [TestCase("blockedBy", "construction")]
        public void SchedulingMetadataAloneDoesNotRequirePresentationButVisualPayloadStillDoes(string property, string value)
        {
            var definition = new NarrativeEventDefinition { eventId = "scheduled-result" };
            definition.eventProperties.Add(new NarrativeEventProperty { property = property, value = value });
            Assert.That(NarrativePresenter.RequiresPresentation(definition, null), Is.False);
            definition.eventProperties.Add(new NarrativeEventProperty { property = "bg", value = "cafe" });
            Assert.That(NarrativePresenter.RequiresPresentation(definition, null), Is.True);
        }

        [Test]
        public void ResultOnlyEventDoesNotRequirePresentationButContentAndAtomicSetupsDo()
        {
            var definition = new NarrativeEventDefinition { eventId = "reserve", titleText = "debug title" };
            Assert.That(NarrativePresenter.RequiresPresentation(definition, null), Is.False);
            definition.contentKeys.Add(" ");
            Assert.That(NarrativePresenter.RequiresPresentation(definition, null), Is.False);
            definition.contentKeys.Add("text:dialogue");
            Assert.That(NarrativePresenter.RequiresPresentation(definition, null), Is.True);
            definition.contentKeys.Clear();
            Assert.That(NarrativePresenter.RequiresPresentation(definition,
                new NarrativeSetupParamsDefinition { tplName = "Tpl_Camera" }), Is.True);
            definition.presentationKey = "configured_reading";
            Assert.That(NarrativePresenter.RequiresPresentation(definition, null), Is.True);
        }
    }
}
