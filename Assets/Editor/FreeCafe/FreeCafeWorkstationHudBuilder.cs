using System;
using System.IO;
using Spotlight.FreeCafe;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>Builds editable workstation UI on the existing cafe HUD and binds its real controls.</summary>
    public static class FreeCafeWorkstationHudBuilder
    {
        private const string Art="Assets/Art/Gameplay/咖啡馆/台面ui/";
        private static readonly Color Ink=new Color(.27f,.15f,.10f);
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SimHei SDF.asset");
        [MenuItem("Tools/Free Cafe/Apply Workstation HUD")]
        public static void Rebuild()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var player=UnityEngine.Object.FindFirstObjectByType<FreeCafeHud>();
            var production=UnityEngine.Object.FindFirstObjectByType<FreeCafeProductionHud>();
            if(player==null || production==null) throw new InvalidOperationException("Open the existing CoffeeShop scene.");
            var scene=player.gameObject.scene;
            string backup="output/workstation-hud-backup/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup); File.Copy(scene.path,backup+"/CoffeeShop.unity");
            Apply(player,production);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject=production.productionRoot;
            Debug.Log("Workstation HUD applied to "+scene.path+"; backup: "+backup);
        }

        public static void Apply(FreeCafeHud player,FreeCafeProductionHud hud)
        {
            Undo.RecordObject(hud,"Bind workstation HUD");
            if(hud.bookPanel!=null) Undo.SetTransformParent(hud.bookPanel.transform,player.transform,"Preserve coffee book");
            var old=player.transform.Find("ProductionInterface"); if(old!=null) Undo.DestroyObjectImmediate(old.gameObject);
            var preview=player.transform.Find("台面ui"); if(preview!=null) preview.gameObject.SetActive(false);
            var oldReceipt=player.transform.Find("DeliveryReceipt"); if(oldReceipt!=null) Undo.DestroyObjectImmediate(oldReceipt.gameObject);
            var root=Root(player.transform,"ProductionInterface"); hud.productionRoot=root.gameObject;
            hud.legacyTop=hud.legacyBottom=null;
            var header=Root(root,"ProductionHeader"); var footer=Root(root,"ProductionFooter");
            var stack=new GameObject[2];
            for(int i=1;i>=0;i--)
            {
                var sheet=Paper(header,"StackedOrder"+i,new Vector2(0,1),new Vector2(294+i*8,-147-i*8),new Vector2(520,220));
                sheet.GetComponent<Image>().color=new Color(.92f-i*.06f,.86f-i*.06f,.75f-i*.06f);
                sheet.GetComponent<Image>().raycastTarget=false;stack[i]=sheet.gameObject;
            }
            hud.stackedOrderSheets=stack;
            var order=Paper(header,"OrderTicket",new Vector2(0,1),new Vector2(290,-138),new Vector2(520,220));
            hud.orderTitle=Label(order,"OrderTitle","当前订单",new Vector2(0,75),new Vector2(450,30),20);
            var avatar=Paper(order,"CustomerAvatar",new Vector2(.5f,.5f),new Vector2(-185,4),new Vector2(82,100),null);
            hud.customerAvatarFallback=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/UI/Avatars/Unknown.png");
            hud.customerAvatar=avatar.GetComponent<Image>();hud.customerAvatar.preserveAspect=true;hud.customerAvatar.raycastTarget=false;
            hud.customerName=Label(order,"CustomerName","等待客人",new Vector2(48,32),new Vector2(335,29),21);
            hud.orderDescription=Label(order,"OrderDescription","订单将显示在这里",new Vector2(48,-16),new Vector2(335,65),20);
            hud.orderRecipe=Button(order,"OrderRecipe","查看咖啡谱",new Vector2(50,-77),new Vector2(206,40));
            hud.previousOrder=Button(order,"PreviousOrder","←",new Vector2(-198,-77),new Vector2(60,40));
            hud.nextOrder=Button(order,"NextOrder","→",new Vector2(198,-77),new Vector2(60,40));
            var tools=Paper(header,"CafeTools",new Vector2(1,1),new Vector2(-191,-88),new Vector2(326,116));
            hud.bookButton=Button(tools,"CoffeeBook","咖啡谱",new Vector2(-48,3),new Vector2(178,56));
            hud.pauseButton=Button(tools,"PlayerMenu","菜单",new Vector2(105,3),new Vector2(88,56));
            hud.machineStatus=Label(header,"MachineHint","悬停容器查看内容 · 加工时显示实时进度",Vector2.zero,new Vector2(640,30),17);
            Place(hud.machineStatus.rectTransform,new Vector2(.5f,1),new Vector2(40,-32),new Vector2(640,30));
            var delivery=Paper(footer,"DeliveryActions",new Vector2(.5f,0),new Vector2(0,83),new Vector2(636,132));
            hud.cupStatus=Label(delivery,"CupStatus","将饮品放入出餐口",new Vector2(0,37),new Vector2(580,35),18);
            hud.deliver=Button(delivery,"DeliverCup","正式出品",new Vector2(-139,-21),new Vector2(246,56),true);
            hud.discard=Button(delivery,"DiscardCup","废弃",new Vector2(139,-21),new Vector2(246,56));
            hud.instruction=Label(footer,"Instruction","",Vector2.zero,new Vector2(880,45),18);
            Place(hud.instruction.rectTransform,new Vector2(.5f,0),new Vector2(0,178),new Vector2(880,45));
            hud.result=Label(footer,"LastResult","",Vector2.zero,new Vector2(920,28),15);
            Place(hud.result.rectTransform,new Vector2(.5f,0),new Vector2(0,16),new Vector2(920,28));
            var cups=Paper(footer,"CupTools",new Vector2(0,0),new Vector2(193,77),new Vector2(330,120));
            Label(cups,"Caption","出餐杯",new Vector2(0,35),new Vector2(280,24),17);
            hud.smallCup=Button(cups,"SelectSmallCup","小杯",new Vector2(-80,-9),new Vector2(138,47));
            hud.largeCup=Button(cups,"SelectLargeCup","大杯",new Vector2(80,-9),new Vector2(138,47));
            hud.returnButton=Button(footer,"ReturnToCafe","返回咖啡厅",Vector2.zero,new Vector2(300,64));
            Place((RectTransform)hud.returnButton.transform,new Vector2(1,0),new Vector2(-181,58),new Vector2(300,64));
            hud.clearMachine=Button(footer,"EmptySelectedMachine","清空所选容器",Vector2.zero,new Vector2(260,43));
            Place((RectTransform)hud.clearMachine.transform,new Vector2(1,0),new Vector2(-181,119),new Vector2(260,43));
            hud.timeCaption=hud.volumeCaption=null;hud.timeGauge=hud.volumeGauge=null;
            hud.timeGreen=hud.timeGold=hud.volumeGreen=hud.volumeGold=null;
            hud.miniGamePanel=null;hud.miniGameButton=hud.cancelMiniGame=null;
            BindCoffeeBook(player.transform,hud);
            BuildReceipt(player.transform,hud);
            var panels=hud.runtime.GetComponent<FreeCafeContainerPanels>();
            if(panels==null) panels=Undo.AddComponent<FreeCafeContainerPanels>(hud.runtime.gameObject);
            var data=new SerializedObject(panels);
            data.FindProperty("font").objectReferenceValue=Font;
            data.FindProperty("paperFrame").objectReferenceValue=Sprite("物品详情框");
            data.FindProperty("cardWidth").floatValue=310;
            data.ApplyModifiedPropertiesWithoutUndo();
            var bench=UnityEngine.Object.FindFirstObjectByType<FreeCafeLatteArtBench>(FindObjectsInactive.Include);
            if(bench!=null)
            {
                const string woodPath="Assets/Art/FreeCafe/UI/LatteWoodBackground.png";
                var woodImporter=AssetImporter.GetAtPath(woodPath) as TextureImporter;
                if(woodImporter!=null && woodImporter.textureType!=TextureImporterType.Sprite)
                { woodImporter.textureType=TextureImporterType.Sprite;woodImporter.spriteImportMode=SpriteImportMode.Single;woodImporter.SaveAndReimport(); }
                bench.woodBackground=AssetDatabase.LoadAssetAtPath<Sprite>(woodPath);
                EditorUtility.SetDirty(bench);
            }
            root.gameObject.SetActive(false);
            EditorUtility.SetDirty(hud);EditorUtility.SetDirty(panels);
        }

        private static void BindCoffeeBook(Transform canvas,FreeCafeProductionHud hud)
        {
            if(hud.bookPanel==null)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FreeCafe/CoffeeBookPanel.prefab");
                if(prefab==null) throw new InvalidOperationException("Missing existing CoffeeBookPanel prefab.");
                hud.bookPanel=(GameObject)PrefabUtility.InstantiatePrefab(prefab,canvas);
                Undo.RegisterCreatedObjectUndo(hud.bookPanel,"Restore coffee book");
                hud.bookPanel.name="CoffeeBookPanel";
            }
            var view=hud.bookPanel.GetComponent<CoffeeBookPanelView>();
            hud.bookTitle=view.title;hud.bookBody=view.body;hud.bookNote=view.note;
            hud.previousRecipe=view.previous;hud.nextRecipe=view.next;hud.closeBook=view.close;
            hud.bookIcon=null;hud.bookPanel.SetActive(false);
        }

        private static void BuildReceipt(Transform canvas,FreeCafeProductionHud hud)
        {
            var root=Root(canvas,"DeliveryReceipt");
            var dim=root.gameObject.AddComponent<Image>();dim.color=new Color(.10f,.055f,.035f,.7f);dim.raycastTarget=true;
            var panel=Paper(root,"ReceiptPaper",new Vector2(.5f,.5f),Vector2.zero,new Vector2(760,650));
            Label(panel,"Caption","咖 啡 馆  ·  出 品 回 执",new Vector2(0,271),new Vector2(650,40),26);
            var seal=new GameObject("QualitySeal",typeof(RectTransform),typeof(CafeReceiptFace));seal.transform.SetParent(panel,false);
            Place((RectTransform)seal.transform,new Vector2(.5f,.5f),new Vector2(0,159),new Vector2(135,135));
            hud.receiptFace=seal.GetComponent<CafeReceiptFace>();hud.receiptFace.raycastTarget=false;
            hud.receiptGrade=Label(panel,"Grade","良好 · 75 分",new Vector2(0,62),new Vector2(650,47),32);
            hud.receiptProduct=Label(panel,"Product","",new Vector2(0,6),new Vector2(650,36),23);
            hud.receiptDetails=Label(panel,"Details","",new Vector2(0,-94),new Vector2(645,132),21);
            hud.receiptDetails.textWrappingMode=TextWrappingModes.Normal;
            hud.receiptRewards=Label(panel,"Rewards","",new Vector2(0,-191),new Vector2(650,42),25);
            hud.confirmReceipt=Button(panel,"ConfirmReceipt","确定 · 继续制作",new Vector2(0,-266),new Vector2(342,58),true);
            hud.receiptPanel=root.gameObject;root.gameObject.SetActive(false);
        }
        private static Sprite Sprite(string name)
        {
            string path=Art+name+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            var border=name=="选中" || name=="未选中" ? new Vector4(65,24,65,24) : new Vector4(45,40,45,40);
            if(importer!=null && importer.spriteBorder!=border)
            {
                importer.spriteBorder=border;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static RectTransform Root(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        private static RectTransform Paper(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,string art="物品详情框")
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.sprite=art==null ? null : Sprite(art);image.type=Image.Type.Sliced;
            image.raycastTarget=false;
            var rect=(RectTransform)go.transform;Place(rect,anchor,position,size);return rect;
        }
        private static TMP_Text Label(Transform parent,string name,string value,Vector2 position,Vector2 size,int fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            Place((RectTransform)go.transform,new Vector2(.5f,.5f),position,size);
            var text=go.GetComponent<TextMeshProUGUI>();text.font=Font;text.text=value;text.fontSize=fontSize;text.color=Ink;
            text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;
            return text;
        }
        private static Button Button(Transform parent,string name,string caption,Vector2 position,Vector2 size,bool primary=false)
        {
            var rect=Paper(parent,name,new Vector2(.5f,.5f),position,size,primary ? "选中" : "未选中");
            var image=rect.GetComponent<Image>();image.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.navigation=new Navigation { mode=Navigation.Mode.None };
            var colors=button.colors;colors.highlightedColor=new Color(1.08f,1.05f,.92f);colors.pressedColor=new Color(.8f,.72f,.59f);colors.disabledColor=new Color(.65f,.65f,.65f,.65f);button.colors=colors;
            Label(rect,"Label",caption,Vector2.zero,size-new Vector2(20,8),primary ? 24 : 21);
            return button;
        }
        private static void Place(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size)
        { rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one; }
    }
}
