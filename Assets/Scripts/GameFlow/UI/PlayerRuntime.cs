using System;
using System.Collections.Generic;
using System.Linq;

namespace Spotlight.GameFlow
{
    public enum PlayerReadStatus { Success, UnknownId, NotReady }
    public delegate PlayerReadStatus PlayerValueReader<T>(string key, out T value);

    [Serializable]
    public sealed class PlayerState
    {
        public int version = 2;
        public string playerId = "player";
        public string playerName = "";
        public AvatarKind avatar;
        public bool hasSelectedAvatar;
        public string originId = "commoner";
        public bool creationSubmitted;
        public string[] optionIds = Array.Empty<string>();
        public string legacyBackgroundText = "";
        public string draftName = "";
        public string[] draftOptionIds = Array.Empty<string>();
    }

    public sealed class PlayerCreationPlan
    {
        public string PlayerName { get; }
        public string OriginId { get; }
        public int StartingMoney { get; }
        public IReadOnlyList<string> OptionIds { get; }
        public IReadOnlyDictionary<string, int> AttributeBonuses { get; }
        public string SpecializationConditionKey { get; }
        internal PlayerCreationPlan(string name, string origin, int money, IEnumerable<string> options,
            IDictionary<string, int> bonuses, string specialization)
        {
            PlayerName = name; OriginId = origin; StartingMoney = money;
            OptionIds = Array.AsReadOnly(options.ToArray());
            AttributeBonuses = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new Dictionary<string, int>(bonuses));
            SpecializationConditionKey = specialization;
        }
    }

    /// <summary>Identity belongs here. All gameplay values are read from their owners.</summary>
    public sealed class PlayerRuntime
    {
        public const string SaveId = "game.player-state.v2";
        public static PlayerRuntime Current { get; set; }
        private PlayerState state;
        private readonly PlayerCreationCatalog catalog;
        private PlayerValueReader<int> bars, items;
        private PlayerValueReader<bool> conditions;
        private bool committing;
        public event Action Changed;
        public string PlayerName => state.playerName;
        public AvatarKind Avatar => state.avatar;
        public bool HasSelectedAvatar => state.hasSelectedAvatar;
        public bool CreationSubmitted => state.creationSubmitted;
        public string OriginId => state.originId;
        public string Background => state.optionIds.Length == 0 ? state.legacyBackgroundText : catalog.Background(state.optionIds);
        public PlayerCreationCatalog Catalog => catalog;
        public PlayerRuntime(PlayerCreationCatalog catalog, PlayerState saved = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            state = saved == null ? new PlayerState { avatar = catalog.DefaultAvatar, originId = catalog.DefaultOriginId, playerName = catalog.DefaultName } : Clone(saved);
            state.optionIds = state.optionIds ?? Array.Empty<string>();
            state.draftOptionIds = state.draftOptionIds ?? Array.Empty<string>();
            state.playerName = state.playerName ?? "";
            state.draftName = state.draftName ?? "";
            state.legacyBackgroundText = state.legacyBackgroundText ?? "";
            if (state.optionIds.Any(string.IsNullOrEmpty) || state.draftOptionIds.Any(string.IsNullOrEmpty))
                throw new ArgumentException("Invalid player option ID.");
            if (state.version != 2 || state.playerId != "player" || !Enum.IsDefined(typeof(AvatarKind), state.avatar))
                throw new ArgumentException("Invalid player state.");
        }
        private static PlayerState Clone(PlayerState value) => UnityEngine.JsonUtility.FromJson<PlayerState>(UnityEngine.JsonUtility.ToJson(value));
        public PlayerState CaptureState()
        {
            if (committing) throw new InvalidOperationException("Cannot save during creation.");
            return Clone(state);
        }
        public void BindQueries(PlayerValueReader<int> readBar, PlayerValueReader<bool> readCondition, PlayerValueReader<int> readItem)
        { bars = readBar; conditions = readCondition; items = readItem; }
        public void UnbindQueries() { bars = null; conditions = null; items = null; }
        public void NotifyOwnersChanged() => Changed?.Invoke();
        public PlayerReadStatus TryReadAttribute(string id, out int value)
        {
            value = 0;
            var attribute = catalog.Attributes.Find(a => a.AttributeId == id);
            return attribute == null ? PlayerReadStatus.UnknownId : Read(bars, attribute.BarKey, out value);
        }
        public PlayerReadStatus TryReadSpecialization(string id, out bool value) => Read(conditions, id.StartsWith("specialization.", StringComparison.Ordinal) ? id : "specialization.player." + id, out value);
        public PlayerReadStatus TryReadItemCount(string id, out int value) => Read(items, id, out value);
        public PlayerReadStatus TryReadMoney(out int value) => Read(bars, "cafe.money", out value);
        public PlayerReadStatus TryReadActionPoints(out int value) => Read(bars, "player.action_points", out value);
        public PlayerReadStatus TryReadActionPointMax(out int value) => Read(bars, "player.action_points_max", out value);
        private static PlayerReadStatus Read<T>(PlayerValueReader<T> reader, string id, out T value)
        { value = default; return reader == null ? PlayerReadStatus.NotReady : reader(id, out value); }
        public void SelectAvatar(AvatarKind avatar)
        {
            if (!Enum.IsDefined(typeof(AvatarKind), avatar)) throw new ArgumentOutOfRangeException(nameof(avatar));
            state.avatar = avatar; state.hasSelectedAvatar = true; Changed?.Invoke();
        }
        public FirstDayLetterDraft RestoreDraft() => new FirstDayLetterDraft(catalog, state.draftName, state.draftOptionIds);
        public void SaveDraft(FirstDayLetterDraft draft)
        {
            if (CreationSubmitted || committing) return;
            state.draftName = draft.PlayerName; state.draftOptionIds = draft.OptionIds.ToArray();
        }
        public PlayerCreationPlan PrepareCreation(FirstDayLetterDraft draft) => catalog.Prepare(draft);
        public PlayerCreationPlan PrepareDefaultCreation() => new PlayerCreationPlan(catalog.DefaultName, catalog.DefaultOriginId,
            catalog.BaseStartingMoney, Array.Empty<string>(), new Dictionary<string, int>(), "");
        /// <summary>Owner callback must preflight dependencies and apply atomically under initialization suppression.
        /// On failure it must restore owners before throwing. Identity commits only after callback succeeds.</summary>
        public bool CommitCreation(PlayerCreationPlan plan, Action<PlayerCreationPlan> applyOwners)
        {
            if (CreationSubmitted || committing) return false;
            if (plan == null || applyOwners == null) throw new ArgumentNullException();
            committing = true;
            try
            {
                applyOwners(plan);
                state.playerName = plan.PlayerName; state.originId = plan.OriginId;
                state.optionIds = plan.OptionIds.ToArray(); state.creationSubmitted = true;
                state.draftName = ""; state.draftOptionIds = Array.Empty<string>();
            }
            finally { committing = false; }
            Changed?.Invoke();
            return true;
        }
    }
}
