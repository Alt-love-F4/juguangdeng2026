using System;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    /// <summary>A host-owned real gameplay attempt. Completion is latched, never executed recursively.</summary>
    public sealed class NarrativeAutomationGameplayRequest
    {
        private readonly Action<NarrativeGameplayResult> complete;
        public NarrativeEventRequest Request { get; }
        public NarrativeGameplayPresentationData Data { get; }
        public bool IsCompleted { get; private set; }
        internal NarrativeAutomationGameplayRequest(NarrativeEventRequest request, NarrativeGameplayPresentationData data,
            Action<NarrativeGameplayResult> complete)
        { Request = request; Data = data; this.complete = complete; }
        public void Complete(NarrativeGameplayResult result)
        {
            if (IsCompleted) return;
            IsCompleted = true;
            complete(result);
        }
    }

    public sealed class NarrativeAutomationPageState
    {
        public string EventId { get; internal set; }
        public string PresentationKey { get; internal set; }
        public string PageId { get; internal set; }
        public string Kind { get; internal set; }
        public string ChoiceGroup { get; internal set; }
        public string SelectedChoiceId { get; internal set; }
        public bool Visible { get; internal set; }
        public bool AwaitingGameplay { get; internal set; }
        public IReadOnlyList<string> TextKeys { get; internal set; } = Array.Empty<string>();
    }

    public sealed class NarrativeAutomationChoiceContext
    {
        public NarrativeController Controller { get; internal set; }
        public NarrativeEventRequest Request { get; internal set; }
        public string PageId { get; internal set; }
        public string GroupKey { get; internal set; }
        /// <summary>Only currently unlocked choices in the current group, in authored order.</summary>
        public IReadOnlyList<NarrativeChoiceDefinition> Choices { get; internal set; }
        public IReadOnlyList<NarrativeAutomationChoiceScore> Scores { get; internal set; }
    }

    public sealed class NarrativeAutomationChoiceScore
    {
        public string ChoiceId { get; internal set; }
        public double Score { get; internal set; }
        public IReadOnlyList<string> Reasons { get; internal set; }
    }

    public sealed class NarrativeAutomationPlanStatus
    {
        public string TargetEndingId { get; internal set; }
        public string SelectedEndingId { get; internal set; }
        public bool TargetReached { get; internal set; }
        /// <summary>Best AND-group's fraction; not a probability or a reachability guarantee.</summary>
        public double ConditionProgress { get; internal set; }
        public IReadOnlyList<NarrativeConditionDiagnostic> Conditions { get; internal set; }
    }
}
