using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Gravity-aligned liquid in a container silhouette, with an adjustable inward outline.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Image))]
    [AddComponentMenu("Spotlight/UI/Gravity Liquid Image")]
    public sealed class GravityLiquidImage : BaseMeshEffect
    {
        public const string ShaderName = "Spotlight/UI/Gravity Liquid";
        public const string MaterialResourcePath = "UI/GravityLiquidUI";

        [Header("Liquid")]
        [SerializeField, Range(0f, 1f)] private float fillAmount = 0.6f;
        [SerializeField] private Color liquidColor = new Color(0.12f, 0.65f, 0.9f, 0.9f);
        [SerializeField] private Color deepColor = new Color(0.03f, 0.25f, 0.55f, 0.98f);
        [SerializeField] private Color surfaceColor = new Color(0.65f, 0.96f, 1f, 1f);
        [SerializeField, Range(0.001f, 0.05f)] private float surfaceWidth = 0.008f;
        [Tooltip("Sprite alpha always clips the result. Set this above zero to also use its RGB.")]
        [SerializeField, Range(0f, 1f)] private float textureInfluence;

        [Header("Container silhouette (optional)")]
        [Tooltip("Alpha defines the vessel cavity, excluding handles/stands. Enable Read/Write for area-correct fill.")]
        [SerializeField] private Texture2D containerMask;
        [Tooltip("Generated masks store signed distance in R; turn off for a regular alpha mask.")]
        [SerializeField] private bool maskIsDistanceField;
        [SerializeField, Min(0.001f)] private float maskDistanceRange = 0.125f;

        [Header("Container outline / fallback three walls")]
        [Tooltip("Inward distance relative to Image height. Follows mask edges; unmasked Images use three walls.")]
        [SerializeField, Range(0f, 0.15f)] private float wallThickness = 0.02f;
        [SerializeField] private Color wallColor = new Color(0.7f, 0.9f, 1f, 0.65f);
        [Tooltip("Runtime only: lose liquid when the flat surface exceeds the lower mouth corner. No droplets simulation.")]
        [SerializeField] private bool allowSpilling = true;
        [SerializeField, Min(0f)] private float spillRate = 0.8f;

        [Header("Gravity and movement")]
        [Tooltip("Gravity in the world XY plane. For Screen Space Overlay, X/Y are screen right/up.")]
        [SerializeField] private Vector2 gravity = new Vector2(0f, -9.81f);
        [Tooltip("UI usually moves in pixels: this scales world-space acceleration into gravity units.")]
        [SerializeField, Min(0f)] private float motionInfluence = 0.003f;
        [SerializeField] private Transform motionSpace;
        [SerializeField, Range(0f, 1f)] private float rotationInfluence = 0.18f;
        [SerializeField, Min(0.1f)] private float springFrequency = 2.2f;
        [SerializeField, Range(0.05f, 2f)] private float dampingRatio = 0.45f;
        [SerializeField, Range(0f, 75f)] private float maxSloshAngle = 35f;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Surface waves")]
        [SerializeField, Range(0f, 0.08f)] private float maxWaveAmplitude = 0.025f;
        [Tooltip("Zero means the surface becomes completely still after settling.")]
        [SerializeField, Range(0f, 0.01f)] private float idleWaveAmplitude;
        [SerializeField, Range(0.5f, 6f)] private float waveCycles = 2f;
        [SerializeField, Min(0f)] private float waveSpeed = 3f;

        private Image image;
        private Material ownedMaterial;
        private Material previousMaterial;
        private float currentFill, lastConfiguredFill;
        private float surfaceAngle, surfaceVelocity, waveEnergy, phase;
        private Vector2 previousPosition, previousVelocity;
        private float previousRotation;
        private bool missingMaterialReported;
        private readonly LiquidMaskGeometry maskGeometry = new LiquidMaskGeometry();

        private static readonly int RectId = Shader.PropertyToID("_ContainerRect");
        private static readonly int BoundsId = Shader.PropertyToID("_LiquidBounds");
        private static readonly int PlaneId = Shader.PropertyToID("_SurfacePlane");
        private static readonly int WaveId = Shader.PropertyToID("_Wave");
        private static readonly int WallId = Shader.PropertyToID("_WallThickness");
        private static readonly int LiquidId = Shader.PropertyToID("_LiquidColor");
        private static readonly int DeepId = Shader.PropertyToID("_DeepColor");
        private static readonly int SurfaceId = Shader.PropertyToID("_SurfaceColor");
        private static readonly int WallColorId = Shader.PropertyToID("_WallColor");
        private static readonly int TextureId = Shader.PropertyToID("_TextureInfluence");

        private static readonly int MaskId = Shader.PropertyToID("_ContainerMask");
        private static readonly int UseMaskId = Shader.PropertyToID("_UseContainerMask");
        private static readonly int MaskSdfId = Shader.PropertyToID("_MaskIsSDF");
        private static readonly int MaskRangeId = Shader.PropertyToID("_MaskDistanceRange");

        public Texture2D ContainerMask => containerMask;
        public float WallThickness => wallThickness;
        public void ConfigureContainerMask(Texture2D mask, bool signedDistance, float distanceRange, float inset)
        {
            inset = Mathf.Clamp(inset, 0f, 0.15f);
            distanceRange = Mathf.Max(0.001f, distanceRange);
            if (containerMask == mask && maskIsDistanceField == signedDistance &&
                Mathf.Approximately(maskDistanceRange, distanceRange) && Mathf.Approximately(wallThickness, inset)) return;
            containerMask = mask;
            maskIsDistanceField = signedDistance;
            maskDistanceRange = distanceRange;
            wallThickness = inset;
            if (isActiveAndEnabled) RefreshVisual();
        }

        public float FillAmount
        {
            get => fillAmount;
            set { fillAmount = Mathf.Clamp01(value); currentFill = lastConfiguredFill = fillAmount; }
        }
        public float CurrentFill => currentFill;
        public void ConfigureAppearance(Color liquid, Color deep, Color surface, Color wall)
        {
            liquidColor = liquid;
            deepColor = deep;
            surfaceColor = surface;
            wallColor = wall;
            if (isActiveAndEnabled) RefreshVisual();
        }
        public void AddPourImpulse(float tilt, float strength = 1f)
        {
            strength = Mathf.Clamp01(strength);
            AddImpulse(Mathf.Clamp(tilt, -1f, 1f) * strength * 4f);
            waveEnergy = Mathf.Max(waveEnergy, strength);
        }
        public bool AllowSpilling { get => allowSpilling; set => allowSpilling = value; }
        public float MotionInfluence { get => motionInfluence; set => motionInfluence = Mathf.Max(0f, value); }
        public Transform MotionSpace
        {
            get => motionSpace;
            set
            {
                if (motionSpace == value) return;
                motionSpace = value;
                ResetMotion();
            }
        }
        public Vector2 Gravity { get => gravity; set => gravity = value; }
        public Vector2 SurfaceNormal { get; private set; } = Vector2.up;
        public float SurfaceOffset { get; private set; }
        public Rect InnerBounds { get; private set; }

        protected override void OnEnable()
        {
            base.OnEnable();
            image = GetComponent<Image>();
            EnsureVertexChannel();
            previousMaterial = image.material;
            currentFill = lastConfiguredFill = Mathf.Clamp01(fillAmount);
            ResetMotion();
            RefreshVisual();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (image != null && image.material == ownedMaterial) image.material = previousMaterial;
            if (ownedMaterial != null)
            {
                if (Application.isPlaying) Destroy(ownedMaterial);
                else DestroyImmediate(ownedMaterial);
            }
            ownedMaterial = null;
        }

        #if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            fillAmount = Mathf.Clamp01(fillAmount);
            springFrequency = Mathf.Max(0.1f, springFrequency);
            spillRate = Mathf.Max(0f, spillRate);
            motionInfluence = Mathf.Max(0f, motionInfluence);
            // Do not create/destroy Unity objects from OnValidate (may run during deserialization).
        }

        #endif

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive()) return;
            EnsureVertexChannel();
            UIVertex vertex = default;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                // Canvas batching bakes positionOS into canvas space. Preserve Image-local coordinates in UV1.
                vertex.uv1 = new Vector4(vertex.position.x, vertex.position.y, 0f, 0f);
                vertices.SetUIVertex(vertex, i);
            }
        }

        private void EnsureVertexChannel()
        {
            Canvas canvas = image != null ? image.canvas : GetComponentInParent<Canvas>();
            if (canvas != null && (canvas.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) == 0)
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        [ContextMenu("Refill and settle")]
        public void Refill()
        {
            currentFill = lastConfiguredFill = Mathf.Clamp01(fillAmount);
            ResetMotion();
            if (isActiveAndEnabled) RefreshVisual();
        }

        /// <summary>Add a small world-space surface angular-velocity impulse (radians/second).</summary>
        public void AddImpulse(float angularVelocity)
        {
            surfaceVelocity = Mathf.Clamp(surfaceVelocity + angularVelocity, -15f, 15f);
            waveEnergy = Mathf.Max(waveEnergy, Mathf.Clamp01(Mathf.Abs(angularVelocity) * 0.2f));
        }

        /// <summary>Reset transform history after teleport/reparent to avoid a spurious acceleration.</summary>
        public void ResetMotion()
        {
            previousPosition = MotionPosition();
            previousVelocity = Vector2.zero;
            previousRotation = transform.eulerAngles.z;
            surfaceAngle = GravityAngle();
            surfaceVelocity = waveEnergy = phase = 0f;
        }

        private Vector2 MotionPosition()
        {
            return motionSpace != null ? transform.position - motionSpace.position : transform.position;
        }

        private float GravityAngle()
        {
            Vector2 up = gravity.sqrMagnitude > 0.000001f ? -gravity.normalized : Vector2.up;
            return Mathf.Atan2(up.x, up.y);
        }

        private void LateUpdate()
        {
            if (image == null) image = GetComponent<Image>();
            EnsureVertexChannel();
            if (!Mathf.Approximately(lastConfiguredFill, fillAmount))
                currentFill = lastConfiguredFill = Mathf.Clamp01(fillAmount);
            float dt = Application.isPlaying ? (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) : 0f;
            if (dt > 0f) Simulate(Mathf.Min(dt, 0.05f), dt);
            else if (!Application.isPlaying) ResetMotion();
            else CaptureMotionBaseline();
            RefreshVisual(dt > 0f ? Mathf.Min(dt, 0.05f) : 0f);
        }

        private void CaptureMotionBaseline()
        {
            previousPosition = MotionPosition();
            previousVelocity = Vector2.zero;
            previousRotation = transform.eulerAngles.z;
        }

        private void Simulate(float dt, float measuredDt)
        {
            Vector2 position = MotionPosition();
            Vector2 velocity = (position - previousPosition) / Mathf.Max(measuredDt, 0.00001f);
            Vector2 acceleration = (velocity - previousVelocity) / Mathf.Max(measuredDt, 0.00001f);
            previousPosition = position;
            previousVelocity = velocity;
            float rotation = transform.eulerAngles.z;
            float turn = Mathf.DeltaAngle(previousRotation, rotation) * Mathf.Deg2Rad;
            previousRotation = rotation;
            float omega = Mathf.Max(0.1f, springFrequency) * 2f * Mathf.PI;
            surfaceVelocity -= Mathf.Clamp(turn, -0.5f, 0.5f) * rotationInfluence * omega;

            Vector2 actualGravity = gravity.sqrMagnitude > 0.000001f ? gravity : new Vector2(0f, -9.81f);
            Vector2 effectiveUp = -actualGravity + Vector2.ClampMagnitude(acceleration * motionInfluence, actualGravity.magnitude * 2f);
            float baseAngle = GravityAngle();
            float target = effectiveUp.sqrMagnitude > 0.000001f ? Mathf.Atan2(effectiveUp.x, effectiveUp.y) : baseAngle;
            float limit = maxSloshAngle * Mathf.Deg2Rad;
            target = baseAngle + Mathf.Clamp(Mathf.DeltaAngle(baseAngle * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad, -limit, limit);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt * 120f));
            float step = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                float error = Mathf.DeltaAngle(surfaceAngle * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                surfaceVelocity += (omega * omega * error - 2f * dampingRatio * omega * surfaceVelocity) * step;
                surfaceVelocity = Mathf.Clamp(surfaceVelocity, -15f, 15f);
                surfaceAngle += surfaceVelocity * step;
                float deviation = Mathf.DeltaAngle(baseAngle * Mathf.Rad2Deg, surfaceAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float clamped = Mathf.Clamp(deviation, -limit, limit);
                if (!Mathf.Approximately(deviation, clamped)) surfaceVelocity = 0f;
                surfaceAngle = baseAngle + clamped;
            }
            float excitation = Mathf.Clamp01(Mathf.Abs(surfaceVelocity) * 0.15f + Mathf.Abs(turn) * 3f);
            waveEnergy = Mathf.Max(excitation, waveEnergy * Mathf.Exp(-dt * 3f));
            phase = Mathf.Repeat(phase + dt * waveSpeed, Mathf.PI * 200f);
        }

        private bool EnsureMaterial()
        {
            if (ownedMaterial != null) return true;
            Material template = Resources.Load<Material>(MaterialResourcePath);
            if (template == null || template.shader == null || template.shader.name != ShaderName)
            {
                if (!missingMaterialReported)
                    Debug.LogError("GravityLiquidImage needs Assets/Resources/UI/GravityLiquidUI.mat and its shader.", this);
                missingMaterialReported = true;
                return false;
            }
            ownedMaterial = new Material(template) { name = "Gravity Liquid (Instance)", hideFlags = HideFlags.HideAndDontSave };
            image.material = ownedMaterial;
            missingMaterialReported = false;
            return true;
        }

        /// <summary>Recompute the surface and push parameters, including the stencil-derived UI material.</summary>
        public void RefreshVisual(float spillDeltaTime = 0f)
        {
            if (!isActiveAndEnabled || image == null || !EnsureMaterial()) return;
            Rect rect = image.GetPixelAdjustedRect();
            Sprite sprite = image.overrideSprite;
            if (image.type == Image.Type.Simple && image.preserveAspect && sprite != null && sprite.rect.height > 0f)
            {
                float spriteAspect = sprite.rect.width / sprite.rect.height;
                if (spriteAspect > rect.width / Mathf.Max(rect.height, 0.001f))
                {
                    float height = rect.width / spriteAspect;
                    rect.y += (rect.height - height) * image.rectTransform.pivot.y;
                    rect.height = height;
                }
                else
                {
                    float width = rect.height * spriteAspect;
                    rect.x += (rect.width - width) * image.rectTransform.pivot.x;
                    rect.width = width;
                }
            }
            float aspect = Mathf.Max(rect.width, 0.001f) / Mathf.Max(rect.height, 0.001f);
            float wall = Mathf.Clamp(wallThickness, 0f, Mathf.Min(aspect * 0.49f, 0.49f));
            InnerBounds = Rect.MinMaxRect(-aspect * 0.5f + wall, -0.5f + wall, aspect * 0.5f - wall, 0.5f);
            bool shaped = containerMask != null && maskGeometry.Configure(containerMask, maskIsDistanceField, maskDistanceRange, wall, aspect);
            if (shaped) InnerBounds = maskGeometry.Bounds;
            Vector2 worldUp = new Vector2(Mathf.Sin(surfaceAngle), Mathf.Cos(surfaceAngle));
            // Covector transformation accounts for non-uniform scaling and mirrored transforms, not just Z rotation.
            Vector3 basisX = transform.TransformVector(Vector3.right);
            Vector3 basisY = transform.TransformVector(Vector3.up);
            Vector2 normal = new Vector2(Vector2.Dot(worldUp, basisX), Vector2.Dot(worldUp, basisY));
            SurfaceNormal = normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector2.up;
            if (Application.isPlaying && allowSpilling && spillDeltaTime > 0f)
            {
                float capacity = shaped ? maskGeometry.OpenTopCapacity(SurfaceNormal)
                    : LiquidContainerMath.OpenTopCapacity(InnerBounds, SurfaceNormal);
                if (currentFill > capacity) currentFill = Mathf.MoveTowards(currentFill, capacity, spillRate * spillDeltaTime);
            }
            SurfaceOffset = shaped ? maskGeometry.SolveSurfaceOffset(SurfaceNormal, currentFill)
                : LiquidContainerMath.SolveSurfaceOffset(InnerBounds, SurfaceNormal, currentFill);
            Vector4 drawing = new Vector4(rect.center.x, rect.center.y, rect.width, rect.height);
            Vector4 bounds = new Vector4(InnerBounds.xMin, InnerBounds.yMin, InnerBounds.xMax, InnerBounds.yMax);
            Vector4 plane = new Vector4(SurfaceNormal.x, SurfaceNormal.y, SurfaceOffset, currentFill);
            float amplitude = (currentFill <= 0.00001f || currentFill >= 0.99999f) ? 0f : idleWaveAmplitude + maxWaveAmplitude * waveEnergy;
            Vector4 wave = new Vector4(amplitude, waveCycles, phase, surfaceWidth);
            Apply(ownedMaterial, drawing, bounds, plane, wave, wall);
            // Mask creates a cached stencil variant. Updating only image.material would freeze the liquid under a Mask.
            Material rendered = image.materialForRendering;
            if (rendered != null && rendered != ownedMaterial) Apply(rendered, drawing, bounds, plane, wave, wall);
        }

        private void Apply(Material material, Vector4 drawing, Vector4 bounds, Vector4 plane, Vector4 wave, float wall)
        {
            material.SetVector(RectId, drawing);
            material.SetVector(BoundsId, bounds);
            material.SetVector(PlaneId, plane);
            material.SetVector(WaveId, wave);
            material.SetFloat(WallId, wall);
            material.SetColor(LiquidId, liquidColor);
            material.SetColor(DeepId, deepColor);
            material.SetColor(SurfaceId, surfaceColor);
            material.SetColor(WallColorId, wallColor);
            material.SetFloat(TextureId, textureInfluence);
            material.SetTexture(MaskId, containerMask != null ? containerMask : Texture2D.whiteTexture);
            material.SetFloat(UseMaskId, containerMask != null ? 1f : 0f);
            material.SetFloat(MaskSdfId, maskIsDistanceField ? 1f : 0f);
            material.SetFloat(MaskRangeId, maskDistanceRange);
        }
    }
}
