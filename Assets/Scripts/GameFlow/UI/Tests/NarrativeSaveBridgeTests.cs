using System;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativeSaveBridgeTests
    {
        [Test]
        public void RoundTripRetainsActivePendingChoicesAndEffects()
        {
            var source = new NarrativeSnapshot();
            source.bars.Add(new NarrativeBarValue { key = "npc_chen.trust", value = 4 });
            source.active = new NarrativePendingValue { eventId = "active", depth = 2 };
            source.active.selectedChoices.Add("choice.a");
            source.pending.Add(new NarrativePendingValue { eventId = "pending" });
            source.pendingEffects.Add(new NarrativeResultDefinition { resultId = "effect", resultType = NarrativeResultType.PlaySetup, target = "setup:letter" });
            var game = new GameSaveData();
            Assert.That(NarrativeSaveBridge.TryCapture(game, () => source, out string error), Is.True, error);
            GameSaveData roundTrip = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(game));
            Assert.That(NarrativeSaveBridge.TryRead(roundTrip, out NarrativeSnapshot result, out error), Is.True, error);
            Assert.That(result.active.eventId, Is.EqualTo("active"));
            Assert.That(result.active.selectedChoices, Is.EqualTo(new[] { "choice.a" }));
            Assert.That(result.pending[0].eventId, Is.EqualTo("pending"));
            Assert.That(result.pendingEffects[0].resultId, Is.EqualTo("effect"));
            Assert.That(result.bars[0].value, Is.EqualTo(4));
        }

        [Test]
        public void LegacySaveWithoutParticipantIsExplicitlySupported()
        {
            Assert.That(NarrativeSaveBridge.TryRead(new GameSaveData(), out NarrativeSnapshot snapshot, out string error), Is.True, error);
            Assert.That(snapshot, Is.Null);
        }

        [TestCase(0)]
        [TestCase(3)]
        public void UnsupportedEnvelopeVersionBlocksLoad(int version)
        {
            GameSaveData data = WithPayload(JsonUtility.ToJson(new NarrativeSaveBridge.Envelope { Version = version, Snapshot = new NarrativeSnapshot() }));
            Assert.That(NarrativeSaveBridge.TryRead(data, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("version"));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("not-json")]
        [TestCase("{\"Version\":1}")]
        public void InvalidPayloadBlocksLoad(string json)
        {
            Assert.That(NarrativeSaveBridge.TryRead(WithPayload(json), out _, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void V1SignalsMigrationWhileV2RetainsExplicitFalseAndEnding()
        {
            Assert.That(NarrativeSaveBridge.TryRead(WithPayload("{\"Version\":1,\"Snapshot\":{\"flags\":[{\"key\":\"met\",\"value\":true}]}}"), out var legacy, out var error), Is.True, error);
            Assert.That(legacy.conditionValues, Is.Null);
            Assert.That(legacy.flags[0].value, Is.True);
            var source = new NarrativeSnapshot { selectedEndingId = "ending.test", conditionValues = new System.Collections.Generic.List<NarrativeBoolValue> { new NarrativeBoolValue { key = "met", value = false } } };
            var data = new GameSaveData();
            Assert.That(NarrativeSaveBridge.TryCapture(data, () => source, out error), Is.True, error);
            Assert.That(NarrativeSaveBridge.TryRead(data, out var restored, out error), Is.True, error);
            Assert.That(restored.conditionValues[0].value, Is.False);
            Assert.That(restored.selectedEndingId, Is.EqualTo("ending.test"));
        }

        [Test]
        public void DuplicateParticipantsBlockLoad()
        {
            GameSaveData data = WithPayload("{}");
            data.Participants.Add(new GameSaveParticipantData { Id = NarrativeSaveBridge.ParticipantId, Json = "{}" });
            Assert.That(NarrativeSaveBridge.TryRead(data, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("duplicate"));
        }

        [Test]
        public void CaptureFailureDoesNotMutateParticipants()
        {
            var data = new GameSaveData();
            data.Participants.Add(new GameSaveParticipantData { Id = "other", Json = "preserved" });
            Assert.That(NarrativeSaveBridge.TryCapture(data, () => throw new InvalidOperationException("atomic settlement"), out string error), Is.False);
            Assert.That(data.Participants.Count, Is.EqualTo(1));
            Assert.That(error, Does.Contain("atomic settlement"));
            Assert.That(NarrativeSaveBridge.TryCapture(data, null, out _), Is.False);
        }

        [Test]
        public void CaptureReplacesOnlyNarrativeParticipant()
        {
            GameSaveData data = WithPayload("old");
            data.Participants.Add(new GameSaveParticipantData { Id = "other", Json = "preserved" });
            Assert.That(NarrativeSaveBridge.TryCapture(data, () => new NarrativeSnapshot(), out _), Is.True);
            Assert.That(data.Participants.Count, Is.EqualTo(2));
            Assert.That(data.Participants[0].Id, Is.EqualTo("other"));
        }

        private static GameSaveData WithPayload(string json)
        {
            var data = new GameSaveData();
            data.Participants.Add(new GameSaveParticipantData { Id = NarrativeSaveBridge.ParticipantId, Json = json });
            return data;
        }
    }
}
