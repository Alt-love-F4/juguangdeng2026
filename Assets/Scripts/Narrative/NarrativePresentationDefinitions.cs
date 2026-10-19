using System;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    [Serializable] public sealed class NarrativePresentationIndexDefinition
    {
        public string presentationKey, presentationClass, logTag;
    }

    public enum NarrativeConversationStepType { Dialogue, PassiveCheck, LetterReveal, LetterRead, End, Gameplay }

    [Serializable] public sealed class NarrativeConversationPageDefinition
    {
        public string pageId, presentationKey, pageCondKeys, pageBarConds, optionGroupKey, bgOverride, bgmOverride;
        public NarrativeConversationStepType stepType;
        public string readingKey, checkId, gameplayKey, nextPageId;
        public int seq;
    }

    [Serializable] public sealed class NarrativeConversationLineDefinition
    {
        public string lineId, pageId, speakerKey, side, textKey, voiceKey, emote, sfxKey, accentFxKey, accentTargetKey, speakerNameKey;
        public int seq;
    }

    [Serializable] public sealed class NarrativeNoticeDefinition
    {
        public string presentationKey, titleKey, timeKey, imageKey, textKey, barConds, noticeBarConds;
    }

    [Serializable] public sealed class NarrativeReadingPageDefinition
    {
        public string pageId, presentationKey, titleKey, imageKey, pageCondKeys, pageBarConds;
        public int seq;
    }

    [Serializable] public sealed class NarrativeReadingLineDefinition
    {
        public string lineId, pageId, mode, speakerKey, textKey, voiceKey;
        public int seq;
    }

    /// <summary>A real host gameplay request. The host owns the coffee interaction, not NCS.</summary>
    public sealed class NarrativeGameplayPresentationData
    {
        public string pageId, gameplayKey, eventId;
        public object continuityOwner;
    }

    public sealed class NarrativeGameplayResult
    {
        public bool passed, cancelled;
    }

    public interface INarrativeGameplayResultSource
    {
        event Action<string, bool> GameplayCompleted;
    }

    public sealed class NarrativeConversationPresentationData
    {
        public object continuityOwner;
        public bool continues;
        public string eventId, presentationKey;
        public string title;
        public string backgroundPath;
        public string bgmOverride;
        public readonly List<NarrativeConversationPresentationOption> options = new List<NarrativeConversationPresentationOption>();
        public Func<string, bool> selectChoice;
        public Action cancelled;
        public Action releaseStage;
        public readonly List<NarrativeConversationPresentationLine> lines = new List<NarrativeConversationPresentationLine>();
    }
    public sealed class NarrativeConversationPresentationOption
    {
        public string id, text, requirement;
        public bool enabled;
    }
    public sealed class NarrativeConversationPresentationLine
    {
        public string speakerKey, speakerName, portraitPath, side, text, accentFxKey, accentTargetKey, sfxKey, voiceKey;
        public string lineId, pageId;
        public int seq;
        public string emote;
        public UnityEngine.Sprite portrait;
    }

    public sealed class NarrativeReadingLinePresentationData
    {
        public string lineId, pageId;
        public int seq;
        public string text, textKey, speakerKey, speakerName, portraitPath, mode, voiceKey;
        public UnityEngine.Sprite portrait;
    }

    /// <summary>Resolved, per-step snapshot. UI must not query NCS or commit story results.</summary>
    public sealed class NarrativeLetterPresentationData
    {
        public object continuityOwner;
        public string eventId, presentationKey, pageId, readingKey, title, imagePath, envelopePath;
        public string titleKey, readingPageId;
        public bool revealOnly, allowCancel;
        public Action<bool> setSuspended;
        public Action cancelled;
        public Action releaseStage;
        public readonly List<NarrativeLetterPresentationLine> lines = new List<NarrativeLetterPresentationLine>();
    }

    public sealed class NarrativeLetterPresentationLine
    {
        public string eventId, presentationKey, pageId, lineId, textKey, text, mode, speakerKey, speakerName, voiceKey;
        public int seq;
    }

    public delegate bool NarrativeLetterPlayback(NarrativeEventRequest request, NarrativeLetterPresentationData data, Action completed);

    public sealed class NarrativeCharacterPresentationData
    {
        public string key, name, portraitPath;
        public UnityEngine.Sprite portrait, avatar;
    }

    public sealed class NarrativeNoticePresentationData
    {
        public string presentationKey, titleKey, timeKey, imageKey, textKey, barConds;
        public string title, time, body;
    }

    public sealed class NarrativePresentationConfiguration
    {
        public readonly List<NarrativePresentationIndexDefinition> index = new List<NarrativePresentationIndexDefinition>();
        public readonly List<NarrativeConversationPageDefinition> conversationPages = new List<NarrativeConversationPageDefinition>();
        public readonly List<NarrativeConversationLineDefinition> conversationLines = new List<NarrativeConversationLineDefinition>();
        public readonly List<NarrativeNoticeDefinition> notices = new List<NarrativeNoticeDefinition>();
        public readonly List<NarrativeReadingPageDefinition> readingPages = new List<NarrativeReadingPageDefinition>();
        public readonly List<NarrativeReadingLineDefinition> readingLines = new List<NarrativeReadingLineDefinition>();
    }
}
