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
            if(bound || runtime==null || startButton==null || pauseButton==null || saveButton==null || recipeButton==null || areaButtons==null || areaButtons.Length<4 || orderButtons==null || orderButtons.Length<3)
                return;
            bound=true;
            runtime.ViewChanged+=Refresh;
            startButton.onClick.AddListener(runtime.StartBusiness); pauseButton.onClick.AddListener(Pause);
            saveButton.onClick.AddListener(runtime.Save); recipeButton.onClick.AddListener(ToggleRecipeTips);
            if(recipeCloseButton!=null) recipeCloseButton.onClick.AddListener(CloseRecipeTips);
            areaButtons[0].onClick.AddListener(Cafe); areaButtons[1].onClick.AddListener(Counter); areaButtons[2].onClick.AddListener(Top); areaButtons[3].onClick.AddListener(Basement);
            orderButtons[0].onClick.AddListener(Order0); orderButtons[1].onClick.AddListener(Order1); orderButtons[2].onClick.AddListener(Order2);
        }
        private void OnDisable()
        {
            if(!bound) return;
            bound=false;
            if(runtime==null) return;
            runtime.ViewChanged-=Refresh;
            startButton.onClick.RemoveListener(runtime.StartBusiness); pauseButton.onClick.RemoveListener(Pause);
            saveButton.onClick.RemoveListener(runtime.Save); recipeButton.onClick.RemoveListener(ToggleRecipeTips);
            if(recipeCloseButton!=null) recipeCloseButton.onClick.RemoveListener(CloseRecipeTips);
            areaButtons[0].onClick.RemoveListener(Cafe); areaButtons[1].onClick.RemoveListener(Counter); areaButtons[2].onClick.RemoveListener(Top); areaButtons[3].onClick.RemoveListener(Basement);
            orderButtons[0].onClick.RemoveListener(Order0); orderButtons[1].onClick.RemoveListener(Order1); orderButtons[2].onClick.RemoveListener(Order2);
        }
        private void Cafe()=>runtime.Enter(AreaId.Cafe); private void Counter()=>runtime.Enter(AreaId.Counter);
        private void Top()=>runtime.Enter(AreaId.TopFloor); private void Basement()=>runtime.Enter(AreaId.Basement);
        private void Order0()=>Focus(0); private void Order1()=>Focus(1); private void Order2()=>Focus(2);
        private void Focus(int i) { if(!runtime.InputBlocked) runtime.World.Focus(requestIds[i]); }
        public void OpenFromFlow(GameFlowBootstrap flow)
        {
            if (flow != null) SetFlow(flow);
            TryBind();
        }
        private void Pause()
        {
            if (runtime != null) runtime.CancelDrag();
            if (Flow != null)
            {
                Flow.OpenPause();
                return;
            }
            runtime?.TogglePause();
        }
        private void ToggleRecipeTips()
        {
            if(recipePanel==null) return;
            recipePanel.SetActive(!recipePanel.activeSelf);
            if(recipePanel.activeSelf) RefreshRecipeTips();
        }
        private void CloseRecipeTips() { if(recipePanel!=null) recipePanel.SetActive(false); }
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
            if(runtime.World==null) return;
            var state=runtime.World.State;
            summary.text="FREE CAFE / 配表驱动原型    金币 "+state.money+"    声望 "+state.reputation+"    第 "+state.day+" 天    "+(state.open ? "营业 "+state.timeRemaining.ToString("0")+"s" : "可自由备料 · 未营业");
            hint.text=runtime.Hint;
            var selected=runtime.World.Find(runtime.SelectedId);
            string text=selected==null ? "拖出杯子 / 材料，在当前画面任意位置摆放；点击「制作提示」查看全部配方。" : "选中："+runtime.World.Spec(selected.specId).label+" · "+selected.id;
            if(selected!=null) foreach(var p in selected.contents) text+=" / "+runtime.World.Spec(p.specId).label+"×"+p.units;
            selection.text=text;
            areaName.text=runtime.World.Area(state.currentArea).label;
            for(int i=0;i<areaButtons.Length;i++) areaButtons[i].interactable=!runtime.InputBlocked && i!=(int)state.currentArea;
            for(int i=0;i<orderButtons.Length;i++)
            {
                if(i>=state.requests.Count) { requestIds[i]=null;orderLabels[i].text="空位 · 等待顾客";orderButtons[i].interactable=false;continue; }
                var request=state.requests[i]; requestIds[i]=request.id; var product=runtime.World.Product(request.productId);
                string recipe=(request.id==state.focusedRequestId ? "● " : "")+product.label+" / "+request.patience.ToString("0")+"s / "+product.price+"金\n"+runtime.World.Spec(product.cupSpecId).label+"：";
                foreach(var ing in product.ingredients) recipe+=runtime.World.Spec(ing.specId).label+"×"+ing.units+" ";
                orderLabels[i].text=recipe; orderButtons[i].interactable=!runtime.InputBlocked;
            }
            pauseButton.GetComponentInChildren<TMP_Text>().text=runtime.InputBlocked ? "继续 / Esc" : "暂停 / Esc";
            recipeButton.GetComponentInChildren<TMP_Text>().text=recipePanel!=null && recipePanel.activeSelf ? "关闭提示" : "制作提示";
            startButton.interactable=!runtime.InputBlocked && !state.open;
            if(recipePanel!=null && recipePanel.activeSelf) RefreshRecipeTips();
        }
    }
}

