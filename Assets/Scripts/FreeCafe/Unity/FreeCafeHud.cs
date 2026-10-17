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
        private bool bound;
        private RectTransform sharedHudContent;
        private Vector2 authoredContentTop;

        private void LateUpdate()
        {
            if (playerRoot == null) return;
            if (sharedHudContent == null)
            {
                sharedHudContent = playerRoot.transform as RectTransform;
                if (sharedHudContent == null) return;
                authoredContentTop = sharedHudContent.offsetMax;
            }
            var hud = GameplayHudPanel.Instance;
            float inset = hud != null && hud.IsVisible ? GameplayHudPanel.HeaderHeight : 0f;
            sharedHudContent.offsetMax = authoredContentTop - new Vector2(0f, inset);
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
        private void OnEnable() => TryBind();
        private void Start() { TryBind(); Refresh(); }
        private void TryBind()
        {
            if(bound || runtime==null)
                return;
            bound=true;
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
            boundOrderButtons=orderButtons==null ? Array.Empty<Button>() : (Button[])orderButtons.Clone();
            orderActions=new UnityAction[boundOrderButtons.Length];
            for(int index=0;index<boundOrderButtons.Length;index++)
            {
                int orderIndex=index;
                orderActions[index]=()=>Focus(orderIndex);
                if(boundOrderButtons[index]!=null) boundOrderButtons[index].onClick.AddListener(orderActions[index]);
            }
        }
        private void OnDisable()
        {
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
            for(int index=0;index<boundOrderButtons.Length;index++)
                if(boundOrderButtons[index]!=null) boundOrderButtons[index].onClick.RemoveListener(orderActions[index]);
            boundOrderButtons=Array.Empty<Button>();
            orderActions=Array.Empty<UnityAction>();
        }
        private void Cafe()=>runtime.Enter(AreaId.Cafe); private void Counter()=>runtime.Enter(AreaId.Counter);

        private void PreviousOrder() => ChangeOrderPage(-1);
        private void NextOrder() => ChangeOrderPage(1);
        private void ChangeOrderPage(int direction)
        {
            if(runtime==null || runtime.World==null || runtime.InputBlocked) return;
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
            if(runtime==null || runtime.World==null) return;
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
            if(playerRoot!=null) playerRoot.SetActive(!runtime.World.ProductionEnabled || state.currentArea!=AreaId.Counter);
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
                    if(areaButtons[index]!=null) areaButtons[index].interactable=runtime.CanEnterArea((AreaId)index) && index!=(int)state.currentArea;
            int orderCount=Math.Max(orderButtons==null ? 0 : orderButtons.Length,orderLabels==null ? 0 : orderLabels.Length);
            int slots=Math.Max(1,orderCount);
            int pages=(state.requests.Count+slots-1)/slots;
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
                    if(label!=null) label.text="等待顾客入座";
                    if(button!=null) { button.interactable=false; SetTicket(button,false); }
                    continue;
                }
                var request=state.requests[requestIndex]; requestIds[index]=request.id; var product=runtime.World.Product(request.productId);
                var customer=runtime.World.Find(request.customerId);string name="顾客";
                if(customer!=null && runtime.World.Catalog.npcs!=null)
                    foreach(var npc in runtime.World.Catalog.npcs) if(npc.id==customer.npcId) name=npc.name;
                string recipe=(request.id==state.focusedRequestId ? "● " : "")+name+"  ·  "+request.patience.ToString("0")+" 秒\n"+FreeCafeProductionText.Recipe(product.label)+"  /  "+product.price+" 王国币";
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

