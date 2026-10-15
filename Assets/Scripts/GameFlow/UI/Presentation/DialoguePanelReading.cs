using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed partial class DialoguePanel
    {
        [SerializeField, Min(.01f)] private float scrollThreshold = 100f;
        [SerializeField, Min(.05f)] private float scrollGestureGap = .22f;
        private readonly HashSet<object> readingPauses = new HashSet<object>();
        private readonly DialogueHistoryService previewHistory = new DialogueHistoryService();
        private DialogueHistoryService history;
        private long historyPlayback;
        private GameObject historyRoot;
        private TMP_Text historyLabel;
        private ScrollRect historyScroll;
        private IDisposable historyPause;
        private bool controlSkipping;
        private float scrollSum, lastScrollTime = -100f;
        private bool scrollConsumed;
        private int inputBlockedFrame = -1;
        private int capturedHistoryCharacters = -1;
        private int capturedHistoryLine = -1;
        public static DialoguePanel Active => active;
        public DialogueSourceLocation CurrentSource => script != null && LineIndex >= 0 && LineIndex < activeLines.Count ? activeLines[LineIndex].Source : null;
        public static Func<bool> ExternalInputBlocked { get; set; }
        public static bool IsExternalInputBlocked => ExternalInputBlocked?.Invoke() == true;
        public bool ReadingPaused => readingPauses.Count > 0;
        public void SetPreviewReading(float speed, bool typewriter)
        {
            if (script == null || !script.IsPreview) return;
            charactersPerSecond = typewriter ? Mathf.Max(0, speed) : 0;
        }
        public bool IsHistoryOpen => historyRoot != null && historyRoot.activeSelf;
        private bool ReadingInputBlocked => ReadingPaused || InputSuspended || IsExternalInputBlocked || Time.frameCount <= inputBlockedFrame;

        public IDisposable AcquireReadingPause(object owner)
        {
            var token = new ReadingPause(this);
            readingPauses.Add(token);
            IsSkipping = false;
            transition?.Pause();
            effects?.SetPaused(true);
            Spotlight.Audio.GameAudio.SetOwnerPaused(voiceOwner, true);
            Spotlight.Audio.GameAudio.SetOwnerPaused(this, true);
            ResetScroll();
            return token;
        }
        private sealed class ReadingPause : IDisposable
        {
            private DialoguePanel panel;
            public ReadingPause(DialoguePanel panel) { this.panel = panel; }
            public void Dispose()
            {
                if (panel == null) return;
                DialoguePanel value = panel; panel = null;
                value.readingPauses.Remove(this);
                value.inputBlockedFrame = Time.frameCount;
                value.ResetScroll();
                if (value.ReadingPaused) return;
                value.transition?.Play(); value.effects?.SetPaused(false);
                Spotlight.Audio.GameAudio.SetOwnerPaused(value.voiceOwner, false);
                Spotlight.Audio.GameAudio.SetOwnerPaused(value, false);
            }
        }
        private void BeginHistory(DialogueScript value, bool seamless)
        {
            history = value.IsPreview ? previewHistory : DialogueHistoryService.Formal;
            if (value.IsPreview && !seamless) previewHistory.Clear();
            historyPlayback = history.BeginPlayback();
            capturedHistoryCharacters = capturedHistoryLine = -1;
        }
        private void CaptureHistory()
        {
            if (history == null || LineIndex < 0 || dialogueText == null || script == null) return;
            int count = Math.Min(dialogueText.maxVisibleCharacters, dialogueText.textInfo.characterCount);
            if (count <= 0 || (capturedHistoryLine == LineIndex && capturedHistoryCharacters == count)) return;
            capturedHistoryLine = LineIndex; capturedHistoryCharacters = count;
            TMP_CharacterInfo last = dialogueText.textInfo.characterInfo[count - 1];
            int length = Math.Min(dialogueText.text.Length, last.index + last.stringLength);
            history.Reveal(historyPlayback, LineIndex, speakerText.text, dialogueText.text.Substring(0, length));
        }
        private void RecordSelectedOption(string text) { CaptureHistory(); history?.Choice(text); }
        private void ResetScroll() { scrollSum = 0; scrollConsumed = false; lastScrollTime = Time.unscaledTime; }
        private void ReadingInput()
        {
            if (!IsPlaying || InputSuspended || IsExternalInputBlocked) return;
            if (IsHistoryOpen)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                { CloseHistory(); lastCancelFrame = Time.frameCount; }
                return;
            }
            if (ReadingInputBlocked || optionsVisible || Mouse.current == null) return;
            float delta = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Approximately(delta, 0f)) return;
            if (Time.unscaledTime - lastScrollTime > scrollGestureGap) { scrollSum = 0; scrollConsumed = false; }
            lastScrollTime = Time.unscaledTime;
            if (scrollConsumed) return;
            scrollSum += delta;
            // Input System 1.17 normalizes one wheel notch to 1 by default.
            float threshold = InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms
                ? scrollThreshold / 100f : scrollThreshold;
            if (Mathf.Abs(scrollSum) < threshold) return;
            scrollConsumed = true;
            if (scrollSum > 0) OpenHistory(); else Advance();
        }
        private void UpdateControlSkip()
        {
            Keyboard keyboard = Keyboard.current;
            bool held = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
            if (controlSkipping && (!held || ReadingInputBlocked || optionsVisible || !IsPlaying))
            {
                controlSkipping = false;
                IsSkipping = false;
                UpdateHint();
            }
            if (!held || ReadingInputBlocked || optionsVisible || !IsPlaying || !IsReady || IsSkipping) return;
            Skip();
            controlSkipping = IsSkipping;
        }

        private void CreateHistoryView()
        {
            Button entry = ReadingButton(transform, "HistoryButton", "历史", new Vector2(.84f, .27f), new Vector2(.94f, .33f), OpenHistory);
            historyRoot = OptionObject(transform, "DialogueHistory", typeof(Image));
            OptionRect((RectTransform)historyRoot.transform, Vector2.zero, Vector2.one);
            historyRoot.GetComponent<Image>().color = new Color(.035f, .03f, .025f, .98f);
            TMP_Text title = OptionText(historyRoot.transform, "Title", new Vector2(.08f, .88f), new Vector2(.7f, .96f), 32);
            title.text = "对话历史";
            ReadingButton(historyRoot.transform, "Close", "关闭 · Esc", new Vector2(.76f, .88f), new Vector2(.93f, .96f), CloseHistory);
            GameObject viewport = OptionObject(historyRoot.transform, "Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            RectTransform view = (RectTransform)viewport.transform;
            OptionRect(view, new Vector2(.08f, .07f), new Vector2(.92f, .85f));
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, .01f);
            historyLabel = OptionText(view, "Text", Vector2.zero, Vector2.one, 26);
            historyLabel.enableAutoSizing = false; historyLabel.richText = false;
            historyLabel.alignment = TextAlignmentOptions.TopLeft;
            historyLabel.textWrappingMode = TextWrappingModes.Normal;
            historyLabel.overflowMode = TextOverflowModes.Overflow;
            RectTransform content = historyLabel.rectTransform;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            historyScroll = viewport.GetComponent<ScrollRect>();
            historyScroll.viewport = view; historyScroll.content = content; historyScroll.horizontal = false;
            historyScroll.movementType = ScrollRect.MovementType.Clamped; historyScroll.scrollSensitivity = 40;
            historyRoot.SetActive(false);
        }
        private Button ReadingButton(Transform parent, string name, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction click)
        {
            GameObject go = OptionObject(parent, name, typeof(Image), typeof(Button));
            OptionRect((RectTransform)go.transform, min, max);
            go.GetComponent<Image>().color = new Color(.23f, .18f, .13f, 1);
            Button button = go.GetComponent<Button>(); button.onClick.AddListener(click);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            OptionText(go.transform, "Label", Vector2.zero, Vector2.one, 24).text = label;
            return button;
        }
        public void OpenHistory()
        {
            if (!IsPlaying || IsHistoryOpen || ReadingInputBlocked) return;
            CaptureHistory();
            historyPause = AcquireReadingPause(this);
            var text = new StringBuilder();
            foreach (var row in history.Entries) text.Append(row.Speaker).Append('\n').Append(row.Text).Append("\n\n");
            historyLabel.text = text.Length == 0 ? "暂无已读记录" : text.ToString();
            historyRoot.SetActive(true); historyRoot.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(historyLabel.rectTransform);
            historyScroll.verticalNormalizedPosition = 0;
            inputBlockedFrame = Time.frameCount;
        }
        public void CloseHistory()
        {
            if (historyRoot != null) historyRoot.SetActive(false);
            historyScroll?.StopMovement();
            historyPause?.Dispose(); historyPause = null;
            inputBlockedFrame = Time.frameCount; ResetScroll();
        }
        private void StopReading()
        {
            CaptureHistory();
            if (IsHistoryOpen) CloseHistory();
            readingPauses.Clear();
            history = null;
        }
    }
}
