using UnityEngine;

namespace Spotlight.FreeCafe
{
    public sealed class FreeCafeMiniGameGateway : MonoBehaviour
    {
        public FreeCafeRuntime runtime;
        public MonoBehaviour presenter;
        public bool HasPresenter => presenter!=null && presenter is ICafeMiniGamePresenter;
        private string token;
        private int generation;
        private void OnEnable() { if(runtime!=null) runtime.ViewChanged+=Refresh; }
        private void Start() => Refresh();
        private void OnDisable() { if(runtime!=null) runtime.ViewChanged-=Refresh; Abort(); }
        public void Abort()
        {
            generation++;
            string previous=token; token=null;
            if(presenter is ICafeMiniGamePresenter adapter && !string.IsNullOrEmpty(previous)) adapter.Cancel(previous);
            if(runtime!=null && runtime.World!=null) runtime.World.CancelProductionMiniGame();
        }
        private void Refresh()
        {
            if(runtime==null || runtime.World==null || !runtime.World.ProductionEnabled) return;
            var session=runtime.World.State.production;
            if(token==session.miniGameToken) return;
            string previous=token; token=session.miniGameToken; int epoch=++generation;
            if(HasPresenter && presenter is ICafeMiniGamePresenter adapter)
            {
                if(!string.IsNullOrEmpty(previous)) adapter.Cancel(previous);
                if(!string.IsNullOrEmpty(token))
                {
                    string expected=token;
                    adapter.Begin(session.miniGameKind,session.miniGameCupId,expected,(returned,completed,score)=>
                    {
                        if(this==null || epoch!=generation || token!=expected || returned!=expected) return;
                        string pattern = (adapter as ICafeLatteArtResultPresenter)?.LastPatternId;
                        runtime.ShowHint(runtime.World.ResolveProductionMiniGame(returned, completed, score, pattern).message);
                    });
                }
            }
            // 未注入时由制作HUD显示占位入口；不自动判定成功或消耗内容物。
        }
    }
}

