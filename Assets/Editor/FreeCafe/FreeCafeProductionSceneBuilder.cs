using System;
using System.IO;
using Spotlight.FreeCafe;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Editor
{
    public static class FreeCafeProductionSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/CoffeeShop.unity";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SimHei SDF.asset");
        [MenuItem("Tools/Free Cafe/Rebuild Production Interface")]
        public static void Rebuild()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("先退出 Play Mode");
            var active=SceneManager.GetActiveScene();
            if(active.isDirty) throw new InvalidOperationException("请先保存当前场景，再重建制作界面；不会覆盖未保存的场景编辑。");
            var scene=active.path==ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
            var runtime=UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            var oldHud=UnityEngine.Object.FindFirstObjectByType<FreeCafeHud>();
            if(runtime==null || oldHud==null || Font==null) throw new InvalidOperationException("已有 CoffeeShop 运行时、HUD 或字体缺失");
            var catalog=JsonUtility.FromJson<CafeCatalog>(runtime.catalogAsset.text);
            new CafeWorld(catalog); // 修改场景前先验证配置
            runtime.prefabLibrary=FreeCafeEntityPrefabBuilder.EnsureCatalog(catalog,runtime.artLibrary,runtime.entityPrefab);
            Directory.CreateDirectory("output/production-validation");
            File.Copy(ScenePath,"output/production-validation/CoffeeShop-before-production.unity",true);
            Undo.RecordObject(runtime,"Enable production interface"); runtime.productionMode=true;
            var warmCamera=UnityEngine.Object.FindFirstObjectByType<Camera>(); if(warmCamera!=null) { Undo.RecordObject(warmCamera,"Warm camera background");warmCamera.clearFlags=CameraClearFlags.SolidColor;warmCamera.backgroundColor=new Color(.29f,.18f,.13f,1f);EditorUtility.SetDirty(warmCamera); }
            Undo.RecordObject(runtime.areaManager,"Production camera closeup");runtime.areaManager.Find(AreaId.Counter).orthographicSize=6.2f;
            DisableObsoletePrototypeImage(scene);
            var hud=runtime.GetComponent<FreeCafeProductionHud>(); if(hud==null) hud=Undo.AddComponent<FreeCafeProductionHud>(runtime.gameObject);
            var gateway=runtime.GetComponent<FreeCafeMiniGameGateway>(); if(gateway==null) gateway=Undo.AddComponent<FreeCafeMiniGameGateway>(runtime.gameObject);
            gateway.runtime=runtime;
            Undo.RecordObject(hud,"Configure production HUD");
            var prior=oldHud.transform.Find("ProductionInterface"); if(prior!=null) Undo.DestroyObjectImmediate(prior.gameObject);
            var root=new GameObject("ProductionInterface",typeof(RectTransform)); root.transform.SetParent(oldHud.transform,false);
            Undo.RegisterCreatedObjectUndo(root,"Build production UI");
            var rect=(RectTransform)root.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
            hud.runtime=runtime; hud.productionRoot=root;
            hud.legacyTop=oldHud.transform.Find("TopBlackBar").gameObject; hud.legacyBottom=oldHud.transform.Find("BottomBlackBar").gameObject;
            var top=Box(rect,"ProductionHeader",Vector2.zero,new Vector2(1920,180),new Color(.095f,.075f,.055f));
            top.anchorMin=new Vector2(0,1); top.anchorMax=Vector2.one; top.pivot=new Vector2(.5f,1); top.offsetMin=new Vector2(0,-180); top.offsetMax=Vector2.zero;
            hud.orderTitle=Text(top,"OrderTitle","交付此订单",new Vector2(-655,65),new Vector2(550,34),25);
            var avatar=Box(top,"CustomerAvatar",new Vector2(-882,-4),new Vector2(68,85),Color.white); hud.customerAvatar=avatar.GetComponent<Image>(); hud.customerAvatar.preserveAspect=true; hud.customerAvatar.raycastTarget=false;
            hud.customerName=Text(top,"CustomerName","等待顾客",new Vector2(-621,22),new Vector2(430,30),20);
            hud.orderDescription=Text(top,"OrderDescription","可提前备料",new Vector2(-599,-25),new Vector2(470,67),19);
            hud.previousOrder=Button(top,"PreviousOrder","‹",new Vector2(-348,10),new Vector2(46,60));
            hud.nextOrder=Button(top,"NextOrder","›",new Vector2(-286,10),new Vector2(46,60));
            hud.machineStatus=Text(top,"MachineStatus","咖啡壶 / 加热壶",new Vector2(80,63),new Vector2(640,34),18);
            hud.timeCaption=Text(top,"TimeLabel","萃取时间 / 完美 7.5s",new Vector2(-27,24),new Vector2(280,25),16);
            hud.timeGauge=Gauge(top,"TimeGauge",new Vector2(87,-1),out hud.timeGreen,out hud.timeGold);
            hud.volumeCaption=Text(top,"VolumeLabel","注水量 / 完美 200ml",new Vector2(-27,-37),new Vector2(280,25),16);
            hud.volumeGauge=Gauge(top,"VolumeGauge",new Vector2(87,-63),out hud.volumeGreen,out hud.volumeGold);
            hud.bookButton=Button(top,"CoffeeBook","咖啡谱",new Vector2(570,45),new Vector2(150,48));
            hud.returnButton=Button(top,"ReturnFromProduction","返回入口",new Vector2(760,45),new Vector2(175,48));
            hud.startBusiness=Button(top,"StartBusiness","开始营业",new Vector2(570,-32),new Vector2(150,48));
            hud.saveButton=Button(top,"SaveWorld","保存",new Vector2(760,-32),new Vector2(175,48));
            var bottom=Box(rect,"ProductionFooter",Vector2.zero,new Vector2(1920,180),new Color(.095f,.075f,.055f));
            bottom.anchorMin=Vector2.zero; bottom.anchorMax=new Vector2(1,0); bottom.pivot=new Vector2(.5f,0); bottom.offsetMin=Vector2.zero; bottom.offsetMax=new Vector2(0,180);
            hud.instruction=Text(bottom,"InteractionHint","拖原料到设备，点击开始加工；壶向下拖动倾倒，抬起停止。",new Vector2(0,65),new Vector2(1830,32),19);
            hud.result=Text(bottom,"DeliveryResult","自由备料，不锁订单步骤",new Vector2(0,32),new Vector2(1830,30),18);
            hud.smallCup=Button(bottom,"SelectSmallCup","小杯 240ml",new Vector2(-821,-11),new Vector2(190,48));
            hud.largeCup=Button(bottom,"SelectLargeCup","大杯 400ml",new Vector2(-611,-11),new Vector2(190,48));
            hud.cupStatus=Text(bottom,"ServingCupStatus","出餐托盘 · 未选杯",new Vector2(-302,-12),new Vector2(390,42),21);
            hud.deliver=Button(bottom,"DeliverCurrentOrder","出品",new Vector2(-20,-11),new Vector2(125,48));
            hud.discard=Button(bottom,"DiscardCup","丢弃杯",new Vector2(125,-11),new Vector2(135,48));
            hud.miniGameButton=Button(bottom,"LatteArtEntry","拉花入口",new Vector2(300,-11),new Vector2(175,48));
            hud.clearMachine=Button(bottom,"ClearSelectedMachine","清空选中设备",new Vector2(531,-11),new Vector2(225,48));
            hud.pauseButton=Button(bottom,"PauseProduction","暂停 / 继续",new Vector2(780,-11),new Vector2(205,48));
            Text(bottom,"ProductionHelp","原料每次一份 · 壶向下拖倾倒 / 向上拖回正 · 允许洒漏 · 右键设备丢弃内容 · 绿区建议 / 金区完美",new Vector2(0,-67),new Vector2(1830,30),18);
            var book=Box(rect,"CoffeeBookPanel",Vector2.zero,new Vector2(950,720),new Color(.13f,.10f,.08f,.99f)); hud.bookPanel=book.gameObject;
            hud.bookTitle=Text(book,"CoffeeBookTitle","咖啡谱",new Vector2(0,306),new Vector2(850,44),28);
            var icon=Box(book,"CoffeeIcon",new Vector2(-342,193),new Vector2(105,120),Color.white);hud.bookIcon=icon.GetComponent<Image>();hud.bookIcon.preserveAspect=true;hud.bookIcon.raycastTarget=false;
            hud.bookBody=Text(book,"CoffeeBookBody","",new Vector2(55,-8),new Vector2(725,540),22);hud.bookBody.alignment=TextAlignmentOptions.TopLeft;hud.bookBody.textWrappingMode=TextWrappingModes.Normal;
            hud.previousRecipe=Button(book,"PreviousRecipe","上一种",new Vector2(-240,-303),new Vector2(170,44));
            hud.closeBook=Button(book,"CloseCoffeeBook","关闭",new Vector2(0,-303),new Vector2(170,44));
            hud.nextRecipe=Button(book,"NextRecipe","下一种",new Vector2(240,-303),new Vector2(170,44));book.gameObject.SetActive(false);
            var mini=Box(rect,"MiniGamePlaceholder",Vector2.zero,new Vector2(820,340),new Color(.13f,.10f,.08f,.99f));hud.miniGamePanel=mini.gameObject;
            Text(mini,"MiniGameTitle","拉花入口已触发",new Vector2(0,112),new Vector2(740,40),28);
            Text(mini,"MiniGameDescription","本轮不实现拉花小游戏。杯子和启动令牌已绑定。\n之后注入 ICafeMiniGamePresenter 即可替换此占位入口。\n取消不会扣除原料，也不会自动判定拉花成功。",new Vector2(0,14),new Vector2(740,135),23);
            hud.cancelMiniGame=Button(mini,"CancelMiniGame","取消并返回制作",new Vector2(0,-112),new Vector2(270,46));mini.gameObject.SetActive(false);
            RebuildCounterPreview(runtime,catalog);
            FreeCafeEntityCaptionBuilder.MigrateLegacyLabels(runtime);
            root.SetActive(false); // scene 默认仍从营业区入口进入
            EditorUtility.SetDirty(hud);EditorUtility.SetDirty(gateway);EditorUtility.SetDirty(runtime);
            FreeCafeHudSceneBuilder.Apply(oldHud,hud);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            CoffeeBookPanelBuilder.ApplySavedPrefab();
            Debug.Log("[Production] 已更新同一个 CoffeeShop 制作区域；其他楼层、镜头、实体Prefab、DayFlow保持不变。");
        }
        
        private static void DisableObsoletePrototypeImage(Scene scene)
        {
            // 保留旧对象身份，只禁用无内容的默认 100x100 白方块；它会遮住加热壶并吞掉世界鼠标输入。
            foreach(var root in scene.GetRootGameObjects())
            {
                if(root.name!="Canvas" || root.GetComponent<Canvas>()==null) continue;
                var child=root.transform.Find("Image");if(child==null || child.childCount!=0) continue;
                var image=child.GetComponent<Image>();var rect=child as RectTransform;
                if(image==null || rect==null || image.sprite!=null || rect.sizeDelta!=new Vector2(100,100) || rect.anchoredPosition!=Vector2.zero) continue;
                Undo.RecordObject(child.gameObject,"Disable obsolete prototype blocker");Undo.RecordObject(image,"Disable prototype raycast");
                image.raycastTarget=false;child.gameObject.SetActive(false);
                Debug.Log("[Production] 禁用旧 Canvas/Image 默认白方块，保留原对象，避免遮住设备或阻挡世界输入。");
            }
        }
        private static void RebuildCounterPreview(FreeCafeRuntime runtime,CafeCatalog catalog)
        {
            FreeCafeEntityPrefabBuilder.RebuildAuthoredLayout(runtime,catalog);
        }
        private static RectTransform Box(Transform parent,string name,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;go.GetComponent<Image>().color=color;return rect;
        }
        private static TMP_Text Text(Transform parent,string name,string content,Vector2 position,Vector2 size,int fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
            var text=go.GetComponent<TextMeshProUGUI>();text.font=Font;text.text=content;text.fontSize=fontSize;text.color=new Color(.95f,.88f,.73f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;return text;
        }
        private static Button Button(Transform parent,string name,string content,Vector2 position,Vector2 size)
        {
            var rect=Box(parent,name,position,size,new Color(.25f,.18f,.11f));var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
            Text(rect,"Label",content,Vector2.zero,size-new Vector2(10,4),21);return button;
        }
        private static Slider Gauge(Transform parent,string name,Vector2 position,out Image green,out Image gold)
        {
            var rect=Box(parent,name,position,new Vector2(410,14),new Color(.8f,.8f,.73f));rect.GetComponent<Image>().raycastTarget=false;
            var greenRect=Box(rect,"SuggestedBand",Vector2.zero,Vector2.zero,new Color(.24f,.6f,.38f));green=greenRect.GetComponent<Image>();green.raycastTarget=false;
            var goldRect=Box(rect,"PerfectBand",Vector2.zero,Vector2.zero,new Color(.94f,.69f,.17f));gold=goldRect.GetComponent<Image>();gold.raycastTarget=false;
            var handle=Box(rect,"Needle",Vector2.zero,new Vector2(5,23),new Color(.16f,.12f,.08f));handle.GetComponent<Image>().raycastTarget=false;
            var slider=rect.gameObject.AddComponent<Slider>();slider.handleRect=handle;slider.direction=Slider.Direction.LeftToRight;slider.interactable=false;slider.navigation=new Navigation { mode=Navigation.Mode.None };return slider;
        }
    }
}
