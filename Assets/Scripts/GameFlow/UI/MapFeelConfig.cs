using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Shared map feel settings, read live so designers can tune during Play Mode.</summary>
    [CreateAssetMenu(menuName = "Spotlight/Map Feel Configuration", fileName = "MapFeelConfig")]
    public sealed class MapFeelConfig : ScriptableObject
    {
        [Header("地点聚焦")]
        [Tooltip("聚焦平移与缩放的缓动时间（秒）。")]
        [Min(.01f)] public float focusSmoothTime = .3f;
        [Tooltip("聚焦目标倍率，受缩放上下限约束。")]
        [Min(.01f)] public float focusZoom = 1.5f;

        [Header("滚轮缩放")]
        [Tooltip("灵敏度越大，每次滚动的倍率变化越明显。")]
        [Min(0f)] public float scrollZoomSensitivity = .01f;
        [Tooltip("缓动时间（秒），越小响应越快。")]
        [Min(.01f)] public float zoomSmoothTime = .06f;
        [Min(.01f)] public float minimumZoom = .7f;
        [Min(.01f)] public float maximumZoom = 1.5f;

        [Header("地图移动与拖动惯性")]
        [Tooltip("WASD 移动速度，单位为屏幕像素/秒。")]
        [Min(0f)] public float keyboardPanSpeed = 520f;
        [Tooltip("移动超过此距离后视为拖动，单位为屏幕像素。")]
        [Min(0f)] public float dragThreshold = 6f;
        [Tooltip("拖动速度采样响应，越大越贴近最后一帧速度。")]
        [Min(.1f)] public float panVelocityResponse = 25f;
        [Tooltip("松开后的减速强度，越小滑行越远。")]
        [Min(.1f)] public float panInertiaDamping = 6f;

        [Header("底图留边")]
        [Tooltip("底图放大比例，独立于地点与标牌。")]
        [Min(1f)] public float backgroundScale = 2.2f;
    }
}
