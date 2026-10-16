using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>Ink-and-paper quality seal; does not rely on platform emoji fonts.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CafeReceiptFace : MaskableGraphic
    {
        [Range(0,3)] public int grade = 2;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect=GetPixelAdjustedRect(); float size=Mathf.Min(rect.width,rect.height)*.42f;
            Vector2 center=rect.center;
            Circle(vh,center,size,new Color(.38f,.22f,.12f));
            Circle(vh,center,size*.93f,new Color(.91f,.73f,.43f));
            Color ink=new Color(.32f,.18f,.11f);
            Circle(vh,center+new Vector2(-size*.32f,size*.17f),size*.07f,ink);
            Circle(vh,center+new Vector2(size*.32f,size*.17f),size*.07f,ink);
            Vector2 previous=default;
            for(int i=0;i<=20;i++)
            {
                float x=Mathf.Lerp(-.4f,.4f,i/20f);
                float y=grade==0 ? -.42f+(.16f*(1-x*x/.16f)) : grade==1 ? -.3f : -.27f-.23f*(1-x*x/.16f);
                Vector2 point=center+new Vector2(x,y)*size;
                if(i>0) Line(vh,previous,point,Mathf.Max(2,size*.045f),ink);
                previous=point;
            }
            if(grade==3)
            {
                for(int i=-1;i<=1;i++)
                    Line(vh,center+new Vector2(i*.28f,.98f)*size,center+new Vector2(i*.35f,1.2f)*size,size*.055f,ink);
                Line(vh,center+new Vector2(-.37f,.98f)*size,center+new Vector2(.37f,.98f)*size,size*.055f,ink);
            }
        }
        private static void Circle(VertexHelper vh,Vector2 center,float radius,Color tint)
        {
            int start=vh.currentVertCount; vh.AddVert(center,tint,Vector2.zero);
            const int segments=48;
            for(int i=0;i<=segments;i++) { float angle=i*Mathf.PI*2/segments; vh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,tint,Vector2.zero); }
            for(int i=0;i<segments;i++) vh.AddTriangle(start,start+i+1,start+i+2);
        }
        private static void Line(VertexHelper vh,Vector2 from,Vector2 to,float width,Color tint)
        {
            Vector2 normal=new Vector2(-(to-from).y,(to-from).x).normalized*width*.5f;
            int index=vh.currentVertCount;
            vh.AddVert(from-normal,tint,Vector2.zero);vh.AddVert(from+normal,tint,Vector2.zero);
            vh.AddVert(to+normal,tint,Vector2.zero);vh.AddVert(to-normal,tint,Vector2.zero);
            vh.AddTriangle(index,index+1,index+2);vh.AddTriangle(index,index+2,index+3);
        }
    }
}
