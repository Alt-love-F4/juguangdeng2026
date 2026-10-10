using System;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    [Serializable] public sealed class NarrativePresentationIndexDefinition
    {
        public string presentationKey, presentationClass, logTag;
    }

    [Serializable] public sealed class NarrativeConversationPageDefinition
    {
        public string pageId, presentationKey, pageCondKeys, pageBarConds, optionGroupKey, bgOverride, bgmOverride;
        public int seq;
    }

    [Serializable] public sealed class NarrativeConversationLineDefinition
    {
        public string lineId, pageId, speakerKey, side, textKey, voiceKey, emote, sfxKey, accentFxKey, accentTargetKey;
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
        public string title;
        public readonly List<NarrativeConversationPresentationLine> lines = new List<NarrativeConversationPresentationLine>();
    }
    public sealed class NarrativeConversationPresentationLine
    {
        public string speakerKey, speakerName, portraitPath, side, text, accentFxKey, accentTargetKey;
    }

    public sealed class NarrativeCharacterPresentationData
    {
        public string key, name, portraitPath;
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
