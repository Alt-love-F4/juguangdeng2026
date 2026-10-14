using System;
using System.Collections.Generic;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class PlayerSaveBridgeTests
    {
        private static GameSaveData Legacy(string json = "{\"playerName\":\"Ada\",\"origin\":\"贵族\",\"attributes\":[1,2,3,4],\"specializations\":[\"letter.science\"]}")
        {
            var data = new GameSaveData();
            data.Participants.Add(new GameSaveParticipantData { Id = PlayerSaveBridge.LegacyParticipantId, Json = json });
            return data;
        }

        [Test]
        public void ExistingNcsZeroWinsAndOnlyMissingAttributesAreImported()
        {
            var data = Legacy();
            string original = JsonUtility.ToJson(data);
            var snapshot = new NarrativeSnapshot();
            snapshot.bars.Add(new NarrativeBarValue { key = "player.language", value = 0 });
            snapshot.active = new NarrativePendingValue { eventId = "queued" };
            Assert.That(PlayerSaveBridge.TryRead(data, snapshot, out var state, out var error), Is.True, error);
            Assert.That(snapshot.bars.Find(b => b.key == "player.language").value, Is.Zero);
            Assert.That(snapshot.bars.Find(b => b.key == "player.perception").value, Is.EqualTo(4));
            Assert.That(snapshot.bars.Count, Is.EqualTo(4));
            Assert.That(snapshot.active.eventId, Is.EqualTo("queued"));
            Assert.That(state.creationSubmitted, Is.True);
            Assert.That(state.originId, Is.EqualTo("noble"));
            Assert.That(JsonUtility.ToJson(data), Is.EqualTo(original));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NewConditionPoolIncludingEmptyNeverResurrectsLegacySpecialization(bool explicitFalse)
        {
            var snapshot = new NarrativeSnapshot { conditionValues = new List<NarrativeBoolValue>() };
            if (explicitFalse) snapshot.conditionValues.Add(new NarrativeBoolValue { key = "specialization.player.letter.science", value = false });
            Assert.That(PlayerSaveBridge.TryRead(Legacy(), snapshot, out _, out var error), Is.True, error);
            Assert.That(snapshot.flags, Is.Empty);
            if (explicitFalse) Assert.That(snapshot.conditionValues[0].value, Is.False);
        }

        [Test]
        public void LegacyPoolImportsSpecializationOnceWithoutInventingNewPool()
        {
            var snapshot = new NarrativeSnapshot();
            Assert.That(PlayerSaveBridge.TryRead(Legacy(), snapshot, out _, out var error), Is.True, error);
            Assert.That(PlayerSaveBridge.TryRead(Legacy(), snapshot, out _, out error), Is.True, error);
            Assert.That(snapshot.flags.Count, Is.EqualTo(1));
            Assert.That(snapshot.flags[0].key, Is.EqualTo("specialization.player.letter.science"));
            Assert.That(snapshot.conditionValues, Is.Null);
        }

        [Test]
        public void NewPlayerWinsEvenWhenLegacyPayloadIsMalformed()
        {
            var data = Legacy("broken old payload");
            data.Participants.Add(new GameSaveParticipantData { Id = PlayerSaveBridge.ParticipantId,
                Json = JsonUtility.ToJson(new PlayerState { playerName = "New", creationSubmitted = true }) });
            var snapshot = new NarrativeSnapshot();
            Assert.That(PlayerSaveBridge.TryRead(data, snapshot, out var state, out var error), Is.True, error);
            Assert.That(state.playerName, Is.EqualTo("New"));
            Assert.That(snapshot.bars, Is.Empty);
            Assert.That(snapshot.flags, Is.Empty);
        }

        [TestCase(1, 9, false)]
        [TestCase(1, 10, true)]
        [TestCase(1, 11, true)]
        [TestCase(1, 3, true)]
        [TestCase(2, 9, true)]
        public void OnlyExplicitFirstDayLetterRestoresUnsubmittedDraft(int day, int phase, bool submitted)
        {
            var data = Legacy("{\"playerName\":\"Ada\",\"draftOptionIds\":[\"origin.noble\"]}");
            data.Participants.Add(new GameSaveParticipantData { Id = "game.day-flow.v1", Json = "{\"day\":" + day + ",\"state\":" + phase + "}" });
            Assert.That(PlayerSaveBridge.TryRead(data, new NarrativeSnapshot(), out var state, out var error), Is.True, error);
            Assert.That(state.creationSubmitted, Is.EqualTo(submitted));
            if (!submitted)
            {
                Assert.That(state.draftName, Is.EqualTo("Ada"));
                Assert.That(state.draftOptionIds, Is.EqualTo(new[] { "origin.noble" }));
            }
        }

        [Test]
        public void FailedMigrationDoesNotPartiallyAppendAttributes()
        {
            var snapshot = new NarrativeSnapshot();
            string original = JsonUtility.ToJson(snapshot);
            Assert.That(PlayerSaveBridge.TryRead(Legacy("{\"attributes\":[1,2,999,4]}"), snapshot, out var state, out var error), Is.False);
            Assert.That(state, Is.Null);
            Assert.That(error, Does.Contain("0..20"));
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(original));
        }

        [Test]
        public void PreNcsSaveMigrationCreatesSnapshotAndPreservesOtherParticipants()
        {
            var data = Legacy();
            data.Participants.Add(new GameSaveParticipantData { Id = "cafe", Json = "{\"money\":1100}" });
            string original = JsonUtility.ToJson(data);
            Assert.That(PlayerSaveBridge.TryPrepareRestore(data, out _, out var snapshot, out var error), Is.True, error);
            Assert.That(snapshot.bars.Count, Is.EqualTo(4));
            Assert.That(JsonUtility.ToJson(data), Is.EqualTo(original));
        }

        [Test]
        public void V2DraftRoundTripKeepsDeletedOptionForExplicitReselection()
        {
            var source = new PlayerState { playerName = "Ada", draftName = "Draft", creationSubmitted = false,
                draftOptionIds = new[] { "origin.deleted" }, legacyBackgroundText = "old background" };
            var data = new GameSaveData();
            data.Participants.Add(new GameSaveParticipantData { Id = PlayerSaveBridge.ParticipantId, Json = JsonUtility.ToJson(source) });
            Assert.That(PlayerSaveBridge.TryPrepareRestore(data, out var result, out _, out var error), Is.True, error);
            Assert.That(result.creationSubmitted, Is.False);
            Assert.That(result.draftName, Is.EqualTo("Draft"));
            Assert.That(result.draftOptionIds, Is.EqualTo(new[] { "origin.deleted" }));
            Assert.That(result.legacyBackgroundText, Is.EqualTo("old background"));
        }

        [Test]
        public void DuplicateNewPlayerIsRejectedWithoutFallingBack()
        {
            var data = Legacy();
            var entry = new GameSaveParticipantData { Id = PlayerSaveBridge.ParticipantId, Json = JsonUtility.ToJson(new PlayerState()) };
            data.Participants.Add(entry); data.Participants.Add(entry);
            Assert.That(PlayerSaveBridge.TryPrepareRestore(data, out _, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("Duplicate"));
        }

        [TestCase("{\"version\":3,\"playerId\":\"player\",\"avatar\":0}")]
        [TestCase("{\"version\":2,\"playerId\":\"player\",\"avatar\":0,\"creationSubmitted\":\"true\"}")]
        public void InvalidNewStateCannotFallBackToOldProfile(string json)
        {
            var data = Legacy();
            data.Participants.Add(new GameSaveParticipantData { Id = PlayerSaveBridge.ParticipantId, Json = json });
            Assert.That(PlayerSaveBridge.TryPrepareRestore(data, out var state, out _, out var error), Is.False);
            Assert.That(state, Is.Null); Assert.That(error, Is.Not.Empty);
        }

        [TestCase(null)]
        [TestCase("null")]
        [TestCase("\"\"")]
        [TestCase("\"   \"")]
        [TestCase("123")]
        public void V2RequiresNonemptyOrigin(string originJson)
        {
            string json = "{\"version\":2,\"playerId\":\"player\",\"avatar\":0,\"creationSubmitted\":true,\"hasSelectedAvatar\":false"
                + (originJson == null ? "" : ",\"originId\":" + originJson) + "}";
            var data = Legacy();
            data.Participants.Add(new GameSaveParticipantData { Id = PlayerSaveBridge.ParticipantId, Json = json });
            Assert.That(PlayerSaveBridge.TryPrepareRestore(data, out var state, out _, out var error), Is.False);
            Assert.That(state, Is.Null);
            Assert.That(error, Does.Contain("originId"));
        }

        [TestCase(null)]
        [TestCase("null")]
        [TestCase("\"false\"")]
        [TestCase("0")]
        public void V2RequiresExplicitBooleanAvatarSelection(string selectionJson)
        {
            string json = "{\"version\":2,\"playerId\":\"player\",\"avatar\":0,\"creationSubmitted\":true,\"originId\":\"legacy/unknown\""
                + (selectionJson == null ? "" : ",\"hasSelectedAvatar\":" + selectionJson) + "}";
            var data = Legacy();
            data.Participants.Add(new GameSaveParticipantData { Id = PlayerSaveBridge.ParticipantId, Json = json });
            Assert.That(PlayerSaveBridge.TryPrepareRestore(data, out var state, out _, out var error), Is.False);
            Assert.That(state, Is.Null);
            Assert.That(error, Does.Contain("hasSelectedAvatar"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void V2AcceptsExplicitAvatarChoiceAndUnknownLegacyOrigin(bool selected)
        {
            var data = new GameSaveData();
            data.Participants.Add(new GameSaveParticipantData { Id = PlayerSaveBridge.ParticipantId,
                Json = JsonUtility.ToJson(new PlayerState { originId = "legacy/unknown", hasSelectedAvatar = selected }) });
            Assert.That(PlayerSaveBridge.TryPrepareRestore(data, out var state, out _, out var error), Is.True, error);
            Assert.That(state.originId, Is.EqualTo("legacy/unknown"));
            Assert.That(state.hasSelectedAvatar, Is.EqualTo(selected));
        }

        [Test]
        public void CaptureDropsLegacyPendingButPreservesUnloadedOwnerAndPropagatesFailures()
        {
            var oldPending = GameSaveParticipantRegistry.PendingRestore;
            var captures = new Dictionary<string, Func<string>>(GameSaveParticipantRegistry.Capturers);
            try
            {
                GameSaveParticipantRegistry.Capturers.Clear();
                GameSaveParticipantRegistry.PendingRestore = Legacy().Participants;
                GameSaveParticipantRegistry.PendingRestore.Add(new GameSaveParticipantData { Id = "cafe", Json = "saved" });
                GameSaveParticipantRegistry.Capturers[PlayerSaveBridge.LegacyParticipantId] = () => "obsolete";
                var result = GameSaveParticipantRegistry.CaptureAll();
                Assert.That(result.Count, Is.EqualTo(1)); Assert.That(result[0].Id, Is.EqualTo("cafe"));
                GameSaveParticipantRegistry.Capturers[PlayerSaveBridge.ParticipantId] = () => throw new InvalidOperationException("settling");
                Assert.Throws<InvalidOperationException>(() => GameSaveParticipantRegistry.CaptureAll());
            }
            finally
            {
                GameSaveParticipantRegistry.PendingRestore = oldPending;
                GameSaveParticipantRegistry.Capturers.Clear();
                foreach (var pair in captures) GameSaveParticipantRegistry.Capturers.Add(pair.Key, pair.Value);
            }
        }
    }
}
