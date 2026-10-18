using TMPro;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>世界内的木质营业立牌。交互仍由 FreeEntityView / CafeWorld 处理，没有独立营业状态。</summary>
    [RequireComponent(typeof(FreeEntityView))]
    public sealed class FreeCafeBusinessSignView : MonoBehaviour
    {
        public FreeEntityView entityView;
        public TMP_Text statusCaption;
        public MeshRenderer face;
        public MeshRenderer[] pieces;
        private MaterialPropertyBlock tint;
        public void Present(bool highlighted = false)
        {
            var state = entityView.Runtime.World.State;
            entityView.label.text = state.open ? "结束营业" : state.businessPhase == BusinessPhase.Night ? "开始新一天" : "启动营业";
            statusCaption.text = state.open ? "点击打烊 · 当前 " + state.requests.Count + " 位客人"
                : entityView.Runtime.CafeBusinessOnboardingActive ? "跟随提示点击开店 · 认识顾客与订单"
                : entityView.Runtime.CafeTutorialActive ? "完成首杯黑咖啡后学习营业" : "点击开店 · 欢迎光临";
            if (tint == null) tint = new MaterialPropertyBlock();
            var color = state.open ? new Color(.81f,.89f,.74f) : new Color(.98f,.91f,.72f);
            tint.SetColor("_Color", highlighted ? Color.Lerp(color,Color.white,.3f) : color);
            face.SetPropertyBlock(tint);
            UpdateSorting(entityView.body.sortingOrder);
        }
        public void UpdateSorting(int bodyOrder)
        {
            for (int i = 0; i < pieces.Length; i++) pieces[i].sortingOrder = bodyOrder + i;
            entityView.label.GetComponent<MeshRenderer>().sortingOrder = bodyOrder + pieces.Length + 1;
            statusCaption.GetComponent<MeshRenderer>().sortingOrder = bodyOrder + pieces.Length + 2;
        }
    }
}
