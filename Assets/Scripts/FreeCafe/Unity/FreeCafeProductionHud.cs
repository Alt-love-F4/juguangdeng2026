using System.Text;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>沿用 CoffeeShop 的世界实体与现有 HUD Canvas，显示制作区的订单纸卡与出餐工具卡。</summary>
    public sealed class FreeCafeProductionHud : MonoBehaviour
    {
        public FreeCafeRuntime runtime;
        public bool IsModalOpen => bookPanel!=null && bookPanel.activeInHierarchy;
        public void CloseOverlays() { if(bookPanel!=null) bookPanel.SetActive(false); }
        private static readonly Color Ink=new Color(.25f,.15f,.095f);
        public GameObject productionRoot, legacyTop, legacyBottom, bookPanel, miniGamePanel;
        public TMP_Text timeCaption, volumeCaption;
        public TMP_Text orderTitle, customerName, orderDescription, cupStatus, instruction, result, machineStatus, bookTitle, bookBody;
        public Image customerAvatar, bookIcon;
        public Button previousOrder, nextOrder, smallCup, largeCup, deliver, discard, returnButton, bookButton, closeBook,
            previousRecipe, nextRecipe, miniGameButton, cancelMiniGame, startBusiness, pauseButton, saveButton, clearMachine;
        public Slider timeGauge, volumeGauge;
        public Image timeGreen, timeGold, volumeGreen, volumeGold;
        private int recipeIndex;
        private bool bound;
        private Coroutine completionFeedback;
        private void Start() { Bind(); Refresh(); }
        private void OnEnable() { Bind(); }
        private void Bind()
        {
            if(bound || runtime==null || productionRoot==null) return;
            bound=true; runtime.ViewChanged+=Refresh;
            previousOrder.onClick.AddListener(PreviousOrder); nextOrder.onClick.AddListener(NextOrder);
            smallCup.onClick.AddListener(SmallCup); largeCup.onClick.AddListener(LargeCup);
            deliver.onClick.AddListener(Deliver); discard.onClick.AddListener(Discard);
            returnButton.onClick.AddListener(runtime.ReturnFromProduction); bookButton.onClick.AddListener(OpenBook);
            closeBook.onClick.AddListener(CloseBook); previousRecipe.onClick.AddListener(PreviousRecipe); nextRecipe.onClick.AddListener(NextRecipe);
            miniGameButton.onClick.AddListener(OpenMiniGame); cancelMiniGame.onClick.AddListener(CancelMiniGame);
            pauseButton.onClick.AddListener(runtime.OpenPlayerMenu);
            clearMachine.onClick.AddListener(ClearMachine);
        }
        private void OnDisable()
        {
            if(completionFeedback!=null) { StopCoroutine(completionFeedback); completionFeedback=null; if(result!=null) result.color=Ink; }
            if(!bound) return; bound=false;
            if(runtime!=null) runtime.ViewChanged-=Refresh;
            previousOrder.onClick.RemoveListener(PreviousOrder); nextOrder.onClick.RemoveListener(NextOrder);
            smallCup.onClick.RemoveListener(SmallCup); largeCup.onClick.RemoveListener(LargeCup);
            deliver.onClick.RemoveListener(Deliver); discard.onClick.RemoveListener(Discard);
            if(runtime!=null) { returnButton.onClick.RemoveListener(runtime.ReturnFromProduction); pauseButton.onClick.RemoveListener(runtime.OpenPlayerMenu); }
            bookButton.onClick.RemoveListener(OpenBook); closeBook.onClick.RemoveListener(CloseBook);
            previousRecipe.onClick.RemoveListener(PreviousRecipe); nextRecipe.onClick.RemoveListener(NextRecipe);
            miniGameButton.onClick.RemoveListener(OpenMiniGame); cancelMiniGame.onClick.RemoveListener(CancelMiniGame); clearMachine.onClick.RemoveListener(ClearMachine);
        }
        private void Execute(ActionResult action) => runtime.ShowHint(action.message);
        private void PreviousOrder() { if(!runtime.InputBlocked) Execute(runtime.World.PageProductionOrder(-1)); }
        private void NextOrder() { if(!runtime.InputBlocked) Execute(runtime.World.PageProductionOrder(1)); }
        private void SmallCup() { if(!runtime.InputBlocked) Execute(runtime.World.SelectServingCup("s.cups.small")); }
        private void LargeCup() { if(!runtime.InputBlocked) Execute(runtime.World.SelectServingCup("s.cups.large")); }
        private void Deliver()
        {
            if(runtime.InputBlocked) return;
            int completed=runtime.World.State.todayServed;Execute(runtime.World.DeliverProductionCup());
            if(runtime.World.State.todayServed>completed)
            { if(completionFeedback!=null) StopCoroutine(completionFeedback);completionFeedback=StartCoroutine(CompletionFeedback()); }
        }
        private IEnumerator CompletionFeedback()
        {
            float elapsed=0;
            while(elapsed<.8f) { elapsed+=Time.unscaledDeltaTime;result.color=Color.Lerp(new Color(.52f,1,.55f),Ink,Mathf.Clamp01(elapsed/.8f));yield return null; }
            completionFeedback=null;
        }
        private void Discard() { if(!runtime.InputBlocked) Execute(runtime.World.DiscardProductionCup()); }
        private void ClearMachine() { if(!runtime.InputBlocked) Execute(runtime.World.EmptyProductionMachine(runtime.SelectedId)); }
        private void OpenMiniGame() { if(!runtime.InputBlocked) Execute(runtime.World.RequestProductionMiniGame()); }
        private void CancelMiniGame()
        {
            var session=runtime.World.State.production;
            Execute(runtime.World.ResolveProductionMiniGame(session.miniGameToken,false,0));
        }
        private void OpenBook() { if(runtime.InputBlocked) return; runtime.CancelDrag(); bookPanel.SetActive(true); RefreshBook(); }
        private void CloseBook() { bookPanel.SetActive(false); }
        private void PreviousRecipe() { recipeIndex--; RefreshBook(); }
        private void NextRecipe() { recipeIndex++; RefreshBook(); }
        private void RefreshBook()
        {
            var world=runtime.World; var learned=world.State.unlockedProducts;
            if(learned.Count==0) { bookTitle.text="尚未学会咖啡"; bookBody.text="经营并解锁后，这里会显示制作说明。"; return; }
            recipeIndex=(recipeIndex%learned.Count+learned.Count)%learned.Count;
            var product=world.Product(learned[recipeIndex]);
            bookTitle.text="咖啡谱  "+(recipeIndex+1)+" / "+learned.Count+"  ·  "+product.label;
            bookIcon.sprite=runtime.artLibrary.Resolve(world.Spec(product.cupSpecId).artId);
            var text=new StringBuilder(); text.AppendLine(product.description).AppendLine().AppendLine(product.tutorial).AppendLine();
            text.Append("杯型：").Append(world.Spec(product.cupSpecId).label).Append(" / 温度：").Append(product.requiredTemperature==LiquidTemperature.Boiling ? "沸腾" : product.requiredTemperature==LiquidTemperature.Warm ? "温热" : "常温").AppendLine();
            text.AppendLine("参考用量（自由操作，不锁步骤）：");
            foreach(var p in product.ingredients) text.Append("  ").Append(world.Spec(p.specId).label).Append("  ").Append(p.milliliters>0 ? p.milliliters.ToString("0")+" ml" : p.units+" 份").AppendLine();
            text.AppendLine().Append("先咖啡原液，再其他液体，最后小料；方糖可任意时候加入。\n空杯、半成品也能出品，但顾客会按实际质量评价。");
            bookBody.text=text.ToString();
        }
        private void Refresh()
        {
            if(runtime==null || runtime.World==null || productionRoot==null) return;
            var world=runtime.World; var state=world.State;
            bool visible=world.ProductionEnabled && state.currentArea==AreaId.Counter;
            productionRoot.SetActive(visible);
            // 玩家 HUD 已改为独立纸卡布局；旧黑边不再由制作 HUD 恢复。
            if(!visible) { bookPanel.SetActive(false); miniGamePanel.SetActive(false); return; }
            bool blocked=runtime.InputBlocked; var request=world.Request(state.focusedRequestId);
            var product=request==null ? null : world.Product(request.productId);
            orderTitle.text="交付此订单  ·  "+(request==null ? "暂无订单" : (state.requests.IndexOf(request)+1)+" / "+state.requests.Count);
            var customer=request==null ? null : world.Find(request.customerId);
            string name="等待顾客";
            if(customer!=null)
            {
                name=world.PersonName(customer);
            }
            customerName.text=name+(request==null ? "" : "  ·  耐心 "+request.patience.ToString("0")+"s");
            string customerArt=customer==null ? "customer" : world.Spec(customer.specId).artId;
            if(customer!=null && customer.person!=null && !string.IsNullOrEmpty(customer.person.visualId) && runtime.artLibrary.Find(customer.person.visualId)!=null)
                customerArt=customer.person.visualId;
            customerAvatar.sprite=runtime.artLibrary.Resolve(customerArt);
            orderDescription.text=product==null ? "尚无订单，可先准备原料。" : product.label+"  ·  "+product.description+"\n满意要求 ≥ "+product.minimumScore.ToString("0")+"分"+(request.remakes>0 ? "  ·  正在重做" : "");
            previousOrder.interactable=nextOrder.interactable=!blocked && state.requests.Count>1;
            var cup=world.ServingCup;
            cupStatus.text=cup==null ? "出餐托盘 · 未选杯" : world.Spec(cup.specId).label+"  "+world.LiquidVolume(cup).ToString("0")+" / "+world.LiquidCapacity(cup).ToString("0")+" ml";
            bool canChange=!blocked && (cup==null || cup.contents.Count==0);
            smallCup.interactable=largeCup.interactable=canChange;
            deliver.interactable=!blocked && cup!=null && request!=null; discard.interactable=!blocked && cup!=null;
            returnButton.interactable=bookButton.interactable=miniGameButton.interactable=!blocked;
            pauseButton.interactable=string.IsNullOrEmpty(state.production.miniGameToken) && !(FreeCafeGMPanel.Instance!=null && FreeCafeGMPanel.Instance.IsVisible);
            var selected=world.Find(runtime.SelectedId); clearMachine.interactable=!blocked && selected!=null && world.Spec(selected.specId).Has(Capability.Processor) && selected.contents.Count>0;
            instruction.text=runtime.Hint;
            result.text=string.IsNullOrEmpty(state.production.lastDelivery) ? "王国币 "+state.money+"  ·  声望 "+state.reputation+"  ·  可提前备料" : state.production.lastDelivery;
            var brewer=world.Find("m.brewer"); var heater=world.Find("m.heater"); var settings=world.Catalog.production;
            if(timeCaption!=null) timeCaption.text="萃取 "+brewer.production.brewElapsed.ToString("0.0")+" s  /  理想 "+settings.brewIdealSeconds.ToString("0.#")+" s";
            if(volumeCaption!=null) volumeCaption.text="注水 "+world.LiquidVolume(brewer).ToString("0")+" ml  /  理想 "+settings.brewIdealMl.ToString("0")+" ml";
            timeGauge.maxValue=settings.brewIdealSeconds+settings.brewGoldSeconds+settings.brewGreenSeconds+2;
            volumeGauge.maxValue=settings.brewIdealMl+settings.brewGoldMl+settings.brewGreenMl+50;
            timeGauge.value=brewer.production.brewElapsed; volumeGauge.value=world.LiquidVolume(brewer);
            SetBand(timeGreen,settings.brewIdealSeconds-settings.brewGoldSeconds-settings.brewGreenSeconds,settings.brewIdealSeconds+settings.brewGoldSeconds+settings.brewGreenSeconds,timeGauge.maxValue);
            SetBand(timeGold,settings.brewIdealSeconds-settings.brewGoldSeconds,settings.brewIdealSeconds+settings.brewGoldSeconds,timeGauge.maxValue);
            SetBand(volumeGreen,settings.brewIdealMl-settings.brewGoldMl-settings.brewGreenMl,settings.brewIdealMl+settings.brewGoldMl+settings.brewGreenMl,volumeGauge.maxValue);
            SetBand(volumeGold,settings.brewIdealMl-settings.brewGoldMl,settings.brewIdealMl+settings.brewGoldMl,volumeGauge.maxValue);
            machineStatus.text="萃取："+world.ProductionCaption(brewer)+"   |   加热壶："+world.ProductionCaption(heater);
            miniGamePanel.SetActive(!string.IsNullOrEmpty(state.production.miniGameToken) && (runtime.GetComponent<FreeCafeMiniGameGateway>()==null || !runtime.GetComponent<FreeCafeMiniGameGateway>().HasPresenter));
            if(bookPanel.activeSelf) RefreshBook();
        }
        private static void SetBand(Image image,float minimum,float maximum,float limit)
        {
            var rect=image.rectTransform; rect.anchorMin=new Vector2(Mathf.Clamp01(minimum/limit),0); rect.anchorMax=new Vector2(Mathf.Clamp01(maximum/limit),1); rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
    }
}
