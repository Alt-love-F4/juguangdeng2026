using NUnit.Framework;

namespace Spotlight.GameFlow.Tests
{
    public sealed class FirstDayLetterDraftTests
    {
        [Test]
        public void ChoiceBonusesMatchReferenceAndNameRejectsMarkup()
        {
            var draft = new FirstDayLetterDraft();
            Assert.That(draft.IsComplete, Is.False);
            draft.SetName("  <阿黛尔>\n ");
            Assert.That(draft.PlayerName, Is.EqualTo("阿黛尔"));
            for (int i = 0; i < 5; i++) Assert.That(draft.Select(i, 0), Is.True);
            Assert.That(draft.IsComplete, Is.True);
            Assert.That(draft.Origin, Is.EqualTo("流浪儿"));
            Assert.That(draft.CalculateBonuses(), Is.EqualTo(new[] { 5, 2, 0, 2 }));
            Assert.That(draft.Specialization, Is.EqualTo("letter.religion"));
            StringAssert.Contains("<color=#D18C00>", draft.BuildBody("choice:3:0"));
            Assert.That(draft.Select(3, 2), Is.True);
            Assert.That(draft.CalculateBonuses(), Is.EqualTo(new[] { 4, 2, 2, 1 }));
            Assert.That(draft.Select(9, 0), Is.False);
        }

        [Test]
        public void ApplyingLetterIsIdempotentAndRoundTripsThroughExistingSave()
        {
            string previous = PlayerDetailsProfile.CaptureJson();
            try
            {
                PlayerDetailsProfile.ResetForNewSession();
                var draft = new FirstDayLetterDraft();
                draft.SetName("小米");
                for (int i = 0; i < 5; i++) draft.Select(i, 0);
                PlayerDetailsProfile.Current.ApplyLetter(draft);
                PlayerDetailsProfile.Current.ApplyLetter(draft);
                Assert.That(PlayerDetailsProfile.Current.Attributes, Is.EqualTo(new[] { 5, 2, 0, 2 }));
                draft.Select(4, 3);
                PlayerDetailsProfile.Current.ApplyLetter(draft);
                Assert.That(PlayerDetailsProfile.Current.Attributes, Is.EqualTo(new[] { 3, 2, 0, 4 }));
                string save = PlayerDetailsProfile.CaptureJson();
                PlayerDetailsProfile.ResetForNewSession();
                PlayerDetailsProfile.RestoreJson(save);
                Assert.That(PlayerDetailsProfile.Current.PlayerName, Is.EqualTo("小米"));
                Assert.That(PlayerDetailsProfile.Current.Origin, Is.EqualTo("流浪儿"));
                Assert.That(PlayerDetailsProfile.Current.Specializations, Is.EquivalentTo(new[] { "letter.religion" }));
                Assert.That(PlayerDetailsProfile.Current.Attributes, Is.EqualTo(new[] { 3, 2, 0, 4 }));
                PlayerDetailsProfile.Current.ApplyLetter(draft);
                Assert.That(PlayerDetailsProfile.Current.Attributes, Is.EqualTo(new[] { 3, 2, 0, 4 }));
            }
            finally { PlayerDetailsProfile.RestoreJson(previous); }
        }
    }
}
