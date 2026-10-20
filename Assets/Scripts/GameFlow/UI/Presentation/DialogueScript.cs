using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.GameFlow
{
    public enum DialogueSpeaker { Narrator = 0, Left = 1, Right = 2, Center = 3, Offstage = 4 }
    public enum DialogueGesture { None, Nod, Shake }

    /// <summary>剧本由调用方提供，可来自 Luban、ScriptableObject 或测试数据源。</summary>
    public interface IDialogueScriptSource
    {
        DialogueScript CreateScript();
    }

    public interface IDialoguePresentation
    {
        bool IsPlaying { get; }
        void Play(DialogueScript script, Action onFinished = null, Action onCancelled = null);
        void Advance();
        void Stop();
    }

    [Serializable]
    public sealed class DialogueCharacter
    {
        [SerializeField] private string characterKey;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite portrait;
        public string CharacterKey => characterKey ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public Sprite Portrait => portrait;
        public bool PortraitSpecified { get; } = true;
        public static bool SameIdentity(DialogueCharacter a, DialogueCharacter b)
            => a != null && b != null && !string.IsNullOrEmpty(a.CharacterKey)
                && string.Equals(a.CharacterKey, b.CharacterKey, StringComparison.Ordinal);
        public static DialogueCharacter Resolve(DialogueCharacter current, DialogueCharacter incoming)
            => SameIdentity(current, incoming) && !incoming.PortraitSpecified
                ? new DialogueCharacter(incoming.CharacterKey, incoming.DisplayName, current.Portrait) : incoming;
        public DialogueCharacter(string displayName, Sprite portrait) : this(string.Empty, displayName, portrait) { }
        public DialogueCharacter(string characterKey, string displayName, Sprite portrait, bool portraitSpecified = true)
        {
            PortraitSpecified = portraitSpecified;
            this.characterKey = characterKey ?? string.Empty;
            this.displayName = displayName;
            this.portrait = portrait;
        }
    }

    [Serializable]
    public sealed class DialogueOption
    {
        [SerializeField] private string optionId;
        [SerializeField] private string text;
        [SerializeField] private DialogueLine response;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string reqHintKey;
        [SerializeField] private string resultKey;
        public bool Enabled => enabled;
        // Already resolved UI text, not a condition evaluator or a result executor.
        public string RequirementHint => reqHintKey ?? string.Empty;
        public string ResultKey => resultKey ?? string.Empty;
        public string OptionId => optionId ?? string.Empty;
        public string Text => text ?? string.Empty;
        public DialogueLine Response => response;

        public DialogueOption(string optionId, string text, DialogueLine response = null,
            bool enabled = true, string requirementHint = null, string resultKey = null)
        {
            if (string.IsNullOrWhiteSpace(optionId)) throw new ArgumentException("An option requires an id.", nameof(optionId));
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("An option requires text.", nameof(text));
            this.optionId = optionId;
            this.text = text;
            this.response = response;
            this.enabled = enabled;
            reqHintKey = requirementHint;
            this.resultKey = resultKey;
        }
    }

    [Serializable]
    public sealed class DialogueLine
    {
        [SerializeField] private DialogueSpeaker speaker;
        [SerializeField, TextArea(3, 8)] private string text;
        [SerializeField] private string speakerNameOverride;
        [SerializeField] private DialogueGesture gesture;
        [SerializeField, Min(0f)] private float autoAdvanceSeconds;
        [SerializeField] private bool replaceLeft;
        [SerializeField] private DialogueCharacter leftCharacter;
        [SerializeField] private bool replaceRight;
        [SerializeField] private DialogueCharacter rightCharacter;
        [SerializeField] private bool replaceCenter;
        [SerializeField] private DialogueCharacter centerCharacter;
        // Matches NCS line field names; no NCS table/runtime access in the presentation layer.
        [SerializeField] private string accentFxKey;
        [SerializeField] private string accentTargetKey;
        [SerializeField] private string sfxKey;
        [SerializeField] private string voiceKey;
        public DialogueSourceLocation Source { get; }
        public DialogueSpeaker Speaker => speaker;
        public string Text => text ?? string.Empty;
        public string SpeakerNameOverride => speakerNameOverride;
        public DialogueGesture Gesture => gesture;
        // 0 waits for the player; the timer starts only after the visible page is fully revealed.
        public float AutoAdvanceSeconds => autoAdvanceSeconds;
        public bool ReplaceLeft => replaceLeft;
        public DialogueCharacter LeftCharacter => leftCharacter;
        public bool ReplaceRight => replaceRight;
        public DialogueCharacter RightCharacter => rightCharacter;
        public bool ReplaceCenter => replaceCenter;
        public DialogueCharacter CenterCharacter => centerCharacter;
        public string AccentFxKey => accentFxKey ?? string.Empty;
        public string AccentTargetKey => accentTargetKey ?? string.Empty;
        public string PassiveHint { get; }
        public string SfxKey => sfxKey ?? string.Empty;
        public string VoiceKey => voiceKey ?? string.Empty;

        public DialogueLine(DialogueSpeaker speaker, string text, string speakerNameOverride = null,
            DialogueGesture gesture = DialogueGesture.None, float autoAdvanceSeconds = 0f,
            bool replaceLeft = false, DialogueCharacter leftCharacter = null,
            bool replaceRight = false, DialogueCharacter rightCharacter = null,
            string accentFxKey = null, string accentTargetKey = null,
            bool replaceCenter = false, DialogueCharacter centerCharacter = null, string passiveHint = null,
            string sfxKey = null, string voiceKey = null, DialogueSourceLocation source = null)
        {
            if (!Enum.IsDefined(typeof(DialogueSpeaker), speaker)) throw new ArgumentOutOfRangeException(nameof(speaker));
            if (!Enum.IsDefined(typeof(DialogueGesture), gesture)) throw new ArgumentOutOfRangeException(nameof(gesture));
            if (float.IsNaN(autoAdvanceSeconds) || float.IsInfinity(autoAdvanceSeconds) || autoAdvanceSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(autoAdvanceSeconds));
            Source = source;
            PassiveHint = passiveHint;
            this.speaker = speaker;
            this.text = text;
            this.speakerNameOverride = speakerNameOverride;
            this.gesture = gesture;
            this.autoAdvanceSeconds = autoAdvanceSeconds;
            this.replaceLeft = replaceLeft;
            this.leftCharacter = leftCharacter;
            this.replaceRight = replaceRight;
            this.rightCharacter = rightCharacter;
            this.replaceCenter = replaceCenter;
            this.centerCharacter = centerCharacter;
            this.accentFxKey = accentFxKey;
            this.accentTargetKey = accentTargetKey;
            this.sfxKey = sfxKey;
            this.voiceKey = voiceKey;
        }
    }

    /// <summary>不可变脚本快照；一次传入任意数量台词，不为每句创建 UI 或 Tween。</summary>
    public sealed class DialogueScript
    {
        private readonly DialogueLine[] lines;
        public object ContinuityOwner { get; }
        public bool IsPreview { get; }
        public bool InstantPortraitPreview { get; }
        public bool Continues { get; }
        public string MusicKey { get; }
        public DialogueScript WithPreviewContext() => new DialogueScript(lines, LeftCharacter, RightCharacter,
            Title, Background, Options, OptionPrompt, CenterCharacter, ContinuityOwner, true, Continues, MusicKey, BackgroundVisual, InstantPortraitPreview);
        public string Title { get; }
        public DialogueCharacter LeftCharacter { get; }
        public DialogueCharacter RightCharacter { get; }
        public DialogueCharacter CenterCharacter { get; }
        public Texture Background { get; }
        public NarrativeStageVisuals.Entry BackgroundVisual { get; }
        public string OptionPrompt { get; }
        public IReadOnlyList<DialogueOption> Options { get; }
        public int Count => lines.Length;
        public DialogueLine this[int index] => lines[index];

        /// <summary>Reconstruct configured persistent stage only; skipped transient cues are never played.</summary>
        public DialogueScript RebuildAt(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            var left = LeftCharacter; var right = RightCharacter; var center = CenterCharacter;
            for (int i = 0; i < index; i++)
            {
                var line = lines[i];
                if (line.ReplaceLeft) left = DialogueCharacter.Resolve(left, line.LeftCharacter);
                if (line.ReplaceRight) right = DialogueCharacter.Resolve(right, line.RightCharacter);
                if (line.ReplaceCenter) center = DialogueCharacter.Resolve(center, line.CenterCharacter);
            }
            var remaining = new DialogueLine[Count - index];
            Array.Copy(lines, index, remaining, 0, remaining.Length);
            return new DialogueScript(remaining, left, right, Title, Background, Options, OptionPrompt, center,
                isPreview: true, musicKey: MusicKey, backgroundVisual: BackgroundVisual, instantPortraitPreview: InstantPortraitPreview);
        }

        public DialogueScript WithFirstPageHint(string hint)
        {
            var copy = (DialogueLine[])lines.Clone();
            var first = copy[0];
            copy[0] = new DialogueLine(first.Speaker, first.Text, first.SpeakerNameOverride, first.Gesture,
                first.AutoAdvanceSeconds, first.ReplaceLeft, first.LeftCharacter, first.ReplaceRight, first.RightCharacter,
                first.AccentFxKey, first.AccentTargetKey, first.ReplaceCenter, first.CenterCharacter, hint, first.SfxKey, first.VoiceKey, first.Source);
            return new DialogueScript(copy, LeftCharacter, RightCharacter, Title, Background, Options, OptionPrompt, CenterCharacter, ContinuityOwner, IsPreview, Continues, MusicKey, BackgroundVisual, InstantPortraitPreview);
        }

        public DialogueScript WithContinuity(object owner, bool continues)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            return new DialogueScript(lines, LeftCharacter, RightCharacter, Title, Background, Options, OptionPrompt,
                CenterCharacter, owner, IsPreview, continues, MusicKey, BackgroundVisual, InstantPortraitPreview);
        }

        public DialogueScript(IReadOnlyList<DialogueLine> lines, DialogueCharacter leftCharacter = null,
            DialogueCharacter rightCharacter = null, string title = null, Texture background = null,
            IReadOnlyList<DialogueOption> options = null, string optionPrompt = null,
            DialogueCharacter centerCharacter = null, object continuityOwner = null, bool isPreview = false, bool continues = false, string musicKey = null, NarrativeStageVisuals.Entry backgroundVisual = null, bool instantPortraitPreview = false)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            if (lines.Count == 0) throw new ArgumentException("A dialogue script requires at least one line.", nameof(lines));
            this.lines = new DialogueLine[lines.Count];
            for (int i = 0; i < lines.Count; i++)
                this.lines[i] = lines[i] ?? throw new ArgumentException("Dialogue line " + i + " is null.", nameof(lines));
            ContinuityOwner = continuityOwner;
            IsPreview = isPreview;
            InstantPortraitPreview = isPreview && instantPortraitPreview;
            Continues = continues;
            MusicKey = musicKey;
            LeftCharacter = leftCharacter;
            RightCharacter = rightCharacter;
            CenterCharacter = centerCharacter;
            Title = title ?? string.Empty;
            BackgroundVisual = backgroundVisual?.Snapshot();
            Background = BackgroundVisual?.useSceneBackground == true ? null : BackgroundVisual?.texture ?? background;
            OptionPrompt = optionPrompt ?? string.Empty;
            if (options != null)
            {
                var copy = new DialogueOption[options.Count];
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < options.Count; i++)
                {
                    copy[i] = options[i] ?? throw new ArgumentException("Dialogue option " + i + " is null.", nameof(options));
                    if (!ids.Add(copy[i].OptionId)) throw new ArgumentException("Duplicate option id: " + copy[i].OptionId, nameof(options));
                }
                Options = Array.AsReadOnly(copy);
            }
            else Options = Array.Empty<DialogueOption>();
        }
    }
}
