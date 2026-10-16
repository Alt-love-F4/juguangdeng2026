namespace Spotlight.GameFlow
{
    /// <summary>Optional host contract. False includes not restored, not open, or temporarily unsafe.</summary>
    public interface IDayFlowBusinessEntryEndpoint
    {
        bool NarrativeBusinessEntryReady { get; }
    }
}
