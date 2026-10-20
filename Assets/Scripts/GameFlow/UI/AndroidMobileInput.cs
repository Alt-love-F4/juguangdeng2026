using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Spotlight.GameFlow
{
    public static class MobileTouchInput
    {
        public static bool IsAndroidPlayer => Application.platform == RuntimePlatform.Android;

        public static bool TryGetPrimaryTouch(out Vector2 position, out bool pressed)
        {
            position = Vector2.zero;
            pressed = false;
            if (!IsAndroidPlayer || Touchscreen.current == null) return false;
            TouchControl touch = Touchscreen.current.primaryTouch;
            position = touch.position.ReadValue();
            pressed = touch.press.isPressed;
            return true;
        }

        public static int GetPressedTouches(out Vector2 first, out Vector2 second)
        {
            first = Vector2.zero;
            second = Vector2.zero;
            if (!IsAndroidPlayer || Touchscreen.current == null) return 0;
            int count = 0;
            var touches = Touchscreen.current.touches;
            for (int i = 0; i < touches.Count && count < 2; i++)
            {
                TouchControl touch = touches[i];
                if (!touch.press.isPressed) continue;
                if (count++ == 0) first = touch.position.ReadValue();
                else second = touch.position.ReadValue();
            }
            return count;
        }
    }

    [DisallowMultipleComponent]
    public sealed class AndroidSafeAreaFitter : MonoBehaviour
    {
        private RectTransform target;
        private Vector2 baseAnchorMin, baseAnchorMax;
        private Vector2 baseOffsetMin, baseOffsetMax;
        private Rect appliedSafeArea;
        private int appliedWidth, appliedHeight;

        private void Awake()
        {
            target = transform as RectTransform;
            if (target == null) { enabled = false; return; }
            baseAnchorMin = target.anchorMin;
            baseAnchorMax = target.anchorMax;
            baseOffsetMin = target.offsetMin;
            baseOffsetMax = target.offsetMax;
            Apply();
        }

        private void LateUpdate()
        {
            if (!MobileTouchInput.IsAndroidPlayer) return;
            if (appliedWidth != Screen.width || appliedHeight != Screen.height || appliedSafeArea != Screen.safeArea)
                Apply();
        }

        private void Apply()
        {
            if (!MobileTouchInput.IsAndroidPlayer || target == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            Vector2 min = safe.position / screen;
            Vector2 max = safe.max / screen;
            Vector2 span = max - min;
            target.anchorMin = min + Vector2.Scale(baseAnchorMin, span);
            target.anchorMax = min + Vector2.Scale(baseAnchorMax, span);
            target.offsetMin = baseOffsetMin;
            target.offsetMax = baseOffsetMax;
            appliedSafeArea = safe;
            appliedWidth = Screen.width;
            appliedHeight = Screen.height;
        }
    }

    public sealed class TouchPourGestureRecognizer
    {
        private const float DwellSeconds = .22f;
        private const float HoldMovementPixels = 18f;
        private const float VerticalActivationPixels = 22f;
        private string targetId;
        private Vector2 stablePosition;
        private Vector2 armedPosition;
        private float stableSince;
        private bool armed;

        public bool IsArmed => armed;

        public bool Update(string currentTargetId, Vector2 position, float unscaledTime, out Vector2 pourOrigin)
        {
            pourOrigin = armedPosition;
            if (string.IsNullOrEmpty(currentTargetId))
            {
                Reset();
                return false;
            }
            if (targetId != currentTargetId)
            {
                targetId = currentTargetId;
                stablePosition = position;
                stableSince = unscaledTime;
                armed = false;
            }
            if (!armed)
            {
                if ((position - stablePosition).sqrMagnitude > HoldMovementPixels * HoldMovementPixels)
                {
                    stablePosition = position;
                    stableSince = unscaledTime;
                    return false;
                }
                if (unscaledTime - stableSince < DwellSeconds) return false;
                armed = true;
                armedPosition = position;
                pourOrigin = armedPosition;
                return false;
            }
            Vector2 delta = position - armedPosition;
            if (Mathf.Abs(delta.y) < VerticalActivationPixels || Mathf.Abs(delta.y) <= Mathf.Abs(delta.x) * 1.25f)
                return false;
            pourOrigin = armedPosition;
            return true;
        }

        public void Reset()
        {
            targetId = null;
            stablePosition = Vector2.zero;
            armedPosition = Vector2.zero;
            stableSince = 0f;
            armed = false;
        }
    }

    internal static class AndroidMobileRuntime
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureDisplay()
        {
            Application.targetFrameRate = 60;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }
#endif
    }
}
