using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed partial class DialoguePanel
    {
        private bool endingSettlementMode, endingSettlementBound;
        private string endingSettlementText = string.Empty;
        private string[] endingSettlementSegments = Array.Empty<string>();
        private int endingSettlementSegment;
        private Action endingSettlementCompleted;
        public event Action SettlementChanged;
        public string SettlementText => endingSettlementMode ? endingSettlementText : string.Empty;
        public bool IsEndingSettlement => endingSettlementMode;
        public int SettlementPageCount => dialogueText != null ? dialogueText.textInfo.pageCount : 0;
        public int SettlementSegmentCount => endingSettlementSegments.Length;
        public int SettlementSegmentIndex => endingSettlementSegment;
        public bool IsSettlementTyping => endingSettlementMode && dialogueText != null && dialogueText.maxVisibleCharacters < pageEnd;
        public bool IsSettlementLastPage => endingSettlementMode
            && endingSettlementSegment == endingSettlementSegments.Length - 1 && page >= SettlementPageCount;

        /// <summary>Embedded ResultPanel view: reuse the real prefab, typography and TMP pagination.
        /// Does not open a narrative session, acquire a pause owner, append history or settle facts.</summary>
        public void ShowEndingSettlement(string text, Action completed = null)
        {
            if (IsPlaying) throw new InvalidOperationException("An active dialogue cannot become an ending settlement view.");
            endingSettlementMode = true;
            endingSettlementCompleted = completed;
            endingSettlementText = text ?? string.Empty;
            var segments = new System.Collections.Generic.List<string>();
            foreach (string paragraph in endingSettlementText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                if (!string.IsNullOrWhiteSpace(paragraph)) segments.Add(paragraph.Trim());
            if (segments.Count == 0) segments.Add(string.Empty);
            endingSettlementSegments = segments.ToArray();
            endingSettlementSegment = 0;
            lastAdvanceFrame = Time.frameCount;
            if (!endingSettlementBound)
            {
                if (continueButton != null) continueButton.onClick.AddListener(AdvanceEndingSettlement);
                if (dialogueBodyButton != null) dialogueBodyButton.onClick.AddListener(AdvanceEndingSettlement);
                endingSettlementBound = true;
            }
            // Preserve DialogueBox and all its authored art/text children. Only its scene/portrait
            // siblings and the full-screen raycast blocker are hidden in this embedded instance.
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                child.gameObject.SetActive(child == artworkRoot || child == dialogueBox || dialogueBox.IsChildOf(child));
            }
            if (artworkRoot != null)
                for (int i = 0; i < artworkRoot.childCount; i++)
                {
                    var child = artworkRoot.GetChild(i);
                    child.gameObject.SetActive(child == dialogueBox || dialogueBox.IsChildOf(child));
                }
            if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.blocksRaycasts = canvasGroup.interactable = true; }
            if (speakerText != null) speakerText.text = "终章";
            if (skipButton != null) skipButton.gameObject.SetActive(false);
            if (historyButton != null) historyButton.gameObject.SetActive(false);
            if (cancelButton != null) cancelButton.gameObject.SetActive(false);
            if (passiveHintText != null) passiveHintText.gameObject.SetActive(false);
            if (hintText != null) hintText.gameObject.SetActive(false);
            if (dialogueBox != null) dialogueBox.anchoredPosition = boxPosition;
            script = new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Narrator, endingSettlementText) }, isPreview: true);
            activeLines.Clear(); activeLines.Add(script[0]); LineIndex = 0;
            dialogueText.richText = false; dialogueText.overflowMode = TextOverflowModes.Page;
            ShowEndingSegment();
        }

        private void ShowEndingSegment()
        {
            dialogueText.text = endingSettlementSegments[endingSettlementSegment];
            dialogueText.maxVisibleCharacters = int.MaxValue;
            dialogueText.pageToDisplay = 1;
            Canvas.ForceUpdateCanvases();
            dialogueText.ForceMeshUpdate();
            ShowEndingPage(1);
        }

        private void ShowEndingPage(int number)
        {
            ShowPage(number);
            revealed = charactersPerSecond <= 0f ? pageEnd : pageStart;
            dialogueText.maxVisibleCharacters = (int)revealed;
            if (continueButton != null) { continueButton.gameObject.SetActive(true); continueButton.interactable = true; }
            SettlementChanged?.Invoke();
        }

        public void AdvanceEndingSettlement()
        {
            if (!endingSettlementMode || ReadingInputBlocked || lastAdvanceFrame == Time.frameCount
                || canvasGroup != null && !canvasGroup.interactable) return;
            lastAdvanceFrame = Time.frameCount;
            if (IsSettlementTyping)
            {
                revealed = pageEnd;
                dialogueText.maxVisibleCharacters = pageEnd;
                SettlementChanged?.Invoke();
                return;
            }
            if (page < SettlementPageCount) { ShowEndingPage(page + 1); return; }
            if (endingSettlementSegment + 1 < endingSettlementSegments.Length)
            {
                endingSettlementSegment++;
                ShowEndingSegment();
                return;
            }
            Action callback = endingSettlementCompleted;
            endingSettlementCompleted = null;
            callback?.Invoke();
        }

        private void UpdateEndingSettlement()
        {
            if (ReadingInputBlocked || canvasGroup != null && !canvasGroup.interactable) return;
            if (IsSettlementTyping)
            {
                revealed += Time.unscaledDeltaTime * charactersPerSecond;
                dialogueText.maxVisibleCharacters = charactersPerSecond <= 0f ? pageEnd : Math.Min(pageEnd, (int)revealed);
                if (!IsSettlementTyping) SettlementChanged?.Invoke();
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame)) AdvanceEndingSettlement();
        }
    }
}
