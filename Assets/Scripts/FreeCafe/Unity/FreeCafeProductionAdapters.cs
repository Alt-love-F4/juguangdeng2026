using System;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>实现者只消费权威库存快照。流体求解器/材质/粒子不拥有第二份库存。</summary>
    public interface ICafeLiquidPresenter
    {
        void Present(CafeLiquidSnapshot snapshot, float tilt, bool pouring);
    }
    /// <summary>后续真实拉花场景/Panel实现此接口。必须回传启动 token，支持取消。</summary>
    public interface ICafeMiniGamePresenter
    {
        void Begin(string kind, string cupId, string token, Action<string, bool, float> complete);
        void Cancel(string token);
    }
    public interface ICafeLatteArtResultPresenter
    {
        string LastPatternId { get; }
    }
    public interface ILatteArtSurface
    {
        int StrokePointCount { get; }
        void SetInteractionEnabled(bool value);
        void ResetLatte();
        LatteArtRecognitionResult Recognize(System.Collections.Generic.IList<LatteArtPatternPreset> presets = null);
    }
}
