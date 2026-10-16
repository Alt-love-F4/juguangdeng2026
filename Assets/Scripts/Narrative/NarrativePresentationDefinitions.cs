using System;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    [Serializable] public sealed class NarrativePresentationIndexDefinition
    {
        public string presentationKey, presentationClass, logTag;
    }

    public enum NarrativeConversationStepType { Dialogue, PassiveCheck, LetterReveal, LetterRead, FadeOut, WaitBusiness, WaitNight, Reading, WaitService, LetterHide }

    [Serializable] public sealed class NarrativeConversationPageDefinition
    {
        public string pageId, presentationKey, pageCondKeys, pageBarConds, optionGroupKey, bgOverride, bgmOverride;
        public NarrativeConversationStepType stepType;
        public string readingKey, checkId;
        public int seq;
    }

    [Serializable] public sealed class NarrativeConversationLineDefinition
    {
        public string lineId, pageId, speakerKey, side, textKey, voiceKey, emote, sfxKey, accentFxKey, accentTargetKey, speakerNameKey;
        public string stageLeftKey, stageRightKey, stageCenterKey;
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

    public sealed class NarrativeConversationPresentationData
    {
        public object continuityOwner;
        public bool continues;
        public string eventId, presentationKey;
        public string title;
        public string backgroundPath;
        public string bgmOverride;
        public readonly List<NarrativeConversationPresentationOption> options = new List<NarrativeConversationPresentationOption>();
        public NarrativeCharacterPresentationData stageLeft, stageRight, stageCenter;
        public bool replaceStageLeft, replaceStageRight, replaceStageCenter;
        public Func<string, bool> selectChoice;
        public Action cancelled;
        public Action releaseStage;
        public Action<float, Action<bool>> fadeStage;
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
        public NarrativeCharacterPresentationData stageLeft, stageRight, stageCenter;
        public bool replaceStageLeft, replaceStageRight, replaceStageCenter;
    }

    public sealed class NarrativeReadingLinePresentationData
    {
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
        public Action dismissStage;
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

    /// <summary>Resolved ordinary reading inside a conversation; never invokes letter or event settlement logic.</summary>
    public sealed class NarrativeConversationReadingPresentationData
    {
        public object continuityOwner;
        public string eventId, presentationKey, pageId, readingKey, readingPageId, titleKey, title, imagePath;
        public bool allowCancel;
        public Action cancelled;
        // ReadingPanel notifies Stop before its natural-finish/user-cancel callback. This is not completion.
        public Action playbackStopped;
        public Action releaseStage;
        public Action<bool> setSuspended;
        public readonly List<NarrativeConversationReadingPresentationLine> lines = new List<NarrativeConversationReadingPresentationLine>();
    }

    public sealed class NarrativeConversationReadingPresentationLine
    {
        public string lineId, textKey, text, mode, speakerKey, speakerName, portraitPath, voiceKey;
        public int seq;
        public UnityEngine.Sprite portrait;
    }

    public delegate bool NarrativeConversationReadingPlayback(NarrativeEventRequest request,
        NarrativeConversationReadingPresentationData data, Action completed);

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
