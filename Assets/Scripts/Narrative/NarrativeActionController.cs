using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spotlight.Narrative
{
    /// <summary>
    /// 地图无关的行动执行层。地点 UI 只需调用 GetAvailableActions/Execute。
    /// </summary>
    public sealed class NarrativeActionController
    {
        private readonly NarrativeController narrative;
        private readonly Dictionary<string, NarrativeActionDefinition> definitions;
        private readonly Dictionary<string, int> counters = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> dailyExecution = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> expiryDays = new Dictionary<string, int>(StringComparer.Ordinal);
        private INarrativeActionInventory inventory;
        private readonly List<NarrativeActionMountValue> mounts = new List<NarrativeActionMountValue>();
        private readonly HashSet<string> retired = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> dueActions = new Queue<string>();
        private readonly Dictionary<string, NarrativeActionDeliveryValue> deliveries = new Dictionary<string, NarrativeActionDeliveryValue>(StringComparer.Ordinal);
        private readonly HashSet<string> seenActionInstances = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> pendingFocusPositions = new List<string>();
        private readonly Dictionary<string, string> mapContentSignatures = new Dictionary<string, string>(StringComparer.Ordinal);
        private bool committing;
        private bool operationOpen;
        private bool executing;
        private string executingAction;
        private Queue<string> pendingEvents;
        private string currentEventId;
        private Action<bool> pendingCompletion;
        private int refundablePoolPoints;

        public NarrativeActionPoints ActionPoints { get; }
        public int CurrentDay { get; private set; } = 1;
        public bool IsExecuting => executing;
        public bool CanCaptureSnapshot => !committing && !(executing && DefersCompletion(Get(executingAction)));
        public event Action StateChanged;
        public event Action<string> MapContentChanged;
        public event Action<NarrativeActionDefinition> ActionStarted;
        public event Action<NarrativeActionDefinition, bool> ActionFinished;

        public NarrativeActionController(NarrativeController narrative, IEnumerable<NarrativeActionDefinition> actions,
            int actionPointMax = 5, INarrativeActionInventory inventory = null)
        {
            this.narrative = narrative ?? throw new ArgumentNullException(nameof(narrative));
            definitions = new Dictionary<string, NarrativeActionDefinition>(StringComparer.Ordinal);
            foreach (NarrativeActionDefinition action in actions ?? Enumerable.Empty<NarrativeActionDefinition>())
            {
                Validate(action);
                if (!definitions.TryAdd(action.actionKey, action)) throw new FormatException("Duplicate action key: " + action.actionKey);
                if (action.parsedRequirements == null || action.parsedRequirements.Count == 0)
                    action.parsedRequirements = NarrativeActionRequirementParser.Parse(action.requirements);
                foreach (var cost in action.parsedRequirements)
                    if (cost == null || (cost.type != NarrativeActionRequirementType.Item && cost.type != NarrativeActionRequirementType.ActionPoint)
                        || cost.value <= 0 || (cost.type == NarrativeActionRequirementType.Item && string.IsNullOrWhiteSpace(cost.key)))
                        throw new FormatException("Action requires positive Item/ActionPoint costs; move read-only checks to Condition: " + action.actionKey);
                if (action.IsDelivery) deliveries.Add(action.actionKey, new NarrativeActionDeliveryValue { actionKey = action.actionKey });
            }
            foreach (var action in definitions.Values)
                if (action.type == NarrativeActionType.Future && (string.IsNullOrEmpty(action.nextActionKey) || Get(action.nextActionKey) == null || action.nextActionKey == action.actionKey))
                    throw new FormatException("Future action requires a different configured nextActionKey: " + action.actionKey);
            ActionPoints = new NarrativeActionPoints(actionPointMax);
            StateChanged += () => RefreshMapContent();
            narrative.State.ConditionsChanged += () => { if (!committing) StateChanged?.Invoke(); };
            narrative.BarChanged += (_, __, ___) => { if (!committing) { RefreshMapContent(); StateChanged?.Invoke(); } };
            ActionPoints.Changed += (_, __) => { if (!committing) StateChanged?.Invoke(); };
            SetInventory(inventory);
            narrative.ActionProgressReader = key => Get(key) == null ? (int?)null : GetProgress(key);
            narrative.ActionCompletedReader = key => Get(key) == null ? (bool?)null : IsCompleted(key);
            narrative.EventCompleted += OnEventCompleted;
            narrative.RefreshDailyAvailability(CurrentDay);
            narrative.EventFailed += (eventId, error) => { if (executing && currentEventId == eventId) ReportFailure(); };
        }

        public void SetInventory(INarrativeActionInventory value)
        {
            if (inventory is NarrativeMapInventory previous) previous.Changed -= OnInventoryChanged;
            inventory = value ?? new EmptyNarrativeActionInventory();
            if (inventory is NarrativeMapInventory mapInventory)
            {
                narrative.Inventory = mapInventory;
                mapInventory.Changed += OnInventoryChanged;
            }
            OnInventoryChanged();
        }

        private void OnInventoryChanged()
        {
            if (!committing) StateChanged?.Invoke();
        }
        public IEnumerable<NarrativeActionDefinition> Actions => definitions.Values;
        public NarrativeActionDefinition Get(string actionKey) => actionKey != null && definitions.TryGetValue(actionKey, out var action) ? action : null;

        public void BeginDay(int day, int actionPointMax = -1)
        {
            if (narrative.IsEnded) return;
            if (executing) throw new InvalidOperationException("Cannot begin a day while an action is executing.");
            if (day < CurrentDay) throw new ArgumentOutOfRangeException(nameof(day), "Day cannot move backwards.");
            if (day == CurrentDay) return;
            bool advanced = day > CurrentDay;
            narrative.BeginOperation();
            RefreshForDay(day);
            if (actionPointMax >= 0) ActionPoints.ResetForDay(actionPointMax);
            else ActionPoints.ResetForDay(ActionPoints.Max);
            if (advanced)
            {
                foreach (var action in definitions.Values)
                    if (IsVisible(action.actionKey) && !IsCompleted(action.actionKey) && expiryDays.TryGetValue(action.actionKey, out int deadline) && CurrentDay >= deadline)
                    {
                        retired.Add(action.actionKey);
                        if (action.type == NarrativeActionType.Future && !string.IsNullOrEmpty(action.nextActionKey))
                            foreach (var position in mounts.Where(m => m.action == action.actionKey).Select(m => m.position).Distinct().ToArray())
                                Mount(position, action.nextActionKey);
                        dueActions.Enqueue(action.actionKey);
                    }
            }
            StateChanged?.Invoke();
            PlayDueAction();
            narrative.EndOperation();
        }

        /// <summary>Refreshes effective delivery progress after the formal day has been restored; never refills points.</summary>
        public void RefreshForDay(int day)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            CurrentDay = day;
            narrative.RefreshDailyAvailability(day);
            bool changed = false;
            foreach (var pair in deliveries)
            {
                var value = pair.Value;
                if (Get(pair.Key).deliveryMode == NarrativeDeliveryMode.Consecutive && !value.completed
                    && value.progress > 0 && (long)day - value.lastSubmissionDay > 1)
                {
                    value.progress = 0;
                    changed = true;
                }
            }
            RefreshMapContent();
            if (!changed) return;
            narrative.NotifyConditionsChanged();
            StateChanged?.Invoke();
        }

        public void Mount(string position, string key, string instanceVersion = null)
        {
            var action = Get(key);
            if (action == null) throw new FormatException("Unknown mounted action: " + key);
            if (string.IsNullOrWhiteSpace(position)) throw new ArgumentException("A map position is required.", nameof(position));
            var mounted = mounts.FirstOrDefault(m => m.position == position && m.action == key);
            if (mounted != null)
            {
                if (string.IsNullOrEmpty(instanceVersion) || mounted.instanceVersion == instanceVersion) return;
                mounted.instanceVersion = instanceVersion;
            }
            else mounts.Add(new NarrativeActionMountValue { position = position, action = key, day = CurrentDay, instanceVersion = instanceVersion });
            if ((action.IsTimed || action.type == NarrativeActionType.Future) && !expiryDays.ContainsKey(key))
                expiryDays[key] = checked(CurrentDay + action.durationDays);
            RefreshMapContent(position);
        }

        /// <summary>One projection shared by details, badges and mandatory guidance. Costs do not hide cards.</summary>
        public IEnumerable<NarrativeActionDefinition> GetDisplayableActions(string position)
        {
            if (!narrative.ValuesReady) return Enumerable.Empty<NarrativeActionDefinition>();
            var values = GetMountedActions(position).Where(a => narrative.EvaluateConditions("action", a.actionKey)).ToList();
            return values.Any(a => a.replacesLocationActions && !IsCompleted(a.actionKey))
                ? values.Where(a => a.replacesLocationActions && !IsCompleted(a.actionKey)) : values;
        }

        /// <summary>Real map content, independent of costs, playback locks and whether its card was read.</summary>
        public IEnumerable<NarrativeActionDefinition> GetMapActions(string position)
            => GetDisplayableActions(position).Where(action => IsAvailable(action.actionKey, out _, ignoreTransientBusy: true, ignoreCosts: true));

        public bool HasMapActions(string position) => GetMapActions(position).Any();

        public bool HasImportantMapActions(string position)
            => GetMapActions(position).Any(action => action.mustExecute && !IsDailyPoolAction(action.actionKey));

        public bool ReplacesLocationActions(string position)
            => GetDisplayableActions(position).Any(a => a.replacesLocationActions && !IsCompleted(a.actionKey));

        public string GetActionInstanceKey(string position, string actionKey)
        {
            var mount = mounts.FirstOrDefault(m => m.position == position && m.action == actionKey);
            if (mount == null) return null;
            var action = Get(actionKey);
            string version = string.IsNullOrEmpty(mount.instanceVersion) ? "mount:" + mount.day : mount.instanceVersion;
            if (action.type == NarrativeActionType.Daily || action.IsDelivery || narrative.UsesDailyPool(action)) version += "/day:" + CurrentDay;
            return Uri.EscapeDataString(position) + "|" + Uri.EscapeDataString(actionKey) + "|" + Uri.EscapeDataString(version);
        }

        private bool IsUnseenCandidate(NarrativeActionDefinition action)
            => !IsCompleted(action.actionKey) && !(dailyExecution.TryGetValue(action.actionKey, out int day) && day == CurrentDay
                && (action.type == NarrativeActionType.Daily || action.IsDelivery));

        /// <summary>Unread visible cards, including those whose costs cannot currently be paid.</summary>
        public bool HasUnseenActions(string position)
            => GetDisplayableActions(position).Any(a => IsUnseenCandidate(a) && !seenActionInstances.Contains(GetActionInstanceKey(position, a.actionKey)));

        /// <summary>Map badge eligibility ignores temporary playback/execution locks, but not costs or world state.</summary>
        public bool HasUnseenAvailableActions(string position)
            => GetDisplayableActions(position).Any(a => IsUnseenCandidate(a)
                && !seenActionInstances.Contains(GetActionInstanceKey(position, a.actionKey))
                && IsAvailable(a.actionKey, out _, ignoreTransientBusy: true));

        /// <summary>Unread important story actions with configured events; daily pools are activity, not map alerts.</summary>
        public bool HasUnseenImportantActions(string position)
            => GetDisplayableActions(position).Any(a => a.mustExecute
                && a.type != NarrativeActionType.Daily
                && !IsDailyPoolAction(a.actionKey)
                && a.resultEvents != null && a.resultEvents.Any(eventId => !string.IsNullOrWhiteSpace(eventId))
                && IsUnseenCandidate(a)
                && !seenActionInstances.Contains(GetActionInstanceKey(position, a.actionKey))
                && IsAvailableForMapBadge(a.actionKey));

        private bool IsAvailableForMapBadge(string actionKey)
            => IsAvailable(actionKey, out _, ignoreTransientBusy: true, ignoreCosts: true);

        /// <summary>Only the actual rendered, unobscured detail page may acknowledge its captured instance token.</summary>
        public bool ConfirmActionDisplayed(string position, string instanceKey)
        {
            if (string.IsNullOrEmpty(instanceKey) || !GetDisplayableActions(position).Any(a => GetActionInstanceKey(position, a.actionKey) == instanceKey)) return false;
            if (!seenActionInstances.Add(instanceKey)) return false;
            RefreshMapContent(position);
            return true;
        }

        /// <summary>Call after changing map context; source data is evaluated in memory, never reloaded from Excel.</summary>
        public void RefreshMapContent(string positionKey = null)
        {
            foreach (string position in mounts.Select(m => m.position).Distinct().Where(p => positionKey == null || p == positionKey).ToArray())
            {
                string signature = string.Join(";", GetDisplayableActions(position).Select(a => GetActionInstanceKey(position, a.actionKey)
                    + ":" + IsUnseenCandidate(a) + ":" + seenActionInstances.Contains(GetActionInstanceKey(position, a.actionKey))
                    + ":" + IsAvailable(a.actionKey, out _, ignoreTransientBusy: true)
                    + ":content:" + IsAvailableForMapBadge(a.actionKey)));
                if (mapContentSignatures.TryGetValue(position, out string previous) && previous == signature) continue;
                mapContentSignatures[position] = signature;
                MapContentChanged?.Invoke(position);
            }
        }

        // Map tasks are hidden while the map is closed (world.map.active guard), but they are still
        // outstanding: the cafe bed must not skip them. Evaluate as if the map were open.
        private const string MapActiveGuard = "world.map.active";
        public IEnumerable<string> GetBlockingPositions()
            => narrative.EvaluateAssumingGuard(MapActiveGuard, true, () => mounts.Select(m => m.position).Distinct()
                .Where(position => GetDisplayableActions(position).Any(IsBlockingAction)).ToList());

        // A mandatory action blocks the night only while today's action points can still pay for it.
        // Ordinary actions cannot spend those reserved points (see IsAvailable), so this fallback only
        // applies when the action unlocked after the points were gone; it then waits for tomorrow.
        private bool IsBlockingAction(NarrativeActionDefinition action)
            => action.mustExecute && !IsCompleted(action.actionKey) && action.type != NarrativeActionType.Future
                && (!dailyExecution.TryGetValue(action.actionKey, out int day) || day != CurrentDay)
                && RequiredActionPoints(action) <= ActionPoints.Current;

        private static long RequiredActionPoints(NarrativeActionDefinition action)
            => (action.parsedRequirements ?? NarrativeActionRequirementParser.Parse(action.requirements))
                .Where(r => r.type == NarrativeActionRequirementType.ActionPoint).Sum(r => (long)r.value);

        /// <summary>Points still needed by outstanding mandatory actions other than <paramref name="except"/>.</summary>
        private long ReservedActionPoints(NarrativeActionDefinition except)
        {
            long reserved = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string position in mounts.Select(m => m.position).Distinct())
                foreach (NarrativeActionDefinition candidate in GetDisplayableActions(position))
                    if (candidate != except && seen.Add(candidate.actionKey) && IsBlockingAction(candidate))
                        reserved += RequiredActionPoints(candidate);
            return reserved;
        }

        public string PendingFocusPosition => pendingFocusPositions.Count > 0 ? pendingFocusPositions[0] : null;
        public bool ConsumePendingFocus(string position) => pendingFocusPositions.Remove(position);

        public IEnumerable<string> GetMapPositions() => mounts.Select(m => m.position).Distinct();
        public bool IsMounted(string position, string actionKey) => mounts.Any(m => m.position == position && m.action == actionKey);
        public bool HasExecutedToday(string actionKey) => dailyExecution.TryGetValue(actionKey, out int day) && day == CurrentDay;

        /// <summary>Daily event pools allow one completed activity per action per day.</summary>
        public bool IsDailyPoolAction(string actionKey) => narrative.UsesDailyPool(Get(actionKey));

        public IEnumerable<NarrativeActionDefinition> GetMountedActions(string position)
            => mounts.Where(m => m.position == position).Select(m => Get(m.action)).Where(a => a != null && IsVisible(a.actionKey));
        public bool IsVisible(string key)
        {
            var action = Get(key);
            return action != null && !retired.Contains(key) && (action.IsDelivery || !IsCompleted(key));
        }
        public int GetRemainingDays(string key) => expiryDays.TryGetValue(key, out int deadline) ? Math.Max(0, deadline - CurrentDay) : 0;
        public int GetProgress(string key) => key != null && deliveries.TryGetValue(key, out var delivery) ? delivery.progress
            : key != null && counters.TryGetValue(key, out int value) ? value : 0;
        public bool IsCompleted(string key)
        {
            if (key != null && deliveries.TryGetValue(key, out var delivery)) return delivery.completed;
            var action = Get(key);
            return action != null && (action.IsProgress ? GetProgress(key) >= action.progressTarget
                : action.type == NarrativeActionType.Once && GetProgress(key) > 0);
        }
        public bool HasSubmittedToday(string key) => key != null && deliveries.TryGetValue(key, out var value) && value.lastSubmissionDay == CurrentDay;
        public bool HasBlockingActions()
        {
            return GetBlockingPositions().Any();
        }

        public bool IsAvailable(string actionKey, out List<NarrativeActionCheck> checks)
            => IsAvailable(actionKey, out checks, ignoreTransientBusy: false);

        private bool IsAvailable(string actionKey, out List<NarrativeActionCheck> checks, bool ignoreTransientBusy)
            => IsAvailable(actionKey, out checks, ignoreTransientBusy, ignoreCosts: false);

        private bool IsAvailable(string actionKey, out List<NarrativeActionCheck> checks, bool ignoreTransientBusy, bool ignoreCosts)
        {
            checks = new List<NarrativeActionCheck>();
            NarrativeActionDefinition action = Get(actionKey);
            if (action == null || !narrative.ValuesReady) return false;
            bool eligible = (ignoreTransientBusy || (!executing && !narrative.IsBusy)) && !narrative.IsEnded
                && IsVisible(actionKey) && !IsCompleted(actionKey) && action.type != NarrativeActionType.Future;
            if (action.IsDelivery && HasSubmittedToday(actionKey)) eligible = false;
            if (action.endDay && HasBlockingActions()) eligible = false;
            if ((action.type == NarrativeActionType.Daily || narrative.UsesDailyPool(action)) && HasExecutedToday(actionKey)) eligible = false;
            if (!ignoreCosts)
            {
                foreach (NarrativeActionRequirement requirement in action.parsedRequirements ?? NarrativeActionRequirementParser.Parse(action.requirements))
                {
                    NarrativeActionCheck check = Check(requirement);
                    checks.Add(check);
                }
                // Repeated costs share the same stock; checking each row independently could overspend.
                foreach (var group in action.parsedRequirements.Where(r => r.type == NarrativeActionRequirementType.Item).GroupBy(r => r.key))
                    if (group.Sum(r => (long)r.value) > inventory.GetCount(group.Key))
                        for (int i = 0; i < action.parsedRequirements.Count; i++)
                            if (action.parsedRequirements[i].type == NarrativeActionRequirementType.Item && action.parsedRequirements[i].key == group.Key) checks[i].satisfied = false;
                long points = action.parsedRequirements.Where(r => r.type == NarrativeActionRequirementType.ActionPoint).Sum(r => (long)r.value);
                // Optional actions may not spend the points an outstanding mandatory action needs today.
                if (points > ActionPoints.Current)
                {
                    for (int i = 0; i < action.parsedRequirements.Count; i++)
                        if (action.parsedRequirements[i].type == NarrativeActionRequirementType.ActionPoint) checks[i].satisfied = false;
                }
                else if (points > 0 && !action.mustExecute && points + ReservedActionPoints(action) > ActionPoints.Current)
                    checks.Add(new NarrativeActionCheck { satisfied = false, requirement = "MustExecute", messageKey = "action.condition.missing" });
            }
            bool conditionsMet = narrative.EvaluateConditions("action", actionKey);
            if (!conditionsMet) checks.Add(new NarrativeActionCheck { satisfied = false, requirement = "Condition(action," + actionKey + ")", messageKey = "action.condition.missing" });
            bool poolReady = !narrative.UsesDailyPool(action) || narrative.TrySelectActionEvents(action, out _, ignoreTransientBusy);
            if (!poolReady) checks.Add(new NarrativeActionCheck { satisfied = false, requirement = "DailyPool", messageKey = "action.condition.missing" });
            return eligible && conditionsMet && poolReady && (ignoreCosts || checks.All(check => check.satisfied));
        }

        public bool Execute(string actionKey, Action<bool> completed = null)
        {
            if (!IsAvailable(actionKey, out _)) return false;
            NarrativeActionDefinition action = definitions[actionKey];
            if (!narrative.TrySelectActionEvents(action, out var selectedEvents)) return false;
            refundablePoolPoints = narrative.UsesDailyPool(action) ? action.parsedRequirements.Where(r => r.type == NarrativeActionRequirementType.ActionPoint).Sum(r => r.value) : 0;
            executing = true;
            committing = true;
            executingAction = actionKey;
            OpenOperation();
            try
            {
                using ((inventory as NarrativeMapInventory)?.DeferNotifications())
                {
                    if (!ConsumeRequirements(action))
                    {
                        executing = false; executingAction = null;
                        CloseOperation();
                        return false;
                    }
                    pendingCompletion = completed;
                    pendingEvents = new Queue<string>();
                    // Narrative Once actions are not completed by accepting a click. Commit only
                    // after every result event succeeds so cancelled mandatory stories remain retryable.
                    if (!DefersCompletion(action)) RecordExecution(action);
                    if (action.IsDelivery)
                    {
                        var delivery = deliveries[actionKey];
                        foreach (var milestone in (action.milestones ?? new List<NarrativeActionMilestoneDefinition>())
                            .OrderBy(m => m.seq).ThenBy(m => m.milestoneId, StringComparer.Ordinal))
                        {
                            if (milestone.atProgress != delivery.progress || delivery.triggeredMilestones.Contains(milestone.milestoneId)) continue;
                            delivery.triggeredMilestones.Add(milestone.milestoneId);
                            pendingEvents.Enqueue(milestone.eventId);
                        }
                    }
                    if (!action.IsProgress || IsCompleted(actionKey))
                        foreach (string eventId in selectedEvents) pendingEvents.Enqueue(eventId);
                }
            }
            finally { committing = false; }
            narrative.NotifyConditionsChanged();
            StateChanged?.Invoke();
            ActionStarted?.Invoke(action);
            PlayNextEvent();
            return true;
        }

        public void CaptureInto(NarrativeActionStateSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (!CanCaptureSnapshot) throw new InvalidOperationException("Cannot save during action commit or an unfinished Once presentation.");
            snapshot.deliveryVersion = 1;
            snapshot.deliveries = deliveries.Values.Select(CloneDelivery).ToList();
            snapshot.day = CurrentDay;
            snapshot.actionPoints = ActionPoints.Current;
            snapshot.actionPointMax = ActionPoints.Max;
            snapshot.counters = counters.Select(pair => new NarrativeActionCounterValue { key = pair.Key, value = pair.Value }).ToList();
            snapshot.daily = dailyExecution.Select(pair => new NarrativeActionDayValue { key = pair.Key, day = pair.Value }).ToList();
            snapshot.expiry = expiryDays.Select(pair => new NarrativeActionExpiryValue { key = pair.Key, day = pair.Value }).ToList();
            snapshot.mounts = mounts.Select(m => new NarrativeActionMountValue { position = m.position, action = m.action, day = m.day, instanceVersion = m.instanceVersion }).ToList();
            snapshot.seenActionInstances = seenActionInstances.ToList();
            snapshot.pendingFocusPositions = new List<string>(pendingFocusPositions);
            snapshot.retired = retired.ToList();
            snapshot.executingAction = executingAction;
            snapshot.currentEvent = currentEventId;
            snapshot.pendingEvents = pendingEvents?.ToList() ?? new List<string>();
            snapshot.dueActions = dueActions.ToList();
            if (inventory is NarrativeMapInventory mapInventory) snapshot.items = mapInventory.Capture();
        }

        public void RestoreFrom(NarrativeActionStateSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (executing) throw new InvalidOperationException("Cannot restore action state during action execution.");
            if (snapshot.deliveryVersion < 0 || snapshot.deliveryVersion > 1) throw new FormatException("Unsupported action delivery snapshot version: " + snapshot.deliveryVersion);
            seenActionInstances.Clear();
            if (snapshot.seenActionInstances != null) foreach (string key in snapshot.seenActionInstances)
                if (!string.IsNullOrEmpty(key)) seenActionInstances.Add(key);
            pendingFocusPositions.Clear();
            if (snapshot.pendingFocusPositions != null) pendingFocusPositions.AddRange(snapshot.pendingFocusPositions.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct());
            mapContentSignatures.Clear();
            CurrentDay = Mathf.Max(1, snapshot.day);
            ActionPoints.ResetForDay(Mathf.Max(0, snapshot.actionPointMax));
            ActionPoints.Set(snapshot.actionPoints);
            counters.Clear(); dailyExecution.Clear(); expiryDays.Clear();
            foreach (var key in deliveries.Keys.ToArray()) deliveries[key] = new NarrativeActionDeliveryValue { actionKey = key };
            if (snapshot.deliveryVersion == 1 && snapshot.deliveries != null)
                foreach (var value in snapshot.deliveries)
                    if (value != null && value.actionKey != null && deliveries.ContainsKey(value.actionKey))
                    {
                        var restored = CloneDelivery(value);
                        restored.progress = Mathf.Clamp(restored.progress, 0, Get(value.actionKey).progressTarget);
                        deliveries[value.actionKey] = restored;
                    }
            if (snapshot.counters != null) foreach (var item in snapshot.counters) if (item != null && !string.IsNullOrEmpty(item.key)) counters[item.key] = Mathf.Max(0, item.value);
            if (snapshot.daily != null) foreach (var item in snapshot.daily) if (item != null && !string.IsNullOrEmpty(item.key)) dailyExecution[item.key] = item.day;
            // Legacy free-repeat counters are not evidence of a dated daily delivery.
            foreach (var key in deliveries.Keys) { counters.Remove(key); dailyExecution.Remove(key); }
            foreach (var value in deliveries.Values)
                if (value.lastSubmissionDay > 0) dailyExecution[value.actionKey] = value.lastSubmissionDay;
            if (snapshot.expiry != null) foreach (var item in snapshot.expiry) if (item != null && !string.IsNullOrEmpty(item.key)) expiryDays[item.key] = item.day;
            if (snapshot.mounts != null) { mounts.Clear(); mounts.AddRange(snapshot.mounts.Where(m => m != null && Get(m.action) != null)); }
            retired.Clear();
            if (snapshot.retired != null) foreach (string key in snapshot.retired)
                if (snapshot.deliveryVersion > 0 || !deliveries.ContainsKey(key)) retired.Add(key);
            foreach (var mount in mounts)
            {
                var action = Get(mount.action);
                if ((action.IsTimed || action.type == NarrativeActionType.Future) && !expiryDays.ContainsKey(action.actionKey))
                    expiryDays[action.actionKey] = checked(mount.day + action.durationDays);
            }
            if (inventory is NarrativeMapInventory mapInventory) mapInventory.Restore(snapshot.items);
            dueActions.Clear();
            if (snapshot.dueActions != null) foreach (string key in snapshot.dueActions) if (Get(key) != null) dueActions.Enqueue(key);
            if (!narrative.IsEnded && !string.IsNullOrEmpty(snapshot.executingAction) && Get(snapshot.executingAction) != null)
            {
                executing = true; executingAction = snapshot.executingAction; currentEventId = snapshot.currentEvent;
                pendingEvents = new Queue<string>(snapshot.pendingEvents ?? new List<string>());
                OpenOperation();
            }
            narrative.NotifyConditionsChanged();
            StateChanged?.Invoke();
        }

        private static NarrativeActionDeliveryValue CloneDelivery(NarrativeActionDeliveryValue value)
            => new NarrativeActionDeliveryValue { actionKey = value.actionKey, progress = value.progress,
                lastSubmissionDay = value.lastSubmissionDay, completed = value.completed,
                triggeredMilestones = new List<string>(value.triggeredMilestones ?? new List<string>()) };

        public void ResumePendingExecution()
        {
            if (narrative.IsEnded) return;
            if (executing && string.IsNullOrEmpty(currentEventId)) PlayNextEvent();
            narrative.ResumePendingEvents();
        }

        public string CaptureJson()
        {
            var snapshot = new NarrativeActionStateSnapshot(); CaptureInto(snapshot); return JsonUtility.ToJson(snapshot);
        }

        public void RestoreJson(string json)
        {
            if (!string.IsNullOrWhiteSpace(json)) RestoreFrom(JsonUtility.FromJson<NarrativeActionStateSnapshot>(json));
        }

        private NarrativeActionCheck Check(NarrativeActionRequirement requirement)
        {
            bool satisfied; string current = string.Empty; string required = requirement.ToString();
            switch (requirement.type)
            {
                case NarrativeActionRequirementType.Item:
                    int count = inventory.GetCount(requirement.key); satisfied = count >= requirement.value; current = count.ToString(); break;
                case NarrativeActionRequirementType.ActionPoint:
                    satisfied = ActionPoints.Current >= requirement.value; current = ActionPoints.Current.ToString(); break;
                default: throw new FormatException("Read-only action requirements must use Condition rows: " + requirement);
            }
            return new NarrativeActionCheck { satisfied = satisfied, requirement = requirement.ToString(), currentValue = current, requiredValue = required, messageKey = satisfied ? "action.requirement.ok" : "action.requirement.missing" };
        }

        private bool ConsumeRequirements(NarrativeActionDefinition action)
        {
            long pointTotal = action.parsedRequirements.Where(r => r.type == NarrativeActionRequirementType.ActionPoint).Sum(r => (long)r.value);
            if (pointTotal > ActionPoints.Current) return false;
            var costs = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var group in action.parsedRequirements.Where(r => r.type == NarrativeActionRequirementType.Item).GroupBy(r => r.key))
            {
                long total = group.Sum(r => (long)r.value);
                if (total > inventory.GetCount(group.Key)) return false;
                costs.Add(group.Key, (int)total);
            }
            if (inventory is NarrativeMapInventory map)
            {
                if (!map.TryConsume(costs)) return false;
            }
            else
            {
                var consumed = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var cost in costs)
                {
                    if (!inventory.TryConsume(cost.Key, cost.Value))
                    {
                        foreach (var prior in consumed)
                            if (!inventory.TryAdd(prior.Key, prior.Value)) throw new InvalidOperationException("Cannot restore rejected action cost: " + prior.Key);
                        return false;
                    }
                    consumed.Add(cost.Key, cost.Value);
                }
            }
            return ActionPoints.TryConsume((int)pointTotal);
        }

        private void RecordExecution(NarrativeActionDefinition action)
        {
            if (action.IsDelivery)
            {
                var value = deliveries[action.actionKey];
                value.progress = Math.Min(action.progressTarget, value.progress + 1);
                value.lastSubmissionDay = CurrentDay;
                value.completed = value.progress >= action.progressTarget;
            }
            else
            {
                if (action.type == NarrativeActionType.Once && !action.IsProgress) counters[action.actionKey] = 1;
                if (action.IsProgress) counters[action.actionKey] = Math.Min(action.progressTarget, GetProgress(action.actionKey) + 1);
            }
            dailyExecution[action.actionKey] = CurrentDay;
        }

        private void PlayDueAction()
        {
            if (executing || narrative.IsEnded || dueActions.Count == 0) return;
            var action = Get(dueActions.Dequeue());
            executing = true;
            executingAction = action.actionKey;
            OpenOperation();
            pendingEvents = new Queue<string>(action.resultEvents ?? new List<string>());
            PlayNextEvent();
        }

        private void PlayNextEvent()
        {
            if (narrative.IsEnded) return;
            if (pendingEvents.Count == 0)
            {
                Finish(true); return;
            }
            string eventId = pendingEvents.Dequeue();
            currentEventId = eventId;
            if (!narrative.QueueMapActionEvent(eventId))
            {
                // Retain the committed action and refused event for recovery; never declare it completed.
                pendingEvents = new Queue<string>(new[] { eventId }.Concat(pendingEvents));
                currentEventId = null;
                Debug.LogError("[NCS][Action] Cannot queue event " + eventId + " for action " + executingAction);
                ReportFailure();
            }
        }

        private bool DefersCompletion(NarrativeActionDefinition action)
            => action != null && (action.type == NarrativeActionType.Once || narrative.UsesDailyPool(action)) && !action.IsProgress && !action.IsDelivery;

        private void ReportFailure()
        {
            if (refundablePoolPoints > 0) ActionPoints.Add(refundablePoolPoints);
            refundablePoolPoints = 0;
            var action = Get(executingAction);
            var callback = pendingCompletion; pendingCompletion = null;
            if (DefersCompletion(action))
            {
                // NCS owns cancelling its presentation/epoch. Release only this action's operation.
                executing = false; executingAction = null; pendingEvents = null; currentEventId = null;
                CloseOperation();
            }
            ActionFinished?.Invoke(action, false);
            StateChanged?.Invoke();
            callback?.Invoke(false);
        }

        private void OnEventCompleted(string eventId)
        {
            narrative.RecordDailyPoolCompletion(eventId);
            narrative.RefreshDailyAvailability(CurrentDay);
            if (!executing || pendingEvents == null || eventId != currentEventId) return;
            currentEventId = null;
            PlayNextEvent();
        }

        private void Finish(bool success)
        {
            NarrativeActionDefinition action = Get(executingAction);
            if (!success && refundablePoolPoints > 0) ActionPoints.Add(refundablePoolPoints);
            refundablePoolPoints = 0;
            if (success && !narrative.IsEnded && DefersCompletion(action)) RecordExecution(action);
            narrative.NotifyConditionsChanged();
            CloseOperation();
            narrative.RunAtSafeBoundary(() =>
            {
                executing = false; executingAction = null; pendingEvents = null; currentEventId = null;
                Action<bool> callback = pendingCompletion; pendingCompletion = null;
                if (success && !narrative.IsEnded && action != null && action.focusLocationOnComplete)
                    foreach (string position in mounts.Where(m => m.action == action.actionKey).Select(m => m.position).Distinct())
                        if (!pendingFocusPositions.Contains(position)) pendingFocusPositions.Add(position);
                ActionFinished?.Invoke(action, success && !narrative.IsEnded);
                StateChanged?.Invoke();
                callback?.Invoke(success && !narrative.IsEnded);
                PlayDueAction();
            });
        }

        private void OpenOperation()
        {
            if (operationOpen) return;
            operationOpen = true;
            narrative.BeginOperation();
        }
        private void CloseOperation()
        {
            if (!operationOpen) return;
            operationOpen = false;
            narrative.EndOperation();
        }

        private static void Validate(NarrativeActionDefinition action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.actionKey)) throw new FormatException("Action key is required.");
            if (action.durationDays < 0 || action.progressTarget < 0) throw new FormatException("Action days and progress cannot be negative: " + action.actionKey);
            if (action.endDay && action.mustExecute) throw new FormatException("EndDay cannot itself be crucial: " + action.actionKey);
            if (action.parsedRequirements != null && action.parsedRequirements.Count > 6) throw new FormatException("An action cannot have more than 6 requirements: " + action.actionKey);
            if (action.type == NarrativeActionType.Future && (action.durationDays <= 0 || action.progressTarget > 0)) throw new FormatException("Future actions require positive days and cannot combine with progress: " + action.actionKey);
            if (action.endDay && action.type != NarrativeActionType.Daily) throw new FormatException("EndDay must be Daily: " + action.actionKey);
            if (action.type == NarrativeActionType.Progress && action.progressTarget <= 0) throw new FormatException("Progress action requires a positive target: " + action.actionKey);
            if (action.type == NarrativeActionType.Timed && action.durationDays <= 0) throw new FormatException("Timed action requires positive duration: " + action.actionKey);
            if (!Enum.IsDefined(typeof(NarrativeDeliveryMode), action.deliveryMode)) throw new FormatException("Invalid delivery mode: " + action.actionKey);
            if (action.IsDelivery && (action.progressTarget <= 0 || action.type == NarrativeActionType.Future || action.endDay))
                throw new FormatException("Delivery requires a positive target and cannot be Future/EndDay: " + action.actionKey);
            var milestoneIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var milestone in action.milestones ?? new List<NarrativeActionMilestoneDefinition>())
                if (milestone == null || !action.IsDelivery || string.IsNullOrWhiteSpace(milestone.milestoneId)
                    || !milestoneIds.Add(milestone.milestoneId) || string.IsNullOrWhiteSpace(milestone.eventId)
                    || milestone.actionKey != action.actionKey || milestone.atProgress < 1 || milestone.atProgress >= action.progressTarget)
                    throw new FormatException("Invalid or duplicate delivery milestone: " + action.actionKey);
        }
    }
}
