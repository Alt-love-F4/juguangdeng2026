using System;
using System.IO;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>Video shell only; caller resolves assets. No narrative scheduler or event settlement.</summary>
    public sealed class CutScenePanel : UIPanel, ICutScenePresentation
    {
        [Header("Prefab bindings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text placeholderText;
        [SerializeField] private TMP_Text skipText;
        [SerializeField] private TMP_Text skipPercentText;
        [SerializeField] private Slider skipProgress;
        [SerializeField] private Button cancelButton;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField, Min(0f)] private float enterSeconds = .2f;
        [SerializeField, Min(0f)] private float exitSeconds = .15f;
        [SerializeField, Min(1f)] private float prepareTimeoutSeconds = 15f;

        private IDisposable pauseOwner;
        private Sequence transition;
        private Action finished;
        private Action cancelled;
        private CutSceneData data;
        private RenderTexture runtimeTexture;
        private bool ready;
        private bool exiting;
        private bool waitingForVideo;
        private float heldSeconds;
        private float preparingSeconds;
        private float skipHoldSeconds;

        public bool IsPlaying { get; private set; }
        public bool WasSkipped { get; private set; }
        public string LastError { get; private set; }
        public float SkipProgress => skipHoldSeconds <= 0f ? 0f : Mathf.Clamp01(heldSeconds / skipHoldSeconds);
        /// <summary>Optional diagnostics; completion and cancellation callbacks remain mutually exclusive.</summary>
        public event Action<string> PlaybackFailed;

        protected override void OnInit(IUIData data = null)
        {
            if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
            if (videoPlayer != null)
            {
                videoPlayer.playOnAwake = false;
                videoPlayer.isLooping = false;
                videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                videoPlayer.prepareCompleted += OnVideoPrepared;
                videoPlayer.loopPointReached += OnVideoEnded;
                videoPlayer.errorReceived += OnVideoError;
            }
        }

        protected override void OnOpen(IUIData data = null) { Stop(); UIKit.SetPanelModal(this, true); }
        protected override void OnShow()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
        }

        public void Play(CutSceneData value, Action onFinished = null, Action onCancelled = null)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (canvasGroup == null || videoPlayer == null || videoImage == null)
                throw new InvalidOperationException("CutScenePanel has incomplete prefab bindings.");
            if (!value.PreviewOnly && value.Clip == null && string.IsNullOrWhiteSpace(value.videoPath))
                throw new ArgumentException("A videoPath or VideoClip is required. PreviewOnly is for debug placeholders.", nameof(value));
            Stop();
            pauseOwner = PresentationPauseRegistry.Acquire(nameof(CutScenePanel));
            try
            {
            data = value;
            finished = onFinished;
            cancelled = onCancelled;
            WasSkipped = false;
            LastError = null;
            skipHoldSeconds = value.SkipHoldSeconds > 0f ? value.SkipHoldSeconds : CutScenePresentationSettings.SkipHoldSeconds;
            IsPlaying = true;
            var system = UnityEngine.EventSystems.EventSystem.current;
            if (system != null) system.SetSelectedGameObject(null);
            if (titleText != null) titleText.text = value.Title;
            if (skipText != null) { skipText.text = value.SkipText; skipText.gameObject.SetActive(value.AllowSkip); }
            if (skipProgress != null) { skipProgress.gameObject.SetActive(value.AllowSkip); skipProgress.value = 0f; }
            if (skipPercentText != null) { skipPercentText.gameObject.SetActive(value.AllowSkip); skipPercentText.text = "0%"; }
            videoImage.enabled = false;
            ShowMessage(value.PreviewOnly ? "视频过场 · 调试占位\n\n传入 VideoClip 或 videoPath 即可播放真实视频\n长按 Enter 测试跳过；Esc 取消" : "正在准备视频…");
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = canvasGroup.interactable = true;
            transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            transition.Append(DOTween.To(() => canvasGroup.alpha, v => canvasGroup.alpha = v, 1f, enterSeconds));
            transition.OnComplete(() =>
            {
                transition = null;
                ready = true;
                if (data != null && !data.PreviewOnly) PrepareVideo();
            });
            }
            catch { Stop(); throw; }
        }

        private void PrepareVideo()
        {
            try
            {
                videoPlayer.clip = null;
                videoPlayer.url = string.Empty;
                if (data.Clip != null) { videoPlayer.source = VideoSource.VideoClip; videoPlayer.clip = data.Clip; }
                else
                {
                    videoPlayer.source = VideoSource.Url;
                    string path = data.videoPath;
                    // Relative paths belong to StreamingAssets. Never interpret this as a Resources key.
                    if (!Uri.TryCreate(path, UriKind.Absolute, out _) && !Path.IsPathRooted(path))
                        path = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + path;
                    videoPlayer.url = path.Replace('\\', '/');
                }
                waitingForVideo = true;
                preparingSeconds = 0f;
                videoPlayer.Prepare();
            }
            catch (Exception error) { Fail(error.Message); }
        }

        private void OnVideoPrepared(VideoPlayer source)
        {
            if (!IsPlaying || exiting || !waitingForVideo) return;
            waitingForVideo = false;
            int width = Mathf.Clamp((int)source.width, 16, 3840);
            int height = Mathf.Clamp((int)source.height, 16, 2160);
            runtimeTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            { name = "CutScenePanelRuntimeTexture", useMipMap = false, autoGenerateMips = false };
            runtimeTexture.Create();
            source.targetTexture = runtimeTexture;
            videoImage.texture = runtimeTexture;
            videoImage.enabled = true;
            var fitter = videoImage.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.aspectRatio = width / (float)height;
            if (placeholderText != null) placeholderText.gameObject.SetActive(false);
            source.Play();
        }

        private void Update()
        {
            if (!IsPlaying || exiting) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            if (waitingForVideo)
            {
                preparingSeconds += Time.unscaledDeltaTime;
                if (preparingSeconds >= prepareTimeoutSeconds) { Fail("视频准备超时"); return; }
            }
            bool holding = keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed);
            UpdateSkipHold(holding, Time.unscaledDeltaTime);
        }

        /// <summary>Input adapter boundary: supports future gamepad/touch hold without an NCS dependency.</summary>
        public void UpdateSkipHold(bool holding, float unscaledDeltaTime)
        {
            if (!IsPlaying || !ready || exiting || data == null || !data.AllowSkip) return;
            heldSeconds = holding ? heldSeconds + Mathf.Max(0f, unscaledDeltaTime) : 0f;
            float ratio = SkipProgress;
            if (skipProgress != null) skipProgress.value = ratio;
            if (skipPercentText != null) skipPercentText.text = Mathf.RoundToInt(ratio * 100f) + "%";
            if (ratio >= 1f) { WasSkipped = true; Finish(); }
        }

        private void OnVideoEnded(VideoPlayer source) { if (IsPlaying && !exiting) Finish(); }
        private void OnVideoError(VideoPlayer source, string message) { if (IsPlaying && !exiting) Fail(message); }
        private void Fail(string message)
        {
            if (!IsPlaying || exiting) return;
            LastError = message;
            Debug.LogWarning("[CutScenePanel] " + message, this);
            Action<string> failed = PlaybackFailed;
            Cancel();
            failed?.Invoke(message);
        }

        private void ShowMessage(string message)
        {
            if (placeholderText == null) return;
            placeholderText.text = message;
            placeholderText.gameObject.SetActive(true);
        }

        private void Finish()
        {
            if (!IsPlaying || exiting) return;
            exiting = true;
            waitingForVideo = false;
            videoPlayer.Stop();
            transition?.Kill(false);
            transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            transition.Append(DOTween.To(() => canvasGroup.alpha, v => canvasGroup.alpha = v, 0f, exitSeconds));
            transition.OnComplete(() => { Action callback = finished; Stop(); callback?.Invoke(); });
        }

        public void Cancel()
        {
            if (!IsPlaying) return;
            Action callback = cancelled;
            Stop();
            CloseSelf();
            callback?.Invoke();
        }

        public void Stop()
        {
            pauseOwner?.Dispose();
            pauseOwner = null;
            finished = cancelled = null;
            IsPlaying = ready = exiting = waitingForVideo = false;
            transition?.Kill(false);
            transition = null;
            if (videoPlayer != null) { videoPlayer.Stop(); videoPlayer.targetTexture = null; videoPlayer.clip = null; videoPlayer.url = string.Empty; }
            if (videoImage != null) { videoImage.texture = null; videoImage.enabled = false; }
            if (runtimeTexture != null) { runtimeTexture.Release(); Destroy(runtimeTexture); runtimeTexture = null; }
            data = null;
            heldSeconds = preparingSeconds = 0f;
            if (canvasGroup != null) { canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = canvasGroup.interactable = false; }
        }

        protected override void OnClose() => Stop();
        protected override void OnWillHide() => Stop();
        private void OnDisable() => Stop();
        protected override void OnBeforeDestroy()
        {
            Stop();
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= OnVideoPrepared;
                videoPlayer.loopPointReached -= OnVideoEnded;
                videoPlayer.errorReceived -= OnVideoError;
            }
            PlaybackFailed = null;
            base.OnBeforeDestroy();
        }
    }
}
