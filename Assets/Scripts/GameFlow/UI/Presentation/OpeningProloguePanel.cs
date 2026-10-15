using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>独立的新游戏开场序章；不依赖 NCS、DayFlow 或存档。</summary>
    public sealed class OpeningProloguePanel : UIPanel, IDayFlowPresentation
    {
        [Serializable]
        public sealed class Beat
        {
            [TextArea(2, 8)] public string text;
            [Min(0f)] public float fadeInSeconds = .8f;
            [Min(0f)] public float fadeOutSeconds = .5f;
        }

        [Header("Prefab bindings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text narrationText;
        [SerializeField] private Button advanceArea;
        [SerializeField] private Button skipButton;
        [Header("Serialized prologue content")]
        [SerializeField] private Beat[] beats = Array.Empty<Beat>();
        [SerializeField, Min(0f)] private float defaultFadeInSeconds = .8f;
        [SerializeField, Min(0f)] private float defaultFadeOutSeconds = .5f;
        [SerializeField, Min(0f)] private float finalBlackHoldSeconds;

        private Coroutine playback;
        private Tween textTween;
        private Action finished;
        private bool acceptingAdvance;
        private bool completing;
        private int beatIndex;
        private float savedTimeScale;
        private bool ownsTimeScale;

        public bool IsPlaying { get; private set; }
        public int BeatCount => beats?.Length ?? 0;
        public int CurrentBeatIndex => beatIndex;
        public bool CanAdvance => IsPlaying && acceptingAdvance;

        protected override void OnOpen(IUIData data = null)
        {
            Stop();
            base.OnOpen(data);
            UIKit.SetPanelModal(this, true);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            if (background != null) background.color = Color.black;
            if (narrationText != null) narrationText.text = string.Empty;
            if (skipButton != null) skipButton.gameObject.SetActive(true);
        }

        protected override void OnShow()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
        }

        protected override void OnInit(IUIData data = null)
        {
            Spotlight.Audio.UIButtonAudio.Install(gameObject);
            if (advanceArea != null) advanceArea.onClick.AddListener(Continue);
            if (skipButton != null) skipButton.onClick.AddListener(Skip);
        }

        public void Play(Action onFinished)
        {
            Stop();
            if (canvasGroup == null || background == null || narrationText == null || advanceArea == null || skipButton == null)
                throw new InvalidOperationException("OpeningProloguePanel has incomplete prefab bindings.");
            savedTimeScale = Time.timeScale;
            ownsTimeScale = true;
            IsPlaying = true;
            completing = false;
            acceptingAdvance = false;
            beatIndex = 0;
            finished = onFinished;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            advanceArea.interactable = true;
            skipButton.gameObject.SetActive(true);
            skipButton.interactable = true;
            narrationText.alpha = 0f;
            narrationText.text = string.Empty;
            playback = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            if (beats == null || beats.Length == 0) { Finish(); yield break; }
            while (beatIndex < beats.Length && IsPlaying)
            {
                Beat beat = beats[beatIndex];
                narrationText.text = beat == null ? string.Empty : beat.text ?? string.Empty;
                narrationText.alpha = 0f;
                acceptingAdvance = false;
                float fadeIn = beat == null || beat.fadeInSeconds <= 0f ? defaultFadeInSeconds : beat.fadeInSeconds;
                textTween = DOTween.To(() => narrationText.alpha, value => narrationText.alpha = value, 1f, fadeIn)
                    .SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
                yield return textTween.WaitForCompletion();
                textTween = null;
                if (!IsPlaying) yield break;
                acceptingAdvance = true;
                while (IsPlaying && acceptingAdvance) yield return null;
                if (!IsPlaying) yield break;
                float fadeOut = beat == null || beat.fadeOutSeconds <= 0f ? defaultFadeOutSeconds : beat.fadeOutSeconds;
                textTween = DOTween.To(() => narrationText.alpha, value => narrationText.alpha = value, 0f, fadeOut)
                    .SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
                yield return textTween.WaitForCompletion();
                textTween = null;
                if (!IsPlaying) yield break;
                beatIndex++;
            }
            if (IsPlaying && finalBlackHoldSeconds > 0f)
                yield return new WaitForSecondsRealtime(finalBlackHoldSeconds);
            if (IsPlaying) Finish();
        }

        private void Update()
        {
            if (!IsPlaying) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) Continue();
        }

        public void Continue()
        {
            if (!CanAdvance) return;
            acceptingAdvance = false;
        }

        public void Skip()
        {
            if (!IsPlaying || completing) return;
            Finish();
        }

        private void Finish()
        {
            if (!IsPlaying || completing) return;
            completing = true;
            IsPlaying = false;
            acceptingAdvance = false;
            if (playback != null) { StopCoroutine(playback); playback = null; }
            textTween?.Kill(false);
            textTween = null;
            if (narrationText != null) { narrationText.alpha = 0f; narrationText.text = string.Empty; }
            if (canvasGroup != null) { canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }
            if (skipButton != null) skipButton.interactable = false;
            if (ownsTimeScale) { Time.timeScale = savedTimeScale; ownsTimeScale = false; }
            Action callback = finished;
            finished = null;
            callback?.Invoke();
        }

        public void Stop()
        {
            IsPlaying = false;
            completing = false;
            acceptingAdvance = false;
            if (playback != null) { StopCoroutine(playback); playback = null; }
            textTween?.Kill(false);
            textTween = null;
            finished = null;
            if (narrationText != null) { narrationText.alpha = 0f; narrationText.text = string.Empty; }
            if (canvasGroup != null) { canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }
            if (skipButton != null) skipButton.interactable = false;
            if (ownsTimeScale) { Time.timeScale = savedTimeScale; ownsTimeScale = false; }
        }

        protected override void OnClose() => Stop();
        protected override void OnWillHide() => Stop();
        protected override void OnBeforeDestroy()
        {
            Stop();
            if (advanceArea != null) advanceArea.onClick.RemoveListener(Continue);
            if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
            base.OnBeforeDestroy();
        }
        private void OnDisable() => Stop();
    }
}
