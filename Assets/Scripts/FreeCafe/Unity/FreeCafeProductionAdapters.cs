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
    public interface ILatteArtBrushSurface
    {
        float BrushRadius { get; set; }
    }
    /// <summary>Display-only terminology; never rename physical foam inventory or its consumption hints.</summary>
    public static class FreeCafeProductionText
    {
        public static string Recipe(string text) => text?.Replace("奶泡", "拉花").Replace("奶沫", "拉花");

        public static string Pattern(string id)
        {
            switch (id)
            {
                case "crown": return "王冠";
                case "heart": return "爱心";
                case "rosetta": return "树叶";
                case "tulip": return "郁金香";
                case "x": return "X 形";
                case "a": return "A 形";
                case "bean": return "咖啡豆";
                case "sword_crown": return "剑与王冠";
                case "concord_ring": return "圣谐之环";
                case "new_rose": return "五瓣玫瑰";
                case "broken_chain": return "断链";
                case null: case "": case "unknown": return "未识别";
                default: return id;
            }
        }

        public static string LatteArtContents(float milliliters, string patternId)
            => "拉花" + milliliters.ToString("0.#") + "ML·图案" + Pattern(patternId);
    }

    public interface ILatteArtFoamSurface
    {
        void SetFoamConsumer(Func<float, float> consumer, float millilitersPerSecond);
    }
}
