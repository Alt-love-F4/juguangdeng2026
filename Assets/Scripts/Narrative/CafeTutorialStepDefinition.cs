using System;

namespace Spotlight.Narrative
{
    [Serializable]
    public sealed class CafeTutorialStepDefinition
    {
        public string tutorialId, stepId, presentationId, focusTargetKey, completionSignalId, nextStepId;
        public bool optionalDemo;
    }

    /// <summary>Runtime gate for the executable first-cafe tutorial steps.</summary>
    public sealed class CafeTutorialSession
    {
        public const string TutorialId = "first_cafe_tutorial";
        public const string WorkstationStep = "workstation_open";
        public const string WorkstationSignal = "workstation_opened";
        public const string CoffeeStep = "make_coffee_base";
        public const string CoffeeSignal = "coffee_base_completed";
        public bool WaitingForWorkstation { get; private set; }
        public bool WaitingForCoffee { get; private set; }
        public bool HandoffReady { get; private set; }
        public bool Completed { get; private set; }
        public string CurrentStepId => WaitingForWorkstation ? WorkstationStep
            : HandoffReady || WaitingForCoffee ? CoffeeStep
            : string.Empty;
        public event Action<string, string> StepCompleted;

        public void Begin()
        {
            if (WaitingForWorkstation || HandoffReady || WaitingForCoffee || Completed) return;
            WaitingForWorkstation = true;
        }

        public bool BeginCoffee()
        {
            if (!HandoffReady || Completed) return false;
            HandoffReady = false;
            WaitingForCoffee = true;
            return true;
        }

        public bool NotifyTutorialStepCompleted(string tutorialId, string stepId)
        {
            if (tutorialId != TutorialId) return false;
            if (WaitingForWorkstation && stepId == WorkstationSignal)
            {
                WaitingForWorkstation = false;
                HandoffReady = true;
            }
            else if (WaitingForCoffee && stepId == CoffeeSignal)
            {
                WaitingForCoffee = false;
                Completed = true;
            }
            else return false;
            StepCompleted?.Invoke(tutorialId, stepId);
            return true;
        }
    }
}
