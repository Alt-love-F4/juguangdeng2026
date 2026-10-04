using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Spotlight.Gameplay
{
    /// <summary>
    /// 可可 QTE 小游戏：滑块在轨道上移动，点击按钮让滑块停在绿区。
    /// 类似钓鱼游戏的追逐条机制。
    /// </summary>
    public sealed class CocoaQTEMinigame : MonoBehaviour
    {
        [Header("UI 引用")]
        public Slider trackSlider;
        public Image targetZone;
        public Image indicator;
        public Button actionButton;
        public TMP_Text hintText;
        public TMP_Text attemptText;

        [Header("游戏参数")]
        public float moveSpeed = 0.8f;
        public float zoneWidth = 0.2f;
        public int maxAttempts = 3;

        private float targetCenter;
        private bool isPlaying;
        private int attemptsLeft;
        private System.Action<bool> onComplete;

        private void Awake()
        {
            if (actionButton != null)
                actionButton.onClick.AddListener(OnActionClicked);
        }

        public void Open(System.Action<bool> callback)
        {
            onComplete = callback;
            gameObject.SetActive(true);
            StartRound();
            if (hintText != null)
                hintText.text = "点击按钮让滑块停在绿区\n失败3次则可可糊锅";
        }

        public void Cancel() { isPlaying = false; onComplete = null; }
        private void OnDisable() => Cancel();

        private void StartRound()
        {
            attemptsLeft = Mathf.Max(1, maxAttempts);
            isPlaying = true;
            // 随机绿区位置
            targetCenter = Random.Range(0.3f, 0.7f);
            UpdateTargetZone();
            UpdateAttemptText();
        }

        private void Update()
        {
            if (!isPlaying || trackSlider == null || Time.timeScale <= 0) return;
            // 滑块往返移动
            trackSlider.value = Mathf.PingPong(Time.time * moveSpeed, 1f);
            if (indicator != null) indicator.rectTransform.anchoredPosition = new Vector2((trackSlider.value - .5f) * ((RectTransform)trackSlider.transform).rect.width, 0);
        }

        private void UpdateTargetZone()
        {
            if (targetZone == null || trackSlider == null) return;
            // 根据 slider 的 handle 区域调整 target zone
            var fillRect = trackSlider.transform as RectTransform;
            if (fillRect == null) return;

            float sliderWidth = fillRect.rect.width;
            float zoneW = sliderWidth * zoneWidth;
            float centerX = sliderWidth * targetCenter;

            targetZone.rectTransform.SetParent(fillRect, false);
            targetZone.rectTransform.anchorMin = new Vector2(0, 0);
            targetZone.rectTransform.anchorMax = new Vector2(0, 1);
            targetZone.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            targetZone.rectTransform.anchoredPosition = new Vector2(centerX, 0);
            targetZone.rectTransform.sizeDelta = new Vector2(zoneW, 0);
        }

        private void UpdateAttemptText()
        {
            if (attemptText != null)
                attemptText.text = $"剩余次数: {attemptsLeft}";
        }

        private void OnActionClicked()
        {
            if (!isPlaying) return;

            float value = trackSlider != null ? trackSlider.value : 0f;
            float halfZone = zoneWidth / 2f;
            bool success = value >= targetCenter - halfZone && value <= targetCenter + halfZone;

            if (success)
            {
                isPlaying = false;
                var callback = onComplete; onComplete = null;
                gameObject.SetActive(false);
                callback?.Invoke(true);
            }
            else
            {
                attemptsLeft--;
                UpdateAttemptText();
                if (attemptsLeft <= 0)
                {
                    isPlaying = false;
                    var callback = onComplete; onComplete = null;
                    gameObject.SetActive(false);
                    callback?.Invoke(false);
                }
            }
        }
    }
}
