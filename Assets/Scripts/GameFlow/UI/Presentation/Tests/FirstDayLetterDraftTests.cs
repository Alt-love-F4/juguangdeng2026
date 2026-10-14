using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;

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
            StringAssert.Contains("<color=#D18C00>", draft.BuildBody("choice:education:education.religion"));
            Assert.That(draft.Select(3, 2), Is.True);
            Assert.That(draft.CalculateBonuses(), Is.EqualTo(new[] { 4, 2, 2, 1 }));
            Assert.That(draft.Select(9, 0), Is.False);
        }

        [Test]
        public void CreationCommitsOnlyOnceEvenAfterRestoreAndChangedDraft()
        {
            var catalog = PlayerCreationCatalog.Load();
            var player = new PlayerRuntime(catalog);
            var draft = new FirstDayLetterDraft(catalog);
            draft.SetName("小米");
            for (int i = 0; i < 5; i++) draft.Select(i, 0);
            int commits = 0;
            var plan = player.PrepareCreation(draft);
            Assert.That(plan.StartingMoney, Is.EqualTo(250));
            Assert.That(player.CommitCreation(plan, p => commits++), Is.True);
            draft.Select(4, 3);
            Assert.That(player.CommitCreation(player.PrepareCreation(draft), p => commits++), Is.False);
            var restored = new PlayerRuntime(catalog, player.CaptureState());
            Assert.That(restored.CommitCreation(plan, p => commits++), Is.False);
            Assert.That(commits, Is.EqualTo(1));
            Assert.That(restored.PlayerName, Is.EqualTo("小米"));
        }

        [Test]
        public void FailedOwnerInitializationLeavesCreationRetryableAndDraftPersistsIds()
        {
            var catalog = PlayerCreationCatalog.Load();
            var player = new PlayerRuntime(catalog);
            var draft = new FirstDayLetterDraft(catalog);
            draft.SetName("小米");
            draft.Select("education", "education.science");
            player.SaveDraft(draft);
            var restored = new PlayerRuntime(catalog, player.CaptureState());
            Assert.That(restored.RestoreDraft().OptionIds, Does.Contain("education.science"));
            Assert.Throws<System.InvalidOperationException>(() => player.CommitCreation(player.PrepareDefaultCreation(), p => throw new System.InvalidOperationException("Owner not ready")));
            Assert.That(player.CreationSubmitted, Is.False);
            Assert.That(player.CommitCreation(player.PrepareDefaultCreation(), p => { }), Is.True);
        }

        [Test]
        public void CatalogRejectsInvalidOwnerAndAmbiguousRewardsBeforeSessionStarts()
        {
            cfg.Tables Tables(System.Action<string, Newtonsoft.Json.Linq.JArray> mutate) => new cfg.Tables(name =>
            {
                var json = Newtonsoft.Json.Linq.JArray.Parse(Resources.Load<TextAsset>("Luban/" + name).text);
                mutate(name, json);
                return json;
            });
            Assert.Throws<System.InvalidOperationException>(() => new PlayerCreationCatalog(Tables((name, rows) =>
            {
                if (name == "tbplayerattribute") rows[0]["barKey"] = "cafe.money";
            })));
            Assert.Throws<System.InvalidOperationException>(() => new PlayerCreationCatalog(Tables((name, rows) =>
            {
                if (name == "tbcreationoption")
                    foreach (var row in rows)
                        if ((string)row["groupId"] == "trait") row["startingMoney"] = 99;
            })));
            Assert.Throws<System.InvalidOperationException>(() => new PlayerCreationCatalog(Tables((name, rows) =>
            {
                if (name == "tbplayerconfig") rows[0]["actionPointMax"] = -1;
            })));
        }

        [Test]
        public void DeletedOptionRequiresReselectionAndUnavailableDiffersFromZero()
        {
            var catalog = PlayerCreationCatalog.Load();
            var draft = new FirstDayLetterDraft(catalog, "小米", new[] { "education.deleted" });
            for (int i = 0; i < 5; i++) if (i != 3) draft.Select(i, 0);
            Assert.That(draft.IsComplete, Is.False);
            draft.Select("education", "education.science");
            Assert.That(draft.IsComplete, Is.True);
            var player = new PlayerRuntime(catalog);
            Assert.That(player.TryReadMoney(out _), Is.EqualTo(PlayerReadStatus.NotReady));
            Assert.That(player.TryReadAttribute("missing", out _), Is.EqualTo(PlayerReadStatus.UnknownId));
            player.BindQueries((string key, out int value) => { value = 0; return PlayerReadStatus.Success; }, null, null);
            Assert.That(player.TryReadMoney(out int money), Is.EqualTo(PlayerReadStatus.Success));
            Assert.That(money, Is.Zero);
        }
    }
}
