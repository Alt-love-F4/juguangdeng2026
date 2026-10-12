#if UNITY_EDITOR
using System;
using System.IO;
using Spotlight.GameFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Editor
{
    public static class FreeCafeFinishingBuilder
    {
        public const string ToastPath="Assets/Resources/Art/UIPrefab/OperationToastPanel.prefab";
        [MenuItem("Tools/Free Cafe/Apply Delivery Zone And Feedback")]
        public static void Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!="Assets/Scenes/CoffeeShop.unity" || scene.isDirty)
                throw new InvalidOperationException("请在已保存的 CoffeeShop 编辑场景执行");
            CreateToast();
            var runtime=UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            var service=Array.Find(runtime.areaRoots[(int)AreaId.Counter].GetComponentsInChildren<FreeEntityView>(true),v=>v.AuthoringEntityId=="service");
            if(service==null) throw new InvalidOperationException("缺少 service 实体");
            var zone=service.GetComponent<FreeCafeDeliveryZone>();
            if(zone==null) zone=Undo.AddComponent<FreeCafeDeliveryZone>(service.gameObject);
            Undo.RecordObject(service.hitbox,"Enlarge delivery zone"); service.hitbox.size=zone.HitboxSize;
            PrefabUtility.RecordPrefabInstancePropertyModifications(service.hitbox);
            var square=CreateSprite("delivery-zone-pixel",false);
            var bellSprite=CreateSprite("delivery-bell",true);
            if(service.transform.Find("DeliveryFrame")==null)
            {
                var frame=new GameObject("DeliveryFrame"); Undo.RegisterCreatedObjectUndo(frame,"Create delivery frame"); frame.transform.SetParent(service.transform,false);
                var size=zone.HitboxSize;
                AddSprite(frame.transform,"Top",square,new Vector2(0,size.y*.5f),new Vector2(size.x,.055f),new Color(1,.8f,.3f,.85f),80);
                AddSprite(frame.transform,"Bottom",square,new Vector2(0,-size.y*.5f),new Vector2(size.x,.055f),new Color(1,.8f,.3f,.85f),80);
                AddSprite(frame.transform,"Left",square,new Vector2(-size.x*.5f,0),new Vector2(.055f,size.y),new Color(1,.8f,.3f,.85f),80);
                AddSprite(frame.transform,"Right",square,new Vector2(size.x*.5f,0),new Vector2(.055f,size.y),new Color(1,.8f,.3f,.85f),80);
                var text=WorldText(frame.transform,"DeliveryCaption","交付区 · 拖入杯子",service.label.font);
                text.transform.localPosition=new Vector3(0,-size.y*.5f-.22f,-.1f);
                text.fontSize=1.8f;
            }
            if(service.transform.Find("DeliveryBell")==null)
            {
                var bell=new GameObject("DeliveryBell",typeof(BoxCollider2D),typeof(FreeCafeDeliveryBell));
                Undo.RegisterCreatedObjectUndo(bell,"Create delivery bell"); bell.transform.SetParent(service.transform,false);
                bell.transform.localPosition=new Vector3(1.95f,-.3f,-.2f);
                var sr=AddSprite(bell.transform,"BellBody",bellSprite,Vector2.zero,new Vector2(.9f,.65f),new Color(.95f,.72f,.25f),500);
                var box=bell.GetComponent<BoxCollider2D>();box.size=new Vector2(1.1f,.9f);box.isTrigger=true;
                var text=WorldText(bell.transform,"BellCaption","按铃交付",service.label.font);text.transform.localPosition=new Vector3(0,-.6f,-.1f);text.fontSize=1.8f;
                var so=new SerializedObject(bell.GetComponent<FreeCafeDeliveryBell>());
                so.FindProperty("runtime").objectReferenceValue=runtime;so.FindProperty("bellBody").objectReferenceValue=sr.transform;so.FindProperty("dome").objectReferenceValue=sr;so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        private static TMP_Text WorldText(Transform parent,string name,string value,TMP_FontAsset font)
        {
            var go=new GameObject(name,typeof(TextMeshPro));go.transform.SetParent(parent,false);
            var text=go.GetComponent<TextMeshPro>();text.font=font;text.text=value;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.28f,.16f,.07f);text.raycastTarget=false;
            text.GetComponent<MeshRenderer>().sortingOrder=501;return text;
        }
        private static SpriteRenderer AddSprite(Transform parent,string name,Sprite sprite,Vector2 position,Vector2 size,Color color,int sorting)
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=position;
            go.transform.localScale=new Vector3(size.x/sprite.bounds.size.x,size.y/sprite.bounds.size.y,1);
            var sr=go.GetComponent<SpriteRenderer>();sr.sprite=sprite;sr.color=color;sr.sortingOrder=sorting;return sr;
        }
        private static Sprite CreateSprite(string name,bool bell)
        {
            string path="Assets/Art/FreeCafe/Geometry/"+name+".png";
            if(!File.Exists(path))
            {
                int width=bell ? 128 : 1, height=bell ? 96 : 1;
                var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
                for(int y=0;y<height;y++) for(int x=0;x<width;x++)
                {
                    float dx=(x-64f)/51f,dy=(y-28f)/48f;
                    bool filled=!bell || y>=28 && y<=76 && dx*dx+dy*dy<=1 || y>=13 && y<=26 && x>=5 && x<=122 || y>=76 && y<=85 && x>=55 && x<=72;
                    texture.SetPixel(x,y,filled ? Color.white : Color.clear);
                }
                texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=bell ? 128 : 1;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static void CreateToast()
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(ToastPath)!=null) return;
            var root=new GameObject("OperationToastPanel",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup),typeof(OperationToastPanel));
            try
            {
                root.GetComponent<Canvas>().overrideSorting=true;root.GetComponent<Canvas>().sortingOrder=100;
                var group=root.GetComponent<CanvasGroup>();group.blocksRaycasts=group.interactable=false;group.alpha=0;
                var go=new GameObject("Banner",typeof(RectTransform),typeof(Image));go.transform.SetParent(root.transform,false);
                var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.6f);rect.sizeDelta=new Vector2(650,54);
                var image=go.GetComponent<Image>();image.color=new Color(.22f,.12f,.075f,.95f);image.raycastTarget=false;
                var textGo=new GameObject("Message",typeof(RectTransform),typeof(TextMeshProUGUI));textGo.transform.SetParent(go.transform,false);
                var text=textGo.GetComponent<TextMeshProUGUI>();text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SimHei SDF.asset");text.text="操作提示";text.fontSize=24;text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=24;text.color=new Color(1,.9f,.72f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.richText=false;
                text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(16,4);text.rectTransform.offsetMax=new Vector2(-16,-4);
                var so=new SerializedObject(root.GetComponent<OperationToastPanel>());so.FindProperty("group").objectReferenceValue=group;so.FindProperty("banner").objectReferenceValue=rect;so.FindProperty("messageText").objectReferenceValue=text;so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,ToastPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
#endif
