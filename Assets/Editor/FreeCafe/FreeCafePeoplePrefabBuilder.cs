using System;
using Spotlight.FreeCafe;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>在独立实体 Prefab 内增量补充结构。立牌为代码原生几何，不依赖临时 Canvas 或图片生成。</summary>
    public static class FreeCafePeoplePrefabBuilder
    {
        private const string Geometry = "Assets/Art/FreeCafe/Geometry";
        public static void Configure(FreeEntityView view, EntitySpec spec)
        {
            if (spec.Has(Capability.Customer))
            {
                var person = view.GetComponent<FreeCafePersonView>();
                if (person == null) person = view.gameObject.AddComponent<FreeCafePersonView>();
                person.entityView = view; person.visualRoot = view.body.transform;
                view.label.fontSize = 5f;
                var anchors = Child(view.transform, "PersonAnchors", Vector3.zero);
                person.portraitAnchor = Child(anchors, "Portrait", new Vector3(0,.5f,0));
                person.speechAnchor = Child(anchors, "Speech", new Vector3(0,1.9f,0));
                person.orderAnchor = Child(anchors, "Order", new Vector3(0,1.35f,-.1f));
                if (person.orderCaption == null)
                    person.orderCaption = Caption(person.orderAnchor,"OrderCaption","等待下单",view,4f,new Vector2(3.2f,1.1f));
                person.orderCaption.fontSize=4f;
            }
            if (spec.Has(Capability.BusinessControl))
            {
                var sign = view.GetComponent<FreeCafeBusinessSignView>();
                if (sign == null) sign = view.gameObject.AddComponent<FreeCafeBusinessSignView>();
                sign.entityView = view;view.body.enabled = false;view.label.fontSize = 6.5f;
                var visuals = Child(view.transform,"BusinessSignVisuals",Vector3.zero);
                if (sign.pieces == null || sign.pieces.Length == 0)
                {
                    if (!AssetDatabase.IsValidFolder(Geometry)) AssetDatabase.CreateFolder("Assets/Art/FreeCafe","Geometry");
                    var wood = Material("SignWood",new Color(.31f,.18f,.105f));
                    var paper = Material("SignPaper",new Color(.98f,.91f,.72f));
                    var mesh = RoundedBoard();
                    var left = Piece(visuals,"LeftLeg",mesh,wood,new Vector2(-.85f,-.75f),new Vector2(.16f,1.25f));
                    var right = Piece(visuals,"RightLeg",mesh,wood,new Vector2(.85f,-.75f),new Vector2(.16f,1.25f));
                    var foot = Piece(visuals,"Foot",mesh,wood,new Vector2(0,-1.27f),new Vector2(2.3f,.14f));
                    var rim = Piece(visuals,"BoardBorder",mesh,wood,new Vector2(0,.2f),new Vector2(2.8f,1.65f));
                    sign.face = Piece(visuals,"BoardFace",mesh,paper,new Vector2(0,.2f),new Vector2(2.61f,1.47f));
                    sign.pieces = new[]{left,right,foot,rim,sign.face};
                }
                if (sign.statusCaption == null)
                {
                    sign.statusCaption = Caption(view.transform,"BusinessStatus","点击开店 · 欢迎光临",view,3.4f,new Vector2(2.6f,.7f));
                    sign.statusCaption.transform.localPosition = new Vector3(0,-.2f,-.12f);
                }
                sign.statusCaption.fontSize=3.4f;
                view.label.transform.localPosition = new Vector3(0,.3f,-.1f);
                for (int i=0;i<sign.pieces.Length;i++) sign.pieces[i].sortingOrder=view.body.sortingOrder+i;
                view.label.GetComponent<MeshRenderer>().sortingOrder=view.body.sortingOrder+sign.pieces.Length+1;
                sign.statusCaption.GetComponent<MeshRenderer>().sortingOrder=view.body.sortingOrder+sign.pieces.Length+2;
            }
        }
        private static Transform Child(Transform parent,string name,Vector3 position)
        {
            var child=parent.Find(name);if(child!=null) return child;
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;
        }
        private static TMP_Text Caption(Transform parent,string name,string text,FreeEntityView view,float size,Vector2 dimensions)
        {
            var child=parent.Find(name);
            if(child==null) child=Child(parent,name,Vector3.zero);
            var caption=child.GetComponent<TextMeshPro>();if(caption==null) caption=child.gameObject.AddComponent<TextMeshPro>();
            caption.font=view.label.font;caption.fontSize=size;caption.alignment=TextAlignmentOptions.Center;
            caption.color=new Color(.25f,.15f,.095f);caption.raycastTarget=false;caption.text=text;
            caption.rectTransform.sizeDelta=dimensions;caption.GetComponent<MeshRenderer>().sortingOrder=view.body.sortingOrder+2;
            return caption;
        }
        private static Material Material(string name,Color tint)
        {
            string path=Geometry+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null) return material;
            material=new Material(Shader.Find("Sprites/Default")){name=name,color=tint};AssetDatabase.CreateAsset(material,path);return material;
        }
        private static Mesh RoundedBoard()
        {
            string path=Geometry+"/SignBoard.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null) return mesh;
            const int steps=8,count=4*(steps+1);const float radius=.065f;
            var vertices=new Vector3[count+1];var triangles=new int[count*3];
            for(int corner=0;corner<4;corner++)
            {
                var pivot=corner==0 ? new Vector2(.5f-radius,.5f-radius) : corner==1 ? new Vector2(-.5f+radius,.5f-radius) :
                    corner==2 ? new Vector2(-.5f+radius,-.5f+radius) : new Vector2(.5f-radius,-.5f+radius);
                for(int i=0;i<=steps;i++)
                {
                    float angle=(corner*90f+i*90f/steps)*Mathf.Deg2Rad;int index=corner*(steps+1)+i;
                    vertices[index+1]=pivot+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                    triangles[index*3]=0;triangles[index*3+1]=1+(index+1)%count;triangles[index*3+2]=index+1;
                }
            }
            mesh=new Mesh{name="CafeSignRoundedBoard",vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static MeshRenderer Piece(Transform parent,string name,Mesh mesh,Material material,Vector2 position,Vector2 size)
        {
            var child=Child(parent,name,position);child.localScale=new Vector3(size.x,size.y,1);
            var filter=child.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            var renderer=child.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;return renderer;
        }
    }
}
