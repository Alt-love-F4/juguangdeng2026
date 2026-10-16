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
        private readonly Dictionary<string, NarrativeActionReceipt> receipts = new Dictionary<string, NarrativeActionReceipt>(StringComparer.Ordinal);
        private long operationSequence;
        private bool committing;
        private bool operationOpen;
        private bool executing;
        private string executingAction;
        private Queue<string> pendingEvents;
        private string currentEventId;
        private string expiryAction;
        private string currentExpiryEvent;
        private Queue<string> pendingExpiryEvents;
        private bool rootCostCommitted;
        private bool submissionRecorded;
        private bool successResultsCommitted;
        private string activeOperationId;
        private Action<bool> pendingCompletion;

        public NarrativeActionPoints ActionPoints { get; }
        public int CurrentDay { get; private set; } = 1;
        public bool IsExecuting => executing && (operationOpen || (pendingEvents?.Count == 0 && successResultsCommitted));
        public bool HasPendingExecution(string actionKey) => executing && executingAction == actionKey && !operationOpen
            && string.IsNullOrEmpty(currentEventId) && pendingEvents != null && !successResultsCommitted;
        private NarrativeActionOperationState lastOperationState;
        public string LastFailure { get; private set; }
        public NarrativeActionOperationState OperationState => committing ? NarrativeActionOperationState.Committing
            : IsExecuting ? NarrativeActionOperationState.Presenting : lastOperationState;
        public string ActiveOperationId => activeOperationId;
        public IReadOnlyCollection<NarrativeActionReceipt> Receipts => receipts.Values;
        public bool CanCaptureSnapshot => !committing && !(executing && operationOpen && DefersCompletion(Get(executingAction)));
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
                if (action != null && action.endDay && action.executionKind == NarrativeActionExecutionKind.Event)
                    action.executionKind = NarrativeActionExecutionKind.EndDay;
                Validate(action);
                if (!definitions.TryAdd(action.actionKey, action)) throw new FormatException("Duplicate action key: " + action.actionKey);
                if (action.parsedRequirements == null || action.parsedRequirements.Count == 0)
                    action.parsedRequirements = NarrativeActionRequirementParser.Parse(action.requirements);
                foreach (var cost in action.parsedRequirements)
                    if (cost == null || (cost.type != NarrativeActionRequirementType.Item && cost.type != NarrativeActionRequirementType.ActionPoint && cost.type != NarrativeActionRequirementType.Money)
                        || cost.value <= 0 || (cost.type == NarrativeActionRequirementType.Item && string.IsNullOrWhiteSpace(cost.key)))
                        throw new FormatException("Action requires positive Item/ActionPoint/Money costs; move read-only checks to Condition: " + action.actionKey);
                if (action.executionKind == NarrativeActionExecutionKind.Navigate
                    && (string.IsNullOrWhiteSpace(action.navigationKey) || action.parsedRequirements.Count > 0 || action.resultEvents.Count > 0 || !string.IsNullOrWhiteSpace(action.successResultOwnerId)))
                    throw new FormatException("Navigate action requires a navigationKey and cannot have costs or results: " + action.actionKey);
                if (action.executionKind != NarrativeActionExecutionKind.Navigate && !string.IsNullOrWhiteSpace(action.navigationKey))
                    throw new FormatException("Only Navigate actions may specify navigationKey: " + action.actionKey);
                if (action.endDay && (action.executionKind != NarrativeActionExecutionKind.EndDay || action.IsTimed || action.IsProgress || action.parsedRequirements.Count > 0))
                    throw new FormatException("EndDay actions cannot have cost, timer or progress modifiers: " + action.actionKey);
                if (action.IsDelivery) deliveries.Add(action.actionKey, new NarrativeActionDeliveryValue { actionKey = action.actionKey });
            }
            foreach (var action in definitions.Values)
                if (action.type == NarrativeActionType.Future && (string.IsNullOrEmpty(action.nextActionKey) || Get(action.nextActionKey) == null || action.nextActionKey == action.actionKey))
                    throw new FormatException("Future action requires a different configured nextActionKey: " + action.actionKey);
            ActionPoints = new NarrativeActionPoints(actionPointMax);
            StateChanged += () => RefreshMapContent();
            narrative.State.ConditionsChanged += () => { if (!committing) RefreshMapContent(); };
            narrative.BarChanged += (_, __, ___) => { if (!committing) { RefreshMapContent(); StateChanged?.Invoke(); } };
            ActionPoints.Changed += (_, __) => { if (!committing) StateChanged?.Invoke(); };
            SetInventory(inventory);
            narrative.ActionProgressReader = key => Get(key) == null ? (int?)null : GetProgress(key);
            narrative.ActionCompletedReader = key => Get(key) == null ? (bool?)null : IsCompleted(key);
            narrative.ActionRootCommit = eventId =>
            {
                var action = Get(executingAction);
                if (!executing || eventId != currentEventId || action == null) return null;
                bool hasFinalResults = pendingEvents?.Count == 0 && IsFinalSubmission(action)
                    && !successResultsCommitted && !string.IsNullOrWhiteSpace(action.successResultOwnerId);
                if ((rootCostCommitted || !StagesRootCosts(action)) && !hasFinalResults) return null;
                return new NarrativeActionRootCommit
                {
                    costs = rootCostCommitted || !StagesRootCosts(action) ? Array.Empty<NarrativeActionRequirement>() : action.parsedRequirements,
                    resultOwner = hasFinalResults ? action.successResultOwnerId : null,
                    points = ActionPoints,
                    canCommit = () => !narrative.IsEnded && IsVisible(action.actionKey)
                        && narrative.EvaluateConditions("action", action.actionKey) && narrative.EvaluateConditions("actionExecute", action.actionKey),
                    committed = () => { rootCostCommitted = true; if (hasFinalResults) successResultsCommitted = true; }
                };
            };
            narrative.EventCompleted += OnEventCompleted;
            narrative.EventFailed += (eventId, error) =>
            {
                if (currentExpiryEvent == eventId) ReportExpiryFailure();
                if (executing && currentEventId == eventId) { LastFailure = error; ReportFailure(); }
            };
        }

        public void SetInventory(INarrativeActionInventory value)
        {
            inventory = value ?? new EmptyNarrativeActionInventory();
            if (inventory is NarrativeMapInventory mapInventory) narrative.Inventory = mapInventory;
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
            bool changed = false;
            foreach (var pair in deliveries)
            {
                var value = pair.Value;
                if (Get(pair.Key).deliveryMode == NarrativeDeliveryMode.Consecutive && !value.completed
                    && value.progress > 0 && value.lastSubmissionDay > 0 && (long)day - value.lastSubmissionDay > 1)
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
            var values = GetMountedActions(position).Where(a => narrative.EvaluateConditions("action", a.actionKey))
                .OrderBy(a => a.sortOrder).ThenBy(a => mounts.FindIndex(m => m.position == position && m.action == a.actionKey)).ToList();
            return values.Any(a => a.replacesLocationActions && !IsCompleted(a.actionKey))
                ? values.Where(a => a.replacesLocationActions && !IsCompleted(a.actionKey)) : values;
        }

        public bool ReplacesLocationActions(string position)
            => GetDisplayableActions(position).Any(a => a.replacesLocationActions && !IsCompleted(a.actionKey));

        public string GetActionInstanceKey(string position, string actionKey)
        {
            var mount = mounts.FirstOrDefault(m => m.position == position && m.action == actionKey);
            if (mount == null) return null;
            var action = Get(actionKey);
            string version = string.IsNullOrEmpty(mount.instanceVersion) ? "mount:" + mount.day : mount.instanceVersion;
            if (action.type == NarrativeActionType.Daily || action.IsDelivery) version += "/day:" + CurrentDay;
            return Uri.EscapeDataString(position) + "|" + Uri.EscapeDataString(actionKey) + "|" + Uri.EscapeDataString(version);
        }

        private bool IsUnseenCandidate(NarrativeActionDefinition action)
            => action.notifyNew && action.executionKind != NarrativeActionExecutionKind.Navigate
                && !IsCompleted(action.actionKey) && !(dailyExecution.TryGetValue(action.actionKey, out int day) && day == CurrentDay
                && (action.type == NarrativeActionType.Daily || action.IsDelivery));

        public bool HasUnseenActions(string position)
            => GetDisplayableActions(position).Any(a => IsUnseenCandidate(a) && !seenActionInstances.Contains(GetActionInstanceKey(position, a.actionKey)));

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
                    + ":" + IsUnseenCandidate(a) + ":" + seenActionInstances.Contains(GetActionInstanceKey(position, a.actionKey))));
                if (mapContentSignatures.TryGetValue(position, out string previous) && previous == signature) continue;
                mapContentSignatures[position] = signature;
                MapContentChanged?.Invoke(position);
            }
        }

        public IEnumerable<string> GetBlockingPositions()
            => mounts.Select(m => m.position).Distinct().Where(position => GetDisplayableActions(position).Any(IsBlockingAction));

        private bool IsBlockingAction(NarrativeActionDefinition action)
            => action.mustExecute && !IsCompleted(action.actionKey) && action.type != NarrativeActionType.Future
                && (HasPendingExecution(action.actionKey) || !dailyExecution.TryGetValue(action.actionKey, out int day) || day != CurrentDay);

        public string PendingFocusPosition => pendingFocusPositions.Count > 0 ? pendingFocusPositions[0] : null;
        public bool ConsumePendingFocus(string position) => pendingFocusPositions.Remove(position);

        public IEnumerable<string> GetMapPositions() => mounts.Select(m => m.position).Distinct();

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
            if (key != null && deliveries.TryGetValue(key, out var delivery))
                return delivery.completed && !(executing && executingAction == key);
            var action = Get(key);
            return action != null && (action.IsProgress ? GetProgress(key) >= action.progressTarget && !(executing && executingAction == key)
                : action.type == NarrativeActionType.Once && GetProgress(key) > 0);
        }
        public bool HasSubmittedToday(string key) => key != null && deliveries.TryGetValue(key, out var value) && value.lastSubmissionDay == CurrentDay;
        public bool HasBlockingActions()
        {
            return GetBlockingPositions().Any();
        }

        public bool IsAvailable(string actionKey, out List<NarrativeActionCheck> checks)
        {
            checks = new List<NarrativeActionCheck>();
            NarrativeActionDefinition action = Get(actionKey);
            if (action == null || !narrative.ValuesReady) return false;
            if (HasPendingExecution(actionKey)) return !narrative.IsEnded && !narrative.IsBusy;
            bool eligible = !executing && !narrative.IsEnded && !narrative.IsBusy && IsVisible(actionKey) && !IsCompleted(actionKey) && action.type != NarrativeActionType.Future;
            if (action.IsDelivery && HasSubmittedToday(actionKey)) eligible = false;
            if (action.endDay && HasBlockingActions()) eligible = false;
            if (action.type == NarrativeActionType.Daily && dailyExecution.TryGetValue(actionKey, out int day) && day == CurrentDay) eligible = false;
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
            if (action.parsedRequirements.Where(r => r.type == NarrativeActionRequirementType.ActionPoint).Sum(r => (long)r.value) > ActionPoints.Current)
                for (int i = 0; i < action.parsedRequirements.Count; i++)
                    if (action.parsedRequirements[i].type == NarrativeActionRequirementType.ActionPoint) checks[i].satisfied = false;
            long moneyTotal = action.parsedRequirements.Where(r => r.type == NarrativeActionRequirementType.Money).Sum(r => (long)r.value);
            if (moneyTotal > 0 && (narrative.MoneyAccount == null || !narrative.MoneyAccount.TryRead(out int balance) || moneyTotal > balance))
                for (int i = 0; i < action.parsedRequirements.Count; i++)
                    if (action.parsedRequirements[i].type == NarrativeActionRequirementType.Money) checks[i].satisfied = false;
            bool conditionsMet = narrative.EvaluateConditions("action", actionKey);
            bool executionConditionsMet = narrative.EvaluateConditions("actionExecute", actionKey);
            if (!executionConditionsMet)
            {
                var diagnostics = narrative.ExplainConditions("actionExecute", actionKey);
                if (diagnostics.Count == 0) checks.Add(new NarrativeActionCheck { satisfied = false, requirement = "Condition(actionExecute," + actionKey + ")", messageKey = "action.condition.missing" });
                else foreach (var diagnostic in diagnostics)
                    checks.Add(new NarrativeActionCheck { satisfied = diagnostic.Passed, requirement = diagnostic.Target, currentValue = diagnostic.Current, requiredValue = diagnostic.Required, messageKey = diagnostic.Passed ? "action.requirement.ok" : "action.condition.missing" });
            }
            return eligible && conditionsMet && executionConditionsMet && checks.All(check => check.satisfied);
        }

        public bool Execute(string actionKey, Action<bool> completed = null, string operationId = null)
        {
            if (!string.IsNullOrWhiteSpace(operationId) && receipts.TryGetValue(operationId, out var receipt))
            {
                if (receipt.actionKey != actionKey) return false;
                if (!receipt.submissionOnly || !HasPendingExecution(actionKey))
                { completed?.Invoke(true); return true; }
            }
            if (executing && executingAction == actionKey && !operationOpen && string.IsNullOrEmpty(currentEventId) && pendingEvents != null)
            {
                if (narrative.IsEnded || narrative.IsBusy || (!string.IsNullOrWhiteSpace(operationId) && operationId != activeOperationId)) return false;
                pendingCompletion = completed;
                OpenOperation();
                PlayNextEvent();
                return true;
            }
            lastOperationState = NarrativeActionOperationState.Checking;
            LastFailure = null;
            if (!IsAvailable(actionKey, out _)) { lastOperationState = NarrativeActionOperationState.Failed; LastFailure = "行动暂不可执行"; return false; }
            NarrativeActionDefinition action = definitions[actionKey];
            if (action.executionKind == NarrativeActionExecutionKind.Navigate) return false;
            if (action.executionKind == NarrativeActionExecutionKind.Exchange)
                return ExecuteExchange(action, completed, operationId);
            executing = true;
            committing = true;
            executingAction = actionKey;
            bool staged = StagesRootCosts(action) && (!action.IsProgress || GetProgress(actionKey) + 1 >= action.progressTarget);
            rootCostCommitted = !staged;
            activeOperationId = CreateOperationId(action, operationId);
            submissionRecorded = false;
            successResultsCommitted = false;
            pendingCompletion = completed;
            OpenOperation();
            try
            {
                {
                    if (!staged && !CommitSubmission(action))
                    {
                        executing = false; executingAction = null;
                        CloseOperation();
                        return false;
                    }
                    if (staged) PrepareSubmissionEvents(action, true);
                }
            }
            catch
            {
                executing = false; executingAction = null; pendingEvents = null; pendingCompletion = null;
                CloseOperation();
                throw;
            }
            finally { committing = false; }
            narrative.NotifyConditionsChanged();
            StateChanged?.Invoke();
            ActionStarted?.Invoke(action);
            PlayNextEvent();
            return true;
        }

        private bool ExecuteExchange(NarrativeActionDefinition action, Action<bool> completed, string operationId)
        {
            // No event fallback: unsupported result owners/providers must fail before payment.
            if (string.IsNullOrWhiteSpace(action.successResultOwnerId) || action.IsDelivery
                || (action.resultEvents != null && action.resultEvents.Count > 0) || !(inventory is NarrativeMapInventory)
                || !ReferenceEquals(narrative.Inventory, inventory)) return false;
            string id = CreateOperationId(action, operationId);
            executing = true; executingAction = action.actionKey; committing = true;
            OpenOperation();
            bool success;
            try
            {
                success = narrative.TryCommitActionBatch(action.successResultOwnerId, action.parsedRequirements, ActionPoints, () =>
                {
                    RecordExecution(action);
                    receipts.Add(id, new NarrativeActionReceipt { operationId = id, actionKey = action.actionKey, day = CurrentDay });
                }, out _);
            }
            finally
            {
                committing = false;
                CloseOperation();
                executing = false; executingAction = null;
            }
            if (!success) { lastOperationState = NarrativeActionOperationState.Failed; LastFailure = "交易提交失败，请检查需求后重试"; return false; }
            lastOperationState = NarrativeActionOperationState.Committed;
            ActionStarted?.Invoke(action);
            StateChanged?.Invoke();
            ActionFinished?.Invoke(action, true);
            completed?.Invoke(true);
            return true;
        }

        private string CreateOperationId(NarrativeActionDefinition action, string operationId)
        {
            if (!string.IsNullOrWhiteSpace(operationId)) return operationId;
            do { operationId = action.actionKey + "/day:" + CurrentDay + "/operation:" + checked(++operationSequence); }
            while (receipts.ContainsKey(operationId));
            return operationId;
        }

        private static bool StagesRootCosts(NarrativeActionDefinition action)
            => action != null && action.executionKind == NarrativeActionExecutionKind.Event && !action.IsDelivery
                && action.resultEvents != null && action.resultEvents.Count > 0;

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
            snapshot.rootCostCommitted = rootCostCommitted;
            snapshot.submissionRecorded = submissionRecorded;
            snapshot.successResultsCommitted = successResultsCommitted;
            snapshot.activeOperationId = activeOperationId;
            snapshot.currentEvent = currentEventId;
            snapshot.pendingEvents = pendingEvents?.ToList() ?? new List<string>();
            snapshot.dueActions = dueActions.ToList();
            snapshot.expiryAction = expiryAction;
            snapshot.currentExpiryEvent = currentExpiryEvent;
            snapshot.pendingExpiryEvents = pendingExpiryEvents?.ToList() ?? new List<string>();
            snapshot.operationSequence = operationSequence;
            snapshot.receipts = receipts.Values.Select(r => new NarrativeActionReceipt { operationId = r.operationId, actionKey = r.actionKey, day = r.day, submissionOnly = r.submissionOnly }).ToList();
            if (inventory is NarrativeMapInventory mapInventory) snapshot.items = mapInventory.Capture();
        }

        public void RestoreFrom(NarrativeActionStateSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (executing) throw new InvalidOperationException("Cannot restore action state during action execution.");
            if (snapshot.deliveryVersion < 0 || snapshot.deliveryVersion > 1) throw new FormatException("Unsupported action delivery snapshot version: " + snapshot.deliveryVersion);
            rootCostCommitted = false; submissionRecorded = false; successResultsCommitted = false; activeOperationId = null;
            lastOperationState = NarrativeActionOperationState.Idle; LastFailure = null;
            operationSequence = Math.Max(0, snapshot.operationSequence);
            receipts.Clear();
            foreach (var receipt in snapshot.receipts ?? new List<NarrativeActionReceipt>())
                if (receipt != null && !string.IsNullOrWhiteSpace(receipt.operationId) && Get(receipt.actionKey) != null)
                    receipts[receipt.operationId] = new NarrativeActionReceipt { operationId = receipt.operationId, actionKey = receipt.actionKey, day = receipt.day, submissionOnly = receipt.submissionOnly };
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
            if (snapshot.counters != null) foreach (var item in snapshot.counters) if (item != null && IsRestorableActionKey(item.key)) counters[item.key] = Mathf.Max(0, item.value);
            if (snapshot.daily != null) foreach (var item in snapshot.daily) if (item != null && IsRestorableActionKey(item.key)) dailyExecution[item.key] = item.day;
            if (snapshot.deliveryVersion == 0)
                foreach (var pair in deliveries)
                {
                    var value = pair.Value;
                    if (counters.TryGetValue(pair.Key, out int legacyProgress)) value.progress = Mathf.Clamp(legacyProgress, 0, Get(pair.Key).progressTarget);
                    if (dailyExecution.TryGetValue(pair.Key, out int legacyDay)) value.lastSubmissionDay = legacyDay;
                    value.completed = value.progress >= Get(pair.Key).progressTarget;
                    if (value.progress > 0 && value.lastSubmissionDay == 0)
                        Debug.LogWarning("[NCS][Action] Preserved undated legacy delivery progress: " + pair.Key + "; consecutive reset begins after the next dated submission.");
                }
            foreach (var key in deliveries.Keys) { counters.Remove(key); dailyExecution.Remove(key); }
            foreach (var value in deliveries.Values)
                if (value.lastSubmissionDay > 0) dailyExecution[value.actionKey] = value.lastSubmissionDay;
            if (snapshot.expiry != null) foreach (var item in snapshot.expiry) if (item != null && IsRestorableActionKey(item.key)) expiryDays[item.key] = item.day;
            if (snapshot.mounts != null) { mounts.Clear(); mounts.AddRange(snapshot.mounts.Where(m => m != null && Get(m.action) != null)); }
            retired.Clear();
            if (snapshot.retired != null) foreach (string key in snapshot.retired)
                if (Get(key) != null) retired.Add(key);
            foreach (var mount in mounts)
            {
                var action = Get(mount.action);
                if ((action.IsTimed || action.type == NarrativeActionType.Future) && !expiryDays.ContainsKey(action.actionKey))
                    expiryDays[action.actionKey] = checked(mount.day + action.durationDays);
            }
            if (inventory is NarrativeMapInventory mapInventory) mapInventory.Restore(snapshot.items);
            dueActions.Clear();
            if (snapshot.dueActions != null) foreach (string key in snapshot.dueActions) if (Get(key) != null) dueActions.Enqueue(key);
            expiryAction = Get(snapshot.expiryAction) != null && dueActions.Count > 0 && dueActions.Peek() == snapshot.expiryAction ? snapshot.expiryAction : null;
            currentExpiryEvent = expiryAction == null ? null : snapshot.currentExpiryEvent;
            pendingExpiryEvents = expiryAction == null ? null : new Queue<string>(snapshot.pendingExpiryEvents ?? new List<string>());
            if (!narrative.IsEnded && !string.IsNullOrEmpty(snapshot.executingAction) && Get(snapshot.executingAction) != null)
            {
                executing = true; executingAction = snapshot.executingAction; currentEventId = snapshot.currentEvent;
                activeOperationId = snapshot.activeOperationId;
                // Older snapshots charged costs before presentation and have no operation token.
                rootCostCommitted = snapshot.rootCostCommitted || string.IsNullOrEmpty(activeOperationId);
                submissionRecorded = snapshot.submissionRecorded || (!StagesRootCosts(Get(executingAction)) && !DefersCompletion(Get(executingAction)));
                successResultsCommitted = snapshot.successResultsCommitted;
                pendingEvents = new Queue<string>(snapshot.pendingEvents ?? new List<string>());
                if (!string.IsNullOrEmpty(currentEventId)) OpenOperation();
            }
            narrative.NotifyConditionsChanged();
            StateChanged?.Invoke();
        }

        private bool IsRestorableActionKey(string key)
        {
            if (Get(key) != null) return true;
            Debug.LogWarning("[NCS][Action] Ignored unknown saved action key: " + (key ?? "<null>"));
            return false;
        }

        private static NarrativeActionDeliveryValue CloneDelivery(NarrativeActionDeliveryValue value)
            => new NarrativeActionDeliveryValue { actionKey = value.actionKey, progress = value.progress,
                lastSubmissionDay = value.lastSubmissionDay, completed = value.completed,
                triggeredMilestones = new List<string>(value.triggeredMilestones ?? new List<string>()) };

        public void ResumePendingExecution()
        {
            if (narrative.IsEnded) return;
            if (executing && string.IsNullOrEmpty(currentEventId)) { OpenOperation(); PlayNextEvent(); }
            if (expiryAction != null && string.IsNullOrEmpty(currentExpiryEvent)) PlayNextExpiryEvent();
            else if (expiryAction == null) PlayDueAction();
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
                case NarrativeActionRequirementType.Money:
                    int balance = 0;
                    bool ready = narrative.MoneyAccount != null && narrative.MoneyAccount.TryRead(out balance);
                    current = ready ? balance.ToString() : "不可用"; satisfied = ready && balance >= requirement.value; break;
                default: throw new FormatException("Read-only action requirements must use Condition rows: " + requirement);
            }
            return new NarrativeActionCheck { satisfied = satisfied, requirement = requirement.ToString(), currentValue = current, requiredValue = required, messageKey = satisfied ? "action.requirement.ok" : "action.requirement.missing" };
        }

        private bool IsFinalSubmission(NarrativeActionDefinition action)
            => action != null && (!action.IsProgress || GetProgress(action.actionKey) + (submissionRecorded ? 0 : 1) >= action.progressTarget);

        private void PrepareSubmissionEvents(NarrativeActionDefinition action, bool staged)
        {
            pendingEvents = new Queue<string>();
            if (action.IsDelivery)
            {
                var delivery = deliveries[action.actionKey];
                foreach (var milestone in (action.milestones ?? new List<NarrativeActionMilestoneDefinition>())
                    .OrderBy(m => m.seq).ThenBy(m => m.milestoneId, StringComparer.Ordinal))
                    if (milestone.atProgress == delivery.progress && !delivery.triggeredMilestones.Contains(milestone.milestoneId))
                    {
                        delivery.triggeredMilestones.Add(milestone.milestoneId);
                        pendingEvents.Enqueue(milestone.eventId);
                    }
            }
            if (staged || !action.IsProgress || GetProgress(action.actionKey) >= action.progressTarget)
                foreach (string eventId in action.resultEvents ?? new List<string>()) pendingEvents.Enqueue(eventId);
        }

        private bool CommitSubmission(NarrativeActionDefinition action)
        {
            Action record = () =>
            {
                rootCostCommitted = true;
                if (!DefersCompletion(action)) { RecordExecution(action); submissionRecorded = true; }
                receipts[activeOperationId] = new NarrativeActionReceipt { operationId = activeOperationId,
                    actionKey = action.actionKey, day = CurrentDay, submissionOnly = true };
                PrepareSubmissionEvents(action, false);
            };
            if (inventory is NarrativeMapInventory && ReferenceEquals(narrative.Inventory, inventory))
            {
                bool ownerWithCosts = !action.IsDelivery && (action.resultEvents == null || action.resultEvents.Count == 0)
                    && IsFinalSubmission(action) && !string.IsNullOrWhiteSpace(action.successResultOwnerId);
                return narrative.TryCommitActionBatch(ownerWithCosts ? action.successResultOwnerId : null, action.parsedRequirements, ActionPoints,
                    () => { record(); if (ownerWithCosts) successResultsCommitted = true; }, out _);
            }
            if (!ConsumeRequirements(action)) return false;
            record();
            return true;
        }

        private bool ConsumeRequirements(NarrativeActionDefinition action)
        {
            int pointsBefore = ActionPoints.Current;
            long pointTotal = action.parsedRequirements.Where(r => r.type == NarrativeActionRequirementType.ActionPoint).Sum(r => (long)r.value);
            if (pointTotal > pointsBefore) return false;
            var costs = new Dictionary<string, int>(StringComparer.Ordinal);
            var countsBefore = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var group in action.parsedRequirements.Where(r => r.type == NarrativeActionRequirementType.Item).GroupBy(r => r.key))
            {
                long total = group.Sum(r => (long)r.value);
                int count = inventory.GetCount(group.Key);
                if (total > count) return false;
                costs.Add(group.Key, (int)total);
                countsBefore.Add(group.Key, count);
            }
            // Check every owner before touching stock, and keep the exact account used by preflight.
            var account = narrative.MoneyAccount;
            long moneyTotal = action.parsedRequirements.Where(r => r.type == NarrativeActionRequirementType.Money).Sum(r => (long)r.value);
            int moneyBefore = 0;
            if (moneyTotal > 0 && (moneyTotal > int.MaxValue || account == null || !account.TryRead(out moneyBefore) || moneyTotal > moneyBefore)) return false;
            bool succeeded = false, moneyCommitted = false;
            var touched = new Dictionary<string, int>(StringComparer.Ordinal);
            using ((account as INarrativeMoneyNotificationScope)?.DeferNotifications())
            using (ActionPoints.DeferNotifications())
            try
            {
                if (inventory is NarrativeMapInventory map)
                {
                    foreach (var pair in countsBefore) touched.Add(pair.Key, pair.Value);
                    if (!map.TryConsume(costs)) return false;
                }
                else
                {
                    foreach (var cost in costs)
                    {
                        // A custom provider may mutate even when it returns false or throws.
                        touched.Add(cost.Key, countsBefore[cost.Key]);
                        if (!inventory.TryConsume(cost.Key, cost.Value)) return false;
                    }
                }
                if (moneyTotal > 0)
                {
                    if (!account.TryCommit(moneyBefore, moneyBefore - (int)moneyTotal, out _)) return false;
                    moneyCommitted = true;
                }
                if (!ActionPoints.TryConsume((int)pointTotal)) return false;
                succeeded = true;
                return true;
            }
            finally
            {
                if (!succeeded)
                {
                    // Attempt every compensation even if another owner refuses restoration.
                    var failures = new List<Exception>();
                    try
                    {
                        if (moneyCommitted && !account.TryCommit(moneyBefore - (int)moneyTotal, moneyBefore, out string error))
                            throw new InvalidOperationException("Cannot restore rejected money cost: " + error);
                    }
                    catch (Exception error) { failures.Add(error); }
                    try { RestoreItemCounts(touched); } catch (Exception error) { failures.Add(error); }
                    try { ActionPoints.Set(pointsBefore); } catch (Exception error) { failures.Add(error); }
                    if (failures.Count > 0) throw new AggregateException("Cannot fully restore rejected action costs.", failures);
                }
            }
        }

        private void RestoreItemCounts(Dictionary<string, int> counts)
        {
            if (inventory is NarrativeMapInventory map)
            {
                if (!map.TryApplyCounts(counts)) throw new InvalidOperationException("Cannot restore rejected map inventory costs.");
                return;
            }
            var failures = new List<Exception>();
            foreach (var pair in counts)
            {
                try
                {
                    int difference = pair.Value - inventory.GetCount(pair.Key);
                    if ((difference > 0 && !inventory.TryAdd(pair.Key, difference))
                        || (difference < 0 && !inventory.TryConsume(pair.Key, -difference)))
                        throw new InvalidOperationException("Cannot restore rejected action cost: " + pair.Key);
                }
                catch (Exception error) { failures.Add(error); }
            }
            if (failures.Count > 0) throw new AggregateException("Inventory provider rejected cost restoration.", failures);
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
            if (expiryAction != null || narrative.IsEnded || dueActions.Count == 0) return;
            var action = Get(dueActions.Peek());
            expiryAction = action.actionKey;
            pendingExpiryEvents = new Queue<string>(string.Equals(action.onExpirePolicy, "RunEvents", StringComparison.OrdinalIgnoreCase)
                ? action.onExpireEvents ?? new List<string>() : new List<string>());
            PlayNextExpiryEvent();
        }

        private void PlayNextExpiryEvent()
        {
            if (narrative.IsEnded || expiryAction == null) return;
            if (pendingExpiryEvents.Count == 0)
            {
                var action = Get(expiryAction);
                if (dueActions.Count > 0 && dueActions.Peek() == expiryAction) dueActions.Dequeue();
                expiryAction = null; currentExpiryEvent = null; pendingExpiryEvents = null;
                ActionFinished?.Invoke(action, true);
                StateChanged?.Invoke();
                PlayDueAction();
                return;
            }
            string eventId = pendingExpiryEvents.Dequeue();
            currentExpiryEvent = eventId;
            // Expiry has no open action operation: a phase event must allow ProcessEntry to run.
            if (!narrative.QueueExpiredActionEvent(eventId))
            {
                Debug.LogError("[NCS][Action] Cannot queue expiry event " + eventId + " for action " + expiryAction);
                ReportExpiryFailure();
            }
        }

        private void ReportExpiryFailure()
        {
            if (expiryAction == null) return;
            if (!string.IsNullOrEmpty(currentExpiryEvent))
                pendingExpiryEvents = new Queue<string>(new[] { currentExpiryEvent }.Concat(pendingExpiryEvents ?? new Queue<string>()));
            currentExpiryEvent = null;
            ActionFinished?.Invoke(Get(expiryAction), false);
            StateChanged?.Invoke();
        }

        private void PlayNextEvent()
        {
            if (narrative.IsEnded) return;
            if (pendingEvents.Count == 0)
            {
                var action = Get(executingAction);
                if (!successResultsCommitted && IsFinalSubmission(action) && !string.IsNullOrWhiteSpace(action.successResultOwnerId))
                {
                    committing = true;
                    bool success;
                    try { success = narrative.TryCommitActionBatch(action.successResultOwnerId, Array.Empty<NarrativeActionRequirement>(), ActionPoints,
                        () => successResultsCommitted = true, out _); }
                    finally { committing = false; }
                    if (!success) { ReportFailure(); return; }
                }
                successResultsCommitted = true;
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

        private static bool DefersCompletion(NarrativeActionDefinition action)
            => action != null && action.type == NarrativeActionType.Once && !action.IsProgress && !action.IsDelivery;

        private void ReportFailure()
        {
            var action = Get(executingAction);
            lastOperationState = LastFailure?.StartsWith("presentation_cancelled:", StringComparison.Ordinal) == true
                ? NarrativeActionOperationState.Cancelled : NarrativeActionOperationState.Failed;
            if (string.IsNullOrEmpty(LastFailure)) LastFailure = "行动已暂停，可继续未完成行动";
            var callback = pendingCompletion; pendingCompletion = null;
            if (!string.IsNullOrEmpty(activeOperationId))
            {
                if (!string.IsNullOrEmpty(currentEventId))
                    pendingEvents = new Queue<string>(new[] { currentEventId }.Concat(pendingEvents ?? new Queue<string>()));
                currentEventId = null;
                CloseOperation();
            }
            else if (DefersCompletion(action))
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
            if (expiryAction != null && eventId == currentExpiryEvent)
            {
                currentExpiryEvent = null;
                PlayNextExpiryEvent();
            }
            if (!executing || pendingEvents == null || eventId != currentEventId) return;
            currentEventId = null;
            PlayNextEvent();
        }

        private void Finish(bool success)
        {
            NarrativeActionDefinition action = Get(executingAction);
            if (success && !narrative.IsEnded && !submissionRecorded) { RecordExecution(action); submissionRecorded = true; }
            if (success && !string.IsNullOrEmpty(activeOperationId))
                receipts[activeOperationId] = new NarrativeActionReceipt { operationId = activeOperationId, actionKey = action.actionKey, day = CurrentDay };
            narrative.NotifyConditionsChanged();
            CloseOperation();
            narrative.RunAtSafeBoundary(() =>
            {
                executing = false; executingAction = null; pendingEvents = null; currentEventId = null; activeOperationId = null;
                lastOperationState = success ? NarrativeActionOperationState.Committed : NarrativeActionOperationState.Failed;
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
            if (action.parsedRequirements != null && action.parsedRequirements.Select(r => r.type + ":" + (r.type == NarrativeActionRequirementType.Item ? r.key : string.Empty)).Distinct().Count() > 6)
                throw new FormatException("An action cannot have more than 6 requirements: " + action.actionKey);
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
