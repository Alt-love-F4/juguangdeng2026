using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.Gameplay
{
    /// <summary>
    /// 场景交互类型：定义世界物体的交互行为
    /// </summary>
    public enum InteractableType
    {
        None,
        CupSmall,             // 小杯
        CupRegular,           // 大杯
        MachineGrinder,       // 咖啡研磨器
        MachineMatchaGrinder, // 抹茶研磨器
        MachineEspresso,      // 摩卡壶/意式机
        MachineMilkPot,       // 牛奶壶
        MachineCocoaPot,      // 可可壶
        IngCoffeeBeans,       // 咖啡豆
        IngMilk,              // 牛奶
        IngMatcha,            // 抹茶粉
        IngChocolate,         // 巧克力
        IngHoney,             // 蜂蜜
        IngCream,             // 奶油
        ServeWindow,          // 出餐口
    }

    /// <summary>
    /// 世界物体交互组件：支持点击与拖拽。
    /// 点击 = 直接触发当前步骤对应交互；
    /// 拖拽组合 = 原料→杯子加料 / 杯子→机器触发机器步骤 / 杯子→出餐口上餐。
    /// 需要 EventSystem + 相机上的 Physics2DRaycaster。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class WorldInteractable : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public InteractableType interactType;

        [Header("视觉反馈")]
        public float hoverScale = 1.1f;
        public float clickScale = 0.95f;
        public float dragScale = 1.15f;

        [Header("拖动")]
        public bool draggable = true;
        [Tooltip("拖动结束后弹回原位（原料/杯子通常弹回，因为只是手势而非真正搬移）")]
        public bool returnToOriginAfterDrag = true;

        private Vector3 baseScale;
        private Vector3 dragOriginPos;
        private Vector3 dragOffset;
        private bool isDragging;
        private Camera cam;
        private CoffeeGameplayController ctrl;

        private void Start()
        {
            baseScale = transform.localScale;
            ctrl = CoffeeGameplayController.Instance;
        }

        // ---------- 悬停 ----------

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isDragging || !IsInteractable()) return;
            transform.localScale = baseScale * hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isDragging) return;
            transform.localScale = baseScale;
        }

        // ---------- 点击 ----------

        public void OnPointerClick(PointerEventData eventData)
        {
            if (isDragging || !IsInteractable()) return;
            StopAllCoroutines();
            StartCoroutine(ClickPulse());
            ExecuteInteraction();
        }

        private IEnumerator ClickPulse()
        {
            transform.localScale = baseScale * clickScale;
            yield return new WaitForSeconds(0.08f);
            transform.localScale = baseScale * hoverScale;
            yield return new WaitForSeconds(0.08f);
            transform.localScale = baseScale;
        }

        // ---------- 拖拽 ----------

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!draggable || !IsInteractable()) return;
            isDragging = true;
            dragOriginPos = transform.position;
            cam = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
            dragOffset = transform.position - ScreenToWorld(eventData.position);
            transform.localScale = baseScale * dragScale;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            transform.position = ScreenToWorld(eventData.position) + dragOffset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            isDragging = false;

            // 检测落点（基于碰撞体重叠，比 OverlapPoint 宽松一些）
            var col = GetComponent<Collider2D>();
            var filter = ContactFilter2D.noFilter;
            var results = new System.Collections.Generic.List<Collider2D>();
            int hitCount = col != null ? Physics2D.OverlapCollider(col, filter, results) : 0;

            WorldInteractable dropTarget = null;
            for (int i = 0; i < hitCount; i++)
            {
                var hit = results[i];
                if (hit == null || hit.gameObject == gameObject) continue;
                var target = hit.GetComponent<WorldInteractable>();
                if (target != null && target != this)
                {
                    dropTarget = target;
                    break;
                }
            }

            bool handled = false;
            if (dropTarget != null)
                handled = TryExecuteCombo(dropTarget);

            if (returnToOriginAfterDrag)
                transform.position = dragOriginPos;
            transform.localScale = baseScale;

            if (!handled && IsInteractable())
                ExecuteInteraction(); // 拖到空地 → 退化为点击
        }

        // ---------- 组合交互 ----------

        /// <summary>根据"被拖物体 + 落点目标"的组合决定是否触发哪一方的交互。</summary>
        private bool TryExecuteCombo(WorldInteractable target)
        {
            // 杯子拖到出餐口 → 上餐
            if (IsCup(interactType) && target.interactType == InteractableType.ServeWindow)
            {
                if (target.IsInteractable()) { target.ExecuteInteraction(); return true; }
                return false;
            }

            // 杯子拖到机器 → 触发该机器当前步骤
            if (IsCup(interactType) && IsMachine(target.interactType))
            {
                if (target.IsInteractable()) { target.ExecuteInteraction(); return true; }
                return false;
            }

            // 原料拖到杯子 → 加原料
            if (IsIngredient(interactType) && IsCup(target.interactType))
            {
                if (IsInteractable()) { ExecuteInteraction(); return true; }
                return false;
            }

            // 原料拖到对应机器（如牛奶拖到牛奶壶） → 触发机器
            if (IsIngredient(interactType) && IsMachine(target.interactType))
            {
                if (target.IsInteractable()) { target.ExecuteInteraction(); return true; }
                return false;
            }

            return false;
        }

        private static bool IsIngredient(InteractableType t) => t switch
        {
            InteractableType.IngCoffeeBeans or InteractableType.IngMilk or InteractableType.IngMatcha
                or InteractableType.IngChocolate or InteractableType.IngHoney or InteractableType.IngCream => true,
            _ => false
        };

        private static bool IsCup(InteractableType t) => t switch
        {
            InteractableType.CupSmall or InteractableType.CupRegular => true,
            _ => false
        };

        private static bool IsMachine(InteractableType t) => t switch
        {
            InteractableType.MachineGrinder or InteractableType.MachineMatchaGrinder
                or InteractableType.MachineEspresso or InteractableType.MachineMilkPot
                or InteractableType.MachineCocoaPot => true,
            _ => false
        };

        // ---------- 内部 ----------

        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            if (cam == null) cam = Camera.main;
            Vector3 p = new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z + transform.position.z);
            Vector3 w = cam.ScreenToWorldPoint(p);
            w.z = transform.position.z;
            return w;
        }

        private bool IsInteractable()
        {
            if (ctrl == null) ctrl = CoffeeGameplayController.Instance;
            if (ctrl == null) return false;

            var step = ctrl.Session?.CurrentStep;
            if (step == null) return false;

            return interactType switch
            {
                InteractableType.CupSmall or InteractableType.CupRegular
                    => step.Kind == RecipeStepKind.SelectCup,
                InteractableType.MachineGrinder
                    => step.Kind == RecipeStepKind.GrindClick,
                InteractableType.MachineMatchaGrinder
                    => step.Kind == RecipeStepKind.MatchaGrind,
                InteractableType.MachineEspresso
                    => step.Kind == RecipeStepKind.MokaPot,
                InteractableType.MachineMilkPot
                    => step.Kind == RecipeStepKind.AddMilk,
                InteractableType.MachineCocoaPot
                    => step.Kind == RecipeStepKind.CocoaMinigame,
                InteractableType.IngCoffeeBeans
                    => step.Kind == RecipeStepKind.AddIngredient && step.IngredientId == "coffee_bean",
                InteractableType.IngMilk
                    => step.Kind == RecipeStepKind.AddMilk,
                InteractableType.IngMatcha
                    => step.Kind == RecipeStepKind.AddIngredient && step.IngredientId == "matcha",
                InteractableType.IngChocolate
                    => step.Kind == RecipeStepKind.AddIngredient && step.IngredientId == "chocolate",
                InteractableType.IngHoney
                    => step.Kind == RecipeStepKind.AddHoney,
                InteractableType.ServeWindow
                    => step.Kind == RecipeStepKind.Serve,
                _ => false
            };
        }

        private void ExecuteInteraction()
        {
            if (ctrl == null) return;

            switch (interactType)
            {
                case InteractableType.CupSmall:
                case InteractableType.CupRegular:
                    ctrl.SelectCup();
                    break;
                case InteractableType.MachineGrinder:
                    ctrl.ClickGrinder();
                    break;
                case InteractableType.MachineMatchaGrinder:
                    ctrl.ClickGrinder(); // 抹茶研磨也走研磨逻辑
                    break;
                case InteractableType.IngCoffeeBeans:
                    ctrl.AddIngredient("coffee_bean");
                    break;
                case InteractableType.IngMilk:
                    ctrl.AddIngredient("milk");
                    break;
                case InteractableType.IngMatcha:
                    ctrl.AddIngredient("matcha");
                    break;
                case InteractableType.IngChocolate:
                    ctrl.AddIngredient("chocolate");
                    break;
                case InteractableType.IngHoney:
                    ctrl.AddIngredient("honey");
                    break;
                case InteractableType.ServeWindow:
                    ctrl.Serve();
                    break;
            }
        }
    }
}
