using System;
using System.Collections;
using Spotlight.GameFlow;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Spotlight.FreeCafe
{
    /// <summary>A read-only tour of the real map controls. Never executes an action for the player.</summary>
    public sealed class MapUsageTutorial : MonoBehaviour
    {
        public enum Stage { Overview, SelectLocation, ReadLocation, Finish, TodoGuidance }
        public Stage CurrentStage { get; private set; }
        public bool IsRunning => panel != null;
        private MapSceneController map;
        private FreeCafeTutorialPanel panel;
        private DayFlowController days;
        private bool recordCompletion, selectedPopup, autoDismissed, cafeArrival;
        private float nextCheck;
        private CafeWorld todoHintWorld;
        private int todoHintDay = -1;

        private bool ShouldShowTodoHint
        {
            get
            {
                var world = PlayerCafeOwner.Current;
                return world != null && !world.State.open && world.State.businessPhase == BusinessPhase.Night
                    && (todoHintWorld != world || todoHintDay != world.State.day)
                    && days != null && !days.TodoGuidanceAcknowledged
                    && !days.IsFirstSessionGuideActive && !map.IsCafeArrivalGuide;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SceneManager.sceneLoaded -= SceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => SceneManager.sceneLoaded += SceneLoaded;

        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var controller in root.GetComponentsInChildren<MapSceneController>(true))
                    if (controller.transform is RectTransform && controller.GetComponent<MapUsageTutorial>() == null)
                        controller.gameObject.AddComponent<MapUsageTutorial>();
        }

        public bool Begin(MapSceneController owner, bool saveCompletion = false)
        {
            if (owner == null || !owner.UsageTutorialAvailable) return false;
            var prefab = Resources.Load<FreeCafeTutorialPanel>("UI/FreeCafeTutorialPanel");
            if (prefab == null) return false;
            Stop();
            map = owner;
            days = FindFirstObjectByType<DayFlowController>();
            recordCompletion = saveCompletion;
            cafeArrival = owner.IsCafeArrivalGuide;
            panel = Instantiate(prefab);
            panel.name = "MapUsageTutorialPanel";
            panel.instruction.fontSizeMax = 30;
            panel.instruction.fontSizeMin = 22;
            panel.shade.enabled = false;
            panel.exitButton.gameObject.SetActive(true);
            panel.exitButton.GetComponentInChildren<TMPro.TMP_Text>().text = "稍后再看";
            panel.exitButton.onClick.AddListener(() => { autoDismissed = true; Stop(); });
            panel.retryButton.onClick.AddListener(Advance);
            var label = panel.retryButton.GetComponentInChildren<TMPro.TMP_Text>();
            label.enableAutoSizing = true;
            label.fontSizeMin = 22;
            label.fontSizeMax = 32;
            panel.canvas.sortingOrder = GameplayHudPanel.SortingOrder + 10;
            map.LocationSelected += LocationSelected;
            CurrentStage = Stage.Overview;
            Draw();
            return true;
        }

        private bool BeginTodoGuidance()
        {
            if (!Begin(map)) return false;
            CurrentStage = Stage.TodoGuidance;
            todoHintWorld = PlayerCafeOwner.Current;
            todoHintDay = todoHintWorld.State.day;
            panel.name = "MapTodoGuidancePanel";
            Draw();
            return true;
        }

        public void Stop()
        {
            if (map != null) map.LocationSelected -= LocationSelected;
            if (panel != null) Destroy(panel.gameObject);
            panel = null;
            autoDismissed = true;
            nextCheck = Time.unscaledTime + 2f;
        }

        private void OnDisable() { Stop(); autoDismissed = false; }
        private void OnDestroy() => Stop();

        public void Advance()
        {
            if (!IsRunning || !CanShow) return;
            switch (CurrentStage)
            {
                case Stage.Overview: CurrentStage = Stage.SelectLocation; break;
                case Stage.ReadLocation: CurrentStage = Stage.Finish; break;
                case Stage.TodoGuidance:
                    days?.AcknowledgeTodoGuidance();
                    Stop();
                    return;
                case Stage.Finish:
                    if (recordCompletion && days != null) days.CompleteMapUsageTutorial();
                    Stop();
                    return;
                default: return;
            }
            Draw();
        }

        private void LocationSelected(string positionKey, bool popupVisible)
        {
            if (!IsRunning || (CurrentStage != Stage.SelectLocation && CurrentStage != Stage.Overview) || !CanShow) return;
            selectedPopup = popupVisible;
            CurrentStage = Stage.ReadLocation;
            Draw();
        }

        private bool CanShow => map != null && map.UsageTutorialAvailable
            && !(FreeCafeGMPanel.Instance != null && FreeCafeGMPanel.Instance.IsVisible)
            && YokiFrame.UIKit.GetPanel<ShortLoadingPanel>()?.gameObject.activeInHierarchy != true
            && YokiFrame.UIKit.GetPanel<LoadingPanel>()?.gameObject.activeInHierarchy != true;

        private void Update()
        {
            if (!IsRunning)
            {
                if (Time.unscaledTime < nextCheck) return;
                nextCheck = Time.unscaledTime + .5f;
                map = GetComponent<MapSceneController>();
                days = FindFirstObjectByType<DayFlowController>();
                if (CanShow && ShouldShowTodoHint && BeginTodoGuidance()) return;
                if (!autoDismissed && days != null && days.ShouldShowMapUsageTutorial && CanShow) Begin(map, true);
                return;
            }
            if (CurrentStage == Stage.TodoGuidance && GameplayHudPanel.Instance?.Tasks?.IsVisible == true)
            {
                days?.AcknowledgeTodoGuidance();
                Stop();
                return;
            }
            panel.canvas.gameObject.SetActive(CanShow);
            if (CanShow) Draw();
        }

        private void Draw()
        {
            string description, hint, button;
            switch (CurrentStage)
            {
                case Stage.TodoGuidance:
                    description = "今天已经打烊，可以在地图上安排接下来的事务。\n如果不知道接下来做什么，用鼠标左键点击上方「待办事项」，或按下 T，查看当前可以进行的事务。";
                    hint = "不知道接下来做什么？\n左键点击上方「待办事项」\n或按 T 查看可进行的事务";
                    button = "知道了";
                    break;
                case Stage.Overview:
                    description = cafeArrival
                        ? "欢迎来到城市地图！先了解如何移动地图和查看地点。\n在地图空白处按住鼠标左键拖动；滚动鼠标滚轮缩放，也可以拖动缩放条。目前先前往咖啡馆，完成新手教程。"
                        : "城市地图可以查看地点、安排行动。\n在地图空白处按住鼠标左键拖动；滚动鼠标滚轮缩放，也可以拖动缩放条。";
                    hint = "地图空白处：按住左键拖动\n鼠标滚轮：放大 / 缩小\n点「下一步」学习查看点位";
                    button = "下一步：查看点位";
                    break;
                case Stage.SelectLocation:
                    description = cafeArrival
                        ? "请用鼠标左键点击金色光圈中的「咖啡馆」，查看地点详情。\n目前只能进入咖啡馆，其他地点暂未开放。查看地点和进入咖啡馆都不会消耗行动点。"
                        : "试着用鼠标左键点击一个可用的地图点位，查看该地点的行动卡与需求。\n查看点位不扣资源。营业中执行其他地图行动时，咖啡店会自动打烊；只查看或返回咖啡馆不会打烊。";
                    hint = cafeArrival
                        ? "鼠标左键点击发光的咖啡馆\n其他地点暂未开放\n查看地点不消耗行动点"
                        : "鼠标左键点击地图点位\n查看该地点的行动与需求\n只查看不会消耗资源";
                    button = cafeArrival ? "请先点击咖啡馆" : "请先点击地图点位";
                    break;
                case Stage.ReadLocation:
                    description = cafeArrival
                        ? "这里是咖啡馆的地点详情，点击「进入咖啡馆」即可前往，不消耗行动点。\n以后探索其他地点时，先查看行动介绍和需求：行动点、物品、属性或前置条件，以实际卡片为准；确认执行后才会结算。"
                        : selectedPopup
                        ? "点位行动卡会显示介绍和需求：行动点、物品、属性或前置条件，以实际卡片为准；多张卡可翻页查看。\n满足需求且确认要做时才点行动按钮；需求不足先按 B 核对物品。本教程无需执行，咖啡馆入口也不扣行动点。"
                        : "这个点位目前没有开放的行动，所以只移动地图，不弹出行动卡。不是每个地点都有事情可做。\n有可用内容时会显示行动介绍及行动点、物品、属性或前置条件等需求；先看需求，再决定是否执行。咖啡馆入口只负责导航。";
                    hint = selectedPopup
                        ? "先看行动说明，再看需求\n行动点 / 物品 / 条件以卡片为准\n确认执行才会结算行动"
                        : "当前地点暂无开放行动\n有内容才显示行动卡与需求\n不需要随意点击执行";
                    button = "明白了，下一步";
                    break;
                default:
                    description = cafeArrival
                        ? "地图上方可查看行动点。以后可以按 T 查看待办、按 B 核对物品；自由探索时可用右键或 Esc 收起地点详情。\n现在请点击「进入咖啡馆」，继续咖啡馆新手教程。其他地点的行动可在后续探索时了解。"
                        : "不知道接下来做什么时，按 T 打开「待办事项」查看目标；按 B 打开「物品栏」核对物品，上方 HUD 可查看行动点。\n点位详情可用右键点击地图空白处或按 Esc 收起。有必须完成的行动时，先处理它；安排好后，回咖啡馆三楼宿舍点击床睡觉，才会进入下一天。";
                    hint = cafeArrival
                        ? "先进入咖啡馆继续新手教程\n进入咖啡馆不消耗行动点\n其他地图操作之后再体验"
                        : "不知道做什么：按 T 看待办\n核对物品：按 B 打开物品栏\n右键地图 / Esc：收起行动卡";
                    button = cafeArrival ? "明白了，前往咖啡馆" : "明白了，开始探索";
                    break;
            }
            if (MobileTouchInput.IsAndroidPlayer)
            {
                switch (CurrentStage)
                {
                    case Stage.Overview:
                        description = recordCompletion
                            ? "营业结束，接下来可以在城市地图中安排行动。单指拖动地图平移；双指捏合缩放，也可以拖动缩放条。"
                            : "城市地图可以查看地点、安排行动。单指拖动地图平移；双指捏合缩放，也可以拖动缩放条。";
                        hint = "单指拖动：平移地图\n双指捏合：放大 / 缩小\n点「下一步」学习查看点位";
                        break;
                    case Stage.SelectLocation:
                        description = "轻触一个地图点位，地图会移向该地点。有开放内容时会弹出行动卡；没有开放内容的地点只会聚焦。查看点位不扣资源。营业中执行其他地图行动时，咖啡店会自动打烊；只查看或返回咖啡馆不会打烊。";
                        hint = "轻触地图点位\n查看该地点的行动与需求\n只查看不会消耗资源";
                        break;
                    case Stage.Finish:
                        description = "不知道接下来做什么时，点击上方「待办事项」查看目标；点击「物品栏」核对物品，上方 HUD 可查看行动点。点击地图空白处可收起点位详情。有必须完成的行动时，先处理它；安排好后，回咖啡馆三楼宿舍点击床睡觉，才会进入下一天。";
                        hint = "点击上方入口查看待办 / 物品\n点击地图空白处收起行动卡\n安排好后回宿舍点击床";
                        break;
                }
            }

            panel.heading.text = CurrentStage == Stage.TodoGuidance ? "打烊后 · 下一步指引" : "城市地图 · 使用教程";
            panel.instruction.text = description;
            panel.errorText.text = CurrentStage == Stage.TodoGuidance
                ? "按 T 或点击待办查看事务 · 此提示可随时关闭，不消耗资源、不推进日期"
                : "教程只介绍操作，不自动执行行动、不扣资源、不推进日期";
            panel.gestureText.text = hint;
            panel.SetMouseMode(false, false);
            panel.gesture.gameObject.SetActive(!MobileTouchInput.IsAndroidPlayer);
            panel.retry.SetActive(CurrentStage != Stage.SelectLocation);
            panel.retryButton.GetComponentInChildren<TMPro.TMP_Text>().text = button;
            Vector2 pointer = MobileTouchInput.IsAndroidPlayer || Mouse.current == null
                ? new Vector2(Screen.width * .5f, Screen.height * .65f)
                : Mouse.current.position.ReadValue();
            float scale = panel.canvas.scaleFactor;
            Vector2 half = panel.gesture.rect.size * .5f + Vector2.one * 12;
            Vector2 badge = pointer + Vector2.down * Mathf.Max(panel.gestureOffset, half.y + 30) * scale;
            badge.x = Mathf.Clamp(badge.x, half.x * scale, Mathf.Max(half.x * scale, Screen.width - half.x * scale));
            badge.y = Mathf.Clamp(badge.y, half.y * scale, Mathf.Max(half.y * scale, Screen.height - half.y * scale));
            panel.gesture.position = badge;
            var location = CurrentStage == Stage.TodoGuidance ? GameplayHudPanel.Instance?.TasksButton : map.TutorialLocationButton;
            Vector2 target = location != null ? RectTransformUtility.WorldToScreenPoint(null, location.transform.position) : pointer;
            for (int i = 0; i < panel.trail.Length; i++)
            {
                var dot = panel.trail[i];
                dot.enabled = CurrentStage == Stage.SelectLocation || CurrentStage == Stage.TodoGuidance;
                float t = Mathf.Repeat(Time.unscaledTime * .65f - i * .035f, 1);
                dot.rectTransform.position = Vector2.Lerp(pointer, target, t);
                dot.color = panel.trailColor;
            }
        }

        public static bool DebugPlay(out string message)
        {
            var map = FindActiveMap();
            if (map != null && map.UsageTutorialAvailable)
            {
                var tutorial = map.GetComponent<MapUsageTutorial>() ?? map.gameObject.AddComponent<MapUsageTutorial>();
                bool started = tutorial.IsRunning || tutorial.Begin(map);
                message = started ? "正在播放地图使用教程；重复播放不会修改首日完成记录。" : "地图教程资源未就绪。";
                return started;
            }
            var flow = FindFirstObjectByType<GameFlowBootstrap>();
            if (flow == null || !flow.CanOpenMap || !flow.DebugForceOpenMap(out message))
            {
                message = "请先进入可自由操作的地图或游戏阶段；建角、咖啡馆首日教程和剧情期间不可播放地图教程。";
                return false;
            }
            flow.StartCoroutine(PlayWhenReady());
            message = "正在打开地图，随后播放使用教程；当前场景与日期保持不变。";
            return true;
        }

        private static MapSceneController FindActiveMap()
        {
            foreach (var candidate in FindObjectsByType<MapSceneController>(FindObjectsSortMode.None))
                if (candidate.isActiveAndEnabled && candidate.transform is RectTransform) return candidate;
            return null;
        }

        private static IEnumerator PlayWhenReady()
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var map = FindActiveMap();
                if (map != null && map.UsageTutorialAvailable)
                {
                    var tutorial = map.GetComponent<MapUsageTutorial>() ?? map.gameObject.AddComponent<MapUsageTutorial>();
                    if (!tutorial.IsRunning) tutorial.Begin(map);
                    yield break;
                }
                yield return null;
            }
            Debug.LogWarning("[MapTutorial] 地图未及时就绪，未启动教程。");
        }
    }
}
