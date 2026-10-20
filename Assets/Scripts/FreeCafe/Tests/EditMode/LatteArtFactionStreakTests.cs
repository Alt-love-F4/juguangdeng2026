using NUnit.Framework;

namespace Spotlight.FreeCafe
{
    public sealed class LatteArtFactionStreakTests
    {
        [Test]
        public void EmblemsMapToExistingTendencyBarsAndCourtSharesTheChurchBar()
        {
            Assert.That(LatteArtFactionStreak.BarFor("broken_chain"), Is.EqualTo("faction.revolution"));
            Assert.That(LatteArtFactionStreak.BarFor("new_rose"), Is.EqualTo("faction.rose"));
            Assert.That(LatteArtFactionStreak.BarFor("concord_ring"), Is.EqualTo("faction.church"));
            Assert.That(LatteArtFactionStreak.BarFor("sword_crown"), Is.EqualTo("faction.church"));
            Assert.That(LatteArtFactionStreak.BarFor("heart"), Is.Null);
            Assert.That(LatteArtFactionStreak.BarFor("unknown"), Is.Null);
        }

        [Test]
        public void ThirdConsecutiveEmblemCupRewardsOnceAndRestarts()
        {
            var state = new CafeSnapshot();
            Assert.That(LatteArtFactionStreak.Record(state, true, "broken_chain"), Is.Null);
            Assert.That(LatteArtFactionStreak.Record(state, true, "broken_chain"), Is.Null);
            Assert.That(LatteArtFactionStreak.Record(state, true, "broken_chain"), Is.EqualTo("faction.revolution"));
            Assert.That(state.latteFactionStreakCount, Is.Zero, "A completed streak starts over.");
            Assert.That(LatteArtFactionStreak.Record(state, true, "broken_chain"), Is.Null);
        }

        [Test]
        public void CourtAndChurchEmblemsShareOneStreak()
        {
            var state = new CafeSnapshot();
            LatteArtFactionStreak.Record(state, true, "sword_crown");
            LatteArtFactionStreak.Record(state, true, "concord_ring");
            Assert.That(LatteArtFactionStreak.Record(state, true, "sword_crown"), Is.EqualTo("faction.church"));
        }

        [Test]
        public void OnlyLatteCupsCountAndOtherPatternsBreakTheStreak()
        {
            var state = new CafeSnapshot();
            LatteArtFactionStreak.Record(state, true, "new_rose");
            LatteArtFactionStreak.Record(state, false, null); // black coffee: ignored
            LatteArtFactionStreak.Record(state, true, LatteArtFactionStreak.AutomaticPattern); // staff art: ignored
            Assert.That(state.latteFactionStreakCount, Is.EqualTo(1));
            LatteArtFactionStreak.Record(state, true, "heart"); // everyday pattern breaks
            Assert.That(state.latteFactionStreakCount, Is.Zero);
            LatteArtFactionStreak.Record(state, true, "new_rose");
            LatteArtFactionStreak.Record(state, true, "broken_chain"); // another faction restarts at 1
            Assert.That(state.latteFactionStreakKey, Is.EqualTo("faction.revolution"));
            Assert.That(state.latteFactionStreakCount, Is.EqualTo(1));
        }
    }
}
