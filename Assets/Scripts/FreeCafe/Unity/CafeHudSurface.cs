using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>代码原生的圆角纸卡：随 Canvas 缩放，不创建贴图，也不覆盖面板外的世界。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CafeHudSurface : MaskableGraphic
    {
        public Color border = new Color(.38f,.23f,.14f,1f);
        [Min(0)] public float radius = 16f;
        [Min(0)] public float borderWidth = 2f;
        public bool shadow = true;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect=GetPixelAdjustedRect();
            if(shadow) Fill(vh,new Rect(rect.x+2,rect.y-5,rect.width,rect.height),radius,new Color(.16f,.09f,.04f,.2f));
            Fill(vh,rect,radius,border);
            float inset=Mathf.Min(borderWidth,Mathf.Min(rect.width,rect.height)*.5f);
            Fill(vh,new Rect(rect.x+inset,rect.y+inset,rect.width-2*inset,rect.height-2*inset),Mathf.Max(0,radius-inset),color);
        }
        private static void Fill(VertexHelper vh,Rect rect,float radius,Color tint)
        {
            if(rect.width<=0 || rect.height<=0) return;
            radius=Mathf.Min(radius,Mathf.Min(rect.width,rect.height)*.5f);
            int center=vh.currentVertCount;
            vh.AddVert(rect.center,tint,Vector2.zero);
            const int steps=6;
            for(int corner=0;corner<4;corner++)
            {
                Vector2 pivot=corner==0 ? new Vector2(rect.xMax-radius,rect.yMax-radius) :
                    corner==1 ? new Vector2(rect.xMin+radius,rect.yMax-radius) :
                    corner==2 ? new Vector2(rect.xMin+radius,rect.yMin+radius) : new Vector2(rect.xMax-radius,rect.yMin+radius);
                for(int i=0;i<=steps;i++)
                {
                    float angle=(corner*90f+i*90f/steps)*Mathf.Deg2Rad;
                    vh.AddVert(pivot+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,tint,Vector2.zero);
                }
            }
            int count=4*(steps+1);
            for(int i=0;i<count;i++) vh.AddTriangle(center,center+1+i,center+1+(i+1)%count);
        }
    }
}
