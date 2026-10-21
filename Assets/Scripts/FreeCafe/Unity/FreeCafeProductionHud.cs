using System.Text;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>沿用 CoffeeShop 的世界实体与现有 HUD Canvas，显示制作区的订单纸卡与出餐工具卡。</summary>
    public sealed class FreeCafeProductionHud : MonoBehaviour
    {
        public FreeCafeRuntime runtime;
        public bool IsModalOpen => bookPanel!=null && bookPanel.activeInHierarchy || receiptPanel!=null && receiptPanel.activeInHierarchy;
        public void CloseOverlays() { CloseBook(); if(receiptPanel!=null) receiptPanel.SetActive(false); }
        private static readonly Color Ink=new Color(.25f,.15f,.095f);
        public GameObject productionRoot, legacyTop, legacyBottom, bookPanel, miniGamePanel;
        public TMP_Text timeCaption, volumeCaption;
        public TMP_Text orderTitle, customerName, orderDescription, cupStatus, instruction, result, machineStatus, bookTitle, bookBody, bookNote;
        public Image customerAvatar, bookIcon;
        public Sprite customerAvatarFallback;
        public Button previousOrder, nextOrder, smallCup, largeCup, deliver, discard, returnButton, bookButton, closeBook,
            previousRecipe, nextRecipe, miniGameButton, cancelMiniGame, startBusiness, pauseButton, saveButton, clearMachine;
        public Slider timeGauge, volumeGauge;
        public Image timeGreen, timeGold, volumeGreen, volumeGold;
        [Header("Workstation HUD")]
        public Button orderRecipe, confirmReceipt;
        public Button resetWorkstation;
        public GameObject[] stackedOrderSheets;
        public GameObject receiptPanel;
        public TMP_Text receiptGrade, receiptProduct, receiptDetails, receiptRewards;
        public CafeReceiptFace receiptFace;
        [Header("Order ticket and status (optional)")]
        public TMP_Text orderProduct, orderIndex, patienceText, moneyText, reputationText;
        public Image patienceFill;
        public GameObject deliveryPanel;
        public Color patienceCalm=new Color(.42f,.55f,.30f), patienceWarn=new Color(.86f,.58f,.18f), patienceLow=new Color(.70f,.20f,.12f);
        private readonly System.Collections.Generic.Dictionary<string,float> patienceMax=new System.Collections.Generic.Dictionary<string,float>();
        private int recipeIndex;
        private bool bound;
        private CoffeeBookPanelView bookView;
        private Coroutine completionFeedback;
        private void Start() { Bind(); Refresh(); }
        private void OnEnable() { Bind(); }
        private void Bind()
        {
            HideSceneReplacedControls();
            if(bound || runtime==null) return;
            bound=true; runtime.ViewChanged+=Refresh; runtime.CupDelivered+=ShowReceipt;
            bookView = bookPanel==null ? null : bookPanel.GetComponent<CoffeeBookPanelView>();
            if(bookView != null) bookView.RecipeSelected += SelectRecipe;
            BindControls(true);
            if (miniGameButton != null) miniGameButton.gameObject.SetActive(false);
            foreach (Button button in new[] { previousOrder, nextOrder, previousRecipe, nextRecipe, smallCup, largeCup, deliver, discard, clearMachine })
                Spotlight.Audio.UIButtonAudio.Configure(button, string.Empty);
            foreach (Button button in new[] { returnButton, closeBook, cancelMiniGame })
                Spotlight.Audio.UIButtonAudio.Configure(button, Spotlight.Audio.AudioKeys.Cancel);
        }
        /// <summary>杯堆、出品盘、按铃和废弃桶已接管这些操作，隐藏旧按钮及其独立纸卡。</summary>
        public void HideSceneReplacedControls()
        {
            HideSceneReplacedControl(smallCup);
            HideSceneReplacedControl(largeCup);
            HideSceneReplacedControl(deliver);
            HideSceneReplacedControl(discard);
            if(deliveryPanel!=null) deliveryPanel.SetActive(false);
        }
        private static void HideSceneReplacedControl(Button button)
        {
            if(button==null) return;
            button.gameObject.SetActive(false);
            var card=button.transform.parent;
            if(card!=null && (card.name=="CupTools" || card.name=="DeliveryActions"))
                card.gameObject.SetActive(false);
        }
        private void OnDisable()
        {
            if(Application.isPlaying) CloseOverlays();
            if(completionFeedback!=null) { StopCoroutine(completionFeedback); completionFeedback=null; if(result!=null) result.color=Ink; }
            if(!bound) return; bound=false;
            if(bookView != null) bookView.RecipeSelected -= SelectRecipe;
            if(runtime!=null) { runtime.ViewChanged-=Refresh; runtime.CupDelivered-=ShowReceipt; }
            BindControls(false);
        }
        private void BindControls(bool listening)
        {
            SetListener(previousOrder,PreviousOrder,listening); SetListener(nextOrder,NextOrder,listening);
            SetListener(smallCup,SmallCup,listening); SetListener(largeCup,LargeCup,listening);
            SetListener(deliver,Deliver,listening); SetListener(discard,Discard,listening);
            SetListener(returnButton,ReturnFromProduction,listening); SetListener(bookButton,OpenBook,listening);
            SetListener(closeBook,CloseBook,listening); SetListener(previousRecipe,PreviousRecipe,listening);
            SetListener(nextRecipe,NextRecipe,listening); SetListener(cancelMiniGame,CancelMiniGame,listening);
            SetListener(orderRecipe,OpenOrderRecipe,listening); SetListener(confirmReceipt,CloseReceipt,listening);
            SetListener(pauseButton,OpenPlayerMenu,listening); SetListener(clearMachine,ClearMachine,listening);
            SetListener(resetWorkstation,ResetWorkstation,listening);
        }
        private static void SetListener(Button button,UnityAction action,bool listening)
        {
            if(button==null) return;
            if(listening) button.onClick.AddListener(action);
            else button.onClick.RemoveListener(action);
        }
        private void ReturnFromProduction() { if(runtime!=null) runtime.ReturnFromProduction(); }
        private void OpenPlayerMenu() { if(runtime!=null) runtime.OpenPlayerMenu(); }
        private void Execute(ActionResult action) => runtime.Report(action);
        private void PreviousOrder() => ChangeOrder(-1);
        private void NextOrder() => ChangeOrder(1);
        private void ChangeOrder(int direction)
        {
            if (runtime.InputBlocked) return;
            string previous = runtime.World.State.focusedRequestId;
            ActionResult changed = runtime.World.PageProductionOrder(direction);
            if (changed.success && previous != runtime.World.State.focusedRequestId)
                Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Page);
            Execute(changed);
        }
        private void SmallCup() { if(!runtime.InputBlocked) Execute(runtime.World.SelectServingCup("s.cups.small")); }
        private void LargeCup() { if(!runtime.InputBlocked) Execute(runtime.World.SelectServingCup("s.cups.large")); }
        private void Deliver()
        {
            if(runtime.InputBlocked) return;
            int completed=runtime.World.State.todayServed;runtime.DeliverCup();
            if(runtime.World.State.todayServed>completed && result!=null)
            { if(completionFeedback!=null) StopCoroutine(completionFeedback);completionFeedback=StartCoroutine(CompletionFeedback()); }
        }
        private IEnumerator CompletionFeedback()
        {
            float elapsed=0;
            while(elapsed<.8f && result!=null) { elapsed+=Time.unscaledDeltaTime;result.color=Color.Lerp(new Color(.52f,1,.55f),Ink,Mathf.Clamp01(elapsed/.8f));yield return null; }
            completionFeedback=null;
        }
        private void Discard() { if(!runtime.InputBlocked) Execute(runtime.World.DiscardProductionCup()); }
        private void ClearMachine() { if(!runtime.InputBlocked) Execute(runtime.World.EmptyProductionMachine(runtime.SelectedId)); }
        private void ResetWorkstation() { if(runtime!=null) runtime.ResetWorkstation(); }
        private void CancelMiniGame()
        {
            var session=runtime.World.State.production;
            Execute(runtime.World.ResolveProductionMiniGame(session.miniGameToken,false,0));
        }
        private void CloseReceipt() { if(receiptPanel!=null) receiptPanel.SetActive(false); Refresh(); }
        private void ShowReceipt(CafeDeliveryReceipt receipt)
        {
            if(receiptPanel==null) return;
            runtime.CancelDrag();
            if(receiptGrade!=null) receiptGrade.text=receipt.GradeLabel+"  ·  "+receipt.quality.total.ToString("0")+" 分";
            if(receiptProduct!=null) receiptProduct.text=receipt.customer+" 的 "+receipt.product;
            if(receiptDetails!=null) receiptDetails.text=receipt.message;
            if(receiptRewards!=null) receiptRewards.text="王国币 "+Signed(receipt.money)+"     声望 "+Signed(receipt.reputation);
            if(receiptFace!=null) { receiptFace.grade=receipt.Grade; receiptFace.SetVerticesDirty(); }
            receiptPanel.SetActive(true);
            receiptPanel.transform.SetAsLastSibling();
            Refresh();
        }
        private static string Signed(int value) => value>=0 ? "+"+value : value.ToString();
        public void OpenOrderRecipe()
        {
            if(runtime==null || runtime.InputBlocked) return;
            var request=runtime.World.Request(runtime.World.State.focusedRequestId);
            var compendium=bookPanel==null ? null : bookPanel.GetComponent<CoffeeCompendiumPanel>();
            if(compendium!=null) { compendium.Open(runtime,request?.productId); return; }
            if(request!=null) recipeIndex=Mathf.Max(0,runtime.World.State.unlockedProducts.IndexOf(request.productId));
            OpenBook();
        }
        public bool HasDeliveryCup()
        {
            if(runtime==null || runtime.World==null) return false;
            var world=runtime.World; var cup=world.ServingCup;
            if(cup==null || world.CupAbove(cup)!=null || !string.IsNullOrEmpty(cup.cupStackBelowId)) return false;
            foreach(var entity in world.State.entities)
                if(world.Spec(entity.specId).Has(Capability.Delivery) && entity.area==cup.area
                    && Mathf.Abs(entity.x-cup.x)<=.1f && Mathf.Abs(entity.y-cup.y)<=.1f) return true;
            return false;
        }
        private void OpenBook()
        {
            if(bookPanel==null || runtime.InputBlocked) return;
            var compendium=bookPanel.GetComponent<CoffeeCompendiumPanel>();
            if(compendium!=null) { compendium.Open(runtime); return; }
            runtime.CancelDrag(); bookPanel.SetActive(true); RefreshBook();
        }
        private void CloseBook()
        {
            if(bookPanel==null) return;
            var compendium=bookPanel.GetComponent<CoffeeCompendiumPanel>();
            if(compendium!=null) compendium.CloseImmediate(); else bookPanel.SetActive(false);
        }
        private void PreviousRecipe() => ChangeRecipe(-1);
        private void NextRecipe() => ChangeRecipe(1);
        private void SelectRecipe(int index) => ChangeRecipe(index - recipeIndex);
        private void ChangeRecipe(int direction)
        {
            int previous = recipeIndex;
            recipeIndex += direction;
            RefreshBook();
            if (previous != recipeIndex && runtime.World.State.unlockedProducts.Count > 1)
                Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Page);
        }
        private void RefreshBook()
        {
            if(bookPanel!=null && bookPanel.GetComponent<CoffeeCompendiumPanel>()!=null) return; // 咖啡宝典自行刷新
            if(bookView != null)
            {
                bookView.Show(runtime, recipeIndex);
                return;
            }
            var world=runtime.World; var learned=world.State.unlockedProducts;
            if(learned.Count==0)
            {
                if(bookTitle!=null) bookTitle.text="咖啡谱";
                if(bookBody!=null) bookBody.text="尚未学会咖啡\n\n经营并解锁后，这里会显示制作说明。";
                if(bookNote!=null) bookNote.text="注意\n\n完成营业并解锁餐品后，咖啡谱会记录对应的配方与制作提示。";
                return;
            }
            recipeIndex=(recipeIndex%learned.Count+learned.Count)%learned.Count;
            var product=world.Product(learned[recipeIndex]);
            if(bookTitle!=null) bookTitle.text=FreeCafeProductionText.Recipe(product.label)+"  ·  "+(recipeIndex+1)+" / "+learned.Count;
            if(bookIcon!=null) bookIcon.sprite=runtime.artLibrary.Resolve(world.Spec(product.cupSpecId).artId);
            var text=new StringBuilder(); text.AppendLine(FreeCafeProductionText.Recipe(product.description)).AppendLine().AppendLine(FreeCafeProductionText.Recipe(product.tutorial)).AppendLine();
            var focused=world.Request(world.State.focusedRequestId);
            bool forOrder=focused!=null && focused.productId==product.id;
            float portionScale=forOrder ? world.OrderPortionScale(focused) : 1f;
            text.Append("杯型：").Append(world.Spec(forOrder ? world.RequiredCupSpecId(focused) : product.cupSpecId).label).Append(" / 温度：").Append(product.requiredTemperature==LiquidTemperature.Boiling ? "沸腾" : product.requiredTemperature==LiquidTemperature.Warm ? "温热" : "常温").AppendLine();
            text.AppendLine(forOrder ? "当前订单用量（按杯型换算）：" : "参考用量（自由操作，不锁步骤）：");
            foreach(var p in product.ingredients) text.Append("  ").Append(FreeCafeProductionText.RecipeIngredient(p, world.Spec(p.specId).label)).Append("  ").Append(p.milliliters>0 ? (p.milliliters*portionScale).ToString("0")+" ml" : p.grams>0 ? (p.grams*portionScale).ToString("0.#")+" g" : (p.units*portionScale).ToString("0.#")+" 份").AppendLine();
            text.AppendLine().Append("任何容器都能自由加料；咖啡粉与水混合是冷萃，放到炉台并点击炉台加热完成萃取。\n空杯、半成品也能出品，但顾客会按实际质量评价。");
            if(bookBody!=null) bookBody.text=text.ToString();
            if(bookNote!=null) bookNote.text="注意\n\n参考用量不锁定操作步骤，最终按实际质量评价。";
        }
        private void Refresh()
        {
            if(runtime==null || runtime.World==null) return;
            var world=runtime.World; var state=world.State;
            var gateway=runtime.GetComponent<FreeCafeMiniGameGateway>();
            bool latteOpen=gateway!=null && gateway.HasPresenter && gateway.BlocksInput;
            bool visible=world.ProductionEnabled && state.currentArea==AreaId.Counter && !latteOpen;
            if(productionRoot!=null) productionRoot.SetActive(visible);
            // 玩家 HUD 已改为独立纸卡布局；旧黑边不再由制作 HUD 恢复。
            if(!visible)
            {
                // A shared compendium also belongs to the lobby; leaving the counter must not close it there.
                var playerHud = bookPanel == null ? null : bookPanel.GetComponentInParent<FreeCafeHud>(true);
                bool sharedBook = playerHud != null && playerHud.recipePanel == bookPanel;
                if (!sharedBook || latteOpen) CloseBook();
                if(receiptPanel!=null) receiptPanel.SetActive(false);
                if(miniGamePanel!=null) miniGamePanel.SetActive(false);
                return;
            }
            if(stackedOrderSheets!=null)
                for(int i=0;i<stackedOrderSheets.Length;i++)
                    if(stackedOrderSheets[i]!=null) stackedOrderSheets[i].SetActive(state.requests.Count>i+1);
            bool blocked=runtime.InputBlocked;
            bool guided=runtime.WorkstationTutorial!=null && runtime.WorkstationTutorial.IsFirstCafeTutorial;
            var request=world.Request(state.focusedRequestId);
            bool storyOrder=world.IsNarrativeServiceOrder(request);
            bool tastingOrder=world.IsNarrativeTastingOrder(request);
            bool tutorialOrder=request!=null && request.id==CafeWorld.TutorialOrderId;
            var product=request==null ? null : world.Product(request.productId);
            int requestNumber=request==null ? 0 : state.requests.IndexOf(request)+1;
            bool splitTicket=orderProduct!=null;
            if(orderTitle!=null) orderTitle.text=splitTicket ? (tastingOrder ? "试喝订单" : storyOrder ? "剧情订单" : tutorialOrder ? "教程订单" : "当前订单") : "交付此订单  ·  "+(request==null ? "暂无订单" : requestNumber+" / "+state.requests.Count);
            if(orderIndex!=null) orderIndex.text=request==null ? "0 / 0" : requestNumber+" / "+state.requests.Count;
            var customer=request==null ? null : world.Find(request.customerId);
            string name=request==null ? "暂无客人" : "等待顾客";
            if(customer!=null) name=world.PersonName(customer);
            if(tastingOrder) name="试喝饮品";
            if(customerName!=null) customerName.text=splitTicket ? name : name+(storyOrder ? "  ·  剧情饮品 · 不限时" : tutorialOrder ? "  ·  教程客人 · 尚未营业" : request==null ? "" : "  ·  耐心 "+request.patience.ToString("0")+"s");
            string customerArt=customer==null ? "customer" : world.Spec(customer.specId).artId;
            if(customer!=null && customer.person!=null && !string.IsNullOrEmpty(customer.person.visualId) && runtime.artLibrary.Find(customer.person.visualId)!=null)
                customerArt=customer.person.visualId;
            var character=customer!=null && customer.person!=null ? runtime.Character(customer.person.characterId) : null;
            if(customerAvatar!=null)
            {
                var identity=customer!=null ? customer.person : null;
                customerAvatar.sprite=CafeCustomerVisualVariants.ResolveAvatar(identity,character);
                if(customerAvatar.sprite==null) customerAvatar.sprite=runtime.artLibrary.Resolve(customerArt);
                if(customerAvatar.sprite==null) customerAvatar.sprite=customerAvatarFallback;
                if(tastingOrder) customerAvatar.sprite=null;
                customerAvatar.gameObject.SetActive(request!=null && customerAvatar.sprite!=null);
                FrameAvatar(customerAvatar);
            }
            string cupRequirement = request == null ? "" : world.Spec(world.RequiredCupSpecId(request)).label;
            string orderLabel = product == null ? "尚无订单" : FreeCafeProductionText.Recipe(product.label) + " · " + cupRequirement;
            if(splitTicket)
            {
                orderProduct.text=orderLabel;
                if(orderDescription!=null) orderDescription.text=product==null ? "可以先准备原料" : storyOrder ? "剧情饮品 · 放上交付盘并按铃"+(request.remakes>0 ? " · 重做中" : "") : tutorialOrder ? "教程首杯 · 完成后放入出餐口" : "满意 ≥ "+product.minimumScore.ToString("0")+" 分"+(request.remakes>0 ? " · 重做中" : "");
            }
            else if(orderDescription!=null) orderDescription.text=product==null ? "尚无订单，可先准备原料。" : orderLabel+"\n"+(storyOrder ? "剧情饮品 · 放上交付盘并按铃"+(request.remakes>0 ? " · 重做中" : "") : tutorialOrder ? "教程首杯 · 完成后放入出餐口" : "满意要求 ≥ "+product.minimumScore.ToString("0")+" 分"+(request.remakes>0 ? " · 正在重做" : ""));
            RefreshPatience(request,tutorialOrder);
            if(moneyText!=null) moneyText.text=state.money.ToString();
            if(reputationText!=null) reputationText.text=state.reputation.ToString();
            if(previousOrder!=null) previousOrder.interactable=!blocked && state.requests.Count>1;
            if(nextOrder!=null) nextOrder.interactable=!blocked && state.requests.Count>1;
            var cup=world.ServingCup;
            if(cupStatus!=null) cupStatus.text=cup==null ? "出餐托盘 · 未选杯" : world.Spec(cup.specId).label+" · "+FreeCafeProductionText.Recipe(world.ClassifyCoffee(cup).label)+"  "+world.LiquidVolume(cup).ToString("0")+" / "+world.LiquidCapacity(cup).ToString("0")+" ml"+(cup.latteArt ? "  · "+FreeCafeProductionText.LatteArtContents(world.LatteArtFoamVolume(cup),cup.latteArtPattern)+" "+cup.latteArtScore.ToString("0")+"分" : "");
            bool canChange=!blocked && !guided && (cup==null || cup.contents.Count==0);
            if(smallCup!=null) smallCup.interactable=canChange;
            if(largeCup!=null) largeCup.interactable=canChange;
            bool onTray=HasDeliveryCup();
            HideSceneReplacedControls();
            if(deliver!=null) deliver.interactable=!blocked && onTray && request!=null && !runtime.PracticeTutorialActive;
            if(discard!=null) discard.interactable=!blocked && !guided && cup!=null;
            if(returnButton!=null) returnButton.interactable=!blocked && !guided;
            if(resetWorkstation!=null) resetWorkstation.interactable=!blocked && !runtime.PracticeTutorialActive && !runtime.CafeTutorialActive;
            if(bookButton!=null) bookButton.interactable=!blocked;
            if(orderRecipe!=null) orderRecipe.interactable=!blocked && request!=null;
            if(pauseButton!=null) pauseButton.interactable=string.IsNullOrEmpty(state.production.miniGameToken) && !(FreeCafeGMPanel.Instance!=null && FreeCafeGMPanel.Instance.IsVisible);
            var selected=world.Find(runtime.SelectedId);
            if(clearMachine!=null)
            {
                clearMachine.interactable=!blocked && !guided && selected!=null && !world.IsInfiniteLiquidContainer(selected) && (world.IsContainer(selected) || world.Spec(selected.specId).Has(Capability.Processor)) && selected.contents.Count>0;
                // The split layout only shows this utility while it can act on the selected vessel.
                if(splitTicket || clearMachine.transform.parent.name=="SecondaryMenus") clearMachine.gameObject.SetActive(clearMachine.interactable);
            }
            if(instruction!=null) instruction.text=runtime.Hint;
            if(result!=null) result.text=moneyText!=null ? state.production.lastDelivery ?? "" : string.IsNullOrEmpty(state.production.lastDelivery) ? "王国币 "+state.money+"  ·  声望 "+state.reputation+"  ·  可提前备料" : state.production.lastDelivery;
            // Container-anchored cards own heating/extraction presentation. Keep the
            // old serialized references for scene compatibility, but never show the
            // fixed brewer gauges (the brewer is not the precision extractor).
            if(timeCaption!=null) timeCaption.gameObject.SetActive(false);
            if(volumeCaption!=null) volumeCaption.gameObject.SetActive(false);
            if(timeGauge!=null) timeGauge.gameObject.SetActive(false);
            if(volumeGauge!=null) volumeGauge.gameObject.SetActive(false);
            if(machineStatus!=null) machineStatus.text="自由调配 · 悬停容器查看成分 · 加热与萃取进度显示在容器旁";
            if(miniGamePanel!=null) miniGamePanel.SetActive(!string.IsNullOrEmpty(state.production.miniGameToken) && (runtime.GetComponent<FreeCafeMiniGameGateway>()==null || !runtime.GetComponent<FreeCafeMiniGameGateway>().HasPresenter));
            if(bookPanel!=null && bookPanel.activeSelf) RefreshBook();
        }
        private void Update()
        {
            // Patience drains continuously; the full refresh only runs on world changes.
            if(patienceFill==null && patienceText==null || runtime==null || runtime.World==null) return;
            if(productionRoot!=null && !productionRoot.activeInHierarchy) return;
            var world=runtime.World; var request=world.Request(world.State.focusedRequestId);
            RefreshPatience(request,request!=null && request.id==CafeWorld.TutorialOrderId);
        }
        private void RefreshPatience(CustomerRequest request,bool tutorialOrder)
        {
            if(patienceFill==null && patienceText==null) return;
            float fraction=1; string caption="—";
            if(request!=null && runtime.World.IsNarrativeServiceOrder(request)) caption="剧情 · 不限时";
            else if(request!=null && !tutorialOrder)
            {
                if(!patienceMax.TryGetValue(request.id,out float max) || request.patience>max)
                    patienceMax[request.id]=max=Mathf.Max(request.patience,runtime.World.Catalog.customerPatience);
                fraction=max>0 ? Mathf.Clamp01(request.patience/max) : 0;
                caption=Mathf.CeilToInt(Mathf.Max(0,request.patience))+" 秒";
            }
            else if(tutorialOrder) caption="不限时";
            if(patienceText!=null && patienceText.text!=caption) patienceText.text=caption;
            if(patienceFill==null) return;
            var rect=patienceFill.rectTransform;
            rect.anchorMin=Vector2.zero; rect.anchorMax=new Vector2(fraction,1); rect.offsetMin=rect.offsetMax=Vector2.zero;
            patienceFill.color=fraction>.5f ? patienceCalm : fraction>.25f ? patienceWarn : patienceLow;
        }
        // Full-body customer art is cropped to head and shoulders; square avatars fill the frame.
        private static void FrameAvatar(Image avatar)
        {
            var frame=avatar.rectTransform.parent as RectTransform;
            if(frame==null || avatar.sprite==null || frame.GetComponent<RectMask2D>()==null) return;
            var rect=avatar.rectTransform; var size=frame.rect.size; var art=avatar.sprite.rect.size;
            float aspect=art.y/Mathf.Max(1,art.x);
            avatar.preserveAspect=true;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);
            if(aspect>1.45f) { float width=size.x*1.6f; rect.sizeDelta=new Vector2(width,width*aspect); rect.anchoredPosition=new Vector2(0,width*aspect*.02f); }
            else { rect.sizeDelta=size; rect.anchoredPosition=Vector2.zero; }
        }
    }
}
