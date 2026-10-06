using System;

namespace Spotlight.GameFlow
{
    /// <summary>统一触发日程演出。Stop 是取消，不得调用正常结束回调。</summary>
    public interface IDayFlowPresentation
    {
        bool IsPlaying { get; }
        void Play(Action onFinished);
        void Stop();
    }
}
