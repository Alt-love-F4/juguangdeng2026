using System.Collections;
using TMPro;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>非模态操作播报：弹出、短暂停留、上浮淡出，不抢占鼠标。</summary>
    public sealed class OperationToastPanel : UIPanel
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform banner;
        [SerializeField] private TMP_Text messageText;
        [SerializeField, Min(.01f)] private float enterSeconds = .16f;
        [SerializeField, Min(0f)] private float holdSeconds = 1.25f;
        [SerializeField, Min(.01f)] private float floatSeconds = .65f;
        [SerializeField] private float riseDistance = 68f;
        private static OperationToastPanel active;
        private Coroutine animation;
        public string Message => messageText != null ? messageText.text : "";

        public static void ShowMessage(string message, string soundKey = Spotlight.Audio.AudioKeys.Error)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (active == null)
                active = UIKit.OpenPanel<OperationToastPanel>(UILevel.Pop, tag: "operation-toast", cachePolicy: PanelCachePolicy.Transient);
            active.Play(message, soundKey);
        }

        protected override void OnShow()
        {
            var rect = (RectTransform)transform;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            group.blocksRaycasts = group.interactable = false;
        }

        public void Play(string message, string soundKey = Spotlight.Audio.AudioKeys.Error)
        {
            Spotlight.Audio.GameAudio.Play(soundKey);
            if (animation != null) StopCoroutine(animation);
            messageText.text = message;
            group.blocksRaycasts = group.interactable = false;
            animation = StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            float elapsed = 0f;
            banner.anchoredPosition = Vector2.zero;
            banner.localScale = Vector3.one * .9f;
            group.alpha = 0f;
            while (elapsed < enterSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / enterSeconds);
                group.alpha = t;
                banner.localScale = Vector3.one * Mathf.Lerp(.9f, 1f, 1f - (1f - t) * (1f - t));
                yield return null;
            }
            group.alpha = 1f; banner.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(holdSeconds);
            elapsed = 0f;
            while (elapsed < floatSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / floatSeconds);
                banner.anchoredPosition = Vector2.up * (riseDistance * t);
                group.alpha = 1f - t;
                yield return null;
            }
            animation = null;
            CloseSelf();
        }

        protected override void OnClose()
        {
            if (animation != null) StopCoroutine(animation);
            animation = null;
            if (group != null) group.alpha = 0f;
            if (active == this) active = null;
        }
        protected override void OnBeforeDestroy()
        {
            if (active == this) active = null;
            base.OnBeforeDestroy();
        }
    }
}
