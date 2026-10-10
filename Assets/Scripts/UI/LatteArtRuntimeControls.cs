using UnityEngine;
using Spotlight.FreeCafe;

namespace OnePersonCompany
{
    /// <summary>拉花参数只在 GM 面板显示，避免把调试控件带入玩家游戏视图。</summary>
    [RequireComponent(typeof(LatteArtPrototype))]
    public sealed class LatteArtRuntimeControls : MonoBehaviour
    {
        private LatteArtPrototype effect;
        private bool registered;
        private void Start() { effect = GetComponent<LatteArtPrototype>(); Register(); }
        private void OnEnable() { if (effect == null) effect = GetComponent<LatteArtPrototype>(); Register(); }
        private void Update() { if (!registered) Register(); }
        private void Register()
        {
            if (registered || effect == null || FreeCafeGMPanel.Instance == null) return;
            registered = true;
            FreeCafeGMPanel.Instance.RegisterCategory("拉花调试", gm =>
            {
                gm.AddAction("清空当前拉花", () => effect.ResetLatte(), "清空 Plane 上的流体与当前轨迹", false);
                gm.AddAction("恢复拉花默认参数", () => effect.RestoreAppearanceDefaults(), "恢复 NS 外观参数", false);
                gm.AddSlider("笔刷半径", .005f, .08f, () => effect.BrushRadius, v => effect.BrushRadius = v, "F3");
                gm.AddSlider("注奶速度", .1f, 15f, () => effect.PourRate, v => effect.PourRate = v, "F2");
                gm.AddSlider("表面扩张", 0f, 2f, () => effect.PushStrength, v => effect.PushStrength = v, "F2");
                gm.AddSlider("鼠标拖动", 0f, 3f, () => effect.DragStrength, v => effect.DragStrength = v, "F2");
                gm.AddSlider("速度阻尼", 0f, 8f, () => effect.VelocityDamping, v => effect.VelocityDamping = v, "F2");
                gm.AddSlider("液体黏性", 0f, .001f, () => effect.Viscosity, v => effect.Viscosity = v, "F5");
                gm.AddSlider("奶泡扩散", 0f, 5f, () => effect.Diffusion, v => effect.Diffusion = v, "F2");
                gm.AddSlider("涡旋增强", 0f, 12f, () => effect.VorticityStrength, v => effect.VorticityStrength = v, "F2");
                gm.AddSlider("压力迭代", 12f, 100f, () => effect.PressureIterations, v => effect.PressureIterations = Mathf.RoundToInt(v), "F0", true);
                gm.AddSlider("奶泡高光起伏", 0f, 20f, () => effect.FoamRelief, v => effect.FoamRelief = v, "F2");
                gm.AddAction("显示当前轨迹识别", () => gm.SetExternalStatus(effect.Recognize().ToString() + " · 点数 " + effect.StrokePointCount), "检查实时模板匹配", false);
            });
        }
    }
}
