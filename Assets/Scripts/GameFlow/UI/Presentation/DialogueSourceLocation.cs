namespace Spotlight.GameFlow
{
    /// <summary>Generated source identity, never a temporary snapshot index or Excel row number.</summary>
    public sealed class DialogueSourceLocation
    {
        public string SourceType { get; }
        public string EventId { get; }
        public string PresentationKey { get; }
        public string SourceLineId { get; }
        public string PageId { get; }
        public int Sequence { get; }
        public DialogueSourceLocation(string sourceType, string eventId, string presentationKey,
            string sourceLineId, int sequence, string pageId = null)
        {
            SourceType = sourceType ?? string.Empty; EventId = eventId ?? string.Empty;
            PresentationKey = presentationKey ?? string.Empty; SourceLineId = sourceLineId ?? string.Empty;
            Sequence = sequence; PageId = pageId ?? string.Empty;
        }
    }
}
