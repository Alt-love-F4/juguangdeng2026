using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Spotlight.GameFlow;

namespace Spotlight.FreeCafe
{
    public sealed class FreeCafeHud : GameFlowPanel
    {
        public FreeCafeRuntime runtime;
        public GameObject playerRoot;
        public bool IsModalOpen => recipePanel!=null && recipePanel.activeInHierarchy;
        public TMP_Text summary,hint,selection,areaName;
        public Button mapButton;
        public Button startButton,pauseButton,saveButton,recipeButton;
        public Button[] areaButtons,orderButtons;
        public TMP_Text[] orderLabels;
        public Button previousOrder, nextOrder;
        public TMP_Text orderPage;
        private int orderOffset;
        public GameObject recipePanel;
        public TMP_Text recipeTitle,recipeBody;
        public Button recipeCloseButton;
        [Header("Split ledger (optional)")]
        public TMP_Text dayLabel, statusLabel, moneyLabel, reputationLabel, stockLabel;
        [Tooltip("Ticket backgrounds for the focused / waiting guest order.")]
        public Sprite orderFocusSprite, orderIdleSprite;
        private string[] requestIds=Array.Empty<string>();
        private Button[] boundOrderButtons=Array.Empty<Button>();
        private UnityAction[] orderActions=Array.Empty<UnityAction>();
        private Image[] orderAvatars=Array.Empty<Image>();
        private OrderLabelLayout[] orderLabelLayouts=Array.Empty<OrderLabelLayout>();
        private readonly System.Collections.Generic.List<Image> createdOrderAvatars=new System.Collections.Generic.List<Image>();
        private struct OrderLabelLayout
        {
            public RectTransform rect;
            public Vector2 offsetMin,offsetMax;
            public float avatarInset;
        }
        private bool bound;
        public Sprite floorCurrentSprite, floorIdleSprite;
        private RectTransform headerDock;
        private Transform currencyDock, floorSwitcher, productionMenus;
        private FreeCafeMiniGameGateway miniGameGateway;
        public bool IsWorkstation => runtime!=null && runtime.World!=null && runtime.World.ProductionEnabled && runtime.World.State.currentArea==AreaId.Counter;

        private void LateUpdate()
        {
            if (playerRoot == null) return;
            if (headerDock == null) headerDock = playerRoot.transform.Find("CafeLobbyHud/HeaderDock") as RectTransform;
            if (headerDock != null && playerRoot.transform is RectTransform content)
                headerDock.localScale = Vector3.one * Mathf.Min(1f, content.rect.width / 1920f);
        }

        private CoffeeBookPanelView bookView;
        private int recipeIndex;
        protected override void Awake()
        {
            base.Awake();
            // Authoring may leave the guide visible. Gameplay starts with no modal over the world.
            if(recipePanel!=null) recipePanel.SetActive(false);
            TryBind();
        }
        private void OnEnable() { TryBind(); Refresh(); }
        private void Start() { TryBind(); Refresh(); }
        private void TryBind()
        {
            if(bound || runtime==null)
                return;
            bound=true;
            miniGameGateway=runtime.GetComponent<FreeCafeMiniGameGateway>();
            bookView = recipePanel == null ? null : recipePanel.GetComponent<CoffeeBookPanelView>();
            if(bookView != null)
            {
                bookView.RecipeSelected += SelectRecipe;
                if(bookView.previous!=null) bookView.previous.onClick.AddListener(PreviousRecipe);
                if(bookView.next!=null) bookView.next.onClick.AddListener(NextRecipe);
            }
            runtime.ViewChanged+=Refresh;
            if(mapButton!=null) mapButton.onClick.AddListener(OpenMap);
            if(pauseButton!=null) pauseButton.onClick.AddListener(OpenPlayerMenu);
            if(recipeButton!=null) recipeButton.onClick.AddListener(ToggleRecipeTips);
            if(recipeCloseButton!=null) recipeCloseButton.onClick.AddListener(CloseRecipeTips);
            if(previousOrder!=null) previousOrder.onClick.AddListener(PreviousOrder);
            if(nextOrder!=null) nextOrder.onClick.AddListener(NextOrder);
            if(areaButtons!=null && areaButtons.Length>0 && areaButtons[0]!=null) areaButtons[0].onClick.AddListener(Cafe);
            if(areaButtons!=null && areaButtons.Length>1 && areaButtons[1]!=null) areaButtons[1].onClick.AddListener(Counter);
            if(areaButtons!=null && areaButtons.Length>2 && areaButtons[2]!=null) areaButtons[2].onClick.AddListener(SecondFloor);
            if(areaButtons!=null && areaButtons.Length>3 && areaButtons[3]!=null) areaButtons[3].onClick.AddListener(ThirdFloor);
            BindOrderAvatars();
            boundOrderButtons=orderButtons==null ? Array.Empty<Button>() : (Button[])orderButtons.Clone();
            orderActions=new UnityAction[boundOrderButtons.Length];
            for(int index=0;index<boundOrderButtons.Length;index++)
            {
                int orderIndex=index;
                orderActions[index]=()=>Focus(orderIndex);
                if(boundOrderButtons[index]!=null) boundOrderButtons[index].onClick.AddListener(orderActions[index]);
            }
        }
        private void BindOrderAvatars()
        {
            ClearOrderAvatars();
            if(!Application.isPlaying) return;
            int count=Math.Max(orderButtons?.Length ?? 0,orderLabels?.Length ?? 0);
            orderAvatars=new Image[count];
            orderLabelLayouts=new OrderLabelLayout[count];
            for(int index=0;index<count;index++)
            {
                var button=orderButtons!=null && index<orderButtons.Length ? orderButtons[index] : null;
                var label=orderLabels!=null && index<orderLabels.Length ? orderLabels[index] : null;
                var textRect=label!=null ? label.rectTransform : null;
                // Use each row's own text parent, not the shared card, so multiple slots stay independent.
                var parent=textRect!=null ? textRect.parent : button!=null ? button.transform : null;
                if(parent==null) continue;
                float size=64f;
                if(textRect!=null)
                {
                    size=Mathf.Min(size,Mathf.Max(0,textRect.rect.height),Mathf.Max(0,textRect.rect.width*.3f));
                    orderLabelLayouts[index]=new OrderLabelLayout {
                        rect=textRect,offsetMin=textRect.offsetMin,offsetMax=textRect.offsetMax,avatarInset=size+8f
                    };
                }
                var child=parent.Find("CharacterAvatar");
                if(child==null)
                {
                    var owner=new GameObject("CharacterAvatar",typeof(RectTransform),typeof(Image));
                    owner.layer=parent.gameObject.layer;
                    owner.transform.SetParent(parent,false);
                    child=owner.transform;
                    createdOrderAvatars.Add(owner.GetComponent<Image>());
                }
                var avatar=child.GetComponent<Image>();
                if(avatar==null) avatar=child.gameObject.AddComponent<Image>();
                orderAvatars[index]=avatar;
                avatar.raycastTarget=false;
                avatar.preserveAspect=true;
                var rect=avatar.rectTransform;
                rect.localRotation=Quaternion.identity;
                rect.localScale=Vector3.one;
                rect.pivot=new Vector2(.5f,.5f);
                if(textRect!=null)
                {
                    rect.anchorMin=rect.anchorMax=new Vector2(textRect.anchorMin.x,Mathf.Lerp(textRect.anchorMin.y,textRect.anchorMax.y,textRect.pivot.y));
                    rect.anchoredPosition=new Vector2(textRect.offsetMin.x+size*.5f,textRect.anchoredPosition.y+textRect.rect.center.y);
                }
                else
                {
                    rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);
                    rect.anchoredPosition=new Vector2(8f+size*.5f,0);
                }
                rect.sizeDelta=new Vector2(size,size);
                avatar.sprite=null;
                avatar.gameObject.SetActive(false);
            }
        }
        private void SetOrderAvatar(int index,Sprite sprite)
        {
            if(index<0 || index>=orderAvatars.Length) return;
            var avatar=orderAvatars[index];
            bool visible=avatar!=null && sprite!=null;
            if(avatar!=null)
            {
                avatar.sprite=sprite;
                avatar.gameObject.SetActive(visible);
            }
            var layout=orderLabelLayouts[index];
            if(layout.rect==null) return;
            // Leave anchors, pivot, vertical placement and the right edge exactly as authored.
            layout.rect.offsetMin=layout.offsetMin+(visible ? new Vector2(layout.avatarInset,0) : Vector2.zero);
            layout.rect.offsetMax=layout.offsetMax;
        }
        private void ClearOrderAvatars()
        {
            for(int index=0;index<orderAvatars.Length;index++) SetOrderAvatar(index,null);
            orderAvatars=Array.Empty<Image>();
            orderLabelLayouts=Array.Empty<OrderLabelLayout>();
        }
        protected override void OnBeforeDestroy()
        {
            ClearOrderAvatars();
            foreach(var avatar in createdOrderAvatars)
                if(avatar!=null) Destroy(avatar.gameObject);
            createdOrderAvatars.Clear();
            base.OnBeforeDestroy();
        }
        private void OnDisable()
        {
            ClearOrderAvatars();
            if(Application.isPlaying) CloseRecipeTips();
            if(!bound) return;
            bound=false;
            if(bookView != null)
            {
                bookView.RecipeSelected -= SelectRecipe;
                if(bookView.previous!=null) bookView.previous.onClick.RemoveListener(PreviousRecipe);
                if(bookView.next!=null) bookView.next.onClick.RemoveListener(NextRecipe);
            }
            if(runtime!=null) runtime.ViewChanged-=Refresh;
            if(mapButton!=null) mapButton.onClick.RemoveListener(OpenMap);
            if(pauseButton!=null) pauseButton.onClick.RemoveListener(OpenPlayerMenu);
            if(recipeButton!=null) recipeButton.onClick.RemoveListener(ToggleRecipeTips);
            if(recipeCloseButton!=null) recipeCloseButton.onClick.RemoveListener(CloseRecipeTips);
            if(previousOrder!=null) previousOrder.onClick.RemoveListener(PreviousOrder);
            if(nextOrder!=null) nextOrder.onClick.RemoveListener(NextOrder);
            if(areaButtons!=null && areaButtons.Length>0 && areaButtons[0]!=null) areaButtons[0].onClick.RemoveListener(Cafe);
            if(areaButtons!=null && areaButtons.Length>1 && areaButtons[1]!=null) areaButtons[1].onClick.RemoveListener(Counter);
            if(areaButtons!=null && areaButtons.Length>2 && areaButtons[2]!=null) areaButtons[2].onClick.RemoveListener(SecondFloor);
            if(areaButtons!=null && areaButtons.Length>3 && areaButtons[3]!=null) areaButtons[3].onClick.RemoveListener(ThirdFloor);
            for(int index=0;index<boundOrderButtons.Length;index++)
                if(boundOrderButtons[index]!=null) boundOrderButtons[index].onClick.RemoveListener(orderActions[index]);
            boundOrderButtons=Array.Empty<Button>();
            orderActions=Array.Empty<UnityAction>();
        }
        private void Cafe()=>runtime.Enter(AreaId.Cafe); private void Counter()=>runtime.Enter(AreaId.Counter);
        private void SecondFloor()=>runtime.Enter(AreaId.TopFloor);
        private void ThirdFloor()=>runtime.Enter(AreaId.Basement);

        private void PreviousOrder() => ChangeOrderPage(-1);
        private void NextOrder() => ChangeOrderPage(1);
        private void ChangeOrderPage(int direction)
        {
            if(runtime==null || runtime.World==null || runtime.InputBlocked) return;
            if(IsWorkstation)
            {
                string previous=runtime.World.State.focusedRequestId;
                var result=runtime.World.PageProductionOrder(direction);
                if(result.success && previous!=runtime.World.State.focusedRequestId)
                    Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Page);
                runtime.Report(result);
                return;
            }
            int slots=Math.Max(1, Math.Max(orderButtons?.Length ?? 0, orderLabels?.Length ?? 0));
            int pages=(runtime.World.State.requests.Count+slots-1)/slots;
            if(pages<2) return;
            orderOffset=((orderOffset/slots+direction+pages)%pages)*slots;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Page);
            Refresh();
        }

        private void Focus(int index)
        {
            if(runtime!=null && runtime.World!=null && !runtime.InputBlocked && index>=0 && index<requestIds.Length && !string.IsNullOrEmpty(requestIds[index]))
                runtime.World.Focus(requestIds[index]);
        }
        public void OpenFromFlow(GameFlowBootstrap flow)
        {
            if (flow != null) SetFlow(flow);
            TryBind();
        }
        public void OpenMap()
        {
            if(runtime==null) return;
            runtime.CancelDrag();
            var flow = Flow != null ? Flow : FindFirstObjectByType<GameFlowBootstrap>();
            flow?.OpenMap();
        }
        public void OpenPlayerMenu()
        {
            if(runtime==null || runtime.World==null || (FreeCafeGMPanel.Instance!=null && FreeCafeGMPanel.Instance.IsVisible)) return;
            CloseRecipeTips();
            var production=runtime.GetComponent<FreeCafeProductionHud>();
            if(production!=null) production.CloseOverlays();
            runtime.CancelDrag();
            if (Flow != null)
            {
                Flow.OpenPause();
                return;
            }
            var flow=UnityEngine.Object.FindFirstObjectByType<GameFlowBootstrap>();
            if(flow!=null) { SetFlow(flow);flow.OpenPause(); }
            else runtime.TogglePause();
        }
        private void ToggleRecipeTips()
        {
            if(recipePanel==null || runtime==null || (runtime.InputBlocked && !IsModalOpen)) return;
            runtime.CancelDrag(); recipePanel.SetActive(!recipePanel.activeSelf);
            if(recipePanel.activeSelf) RefreshRecipeTips();
        }
        public void CloseRecipeTips() { if(recipePanel!=null) recipePanel.SetActive(false); }
        private void PreviousRecipe() => SelectRecipe(recipeIndex - 1);
        private void NextRecipe() => SelectRecipe(recipeIndex + 1);
        private void SelectRecipe(int index)
        {
            if(runtime == null || runtime.World == null) return;
            int count = runtime.World.State.unlockedProducts.Count;
            if(count == 0) return;
            int previous = recipeIndex;
            recipeIndex = (index % count + count) % count;
            RefreshRecipeTips();
            if(previous != recipeIndex) Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Page);
        }
        private void RefreshRecipeTips()
        {
            if(runtime==null || runtime.World==null) return;
            if(bookView != null && runtime.World.ProductionEnabled)
            {
                bookView.Show(runtime, recipeIndex);
                return;
            }
            if(runtime.World.ProductionEnabled)
            {
                if(recipeTitle!=null) recipeTitle.text="咖啡谱 · 已学会的餐品";
                if(recipeBody==null) return;
                var productionText=new StringBuilder();
                foreach(var id in runtime.World.State.unlockedProducts)
                {
                    var product=runtime.World.Product(id);
                    productionText.Append("【").Append(FreeCafeProductionText.Recipe(product.label)).Append("】").AppendLine(FreeCafeProductionText.Recipe(product.description)).AppendLine(FreeCafeProductionText.Recipe(product.tutorial)).AppendLine();
                }
                recipeBody.text=productionText.ToString(); return;
            }
            if(recipeTitle!=null) recipeTitle.text="餐品制作提示（配表）";
            if(recipeBody==null) return;
            var text=new StringBuilder();
            var catalog=runtime.World.Catalog;
            if(catalog.recipes==null || catalog.recipes.Length==0)
            {
                recipeBody.text="当前没有配置制作流程。请在 catalog.json 的 recipes 表中添加 Recipe ID 和步骤。";
                return;
            }
            foreach(var recipe in catalog.recipes)
            {
                if(recipe==null) continue;
                var product=runtime.World.Product(recipe.productId);
                text.Append("【").Append(recipe.label).Append("】");
                if(product!=null) text.Append(" / ").Append(runtime.World.Spec(product.cupSpecId).label);
                text.Append("：");
                if(recipe.steps!=null)
                {
                    for(int i=0;i<recipe.steps.Length;i++)
                    {
                        if(i>0) text.Append(" → ");
                        var step=recipe.steps[i];
                        text.Append(i+1).Append(".").Append(step.label);
                        if(!string.IsNullOrEmpty(step.operationId))
                        {
                            var op=runtime.World.Operation(step.operationId);
                            if(op!=null)
                            {
                                if(op.control==OperationControl.Clicks) text.Append("[点").Append(op.clicks).Append("次/").Append(op.clickInterval.ToString("0.0")).Append("s]");
                                else if(op.control==OperationControl.Qte) text.Append("[QTE]");
                                else text.Append("[").Append(op.seconds.ToString("0.#")).Append("s]");
                            }
                        }
                    }
                }
                text.AppendLine();
            }
            text.AppendLine();
            text.Append("规则：步骤、加工次数、等待时间和售价均来自 catalog.json；材料不足时按配表价格购买。流程全部完成后才能确认上餐。 ");
            recipeBody.text=text.ToString();
        }
        private void Refresh()
        {
            if(runtime==null || runtime.World==null)
            {
                for(int index=0;index<orderAvatars.Length;index++) SetOrderAvatar(index,null);
                return;
            }
            var state=runtime.World.State;
            if(summary!=null)
            {
                summary.text="第 "+state.day+" 天    王国币 "+state.money+"    声望 "+state.reputation+"\n"+(state.open ? "营业中 · 剩余 "+state.timeRemaining.ToString("0")+" 秒" : state.businessPhase==BusinessPhase.Night ? "已打烊 · 点击立牌开始新一天" : "准备营业 · 点击大厅立牌开店")+"    顾客 "+state.requests.Count+" 位";
                var beans=runtime.World.Find("s.beans");var milk=runtime.World.Find("s.milk");
                summary.text+="\n咖啡豆 "+(beans==null ? "—" : beans.stock.ToString())+"  ·  牛奶 "+(milk==null ? "—" : milk.stock.ToString());
            }
            if(dayLabel!=null) dayLabel.text="第 "+state.day+" 天";
            if(statusLabel!=null) statusLabel.text=state.open ? "营业中 · 剩余 "+state.timeRemaining.ToString("0")+" 秒 · 顾客 "+state.requests.Count+" 位" : state.businessPhase==BusinessPhase.Night ? "已打烊 · 点击立牌开始新一天" : "准备营业 · 点击大厅立牌开店";
            if(moneyLabel!=null) moneyLabel.text=state.money.ToString();
            if(reputationLabel!=null) reputationLabel.text=state.reputation.ToString();
            if(stockLabel!=null)
            {
                var beans=runtime.World.Find("s.beans");var milk=runtime.World.Find("s.milk");
                stockLabel.text="咖啡豆 "+(beans==null ? "—" : beans.stock.ToString())+"   牛奶 "+(milk==null ? "—" : milk.stock.ToString());
            }
            bool workstation=IsWorkstation;
            if(playerRoot!=null)
            {
                playerRoot.SetActive(!(workstation && miniGameGateway!=null && miniGameGateway.HasPresenter && miniGameGateway.BlocksInput));
                if(headerDock==null) headerDock=playerRoot.transform.Find("CafeLobbyHud/HeaderDock") as RectTransform;
                if(currencyDock==null) currencyDock=playerRoot.transform.Find("CafeLobbyHud/HeaderDock/Resources");
                if(floorSwitcher==null) floorSwitcher=playerRoot.transform.Find("CafeLobbyHud/FloorSwitcher");
                if(productionMenus==null) productionMenus=playerRoot.transform.Find("CafeLobbyHud/HeaderDock/SecondaryMenus");
                if(currencyDock!=null) currencyDock.gameObject.SetActive(!workstation);
                if(floorSwitcher!=null) floorSwitcher.gameObject.SetActive(!workstation);
                if(productionMenus!=null) productionMenus.gameObject.SetActive(workstation);
            }
            if(hint!=null) hint.text=runtime.Hint;
            var selected=runtime.World.Find(runtime.SelectedId);
            string text=selected==null ? "点击顾客查看需求；前往制作台调配饮品。" : "选中："+runtime.World.Spec(selected.specId).label+"";
            if(selected!=null && runtime.World.ProductionEnabled && runtime.World.IsContainer(selected))
                text+=" / "+runtime.World.ContainerInspection(selected);
            else if(selected!=null) foreach(var p in selected.contents) text+=" / "+runtime.World.Spec(p.specId).label+"×"+p.units;
            if(selection!=null) selection.text=text;
            if(areaName!=null) areaName.text=runtime.World.Area(state.currentArea).label;
            if(areaButtons!=null)
                for(int index=0;index<areaButtons.Length;index++)
                    if(areaButtons[index]!=null)
                    {
                        bool current=index==(int)state.currentArea;
                        var button=areaButtons[index];
                        button.interactable=runtime.CanEnterArea((AreaId)index) && !current;
                        if(index!=(int)AreaId.Counter && button.targetGraphic is Image image && floorCurrentSprite!=null && floorIdleSprite!=null)
                            image.sprite=current ? floorCurrentSprite : floorIdleSprite;
                    }
            int orderCount=Math.Max(orderButtons==null ? 0 : orderButtons.Length,orderLabels==null ? 0 : orderLabels.Length);
            int slots=Math.Max(1,orderCount);
            int pages=(state.requests.Count+slots-1)/slots;
            if(workstation)
            {
                int focused=0;
                for(int index=0;index<state.requests.Count;index++)
                    if(state.requests[index].id==state.focusedRequestId) { focused=index;break; }
                orderOffset=focused/slots*slots;
            }
            orderOffset=previousOrder==null && nextOrder==null ? 0 : Mathf.Clamp(orderOffset/slots,0,Math.Max(0,pages-1))*slots;
            if(previousOrder!=null) previousOrder.interactable=pages>1 && !runtime.InputBlocked;
            if(nextOrder!=null) nextOrder.interactable=pages>1 && !runtime.InputBlocked;
            if(orderPage!=null) orderPage.text=pages==0 ? "暂无订单" : (orderOffset/slots+1).ToString("00")+" / "+pages.ToString("00");
            if(requestIds.Length!=orderCount) Array.Resize(ref requestIds,orderCount);
            for(int index=0;index<orderCount;index++)
            {
                var button=orderButtons!=null && index<orderButtons.Length ? orderButtons[index] : null;
                var label=orderLabels!=null && index<orderLabels.Length ? orderLabels[index] : null;
                int requestIndex=orderOffset+index;
                if(requestIndex>=state.requests.Count)
                {
                    requestIds[index]=null;
                    SetOrderAvatar(index,null);
                    if(label!=null) label.text="等待顾客入座";
                    if(button!=null) { button.interactable=false; SetTicket(button,false); }
                    continue;
                }
                var request=state.requests[requestIndex]; requestIds[index]=request.id; var product=runtime.World.Product(request.productId);
                var customer=runtime.World.Find(request.customerId);string name="顾客";
                if(customer!=null) name=runtime.World.PersonName(customer);
                var character=customer!=null && customer.person!=null ? runtime.Character(customer.person.characterId) : null;
                SetOrderAvatar(index,character?.avatar);
                string status=request.id==CafeWorld.TutorialOrderId ? "教程订单 · 尚未营业" : name+"  ·  "+request.patience.ToString("0")+" 秒";
                string recipe=(request.id==state.focusedRequestId ? "● " : "")+status+"\n"+FreeCafeProductionText.Recipe(product.label)+"  /  "+product.price+" 王国币";
                if(label!=null) label.text=recipe;
                if(button!=null) { button.interactable=!runtime.InputBlocked; SetTicket(button,request.id==state.focusedRequestId); }
            }
            if(pauseButton!=null) pauseButton.interactable=string.IsNullOrEmpty(state.production.miniGameToken) && !(FreeCafeGMPanel.Instance!=null && FreeCafeGMPanel.Instance.IsVisible);
            if(mapButton!=null) mapButton.interactable=Flow != null && Flow.CanOpenMap;
            if(recipeButton!=null) recipeButton.interactable=!runtime.InputBlocked || IsModalOpen;

            if(recipePanel!=null && recipePanel.activeSelf) RefreshRecipeTips();
        }
        private void SetTicket(Button button,bool focused)
        {
            var image=button.targetGraphic as Image;
            var sprite=focused ? orderFocusSprite : orderIdleSprite;
            if(image!=null && sprite!=null && image.sprite!=sprite) image.sprite=sprite;
        }
    }
}

