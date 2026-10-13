using System.Text.RegularExpressions;
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
            StringAssert.Contains("<color=#D18C00>", draft.BuildBody("slot:3"));
            Assert.That(draft.Select(3, 2), Is.True);
            Assert.That(draft.CalculateBonuses(), Is.EqualTo(new[] { 4, 2, 2, 1 }));
            Assert.That(draft.Select(9, 0), Is.False);
        }

        [Test]
        public void BodyContainsOnlyClickableFilledWordsWithoutOptionDescriptions()
        {
            var draft = new FirstDayLetterDraft();
            draft.SetName("小米");
            for (int i = 0; i < 5; i++) draft.Select(i, 0);
            string body = draft.BuildBody();
            StringAssert.Contains("<link=\"slot:0\"><color=#B3000F><b><u>贫寒</u></b></color></link>", body);
            StringAssert.DoesNotContain("富贵", body);
            StringAssert.DoesNotContain("选项", body);
            StringAssert.DoesNotContain("choice:", body);
            StringAssert.DoesNotContain("______", body);
            foreach (string[] group in FirstDayLetterDraft.Effects)
                foreach (string effect in group) StringAssert.DoesNotContain(effect, body);
            draft.Select(0, 2);
            StringAssert.Contains("富贵", draft.BuildBody());
            StringAssert.DoesNotContain("贫寒", draft.BuildBody());
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ExpandedGroupInsertsClickableLabelsAndEffectsImmediatelyAfterItsSlot(int group)
        {
            var draft = new FirstDayLetterDraft();
            draft.SetName("小米");
            string collapsed = draft.BuildBody();
            string body = draft.BuildBody(expandedGroup: group);
            AssertExpandedGroup(body, group, "______");
            Assert.That(draft.Selection(group), Is.EqualTo(-1), "Rendering choices must not fill the blank.");
            Assert.That(draft.BuildBody(), Is.EqualTo(collapsed), "Expanded state belongs to the caller, not the draft.");
            StringAssert.DoesNotContain("choice:", collapsed);
            for (int i = 0; i < FirstDayLetterDraft.Labels.Length; i++) draft.Select(i, 0);
            collapsed = draft.BuildBody();
            body = draft.BuildBody(expandedGroup: group);
            AssertExpandedGroup(body, group, FirstDayLetterDraft.Labels[group][0]);
            Assert.That(draft.Selection(group), Is.Zero);
            Assert.That(draft.BuildBody(), Is.EqualTo(collapsed));
            int option = FirstDayLetterDraft.Labels[group].Length - 1;
            string choiceId = $"choice:{group}:{option}";
            string hovered = draft.BuildBody(choiceId, group);
            StringAssert.Contains($"<link=\"{choiceId}\"><color=#{FirstDayLetterDraft.HoverInk}>", hovered);
            Assert.That(draft.Select(group, option), Is.True);
            collapsed = draft.BuildBody();
            Assert.That(LinkText(collapsed, $"slot:{group}"), Is.EqualTo(FirstDayLetterDraft.Labels[group][option]));
            StringAssert.DoesNotContain("choice:", collapsed);
            foreach (string[] effects in FirstDayLetterDraft.Effects)
                foreach (string effect in effects) StringAssert.DoesNotContain(effect, collapsed);
            for (int i = 0; i < FirstDayLetterDraft.Labels[group].Length; i++)
                if (i != option) StringAssert.DoesNotContain(FirstDayLetterDraft.Labels[group][i], collapsed);
        }

        private static void AssertExpandedGroup(string body, int group, string slotWord)
        {
            Assert.That(LinkText(body, $"slot:{group}"), Is.EqualTo(slotWord));
            Match slot = Regex.Match(body, $"<link=\"slot:{group}\">.*?</link>", RegexOptions.Singleline);
            MatchCollection choices = Regex.Matches(body, "<link=\"choice:[0-9]+:[0-9]+\">.*?</link>", RegexOptions.Singleline);
            Assert.That(choices.Count, Is.EqualTo(FirstDayLetterDraft.Labels[group].Length));
            int previousEnd = slot.Index + slot.Length;
            for (int option = 0; option < choices.Count; option++)
            {
                Match choice = choices[option];
                Assert.That(LinkText(choice.Value, $"choice:{group}:{option}"),
                    Is.EqualTo(FirstDayLetterDraft.Labels[group][option] + "【" + FirstDayLetterDraft.Effects[group][option] + "】"));
                Assert.That(choice.Index, Is.GreaterThanOrEqualTo(previousEnd));
                string between = Regex.Replace(body.Substring(previousEnd, choice.Index - previousEnd), "<[^>]+>", "");
                between = between.Replace("选项", "");
                Assert.That(Regex.Replace(between, @"[\s（）()；;：:]", ""), Is.Empty,
                    "Choices must follow the slot without later letter prose between them.");
                previousEnd = choice.Index + choice.Length;
            }
            if (group + 1 < FirstDayLetterDraft.Labels.Length)
                Assert.That(body.IndexOf($"<link=\"slot:{group + 1}\">", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(previousEnd));
        }

        private static string LinkText(string body, string id)
        {
            Match link = Regex.Match(body, $"<link=\"{Regex.Escape(id)}\">(?<text>.*?)</link>", RegexOptions.Singleline);
            Assert.That(link.Success, Is.True, $"Missing link {id}");
            return Regex.Replace(link.Groups["text"].Value, "<[^>]+>", "");
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
