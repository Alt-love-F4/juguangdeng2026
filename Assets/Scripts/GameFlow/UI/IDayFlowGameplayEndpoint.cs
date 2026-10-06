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
}
