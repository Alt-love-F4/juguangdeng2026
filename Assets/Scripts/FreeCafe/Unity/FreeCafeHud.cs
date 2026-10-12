using System;
using System.Text;
using TMPro;
using UnityEngine;
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
        public Button startButton,pauseButton,saveButton,recipeButton;
        public Button[] areaButtons,orderButtons;
        public TMP_Text[] orderLabels;
        public GameObject recipePanel;
        public TMP_Text recipeTitle,recipeBody;
        public Button recipeCloseButton;
        private readonly string[] requestIds=new string[3];
        private bool bound;
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
            if(bound || runtime==null || pauseButton==null || recipeButton==null || areaButtons==null || areaButtons.Length<4 || orderButtons==null || orderButtons.Length<3)
                return;
            bound=true;
            runtime.ViewChanged+=Refresh;
            pauseButton.onClick.AddListener(OpenPlayerMenu);
            recipeButton.onClick.AddListener(ToggleRecipeTips);
            if(recipeCloseButton!=null) recipeCloseButton.onClick.AddListener(CloseRecipeTips);
            if(areaButtons[0]!=null) areaButtons[0].onClick.AddListener(Cafe); if(areaButtons[1]!=null) areaButtons[1].onClick.AddListener(Counter);
            orderButtons[0].onClick.AddListener(Order0); orderButtons[1].onClick.AddListener(Order1); orderButtons[2].onClick.AddListener(Order2);
        }
        private void OnDisable()
        {
            if(!bound) return;
            bound=false;
            if(runtime==null) return;
            runtime.ViewChanged-=Refresh;
            pauseButton.onClick.RemoveListener(OpenPlayerMenu);
            recipeButton.onClick.RemoveListener(ToggleRecipeTips);
            if(recipeCloseButton!=null) recipeCloseButton.onClick.RemoveListener(CloseRecipeTips);
            if(areaButtons[0]!=null) areaButtons[0].onClick.RemoveListener(Cafe); if(areaButtons[1]!=null) areaButtons[1].onClick.RemoveListener(Counter);
            orderButtons[0].onClick.RemoveListener(Order0); orderButtons[1].onClick.RemoveListener(Order1); orderButtons[2].onClick.RemoveListener(Order2);
        }
        private void Cafe()=>runtime.Enter(AreaId.Cafe); private void Counter()=>runtime.Enter(AreaId.Counter);

        private void Order0()=>Focus(0); private void Order1()=>Focus(1); private void Order2()=>Focus(2);
        private void Focus(int i) { if(!runtime.InputBlocked) runtime.World.Focus(requestIds[i]); }
        public void OpenFromFlow(GameFlowBootstrap flow)
        {
            if (flow != null) SetFlow(flow);
            TryBind();
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
        private void RefreshRecipeTips()
        {
            if(recipeTitle==null || recipeBody==null || runtime==null || runtime.World==null) return;
            if(runtime.World.ProductionEnabled)
            {
                recipeTitle.text="咖啡谱 · 已学会的餐品";
                var productionText=new StringBuilder();
                foreach(var id in runtime.World.State.unlockedProducts)
                {
                    var product=runtime.World.Product(id);
                    productionText.Append("【").Append(product.label).Append("】").AppendLine(product.description).AppendLine(product.tutorial).AppendLine();
                }
                recipeBody.text=productionText.ToString(); return;
            }
            recipeTitle.text="餐品制作提示（配表）";
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
            summary.text="第 "+state.day+" 天    王国币 "+state.money+"    声望 "+state.reputation+"\n"+(state.open ? "营业中 · 剩余 "+state.timeRemaining.ToString("0")+" 秒" : state.businessPhase==BusinessPhase.Night ? "已打烊 · 点击立牌开始新一天" : "准备营业 · 点击大厅立牌开店")+"    顾客 "+state.requests.Count+" 位";
            var beans=runtime.World.Find("s.beans");var milk=runtime.World.Find("s.milk");
            summary.text+="\n咖啡豆 "+(beans==null ? "—" : beans.stock.ToString())+"  ·  牛奶 "+(milk==null ? "—" : milk.stock.ToString());
            if(playerRoot!=null) playerRoot.SetActive(!runtime.World.ProductionEnabled || state.currentArea!=AreaId.Counter);
            hint.text=runtime.Hint;
            var selected=runtime.World.Find(runtime.SelectedId);
            string text=selected==null ? "点击顾客查看需求；前往制作台调配饮品。" : "选中："+runtime.World.Spec(selected.specId).label+"";
            if(selected!=null && runtime.World.ProductionEnabled && runtime.World.IsContainer(selected))
                text+=" / "+runtime.World.ContainerInspection(selected);
            else if(selected!=null) foreach(var p in selected.contents) text+=" / "+runtime.World.Spec(p.specId).label+"×"+p.units;
            selection.text=text;
            areaName.text=runtime.World.Area(state.currentArea).label;
            for(int i=0;i<areaButtons.Length;i++) if(areaButtons[i]!=null) areaButtons[i].interactable=!runtime.InputBlocked && i!=(int)state.currentArea;
            for(int i=0;i<orderButtons.Length;i++)
            {
                if(i>=state.requests.Count) { requestIds[i]=null;orderLabels[i].text="座位 "+(i+1)+"\n等待顾客入座";orderButtons[i].interactable=false;continue; }
                var request=state.requests[i]; requestIds[i]=request.id; var product=runtime.World.Product(request.productId);
                var customer=runtime.World.Find(request.customerId);string name="顾客";
                if(customer!=null && runtime.World.Catalog.npcs!=null)
                    foreach(var npc in runtime.World.Catalog.npcs) if(npc.id==customer.npcId) name=npc.name;
                string recipe=(request.id==state.focusedRequestId ? "● " : "")+name+"  ·  "+request.patience.ToString("0")+" 秒\n"+product.label+"  /  "+product.price+" 王国币";
                orderLabels[i].text=recipe; orderButtons[i].interactable=!runtime.InputBlocked;
            }
            pauseButton.interactable=string.IsNullOrEmpty(state.production.miniGameToken) && !(FreeCafeGMPanel.Instance!=null && FreeCafeGMPanel.Instance.IsVisible);
            recipeButton.interactable=!runtime.InputBlocked || IsModalOpen;

            if(recipePanel!=null && recipePanel.activeSelf) RefreshRecipeTips();
        }
    }
}

