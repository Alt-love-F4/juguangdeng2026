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
        public void SetMouseMode(bool right, bool rotate)
        {
            leftButton.color = right ? inactiveButtonColor : activeButtonColor;
            rightButton.color = right ? activeButtonColor : inactiveButtonColor;
            rotationArrow.SetActive(rotate);
        }
    }
}
