using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Narrative
{
    public enum NarrativeObjectType { Npc, Player, World }
    public enum NarrativeBarType { Stat, Relation, World }
    public enum NarrativeDirection { HighIsGood, LowIsGood }
    public enum NarrativeSourceMode { Ncs, External }
    public enum NarrativeAdvanceMode { Monotonic, EventDriven, Derived }
    public enum NarrativeTriggerDirection { Up, Down, Both }
    public enum NarrativeCrossType { Cross, Hold }
    public enum NarrativeOnceType { Once, Repeat }
    public enum NarrativeEventKind { Actor, World }
    public enum NarrativeResultType { BarAdd, BarSet, RelAdd, SetFlag, ClearFlag, SetSwitch, ObjectState, UnlockEvent, LockEvent, AdvanceDay, AwardAchievement, PlaySetup, BarHint, SpawnNpc, DespawnNpc, ShowPosition, HidePosition, AddTempPosition, RemoveTempPosition, ItemAdd, ItemRemove, MoneyAdd, MoneyRemove, ReserveCafeVisit, QueueEvent }
    public enum NarrativeTimeSlot { Morning, Noon, Night }
    public enum NarrativeRepeatRule { Once, Daily, Weekly, Yearly }

    [Serializable]
    public sealed class NarrativeObjectDefinition
    {
        public string objId;
        public NarrativeObjectType objType;
        public string nameText;
        public string iconPath;
        public string portraitPath;
        public string portraitIndoorPath;
        public string avtarPath; // 保留策划 Object 字段拼写。
        public string descText;
        public string tags;
    }

    [Serializable]
    public sealed class NarrativeBarDefinition
    {
        public string barKey;
        public string ownerId;
        public string targetId;
        public NarrativeBarType barType;
        public int min;
        public int max;
        public NarrativeDirection direction;
        public NarrativeAdvanceMode advanceMode = NarrativeAdvanceMode.EventDriven;
        public NarrativeSourceMode sourceMode = NarrativeSourceMode.Ncs;
        public string providerId;
        public string sourceKey;
        public int initValue;
        public int reverseInitValue;
        public string nameText;
        public string derivedFormula;
        public List<string> derivedSources = new List<string>();
        public List<float> derivedWeights = new List<float>();
        public int autoStep = 1;

        public int Clamp(int value) => Mathf.Clamp(value, min, max);
    }

    [Serializable]
    public sealed class NarrativeTriggerDefinition
    {
        public string triggerId;
        public string barKey;
        public int threshold;
        public NarrativeTriggerDirection direction;
        public NarrativeCrossType crossType;
        public NarrativeOnceType onceType;
        public string reachCount = "all";
        public bool resetOnExit = true;
        public string eventId;
        public int priority;
        public bool enabled = true;

        public bool MatchesReach(int count)
        {
            return string.Equals(reachCount, "all", StringComparison.OrdinalIgnoreCase) || int.TryParse(reachCount, out int target) && target == count;
        }
    }

    [Serializable]
    public sealed class NarrativeEventDefinition
    {
        public string eventId;
        public NarrativeEventKind eventKind;
        public string eventKindRaw;
        public string actorId;
        public string targetId;
        public string titleText;
        public List<string> contentKeys = new List<string>();
        public string presentationClass;
        public string presentationKey;
        public string presentationLogTag;
        public string eventProperty;
        public string setupType;
        public string setupKey;
        public List<NarrativeEventProperty> eventProperties = new List<NarrativeEventProperty>();
        public bool onceOnly;
        public string logTag;
        public string conditionOwnerId;
    }

    [Serializable]
    public sealed class NarrativeResultDefinition
    {
        public string resultId;
        public string eventId;
        public int seq;
        public NarrativeResultType resultType;
        public string target;
        public string value;
        public string note;
    }

    [Serializable]
    public sealed class NarrativeConditionKeyDefinition
    {
        public string key, nameText;
        public bool initialValue, onlySetTrue;
    }

    [Serializable]
    public sealed class NarrativeEndingDefinition
    {
        public string endingId, titleText, messageText;
        public bool enabled;
        public int priority;
    }

    [Serializable]
    public sealed class NarrativeConditionDefinition
    {
        public string conditionId;
        public string ownerType;
        public string ownerId;
        public int groupIndex;
        public string condType;
        public string target;
        public string op;
        public string value;
        public bool notFlag;
    }

    [Serializable] public sealed class NarrativeTemplateDefinition
    {
        public string tplName, descText, interruptType, atomSeq, slotDef;
        public bool defaultSkip;
    }
    [Serializable] public sealed class NarrativeSetupParamsDefinition
    {
        public string setupKey, tplName, slotValues, checkId;
        public bool skipAble, autoAdvance;
        public List<NarrativeAtomParameter> parameters = new List<NarrativeAtomParameter>();
    }
    [Serializable] public sealed class NarrativeAtomParameter { public string slot, value; }
    [Serializable] public sealed class NarrativeEventProperty { public string property, value; }
    [Serializable] public sealed class NarrativeChoiceDefinition
    {
        public string choiceId, groupKey, textKey, resultOwnerId, checkId;
        public int seq;
    }
    [Serializable] public sealed class NarrativeTextDefinition { public string key, content; }
    [Serializable] public sealed class NarrativeOpeningPresentationDefinition { public string presentationId, titleKey, leftNameKey, rightNameKey, visualsResource, centerNameKey; }
    [Serializable] public sealed class NarrativeOpeningLineDefinition { public string lineId, presentationId, speaker, textKey, gesture; public int seq; public float autoAdvanceSeconds; }
    [Serializable] public sealed class NarrativeEventDateDefinition
    {
        public string eventId;
        public int day, priority, rowIndex;
        public NarrativeTimeSlot timeSlot;
        public NarrativeRepeatRule repeatRule;
    }
    public sealed class NarrativeConditionContext
    {
        public string ActorId, TargetId, Location;
        public NarrativeTimeSlot TimeSlot;
    }
    [Serializable]
    public sealed class NarrativeSnapshot
    {
        public long cafeVisitVersion, cafeVisitSequence;
        public List<NarrativeCafeVisitSave> cafeVisits = new List<NarrativeCafeVisitSave>();
        public List<NarrativeCafeFeedbackSave> cafeFeedback = new List<NarrativeCafeFeedbackSave>();
        public List<NarrativeStringValue> cafeLocationOwners = new List<NarrativeStringValue>();
        // Null identifies a legacy snapshot; an empty list is an authoritative new bool pool.
        public List<NarrativeBoolValue> conditionValues;
        public string selectedEndingId;
        public int officialDayVersion;
        public int officialDayProcessed;
        public List<NarrativeBarValue> bars = new List<NarrativeBarValue>();
        public List<NarrativeCounterValue> counters = new List<NarrativeCounterValue>();
        public List<string> firedTriggers = new List<string>();
        public List<string> happenedEvents = new List<string>();
        public List<NarrativeBoolValue> flags = new List<NarrativeBoolValue>();
        public List<NarrativeBoolValue> switches = new List<NarrativeBoolValue>();
        public List<NarrativeStringValue> objectStates = new List<NarrativeStringValue>();
        public string location;
        public NarrativeTimeSlot timeSlot;
        public uint randomSeed;
        public bool turnOpen, turnEnding;
        public List<string> turnEvents = new List<string>();
        public List<NarrativePendingValue> pending = new List<NarrativePendingValue>();
        public NarrativePendingValue active;
        public NarrativeAbilityCheckSnapshot activeAbilityCheck;
        public string activeAbilityCheckId;
        public List<NarrativeBarChange> changes = new List<NarrativeBarChange>();
        public List<NarrativeStringValue> npcLocations = new List<NarrativeStringValue>();
        public List<NarrativeResultDefinition> pendingEffects = new List<NarrativeResultDefinition>();
        public NarrativeActionStateSnapshot actionState;
    }

    [Serializable] public sealed class NarrativeBarValue { public string key; public int value; public bool reverse; }
    [Serializable] public sealed class NarrativeCounterValue { public string key; public int value; }
    [Serializable] public sealed class NarrativeBoolValue { public string key; public bool value; }
    [Serializable] public sealed class NarrativeStringValue { public string key; public string value; }

    public readonly struct NarrativeEventRequest
    {
        public readonly NarrativeEventDefinition Event;
        public readonly NarrativeTriggerDefinition Trigger;
        public readonly int Depth;
        public readonly string RequestId;
        public readonly string PassiveHint;
        public readonly IReadOnlyList<NarrativeReadingLinePresentationData> ReadingLines;
        public readonly IReadOnlyList<string> SelectedChoices;
        public readonly NarrativeConditionContext Context;
        public string SelectedChoiceId => SelectedChoices != null && SelectedChoices.Count > 0 ? SelectedChoices[0] : null;
        public NarrativeEventRequest(NarrativeEventDefinition @event, NarrativeTriggerDefinition trigger, int depth, IReadOnlyList<string> selectedChoices = null, NarrativeConditionContext context = null, string requestId = null, string passiveHint = null,
            IReadOnlyList<NarrativeReadingLinePresentationData> readingLines = null)
        { ReadingLines = readingLines; PassiveHint = passiveHint; Event = @event; Trigger = trigger; Depth = depth; SelectedChoices = selectedChoices; Context = context; RequestId = requestId ?? Guid.NewGuid().ToString("N"); }
    }
    [Serializable] public sealed class NarrativePendingValue { public string eventId, triggerId, requestId, location, passiveHint; public int depth; public NarrativeTimeSlot timeSlot; public List<string> selectedChoices = new List<string>(); }
    [Serializable] public sealed class NarrativeBarChange { public string key; public int oldValue, newValue, depth; }
    public sealed class NarrativeEffectNotification { public NarrativeResultDefinition Result; public string Target, Value; }

    /// <summary>Cancellation is not successful completion. Terminal reasons: disposed, host_unavailable.</summary>
    public interface INarrativePresentationCancellation
    {
        event Action<string> Cancelled;
    }

    public interface INarrativePresentationDrain
    {
        void Drain(Action completed);
    }

    public delegate bool NarrativeConversationPlayback(NarrativeEventRequest request, NarrativeConversationPresentationData data, Action completed);

    public delegate bool NarrativeNoticePlayback(NarrativeEventRequest request, NarrativeNoticePresentationData data, Action completed);

    public delegate bool NarrativeReadingPlayback(
        NarrativeEventRequest request, string title, IReadOnlyList<string> pages, string imageResourcePath,
        bool dialogue, string speaker, string portraitResourcePath, Action completed);

    public interface INarrativePresenter
    {
        void Play(NarrativeEventRequest request, Action completed);
    }

    public sealed class ImmediateNarrativePresenter : INarrativePresenter
    {
        public void Play(NarrativeEventRequest request, Action completed)
        {
            string title = request.Event.titleText ?? request.Event.eventId;
            Debug.Log($"[NCS][Presentation] {request.Event.eventId}: {title} (setup={request.Event.setupType}/{request.Event.setupKey})");
            if (request.Event.contentKeys != null)
                for (int i = 0; i < request.Event.contentKeys.Count; i++)
                    Debug.Log($"[NCS][Content] {request.Event.contentKeys[i]}");
            completed?.Invoke();
        }
    }
}
