using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Spotlight.Narrative
{
    public enum NarrativeActionType { Repeat, Daily, Once, Timed, Progress, Future }
    public enum NarrativeDeliveryMode { None, Cumulative, Consecutive }
    public enum NarrativeActionRequirementType { Item, Bar, Condition, NotCondition, ActionPoint }

    [Serializable]
    public sealed class NarrativeActionDefinition
    {
        public string actionKey;
        public NarrativeActionType type = NarrativeActionType.Repeat;
        public NarrativeDeliveryMode deliveryMode;
        public List<NarrativeActionMilestoneDefinition> milestones = new List<NarrativeActionMilestoneDefinition>();
        public bool IsDelivery => deliveryMode != NarrativeDeliveryMode.None;
        public bool mustExecute;
        public string nameText;
        public string descText;
        public string requirements;
        public List<NarrativeActionRequirement> parsedRequirements = new List<NarrativeActionRequirement>();
        public List<string> resultEvents = new List<string>();
        public int durationDays;
        public int progressTarget;
        public string nextActionKey;
        public bool endDay;
        // Existing enum values remain stable for serialized data. Modifiers can combine with a base type.
        public bool IsTimed => type == NarrativeActionType.Timed || (type != NarrativeActionType.Future && durationDays > 0);
        public bool IsProgress => type == NarrativeActionType.Progress || (type != NarrativeActionType.Future && progressTarget > 0);
    }

    [Serializable]
    public sealed class NarrativeActionMilestoneDefinition
    {
        public string milestoneId;
        public string actionKey;
        public int atProgress;
        public string eventId;
        public int seq;
    }

    [Serializable]
    public sealed class NarrativeActionDeliveryValue
    {
        public string actionKey;
        public int progress;
        public int lastSubmissionDay;
        public bool completed;
        public List<string> triggeredMilestones = new List<string>();
    }

    [Serializable]
    public sealed class NarrativeActionRequirement
    {
        public NarrativeActionRequirementType type;
        public string key;
        public string op;
        public int value;
        public string displayKey;

        public override string ToString() => type switch
        {
            NarrativeActionRequirementType.Item => "Item(" + key + "," + value + ")",
            NarrativeActionRequirementType.Bar => "Bar(" + key + "," + op + "," + value + ")",
            NarrativeActionRequirementType.Condition => "Cond(" + key + ")",
            NarrativeActionRequirementType.NotCondition => "NotCond(" + key + ")",
            _ => "ActionPoint(" + value + ")"
        };
    }

    [Serializable]
    public sealed class NarrativeActionCheck
    {
        public bool satisfied;
        public string requirement;
        public string messageKey;
        public string currentValue;
        public string requiredValue;
    }

    [Serializable]
    public sealed class NarrativeActionStateSnapshot
    {
        public int deliveryVersion;
        public List<NarrativeActionDeliveryValue> deliveries = new List<NarrativeActionDeliveryValue>();
        public int day = 1;
        public int actionPoints;
        public int actionPointMax = 5;
        public List<NarrativeActionCounterValue> counters = new List<NarrativeActionCounterValue>();
        public List<NarrativeActionDayValue> daily = new List<NarrativeActionDayValue>();
        public List<NarrativeActionExpiryValue> expiry = new List<NarrativeActionExpiryValue>();
        public List<NarrativeActionMountValue> mounts = new List<NarrativeActionMountValue>();
        public List<string> retired = new List<string>();
        public List<NarrativeActionCounterValue> items = new List<NarrativeActionCounterValue>();
        public string executingAction;
        public string currentEvent;
        public List<string> pendingEvents = new List<string>();
        public List<string> dueActions = new List<string>();
    }

    [Serializable] public sealed class NarrativeActionMountValue { public string position; public string action; public int day; }

    [Serializable] public sealed class NarrativeActionCounterValue { public string key; public int value; }
    [Serializable] public sealed class NarrativeActionDayValue { public string key; public int day; }
    [Serializable] public sealed class NarrativeActionExpiryValue { public string key; public int day; }

    public interface INarrativeActionInventory
    {
        int GetCount(string itemKey);
        bool TryConsume(string itemKey, int count);
        bool TryAdd(string itemKey, int count);
    }

    public sealed class EmptyNarrativeActionInventory : INarrativeActionInventory
    {
        public int GetCount(string itemKey) => 0;
        public bool TryConsume(string itemKey, int count) => count <= 0;
        public bool TryAdd(string itemKey, int count) => count <= 0;
    }

    public sealed class NarrativeActionPoints
    {
        public int Current { get; private set; }
        public int Max { get; private set; }
        public event Action<int, int> Changed;

        public NarrativeActionPoints(int max = 5)
        {
            Max = Mathf.Max(0, max);
            Current = Max;
        }

        public void ResetForDay(int max)
        {
            Max = Mathf.Max(0, max);
            Set(Max);
        }

        public bool TryConsume(int amount)
        {
            if (amount < 0 || Current < amount) return false;
            Set(Current - amount);
            return true;
        }

        public void Add(int amount) => Set(Current + amount);
        public void SetMax(int max) { Max = Mathf.Max(0, max); Set(Current); }
        public void Set(int value)
        {
            int next = Mathf.Clamp(value, 0, Max);
            if (next == Current) return;
            int old = Current;
            Current = next;
            Changed?.Invoke(old, next);
        }
    }

    public static class NarrativeActionRequirementParser
    {
        public static List<NarrativeActionRequirement> Parse(string expression)
        {
            var result = new List<NarrativeActionRequirement>();
            if (string.IsNullOrWhiteSpace(expression)) return result;
            foreach (string raw in expression.Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = raw.Trim();
                int open = token.IndexOf('(');
                if (open <= 0 || !token.EndsWith(")", StringComparison.Ordinal))
                    throw new FormatException("Invalid action requirement: " + token);
                string name = token.Substring(0, open).Trim();
                string[] args = token.Substring(open + 1, token.Length - open - 2).Split(',');
                for (int i = 0; i < args.Length; i++) args[i] = args[i].Trim();
                switch (name)
                {
                    case "Item":
                        Require(args, 1, token); if (args.Length > 2) throw new FormatException("Invalid Item requirement: " + token);
                        result.Add(new NarrativeActionRequirement { type = NarrativeActionRequirementType.Item, key = args[0], value = args.Length > 1 ? PositiveInt(args[1], token) : 1 }); break;
                    case "Bar":
                    case "Cond":
                    case "NotCond":
                        throw new FormatException("Read-only action requirements must use Condition rows with ownerType=action: " + token);
                    case "ActionPoint": Exact(args, 1, token); result.Add(new NarrativeActionRequirement { type = NarrativeActionRequirementType.ActionPoint, value = PositiveInt(args[0], token) }); break;
                    default: throw new FormatException("Unknown action requirement: " + name);
                }
            }
            if (result.Count > 6) throw new FormatException("An action cannot have more than 6 requirements.");
            return result;
        }

        private static void Require(string[] args, int count, string token)
        {
            if (args.Length < count || string.IsNullOrWhiteSpace(args[0])) throw new FormatException("Invalid requirement: " + token);
        }
        private static void Exact(string[] args, int count, string token)
        {
            Require(args, count, token);
            if (args.Length != count) throw new FormatException("Invalid argument count: " + token);
        }
        private static int Int(string value, string token) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : throw new FormatException("Invalid number in requirement: " + token);
        private static int PositiveInt(string value, string token) { int parsed = Int(value, token); if (parsed <= 0) throw new FormatException("Expected positive number in requirement: " + token); return parsed; }
    }
}
