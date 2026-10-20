using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeChoiceRequirementTextTests
    {
        private NarrativeConfiguration config;

        [SetUp] public void SetUp()
        {
            config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "player.language", nameText = "bar.language" });
        }

        private void Add(string type, string target, string value, int group = 0)
        {
            config.conditions.Add(new NarrativeConditionDefinition {
                ownerType = "choice", ownerId = "choice", condType = type,
                target = target, value = value, groupIndex = group });
        }

        private string Hint() => NarrativeChoiceRequirementText.Format(config, "choice", key => key == "bar.language" ? "口才" : key);

        [TestCase("blocked.neutral.reporter_advert_price")]
        [TestCase("construction.church.1023.s1.bribery_ready")]
        [TestCase("church.detention.bribe_quote_ready")]
        [TestCase("church.detention.bribe_affordable")]
        public void InternalFlagsNeverAppearInPlayerHint(string flag)
        {
            Add("BOOL_EQ", flag, "true");
            Assert.That(Hint(), Is.EqualTo("条件未满足"));
        }

        [Test] public void MixedStoryAndNumericConditionsUseSafeFallback()
        {
            Add("BAR_GTE", "player.language", "7");
            Add("BOOL_EQ", "church.detention.bribe_affordable", "true");
            Assert.That(Hint(), Is.EqualTo("条件未满足"));
        }

        [Test] public void NumericBoundsAndAlternativeGroupsHaveReadableSemantics()
        {
            Add("BAR_GTE", "bar:player.language", "7");
            Add("BAR_LTE", "player.language", "10");
            Add("REL_GTE", "rel:player.language", "20", 1);
            Assert.That(Hint(), Is.EqualTo("需要 口才 达到 7，且需要 口才 不高于 10；或 需要 口才 达到 20"));
        }

        [Test] public void UnknownTargetsAndMissingTranslationsNeverFallBackToIdentifiers()
        {
            Add("BAR_GTE", "missing.bar", "7");
            Assert.That(Hint(), Is.EqualTo("条件未满足"));
            config.conditions[0].target = "player.language";
            Assert.That(NarrativeChoiceRequirementText.Format(config, "choice", key => key), Is.EqualTo("条件未满足"));
        }

        [TestCase("EVENT_HAPPENED", "event.secret", "")]
        [TestCase("ITEM_GTE", "item.secret", "1")]
        [TestCase("FUTURE_TYPE", "internal.target", "true")]
        public void UnsupportedConditionsRemainPrivate(string type, string target, string value)
        {
            Add(type, target, value);
            Assert.That(Hint(), Is.EqualTo("条件未满足"));
        }

        [Test] public void NoRequirementsUsesSafeFallback()
        {
            Assert.That(Hint(), Is.EqualTo("条件未满足"));
        }
    }
}
