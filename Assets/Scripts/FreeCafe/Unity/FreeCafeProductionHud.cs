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
        public bool IsModalOpen => bookPanel!=null && bookPanel.activeInHierarchy;
        public void CloseOverlays() { if(bookPanel!=null) bookPanel.SetActive(false); }
        private static readonly Color Ink=new Color(.25f,.15f,.095f);
        public GameObject productionRoot, legacyTop, legacyBottom, bookPanel, miniGamePanel;
        public TMP_Text timeCaption, volumeCaption;
        public TMP_Text orderTitle, customerName, orderDescription, cupStatus, instruction, result, machineStatus, bookTitle, bookBody, bookNote;
        public Image customerAvatar, bookIcon;
        public Button previousOrder, nextOrder, smallCup, largeCup, deliver, discard, returnButton, bookButton, closeBook,
            previousRecipe, nextRecipe, miniGameButton, cancelMiniGame, startBusiness, pauseButton, saveButton, clearMachine;
        public Slider timeGauge, volumeGauge;
        public Image timeGreen, timeGold, volumeGreen, volumeGold;
        private int recipeIndex;
        private bool bound;
        private CoffeeBookPanelView bookView;
        private Coroutine completionFeedback;
        private void Start() { Bind(); Refresh(); }
        private void OnEnable() { Bind(); }
        private void Bind()
        {
            if(bound || runtime==null) return;
            bound=true; runtime.ViewChanged+=Refresh;
            bookView = bookPanel==null ? null : bookPanel.GetComponent<CoffeeBookPanelView>();
            if(bookView != null) bookView.RecipeSelected += SelectRecipe;
            BindControls(true);
            if (miniGameButton != null) miniGameButton.gameObject.SetActive(false);
            foreach (Button button in new[] { previousOrder, nextOrder, previousRecipe, nextRecipe, smallCup, largeCup, deliver, discard, clearMachine })
                Spotlight.Audio.UIButtonAudio.Configure(button, string.Empty);
            foreach (Button button in new[] { returnButton, closeBook, cancelMiniGame })
                Spotlight.Audio.UIButtonAudio.Configure(button, Spotlight.Audio.AudioKeys.Cancel);
        }
        private void OnDisable()
        {
            if(completionFeedback!=null) { StopCoroutine(completionFeedback); completionFeedback=null; if(result!=null) result.color=Ink; }
            if(!bound) return; bound=false;
            if(bookView != null) bookView.RecipeSelected -= SelectRecipe;
            if(runtime!=null) runtime.ViewChanged-=Refresh;
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
            SetListener(pauseButton,OpenPlayerMenu,listening); SetListener(clearMachine,ClearMachine,listening);
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
        private void CancelMiniGame()
        {
            var session=runtime.World.State.production;
            Execute(runtime.World.ResolveProductionMiniGame(session.miniGameToken,false,0));
        }
        private void OpenBook() { if(bookPanel==null || runtime.InputBlocked) return; runtime.CancelDrag(); bookPanel.SetActive(true); RefreshBook(); }
        private void CloseBook() { if(bookPanel!=null) bookPanel.SetActive(false); }
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
            text.Append("杯型：").Append(world.Spec(product.cupSpecId).label).Append(" / 温度：").Append(product.requiredTemperature==LiquidTemperature.Boiling ? "沸腾" : product.requiredTemperature==LiquidTemperature.Warm ? "温热" : "常温").AppendLine();
            text.AppendLine("参考用量（自由操作，不锁步骤）：");
            foreach(var p in product.ingredients) text.Append("  ").Append(FreeCafeProductionText.Recipe(world.Spec(p.specId).label)).Append("  ").Append(p.milliliters>0 ? p.milliliters.ToString("0")+" ml" : p.units+" 份").AppendLine();
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
                if(bookPanel!=null) bookPanel.SetActive(false);
                if(miniGamePanel!=null) miniGamePanel.SetActive(false);
                return;
            }
            bool blocked=runtime.InputBlocked;
            bool guided=runtime.WorkstationTutorial!=null && runtime.WorkstationTutorial.IsFirstCafeTutorial;
            var request=world.Request(state.focusedRequestId);
            bool tutorialOrder=request!=null && request.id==CafeWorld.TutorialOrderId;
            var product=request==null ? null : world.Product(request.productId);
            if(orderTitle!=null) orderTitle.text="交付此订单  ·  "+(request==null ? "暂无订单" : (state.requests.IndexOf(request)+1)+" / "+state.requests.Count);
            var customer=request==null ? null : world.Find(request.customerId);
            string name="等待顾客";
            if(customer!=null)
            {
                name=world.PersonName(customer);
            }
            if(customerName!=null) customerName.text=name+(tutorialOrder ? "  ·  教程客人 · 尚未营业" : request==null ? "" : "  ·  耐心 "+request.patience.ToString("0")+"s");
            string customerArt=customer==null ? "customer" : world.Spec(customer.specId).artId;
            if(customer!=null && customer.person!=null && !string.IsNullOrEmpty(customer.person.visualId) && runtime.artLibrary.Find(customer.person.visualId)!=null)
                customerArt=customer.person.visualId;
            var character=customer!=null && customer.person!=null ? runtime.Character(customer.person.characterId) : null;
            if(customerAvatar!=null)
            {
                customerAvatar.sprite=character!=null && character.avatar!=null ? character.avatar : runtime.artLibrary.Resolve(customerArt);
                customerAvatar.gameObject.SetActive(customerAvatar.sprite!=null);
            }
            if(orderDescription!=null) orderDescription.text=product==null ? "尚无订单，可先准备原料。" : tutorialOrder ? "首杯黑咖啡\n跟随指引制作黑咖啡，放入交付区后按铃交给客人。" : FreeCafeProductionText.Recipe(product.label)+"  ·  "+FreeCafeProductionText.Recipe(product.description)+"\n满意要求 ≥ "+product.minimumScore.ToString("0")+"分"+(request.remakes>0 ? "  ·  正在重做" : "");
            if(previousOrder!=null) previousOrder.interactable=!blocked && state.requests.Count>1;
            if(nextOrder!=null) nextOrder.interactable=!blocked && state.requests.Count>1;
            var cup=world.ServingCup;
            if(cupStatus!=null) cupStatus.text=cup==null ? "出餐托盘 · 未选杯" : world.Spec(cup.specId).label+" · "+FreeCafeProductionText.Recipe(world.ClassifyCoffee(cup).label)+"  "+world.LiquidVolume(cup).ToString("0")+" / "+world.LiquidCapacity(cup).ToString("0")+" ml"+(cup.latteArt ? "  · "+FreeCafeProductionText.LatteArtContents(world.LatteArtFoamVolume(cup),cup.latteArtPattern)+" "+cup.latteArtScore.ToString("0")+"分" : "");
            bool canChange=!blocked && !guided && (cup==null || cup.contents.Count==0);
            if(smallCup!=null) smallCup.interactable=canChange;
            if(largeCup!=null) largeCup.interactable=canChange;
            if(deliver!=null) deliver.interactable=!blocked && (runtime.WorkstationTutorial==null || runtime.WorkstationTutorial.CanSubmitCup);
            if(discard!=null) discard.interactable=!blocked && !guided && cup!=null;
            if(returnButton!=null) returnButton.interactable=!blocked && !guided;
            if(bookButton!=null) bookButton.interactable=!blocked;
            if(pauseButton!=null) pauseButton.interactable=string.IsNullOrEmpty(state.production.miniGameToken) && !(FreeCafeGMPanel.Instance!=null && FreeCafeGMPanel.Instance.IsVisible);
            var selected=world.Find(runtime.SelectedId);
            if(clearMachine!=null) clearMachine.interactable=!blocked && !guided && selected!=null && (world.IsContainer(selected) || world.Spec(selected.specId).Has(Capability.Processor)) && selected.contents.Count>0;
            if(instruction!=null) instruction.text=runtime.Hint;
            if(result!=null) result.text=string.IsNullOrEmpty(state.production.lastDelivery) ? "王国币 "+state.money+"  ·  声望 "+state.reputation+"  ·  可提前备料" : state.production.lastDelivery;
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
    }
}
