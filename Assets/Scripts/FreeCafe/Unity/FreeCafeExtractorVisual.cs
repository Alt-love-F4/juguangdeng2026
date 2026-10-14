using UnityEngine;

namespace Spotlight.FreeCafe
{
    [RequireComponent(typeof(FreeEntityView))]
    public sealed class FreeCafeExtractorVisual : MonoBehaviour
    {
        private FreeEntityView view;
        private Transform assembly;
        private SpriteRenderer lid, grounds;
        private Mesh surface;
        private MeshRenderer liquidRenderer;
        private Material liquidMaterial;
        private LineRenderer drops;
        private readonly Vector3[] vertices = new Vector3[18];
        private readonly Color[] colors = new Color[18];
        private readonly int[] triangles = new int[48];
        private float priorGrounds, loadAnimation, lidOpen, priorDrained, dropLife;
        public bool LidOpen => lidOpen > .5f;
        public float UpperWaterMl { get; private set; }
        public bool Dripping => dropLife > 0;

        private void Awake() { view = GetComponent<FreeEntityView>(); }

        private void LateUpdate()
        {
            if (view.Runtime == null || view.Runtime.World == null) return;
            var world = view.Runtime.World;
            var entity = world.Find(view.EntityId);
            if (entity == null) return;
            bool filter = world.IsExtractionFilter(entity);
            if (!filter && !world.IsPrecisionExtractor(entity)) return;
            if (assembly == null) Build(filter);
            var jug = world.ExtractionJug(entity);
            assembly.localRotation = view.body.transform.localRotation;
            float seconds = Time.unscaledDeltaTime;
            if (!filter)
            {
                bool capped = entity.production.brewCompleted && world.ExtractionFilter(entity) == null;
                lid.enabled = capped;
                lid.transform.localPosition = new Vector3(-.22f, .55f, -.025f);
                Fit(lid, .78f, .29f);
                var attached = world.ExtractionFilter(entity);
                bool filling = attached != null && world.LiquidVolume(attached) > .001f;
                view.SetExtractionFilling(filling);
                if (filling) view.RefreshLiquid();
                lid.sortingOrder = view.body.sortingOrder + 4;
                liquidRenderer.enabled = false;
                return;
            }
            float mass = world.SolidMass(entity);
            if (mass > priorGrounds + .001f) loadAnimation = .28f;
            priorGrounds = mass;
            loadAnimation = Mathf.Max(0, loadAnimation - seconds);
            var kettle = view.Runtime.DraggedView;
            var kettleEntity = kettle != null ? world.Find(kettle.EntityId) : null;
            bool nearby = kettleEntity != null && world.CanPour(kettleEntity)
                && world.LiquidSnapshot(kettleEntity).temperature != LiquidTemperature.Room
                && Vector2.Distance(kettle.transform.position, transform.position) < 2.2f;
            view.label.gameObject.SetActive(jug == null);
            bool receiving = view.IsReceivingPour || jug != null && view.Runtime.View(jug.id) != null
                && view.Runtime.View(jug.id).IsReceivingPour;
            lidOpen = Mathf.MoveTowards(lidOpen, nearby || receiving || loadAnimation > 0 ? 1 : 0, seconds * 12);
            lid.enabled = jug != null;
            lid.transform.localPosition = new Vector3(lidOpen * .24f, .23f + lidOpen * .38f, -.025f);
            lid.transform.localRotation = Quaternion.Euler(0, 0, -lidOpen * 65);
            Fit(lid, .8f, .29f);
            grounds.enabled = mass > 0;
            grounds.transform.localPosition = new Vector3(-.08f, .14f + loadAnimation * 1.4f, -.03f);
            Fit(grounds, .47f, .15f);
            UpperWaterMl = world.LiquidVolume(entity);
            PaintSurface(UpperWaterMl);
            if (entity.production.filterDrainedMl > priorDrained + .001f) dropLife = .18f;
            priorDrained = entity.production.filterDrainedMl;
            dropLife = Mathf.Max(0, dropLife - seconds);
            drops.enabled = jug != null && dropLife > 0;
            if (drops.enabled)
            {
                var jugView = view.Runtime.View(jug.id);
                float fall = Mathf.Repeat(Time.unscaledTime * 6, 1);
                var start = transform.position + Vector3.down * .27f;
                var end = jugView != null ? jugView.transform.position + new Vector3(-.22f, .17f, 0) : start + Vector3.down * .4f;
                drops.SetPosition(0, Vector3.Lerp(start, end, fall));
                drops.SetPosition(1, Vector3.Lerp(start, end, Mathf.Min(1, fall + .17f)));
            }
            if (jug != null && view.Runtime.DraggedView == view.Runtime.View(jug.id))
                transform.position = view.Runtime.View(jug.id).transform.position + new Vector3(-.22f, .91f, 0);
            lid.sortingOrder = view.body.sortingOrder + 4;
            grounds.sortingOrder = view.body.sortingOrder + 1;
            liquidRenderer.sortingOrder = view.body.sortingOrder + 2;
            drops.sortingOrder = view.body.sortingOrder + 3;
        }

        private void Build(bool filter)
        {
            assembly = new GameObject("ExtractionAssembly").transform;
            assembly.SetParent(transform, false);
            lid = SpriteNode("LowerJugLid", "container.extractor.lid");
            grounds = SpriteNode("GroundsBed", "grounds");
            grounds.enabled = false;
            liquidMaterial = new Material(Shader.Find("Sprites/Default"));
            surface = new Mesh { name = "FilterWaterSurface" };
            var water = new GameObject("UpperWater", typeof(MeshFilter), typeof(MeshRenderer));
            water.transform.SetParent(assembly, false);
            water.GetComponent<MeshFilter>().sharedMesh = surface;
            liquidRenderer = water.GetComponent<MeshRenderer>();
            liquidRenderer.sharedMaterial = liquidMaterial;
            var dropObject = new GameObject("GravityDrops", typeof(LineRenderer));
            dropObject.transform.SetParent(assembly, false);
            drops = dropObject.GetComponent<LineRenderer>();
            drops.sharedMaterial = liquidMaterial; drops.positionCount = 2;
            drops.startWidth = .025f; drops.endWidth = .014f;
            drops.startColor = drops.endColor = new Color(.24f, .11f, .045f, .8f);
            drops.enabled = false;
            for (int index = 0; index < 8; index++)
            {
                int offset = index * 6, vertex = index * 2;
                triangles[offset] = vertex; triangles[offset + 1] = vertex + 1; triangles[offset + 2] = vertex + 2;
                triangles[offset + 3] = vertex + 1; triangles[offset + 4] = vertex + 3; triangles[offset + 5] = vertex + 2;
            }
            if (!filter) grounds.enabled = false;
        }

        private SpriteRenderer SpriteNode(string name, string artId)
        {
            var node = new GameObject(name); node.transform.SetParent(assembly, false);
            var renderer = node.AddComponent<SpriteRenderer>();
            renderer.sprite = view.Runtime.artLibrary.Resolve(artId, "default");
            renderer.sortingLayerID = view.body.sortingLayerID;
            return renderer;
        }

        private static void Fit(SpriteRenderer renderer, float width, float height)
        {
            if (renderer.sprite == null) return;
            var size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(width / size.x, height / size.y, 1);
        }

        private void PaintSurface(float volume)
        {
            liquidRenderer.enabled = volume > .001f;
            if (!liquidRenderer.enabled) return;
            float fill = Mathf.Clamp01(volume / 24);
            for (int index = 0; index < 9; index++)
            {
                float fraction = index / 8f;
                float wave = Mathf.Sin(Time.unscaledTime * 15 + fraction * 6) * .008f * fill;
                float edge = Mathf.Sin(fraction * Mathf.PI);
                vertices[index * 2] = new Vector3(-.38f + fraction * .6f, .13f - .025f * edge, -.03f);
                vertices[index * 2 + 1] = new Vector3(-.38f + fraction * .6f, .13f + .055f * fill * edge + wave, -.03f);
                colors[index * 2] = new Color(.46f, .29f, .12f, .68f);
                colors[index * 2 + 1] = new Color(.66f, .80f, .83f, .84f);
            }
            surface.Clear(); surface.vertices = vertices; surface.colors = colors; surface.triangles = triangles;
            surface.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (surface != null) Destroy(surface);
            if (liquidMaterial != null) Destroy(liquidMaterial);
        }
    }
}
