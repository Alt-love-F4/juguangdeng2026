using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// 独立的新游戏开场序章；不依赖 NCS、DayFlow 或存档。
    /// 每段：正文逐字渐显，同时分镜格依次滑入/飞入 → 等待点击 → 整段淡出。
    /// 演出中点击先补全当前段，再次点击才翻页。
    /// </summary>
    public sealed class OpeningProloguePanel : UIPanel, IDayFlowPresentation
    {
        [Serializable]
        public sealed class Beat
        {
            [TextArea(2, 8)] public string text;
            [Tooltip("单字渐显时长；<=0 使用默认值。")]
            [Min(0f)] public float fadeInSeconds = .55f;
            [Min(0f)] public float fadeOutSeconds = .5f;
            [Tooltip("本段分镜根节点（子节点为 ProloguePanelArt）；为空表示纯文字段。")]
            public CanvasGroup art;
            [Tooltip("分镜相对正文开始渐显的延迟秒数；0 表示图文同时出场。")]
            [Min(0f)] public float artDelaySeconds;
            [Tooltip("正文区域的纵向锚点 (minY, maxY)，0~1。")]
            public Vector2 textArea = new Vector2(.22f, .78f);
            [Min(0f)] public float fontSize = 40f;
        }

        [Header("Prefab bindings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text narrationText;
        [SerializeField] private Button advanceArea;
        [SerializeField] private Button skipButton;
        [Header("Serialized prologue content")]
        [SerializeField] private Beat[] beats = Array.Empty<Beat>();
        [SerializeField, Min(0f)] private float defaultFadeInSeconds = .55f;
        [SerializeField, Min(0f)] private float defaultFadeOutSeconds = .5f;
        [SerializeField, Min(0f)] private float finalBlackHoldSeconds;
        [Header("Text reveal")]
        [SerializeField, Min(0f)] private float charStaggerSeconds = .035f;
        [SerializeField] private float charRisePixels = 14f;

        private Coroutine playback;
        private Action finished;
        private bool acceptingAdvance;
        private bool fastForwardRequested;
        private bool completing;
        private int beatIndex;
        private int lastInputFrame = -1;
        private float savedTimeScale;
        private bool ownsTimeScale;
        private TMP_MeshInfo[] restMesh;
        private ProloguePanelArt[][] beatPanels;

        public bool IsPlaying { get; private set; }
        public int BeatCount => beats?.Length ?? 0;
        public int CurrentBeatIndex => beatIndex;
        public bool CanAdvance => IsPlaying && acceptingAdvance;
        /// <summary>Text or art of the current beat is still animating in.</summary>
        public bool IsRevealing { get; private set; }

        public bool BeatHasArt(int index)
            => beats != null && index >= 0 && index < beats.Length && beats[index]?.art != null;

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
            fastForwardRequested = false;
            beatIndex = 0;
            finished = onFinished;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            advanceArea.interactable = true;
            skipButton.gameObject.SetActive(true);
            skipButton.interactable = true;
            CacheArt();
            playback = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            if (beats == null || beats.Length == 0) { Finish(); yield break; }
            while (beatIndex < beats.Length && IsPlaying)
            {
                Beat beat = beats[beatIndex] ?? new Beat { text = string.Empty };
                ProloguePanelArt[] panels = beatPanels[beatIndex];
                float charFade = beat.fadeInSeconds <= 0f ? defaultFadeInSeconds : beat.fadeInSeconds;
                int glyphs = PrepareText(beat);
                float textEnd = Mathf.Max(0, glyphs - 1) * charStaggerSeconds + charFade;
                float artStart = beat.artDelaySeconds;
                float total = textEnd;
                foreach (ProloguePanelArt panel in panels) total = Mathf.Max(total, artStart + panel.EndTime);
                if (beat.art != null) { beat.art.gameObject.SetActive(true); beat.art.alpha = 1f; }

                // Reveal: chars rise in one by one, panels pop at the end of the sentence.
                IsRevealing = true;
                fastForwardRequested = false;
                float t = 0f;
                while (IsPlaying && t < total)
                {
                    if (fastForwardRequested) t = total;
                    EvaluateText(t, charFade);
                    EvaluateArt(panels, t - artStart);
                    if (t >= total) break;
                    yield return null;
                    t += Time.unscaledDeltaTime;
                }
                if (!IsPlaying) yield break;
                IsRevealing = false;
                ShowTextAtRest();

                // Hold: panels keep a slow push-in while waiting for the player.
                acceptingAdvance = true;
                while (IsPlaying && acceptingAdvance)
                {
                    yield return null;
                    t += Time.unscaledDeltaTime;
                    EvaluateArt(panels, t - artStart);
                }
                if (!IsPlaying) yield break;

                float fadeOut = beat.fadeOutSeconds <= 0f ? defaultFadeOutSeconds : beat.fadeOutSeconds;
                for (float f = 0f; IsPlaying && f < fadeOut; f += Time.unscaledDeltaTime)
                {
                    float a = 1f - Mathf.SmoothStep(0f, 1f, f / fadeOut);
                    narrationText.alpha = a;
                    if (beat.art != null) beat.art.alpha = a;
                    t += Time.unscaledDeltaTime;
                    EvaluateArt(panels, t - artStart);
                    yield return null;
                }
                if (!IsPlaying) yield break;
                ClearBeat(beat, panels);
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

        /// <summary>First input completes the current reveal; the next one advances. Fade-out ignores input.</summary>
        public void Continue()
        {
            if (!IsPlaying || completing) return;
            // Keyboard handlers (panel + debug controller) may both fire on the same frame; count one press.
            if (Time.frameCount == lastInputFrame) return;
            if (IsRevealing) { lastInputFrame = Time.frameCount; fastForwardRequested = true; return; }
            if (!acceptingAdvance) return;
            lastInputFrame = Time.frameCount;
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
            ResetPlayback();
            if (canvasGroup != null) { canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }
            if (skipButton != null) skipButton.interactable = false;
            Action callback = finished;
            finished = null;
            callback?.Invoke();
        }

        public void Stop()
        {
            completing = false;
            ResetPlayback();
            finished = null;
            if (canvasGroup != null) { canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }
            if (skipButton != null) skipButton.interactable = false;
        }

        private void ResetPlayback()
        {
            IsPlaying = false;
            IsRevealing = false;
            acceptingAdvance = false;
            fastForwardRequested = false;
            if (playback != null) { StopCoroutine(playback); playback = null; }
            if (narrationText != null) { narrationText.alpha = 0f; narrationText.text = string.Empty; }
            restMesh = null;
            HideAllArt();
            if (ownsTimeScale) { Time.timeScale = savedTimeScale; ownsTimeScale = false; }
        }

        private void CacheArt()
        {
            beatPanels = new ProloguePanelArt[beats?.Length ?? 0][];
            for (int i = 0; i < beatPanels.Length; i++)
                beatPanels[i] = beats[i]?.art != null
                    ? beats[i].art.GetComponentsInChildren<ProloguePanelArt>(true)
                    : Array.Empty<ProloguePanelArt>();
            HideAllArt();
        }

        private void HideAllArt()
        {
            if (beats == null) return;
            for (int i = 0; i < beats.Length; i++)
            {
                CanvasGroup art = beats[i]?.art;
                if (art == null) continue;
                if (beatPanels != null && i < beatPanels.Length && beatPanels[i] != null)
                    foreach (ProloguePanelArt panel in beatPanels[i]) panel.Hide();
                art.alpha = 0f;
                art.gameObject.SetActive(false);
            }
        }

        private static void EvaluateArt(ProloguePanelArt[] panels, float time)
        {
            foreach (ProloguePanelArt panel in panels) panel.Evaluate(time);
        }

        private void ClearBeat(Beat beat, ProloguePanelArt[] panels)
        {
            narrationText.alpha = 0f;
            narrationText.text = string.Empty;
            restMesh = null;
            foreach (ProloguePanelArt panel in panels) panel.Hide();
            if (beat.art != null) { beat.art.alpha = 0f; beat.art.gameObject.SetActive(false); }
        }

        // ---- per-character text reveal (vertex alpha + rise on TMP mesh) ----

        private int PrepareText(Beat beat)
        {
            RectTransform rect = narrationText.rectTransform;
            Vector2 area = beat.textArea;
            rect.anchorMin = new Vector2(rect.anchorMin.x, Mathf.Clamp01(Mathf.Min(area.x, area.y)));
            rect.anchorMax = new Vector2(rect.anchorMax.x, Mathf.Clamp01(Mathf.Max(area.x, area.y)));
            rect.offsetMin = new Vector2(rect.offsetMin.x, 0f);
            rect.offsetMax = new Vector2(rect.offsetMax.x, 0f);
            if (beat.fontSize > 0f) { narrationText.fontSize = beat.fontSize; narrationText.fontSizeMax = beat.fontSize; }
            narrationText.text = beat.text ?? string.Empty;
            narrationText.alpha = 1f;
            EvaluateText(0f, 1f);
            int visible = 0;
            TMP_TextInfo info = narrationText.textInfo;
            for (int i = 0; i < info.characterCount; i++) if (info.characterInfo[i].isVisible) visible++;
            return visible;
        }

        private void EvaluateText(float time, float charFade)
        {
            // Regenerate first so pending layout/property changes can never overwrite the animated vertices.
            narrationText.ForceMeshUpdate(true);
            TMP_TextInfo info = narrationText.textInfo;
            restMesh = info.CopyMeshInfoVertexData();
            int order = 0;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible) continue;
                float p = charFade <= 0f ? 1f : Mathf.Clamp01((time - order * charStaggerSeconds) / charFade);
                order++;
                float eased = 1f - (1f - p) * (1f - p);
                float rise = (1f - eased) * -charRisePixels;
                int m = c.materialReferenceIndex, v = c.vertexIndex;
                Vector3[] dst = info.meshInfo[m].vertices;
                Color32[] col = info.meshInfo[m].colors32;
                Vector3[] src = restMesh[m].vertices;
                Color32[] srcCol = restMesh[m].colors32;
                for (int k = 0; k < 4; k++)
                {
                    dst[v + k] = src[v + k] + new Vector3(0f, rise, 0f);
                    Color32 color = srcCol[v + k];
                    color.a = (byte)(color.a * p);
                    col[v + k] = color;
                }
            }
            narrationText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        private void ShowTextAtRest()
        {
            if (restMesh == null) return;
            EvaluateText(float.PositiveInfinity, 1f);
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
