namespace Spotlight.Audio
{
    /// <summary>Authored music assignments live alongside other cues in GameAudioCatalog.</summary>
    public static class NarrativeMusic
    {
        public const string EndingPrefix = "music.ending.";
        public const string ConversationPrefix = "music.conversation.";

        public static string Ending(string endingId)
        {
            string key = EndingPrefix + endingId;
            // Unknown/debug endings still receive a restrained closing theme.
            return GameAudio.Catalog != null && GameAudio.Catalog.Find(key) != null
                ? key : "music.track.回忆";
        }

        public static string Conversation(string presentationKey)
        {
            if (string.IsNullOrWhiteSpace(presentationKey)) return null;
            string key = ConversationPrefix + presentationKey;
            return GameAudio.Catalog != null && GameAudio.Catalog.Find(key) != null ? key : null;
        }
    }
}
