using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>Editable presentation for both GM tutorials. Gameplay stays in the tutorial controller.</summary>
    public sealed class FreeCafeTutorialPanel : MonoBehaviour
    {
        public Canvas canvas;
        public Image shade;
        public TMP_Text heading, instruction, errorText, gestureText;
        public RectTransform gesture;
        public Image leftButton, rightButton;
        public GameObject rotationArrow;
        public GameObject retry;
        public Button retryButton, exitButton;
        public Image[] trail;
        [Header("按键颜色")]
        public Color activeButtonColor = new Color(.85f, .38f, .12f);
        public Color inactiveButtonColor = new Color(.85f, .8f, .66f);
        [Header("光效与位置")]
        public Color trailColor = new Color(1, .73f, .25f);
        public float trailHeadSize = 22, trailSize = 12;
        public float gestureOffset = 110;
        [Header("鼠标提示透明度")]
        [Range(0f, 1f)] public float gestureOpacity = .78f;

        private void Awake()
        {
            // Every tutorial shares this prefab. Do not rely on one controller to
            // replace the authored 100x100 placeholder sizes before displaying it.
            for (int i = 0; i < trail.Length; i++)
            {
                if (trail[i] == null) continue;
                var rect = trail[i].rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.localScale = Vector3.one;
                rect.sizeDelta = Vector2.one * (i == 0 ? trailHeadSize : trailSize);
                rect.localRotation = Quaternion.Euler(0, 0, 45);
                trail[i].raycastTarget = false;
            }
            if (gesture != null)
            {
                var group = gesture.GetComponent<CanvasGroup>();
                if (group == null) group = gesture.gameObject.AddComponent<CanvasGroup>();
                group.alpha = gestureOpacity;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        public void SetMouseMode(bool right, bool rotate)
        {
            leftButton.color = right ? inactiveButtonColor : activeButtonColor;
            rightButton.color = right ? activeButtonColor : inactiveButtonColor;
            rotationArrow.SetActive(rotate);
        }
    }
}
