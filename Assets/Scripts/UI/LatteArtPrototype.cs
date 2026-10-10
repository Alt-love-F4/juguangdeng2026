using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UIEventSystem = UnityEngine.EventSystems.EventSystem;
using System.Collections.Generic;
using YokiFrame;

namespace OnePersonCompany
{
    /// <summary>
    /// GPU incompressible NS on a MAC grid. One Shader supplies display and all solver
    /// passes. Only GPU appearance is changed, never gameplay state. Production time
    /// comes from TickEvent. User-approved preview fallback is restricted to scenes
    /// without GameRoot, and never reactivates after receiving a game Tick.
    /// </summary>
    public sealed class LatteArtPrototype : MonoBehaviour
    {
        [Header("Single multi-pass NS Shader")]
        [SerializeField] private Shader simulationShader;
        [Header("Milk detail / fluid solver")]
        [SerializeField, Range(128, 1024)] private int resolution = 512;
        [SerializeField, Range(64, 512)] private int fluidResolution = 192;
        [SerializeField, Range(12, 100)] private int pressureIterations = 40;
        [SerializeField, Range(0, 12)] private int viscosityIterations = 4;
        [SerializeField, Range(0f, 0.001f)] private float viscosity = 0.0004f;
        [SerializeField, Range(0f, 12f)] private float vorticityStrength = 4f;
        [SerializeField, Range(0.005f, 0.08f)] private float brushRadius = 0.018f;
        [SerializeField, Range(0.1f, 15f)] private float pourRate = 4f;
        [Tooltip("Expansion gain at radius 0.018. Actual expansion scales linearly with brush radius; independent of NS pressure.")]
        [SerializeField, Min(0f)] private float pushStrength = 0.35f;
        [SerializeField, Range(0f, 3f)] private float dragStrength = 0.9f;
        [SerializeField, Range(0f, 8f)] private float velocityDamping = 3f;
        [SerializeField, Range(0f, 5f)] private float diffusion = 0.15f;
        [Header("Mouse")]
        [Tooltip("Off: hovering pours continuously. On: hold left mouse button.")]
        [SerializeField] private bool requireLeftButton = true;
        [Header("Standalone test scene only")]
        [Tooltip("Preview only with no GameRoot and no received game Tick. Paused games never fall back.")]
        [SerializeField] private bool allowUpdateFallback = true;

        private const float BowlRadius = 0.46f;
        private const float MaxStep = 1f / 60f;
        private const float ReferenceBrushRadius = 0.018f;
        [Header("Brush preview / wheel")]
        [SerializeField] private bool showBrushPreview = true;
        [SerializeField, Range(0.0005f, 0.01f)] private float wheelRadiusStep = 0.0025f;
        private RawImage image;
        private Renderer worldRenderer;
        private Collider worldCollider;
        [SerializeField, Tooltip("Camera that handles world-surface mouse input; defaults to Main Camera.")] private Camera inputCamera;
        private bool worldSurface;
        private Material originalRendererMaterial;
        private Material simulationMaterial, surfaceMaterial;
        private RenderTexture velocityRead, velocityWrite, velocityScratch;
        private RenderTexture pressureRead, pressureWrite, divergence;
        private RenderTexture densityRead, densityWrite, densityForward;
        private Texture originalTexture;
        private Material originalMaterial;
        private IUnRegister tickLink;
        private Vector2 previousUV;
        private bool wasPouring, receivedGameTick, gameClockPresent;
        private float simulationTime;
        private int simulationFrameCount;
        private PointerEventData uiPointer;
        private UIEventSystem cachedUIEventSystem;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private int advectVelocityPass, copyPass, diffusePass, curlPass, confinePass;
        private int divergencePass, pressurePass, projectPass, advectMilkPass, correctMilkPass;

        public RenderTexture VelocityTexture => velocityRead;
        public RenderTexture MilkTexture => densityRead;
        public RenderTexture PressureTexture => pressureRead;
        public RenderTexture DivergenceTexture => divergence;
        public RenderTexture VelocityScratchTexture => velocityScratch;
        public int SimulationFrameCount => simulationFrameCount;
        public bool UsesStandalonePreview => allowUpdateFallback && !receivedGameTick && !gameClockPresent;
        public Material SurfaceMaterial => surfaceMaterial;
        // These settings affect GPU appearance only, not game services/state.
        public float BrushRadius { get => brushRadius; set => brushRadius = Mathf.Clamp(value, 0.005f, 0.08f); }
        public float PourRate { get => pourRate; set => pourRate = Mathf.Clamp(value, 0.1f, 15f); }
        private float BrushRadiusScale => brushRadius / ReferenceBrushRadius;
        public float PushStrength
        {
            get => Mathf.Clamp(pushStrength * BrushRadiusScale, 0f, 2f);
            // The UI edits actual strength at the CURRENT radius; subsequent radius changes
            // keep this calibrated gain and scale expansion with the brush area.
            set => pushStrength = Mathf.Clamp(value, 0f, 2f) / Mathf.Max(BrushRadiusScale, 0.0001f);
        }
        public float DragStrength { get => dragStrength; set => dragStrength = Mathf.Clamp(value, 0f, 3f); }
        public float VelocityDamping { get => velocityDamping; set => velocityDamping = Mathf.Clamp(value, 0f, 8f); }
        public float Viscosity { get => viscosity; set => viscosity = Mathf.Clamp(value, 0f, 0.001f); }
        public float Diffusion { get => diffusion; set => diffusion = Mathf.Clamp(value, 0f, 5f); }
        public float VorticityStrength { get => vorticityStrength; set => vorticityStrength = Mathf.Clamp(value, 0f, 12f); }
        public int PressureIterations { get => pressureIterations; set => pressureIterations = Mathf.Clamp(value, 12, 100); }
        public float FoamRelief
        {
            get => surfaceMaterial != null ? surfaceMaterial.GetFloat("_NormalStrength") : 5f;
            set { if (surfaceMaterial != null) surfaceMaterial.SetFloat("_NormalStrength", Mathf.Clamp(value, 0f, 20f)); }
        }
        public void RestoreAppearanceDefaults()
        {
            brushRadius = ReferenceBrushRadius; pourRate = 4f; pushStrength = 0.35f; dragStrength = 0.9f;
            velocityDamping = 3f; viscosity = 0.0004f; diffusion = 0.15f; vorticityStrength = 4f;
            pressureIterations = 40; FoamRelief = 5f;
        }
        public string Solver => "2D MAC-grid NS + limited MacCormack milk + damped surface waves";

        private void OnEnable()
        {
            image = GetComponent<RawImage>();
            worldRenderer = GetComponent<Renderer>();
            worldCollider = GetComponent<MeshCollider>();
            worldSurface = image == null && worldRenderer != null;
            if (!worldSurface && image == null)
            {
                Debug.LogError("LatteArt: add this component to a RawImage or a world-space Renderer.", this);
                enabled = false;
                return;
            }
            if (inputCamera == null) inputCamera = Camera.main;
            if (image != null)
            {
                originalTexture = image.texture;
                originalMaterial = image.material;
            }
            else
            {
                originalRendererMaterial = worldRenderer.sharedMaterial;
                originalMaterial = originalRendererMaterial;
            }
            receivedGameTick = false;
            gameClockPresent = Object.FindFirstObjectByType<GameRoot>(FindObjectsInactive.Include) != null;
            if (simulationShader == null) simulationShader = Shader.Find("LatteArt/NavierStokes");
            if (simulationShader == null || !simulationShader.isSupported ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                Debug.LogError("LatteArt: missing/unsupported NS Shader or ARGBHalf support.", this);
                enabled = false;
                return;
            }
            simulationMaterial = new Material(simulationShader) { name = "Latte NS Solver (Runtime)" };
            surfaceMaterial = originalMaterial != null ? new Material(originalMaterial) : new Material(simulationShader);
            surfaceMaterial.shader = simulationShader;
            surfaceMaterial.name = "Latte NS Surface (Runtime)";
            surfaceMaterial.SetFloat("_WorldSurface", worldSurface ? 1f : 0f);
            surfaceMaterial.SetFloat("_SurfaceZTest", worldSurface ? 4f : 8f);
            advectVelocityPass = simulationMaterial.FindPass("AdvectVelocity");
            copyPass = simulationMaterial.FindPass("CopyState");
            diffusePass = simulationMaterial.FindPass("DiffuseVelocity");
            curlPass = simulationMaterial.FindPass("Curl");
            confinePass = simulationMaterial.FindPass("ConfineVorticity");
            divergencePass = simulationMaterial.FindPass("Divergence");
            pressurePass = simulationMaterial.FindPass("Pressure");
            projectPass = simulationMaterial.FindPass("Project");
            advectMilkPass = simulationMaterial.FindPass("AdvectMilk");
            correctMilkPass = simulationMaterial.FindPass("CorrectMilkAndWaves");
            if (advectVelocityPass < 0 || copyPass < 0 || diffusePass < 0 || curlPass < 0 || confinePass < 0 ||
                divergencePass < 0 || pressurePass < 0 || projectPass < 0 || advectMilkPass < 0 || correctMilkPass < 0)
            {
                Debug.LogError("LatteArt: incomplete multi-pass NS Shader.", this);
                enabled = false;
                return;
            }
            int size = Mathf.Clamp(resolution, 128, 1024);
            int grid = Mathf.Clamp(fluidResolution, 64, 512);
            velocityRead = CreateRT(grid, "Latte NS Velocity A", FilterMode.Bilinear);
            velocityWrite = CreateRT(grid, "Latte NS Velocity B", FilterMode.Bilinear);
            velocityScratch = CreateRT(grid, "Latte NS Velocity RHS / Curl", FilterMode.Bilinear);
            pressureRead = CreateRT(grid, "Latte NS Pressure A", FilterMode.Point);
            pressureWrite = CreateRT(grid, "Latte NS Pressure B", FilterMode.Point);
            divergence = CreateRT(grid, "Latte NS Divergence", FilterMode.Point);
            densityRead = CreateRT(size, "Latte NS Milk A", FilterMode.Bilinear);
            densityWrite = CreateRT(size, "Latte NS Milk B", FilterMode.Bilinear);
            densityForward = CreateRT(size, "Latte NS Milk Forward", FilterMode.Bilinear);
            ResetLatte();
            surfaceMaterial.SetFloat("_BowlRadius", BowlRadius);
            if (image != null)
            {
                image.material = surfaceMaterial;
                image.texture = densityRead;
            }
            else
            {
                worldRenderer.material = surfaceMaterial;
                surfaceMaterial.SetTexture("_MainTex", densityRead);
            }
            tickLink = EventKit.Type.Register<TickEvent>(OnTick);
        }
        private RenderTexture CreateRT(int size, string name, FilterMode filtering)
        {
            var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = name, filterMode = filtering, wrapMode = TextureWrapMode.Clamp,
                useMipMap = false, autoGenerateMips = false, antiAliasing = 1
            };
            if (!rt.Create()) throw new System.InvalidOperationException("Unable to allocate " + name);
            return rt;
        }
        private static void ClearRT(RenderTexture rt)
        {
            if (rt == null) return;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
        }
        public void ResetLatte()
        {
            RenderTexture oldActive = RenderTexture.active;
            bool oldSRGB = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = false;
                ClearRT(velocityRead); ClearRT(velocityWrite); ClearRT(velocityScratch);
                ClearRT(pressureRead); ClearRT(pressureWrite); ClearRT(divergence);
                ClearRT(densityRead); ClearRT(densityWrite); ClearRT(densityForward);
            }
            finally { RenderTexture.active = oldActive; GL.sRGBWrite = oldSRGB; }
            wasPouring = false;
            simulationTime = 0;
            simulationFrameCount = 0;
            if (image != null && surfaceMaterial != null) image.texture = densityRead;
            if (worldSurface && surfaceMaterial != null) surfaceMaterial.SetTexture("_MainTex", densityRead);
        }
        private bool TryGetPointerUV(out Vector2 uv)
        {
            uv = Vector2.one * 0.5f;
            if (Mouse.current == null) return false;
            if (!worldSurface)
            {
                if (image == null || image.canvas == null) return false;
                Canvas root = image.canvas.rootCanvas;
                Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform,
                    Mouse.current.position.ReadValue(), camera, out Vector2 local)) return false;
                Rect rect = image.rectTransform.rect;
                if (!rect.Contains(local) || rect.width <= 0 || rect.height <= 0) return false;
                uv = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
            }
            else
            {
                Camera camera = inputCamera != null ? inputCamera : Camera.main;
                if (camera == null || worldCollider == null) return false;
                Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (!Physics.Raycast(ray, out RaycastHit hit, camera.farClipPlane, camera.cullingMask, QueryTriggerInteraction.Ignore) ||
                    hit.collider != worldCollider) return false;
                uv = hit.textureCoord;
            }
            return (uv - Vector2.one * 0.5f).sqrMagnitude < BowlRadius * BowlRadius;
        }
        private void OnTick(TickEvent evt) { receivedGameTick = true; SimulateInput(evt.DeltaTime); }
        private void Update()
        {
            // Read pointer preview and wheel in Update, immediately after the Input System
            // update. LateUpdate is too easy to miss for delta-style scroll values.
            UpdateBrushPointer();

            // User-approved isolated preview exception; never revive a paused game clock.
            if (!allowUpdateFallback || receivedGameTick) return;
            gameClockPresent = GameRoot.Instance != null ||
                Object.FindFirstObjectByType<GameRoot>(FindObjectsInactive.Include) != null;
            if (!gameClockPresent) SimulateInput(Time.unscaledDeltaTime);
        }
        private void UpdateBrushPointer()
        {
            if (surfaceMaterial == null) return;
            bool hit = TryGetPointerUV(out Vector2 uv) && !IsPointerOverUI();
            if (hit && Mouse.current != null)
            {
                // Mouse devices report a notch as either 1 or 120 depending on the
                // platform/driver. Normalize both forms before applying the same
                // radius step, while preserving fractional trackpad deltas.
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 8f) wheel /= 120f;
                if (Mathf.Abs(wheel) > 0.001f)
                    BrushRadius += Mathf.Clamp(wheel, -8f, 8f) * wheelRadiusStep;
            }
            surfaceMaterial.SetFloat("_BrushPreviewActive", showBrushPreview && hit ? 1f : 0f);
            surfaceMaterial.SetFloat("_BrushPreviewRadius", BrushRadius);
            surfaceMaterial.SetVector("_BrushPreviewUV", new Vector4(uv.x, uv.y, 0, 0));
            surfaceMaterial.SetFloat("_BrushPreviewPressed", hit && Mouse.current != null && Mouse.current.leftButton.isPressed ? 1f : 0f);
        }
        private void SimulateInput(float dt)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetLatte();
            bool inside = TryGetPointerUV(out Vector2 uv);
            bool pouring = inside && !IsPointerOverUI() && (!requireLeftButton || (Mouse.current != null && Mouse.current.leftButton.isPressed));
            AdvanceFrame(dt, uv, pouring);
        }
        private bool IsPointerOverUI()
        {
            // Raycast current coordinates directly; do not depend on UI Update ordering.
            if (!worldSurface || Mouse.current == null || UIEventSystem.current == null) return false;
            if (uiPointer == null || cachedUIEventSystem != UIEventSystem.current)
            {
                cachedUIEventSystem = UIEventSystem.current;
                uiPointer = new PointerEventData(cachedUIEventSystem);
            }
            uiPointer.position = Mouse.current.position.ReadValue();
            uiHits.Clear();
            UIEventSystem.current.RaycastAll(uiPointer, uiHits);
            foreach (var hit in uiHits) if (hit.module is GraphicRaycaster) return true;
            return false;
        }
        // Editor-only deterministic Play Mode validation invokes this private visual step.
        private void AdvanceFrame(float deltaTime, Vector2 currentUV, bool pouring)
        {
            if (densityRead == null || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            float dt = Mathf.Min(deltaTime, 0.1f);
            Vector2 start = pouring && wasPouring ? previousUV : currentUV;
            Vector2 pointerVelocity = pouring ? Vector2.ClampMagnitude((currentUV - start) / dt, 1.5f) : Vector2.zero;
            float waveSpeed = 0.05f;
            // Wave CFL remains bounded even at 1024 density resolution.
            float maxStep = Mathf.Min(MaxStep, 0.55f / (densityRead.width * waveSpeed));
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / maxStep), 1, 12);
            float stepDt = dt / steps;
            Material m = simulationMaterial;
            m.SetFloat("_FluidResolution", velocityRead.width);
            m.SetFloat("_BowlRadius", BowlRadius);
            m.SetFloat("_BrushRadius", brushRadius);
            m.SetFloat("_PourRate", pourRate);
            m.SetFloat("_PushStrength", PushStrength);
            m.SetFloat("_DragStrength", dragStrength);
            m.SetFloat("_DragResponse", 18f);
            m.SetFloat("_VelocityDamping", velocityDamping);
            m.SetFloat("_Viscosity", viscosity);
            m.SetFloat("_Diffusion", diffusion);
            m.SetFloat("_Vorticity", vorticityStrength);
            m.SetFloat("_MaxVelocity", 0.75f);
            m.SetFloat("_Pouring", pouring ? 1 : 0);
            m.SetFloat("_DeltaTime", stepDt);
            m.SetFloat("_WaveSpeed", waveSpeed);
            m.SetFloat("_WaveDamping", 2.8f);
            m.SetVector("_PointerVelocity", new Vector4(pointerVelocity.x, pointerVelocity.y, 0, 0));
            RenderTexture oldActive = RenderTexture.active;
            bool oldSRGB = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = false;
                for (int s = 0; s < steps; s++)
                {
                    Vector2 from = Vector2.Lerp(start, currentUV, s / (float)steps);
                    Vector2 to = Vector2.Lerp(start, currentUV, (s + 1f) / steps);
                    m.SetVector("_PreviousPourUV", new Vector4(from.x, from.y, 0, 0));
                    m.SetVector("_PourUV", new Vector4(to.x, to.y, 0, 0));
                    SolveStep();
                    simulationTime += stepDt;
                    simulationFrameCount++;
                }
            }
            finally { GL.sRGBWrite = oldSRGB; RenderTexture.active = oldActive; }
            if (image != null) image.texture = densityRead;
            if (worldSurface) surfaceMaterial.SetTexture("_MainTex", densityRead);
            surfaceMaterial.SetFloat("_SimulationTime", simulationTime);
            previousUV = currentUV;
            wasPouring = pouring;
        }
        private void SolveStep()
        {
            Material m = simulationMaterial;
            Graphics.Blit(velocityRead, velocityWrite, m, advectVelocityPass);
            Swap(ref velocityRead, ref velocityWrite);
            if (viscosity > 0 && viscosityIterations > 0)
            {
                Graphics.Blit(velocityRead, velocityScratch, m, copyPass);
                m.SetTexture("_VelocityRhsTex", velocityScratch);
                for (int i = 0; i < Mathf.Clamp(viscosityIterations, 0, 12); i++)
                {
                    Graphics.Blit(velocityRead, velocityWrite, m, diffusePass);
                    Swap(ref velocityRead, ref velocityWrite);
                }
            }
            if (vorticityStrength > 0)
            {
                Graphics.Blit(velocityRead, velocityScratch, m, curlPass);
                m.SetTexture("_CurlTex", velocityScratch);
                Graphics.Blit(velocityRead, velocityWrite, m, confinePass);
                Swap(ref velocityRead, ref velocityWrite);
            }
            Graphics.Blit(velocityRead, divergence, m, divergencePass);
            m.SetTexture("_DivergenceTex", divergence);
            for (int i = 0; i < Mathf.Clamp(pressureIterations, 12, 100); i++)
            {
                Graphics.Blit(pressureRead, pressureWrite, m, pressurePass);
                Swap(ref pressureRead, ref pressureWrite);
            }
            m.SetTexture("_VelocityTex", velocityRead);
            Graphics.Blit(pressureRead, velocityWrite, m, projectPass);
            Swap(ref velocityRead, ref velocityWrite);
            m.SetTexture("_VelocityTex", velocityRead);
            m.SetTexture("_OriginalDensity", densityRead);
            Graphics.Blit(densityRead, densityForward, m, advectMilkPass);
            Graphics.Blit(densityForward, densityWrite, m, correctMilkPass);
            Swap(ref densityRead, ref densityWrite);
        }
        private static void Swap(ref RenderTexture a, ref RenderTexture b) { RenderTexture t = a; a = b; b = t; }
        private void OnDisable()
        {
            tickLink?.UnRegister(); tickLink = null;
            if (image != null && surfaceMaterial != null) { image.material = originalMaterial; image.texture = originalTexture; }
            if (worldSurface && worldRenderer != null) worldRenderer.sharedMaterial = originalRendererMaterial;
            Release(ref velocityRead); Release(ref velocityWrite); Release(ref velocityScratch);
            Release(ref pressureRead); Release(ref pressureWrite); Release(ref divergence);
            Release(ref densityRead); Release(ref densityWrite); Release(ref densityForward);
            if (simulationMaterial != null) Destroy(simulationMaterial);
            if (surfaceMaterial != null) Destroy(surfaceMaterial);
            simulationMaterial = null; surfaceMaterial = null; wasPouring = false;
        }
        private static void Release(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release(); Destroy(rt); rt = null;
        }
    }
}





