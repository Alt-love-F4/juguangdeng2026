using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spotlight.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class UIButtonAudio : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        [SerializeField] private string selectKey = AudioKeys.Select;
        [SerializeField] private string clickKey = AudioKeys.Confirm;
        private Button button;
        private float enabledAt;
        private float lastSelect = -1f;
        private bool Usable => isActiveAndEnabled && button != null && button.IsActive() && button.IsInteractable();

        private void Awake() => button = GetComponent<Button>();
        private void OnEnable() { enabledAt = Time.unscaledTime; lastSelect = -1f; Bind(); }

        private void Bind()
        {
            if (button == null) button = GetComponent<Button>();
            button.onClick.RemoveListener(Clicked);
            button.onClick.AddListener(Clicked);
        }

        private void Clicked() { if (Usable) GameAudio.Play(clickKey); }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Clicked); }

        public static void Install(GameObject root)
        {
            if (root == null) return;
            foreach (Button target in root.GetComponentsInChildren<Button>(true)) Configure(target);
        }

        public static void Configure(Button target, string key = null)
        {
            if (target == null) return;
            UIButtonAudio feedback = target.GetComponent<UIButtonAudio>();
            if (feedback == null) feedback = target.gameObject.AddComponent<UIButtonAudio>();
            if (key != null) feedback.clickKey = key;
            feedback.Bind();
        }

        public void OnPointerEnter(PointerEventData eventData) => Selected();
        public void OnSelect(BaseEventData eventData)
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            bool navigating = keyboard != null && (keyboard.upArrowKey.isPressed || keyboard.downArrowKey.isPressed ||
                keyboard.leftArrowKey.isPressed || keyboard.rightArrowKey.isPressed || keyboard.tabKey.isPressed);
            navigating |= gamepad != null && (gamepad.dpad.ReadValue().sqrMagnitude > .1f || gamepad.leftStick.ReadValue().sqrMagnitude > .1f);
            if (navigating && Time.unscaledTime - enabledAt > .12f) Selected();
        }

        private void Selected()
        {
            if (!Usable || Time.unscaledTime - lastSelect < .08f) return;
            lastSelect = Time.unscaledTime;
            GameAudio.Play(selectKey, this);
        }

    }
}
