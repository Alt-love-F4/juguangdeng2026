using System;

namespace Spotlight.GameFlow
{
    /// <summary>Small boundary between the day loop and the active gameplay module.</summary>
    public interface IDayFlowGameplayEndpoint
    {
        bool DayFlowBusinessActive { get; }
        bool DayFlowBusinessFinished { get; }
        void StartDayFlowBusiness();
        void EndDayFlowBusiness();
    }

    /// <summary>Optional first-cafe tutorial bridge kept outside the FreeCafe assembly.</summary>
    public interface ICafeTutorialGameplayEndpoint
    {
        event Action<string, string> CafeTutorialStepCompleted;
        void PrepareCafeTutorial();
        bool BeginCafeTutorialCoffee();
        void FinishCafeTutorial();
    }
}
