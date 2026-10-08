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
        private bool executing;
        private string executingAction;
        private Queue<string> pendingEvents;
        private string currentEventId;
        private Action<bool> pendingCompletion;

        public NarrativeActionPoints ActionPoints { get; }
        public int CurrentDay { get; private set; } = 1;
        public bool IsExecuting => executing;
        public event Action StateChanged;
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
                if (action.type != NarrativeActionType.Repeat && action.type != NarrativeActionType.Daily && action.type != NarrativeActionType.Once)
                    throw new NotSupportedException("Action type is reserved for the next action-system phase: " + action.type);
                if (!definitions.TryAdd(action.actionKey, action)) throw new FormatException("Duplicate action key: " + action.actionKey);
                if (action.parsedRequirements == null || action.parsedRequirements.Count == 0)
                    action.parsedRequirements = NarrativeActionRequirementParser.Parse(action.requirements);
            }
            ActionPoints = new NarrativeActionPoints(actionPointMax);
            ActionPoints.Changed += (_, __) => StateChanged?.Invoke();
            this.inventory = inventory ?? new EmptyNarrativeActionInventory();
            narrative.EventCompleted += OnEventCompleted;
        }

        public void SetInventory(INarrativeActionInventory value) => inventory = value ?? new EmptyNarrativeActionInventory();
        public IEnumerable<NarrativeActionDefinition> Actions => definitions.Values;
        public NarrativeActionDefinition Get(string actionKey) => actionKey != null && definitions.TryGetValue(actionKey, out var action) ? action : null;

        public void BeginDay(int day, int actionPointMax = -1)
        {
            if (executing) throw new InvalidOperationException("Cannot begin a day while an action is executing.");
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            CurrentDay = day;
            if (actionPointMax >= 0) ActionPoints.ResetForDay(actionPointMax);
            else ActionPoints.ResetForDay(ActionPoints.Max);
            StateChanged?.Invoke();
        }

        public bool HasBlockingActions()
            => definitions.Values.Any(action => action.mustExecute && IsAvailable(action.actionKey, out _));

        public bool IsAvailable(string actionKey, out List<NarrativeActionCheck> checks)
        {
            checks = new List<NarrativeActionCheck>();
            NarrativeActionDefinition action = Get(actionKey);
            if (action == null || executing || IsExpired(action)) return false;
            if (action.type == NarrativeActionType.Daily && dailyExecution.TryGetValue(actionKey, out int day) && day == CurrentDay) return false;
            if (action.type == NarrativeActionType.Once && counters.ContainsKey(actionKey)) return false;
            foreach (NarrativeActionRequirement requirement in action.parsedRequirements ?? NarrativeActionRequirementParser.Parse(action.requirements))
            {
                NarrativeActionCheck check = Check(requirement);
                checks.Add(check);
            }
            return checks.All(check => check.satisfied);
        }

        public bool Execute(string actionKey, Action<bool> completed = null)
        {
            if (!IsAvailable(actionKey, out _)) return false;
            NarrativeActionDefinition action = definitions[actionKey];
            if (!ConsumeRequirements(action)) return false;
            executing = true;
            executingAction = actionKey;
            pendingCompletion = completed;
            pendingEvents = new Queue<string>(action.resultEvents ?? new List<string>());
            ActionStarted?.Invoke(action);
            RecordExecution(action);
            PlayNextEvent();
            return true;
        }

        public void CaptureInto(NarrativeActionStateSnapshot snapshot)
        {
            if (snapshot == null) return;
            snapshot.day = CurrentDay;
            snapshot.actionPoints = ActionPoints.Current;
            snapshot.actionPointMax = ActionPoints.Max;
            snapshot.counters = counters.Select(pair => new NarrativeActionCounterValue { key = pair.Key, value = pair.Value }).ToList();
            snapshot.daily = dailyExecution.Select(pair => new NarrativeActionDayValue { key = pair.Key, day = pair.Value }).ToList();
            snapshot.expiry = expiryDays.Select(pair => new NarrativeActionExpiryValue { key = pair.Key, day = pair.Value }).ToList();
        }

        public void RestoreFrom(NarrativeActionStateSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (executing) throw new InvalidOperationException("Cannot restore action state during action execution.");
            CurrentDay = Mathf.Max(1, snapshot.day);
            ActionPoints.ResetForDay(Mathf.Max(0, snapshot.actionPointMax));
            ActionPoints.Set(snapshot.actionPoints);
            counters.Clear(); dailyExecution.Clear(); expiryDays.Clear();
            if (snapshot.counters != null) foreach (var item in snapshot.counters) if (item != null && !string.IsNullOrEmpty(item.key)) counters[item.key] = Mathf.Max(0, item.value);
            if (snapshot.daily != null) foreach (var item in snapshot.daily) if (item != null && !string.IsNullOrEmpty(item.key)) dailyExecution[item.key] = item.day;
            if (snapshot.expiry != null) foreach (var item in snapshot.expiry) if (item != null && !string.IsNullOrEmpty(item.key)) expiryDays[item.key] = item.day;
            StateChanged?.Invoke();
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
                case NarrativeActionRequirementType.Bar:
                    satisfied = narrative.State.TryGetBar(requirement.key, out int bar) && Compare(bar, requirement.op, requirement.value); current = bar.ToString(); break;
                case NarrativeActionRequirementType.Condition:
                    satisfied = narrative.State.IsFlagSet(requirement.key); break;
                case NarrativeActionRequirementType.NotCondition:
                    satisfied = !narrative.State.IsFlagSet(requirement.key); break;
                default:
                    satisfied = ActionPoints.Current >= requirement.value; current = ActionPoints.Current.ToString(); break;
            }
            return new NarrativeActionCheck { satisfied = satisfied, requirement = requirement.ToString(), currentValue = current, requiredValue = required, messageKey = satisfied ? "action.requirement.ok" : "action.requirement.missing" };
        }

        private bool ConsumeRequirements(NarrativeActionDefinition action)
        {
            foreach (NarrativeActionRequirement requirement in action.parsedRequirements)
                if (!Check(requirement).satisfied) return false;
            var consumedItems = new List<NarrativeActionRequirement>();
            foreach (NarrativeActionRequirement requirement in action.parsedRequirements)
            {
                if (requirement.type == NarrativeActionRequirementType.ActionPoint && !ActionPoints.TryConsume(requirement.value)) return false;
                if (requirement.type != NarrativeActionRequirementType.Item) continue;
                if (!inventory.TryConsume(requirement.key, requirement.value))
                {
                    foreach (NarrativeActionRequirement consumed in consumedItems) inventory.TryAdd(consumed.key, consumed.value);
                    foreach (NarrativeActionRequirement point in action.parsedRequirements)
                        if (point.type == NarrativeActionRequirementType.ActionPoint) ActionPoints.Add(point.value);
                    return false;
                }
                consumedItems.Add(requirement);
            }
            return true;
        }

        private void RecordExecution(NarrativeActionDefinition action)
        {
            switch (action.type)
            {
                case NarrativeActionType.Daily: dailyExecution[action.actionKey] = CurrentDay; break;
                case NarrativeActionType.Once: counters[action.actionKey] = 1; break;
                case NarrativeActionType.Progress:
                    counters[action.actionKey] = GetProgress(action.actionKey) + 1;
                    break;
            }
            StateChanged?.Invoke();
        }

        private int GetProgress(string key) => counters.TryGetValue(key, out int value) ? value : 0;
        private bool IsExpired(NarrativeActionDefinition action) => action.type == NarrativeActionType.Timed && expiryDays.TryGetValue(action.actionKey, out int day) && CurrentDay > day;

        private void PlayNextEvent()
        {
            if (pendingEvents.Count == 0)
            {
                Finish(true); return;
            }
            string eventId = pendingEvents.Dequeue();
            currentEventId = eventId;
            if (!narrative.QueueEvent(eventId))
            {
                Finish(false); return;
            }
        }

        private void OnEventCompleted(string eventId)
        {
            if (!executing || pendingEvents == null || eventId != currentEventId) return;
            currentEventId = null;
            PlayNextEvent();
        }

        private void Finish(bool success)
        {
            NarrativeActionDefinition action = Get(executingAction);
            executing = false; executingAction = null; pendingEvents = null; currentEventId = null;
            Action<bool> callback = pendingCompletion; pendingCompletion = null;
            ActionFinished?.Invoke(action, success);
            StateChanged?.Invoke();
            callback?.Invoke(success);
        }

        private static bool Compare(int current, string op, int expected) => op switch
        {
            ">" => current > expected, ">=" => current >= expected, "<" => current < expected, "<=" => current <= expected, _ => current == expected
        };

        private static void Validate(NarrativeActionDefinition action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.actionKey)) throw new FormatException("Action key is required.");
            if (action.mustExecute && action.type == NarrativeActionType.Once) Debug.LogWarning("[NCS][Action] Once action is marked mustExecute: " + action.actionKey);
            if (action.type == NarrativeActionType.Progress && action.progressTarget <= 0) throw new FormatException("Progress action requires a positive target: " + action.actionKey);
            if (action.type == NarrativeActionType.Timed && action.durationDays <= 0) throw new FormatException("Timed action requires positive duration: " + action.actionKey);
        }
    }
}
