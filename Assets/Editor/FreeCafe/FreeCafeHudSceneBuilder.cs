using System;
using Spotlight.FreeCafe;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>只重构既有 HUD Canvas；不重建实体、楼层或输入系统。</summary>
    public static class FreeCafeHudSceneBuilder
    {
        private static readonly Color Paper=new Color(.96f,.89f,.75f);
        private static readonly Color Ink=new Color(.25f,.15f,.095f);
        private static readonly Color Accent=new Color(.39f,.47f,.32f);
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SimHei SDF.asset");
        [MenuItem("Tools/Free Cafe/Rebuild Player HUD")]
        public static void Rebuild() => FreeCafeProductionSceneBuilder.Rebuild();

        public static void Apply(FreeCafeHud hud,FreeCafeProductionHud production)
        {
            Undo.RecordObject(hud,"Configure player HUD");
            if(hud.runtime!=null && hud.runtime.worldCamera!=null)
            {
                Undo.RecordObject(hud.runtime.worldCamera,"Warm cafe camera background");
                hud.runtime.worldCamera.clearFlags=CameraClearFlags.SolidColor;
                hud.runtime.worldCamera.backgroundColor=new Color(.29f,.18f,.13f,1f);
            }
            foreach(string name in new[]{"TopBlackBar","BottomBlackBar"})
            {
                var old=hud.transform.Find(name);
                if(old==null) continue;
                old.gameObject.SetActive(false);
                var image=old.GetComponent<Image>();if(image!=null) { image.enabled=false;image.raycastTarget=false; }
            }
            var previous=hud.transform.Find("PlayerInterface");if(previous!=null) Undo.DestroyObjectImmediate(previous.gameObject);
            var root=Root(hud.transform,"PlayerInterface");hud.playerRoot=root.gameObject;
            var info=Card(root,"CafeLedger",new Vector2(0,1),new Vector2(258,-91),new Vector2(468,138));
            Label(info,"CafeTitle","沸点咖啡馆",new Vector2(0,45),new Vector2(410,30),24);
            hud.summary=Label(info,"Resources","",new Vector2(0,-13),new Vector2(420,79),19);
            var orders=Card(root,"GuestTickets",new Vector2(1,1),new Vector2(-450,-90),new Vector2(850,136));
            Label(orders,"Heading","来客订单",new Vector2(0,45),new Vector2(790,27),21);
            hud.orderButtons=new Button[3];hud.orderLabels=new TMP_Text[3];
            for(int i=0;i<3;i++)
            {
                hud.orderButtons[i]=Button(orders,"Order"+i,"等待顾客",new Vector2(-274+i*274,-12),new Vector2(258,70));
                hud.orderLabels[i]=hud.orderButtons[i].GetComponentInChildren<TMP_Text>();
                hud.orderLabels[i].fontSize=18;
            }
            var navigation=Card(root,"PlayerNavigation",new Vector2(1,0),new Vector2(-302,60),new Vector2(556,76));
            hud.areaButtons=new Button[4];
            hud.areaButtons[0]=Button(navigation,"ReturnToCafe","返回大堂",new Vector2(-140,0),new Vector2(230,48));
            hud.areaButtons[1]=Button(navigation,"EnterProduction","前往制作台",new Vector2(112,0),new Vector2(252,48),true);
            var tools=Card(root,"PlayerTools",new Vector2(0,0),new Vector2(200,60),new Vector2(350,76));
            hud.recipeButton=Button(tools,"CoffeeBook","咖啡谱",new Vector2(-76,0),new Vector2(156,48));
            hud.pauseButton=Button(tools,"PlayerMenu","菜单",new Vector2(88,0),new Vector2(136,48));
            var note=Card(root,"CafeNote",new Vector2(.5f,0),new Vector2(-55,65),new Vector2(650,86),false);
            hud.hint=Label(note,"Hint","",new Vector2(0,14),new Vector2(610,37),16);
            hud.selection=Label(note,"Selection","",new Vector2(0,-24),new Vector2(610,25),15);
            hud.areaName=Label(root,"CurrentArea","一楼大堂",Vector2.zero,new Vector2(360,34),24);
            Place(hud.areaName.transform,new Vector2(.5f,1),new Vector2(-150,-40),new Vector2(360,34));
            var recipe=hud.recipePanel;
            if(recipe!=null)
            {
                StyleCard((RectTransform)recipe.transform,true);
                foreach(var text in recipe.GetComponentsInChildren<TMP_Text>(true)) { text.color=Ink;text.fontSize=Mathf.Max(18,text.fontSize); }
                StyleButton(hud.recipeCloseButton,false);
                recipe.SetActive(false);
            }
            if(hud.startButton!=null) hud.startButton.gameObject.SetActive(false);
            if(hud.saveButton!=null) hud.saveButton.gameObject.SetActive(false);
            if(production!=null)
            {
                production.legacyTop=null;production.legacyBottom=null;
                FreeCafeWorkstationHudBuilder.Apply(hud,production);
            }
        }
        private static void ApplyProduction(FreeCafeProductionHud hud)
        {
            foreach(var text in hud.productionRoot.GetComponentsInChildren<TMP_Text>(true)) text.color=Ink;
            foreach(var button in hud.productionRoot.GetComponentsInChildren<Button>(true)) StyleButton(button,button==hud.deliver);
            var top=(RectTransform)hud.productionRoot.transform.Find("ProductionHeader");
            var bottom=(RectTransform)hud.productionRoot.transform.Find("ProductionFooter");
            ClearBackground(top);ClearBackground(bottom);
            // 容器铺满 Canvas 但自身没有 Graphic，卡片间隙不吞掉实体指针。
            Stretch(top);Stretch(bottom);
            var order=Card(top,"OrderTicket",new Vector2(0,1),new Vector2(319,-96),new Vector2(590,148));
            Label(order,"TicketCaption","ORDER / 订单签",new Vector2(-195,53),new Vector2(180,23),13);
            Move(hud.orderTitle,order,new Vector2(70,51),new Vector2(400,28),22);
            Move(hud.customerAvatar,order,new Vector2(-234,-13),new Vector2(65,80));
            Move(hud.customerName,order,new Vector2(30,15),new Vector2(415,27),19);
            Move(hud.orderDescription,order,new Vector2(12,-30),new Vector2(402,58),17);
            Move(hud.previousOrder,order,new Vector2(221,-9),new Vector2(42,50));
            Move(hud.nextOrder,order,new Vector2(266,-9),new Vector2(42,50));
            var gauges=Card(top,"ExtractionCard",new Vector2(.5f,1),new Vector2(-58,-96),new Vector2(510,148),false);
            Move(hud.machineStatus,gauges,new Vector2(0,51),new Vector2(470,28),16);
            Move(hud.timeCaption,gauges,new Vector2(0,19),new Vector2(462,25),16);
            Move(hud.timeGauge,gauges,new Vector2(0,-2),new Vector2(458,12));
            Move(hud.volumeCaption,gauges,new Vector2(0,-29),new Vector2(462,25),16);
            Move(hud.volumeGauge,gauges,new Vector2(0,-51),new Vector2(458,12));
            var tools=Card(top,"ProductionTools",new Vector2(1,1),new Vector2(-268,-60),new Vector2(486,76));
            Move(hud.bookButton,tools,new Vector2(-158,0),new Vector2(142,48));
            Move(hud.returnButton,tools,new Vector2(12,0),new Vector2(174,48));
            Move(hud.pauseButton,tools,new Vector2(172,0),new Vector2(126,48));
            hud.pauseButton.GetComponentInChildren<TMP_Text>().text="菜单";
            var cups=Card(bottom,"CupChoiceCard",new Vector2(0,0),new Vector2(217,71),new Vector2(386,98));
            Label(cups,"Heading","选择出餐杯",new Vector2(0,29),new Vector2(344,25),18);
            Move(hud.smallCup,cups,new Vector2(-91,-14),new Vector2(172,44));
            Move(hud.largeCup,cups,new Vector2(91,-14),new Vector2(172,44));
            var serving=Card(bottom,"ServingCard",new Vector2(1,0),new Vector2(-602,71),new Vector2(1156,98));
            Move(hud.cupStatus,serving,new Vector2(-372,2),new Vector2(374,50),20);
            Move(hud.deliver,serving,new Vector2(-114,0),new Vector2(120,50));
            Move(hud.discard,serving,new Vector2(30,0),new Vector2(138,50));
            Move(hud.miniGameButton,serving,new Vector2(190,0),new Vector2(156,50));
            Move(hud.clearMachine,serving,new Vector2(418,0),new Vector2(252,50));
            hud.miniGameButton.GetComponentInChildren<TMP_Text>().text="拉花";
            hud.miniGameButton.gameObject.SetActive(false);
            var status=Card(bottom,"ProductionNote",new Vector2(.5f,0),new Vector2(0,157),new Vector2(1420,48),false);
            Move(hud.instruction,status,new Vector2(-155,0),new Vector2(1030,32),16);
            Move(hud.result,status,new Vector2(510,0),new Vector2(350,32),14);
            var resultText=hud.result;resultText.enableAutoSizing=true;resultText.fontSizeMin=12;resultText.fontSizeMax=16;
            var help=bottom.Find("Help");if(help!=null) help.gameObject.SetActive(false);
            hud.startBusiness.gameObject.SetActive(false);hud.saveButton.gameObject.SetActive(false);
            foreach(var panel in new[]{hud.bookPanel,hud.miniGamePanel})
            {
                StyleCard((RectTransform)panel.transform,true);
                foreach(var text in panel.GetComponentsInChildren<TMP_Text>(true)) text.color=Ink;
                panel.SetActive(false);
            }
        }
        private static RectTransform Root(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            Undo.RegisterCreatedObjectUndo(go,"Build cafe player UI");Stretch((RectTransform)go.transform);return (RectTransform)go.transform;
        }
        private static RectTransform Card(Transform parent,string name,Vector2 anchor,Vector2 offset,Vector2 size,bool block=true)
        {
            var rect=Root(parent,name);Place(rect,anchor,offset,size);StyleCard(rect,block);return rect;
        }
        private static void StyleCard(RectTransform rect,bool block)
        {
            var oldImage=rect.GetComponent<Image>();if(oldImage!=null) Undo.DestroyObjectImmediate(oldImage);
            var surface=rect.GetComponent<CafeHudSurface>();if(surface==null) surface=rect.gameObject.AddComponent<CafeHudSurface>();
            surface.color=Paper;surface.raycastTarget=block;
        }
        private static void ClearBackground(RectTransform rect)
        {
            var image=rect.GetComponent<Image>();if(image!=null) { image.enabled=false;image.raycastTarget=false; }
        }
        private static TMP_Text Label(Transform parent,string name,string content,Vector2 offset,Vector2 size,int fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var text=go.GetComponent<TextMeshProUGUI>();text.text=content;text.font=Font;text.fontSize=fontSize;text.color=Ink;
            text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;Place(go.transform,new Vector2(.5f,.5f),offset,size);return text;
        }
        private static Button Button(Transform parent,string name,string content,Vector2 offset,Vector2 size,bool accent=false)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CafeHudSurface),typeof(Button));go.transform.SetParent(parent,false);
            var button=go.GetComponent<Button>();Label(go.transform,"Label",content,Vector2.zero,size-new Vector2(12,6),21);
            Place(go.transform,new Vector2(.5f,.5f),offset,size);StyleButton(button,accent);return button;
        }
        private static void StyleButton(Button button,bool accent)
        {
            if(button==null) return;
            var image=button.GetComponent<Image>();if(image!=null) Undo.DestroyObjectImmediate(image);
            var surface=button.GetComponent<CafeHudSurface>();if(surface==null) surface=button.gameObject.AddComponent<CafeHudSurface>();
            surface.color=accent ? Accent : new Color(.91f,.8f,.62f);surface.radius=10;surface.borderWidth=1.5f;surface.shadow=false;surface.raycastTarget=true;
            button.targetGraphic=surface;button.navigation=new Navigation { mode=Navigation.Mode.None };
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.96f,.84f);colors.pressedColor=new Color(.78f,.73f,.61f);colors.disabledColor=new Color(.73f,.7f,.64f,.6f);button.colors=colors;
            foreach(var text in button.GetComponentsInChildren<TMP_Text>(true)) text.color=accent ? new Color(1,.96f,.86f) : Ink;
        }
        private static void Move(Component component,Transform parent,Vector2 position,Vector2 size,int fontSize=0)
        {
            component.transform.SetParent(parent,false);Place(component.transform,new Vector2(.5f,.5f),position,size);
            if(component is TMP_Text text && fontSize>0) text.fontSize=fontSize;
            if(component is Button button)
            {
                var label=button.GetComponentInChildren<TMP_Text>();if(label!=null) { Place(label.transform,new Vector2(.5f,.5f),Vector2.zero,size-new Vector2(10,4));label.fontSize=19; }
            }
        }
        private static void Place(Transform target,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var rect=(RectTransform)target;rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero; }
    }
}
