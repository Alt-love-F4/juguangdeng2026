using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>Animates the authored grinder parts from the existing production snapshot.</summary>
    [DefaultExecutionOrder(110)]
    [RequireComponent(typeof(FreeEntityView))]
    public sealed class FreeCafeGrinderVisual : MonoBehaviour
    {
        public SpriteRenderer crankArm, crankKnob, closedDrawer, filledDrawer;
        public Vector3 crankPivot = new Vector3(-.545f, 2.03f, 0);
        public Vector2 crankReach = new Vector2(2.39f, .32f);
        public Vector3 drawerOrigin = new Vector3(-.515f, -2.575f, 0);
        public Vector3 drawerTravel = new Vector3(0, -.38f, 0);
        private FreeEntityView view;
        private float angle, opening;
        public float CrankAngle => angle;
        public float DrawerOpenAmount => opening;
        public bool DrawerReserved { get; private set; }
        public Vector3 DrawerWorldPosition => filledDrawer != null ? filledDrawer.transform.position : transform.position;

        private void Awake() { view = GetComponent<FreeEntityView>(); }

        private void LateUpdate()
        {
            if (view == null || view.Runtime == null || view.Runtime.World == null) return;
            var entity = view.Runtime.World.Find(view.EntityId);
            if (entity == null || entity.specId != "machine.grinder") return;
            bool ready = entity.production.phase == ProductionPhase.Ready;
            bool grinding = entity.production.phase == ProductionPhase.Processing;
            DrawerReserved = ready && view.Runtime.IsDraggingOutputFrom(entity.id);
            float dt = Time.timeScale > 0 ? Mathf.Min(Time.deltaTime, .05f) : 0;
            if (grinding)
                angle = Mathf.Repeat(angle + (entity.production.clickCooldown > 0 ? 440 : 240) * dt, 360);
            else angle = Mathf.MoveTowardsAngle(angle, 0, 360 * dt);
            opening = Mathf.MoveTowards(opening, ready ? 1 : 0, dt * 3.6f);
            Present(ready);
        }

        private void Present(bool ready)
        {
            if (crankArm == null || crankKnob == null || closedDrawer == null || filledDrawer == null || view == null || view.body == null) return;
            // Project a horizontal circular turn into the shallow ellipse of the supplied front view.
            // The wooden grip stays upright instead of rotating like a clock hand.
            float radians = angle * Mathf.Deg2Rad;
            var reach = new Vector2(crankReach.x * Mathf.Cos(radians), crankReach.y + .58f * Mathf.Sin(radians));
            float turn = (Mathf.Atan2(reach.y, reach.x) - Mathf.Atan2(crankReach.y, crankReach.x)) * Mathf.Rad2Deg;
            crankArm.transform.localPosition = crankPivot;
            crankArm.transform.localRotation = Quaternion.Euler(0, 0, turn);
            crankArm.transform.localScale = Vector3.one * (reach.magnitude / crankReach.magnitude);
            crankKnob.transform.localPosition = crankPivot + new Vector3(reach.x, reach.y, 0);
            crankKnob.transform.localRotation = Quaternion.identity;
            var drawerPosition = drawerOrigin + drawerTravel * Mathf.SmoothStep(0, 1, opening);
            closedDrawer.transform.localPosition = filledDrawer.transform.localPosition = drawerPosition;
            closedDrawer.enabled = !ready;
            filledDrawer.enabled = ready && opening > .001f && !DrawerReserved;
            int order = view.body.sortingOrder;
            SetStyle(crankArm, order + 2); SetStyle(crankKnob, order + 3);
            SetStyle(closedDrawer, order + 1); SetStyle(filledDrawer, order + 1);
        }

        private void SetStyle(SpriteRenderer part, int order)
        {
            part.sortingLayerID = view.body.sortingLayerID;
            part.sortingOrder = order;
            part.color = view.body.color;
        }

        private void OnDisable()
        {
            angle = opening = 0; DrawerReserved = false;
            if (view == null) view = GetComponent<FreeEntityView>();
            Present(false);
        }
    }
}
