using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>
    /// 在 2D 杯子的杯口椭圆上贴识别出的饮品液面。只做展示：识别结果由 CafeWorld.ClassifyCoffee 决定，
    /// 不改库存、评分或存档。倾倒 / 接液时让位给 GravityLiquidImage 的实时液体。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FreeCafeCupDrinkSurface : MonoBehaviour
    {
        public const string SurfaceName = "DrinkSurface";

        private FreeEntityView view;
        private SpriteRenderer surface;
        private CafeDrinkSurfaceLibrary library;
        private CafeDrinkSurfaceLibrary.CupMouth mouth;
        private Sprite target;
        private float alpha;
        private bool suppressed;

        /// <summary>The surface currently shown (or fading in), null when the cup shows its own art.</summary>
        public Sprite Current => target;
        public string ProductId { get; private set; }
        public bool Visible => surface != null && surface.enabled && surface.sprite != null && surface.color.a > .001f;
        public SpriteRenderer Renderer => surface;

        public void Bind(FreeEntityView owner, CafeDrinkSurfaceLibrary source)
        {
            view = owner;
            library = source;
        }

        /// <summary>Shows <paramref name="productId"/>'s surface on the cup; null/unknown clears it.</summary>
        public void Present(string cupSpecId, string productId, bool suppress)
        {
            if (library == null) library = CafeDrinkSurfaceLibrary.Default;
            mouth = library != null ? library.Cup(cupSpecId) : null;
            Sprite sprite = mouth != null ? library.Surface(productId) : null;
            ProductId = sprite != null ? productId : null;
            suppressed = suppress;
            if (sprite != null && sprite != target)
            {
                // Switching drink: start the new surface from the current opacity so it crossfades, not pops.
                EnsureRenderer();
                surface.sprite = sprite;
            }
            target = sprite;
            if (!Application.isPlaying) { alpha = target != null && !suppress ? 1f : 0f; Apply(); }
        }

        private void EnsureRenderer()
        {
            if (surface != null || view == null || view.body == null) return;
            var existing = view.body.transform.Find(SurfaceName);
            surface = existing != null ? existing.GetComponent<SpriteRenderer>() : null;
            if (surface == null)
            {
                var go = new GameObject(SurfaceName, typeof(SpriteRenderer));
                go.transform.SetParent(view.body.transform, false);
                surface = go.GetComponent<SpriteRenderer>();
            }
            surface.enabled = false;
        }

        private void LateUpdate()
        {
            if (surface == null && target == null) return;
            bool show = target != null && !suppressed && view != null && !view.LiquidVisualActive;
            float fade = library != null ? library.fadeSeconds : .18f;
            float step = fade <= 0f ? 1f : Time.unscaledDeltaTime / fade;
            alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, step);
            Apply();
        }

        private void Apply()
        {
            EnsureRenderer();
            if (surface == null || view == null || library == null) return;
            var body = view.body;
            bool visible = alpha > .001f && surface.sprite != null && mouth != null && body != null && body.enabled && body.sprite != null;
            surface.enabled = visible;
            if (!visible) return;

            // Cup mouth ellipse in the body's local (sprite) space.
            Bounds cup = body.sprite.bounds;
            Vector2 cupSize = cup.size;
            float u = body.flipX ? 1f - mouth.center.x : mouth.center.x;
            float v = body.flipY ? 1f - mouth.center.y : mouth.center.y;
            Vector2 cupCenter = (Vector2)cup.min + new Vector2(u * cupSize.x, v * cupSize.y);
            Vector2 cupRadius = new Vector2(mouth.radius.x * cupSize.x, mouth.radius.y * cupSize.y) * mouth.inset;

            // Matching ellipse inside the surface sprite (all surfaces share one frame).
            Bounds art = surface.sprite.bounds;
            Vector2 artSize = art.size;
            Vector2 artCenter = (Vector2)art.min + Vector2.Scale(library.surfaceCenter, artSize);
            Vector2 artRadius = Vector2.Scale(library.surfaceRadius, artSize);
            var scale = new Vector3(cupRadius.x / Mathf.Max(1e-5f, artRadius.x), cupRadius.y / Mathf.Max(1e-5f, artRadius.y), 1f);

            var t = surface.transform;
            t.localRotation = Quaternion.identity;
            t.localScale = scale;
            t.localPosition = new Vector3(cupCenter.x - artCenter.x * scale.x, cupCenter.y - artCenter.y * scale.y, -.001f);
            surface.flipX = false;
            surface.flipY = body.flipY;
            surface.sortingLayerID = body.sortingLayerID;
            surface.sortingOrder = body.sortingOrder + 1;
            Color tint = body.color;
            tint.a *= alpha;
            surface.color = tint;
        }

        private void OnDisable()
        {
            alpha = 0f;
            if (surface != null) surface.enabled = false;
        }
    }
}
