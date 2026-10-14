using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Pure pre-restore migration. Never applies gameplay rewards or touches live owners.</summary>
    public static class PlayerSaveBridge
    {
        public const string ParticipantId = "game.player-state.v2";
        public const string LegacyParticipantId = "game.player-details-profile.v1";
        private static readonly string[] AttributeKeys = { "player.language", "player.courage", "player.wisdom", "player.perception" };
        private static readonly HashSet<string> Specializations = new HashSet<string>(StringComparer.Ordinal)
        { "letter.religion", "letter.literature", "letter.science", "letter.politics" };

        public static void Register(Func<PlayerRuntime> getRuntime)
        {
            if (getRuntime == null) throw new ArgumentNullException(nameof(getRuntime));
            GameSaveParticipantRegistry.Capturers.Remove(LegacyParticipantId);
            GameSaveParticipantRegistry.Restorers.Remove(LegacyParticipantId);
            GameSaveParticipantRegistry.Restorers.Remove(ParticipantId);
            GameSaveParticipantRegistry.Capturers[ParticipantId] = () =>
            {
                var runtime = getRuntime();
                if (runtime == null) throw new InvalidOperationException("Player session is unavailable.");
                return JsonUtility.ToJson(runtime.CaptureState());
            };
        }

        public static bool TryPrepareRestore(GameSaveData data, out PlayerState state, out NarrativeSnapshot snapshot, out string error)
        {
            state = null;
            if (!NarrativeSaveBridge.TryRead(data, out snapshot, out error)) return false;
            // A pre-NCS save still needs a snapshot to carry imported attributes/flags.
            var candidate = snapshot ?? new NarrativeSnapshot();
            if (!TryRead(data, candidate, out state, out error)) { snapshot = null; return false; }
            snapshot = candidate;
            return true;
        }

        /// <summary>On success updates only missing legacy values in snapshot. On failure neither input is changed.</summary>
        public static bool TryRead(GameSaveData data, NarrativeSnapshot snapshot, out PlayerState state, out string error)
        {
            state = null; error = null;
            try
            {
                if (data == null) throw new ArgumentException("Game save data is missing.");
                var current = Find(data, ParticipantId);
                if (current != null)
                {
                    var json = Parse(current);
                    ValidatePlayer(json);
                    state = JsonUtility.FromJson<PlayerState>(current.Json);
                    Normalize(state);
                    return true;
                }
                var legacy = Find(data, LegacyParticipantId);
                var profile = legacy == null ? new JObject() : Parse(legacy);
                var candidate = new PlayerState { creationSubmitted = IsSubmitted(data) };
                candidate.playerName = ReadString(profile, "playerName");
                candidate.legacyBackgroundText = ReadString(profile, "background");
                candidate.avatar = (AvatarKind)ReadInt(profile, "avatar", 0);
                if (!Enum.IsDefined(typeof(AvatarKind), candidate.avatar)) throw new ArgumentException("Invalid legacy avatar.");
                candidate.hasSelectedAvatar = ReadBool(profile, "hasSelectedAvatar", false);
                string origin = ReadString(profile, "origin");
                switch (origin)
                {
                    case "流浪儿": case "waif": candidate.originId = "waif"; break;
                    case "平民": case "commoner": candidate.originId = "commoner"; break;
                    case "贵族": case "noble": candidate.originId = "noble"; break;
                    default:
                        candidate.originId = "legacy/unknown";
                        if (!string.IsNullOrEmpty(origin)) Debug.LogWarning("[Player migration] Unknown origin retained as legacy/unknown: " + origin);
                        break;
                }
                if (!candidate.creationSubmitted)
                {
                    candidate.draftName = ReadString(profile, "draftName", candidate.playerName);
                    candidate.draftOptionIds = ReadStrings(profile, "draftOptionIds");
                }
                var importedBars = new List<NarrativeBarValue>();
                var existing = new HashSet<string>(StringComparer.Ordinal);
                if (snapshot?.bars != null)
                    foreach (var bar in snapshot.bars)
                    {
                        if (bar == null || string.IsNullOrEmpty(bar.key)) throw new ArgumentException("Invalid NCS bar entry.");
                        if (!bar.reverse && !existing.Add(bar.key)) throw new ArgumentException("Duplicate NCS bar: " + bar.key);
                    }
                if (profile["attributes"] != null && profile["attributes"].Type != JTokenType.Null)
                {
                    var attributes = profile["attributes"] as JArray;
                    if (attributes == null || attributes.Count != 4) throw new ArgumentException("Legacy attributes must contain four values.");
                    for (int i = 0; i < 4; i++)
                    {
                        if (existing.Contains(AttributeKeys[i])) continue;
                        int value = Integer(attributes[i], "attributes[" + i + "]");
                        if (value < 0 || value > 20) throw new ArgumentException("Legacy attribute is outside 0..20.");
                        importedBars.Add(new NarrativeBarValue { key = AttributeKeys[i], value = value });
                    }
                }
                var importedFlags = new List<NarrativeBoolValue>();
                if (snapshot == null || snapshot.conditionValues == null)
                    foreach (string id in ReadStrings(profile, "specializations"))
                    {
                        if (!Specializations.Contains(id)) { Debug.LogWarning("[Player migration] Undefined legacy specialization ignored: " + id); continue; }
                        importedFlags.Add(new NarrativeBoolValue { key = "specialization.player." + id, value = true });
                    }
                if (snapshot == null && (importedBars.Count > 0 || importedFlags.Count > 0))
                    throw new ArgumentException("Use TryPrepareRestore for a legacy save without an NCS snapshot.");
                // Commit only after all JSON and migration values have passed validation.
                if (snapshot != null)
                {
                    if (snapshot.bars == null) snapshot.bars = new List<NarrativeBarValue>();
                    snapshot.bars.AddRange(importedBars);
                    if (importedFlags.Count > 0)
                    {
                        if (snapshot.flags == null) snapshot.flags = new List<NarrativeBoolValue>();
                        foreach (var flag in importedFlags)
                            if (!snapshot.flags.Exists(item => item != null && item.key == flag.key)) snapshot.flags.Add(flag);
                    }
                }
                state = candidate;
                return true;
            }
            catch (Exception exception) { state = null; error = "Player save migration failed: " + exception.Message; return false; }
        }

        private static bool IsSubmitted(GameSaveData data)
        {
            var day = Find(data, "game.day-flow.v1");
            if (day == null) return true;
            JObject json = Parse(day);
            int number = ReadInt(json, "day", 1);
            int phase = ReadInt(json, "state", 0);
            if (number < 1 || number > 9999 || !Enum.IsDefined(typeof(DayFlowState), phase)) throw new ArgumentException("Invalid DayFlow state.");
            return number != 1 || phase != (int)DayFlowState.IntroLetter || ReadBool(json, "firstDayIntroductionCompleted", false);
        }
        private static GameSaveParticipantData Find(GameSaveData data, string id)
        {
            GameSaveParticipantData found = null;
            if (data.Participants != null) foreach (var item in data.Participants)
                if (item != null && item.Id == id)
                { if (found != null) throw new ArgumentException("Duplicate participant: " + id); found = item; }
            return found;
        }
        private static JObject Parse(GameSaveParticipantData entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Json)) throw new ArgumentException("Empty participant: " + entry.Id);
            return JObject.Parse(entry.Json);
        }
        private static void ValidatePlayer(JObject json)
        {
            if (ReadInt(json, "version", -1) != 2 || ReadString(json, "playerId") != "player") throw new ArgumentException("Unsupported player state version or identity.");
            if (!Enum.IsDefined(typeof(AvatarKind), ReadInt(json, "avatar", -1))) throw new ArgumentException("Invalid player avatar.");
            if (json["creationSubmitted"] == null) throw new ArgumentException("Player creationSubmitted is missing.");
            if (json["hasSelectedAvatar"] == null) throw new ArgumentException("Player hasSelectedAvatar is missing.");
            ReadBool(json, "creationSubmitted", false); ReadBool(json, "hasSelectedAvatar", false);
            if (string.IsNullOrWhiteSpace(ReadString(json, "originId"))) throw new ArgumentException("Player originId is missing or empty.");
            foreach (string field in new[] { "playerName", "originId", "legacyBackgroundText", "draftName" }) ReadString(json, field);
            ReadStrings(json, "optionIds"); ReadStrings(json, "draftOptionIds");
        }
        private static void Normalize(PlayerState state)
        {
            state.playerName = state.playerName ?? ""; state.originId = state.originId ?? "legacy/unknown";
            state.legacyBackgroundText = state.legacyBackgroundText ?? ""; state.draftName = state.draftName ?? "";
            state.optionIds = state.optionIds ?? Array.Empty<string>(); state.draftOptionIds = state.draftOptionIds ?? Array.Empty<string>();
        }
        private static int Integer(JToken token, string field)
        {
            if (token == null || token.Type != JTokenType.Integer) throw new ArgumentException("Expected integer: " + field);
            long value = token.Value<long>();
            if (value < int.MinValue || value > int.MaxValue) throw new ArgumentException("Integer overflow: " + field);
            return (int)value;
        }
        private static int ReadInt(JObject json, string field, int fallback) => json[field] == null ? fallback : Integer(json[field], field);
        private static bool ReadBool(JObject json, string field, bool fallback)
        {
            if (json[field] == null) return fallback;
            if (json[field].Type != JTokenType.Boolean) throw new ArgumentException("Expected bool: " + field);
            return json[field].Value<bool>();
        }
        private static string ReadString(JObject json, string field, string fallback = "")
        {
            if (json[field] == null || json[field].Type == JTokenType.Null) return fallback;
            if (json[field].Type != JTokenType.String) throw new ArgumentException("Expected string: " + field);
            return json[field].Value<string>();
        }
        private static string[] ReadStrings(JObject json, string field)
        {
            if (json[field] == null || json[field].Type == JTokenType.Null) return Array.Empty<string>();
            if (!(json[field] is JArray array)) throw new ArgumentException("Expected array: " + field);
            var result = new string[array.Count];
            for (int i = 0; i < result.Length; i++)
            {
                if (array[i].Type != JTokenType.String || string.IsNullOrWhiteSpace(array[i].Value<string>())) throw new ArgumentException("Invalid string in " + field);
                result[i] = array[i].Value<string>();
            }
            return result;
        }
    }
}
