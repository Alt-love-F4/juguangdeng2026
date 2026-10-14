using System;

namespace Spotlight.Narrative
{
    [Serializable]
    public sealed class CafeTutorialStepDefinition
    {
        public string tutorialId, stepId, presentationId, focusTargetKey, completionSignalId, nextStepId;
        public bool optionalDemo;
    }

    /// <summary>Only the workstation entrance is executable here. Later steps are content, not completed gameplay.</summary>
    public sealed class CafeTutorialSession
    {
        public const string TutorialId = "first_cafe_tutorial";
        public const string WorkstationStep = "workstation_open";
        public const string WorkstationSignal = "workstation_opened";
        public bool WaitingForWorkstation { get; private set; }
        public bool HandoffReady { get; private set; }
        public string CurrentStepId => WaitingForWorkstation ? WorkstationStep : HandoffReady ? "make_coffee_base" : string.Empty;
        public event Action<string, string> StepCompleted;

        public void Begin()
        {
            if (WaitingForWorkstation || HandoffReady) return;
            WaitingForWorkstation = true;
        }

        public void NotifyTutorialStepCompleted(string tutorialId, string stepId)
        {
            if (!WaitingForWorkstation || tutorialId != TutorialId || stepId != WorkstationSignal) return;
            WaitingForWorkstation = false;
            HandoffReady = true;
            StepCompleted?.Invoke(tutorialId, stepId);
        }
    }
}
