using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>Heat-source animation only: consumes Processing/selfHeating or the stove's docked-vessel snapshot.</summary>
    [DefaultExecutionOrder(110), DisallowMultipleComponent, RequireComponent(typeof(FreeEntityView))]
    public sealed class FreeCafeHeatingVisual : MonoBehaviour
    {
        [Tooltip("Source sprite coordinates, origin at bottom left.")]
        public Vector2 burnerUV = new Vector2(.5f, .75f);
        public Vector2 flameSizeUV = new Vector2(.32f, .13f);
        public Vector2 glowSizeUV = new Vector2(.4f, .09f);
        [Range(1,12)] public int flameCount = 7;
        public int sortingOffset = 1;
        private FreeEntityView view;
        private CafeThermalLayer fire, glow;
        private float clock, visibility;
        public bool IsHeating { get; private set; }

        private void Awake() { view = GetComponent<FreeEntityView>(); }
        private void LateUpdate()
        {
            if (view == null || view.body == null || view.body.sprite == null || view.Runtime == null || view.Runtime.World == null) return;
            var world = view.Runtime.World;
            var entity = world.Find(view.EntityId);
            IsHeating = false;
            if (world.ProductionEnabled && entity != null)
            {
                if (entity.specId == "machine.heater")
                    IsHeating = entity.production.selfHeating && entity.production.phase == ProductionPhase.Processing;
                else if (entity.specId == "machine.stove")
                {
                    var vessel = world.Find(entity.production.heatingVesselId);
                    // A remembered docking ID must not keep an empty or abandoned stove lit.
                    IsHeating = vessel != null && world.IsContainer(vessel) && vessel.area == entity.area &&
                        !vessel.production.selfHeating && vessel.production.phase == ProductionPhase.Processing &&
                        Mathf.Abs(vessel.x - entity.x) <= .8f &&
                        Mathf.Abs(vessel.y - entity.y - world.Spec(entity.specId).height * .45f - world.Spec(vessel.specId).height * .5f) <= .7f;
                }
            }
            float dt = CafeThermalLayer.DeltaTime(view);
            visibility = Mathf.MoveTowards(visibility, IsHeating ? 1 : 0, dt * 7);
            if (visibility > .001f) clock += dt;
            if (fire == null && visibility > .001f)
            {
                var material = Resources.Load<Material>("FreeCafe/ThermalEffects");
                if (material == null) return;
                glow = new CafeThermalLayer(view.body.transform, "HeatingGlow", material, 0);
                fire = new CafeThermalLayer(view.body.transform, "HeatingFlames", material, 1);
            }
            if (fire == null) return;
            var size = view.body.sprite.bounds.size;
            var point = CafeThermalLayer.SpritePoint(view.body.sprite, burnerUV);
            fire.Transform.localPosition = point;
            fire.Transform.localScale = new Vector3(size.x * flameSizeUV.x, size.y * flameSizeUV.y, 1);
            glow.Transform.localPosition = point - Vector3.up * (size.y * glowSizeUV.y * .4f);
            glow.Transform.localScale = new Vector3(size.x * glowSizeUV.x, size.y * glowSizeUV.y, 1);
            glow.Present(view.body, sortingOffset, visibility, clock);
            fire.Present(view.body, sortingOffset, visibility, clock, flameCount);
        }
        private void OnDisable()
        {
            IsHeating = false; visibility = clock = 0;
            if (fire != null) fire.Hide();
            if (glow != null) glow.Hide();
        }
        private void OnDestroy()
        {
            if (fire != null) fire.Dispose();
            if (glow != null) glow.Dispose();
        }
    }
}
