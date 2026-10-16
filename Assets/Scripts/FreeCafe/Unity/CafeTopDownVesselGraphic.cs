using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>Editable UI-native overhead cup and milk pitcher illustrations.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CafeTopDownVesselGraphic : MaskableGraphic
    {
        public enum Vessel { SmallCup, LargeCup, MilkPitcher }
        public Vessel vessel;
        public bool selected;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=GetPixelAdjustedRect();var center=rect.center;
            float radius=Mathf.Min(rect.width,rect.height)*(vessel==Vessel.SmallCup ? .28f : .34f);
            Color ink=new Color(.29f,.17f,.11f), cream=new Color(.94f,.86f,.69f), coffee=new Color(.26f,.12f,.065f);
            if(selected) Disk(vh,center,radius*1.31f,new Color(.85f,.60f,.25f));
            Disk(vh,center+Vector2.right*radius*.94f,radius*.43f,ink);
            Disk(vh,center+Vector2.right*radius*.94f,radius*.28f,cream);
            if(vessel==Vessel.MilkPitcher)
            {
                int n=vh.currentVertCount;
                vh.AddVert(center+new Vector2(-1.45f,.38f)*radius,ink,Vector2.zero);
                vh.AddVert(center+new Vector2(-.6f,.8f)*radius,ink,Vector2.zero);
                vh.AddVert(center+new Vector2(-.6f,-.2f)*radius,ink,Vector2.zero);vh.AddTriangle(n,n+1,n+2);
            }
            Disk(vh,center,radius,ink);Disk(vh,center,radius*.93f,cream);
            Disk(vh,center,radius*.78f,vessel==Vessel.MilkPitcher ? new Color(.98f,.94f,.83f) : coffee);
            if(vessel!=Vessel.MilkPitcher) Disk(vh,center+new Vector2(-.15f,.27f)*radius,radius*.09f,new Color(.64f,.40f,.20f));
        }
        private static void Disk(VertexHelper vh,Vector2 center,float radius,Color tint)
        {
            int n=vh.currentVertCount;vh.AddVert(center,tint,Vector2.zero);
            for(int i=0;i<=48;i++) { float a=i*Mathf.PI/24;vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,tint,Vector2.zero); }
            for(int i=0;i<48;i++)vh.AddTriangle(n,n+i+1,n+i+2);
        }
    }
}
