using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    public sealed partial class FirstDayLetterPanel
    {
        private bool genericLetterMode, genericSuspended, genericReachedEnd, genericLayoutReady;
        private LetterReadingSnapshot genericLetter;
        private Action genericCompleted, genericCancelled;
        private IDisposable genericDialoguePause, genericSimulationPause;
        private Coroutine genericClose;
        private Button genericDone;
        private GameObject genericInputShield;
        private int genericInputFrame, genericTitleEnd;
        private float genericSuspendedAlpha;
        private Vector2 genericPaperPosition, genericViewportSize, genericRootSize;
        private long genericHistoryPlayback;
        private bool[] genericSeen;
        private Func<bool> genericPreviousInputBlocked, genericInputBlocked;
        private readonly List<int> genericLineStarts = new List<int>();
        private readonly List<int> genericLineEnds = new List<int>();
        private readonly List<Action> genericRestore = new List<Action>();

        public bool IsGenericLetter => genericLetterMode;
        public bool CanFinishLetter => genericLetterMode && genericReachedEnd && !genericSuspended
            && Stage == LetterStage.Reading;
        public object LetterOwner => genericLetter?.Owner;
        public LetterReadingSnapshot LetterSnapshot => genericLetter;

        /// <summary>Export existing bindings, not duplicate assets or a second letter prefab.</summary>
        public LetterVisualDefaults GetLetterVisualDefaults()
        {
            ValidateBindings();
            Image page = paper.Find("Page")?.GetComponent<Image>();
            return new LetterVisualDefaults(envelopeClosedSprite, envelopeOpenSprite, page != null ? page.sprite : null);
        }

        public bool OwnsLetter(object owner) => genericLetterMode && ReferenceEquals(LetterOwner, owner);

        /// <summary>Only copied presentation data enters this mode; no creation draft or NCS access.</summary>
        public void PlayLetter(LetterReadingSnapshot snapshot, Action completed, Action cancelled = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!snapshot.RevealOnly && snapshot.Lines.Count == 0)
                throw new ArgumentException("Letter read snapshot has no lines.", nameof(snapshot));
            ValidateBindings();
            bool reuse = OwnsLetter(snapshot.Owner);
            Sprite heldEnvelope = reuse && envelopeArt != null ? envelopeArt.sprite : null;
            // A reveal has already acknowledged its step. Reuse must not call its old cancellation delegate.
            if (reuse) genericCancelled = null;
            Stop();
            CapturePose();
            RestorePose();
            genericLetter = snapshot;
            genericLetterMode = true;
            genericCompleted = completed;
            genericCancelled = cancelled;
            genericInputFrame = Time.frameCount;
            genericReachedEnd = genericLayoutReady = genericSuspended = false;
            draft = null;
            hoveredLink = null;
            ConfigureGenericLayout();
            ApplyGenericArt(snapshot);
            FitGenericPaperToSprite();
            if (reuse && string.IsNullOrWhiteSpace(snapshot.EnvelopePath) && heldEnvelope != null && envelopeArt != null)
                envelopeArt.sprite = heldEnvelope;
            rootGroup.alpha = 1;
            rootGroup.blocksRaycasts = rootGroup.interactable = !snapshot.RevealOnly;
            envelopeGroup.gameObject.SetActive(true);
            envelopeGroup.alpha = 1;
            envelopeGroup.blocksRaycasts = envelopeGroup.interactable = !snapshot.RevealOnly;
            envelopeButton.interactable = !snapshot.RevealOnly;
            paperGroup.gameObject.SetActive(false);
            paperGroup.alpha = 0;
            paperGroup.blocksRaycasts = paperGroup.interactable = !snapshot.RevealOnly;
            UIKit.SetPanelLevel(this, UILevel.Pop, snapshot.RevealOnly ? 1 : 2);
            UIKit.SetPanelModal(this, !snapshot.RevealOnly);
            ClearGenericSelection();
            Stage = LetterStage.Envelope;
            IsPlaying = !snapshot.RevealOnly;
            if (snapshot.RevealOnly)
            {
                // The owner keeps the prop until Read/finally. No raycaster, backdrop, prompt, or input lease.
                Action callback = genericCompleted;
                genericCompleted = null;
                callback?.Invoke();
                return;
            }
            genericDialoguePause = DialoguePanel.Active?.AcquireReadingPause(this);
            genericPreviousInputBlocked = DialoguePanel.ExternalInputBlocked;
            genericInputBlocked = () => genericLetterMode && !genericLetter.RevealOnly || genericPreviousInputBlocked?.Invoke() == true;
            DialoguePanel.ExternalInputBlocked = genericInputBlocked;
            genericSimulationPause = PresentationPauseRegistry.Acquire(nameof(FirstDayLetterPanel) + ".LetterRead");
            letterScroll.onValueChanged.AddListener(GenericScrolled);
            readingHint.text = "左键 / Enter / Space：拆信";
            hintGroup.gameObject.SetActive(true);
            TMP_Text prompt = hintGroup.GetComponent<TMP_Text>();
            if (prompt == null) prompt = hintGroup.GetComponentInChildren<TMP_Text>(true);
            if (prompt != null)
            {
                string previous = prompt.text;
                genericRestore.Add(() => { if (prompt != null) prompt.text = previous; });
                prompt.text = "左键 / Enter / Space：拆信";
            }
            hintGroup.alpha = 1;
        }

        private void ConfigureGenericLayout()
        {
            // Restore every changed value when this cached panel returns to the original opening.
            foreach (Transform child in envelope.parent)
                if (child != envelope) HideGenericObject(child.gameObject);
            foreach (Transform child in envelope)
            {
                bool required = envelopeArt != null && (child == envelopeArt.transform || envelopeArt.transform.IsChildOf(child))
                    || child == envelopeButton.transform || envelopeButton.transform.IsChildOf(child);
                if (!required) HideGenericObject(child.gameObject);
            }
            HideGenericObject(transform.Find("Blackout")?.gameObject);
            HideGenericObject(nameEditor != null ? nameEditor.gameObject : null);
            HideGenericObject(signatureButton.gameObject);
            HideGenericObject(skipButton.gameObject);
            HideGenericObject(paper.Find("Title")?.gameObject);
            HideGenericObject(paper.Find("Rule")?.gameObject);
            SaveGenericGroup(envelopeGroup);
            SaveGenericGroup(paperGroup);
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                int oldOrder = canvas.sortingOrder;
                bool oldOverride = canvas.overrideSorting;
                genericRestore.Add(() => { if (canvas != null) { canvas.sortingOrder = oldOrder; canvas.overrideSorting = oldOverride; } });
                // Existing presentation prefabs opt out of root ordering (Dialogue=800, opening letter=700).
                // Keep UIKit's managed levels, but place just this prop/paper above the retained dialogue stage.
                Canvas dialogueCanvas = UIKit.GetPanel<DialoguePanel>()?.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = (dialogueCanvas != null ? dialogueCanvas.sortingOrder : 800) + (genericLetter.RevealOnly ? 1 : 2);
            }
            if (genericInputShield == null)
            {
                genericInputShield = new GameObject("LetterReadInputShield", typeof(RectTransform), typeof(Image));
                genericInputShield.transform.SetParent(transform, false);
                genericInputShield.transform.SetAsFirstSibling();
                StretchGeneric((RectTransform)genericInputShield.transform, Vector2.zero, Vector2.one);
                genericInputShield.GetComponent<Image>().color = Color.clear;
            }
            genericInputShield.SetActive(!genericLetter.RevealOnly);
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                bool enabled = raycaster.enabled;
                genericRestore.Add(() => { if (raycaster != null) raycaster.enabled = enabled; });
                raycaster.enabled = !genericLetter.RevealOnly;
            }
            SaveRect(envelope);
            SaveRect(paper);
            SaveRect(letterScroll.transform as RectTransform);
            SaveRect(readingHint.rectTransform);
            RectTransform root = transform as RectTransform;
            float width = root != null && root.rect.width > 0 ? root.rect.width : 1920;
            float height = root != null && root.rect.height > 0 ? root.rect.height : 1080;
            float propWidth = Mathf.Min(width * .32f, height * .24f * 650f / 360f);
            envelope.anchorMin = envelope.anchorMax = envelope.pivot = new Vector2(.5f, .5f);
            envelope.anchoredPosition = Vector2.zero;
            envelope.sizeDelta = new Vector2(propWidth, propWidth * 360f / 650f);
            // Bounds are relative to the fitted sprite, not the wider screen-sized paper root.
            // Leave the baked-in heading and the lower ornamental border clear.
            StretchGeneric(letterScroll.transform as RectTransform, new Vector2(.12f, .22f), new Vector2(.88f, .74f));
            StretchGeneric(readingHint.rectTransform, new Vector2(.12f, .12f), new Vector2(.65f, .18f));
            string body = letterBody.text;
            bool rich = letterBody.richText, autosize = letterBody.enableAutoSizing;
            float size = letterBody.fontSize;
            TextOverflowModes overflow = letterBody.overflowMode;
            TextWrappingModes wrap = letterBody.textWrappingMode;
            genericRestore.Add(() =>
            {
                if (letterBody == null) return;
                letterBody.text = body; letterBody.richText = rich; letterBody.enableAutoSizing = autosize;
                letterBody.fontSize = size; letterBody.overflowMode = overflow; letterBody.textWrappingMode = wrap;
            });
            letterBody.richText = true;
            letterBody.enableAutoSizing = false;
            letterBody.fontSize = Mathf.Max(30, size);
            letterBody.textWrappingMode = TextWrappingModes.Normal;
            letterBody.overflowMode = TextOverflowModes.Overflow;
            letterBody.maxVisibleCharacters = int.MaxValue;
            letterBody.text = BuildGenericBody();
            if (genericDone == null)
            {
                // One small control inside the existing paper; never a new Canvas or independent prefab.
                var go = new GameObject("LetterReadDone", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(paper, false);
                StretchGeneric((RectTransform)go.transform, new Vector2(.7f, .12f), new Vector2(.88f, .18f));
                go.GetComponent<Image>().color = new Color(.24f, .18f, .12f, .95f);
                genericDone = go.GetComponent<Button>();
                genericDone.navigation = new Navigation { mode = Navigation.Mode.None };
                genericDone.onClick.AddListener(FinishReadingLetter);
                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(go.transform, false);
                StretchGeneric((RectTransform)label.transform, Vector2.zero, Vector2.one);
                var text = label.GetComponent<TextMeshProUGUI>();
                text.font = readingHint.font; text.fontSize = 25; text.color = Color.white;
                text.alignment = TextAlignmentOptions.Center; text.text = "读完"; text.raycastTarget = false;
            }
            genericDone.gameObject.SetActive(false);
        }

        private void FitGenericPaperToSprite()
        {
            Image page = paper.Find("Page")?.GetComponent<Image>();
            RectTransform root = transform as RectTransform;
            float width = root != null && root.rect.width > 0 ? root.rect.width : 1920;
            float height = root != null && root.rect.height > 0 ? root.rect.height : 1080;
            float aspect = page != null && page.sprite != null && page.sprite.rect.height > 0
                ? page.sprite.rect.width / page.sprite.rect.height
                : Mathf.Max(1, paper.sizeDelta.x) / Mathf.Max(1, paper.sizeDelta.y);
            float fittedWidth = Mathf.Min(width * .8f, height * .8f * aspect);
            paper.anchorMin = paper.anchorMax = paper.pivot = new Vector2(.5f, .5f);
            paper.anchoredPosition = Vector2.zero;
            paper.sizeDelta = new Vector2(fittedWidth, fittedWidth / aspect);
            genericPaperPosition = paper.anchoredPosition;
            genericRootSize = new Vector2(width, height);
            // Page keeps its original preserveAspect setting; SaveRect(paper) restores the opening layout.
        }

        private string BuildGenericBody()
        {
            var text = new StringBuilder();
            genericLineStarts.Clear(); genericLineEnds.Clear();
            if (!string.IsNullOrEmpty(genericLetter.Title))
                text.Append(LetterReadingSnapshot.LayoutText(genericLetter.Title)).Append("\n\n");
            genericTitleEnd = text.Length;
            for (int i = 0; i < genericLetter.Lines.Count; i++)
            {
                if (i > 0) text.Append('\n');
                genericLineStarts.Add(text.Length);
                text.Append(LetterReadingSnapshot.LayoutText(genericLetter.Lines[i].Text));
                genericLineEnds.Add(text.Length);
            }
            return text.ToString();
        }

        private void ApplyGenericArt(LetterReadingSnapshot snapshot)
        {
            if (envelopeArt != null)
            {
                Sprite original = envelopeArt.sprite;
                genericRestore.Add(() => { if (envelopeArt != null) envelopeArt.sprite = original; });
                if (!string.IsNullOrWhiteSpace(snapshot.EnvelopePath))
                    envelopeArt.sprite = LoadGenericSprite(snapshot.EnvelopePath);
                else if (envelopeClosedSprite != null) envelopeArt.sprite = envelopeClosedSprite;
            }
            if (string.IsNullOrWhiteSpace(snapshot.ImagePath)) return;
            Image page = paper.Find("Page")?.GetComponent<Image>();
            if (page == null) throw new InvalidOperationException("Original letter paper sprite binding is missing.");
            Sprite originalPage = page.sprite;
            genericRestore.Add(() => { if (page != null) page.sprite = originalPage; });
            page.sprite = LoadGenericSprite(snapshot.ImagePath);
        }

        private static Sprite LoadGenericSprite(string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Letter sprite Resources path not found: " + path);
            return sprite;
        }

        private void OpenGenericEnvelope()
        {
            if (!genericLetterMode || genericLetter.RevealOnly || genericSuspended || !IsPlaying
                || Stage != LetterStage.Envelope || genericInputFrame == Time.frameCount) return;
            genericInputFrame = Time.frameCount;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Envelope, this);
            Stage = LetterStage.Opening;
            envelopeButton.interactable = false;
            if (envelopeArt != null && envelopeOpenSprite != null) envelopeArt.sprite = envelopeOpenSprite;
            hintGroup.alpha = 0;
            paperGroup.gameObject.SetActive(true);
            paper.anchoredPosition = genericPaperPosition + Vector2.down * paperRise;
            paper.localScale = paperScale * .96f;
            letterBody.maxVisibleCharacters = int.MaxValue;
            Canvas.ForceUpdateCanvases();
            letterBody.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(letterScroll.content);
            letterScroll.verticalNormalizedPosition = 1;
            transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            transition.Append(DOTween.To(() => paper.anchoredPosition, p => paper.anchoredPosition = p, genericPaperPosition, openSeconds).SetEase(Ease.OutCubic));
            transition.Join(paper.DOScale(paperScale, openSeconds).SetEase(Ease.OutCubic));
            transition.Join(DOTween.To(() => paperGroup.alpha, a => paperGroup.alpha = a, 1, openSeconds));
            transition.Join(DOTween.To(() => envelopeGroup.alpha, a => envelopeGroup.alpha = a, 0, openSeconds * .6f));
            transition.Join(envelope.DOScale(envelopeScale * .92f, openSeconds));
            transition.OnComplete(() =>
            {
                transition = null;
                if (!genericLetterMode || !IsPlaying || Stage != LetterStage.Opening) return;
                envelopeGroup.gameObject.SetActive(false);
                Stage = LetterStage.Reading;
                genericInputFrame = Time.frameCount;
                Canvas.ForceUpdateCanvases(); letterBody.ForceMeshUpdate();
                LayoutRebuilder.ForceRebuildLayoutImmediate(letterScroll.content);
                genericSeen = new bool[letterBody.textInfo.characterCount];
                genericLayoutReady = true;
                genericViewportSize = letterScroll.viewport.rect.size;
                if (!genericLetter.IsPreview) genericHistoryPlayback = DialogueHistoryService.Formal.BeginPlayback();
                RefreshGenericReading();
            });
        }

        private void UpdateGenericLetter()
        {
            if (genericSuspended || genericLetter == null || genericLetter.RevealOnly || !IsPlaying) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && genericLetter.AllowCancel)
            { CancelLetter(); return; } // Formal ESC remains the project's pause command.
            if (Stage != LetterStage.Envelope && Stage != LetterStage.Reading) return;
            if (transform is RectTransform root && root.rect.size != genericRootSize)
                FitGenericPaperToSprite();
            if (Stage == LetterStage.Reading && letterScroll.viewport.rect.size != genericViewportSize)
            {
                genericViewportSize = letterScroll.viewport.rect.size;
                Canvas.ForceUpdateCanvases(); letterBody.ForceMeshUpdate();
                LayoutRebuilder.ForceRebuildLayoutImmediate(letterScroll.content);
                RefreshGenericReading();
            }
            bool submit = keyboard != null && (keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
            bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (Stage == LetterStage.Envelope && (submit || click)) OpenGenericEnvelope();
            else if (Stage == LetterStage.Reading && submit && CanFinishLetter) FinishReadingLetter();
        }

        private void GenericScrolled(Vector2 _) => RefreshGenericReading();

        private void RefreshGenericReading()
        {
            if (!genericLayoutReady || genericSuspended || Stage != LetterStage.Reading) return;
            CaptureGenericHistory();
            bool overflow = letterScroll.content.rect.height > letterScroll.viewport.rect.height + .5f;
            if (!overflow || letterScroll.verticalNormalizedPosition <= .001f) genericReachedEnd = true;
            genericDone.gameObject.SetActive(genericReachedEnd);
            genericDone.interactable = CanFinishLetter;
            readingHint.text = genericReachedEnd ? "读完 · Enter / Space" : "滚轮 / 拖动：向下阅读，读到底后可读完";
        }

        private void CaptureGenericHistory()
        {
            if (genericLetter.IsPreview || genericSeen == null || !genericLayoutReady || paperGroup.alpha < .999f) return;
            TMP_TextInfo info = letterBody.textInfo;
            if (genericSeen.Length != info.characterCount) return;
            RectTransform viewport = letterScroll.viewport;
            var changed = new bool[genericLetter.Lines.Count];
            bool titleChanged = false;
            for (int i = 0; i < info.characterCount; i++)
            {
                if (genericSeen[i]) continue;
                TMP_CharacterInfo c = info.characterInfo[i];
                Vector3 top = viewport.InverseTransformPoint(letterBody.transform.TransformPoint(new Vector3(c.origin, c.ascender, 0)));
                Vector3 bottom = viewport.InverseTransformPoint(letterBody.transform.TransformPoint(new Vector3(c.xAdvance, c.descender, 0)));
                if (top.y > viewport.rect.yMax + .5f || bottom.y < viewport.rect.yMin - .5f) continue;
                genericSeen[i] = true;
                if (c.index < genericTitleEnd) titleChanged = true;
                for (int line = 0; line < changed.Length; line++)
                    if (c.index >= genericLineStarts[line] && c.index < genericLineEnds[line]) { changed[line] = true; break; }
            }
            // History gets only characters that actually entered the viewport, never hidden rows on Play.
            if (titleChanged)
            {
                var seenTitle = new StringBuilder();
                for (int i = 0; i < info.characterCount && info.characterInfo[i].index < genericTitleEnd; i++)
                    if (genericSeen[i]) seenTitle.Append(info.characterInfo[i].character);
                DialogueHistoryService.Formal.Reveal(genericHistoryPlayback, -1, string.Empty, seenTitle.ToString().TrimEnd());
            }
            for (int line = 0; line < changed.Length; line++)
            {
                if (!changed[line]) continue;
                var seenText = new StringBuilder();
                bool gap = false;
                for (int i = 0; i < info.characterCount; i++)
                {
                    TMP_CharacterInfo c = info.characterInfo[i];
                    if (c.index < genericLineStarts[line] || c.index >= genericLineEnds[line]) continue;
                    if (!genericSeen[i]) { gap = seenText.Length > 0; continue; }
                    if (gap) { seenText.Append('…'); gap = false; }
                    seenText.Append(c.character);
                }
                DialogueHistoryService.Formal.Reveal(genericHistoryPlayback, line, genericLetter.Lines[line].Speaker, seenText.ToString());
            }
        }

        public void FinishReadingLetter()
        {
            if (!CanFinishLetter || genericInputFrame == Time.frameCount) return;
            genericInputFrame = Time.frameCount;
            CaptureGenericHistory();
            Stage = LetterStage.Closing;
            genericDone.interactable = false;
            ClearGenericSelection();
            transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            transition.Append(DOTween.To(() => rootGroup.alpha, a => rootGroup.alpha = a, 0, closeSeconds));
            transition.OnComplete(() =>
            {
                transition = null;
                if (!genericLetterMode || Stage != LetterStage.Closing) return;
                genericClose = StartCoroutine(CompleteGenericOnFreshFrame());
            });
        }

        private IEnumerator CompleteGenericOnFreshFrame()
        {
            // Even zero-duration previews cannot submit to the resumed dialogue on the closing frame.
            yield return null;
            while (genericSuspended) yield return null;
            genericClose = null;
            Action callback = genericCompleted;
            genericCompleted = genericCancelled = null;
            Stop();
            Stage = LetterStage.Finished;
            callback?.Invoke();
        }

        public void CancelLetter()
        {
            if (!genericLetterMode || genericSuspended || !genericLetter.AllowCancel || Stage == LetterStage.Closing) return;
            genericCompleted = null;
            Stage = LetterStage.Closing;
            rootGroup.alpha = 0;
            genericClose = StartCoroutine(CancelGenericOnFreshFrame());
        }

        private IEnumerator CancelGenericOnFreshFrame()
        {
            yield return null;
            genericClose = null;
            Stop();
        }

        /// <summary>Pause hides only this overlay, keeping the underlying night stage intact.</summary>
        public void SetSuspended(bool suspended)
        {
            if (!genericLetterMode || genericSuspended == suspended) return;
            genericSuspended = suspended;
            if (suspended) { genericSuspendedAlpha = rootGroup.alpha; rootGroup.alpha = 0; }
            else rootGroup.alpha = genericSuspendedAlpha;
            bool interactive = !suspended && !genericLetter.RevealOnly && Stage != LetterStage.Closing;
            rootGroup.blocksRaycasts = rootGroup.interactable = interactive;
            UIKit.SetPanelModal(this, interactive);
            if (suspended) transition?.Pause(); else transition?.Play();
            Spotlight.Audio.GameAudio.SetOwnerPaused(this, suspended);
            if (suspended) letterScroll.StopMovement();
            else { genericInputFrame = Time.frameCount; ClearGenericSelection(); RefreshGenericReading(); }
        }

        private Action StopGenericLetter()
        {
            if (!genericLetterMode) return null;
            Action cancel = genericCancelled;
            genericCompleted = genericCancelled = null;
            genericLetterMode = false;
            genericSuspended = genericLayoutReady = genericReachedEnd = false;
            if (genericClose != null) StopCoroutine(genericClose);
            genericClose = null;
            letterScroll.onValueChanged.RemoveListener(GenericScrolled);
            genericDialoguePause?.Dispose(); genericDialoguePause = null;
            if (ReferenceEquals(DialoguePanel.ExternalInputBlocked, genericInputBlocked))
                DialoguePanel.ExternalInputBlocked = genericPreviousInputBlocked;
            genericPreviousInputBlocked = genericInputBlocked = null;
            genericSimulationPause?.Dispose(); genericSimulationPause = null;
            genericSeen = null; genericHistoryPlayback = 0;
            genericLetter = null;
            if (genericDone != null) genericDone.gameObject.SetActive(false);
            if (genericInputShield != null) genericInputShield.SetActive(false);
            for (int i = genericRestore.Count - 1; i >= 0; i--) genericRestore[i]();
            genericRestore.Clear();
            rootGroup.alpha = 0;
            UIKit.SetPanelModal(this, false);
            return cancel;
        }

        private void HideGenericObject(GameObject target)
        {
            if (target == null) return;
            bool active = target.activeSelf;
            genericRestore.Add(() => { if (target != null) target.SetActive(active); });
            target.SetActive(false);
        }

        private void SaveGenericGroup(CanvasGroup group)
        {
            bool interactive = group.interactable, blocks = group.blocksRaycasts;
            genericRestore.Add(() => { if (group != null) { group.interactable = interactive; group.blocksRaycasts = blocks; } });
        }

        private void SaveRect(RectTransform target)
        {
            if (target == null) return;
            Vector2 min = target.anchorMin, max = target.anchorMax, pivot = target.pivot, pos = target.anchoredPosition, size = target.sizeDelta;
            Vector3 scale = target.localScale;
            genericRestore.Add(() =>
            {
                if (target == null) return;
                target.anchorMin = min; target.anchorMax = max; target.pivot = pivot;
                target.anchoredPosition = pos; target.sizeDelta = size; target.localScale = scale;
            });
        }

        private static void StretchGeneric(RectTransform target, Vector2 min, Vector2 max)
        {
            target.anchorMin = min; target.anchorMax = max; target.pivot = new Vector2(.5f, .5f);
            target.offsetMin = target.offsetMax = Vector2.zero;
        }

        private static void ClearGenericSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public sealed class LetterVisualDefaults
    {
        public Sprite EnvelopeClosed { get; }
        public Sprite EnvelopeOpen { get; }
        public Sprite Paper { get; }
        public LetterVisualDefaults(Sprite envelopeClosed, Sprite envelopeOpen, Sprite paper)
        { EnvelopeClosed = envelopeClosed; EnvelopeOpen = envelopeOpen; Paper = paper; }
    }
}
