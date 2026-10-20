using Spotlight.GameFlow;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>Real first-day handoff from the practice order to the business loop.</summary>
    public sealed class FreeCafeBusinessTutorial : MonoBehaviour
    {
        public enum Stage { OpenRecipe, CloseRecipe, ReturnToCafe, StartBusiness, WaitForCustomer, InspectCustomer, ReadOrder, TodoGuidance, OpenDetails, CloseDetails, ReadTodo }
        public Stage CurrentStage { get; private set; }
        public bool IsRunning { get; private set; }
        public string TodoInstruction => panel != null ? panel.instruction.text : "跟随正式营业教程继续操作。";
        public bool AllowBusinessStart => IsRunning && CurrentStage == Stage.StartBusiness;
        public bool PauseBusinessSimulation => IsRunning && CurrentStage != Stage.StartBusiness && CurrentStage != Stage.WaitForCustomer;
        private FreeCafeRuntime app;
        private FreeCafeProductionHud production;
        private FreeCafeHud hud;
        private FreeCafeTutorialPanel panel;
        private const string TutorialTodoId = "guide.cafe.tutorial";
        private string customerId;
        private int defaultSortingOrder;
        private bool HudGuidance => CurrentStage == Stage.OpenDetails || CurrentStage == Stage.CloseDetails
            || CurrentStage == Stage.TodoGuidance || CurrentStage == Stage.ReadTodo;

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
            defaultSortingOrder = panel.canvas.sortingOrder;
            IsRunning = true;
            CurrentStage = Stage.OpenRecipe;
            // Reuse the prominent button for the final order and todo acknowledgements.
            panel.retryButton.onClick.AddListener(ConfirmCurrentStage);
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
        private void ConfirmCurrentStage()
        {
            if (CurrentStage == Stage.ReadTodo) ConfirmTodoGuidance();
            else ConfirmOrderRead();
        }

        public void ConfirmOrderRead()
        {
            if (!IsRunning || CurrentStage != Stage.ReadOrder || app.TutorialInputBlocked) return;
            if (app.World.Request(app.World.State.focusedRequestId) == null) return;
            CurrentStage = Stage.OpenDetails;
            Draw();
        }

        public void ConfirmTodoGuidance()
        {
            if (!IsRunning || CurrentStage != Stage.ReadTodo || app.TutorialInputBlocked) return;
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
            var sharedHud = GameplayHudPanel.Instance;
            bool detailsOpen = sharedHud != null && sharedHud.IsDetailsOpen;
            bool readingDetails = detailsOpen && (CurrentStage == Stage.OpenDetails || CurrentStage == Stage.CloseDetails);
            // The details window normally blocks world/tutorial input. This step deliberately
            // stays visible above it, without accepting gameplay input or covering its controls.
            bool visible = !receipt && (!app.TutorialInputBlocked
                || (bookOpen || readingDetails) && !DialoguePanel.IsAnyPlaying && Time.timeScale > 0);
            panel.canvas.gameObject.SetActive(visible);
            if (!visible) return;
            if (CurrentStage == Stage.OpenRecipe && bookOpen) CurrentStage = Stage.CloseRecipe;
            else if (CurrentStage == Stage.CloseRecipe && !bookOpen) CurrentStage = Stage.ReturnToCafe;
            else if (CurrentStage == Stage.ReturnToCafe && app.World.State.currentArea == AreaId.Cafe) CurrentStage = Stage.StartBusiness;
            else if (CurrentStage == Stage.StartBusiness && app.World.State.open) CurrentStage = Stage.WaitForCustomer;
            else if (CurrentStage == Stage.OpenDetails && detailsOpen) CurrentStage = Stage.CloseDetails;
            else if (CurrentStage == Stage.CloseDetails && !detailsOpen) CurrentStage = Stage.TodoGuidance;
            else if (CurrentStage == Stage.TodoGuidance && sharedHud != null && sharedHud.Tasks.IsVisible) CurrentStage = Stage.ReadTodo;
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
                    description = "首杯黑咖啡已交付！接下来准备开始营业。\n先用鼠标左键点击上方「咖啡谱」，记住配方、用量与制作说明的查看位置；看完返回大厅，点击「开始营业」挂牌。";
                    summary = "黑咖啡已交付，准备营业\n鼠标左键点击上方「咖啡谱」\n看完回大厅点击「开始营业」";
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
                    description = "用鼠标左键点击大厅左侧窗边的「开始营业」挂牌，开始今天的正式经营。\n开店后顾客会陆续入座。挂牌翻面后显示「结束营业」，完成教程后再次点击即可打烊。";
                    summary = "准备开店接客\n鼠标左键点击「开始营业」挂牌\n等顾客入座后查看需求";
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
                case Stage.OpenDetails:
                    description = "最后认识上方 HUD 的常用入口。把鼠标移到上方「详情面板」，用鼠标左键点击，或按下 Tab 打开。\n这里可以查看自己的属性与专精、人物信息和世界进展。";
                    summary = "把鼠标移到上方 HUD\n左键点击「详情面板」\n或按 Tab 打开，查看各项功能";
                    ui = GameplayHudPanel.Instance != null ? GameplayHudPanel.Instance.DetailsButton.transform as RectTransform : null;
                    break;
                case Stage.CloseDetails:
                    description = "详情面板：查看属性与专精、人物信息、世界进展。点击页签切换，悬停属性或专精查看说明。\n看完按 Tab / Esc 或点击关闭按钮，再认识待办事项。";
                    summary = DetailsSummary();
                    break;
                case Stage.TodoGuidance:
                    description = "如果不知道接下来做什么，一定要打开「待办事项」！\n把鼠标移到上方 HUD，鼠标左键点击「待办事项」，或按下 T，查看下一步目标。";
                    summary = "不知道接下来做什么？\n一定要打开「待办事项」\n左键点击上方入口，或按 T";
                    ui = GameplayHudPanel.Instance != null ? GameplayHudPanel.Instance.TasksButton.transform as RectTransform : null;
                    break;
                case Stage.ReadTodo:
                    var tasks = GameplayHudPanel.Instance != null ? GameplayHudPanel.Instance.Tasks : null;
                    bool expanded = tasks != null && tasks.ExpandedId == TutorialTodoId;
                    bool tracked = tasks != null && tasks.TrackedId == TutorialTodoId;
                    description = "左键点击「完成当前咖啡馆教程」任务，展开任务说明和「追踪此待办」按钮；点击追踪即可快速查看当前目标。\n这是可选体验：按钮没有显示、UI 对不上，或已经了解时，直接点击下方「完成教程」即可，不必完成追踪。";
                    summary = tracked
                        ? "已开启任务追踪\n以后按 T 查看当前目标\n点击「完成教程」继续经营"
                        : expanded
                            ? "任务已展开\n可点击「追踪此待办」快速追踪\n也可直接点击「完成教程」"
                            : "左键点击「完成当前咖啡馆教程」\n展开后可选择「追踪此待办」\n找不到也可直接「完成教程」";
                    var todoButton = tasks != null ? (expanded && !tracked
                        ? tasks.GetTrackingButton(TutorialTodoId) : tasks.GetItemButton(TutorialTodoId)) : null;
                    if (tasks == null || !tasks.IsVisible || todoButton == null && !tracked)
                        summary = "按 T 打开待办，点击任务可展开追踪\n任务或按钮未显示也不影响继续\n直接点击「完成教程」即可";
                    ui = todoButton != null ? todoButton.transform as RectTransform : null;
                    break;
                default:
                    description = "看左上角订单卡：确认顾客、饮品、杯型和耐心。多份订单可切换查看；不记得做法就到工作台查咖啡谱。\n查看需求 → 制作咖啡 → 放盘按铃交付。读完后点击「明白了，下一步」。";
                    summary = "看左上角订单\n确认饮品、杯型和耐心\n读完点「明白了，下一步」";
                    if (hud != null && hud.orderButtons != null && hud.orderButtons.Length > 0 && hud.orderButtons[0] != null)
                        ui = hud.orderButtons[0].transform as RectTransform;
                    break;
            }
            if (ui != null) target = RectTransformUtility.WorldToScreenPoint(null, ui.TransformPoint(ui.rect.center));
            if (MobileTouchInput.IsAndroidPlayer)
            {
                description = ToAndroidInstructions(description);
                summary = ToAndroidInstructions(summary);
            }
            bool readingDetails = CurrentStage == Stage.CloseDetails;
            panel.canvas.sortingOrder = readingDetails ? short.MaxValue : defaultSortingOrder;
            // The full-size instruction card would cover the details pages; keep only the
            // non-interactive mouse hint while the player explores this modal window.
            panel.instruction.transform.parent.gameObject.SetActive(!readingDetails);
            panel.heading.text = HudGuidance ? "首日新手教程 · HUD 与下一步指引" : "首日新手教程 · 正式营业";
            panel.instruction.text = description;
            panel.errorText.text = CurrentStage == Stage.ReadTodo
                ? "任务展开与追踪均可跳过 · 点击「完成教程」即可继续经营 · 顾客耐心暂停"
                : HudGuidance ? "查看详情并关闭后，再打开待办事项；教程期间顾客耐心暂停" : "完成咖啡谱、营业、顾客需求和订单引导后，进入自由经营";
            panel.gesture.gameObject.SetActive(true);
            panel.gestureText.text = summary;
            panel.SetMouseMode(false, false);
            if (MobileTouchInput.IsAndroidPlayer) panel.gesture.gameObject.SetActive(false);
            bool confirm = CurrentStage == Stage.ReadOrder || CurrentStage == Stage.ReadTodo;
            panel.retry.SetActive(confirm);
            panel.retryButton.GetComponentInChildren<TMPro.TMP_Text>().text = CurrentStage == Stage.ReadTodo ? "完成教程" : "明白了，下一步";
            float scale = panel.canvas.scaleFactor;
            Vector2 half = panel.gesture.rect.size * .5f + Vector2.one * 12;
            Vector2 pointer = MobileTouchInput.IsAndroidPlayer || Mouse.current == null
                ? target : Mouse.current.position.ReadValue();
            var badge = HudGuidance
                ? pointer + Vector2.down * Mathf.Max(panel.gestureOffset, half.y + 35) * scale
                : target + Vector2.down * (half.y + 65) * scale;
            badge.x = Mathf.Clamp(badge.x, half.x * scale, Mathf.Max(half.x * scale, Screen.width - half.x * scale));
            float minY = (HudGuidance ? 0 : Screen.height * .28f) + half.y * scale;
            badge.y = Mathf.Clamp(badge.y, minY, Mathf.Max(minY, Screen.height - half.y * scale));
            panel.gesture.position = badge;
            bool pointToHud = CurrentStage == Stage.OpenDetails || CurrentStage == Stage.TodoGuidance
                || CurrentStage == Stage.ReadTodo && ui != null;
            for (int i = 0; i < panel.trail.Length; i++)
            {
                var dot = panel.trail[i];
                dot.enabled = CurrentStage != Stage.WaitForCustomer && !readingDetails
                    && (CurrentStage != Stage.ReadTodo || ui != null);
                float angle = i * Mathf.PI * 2 / panel.trail.Length;
                float radius = (40 + 12 * Mathf.Sin(Time.unscaledTime * 4)) * scale;
                // Moving light leads from the actual cursor to the exact HUD button.
                float t = Mathf.Repeat(Time.unscaledTime * .65f - i * .035f, 1);
                dot.rectTransform.position = pointToHud ? Vector2.Lerp(pointer, target, t)
                    : target + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                dot.color = panel.trailColor;
            }
        }

        private static string ToAndroidInstructions(string text)
        {
            return text.Replace("按住鼠标左键拖动", "按住并拖动")
                .Replace("鼠标左键点击", "点击")
                .Replace("鼠标左键拖动", "按住并拖动")
                .Replace("鼠标左键", "触屏")
                .Replace("把鼠标移到", "点击")
                .Replace("鼠标移到", "点击")
                .Replace("鼠标", "触屏")
                .Replace("左键点击", "点击")
                .Replace("左键拖动", "按住并拖动")
                .Replace("左键", "触屏")
                .Replace("按 Tab / Esc", "点击关闭按钮")
                .Replace("按 T 打开「待办事项」", "点击上方「待办事项」")
                .Replace("按 B 打开「物品栏」", "点击上方「物品栏」")
                .Replace("按下 Tab", "点击上方详情入口")
                .Replace("按 Tab", "点击上方详情入口")
                .Replace("按下 T", "点击上方「待办事项」")
                .Replace("按 T", "点击上方「待办事项」")
                .Replace("按 B", "点击上方「物品栏」")
                .Replace("右键点击地图空白处或按 Esc", "点击地图空白处");
        }
        private string DetailsSummary()
        {
            var details = GameplayHudPanel.Instance != null ? GameplayHudPanel.Instance.OpenDetailsPanel : null;
            string feature;
            switch (details != null ? details.CurrentPage : DetailsPanel.Page.MyInfo)
            {
                case DetailsPanel.Page.Relationships:
                    feature = "人物关系：点击已结识人物\n查看介绍、熟悉度及相关信息";
                    break;
                case DetailsPanel.Page.World:
                    feature = "世界进展：点击条目\n查看世界状态与进展说明";
                    break;
                default:
                    feature = "我的信息：查看属性与专精\n悬停属性 / 专精可查看说明";
                    break;
            }
            return feature + "\n点击页签切换功能\n看完按 Tab / Esc 关闭";
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
