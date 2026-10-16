using UnityEngine;
using UnityEngine.UI;
using Spotlight.GameFlow;

namespace Spotlight.FreeCafe
{
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(GravityLiquidImage))]
    public sealed class OrthographicGlassCupPresenter : MonoBehaviour, ICafeLiquidPresenter
    {
        [SerializeField] private Vector2 smallSize = new Vector2(0.8f, 1f);
        [SerializeField] private Vector2 largeSize = new Vector2(1f, 1.2f);
        [SerializeField, Min(0.1f)] private float fillResponse = 12f;
        [SerializeField] private Color coffee = new Color(0.22f, 0.075f, 0.025f, 0.94f);
        [SerializeField] private Color milk = new Color(0.96f, 0.92f, 0.82f, 0.94f);
        [SerializeField] private Color water = new Color(0.62f, 0.85f, 0.92f, 0.36f);
        [SerializeField] private Color matcha = new Color(0.34f, 0.48f, 0.12f, 0.94f);
        [SerializeField] private Color cocoa = new Color(0.32f, 0.14f, 0.06f, 0.94f);
        [SerializeField] private Color glass = new Color(0.72f, 0.92f, 1f, 0.82f);

        [SerializeField] private Color silhouetteOutline = new Color(0.16f, 0.11f, 0.07f, 0.65f);

        [Header("Color weights (volume × weight; do not affect inventory)")]
        [SerializeField, Min(0.01f)] private float coffeeColorWeight = 3f;
        [SerializeField, Min(0.01f)] private float milkColorWeight = 1f;
        [SerializeField, Min(0.01f)] private float milkFoamColorWeight = 0.1f;
        [SerializeField, Min(0.01f)] private float waterColorWeight = 1f;
        [SerializeField, Min(0.01f)] private float matchaColorWeight = 1f;
        [SerializeField, Min(0.01f)] private float cocoaColorWeight = 1f;
        [SerializeField, Min(0.01f)] private float otherColorWeight = 1f;

        private Image image;
        private GravityLiquidImage liquid;
        private RectTransform rect;
        private bool configured;
        private bool hasSnapshot;
        private float targetFill, previousVolume;

        public Color MixedColor { get; private set; }
        public Color LiquidSurfaceColor { get; private set; }
        public float TargetFill => targetFill;

        private void Awake() { Ensure(); }

        private void OnEnable()
        {
            Ensure();
            hasSnapshot = false;
            // Hiding a parent/workstation is presentation-only. Restore the last
            // authoritative fill instead of flashing an empty glass until Refresh.
            liquid.FillAmount = targetFill;
            liquid.ResetMotion();
            liquid.RefreshVisual();
        }

        private void Ensure()
        {
            if (configured) return;
            rect = GetComponent<RectTransform>();
            image = GetComponent<Image>();
            liquid = GetComponent<GravityLiquidImage>();
            image.raycastTarget = false;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.sprite = null;
            image.preserveAspect = false;
            liquid.AllowSpilling = false;
            liquid.MotionInfluence = 0.7f;
            liquid.Gravity = new Vector2(0f, -9.81f);
            SetColor(coffee);
            configured = true;
        }

        public void Present(CafeLiquidSnapshot snapshot, float tilt, bool pouring)
        {
            Ensure();
            float fraction = snapshot.capacityMl > 0f ? Mathf.Clamp01(snapshot.volumeMl / snapshot.capacityMl) : 0f;
            SetColor(ResolveColor(snapshot));
            targetFill = fraction;
            if (!hasSnapshot || !Application.isPlaying)
            {
                liquid.FillAmount = fraction;
                liquid.ResetMotion();
            }
            else
            {
                float received = snapshot.volumeMl - previousVolume;
                if (received > 0.00001f)
                    liquid.AddPourImpulse(tilt, Mathf.Clamp01(0.3f + received / Mathf.Max(1f, snapshot.capacityMl) * 16f));
                else if (pouring && received < -0.00001f)
                    liquid.AddPourImpulse(tilt, Mathf.Clamp01(-received / Mathf.Max(1f, snapshot.capacityMl) * 16f));
            }
            if (fraction <= 0f) liquid.FillAmount = 0f;
            previousVolume = snapshot.volumeMl;
            hasSnapshot = true;
            liquid.RefreshVisual();
        }

        public void ConfigureSize(bool large)
        {
            Ensure();
            rect.sizeDelta = large ? largeSize : smallSize;
            liquid.RefreshVisual();
        }

        public void ConfigureSize(Vector2 size)
        {
            Ensure();
            rect.sizeDelta = size;
            liquid.RefreshVisual();
        }

        public void ConfigureContainerMask(Texture2D mask, bool signedDistance, float distanceRange, float inset)
        {
            Ensure();
            bool changed = liquid.ContainerMask != mask;
            liquid.ConfigureContainerMask(mask, signedDistance, distanceRange, inset);
            if (changed) SetColor(MixedColor);
        }

        public void FitSprite(Sprite sprite)
        {
            if (sprite == null) return;
            Ensure();
            Vector2 size = sprite.bounds.size;
            Vector2 pivot = sprite.pivot / sprite.rect.size;
            bool changed = rect.sizeDelta != size || rect.pivot != pivot;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition3D = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            if (changed) liquid.RefreshVisual();
        }

        public void SetTint(Color tint)
        {
            Ensure();
            image.color = tint;
        }

        public void ConfigureMotionSpace(Transform space)
        {
            Ensure();
            liquid.MotionSpace = space;
        }

        private Color ResolveColor(CafeLiquidSnapshot snapshot)
        {
            var composition = snapshot.composition;
            if (composition.TotalMl > 0f)
            {
                float coffeeAmount = composition.coffeeMl * Mathf.Max(.01f, coffeeColorWeight);
                float milkAmount = (composition.milkMl - composition.milkFoamMl) * Mathf.Max(.01f, milkColorWeight)
                    + composition.milkFoamMl * Mathf.Max(.01f, milkFoamColorWeight);
                float waterAmount = composition.waterMl * Mathf.Max(.01f, waterColorWeight);
                float matchaAmount = composition.matchaMl * Mathf.Max(.01f, matchaColorWeight);
                float cocoaAmount = composition.cocoaMl * Mathf.Max(.01f, cocoaColorWeight);
                float otherAmount = composition.otherMl * Mathf.Max(.01f, otherColorWeight);
                float total = coffeeAmount + milkAmount + waterAmount + matchaAmount + cocoaAmount + otherAmount;
                return (coffee * coffeeAmount + milk * milkAmount + water * waterAmount
                    + matcha * matchaAmount + cocoa * cocoaAmount + water * otherAmount) / total;
            }
            switch (snapshot.primaryIngredient)
            {
                case "coffee": return coffee;
                case "milk": case "hotmilk": case "milkfoam": case "cream": return milk;
                case "matcha.drink": return matcha;
                case "cocoa.drink": return cocoa;
                default: return water;
            }
        }

        private void SetColor(Color color)
        {
            MixedColor = color;
            Color deep = Color.Lerp(color, Color.black, 0.35f);
            deep.a = color.a;
            Color surface = Color.Lerp(color, Color.white, 0.45f);
            surface.a = color.a;
            LiquidSurfaceColor = surface;
            liquid.ConfigureAppearance(color, deep, surface, liquid.ContainerMask != null ? silhouetteOutline : glass);
        }

        private void Update()
        {
            if (!Application.isPlaying || !hasSnapshot) return;
            float blend = 1f - Mathf.Exp(-fillResponse * Time.unscaledDeltaTime);
            float fill = Mathf.Lerp(liquid.CurrentFill, targetFill, blend);
            liquid.FillAmount = Mathf.Abs(fill - targetFill) < 0.0001f ? targetFill : fill;
        }
    }
}
