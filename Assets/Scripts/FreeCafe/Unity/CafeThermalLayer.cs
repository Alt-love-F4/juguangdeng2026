using UnityEngine;
using UnityEngine.Rendering;

namespace Spotlight.FreeCafe
{
    /// <summary>One owned visual quad. Shared material is an asset; animation uses a cached property block.</summary>
    internal sealed class CafeThermalLayer
    {
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
        private static readonly int PhaseId = Shader.PropertyToID("_Phase");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int CountId = Shader.PropertyToID("_Count");
        public Transform Transform => root.transform;

        public CafeThermalLayer(Transform parent, string name, Material material, float mode)
        {
            root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(parent, false);
            mesh = new Mesh { name = name + " Quad" };
            mesh.vertices = new[] { new Vector3(-.5f,0,0), new Vector3(.5f,0,0), new Vector3(.5f,1,0), new Vector3(-.5f,1,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0,1,2,0,2,3 };
            mesh.RecalculateBounds();
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            properties.SetFloat(ModeId, mode);
        }

        public void Present(SpriteRenderer body, int orderOffset, float visibility, float clock, float count = 5)
        {
            renderer.enabled = body.enabled && visibility > .001f;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder + orderOffset;
            properties.SetColor(ColorId, body.color);
            properties.SetFloat(VisibilityId, visibility);
            properties.SetFloat(PhaseId, clock);
            properties.SetFloat(CountId, count);
            renderer.SetPropertyBlock(properties);
        }
        public void Hide() { if (renderer != null) renderer.enabled = false; }
        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
            if (mesh != null) Object.Destroy(mesh);
        }
        public static Vector3 SpritePoint(Sprite sprite, Vector2 uv)
        {
            var bounds = sprite.bounds;
            return bounds.min + new Vector3(bounds.size.x * uv.x, bounds.size.y * uv.y, 0);
        }
        public static float DeltaTime(FreeEntityView view) => Time.timeScale > 0 && !view.Runtime.BusinessSimulationPaused
            ? Mathf.Min(Time.deltaTime, .05f) : 0;
    }
}
