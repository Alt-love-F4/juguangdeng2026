using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.FreeCafe
{
    /// <summary>拉花工作台（占位）：工作台右侧的嵌套子场景。令牌激活时把拉花台根平滑移动到镜头前，点击背景取消并滑回原位。</summary>
    public sealed class FreeCafeLatteArtBench : MonoBehaviour, ICafeMiniGamePresenter, IPointerClickHandler
    {
        [Tooltip("拉花台根的停靠组件；激活时停靠到工作台原点（镜头前）")]
        public FreeCafeSubSceneDock dock;
        private string activeToken;
        private Action<string, bool, float> completion;

        public void Begin(string kind, string cupId, string token, Action<string, bool, float> complete)
        {
            activeToken = token;
            completion = complete;
            // 打开拉花：拉花面板的根移动到摄像头前
            if (dock != null) dock.SetDocked(true);
        }

        public void Cancel(string token)
        {
            if (!string.IsNullOrEmpty(activeToken) && token != activeToken) return;
            activeToken = null;
            completion = null;
            // 小游戏结束/取消：拉花台滑回工作台右侧
            if (dock != null) dock.SetDocked(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || string.IsNullOrEmpty(activeToken)) return;
            // 占位阶段：点击背景视为放弃拉花，回传取消结果且不改变杯中内容；归位由 Cancel 路径完成
            var finish = completion;
            var token = activeToken;
            completion = null;
            finish?.Invoke(token, false, 0);
        }
    }
}
