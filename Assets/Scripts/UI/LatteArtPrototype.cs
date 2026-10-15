using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using Spotlight.FreeCafe;
using UIEventSystem = UnityEngine.EventSystems.EventSystem;

namespace OnePersonCompany
{
    /// <summary>GPU 拉花流体与玩家轨迹采集。轨迹仅用于识别，库存和订单仍由 FreeCafe 域层管理。</summary>
    public sealed class LatteArtPrototype : MonoBehaviour, ILatteArtSurface, ILatteArtFoamSurface, ILatteArtFeedbackSurface
    {
        [Header("Single multi-pass NS Shader")]
        [SerializeField] private Shader simulationShader;
        [Header("Milk detail / fluid solver")]
        [SerializeField, Range(128, 1024)] private int resolution = 512;
        [SerializeField, Range(64, 512)] private int fluidResolution = 192;
        [SerializeField, Range(12, 100)] private int pressureIterations = 40;
        [SerializeField, Range(0, 12)] private int viscosityIterations = 6;
        [SerializeField, Range(0f, 0.001f)] private float viscosity = 0.00055f;
        [SerializeField, Range(0f, 12f)] private float vorticityStrength = 1.2f;
        [SerializeField, Range(0.005f, 0.08f)] private float brushRadius = 0.02f;
        [SerializeField, Range(0.1f, 15f)] private float pourRate = 5f;
        [SerializeField, Min(0f)] private float pushStrength = 0.2f;
        [SerializeField, Range(0f, 3f)] private float dragStrength = 0.38f;
        [SerializeField, Range(0f, 8f)] private float velocityDamping = 4.2f;
        [SerializeField, Range(0f, 5f)] private float diffusion = 0.1f;
        [Tooltip("快速划动的补奶强度；过高会像不透明画笔。")]
        [SerializeField, Range(0f, .08f)] private float strokeDepositAssist = .03f;
        [Header("Mouse")]
        [SerializeField] private bool requireLeftButton = true;
        [SerializeField] private bool showBrushPreview = true;
        [SerializeField, Range(0.0005f, 0.01f)] private float wheelRadiusStep = 0.0025f;
        [SerializeField] private Camera inputCamera;

        private const float BowlRadius = 0.46f, MaxStep = 1f / 60f, ReferenceBrushRadius = 0.018f;
        private const int MaxFrameSteps = 8, MaxStrokePoints = 8192;
        private RawImage image; private Renderer worldRenderer; private Collider worldCollider; private bool worldSurface;
        private Material originalRendererMaterial, simulationMaterial, surfaceMaterial, originalMaterial;
        private Texture originalTexture; private Vector2 previousUV; private bool wasPouring;
        private float simulationTime; private int simulationFrameCount;
        private Vector3[] surfaceVertices;
        private Vector2[] surfaceUVs;
        private int[] surfaceTriangles;
        private PointerEventData uiPointer; private UIEventSystem cachedUIEventSystem;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private readonly List<Vector2> strokePoints = new List<Vector2>();
        private readonly List<int> strokeStarts = new List<int>();
        private int advectVelocityPass, copyPass, diffusePass, curlPass, confinePass, divergencePass, pressurePass, projectPass, advectMilkPass, correctMilkPass;
        private bool interactionEnabled = true;
        private bool strokeActive;
        private Func<float, float> foamConsumer;
        private float foamMlPerSecond = 6;
        private Action resetHandler;

        public void SetResetHandler(Action handler) => resetHandler = handler;
        public void GetPatternFits(IList<LatteArtPatternPreset> presets, IList<LatteArtRecognitionResult> results)
            => LatteArtPatternRecognizer.GetPatternFits(strokePoints, presets, strokeStarts, results);
        public void SetLiquidColor(Color color)
        {
            if (surfaceMaterial == null) return;
            color.a = 1;
            surfaceMaterial.SetColor("_CoffeeColor", color);
            surfaceMaterial.SetColor("_CremaColor", Color.Lerp(color, Color.white, .12f));
        }

        public void SetFoamConsumer(Func<float, float> consumer, float millilitersPerSecond)
        {
            foamConsumer = consumer;
            foamMlPerSecond = Mathf.Max(.1f, millilitersPerSecond);
        }

        public RenderTexture VelocityTexture => velocityRead; public RenderTexture MilkTexture => densityRead;
        public RenderTexture PressureTexture => pressureRead; public RenderTexture DivergenceTexture => divergence;
        public RenderTexture VelocityScratchTexture => velocityScratch; public int SimulationFrameCount => simulationFrameCount;
        public Material SurfaceMaterial => surfaceMaterial;
        public float BrushRadius { get => brushRadius; set => brushRadius = Mathf.Clamp(value, .005f, .08f); }
        public float PourRate { get => pourRate; set => pourRate = Mathf.Clamp(value, .1f, 15f); }
        private float BrushRadiusScale => brushRadius / ReferenceBrushRadius;
        public float PushStrength { get => Mathf.Clamp(pushStrength * BrushRadiusScale, 0f, 2f); set => pushStrength = Mathf.Clamp(value, 0f, 2f) / Mathf.Max(BrushRadiusScale, .0001f); }
        public float DragStrength { get => dragStrength; set => dragStrength = Mathf.Clamp(value, 0f, 3f); }
        public float VelocityDamping { get => velocityDamping; set => velocityDamping = Mathf.Clamp(value, 0f, 8f); }
        public float Viscosity { get => viscosity; set => viscosity = Mathf.Clamp(value, 0f, .001f); }
        public float Diffusion { get => diffusion; set => diffusion = Mathf.Clamp(value, 0f, 5f); }
        public float VorticityStrength { get => vorticityStrength; set => vorticityStrength = Mathf.Clamp(value, 0f, 12f); }
        public int PressureIterations { get => pressureIterations; set => pressureIterations = Mathf.Clamp(value, 12, 100); }
        public float FoamRelief { get => surfaceMaterial != null ? surfaceMaterial.GetFloat("_NormalStrength") : 5f; set { if (surfaceMaterial != null) surfaceMaterial.SetFloat("_NormalStrength", Mathf.Clamp(value, 0f, 20f)); } }
        public string Solver => "2D MAC-grid NS + limited MacCormack milk + damped surface waves";
        public int StrokePointCount => strokePoints.Count;
        public LatteArtRecognitionResult Recognize(IList<LatteArtPatternPreset> presets = null) => LatteArtPatternRecognizer.Recognize(strokePoints, presets, strokeStarts);
        public void SetInteractionEnabled(bool value) { interactionEnabled = value; if (!value) { strokeActive = false; wasPouring = false; } }

        private RenderTexture velocityRead, velocityWrite, velocityScratch, pressureRead, pressureWrite, divergence, densityRead, densityWrite, densityForward;

        private void OnEnable()
        {
            image = GetComponent<RawImage>(); worldRenderer = GetComponent<Renderer>(); worldCollider = GetComponent<Collider>(); worldSurface = image == null && worldRenderer != null;
            if (worldSurface)
            {
                var meshFilter = GetComponent<MeshFilter>();
                var mesh = meshFilter != null ? meshFilter.sharedMesh : null;
                if (mesh != null && mesh.isReadable)
                {
                    surfaceVertices = mesh.vertices;
                    surfaceUVs = mesh.uv;
                    surfaceTriangles = mesh.triangles;
                }
                // LatteArtBench 里背景是 SpriteRenderer，同一 SortingGroup 下必须明确让液面排在背景之后。
                worldRenderer.sortingOrder = Mathf.Max(worldRenderer.sortingOrder, 1000);
                worldRenderer.enabled = true;
                worldRenderer.shadowCastingMode = ShadowCastingMode.Off;
                worldRenderer.receiveShadows = false;
            }
            if (!worldSurface && image == null) { Debug.LogError("LatteArt: add this component to a RawImage or a world-space Renderer.", this); enabled = false; return; }
            if (inputCamera == null) inputCamera = Camera.main;
            if (image != null) { originalTexture = image.texture; originalMaterial = image.material; } else { originalRendererMaterial = worldRenderer.sharedMaterial; originalMaterial = originalRendererMaterial; }
            if (simulationShader == null) simulationShader = Shader.Find("LatteArt/NavierStokes");
            if (simulationShader == null || !simulationShader.isSupported || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) { Debug.LogError("LatteArt: missing/unsupported NS Shader or ARGBHalf support.", this); enabled = false; return; }
            simulationMaterial = new Material(simulationShader) { name = "Latte NS Solver (Runtime)" }; surfaceMaterial = originalMaterial != null ? new Material(originalMaterial) : new Material(simulationShader); surfaceMaterial.shader = simulationShader; surfaceMaterial.name = "Latte NS Surface (Runtime)";
            surfaceMaterial.SetFloat("_WorldSurface", worldSurface ? 1f : 0f); surfaceMaterial.SetFloat("_SurfaceZTest", 8f); surfaceMaterial.SetFloat("_BowlRadius", BowlRadius);
            if (worldSurface) surfaceMaterial.renderQueue = 3000;
            advectVelocityPass = simulationMaterial.FindPass("AdvectVelocity"); copyPass = simulationMaterial.FindPass("CopyState"); diffusePass = simulationMaterial.FindPass("DiffuseVelocity"); curlPass = simulationMaterial.FindPass("Curl"); confinePass = simulationMaterial.FindPass("ConfineVorticity"); divergencePass = simulationMaterial.FindPass("Divergence"); pressurePass = simulationMaterial.FindPass("Pressure"); projectPass = simulationMaterial.FindPass("Project"); advectMilkPass = simulationMaterial.FindPass("AdvectMilk"); correctMilkPass = simulationMaterial.FindPass("CorrectMilkAndWaves");
            Allocate(); ApplySurface(); ResetLatte();
        }
        private void Allocate()
        {
            int size = Mathf.Clamp(resolution, 128, 1024), grid = Mathf.Clamp(fluidResolution, 64, 512);
            velocityRead = CreateRT(grid, "Latte Velocity A", FilterMode.Bilinear); velocityWrite = CreateRT(grid, "Latte Velocity B", FilterMode.Bilinear); velocityScratch = CreateRT(grid, "Latte Velocity Scratch", FilterMode.Bilinear);
            pressureRead = CreateRT(grid, "Latte Pressure A", FilterMode.Point); pressureWrite = CreateRT(grid, "Latte Pressure B", FilterMode.Point); divergence = CreateRT(grid, "Latte Divergence", FilterMode.Point);
            densityRead = CreateRT(size, "Latte Milk A", FilterMode.Bilinear); densityWrite = CreateRT(size, "Latte Milk B", FilterMode.Bilinear); densityForward = CreateRT(size, "Latte Milk Forward", FilterMode.Bilinear);
        }
        private RenderTexture CreateRT(int size, string name, FilterMode filter) { var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBHalf) { name = name, filterMode = filter, wrapMode = TextureWrapMode.Clamp, useMipMap = false, autoGenerateMips = false }; rt.Create(); return rt; }
        private void ApplySurface() { if (image != null) { image.material = surfaceMaterial; image.texture = densityRead; } else if (worldSurface) { worldRenderer.material = surfaceMaterial; surfaceMaterial.SetTexture("_MainTex", densityRead); } }
        public void ResetLatte()
        {
            ClearRT(velocityRead); ClearRT(velocityWrite); ClearRT(velocityScratch); ClearRT(pressureRead); ClearRT(pressureWrite); ClearRT(divergence); ClearRT(densityRead); ClearRT(densityWrite); ClearRT(densityForward);
            strokePoints.Clear(); strokeStarts.Clear(); strokeActive = false; wasPouring = false; simulationTime = 0; simulationFrameCount = 0; ApplySurface();
        }
        private static void ClearRT(RenderTexture rt) { if (rt == null) return; var old = RenderTexture.active; RenderTexture.active = rt; GL.Clear(false, true, Color.clear); RenderTexture.active = old; }
        private bool TryGetPointerUV(out Vector2 uv)
        {
            uv = Vector2.one * .5f;
            return interactionEnabled && Mouse.current != null &&
                   TryGetSurfaceUV(Mouse.current.position.ReadValue(), out uv);
        }

        /// <summary>屏幕点映射到渲染使用的真实 UV；不依赖 Collider 朝向或固定 Plane 尺寸。</summary>
        public bool TryGetSurfaceUV(Vector2 screenPoint, out Vector2 uv)
        {
            uv = Vector2.one * .5f;
            if (!worldSurface)
            {
                if (image == null || image.canvas == null) return false;
                var canvas = image.canvas.rootCanvas;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform, screenPoint, camera, out Vector2 local)) return false;
                Rect rect = image.rectTransform.rect;
                if (!rect.Contains(local) || rect.width <= 0 || rect.height <= 0) return false;
                var displayUV = image.uvRect;
                uv = new Vector2(displayUV.x + (local.x - rect.xMin) / rect.width * displayUV.width,
                                 displayUV.y + (local.y - rect.yMin) / rect.height * displayUV.height);
            }
            else
            {
                Camera camera = inputCamera != null ? inputCamera : Camera.main;
                if (camera == null) return false;
                Ray worldRay = camera.ScreenPointToRay(screenPoint);
                if (surfaceVertices != null && surfaceUVs != null && surfaceUVs.Length == surfaceVertices.Length && surfaceTriangles != null)
                {
                    // Shader Cull Off renders either face. Intersect the same mesh on either face,
                    // then interpolate its UVs; Unity's built-in Plane has both UV axes reversed.
                    Vector3 origin = transform.InverseTransformPoint(worldRay.origin);
                    Vector3 direction = transform.InverseTransformVector(worldRay.direction);
                    float nearest = float.PositiveInfinity;
                    bool found = false;
                    for (int i = 0; i + 2 < surfaceTriangles.Length; i += 3)
                    {
                        int a = surfaceTriangles[i], b = surfaceTriangles[i + 1], c = surfaceTriangles[i + 2];
                        Vector3 edge1 = surfaceVertices[b] - surfaceVertices[a];
                        Vector3 edge2 = surfaceVertices[c] - surfaceVertices[a];
                        Vector3 cross = Vector3.Cross(direction, edge2);
                        float determinant = Vector3.Dot(edge1, cross);
                        if (Mathf.Abs(determinant) < 1e-8f) continue;
                        Vector3 offset = origin - surfaceVertices[a];
                        float u = Vector3.Dot(offset, cross) / determinant;
                        Vector3 q = Vector3.Cross(offset, edge1);
                        float v = Vector3.Dot(direction, q) / determinant;
                        if (u < -.00001f || v < -.00001f || u + v > 1.00001f) continue;
                        float distance = Vector3.Dot(edge2, q) / determinant;
                        if (distance < 0 || distance >= nearest) continue;
                        nearest = distance;
                        uv = surfaceUVs[a] * (1 - u - v) + surfaceUVs[b] * u + surfaceUVs[c] * v;
                        found = true;
                    }
                    if (!found) return false;
                }
                else
                {
                    if (worldCollider == null || !worldCollider.Raycast(worldRay, out RaycastHit hit, camera.farClipPlane)) return false;
                    uv = hit.textureCoord;
                }
            }
            return (uv - Vector2.one * .5f).sqrMagnitude < BowlRadius * BowlRadius;
        }
        private void Update()
        {
            UpdateBrushPointer(); SimulateInput(Time.deltaTime);
        }
        private void UpdateBrushPointer()
        {
            if (surfaceMaterial == null) return; bool hit = TryGetPointerUV(out Vector2 uv) && !IsPointerOverUI();
            if (hit && Mouse.current != null) { float wheel = Mouse.current.scroll.ReadValue().y; if (Mathf.Abs(wheel) > 8f) wheel /= 120f; if (Mathf.Abs(wheel) > .001f) BrushRadius += Mathf.Clamp(wheel, -8f, 8f) * wheelRadiusStep; }
            surfaceMaterial.SetFloat("_BrushPreviewActive", showBrushPreview && hit ? 1f : 0f); surfaceMaterial.SetFloat("_BrushPreviewRadius", BrushRadius); surfaceMaterial.SetVector("_BrushPreviewUV", new Vector4(uv.x, uv.y, 0, 0)); surfaceMaterial.SetFloat("_BrushPreviewPressed", hit && Mouse.current != null && Mouse.current.leftButton.isPressed ? 1f : 0f);
        }
        private void SimulateInput(float dt)
        {
            if (interactionEnabled && !IsPointerOverUI() && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                if (resetHandler != null) resetHandler();
                else ResetLatte();
            }
            bool inside = TryGetPointerUV(out Vector2 uv); bool pouring = inside && !IsPointerOverUI() && (!requireLeftButton || Mouse.current != null && Mouse.current.leftButton.isPressed);
            float depositFraction = 1;
            if (pouring && foamConsumer != null)
            {
                if (surfaceMaterial == null || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) pouring = false;
                else
                {
                    float elapsed = Mathf.Min(dt, Mathf.Min(MaxStep, .55f / (densityRead.width * .05f)) * MaxFrameSteps);
                    float requested = foamMlPerSecond * elapsed * (pourRate / 5f) * BrushRadiusScale * BrushRadiusScale;
                    float accepted = foamConsumer(requested);
                    depositFraction = Mathf.Clamp01(accepted / requested);
                    pouring = accepted > 0;
                }
            }
            Spotlight.Audio.GameAudio.SetLoop(this, "latte", Spotlight.Audio.AudioKeys.LattePour, pouring && dt > 0);
            if (pouring && !strokeActive && strokePoints.Count < MaxStrokePoints)
            {
                strokeStarts.Add(strokePoints.Count);
                strokeActive = true;
            }
            if (pouring && strokeActive && strokePoints.Count < MaxStrokePoints)
            {
                // Uniform screen units preserve the player's visible shape even when mesh UVs
                // are mirrored/rotated; render/deposition continue to use the mesh UV above.
                Vector2 point = Mouse.current != null ? Mouse.current.position.ReadValue() / Mathf.Max(1, Screen.height) : uv;
                int startIndex = strokeStarts[strokeStarts.Count - 1];
                if (strokePoints.Count == startIndex || (strokePoints[strokePoints.Count - 1] - point).sqrMagnitude > .00000225f)
                    strokePoints.Add(point);
            }
            if (!pouring) strokeActive = false;
            AdvanceFrame(dt, uv, pouring, depositFraction);
        }
        private bool IsPointerOverUI()
        {
            if (!worldSurface || Mouse.current == null || UIEventSystem.current == null) return false;
            if (uiPointer == null || cachedUIEventSystem != UIEventSystem.current) { cachedUIEventSystem = UIEventSystem.current; uiPointer = new PointerEventData(cachedUIEventSystem); }
            uiPointer.position = Mouse.current.position.ReadValue(); uiHits.Clear(); cachedUIEventSystem.RaycastAll(uiPointer, uiHits);
            // CoffeeShop has a full-screen 2D background collider. It is an interaction target for
            // the cafe, but it must not make the latte surface think the pointer is over UI.
            // Only the high-priority latte/GM canvases should block drawing.
            foreach (var hit in uiHits)
            {
                var canvas = hit.gameObject != null ? hit.gameObject.GetComponentInParent<Canvas>() : null;
                if (canvas != null && canvas.sortingOrder >= 900) return true;
            }
            return false;
        }
        private void AdvanceFrame(float dt, Vector2 currentUV, bool pouring, float depositFraction = 1)
        {
            if (surfaceMaterial == null || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            const float waveSpeed = 0.05f;
            // Respect the explicit wave CFL limit, but consume real elapsed time with bounded
            // substeps. Only exceptional stalls are truncated (never replay a huge catch-up).
            float maxStep = Mathf.Min(MaxStep, 0.55f / (densityRead.width * waveSpeed));
            float elapsed = Mathf.Min(dt, maxStep * MaxFrameSteps);
            int steps = Mathf.Clamp(Mathf.CeilToInt(elapsed / maxStep), 1, MaxFrameSteps);
            float stepDt = elapsed / steps;
            Vector2 start = wasPouring && pouring ? previousUV : currentUV;
            // Pointer velocity uses actual wall time, not the CFL-clipped solver timestep.
            Vector2 pointerVelocity = Vector2.ClampMagnitude((currentUV - start) / Mathf.Max(dt, .0001f), .75f);
            var m = simulationMaterial;
            m.SetInt("_FluidResolution", velocityRead.width); m.SetFloat("_BowlRadius", BowlRadius); m.SetFloat("_BrushRadius", brushRadius); m.SetFloat("_PourRate", pourRate); m.SetFloat("_PushStrength", PushStrength); m.SetFloat("_DragStrength", dragStrength); m.SetFloat("_DragResponse", 18f); m.SetFloat("_VelocityDamping", velocityDamping); m.SetFloat("_Viscosity", viscosity); m.SetFloat("_Diffusion", diffusion); m.SetFloat("_Vorticity", vorticityStrength); m.SetFloat("_MaxVelocity", .75f); m.SetFloat("_Pouring", pouring ? 1 : 0); m.SetFloat("_DeltaTime", stepDt); m.SetFloat("_WaveSpeed", waveSpeed); m.SetFloat("_WaveDamping", 2.8f); m.SetVector("_PointerVelocity", new Vector4(pointerVelocity.x, pointerVelocity.y, 0, 0));
            // A swept capsule alone gets almost transparent during a fast cursor move.
            // Maintain a minimum local dwell/deposit along it without increasing fluid force.
            float distancePerStep = Vector2.Distance(start, currentUV) / steps;
            float depositTime = stepDt + Mathf.Min(distancePerStep / Mathf.Max(2f * brushRadius, .001f) * strokeDepositAssist, .08f / steps);
            m.SetFloat("_DepositTime", depositTime);
            m.SetFloat("_PourRate", pourRate * depositFraction);
            var oldActive = RenderTexture.active; bool oldSRGB = GL.sRGBWrite; try { GL.sRGBWrite = false; for (int s = 0; s < steps; s++) { Vector2 from = Vector2.Lerp(start, currentUV, s / (float)steps), to = Vector2.Lerp(start, currentUV, (s + 1f) / steps); m.SetVector("_PreviousPourUV", new Vector4(from.x, from.y, 0, 0)); m.SetVector("_PourUV", new Vector4(to.x, to.y, 0, 0)); SolveStep(); simulationTime += stepDt; simulationFrameCount++; } } finally { GL.sRGBWrite = oldSRGB; RenderTexture.active = oldActive; }
            if (image != null) image.texture = densityRead; if (worldSurface) surfaceMaterial.SetTexture("_MainTex", densityRead); surfaceMaterial.SetFloat("_SimulationTime", simulationTime); previousUV = currentUV; wasPouring = pouring;
        }
        private void SolveStep()
        {
            var m = simulationMaterial; Graphics.Blit(velocityRead, velocityWrite, m, advectVelocityPass); Swap(ref velocityRead, ref velocityWrite); if (viscosity > 0 && viscosityIterations > 0) { Graphics.Blit(velocityRead, velocityScratch, m, copyPass); m.SetTexture("_VelocityRhsTex", velocityScratch); for (int i = 0; i < viscosityIterations; i++) { Graphics.Blit(velocityRead, velocityWrite, m, diffusePass); Swap(ref velocityRead, ref velocityWrite); } } if (vorticityStrength > 0) { Graphics.Blit(velocityRead, velocityScratch, m, curlPass); m.SetTexture("_CurlTex", velocityScratch); Graphics.Blit(velocityRead, velocityWrite, m, confinePass); Swap(ref velocityRead, ref velocityWrite); } Graphics.Blit(velocityRead, divergence, m, divergencePass); m.SetTexture("_DivergenceTex", divergence); for (int i = 0; i < pressureIterations; i++) { Graphics.Blit(pressureRead, pressureWrite, m, pressurePass); Swap(ref pressureRead, ref pressureWrite); } m.SetTexture("_VelocityTex", velocityRead); Graphics.Blit(pressureRead, velocityWrite, m, projectPass); Swap(ref velocityRead, ref velocityWrite); m.SetTexture("_VelocityTex", velocityRead); m.SetTexture("_OriginalDensity", densityRead); Graphics.Blit(densityRead, densityForward, m, advectMilkPass); Graphics.Blit(densityForward, densityWrite, m, correctMilkPass); Swap(ref densityRead, ref densityWrite);
        }
        private static void Swap(ref RenderTexture a, ref RenderTexture b) { RenderTexture t = a; a = b; b = t; }
        private void OnDisable()
        {
            Spotlight.Audio.GameAudio.StopOwner(this);
            if (image != null && surfaceMaterial != null) { image.material = originalMaterial; image.texture = originalTexture; } if (worldSurface && worldRenderer != null) worldRenderer.sharedMaterial = originalRendererMaterial;
            Release(ref velocityRead); Release(ref velocityWrite); Release(ref velocityScratch); Release(ref pressureRead); Release(ref pressureWrite); Release(ref divergence); Release(ref densityRead); Release(ref densityWrite); Release(ref densityForward); if (simulationMaterial != null) Destroy(simulationMaterial); if (surfaceMaterial != null) Destroy(surfaceMaterial); simulationMaterial = null; surfaceMaterial = null;
        }
        private static void Release(ref RenderTexture rt) { if (rt == null) return; rt.Release(); Destroy(rt); rt = null; }
        public void RestoreAppearanceDefaults() { brushRadius = .02f; pourRate = 5f; pushStrength = .2f; dragStrength = .38f; velocityDamping = 4.2f; viscosity = .00055f; viscosityIterations = 6; diffusion = .1f; vorticityStrength = 1.2f; strokeDepositAssist = .03f; pressureIterations = 40; FoamRelief = 5f; }
    }
}




