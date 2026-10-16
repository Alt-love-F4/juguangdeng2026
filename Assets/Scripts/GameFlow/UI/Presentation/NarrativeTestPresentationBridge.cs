using System.Collections;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Connects the standalone Narrative workbench to the production presentation panels.</summary>
    public sealed class NarrativeTestPresentationBridge : MonoBehaviour
    {
        private IEnumerator Start()
        {
            while (NarrativeRuntime.Instance == null || !NarrativeRuntime.Instance.IsReady)
                yield return null;
            var runtime = NarrativeRuntime.Instance;
            runtime.ReadingPlayback = ReadingEventPlayback.Play;
            runtime.NoticePlayback = NarrativeNoticeEventPlayback.Play;
            runtime.ConversationPlayback = NarrativeConversationEventPlayback.Play;
            runtime.LetterPlayback = LetterEventPlayback.Play;
            runtime.ConversationReadingPlayback = ConversationReadingPlayback.Play;
            runtime.CharacterResolver = NarrativeCharacterResolver.Resolve;
        }
    }
}
