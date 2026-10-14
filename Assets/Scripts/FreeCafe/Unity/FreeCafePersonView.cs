using TMPro;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>人物的视图结构，不持有第二份人物/订单状态，不调用 NCS。动画/对话 Presenter 可接入锚点。</summary>
    [RequireComponent(typeof(FreeEntityView))]
    public sealed class FreeCafePersonView : MonoBehaviour
    {
        public FreeEntityView entityView;
        public Transform visualRoot, portraitAnchor, speechAnchor, orderAnchor;
        public TMP_Text orderCaption;
        public CafePersonIdentity Identity => entityView != null && entityView.Runtime != null
            ? entityView.Runtime.World.Find(entityView.EntityId)?.person : null;

        public void Present(WorldEntity entity)
        {
            var world = entityView.Runtime.World;
            entityView.label.text = world.PersonName(entity);
            CustomerRequest active = null;
            foreach (var request in world.State.requests)
                if (request.customerId == entity.id) { active = request; break; }
            orderCaption.gameObject.SetActive(active != null);
            if (active != null)
                orderCaption.text = "想要 " + FreeCafeProductionText.Recipe(world.Product(active.productId).label) + "\n" + active.patience.ToString("0") + " 秒 · 点击查看";
            orderCaption.GetComponent<MeshRenderer>().sortingOrder = entityView.body.sortingOrder + 2;
        }
    }
}
