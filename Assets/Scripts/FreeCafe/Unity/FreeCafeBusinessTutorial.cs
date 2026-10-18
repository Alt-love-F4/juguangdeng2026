using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>Real first-day handoff from the practice order to the business loop.</summary>
    public sealed class FreeCafeBusinessTutorial : MonoBehaviour
    {
        public enum Stage { OpenRecipe, CloseRecipe, ReturnToCafe, StartBusiness, WaitForCustomer, InspectCustomer, ReadOrder }
        public Stage CurrentStage { get; private set; }
        public bool IsRunning { get; private set; }
        public bool AllowBusinessStart => IsRunning && CurrentStage == Stage.StartBusiness;
        public bool PauseBusinessSimulation => IsRunning && CurrentStage != Stage.StartBusiness && CurrentStage != Stage.WaitForCustomer;
        private FreeCafeRuntime app;
        private FreeCafeProductionHud production;
        private FreeCafeHud hud;
        private FreeCafeTutorialPanel panel;
        private string customerId;

        public void Begin(FreeCafeRuntime owner)
        {
            Stop();
            app = owner;
            production = owner.GetComponent<FreeCafeProductionHud>();
            hud = owner.GetComponent<FreeCafeHud>();
            panel = Instantiate(Resources.Load<FreeCafeTutorialPanel>("UI/FreeCafeTutorialPanel"));
            panel.retry.SetActive(false);
            panel.exitButton.gameObject.SetActive(false);
            panel.shade.enabled = false;
            IsRunning = true;
            CurrentStage = Stage.OpenRecipe;
            // Reuse the prominent button only for the final explicit acknowledgement.
            panel.retryButton.onClick.AddListener(ConfirmOrderRead);
            var confirmRect = (RectTransform)panel.retryButton.transform;
            confirmRect.sizeDelta = new Vector2(360, confirmRect.sizeDelta.y);
            var confirmText = panel.retryButton.GetComponentInChildren<TMPro.TMP_Text>();
            confirmText.enableAutoSizing = true;
            confirmText.fontSizeMin = 24;
            confirmText.fontSizeMax = 32;
        }

        public void Stop()
        {
            IsRunning = false;
            if (panel != null) Destroy(panel.gameObject);
            panel = null;
        }
        private void OnDestroy() => Stop();

        public void CustomerInspected(string id)
        {
            if (!IsRunning || CurrentStage != Stage.InspectCustomer || app.World.Find(id) == null) return;
            foreach (var request in app.World.State.requests)
            {
                if (request.customerId != id) continue;
                customerId = id;
                CurrentStage = Stage.ReadOrder;
                return;
            }
        }
        public void ConfirmOrderRead()
        {
            if (!IsRunning || CurrentStage != Stage.ReadOrder || app.TutorialInputBlocked) return;
            if (app.World.Request(app.World.State.focusedRequestId) == null) return;
            app.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.PostCoffeeSignal);
            // The day-flow completion normally calls Stop; also handle an endpoint without a subscriber.
            Stop();
        }

        private string CustomerWithOrder()
        {
            foreach (var request in app.World.State.requests)
                if (request.id != CafeWorld.TutorialOrderId && app.World.Find(request.customerId) != null)
                    return request.customerId;
            return null;
        }
        private void LateUpdate()
        {
            if (!IsRunning || panel == null || app == null) return;
            bool bookOpen = production.bookPanel != null && production.bookPanel.activeInHierarchy;
            // Do not cover the first delivery receipt or dialogue with tutorial graphics.
            bool receipt = production.receiptPanel != null && production.receiptPanel.activeInHierarchy;
            bool visible = !receipt && (!app.TutorialInputBlocked || bookOpen && !Spotlight.GameFlow.DialoguePanel.IsAnyPlaying);
            panel.canvas.gameObject.SetActive(visible);
            if (!visible) return;
            if (CurrentStage == Stage.OpenRecipe && bookOpen) CurrentStage = Stage.CloseRecipe;
            else if (CurrentStage == Stage.CloseRecipe && !bookOpen) CurrentStage = Stage.ReturnToCafe;
            else if (CurrentStage == Stage.ReturnToCafe && app.World.State.currentArea == AreaId.Cafe) CurrentStage = Stage.StartBusiness;
            else if (CurrentStage == Stage.StartBusiness && app.World.State.open) CurrentStage = Stage.WaitForCustomer;
            if (CurrentStage == Stage.WaitForCustomer)
            {
                customerId = CustomerWithOrder();
                if (customerId != null) CurrentStage = Stage.InspectCustomer;
            }
            Draw();
        }

        private void Draw()
        {
            Vector2 target = new Vector2(Screen.width * .5f, Screen.height * .55f);
            RectTransform ui = null;
            string description, summary;
            switch (CurrentStage)
            {
                case Stage.OpenRecipe:
                    description = "首杯黑咖啡已交付！接下来认识正式营业的常用操作。\n点击上方「咖啡谱」，查看饮品配方、用量与制作说明；忘记做法时随时回来查。";
                    summary = "忘记做法就查咖啡谱\n点击上方「咖啡谱」";
                    ui = production.bookButton.transform as RectTransform;
                    break;
                case Stage.CloseRecipe:
                    description = "咖啡谱记录饮品配方、参考用量和制作说明。\n记住这个入口即可；关闭咖啡谱，接下来回大厅开始正式营业。";
                    summary = "咖啡谱可以随时查\n看完关闭咖啡谱\n接下来回大厅营业";
                    var compendium = production.bookPanel.GetComponent<CoffeeCompendiumPanel>();
                    ui = compendium != null && compendium.closeButton != null
                        ? compendium.closeButton.transform as RectTransform : production.bookButton.transform as RectTransform;
                    break;
                case Stage.ReturnToCafe:
                    description = "准备接待正式顾客。点击上方「返回大厅」，回到咖啡馆一楼。\n工作台用于制作；大厅用于接待顾客和开始营业。";
                    summary = "准备接待正式顾客\n点击「返回大厅」\n不是退出游戏";
                    ui = production.returnButton.transform as RectTransform;
                    break;
                case Stage.StartBusiness:
                    description = "点击大厅左侧窗边的「开始营业」挂牌，开始今天的正式经营。\n开店后顾客会陆续入座。挂牌翻面后显示「结束营业」，完成教程后再次点击即可打烊。";
                    summary = "准备开店接客\n点击窗边「开始营业」挂牌\n等顾客入座后查看需求";
                    foreach (var entity in app.World.State.entities)
                        if (app.World.Spec(entity.specId).Has(Capability.BusinessControl)) { target = SignPoint(entity.id); break; }
                    break;
                case Stage.WaitForCustomer:
                    description = "已经开始营业，等待第一位正式顾客入座。\n顾客到店后，点击顾客查看他想喝什么；不需要立即回工作台。";
                    summary = "营业已开始，等待顾客\n入座后点击顾客查看需求";
                    break;
                case Stage.InspectCustomer:
                    description = "顾客已经入座。点击这位顾客，查看他想要的饮品与需求。\n看清需求后，再根据左上角订单准备对应的咖啡。";
                    summary = "先问顾客想喝什么\n点击顾客查看需求\n不要直接开始制作";
                    target = WorldPoint(customerId);
                    break;
                default:
                    description = "看左上角订单卡：确认顾客、饮品、杯型和耐心。多份订单可切换查看；不记得做法就到工作台查咖啡谱。\n点击「明白了，开始经营」结束引导：查看需求 → 制作咖啡 → 放盘按铃交付。";
                    summary = "看左上角订单\n确认饮品、杯型和耐心\n读完点「明白了，开始经营」";
                    if (hud != null && hud.orderButtons != null && hud.orderButtons.Length > 0 && hud.orderButtons[0] != null)
                        ui = hud.orderButtons[0].transform as RectTransform;
                    break;
            }
            if (ui != null) target = RectTransformUtility.WorldToScreenPoint(null, ui.TransformPoint(ui.rect.center));
            panel.heading.text = "首日新手教程 · 正式营业";
            panel.instruction.text = description;
            panel.errorText.text = "完成咖啡谱、营业、顾客需求和订单引导后，进入自由经营";
            panel.gesture.gameObject.SetActive(true);
            panel.gestureText.text = summary;
            panel.SetMouseMode(false, false);
            bool confirm = CurrentStage == Stage.ReadOrder;
            panel.retry.SetActive(confirm);
            panel.retryButton.GetComponentInChildren<TMPro.TMP_Text>().text = "明白了，开始经营";
            float scale = panel.canvas.scaleFactor;
            Vector2 half = panel.gesture.rect.size * .5f + Vector2.one * 12;
            var badge = target + Vector2.down * (half.y + 65) * scale;
            badge.x = Mathf.Clamp(badge.x, half.x * scale, Mathf.Max(half.x * scale, Screen.width - half.x * scale));
            badge.y = Mathf.Clamp(badge.y, Screen.height * .28f + half.y * scale, Mathf.Max(Screen.height * .28f + half.y * scale, Screen.height - half.y * scale));
            panel.gesture.position = badge;
            for (int i = 0; i < panel.trail.Length; i++)
            {
                var dot = panel.trail[i];
                dot.enabled = CurrentStage != Stage.WaitForCustomer;
                float angle = i * Mathf.PI * 2 / panel.trail.Length;
                float radius = (40 + 12 * Mathf.Sin(Time.unscaledTime * 4)) * scale;
                dot.rectTransform.position = target + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                dot.color = panel.trailColor;
            }
        }
        private Vector2 SignPoint(string id)
        {
            var sign = app.View(id)?.GetComponent<FreeCafeBusinessSignView>();
            return sign == null ? WorldPoint(id) : (Vector2)app.worldCamera.WorldToScreenPoint(sign.CaptionWorldPosition);
        }
        private Vector2 WorldPoint(string id)
        {
            var view = app.View(id);
            return view == null ? new Vector2(Screen.width * .5f, Screen.height * .55f)
                : (Vector2)app.worldCamera.WorldToScreenPoint(view.transform.position);
        }
    }
}
