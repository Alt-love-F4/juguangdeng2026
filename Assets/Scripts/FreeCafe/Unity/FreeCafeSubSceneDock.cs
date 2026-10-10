using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>嵌套子场景的停靠位移：需要时把子场景根平滑移动到父级原点（镜头前），用完滑回原位。摄像头不动。</summary>
    public sealed class FreeCafeSubSceneDock : MonoBehaviour
    {
        [Tooltip("停靠时的本地坐标；通常为 (0,0,0)，即与父场景原点（镜头前）重合")]
        public Vector3 dockedLocalPosition = Vector3.zero;
        [Tooltip("区域管理器；平移时间与镜头区域切换共用，测试将其设为 0 可瞬间到位")]
        public FreeCafeAreaManager areaManager;
        private Vector3 homeLocalPosition;
        private Vector3 velocity;
        private bool docked;

        public bool IsDocked => docked;

        private void Awake()
        {
            // 场景里摆放的位置就是归位位置
            homeLocalPosition = transform.localPosition;
        }

        public void SetDocked(bool value, bool instant = false)
        {
            docked = value;
            // panSmoothTime<=0 表示本帧立即就位（读档/测试），不依赖下一帧 Update
            if (areaManager != null && areaManager.panSmoothTime <= 0) instant = true;
            if (instant) { transform.localPosition = docked ? dockedLocalPosition : homeLocalPosition; velocity = Vector3.zero; }
        }

        private void Update()
        {
            var target = docked ? dockedLocalPosition : homeLocalPosition;
            if ((transform.localPosition - target).sqrMagnitude < .000001f) return;
            float smoothTime = areaManager != null ? areaManager.panSmoothTime : .35f;
            if (smoothTime <= 0) { transform.localPosition = target; velocity = Vector3.zero; return; }
            transform.localPosition = Vector3.SmoothDamp(transform.localPosition, target, ref velocity, smoothTime);
            if ((transform.localPosition - target).sqrMagnitude < .0004f)
            {
                transform.localPosition = target;
                velocity = Vector3.zero;
            }
        }
    }
}
