using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    public static class NarrativeSaveBridge
    {
        public const string ParticipantId = "spotlight.narrative";
        public const int CurrentVersion = 2;

        [Serializable]
        public sealed class Envelope
        {
            public int Version;
            public NarrativeSnapshot Snapshot;
        }

        public static bool TryCapture(GameSaveData data, Func<NarrativeSnapshot> capture, out string error)
        {
            error = null;
            if (data == null || capture == null) { error = "NCS runtime is not initialized; save was not written."; return false; }
            try
            {
                NarrativeSnapshot snapshot = capture();
                if (snapshot == null) { error = "NCS snapshot is unavailable; save was not written."; return false; }
                string json = JsonUtility.ToJson(new Envelope { Version = CurrentVersion, Snapshot = snapshot });
                if (data.Participants == null) data.Participants = new List<GameSaveParticipantData>();
                data.Participants.RemoveAll(item => item != null && item.Id == ParticipantId);
                data.Participants.Add(new GameSaveParticipantData { Id = ParticipantId, Json = json });
                return true;
            }
            catch (Exception exception) { error = "Could not capture NCS state: " + exception.Message; return false; }
        }

        public static bool TryRead(GameSaveData data, out NarrativeSnapshot snapshot, out string error)
        {
            snapshot = null; error = null;
            if (data == null) { error = "Game save data is missing."; return false; }
            GameSaveParticipantData found = null;
            if (data.Participants != null)
                foreach (GameSaveParticipantData item in data.Participants)
                    if (item != null && item.Id == ParticipantId)
                    {
                        if (found != null) { error = "Save has duplicate NCS participants."; return false; }
                        found = item;
                    }
            // Pre-NCS saves are supported by starting a fresh narrative session explicitly.
            if (found == null) return true;
            if (string.IsNullOrWhiteSpace(found.Json)) { error = "NCS save payload is empty."; return false; }
            try
            {
                JObject payload = JObject.Parse(found.Json);
                JToken version = payload["Version"];
                if (version == null || version.Type != JTokenType.Integer || (version.Value<long>() != 1 && version.Value<long>() != CurrentVersion))
                {
                    error = "Unsupported NCS save envelope version.";
                    return false;
                }
                JToken snapshotToken = payload["Snapshot"];
                if (snapshotToken == null || snapshotToken.Type != JTokenType.Object)
                {
                    error = "NCS save snapshot is missing or is not an object.";
                    return false;
                }
                Envelope envelope = JsonUtility.FromJson<Envelope>(found.Json);
                if (envelope == null || (envelope.Version != 1 && envelope.Version != CurrentVersion))
                {
                    error = "Unsupported NCS save envelope version: " + (envelope == null ? "missing" : envelope.Version.ToString());
                    return false;
                }
                if (envelope.Snapshot == null) { error = "NCS save snapshot is missing."; return false; }
                snapshot = envelope.Snapshot;
                // JsonUtility may initialize absent lists: preserve the explicit v1 migration signal.
                if (envelope.Version == 1 && snapshotToken["conditionValues"] == null)
                    snapshot.conditionValues = null;
                return true;
            }
            catch (Exception exception) { error = "NCS save payload is invalid: " + exception.Message; return false; }
        }
    }
}
