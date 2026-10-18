using System;
using Spotlight.FreeCafe;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>在独立实体 Prefab 内增量补充结构。营业挂牌复用正式手绘素材。</summary>
    public static class FreeCafePeoplePrefabBuilder
    {
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
                FreeCafeHangingSignBuilder.ConfigureBusinessSign(view);
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
    }
}
