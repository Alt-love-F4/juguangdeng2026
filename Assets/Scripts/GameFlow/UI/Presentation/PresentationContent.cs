using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Video;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// Presentation data contracts kept independent from NCS. NCS/Luban adapters can later
    /// construct these snapshots without changing any panel implementation.
    /// Field names intentionally mirror the planned table columns.
    /// </summary>
    public enum ReadingLineKind { Narration, Dialogue }

    public sealed class ReadingLine
    {
        public string textKey { get; }
        public string speakerKey { get; }
        public string voiceKey { get; }
        public string Text { get; }
        public string Speaker { get; }
        public Sprite Portrait { get; }
        public ReadingLineKind Kind { get; }

        public ReadingLine(string text, string speaker = null, string textKey = null,
            string speakerKey = null, string voiceKey = null, Sprite portrait = null,
            ReadingLineKind kind = ReadingLineKind.Narration)
        {
            Text = text ?? string.Empty;
            Speaker = speaker ?? string.Empty;
            Portrait = portrait;
            Kind = kind;
            this.textKey = textKey ?? string.Empty;
            this.speakerKey = speakerKey ?? string.Empty;
            this.voiceKey = voiceKey ?? string.Empty;
        }
    }

    public sealed class ReadingPage
    {
        public string pageId { get; }
        public int seq { get; }
        public string pageCondKeys { get; }
        public string pageBarConds { get; }
        public string titleKey { get; }
        public string imageKey { get; }
        public string textKey { get; }
        public string speakerKey { get; }
        public string voiceKey { get; }
        public string Title { get; }
        public Texture Image { get; }
        public IReadOnlyList<ReadingLine> Lines { get; }

        public ReadingPage(string title, IEnumerable<ReadingLine> lines, Texture image = null,
            string pageId = null, int seq = 0, string pageCondKeys = null, string pageBarConds = null,
            string titleKey = null, string imageKey = null, string textKey = null,
            string speakerKey = null, string voiceKey = null)
        {
            this.pageId = pageId ?? string.Empty;
            this.seq = seq;
            this.pageCondKeys = pageCondKeys ?? string.Empty;
            this.pageBarConds = pageBarConds ?? string.Empty;
            this.titleKey = titleKey ?? string.Empty;
            this.imageKey = imageKey ?? string.Empty;
            this.textKey = textKey ?? string.Empty;
            this.speakerKey = speakerKey ?? string.Empty;
            this.voiceKey = voiceKey ?? string.Empty;
            Title = title ?? string.Empty;
            Image = image;
            var copy = lines == null ? new List<ReadingLine>() : new List<ReadingLine>(lines);
            if (copy.Exists(line => line == null)) throw new ArgumentException("Reading lines must not contain null.", nameof(lines));
            if (copy.Count == 0) copy.Add(new ReadingLine(string.Empty));
            Lines = copy.AsReadOnly();
        }

        public string GetDisplayText()
        {
            if (Lines == null || Lines.Count == 0) return string.Empty;
            var result = new StringBuilder();
            for (int i = 0; i < Lines.Count; i++)
            {
                if (i > 0) result.Append("\n");
                if (!string.IsNullOrEmpty(Lines[i].Speaker)) result.Append("【").Append(Lines[i].Speaker).Append("】 ");
                result.Append(Lines[i].Text);
            }
            return result.ToString();
        }
    }

    public sealed class ReadingScript
    {
        private readonly ReadingPage[] pages;
        public string titleKey { get; }
        public string Title { get; }
        public int Count => pages.Length;
        public ReadingPage this[int index] => pages[index];

        public ReadingScript(IEnumerable<ReadingPage> pages, string title = null, string titleKey = null)
        {
            this.pages = pages == null ? new ReadingPage[0] : new List<ReadingPage>(pages).ToArray();
            if (this.pages.Length == 0) throw new ArgumentException("Reading script requires at least one page.", nameof(pages));
            if (Array.Exists(this.pages, page => page == null)) throw new ArgumentException("Reading pages must not contain null.", nameof(pages));
            Title = title ?? string.Empty;
            this.titleKey = titleKey ?? string.Empty;
        }
    }

    public sealed class NoticeData
    {
        public string noticeId { get; }
        public string titleKey { get; }
        public string timeKey { get; }
        public string imageKey { get; }
        public string textKey { get; }
        public string barConds { get; }
        public string Title { get; }
        public string Time { get; }
        public string Body { get; }
        public Texture Image { get; }
        public bool HasProgress { get; }
        public float ProgressValue { get; }
        public string ProgressLabel { get; }
        public bool Visible { get; }

        public NoticeData(string body, string title = null, string time = null, Texture image = null,
            bool hasProgress = false, float progressValue = 0f, string progressLabel = null,
            string noticeId = null, string titleKey = null, string timeKey = null,
            string imageKey = null, string textKey = null, string barConds = null, bool visible = true)
        {
            Visible = visible;
            Body = body ?? string.Empty;
            Title = title ?? string.Empty;
            Time = time ?? string.Empty;
            Image = image;
            HasProgress = hasProgress;
            ProgressValue = Mathf.Clamp01(progressValue);
            ProgressLabel = progressLabel ?? string.Empty;
            this.noticeId = noticeId ?? string.Empty;
            this.titleKey = titleKey ?? string.Empty;
            this.timeKey = timeKey ?? string.Empty;
            this.imageKey = imageKey ?? string.Empty;
            this.textKey = textKey ?? string.Empty;
            this.barConds = barConds ?? string.Empty;
        }
    }

    public sealed class CutSceneData
    {
        public string videoPath { get; }
        public string titleKey { get; }
        public string skipTextKey { get; }
        public VideoClip Clip { get; }
        public string Title { get; }
        public string SkipText { get; }
        // Zero uses the shared configuration. A positive value overrides it for this clip.
        public float SkipHoldSeconds { get; }
        public bool PreviewOnly { get; }
        public bool AllowSkip { get; }

        public CutSceneData(string videoPath = null, VideoClip clip = null, string title = null,
            float skipHoldSeconds = 0f, bool allowSkip = true, string titleKey = null,
            string skipTextKey = null, string skipText = null, bool previewOnly = false)
        {
            this.videoPath = videoPath ?? string.Empty;
            Clip = clip;
            Title = title ?? string.Empty;
            if (float.IsNaN(skipHoldSeconds) || float.IsInfinity(skipHoldSeconds) || skipHoldSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(skipHoldSeconds));
            SkipText = skipText ?? "长按 Enter 跳过";
            SkipHoldSeconds = skipHoldSeconds;
            PreviewOnly = previewOnly;
            AllowSkip = allowSkip;
            this.titleKey = titleKey ?? string.Empty;
            this.skipTextKey = skipTextKey ?? string.Empty;
        }
    }

    /// <summary>Shared setting for future config injection; no NCS/table access.</summary>
    public static class CutScenePresentationSettings
    {
        private static float skipHoldSeconds = 1.2f;
        public static float SkipHoldSeconds
        {
            get => skipHoldSeconds;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                    throw new ArgumentOutOfRangeException(nameof(value));
                skipHoldSeconds = value;
            }
        }
    }

    public interface IReadingPresentation
    {
        bool IsPlaying { get; }
        void Play(ReadingScript script, Action onFinished = null, Action onCancelled = null);
        void Advance();
        void Back();
        void Stop();
    }

    public interface INoticePresentation
    {
        bool IsPlaying { get; }
        void Play(NoticeData data, Action onFinished = null, Action onCancelled = null);
        void Confirm();
        void Stop();
    }

    public interface ICutScenePresentation
    {
        bool IsPlaying { get; }
        void Play(CutSceneData data, Action onFinished = null, Action onCancelled = null);
        void Stop();
    }
}
