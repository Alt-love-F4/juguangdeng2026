using System;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>NCS opening ID -> resolved dialogue snapshot. Explicit sources override the current session.</summary>
    public sealed class LubanOpeningDialogueSource : IDialogueScriptSource
    {
        private readonly string presentationId;
        private readonly NarrativeConfiguration configuration;
        private readonly NarrativeRuntime runtime;

        public LubanOpeningDialogueSource(
            string presentationId = NarrativePresentationDataAdapter.DefaultOpeningPresentationId,
            NarrativeConfiguration configuration = null,
            NarrativeRuntime runtime = null)
        {
            this.presentationId = string.IsNullOrWhiteSpace(presentationId)
                ? NarrativePresentationDataAdapter.DefaultOpeningPresentationId : presentationId;
            this.configuration = configuration;
            // Explicit configuration must not accidentally use a different live session/resolver.
            this.runtime = configuration == null ? runtime : null;
        }

        public DialogueScript CreateScript()
        {
            NarrativeRuntime session = runtime;
            if (session == null && configuration == null) session = NarrativeRuntime.Instance;
            DialogueScript script;
            string error;
            bool created;
            if (session != null && session.IsReady)
                created = NarrativePresentationDataAdapter.TryCreateOpeningDialogue(session, presentationId, out script, out error);
            else
                created = NarrativePresentationDataAdapter.TryCreateOpeningDialogue(
                    configuration ?? LubanNarrativeLoader.Load(), presentationId, out script, out error);
            if (!created) throw new InvalidOperationException(error);
            return script;
        }
    }
}
