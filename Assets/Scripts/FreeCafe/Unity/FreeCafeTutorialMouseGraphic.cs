using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>Vector mouse and rotation arrow; no font glyph or external texture dependency.</summary>
    public sealed class FreeCafeTutorialMouseGraphic : MaskableGraphic
    {
        public bool rightButton;
        public bool rotate;
        private static readonly Color Mint = new Color(.4f, 1f, .84f);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Box(vh, new Vector2(-25, -33), new Vector2(25, 33), new Color(.85f, .93f, 1));
            Box(vh, new Vector2(-22, -30), new Vector2(22, 30), new Color(.06f, .1f, .14f));
            Box(vh, new Vector2(-20, 3), new Vector2(-2, 28), rightButton ? new Color(.22f,.28f,.32f) : Mint);
            Box(vh, new Vector2(2, 3), new Vector2(20, 28), rightButton ? Mint : new Color(.22f,.28f,.32f));
            Box(vh, new Vector2(-1, 9), new Vector2(1, 22), Color.white);
            if (!rotate) return;
            const int count = 24;
            for (int i = 0; i < count; i++)
            {
                float a = (-70 + i * 280f / count) * Mathf.Deg2Rad;
                float b = (-70 + (i + 1) * 280f / count) * Mathf.Deg2Rad;
                Quad(vh, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 43,
                    new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 47,
                    new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 47,
                    new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 43, Mint);
            }
            Vector2 tip = new Vector2(Mathf.Cos(210*Mathf.Deg2Rad), Mathf.Sin(210*Mathf.Deg2Rad))*45;
            Quad(vh, tip + new Vector2(-8, 5), tip + new Vector2(10, -9), tip + new Vector2(9, 10), tip + new Vector2(9, 10), Mint);
        }
        private void Box(VertexHelper vh, Vector2 min, Vector2 max, Color tint) => Quad(vh, min, new Vector2(min.x,max.y), max, new Vector2(max.x,min.y), tint);
        private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int n = vh.currentVertCount;
            vh.AddVert(a,tint,Vector2.zero); vh.AddVert(b,tint,Vector2.zero); vh.AddVert(c,tint,Vector2.zero); vh.AddVert(d,tint,Vector2.zero);
            vh.AddTriangle(n,n+2,n+1); vh.AddTriangle(n,n+3,n+2);
        }
        public void SetMode(bool right, bool rotation)
        {
            if (rightButton == right && rotate == rotation) return;
            rightButton = right; rotate = rotation; SetVerticesDirty();
        }
    }
}
