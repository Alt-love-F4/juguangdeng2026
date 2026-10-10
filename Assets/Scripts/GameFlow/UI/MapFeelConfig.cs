using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Shared map feel settings, read live so designers can tune during Play Mode.</summary>
    [CreateAssetMenu(menuName = "Spotlight/Map Feel Configuration", fileName = "MapFeelConfig")]
    public sealed class MapFeelConfig : ScriptableObject
    {
        [Header("鼠标左键：地点聚焦")]
        [InspectorName("聚焦缓动时间（秒）")]
        [Tooltip("左键点击地点后，平移到地点和缩放到聚焦倍率使用的缓动时间（秒）。越小越迅速，越大越柔和；这是缓动参数，并非到达目标的精确时长。WASD 或拖动会中断聚焦平移。")]
        [Min(.01f)] public float focusSmoothTime = .3f;
        [InspectorName("地点聚焦倍率")]
        [Tooltip("左键点击地点后的目标缩放倍率，1 表示原始大小。实际值限制在最小与最大缩放倍率之间；右键关闭地点面板不会自动恢复原倍率。")]
        [Min(.01f)] public float focusZoom = 1.5f;

        [Header("鼠标滚轮：地图缩放")]
        [InspectorName("滚轮缩放灵敏度")]
        [Tooltip("每单位滚轮输入对目标倍率的影响，按指数比例缩放：目标倍率乘以 exp(滚轮输入 × 灵敏度)。越大每次滚动变化越明显，0 禁用滚轮缩放。不同鼠标的滚轮输入数值可能不同。")]
        [Min(0f)] public float scrollZoomSensitivity = .01f;
        [InspectorName("滚轮缩放缓动时间（秒）")]
        [Tooltip("滚轮缩放追随目标倍率的缓动时间（秒）。越小响应越快，越大过渡越柔和；地点聚焦使用上方的聚焦缓动时间。")]
        [Min(.01f)] public float zoomSmoothTime = .06f;
        [InspectorName("最小缩放倍率")]
        [Tooltip("地图允许缩小到的最低倍率，1 表示原始大小，0.7 表示原始大小的 70%。同时约束滚轮缩放和地点聚焦；运行时最低为 0.01。")]
        [Min(.01f)] public float minimumZoom = .7f;
        [InspectorName("最大缩放倍率")]
        [Tooltip("地图允许放大到的最高倍率，1.5 表示原始大小的 150%。同时约束滚轮缩放和地点聚焦；小于最小倍率时按最小倍率处理。")]
        [Min(.01f)] public float maximumZoom = 1.5f;

        [Header("WASD：地图移动")]
        [InspectorName("启用 WASD 移动")]
        [Tooltip("开启后 W/S 向上/下、A/D 向左/右移动地图观察中心，画面中的地图向相反方向移动。关闭后立即停止键盘移动，鼠标操作仍可使用。")]
        public bool keyboardPanEnabled = true;
        [InspectorName("WASD 移动速度")]
        [Tooltip("键盘移动的最高速度，单位为地图 UI 坐标单位/秒；启用缩放速度补偿时为缩放后的 UI 单位/秒。越大越快，0 停止键盘移动。斜向移动与单轴移动速度相同，使用不受时间缩放影响的时间。")]
        [Min(0f)] public float keyboardPanSpeed = 520f;
        [InspectorName("WASD 加速度（单位/秒²）")]
        [Tooltip("按住方向键时，移动速度每秒变化的量；也影响转向和反向移动。越大启动与转向越快，越小越柔和。0 表示立即达到目标速度，保留原先手感；从静止到最高速度约需 移动速度 ÷ 加速度 秒。")]
        [Min(0f)] public float keyboardPanAcceleration = 0f;
        [InspectorName("WASD 松开减速度（单位/秒²）")]
        [Tooltip("松开所有 WASD 键后，移动速度每秒减少的量。越大停止越快，越小滑行越远。0 表示松开立即停止，保留原先手感。键盘滑行与鼠标拖动惯性独立配置。")]
        [Min(0f)] public float keyboardPanDeceleration = 0f;
        [InspectorName("WASD 缩放速度补偿")]
        [Tooltip("开启后按当前缩放倍率补偿位移，使不同倍率下画面移动速度一致（原先行为）。关闭后地图坐标移动速度固定，放大时画面移动更快，缩小时更慢。")]
        public bool keyboardPanCompensateZoom = true;

        [Header("鼠标左键：拖动与惯性")]
        [InspectorName("拖动判定距离（屏幕像素）")]
        [Tooltip("按住左键后，鼠标相对按下位置移动超过此距离才视为拖动。越大越不易误拖，越小越容易开始拖动；0 表示移动即拖动。此值使用屏幕像素，不受地图缩放影响。")]
        [Min(0f)] public float dragThreshold = 6f;
        [InspectorName("拖动速度采样响应")]
        [Tooltip("拖动时对鼠标速度的跟随强度（每秒），用于决定松开后的初始惯性速度。越大越接近最后一帧速度，越小越平滑；不会改变按住拖动时的实际位移。")]
        [Min(.1f)] public float panVelocityResponse = 25f;
        [InspectorName("拖动惯性衰减强度")]
        [Tooltip("松开左键后速度的指数衰减强度（每秒）。越大停止越快，越小滑行越远；运行时最低为 0.1。抵达地图边界会停止该轴惯性，WASD 输入或地点聚焦会清除拖动惯性。")]
        [Min(.1f)] public float panInertiaDamping = 6f;

        [Header("底图留边")]
        [InspectorName("底图放大比例")]
        [Tooltip("仅放大背景底图，不改变地点、标牌和地图移动边界。1 表示原始大小；越大边缘留白覆盖越多。为防止露出底图外区域，运行时还会按窗口尺寸与最小倍率自动保证足够留边。")]
        [Min(1f)] public float backgroundScale = 2.2f;
    }
}
