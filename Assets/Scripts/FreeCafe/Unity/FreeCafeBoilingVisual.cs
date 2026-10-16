using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>Boiling presentation only; independent from the source's heating/fire animation.</summary>
    [DefaultExecutionOrder(110), DisallowMultipleComponent, RequireComponent(typeof(FreeEntityView))]
    public sealed class FreeCafeBoilingVisual : MonoBehaviour
    {
        [Tooltip("Steam outlet in source sprite coordinates, origin at bottom left.")]
        public Vector2 steamOriginUV = new Vector2(.5f, .85f);
        public Vector2 steamSizeUV = new Vector2(.3f, .24f);
        [Range(0,.01f)] public float vibrationHeight = .002f;
        private FreeEntityView view;
        private CafeThermalLayer steam;
        private Vector3 appliedOffset;
        private float clock, visibility;
        public bool IsBoiling { get; private set; }

        private void Awake() { view = GetComponent<FreeEntityView>(); }
        private void Update()
        {
            if (view == null || view.body == null || view.body.sprite == null || view.Runtime == null || view.Runtime.World == null) return;
            var world = view.Runtime.World;
            var entity = world.Find(view.EntityId);
            IsBoiling = world.ProductionEnabled && entity != null && entity.production.phase == ProductionPhase.Boiling && world.LiquidVolume(entity) > .001f;
            float dt = CafeThermalLayer.DeltaTime(view);
            visibility = Mathf.MoveTowards(visibility, IsBoiling ? 1 : 0, dt * 5);
            if (visibility > .001f) clock += dt;
            float height = view.body.sprite.bounds.size.y * Mathf.Abs(view.body.transform.localScale.y);
            var offset = new Vector3(Mathf.Sin(clock * 29) * .18f, Mathf.Sin(clock * 37), 0) * (height * vibrationHeight * visibility);
            view.body.transform.localPosition += offset - appliedOffset;
            appliedOffset = offset;
        }
        private void LateUpdate()
        {
            if (view == null || view.body == null || view.body.sprite == null) return;
            if (steam == null && visibility > .001f)
            {
                var material = Resources.Load<Material>("FreeCafe/ThermalEffects");
                if (material == null) return;
                steam = new CafeThermalLayer(transform, "BoilingSteam", material, 2);
            }
            if (steam == null) return;
            steam.Transform.position = view.body.transform.TransformPoint(CafeThermalLayer.SpritePoint(view.body.sprite, steamOriginUV));
            // The outlet follows a tilted vessel; steam continues to rise in world space.
            steam.Transform.rotation = Quaternion.identity;
            var spriteSize = view.body.sprite.bounds.size;
            var bodyScale = view.body.transform.lossyScale;
            var parentScale = transform.lossyScale;
            steam.Transform.localScale = new Vector3(
                spriteSize.x * Mathf.Abs(bodyScale.x) * steamSizeUV.x / Mathf.Max(Mathf.Abs(parentScale.x), .0001f),
                spriteSize.y * Mathf.Abs(bodyScale.y) * steamSizeUV.y / Mathf.Max(Mathf.Abs(parentScale.y), .0001f), 1);
            steam.Present(view.body, 3, visibility, clock);
        }
        private void OnDisable()
        {
            if (view != null && view.body != null) view.body.transform.localPosition -= appliedOffset;
            appliedOffset = Vector3.zero;
            IsBoiling = false; visibility = clock = 0;
            if (steam != null) steam.Hide();
        }
        private void OnDestroy() { if (steam != null) steam.Dispose(); }
    }
}
