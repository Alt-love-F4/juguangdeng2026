using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>PSD has separate complete top cups and open-rim lower cups. Stock stays authoritative.</summary>
    [ExecuteAlways]
    public sealed class FreeCafeCupStackVisual : MonoBehaviour
    {
        [Min(0f), InspectorName("杯子层间距"), Tooltip("相邻两只杯子中心的距离，单位为杯堆本地单位。修改后无需运行游戏即可预览。")]
        public float cupSpacing = .45f;
        [Min(1), InspectorName("最大显示堆叠数"), Tooltip("仅限制来源杯堆的可见杯数，不改变实际库存。大小杯堆可分别设置，未运行时也能预览。")]
        public int maxVisibleCups = 5;
        [SerializeField, HideInInspector] private bool spacingConfigured;
        private bool refreshEditorPreview;
        private FreeEntityView view;
        private readonly List<SpriteRenderer> lower = new List<SpriteRenderer>();
        public int VisibleCupCount { get; private set; }
        private EntitySpec cupSpec;
        private CafeCatalog lastCatalog;
        private bool discoveredLayers, pickupReserved;
        private EntitySpec lastSpec;
        private CafeArtLibrary lastArt;
        private int lastStock;
        private bool lastHasCupAbove;
        public float StackStep { get; private set; }
        public Vector3 TopPosition => view != null && view.body != null ? view.body.transform.position : transform.position;
        public void ReserveTopCup(bool reserved)
        {
            if (pickupReserved == reserved) return;
            pickupReserved = reserved;
            if (lastSpec != null) Present(lastSpec, lastCatalog, lastArt, lastStock, lastHasCupAbove);
        }
        private void Awake() { view = GetComponent<FreeEntityView>(); }
        private void OnEnable() { refreshEditorPreview = true; }
        private void OnValidate()
        {
            cupSpacing = Mathf.Max(0, cupSpacing);
            maxVisibleCups = Mathf.Max(1, maxVisibleCups);
            refreshEditorPreview = true;
        }
        private void Update()
        {
            if (!Application.IsPlaying(gameObject) && refreshEditorPreview)
            {
                refreshEditorPreview = false;
                RefreshAuthoringPreview();
            }
        }
        [ContextMenu("刷新初始杯堆预览")]
        public void RefreshAuthoringPreview()
        {
            if (Application.IsPlaying(gameObject)) return;
            if (view == null) view = GetComponent<FreeEntityView>();
            if (view == null || (view.PrefabSpecId != "source.cups.small" && view.PrefabSpecId != "source.cups.large")) return;
            var asset = Resources.Load<TextAsset>("FreeCafe/catalog");
            var art = Resources.Load<CafeArtLibrary>("FreeCafe/ArtLibrary");
            if (asset == null || art == null) return;
            var catalog = JsonUtility.FromJson<CafeCatalog>(asset.text);
            foreach (var spec in catalog.entities)
                if (spec.id == view.PrefabSpecId)
                {
                    Present(spec, catalog, art, spec.initialStock, false);
                    if (view.label != null) view.label.text = spec.label + " [" + spec.initialStock + "]";
                    break;
                }
        }
        public void Present(EntitySpec spec, CafeCatalog catalog, CafeArtLibrary art, int stock, bool hasCupAbove)
        {
            if (view == null) view = GetComponent<FreeEntityView>();
            lastSpec = spec; lastArt = art; lastStock = stock; lastHasCupAbove = hasCupAbove;
            bool source = spec.id == "source.cups.small" || spec.id == "source.cups.large";
            string cupId = source ? spec.sourceItemId : spec.id;
            if (cupSpec == null || lastCatalog != catalog || cupSpec.id != cupId)
            {
                lastCatalog = catalog; cupSpec = null;
                foreach (var entry in catalog.entities) if (entry.id == cupId) { cupSpec = entry; break; }
            }
            if (!discoveredLayers)
            {
                discoveredLayers = true;
                for (int i = 0; ; i++)
                {
                    var child = transform.Find("NestedCup" + i); if (child == null) break;
                    var renderer = child.GetComponent<SpriteRenderer>(); if (renderer != null) lower.Add(renderer);
                }
            }
            var cup = cupSpec;
            if (cup == null || view.body == null) return;
            var top = art.Resolve(cup.artId);
            var nested = art.Resolve(cup.artId, "nested");
            if (top == null || nested == null) return;
            // Fixed author-controlled pitch: stock changes never move the remaining bottom cups.
            if (!spacingConfigured)
            {
                cupSpacing = cupId == "cup.large" ? .7f : .45f;
                spacingConfigured = true;
            }
            StackStep = source ? Mathf.Max(0, cupSpacing) : cup.height * (cupId == "cup.large" ? .46f : .43f);
            // Reserve against real stock before capping, so taking 6 -> 5 at cap 5 stays full.
            int remainingStock = Mathf.Max(0, stock - (pickupReserved ? 1 : 0));
            VisibleCupCount = source ? Mathf.Min(remainingStock, Mathf.Max(1, maxVisibleCups)) : 1;
            view.body.sprite = !source && hasCupAbove && !pickupReserved ? nested : top;
            Fit(view.body, cup.width, cup.height);
            float extra = source ? Mathf.Max(0, VisibleCupCount - 1) * StackStep : 0;
            view.body.transform.localPosition = new Vector3(0, extra, view.body.transform.localPosition.z);
            if (source) view.body.enabled = VisibleCupCount > 0;
            for (int i = 0; i < Mathf.Max(lower.Count, VisibleCupCount - 1); i++)
            {
                if (i == lower.Count)
                {
                    var node = transform.Find("NestedCup" + i);
                    if (node == null) { node = new GameObject("NestedCup" + i).transform; node.SetParent(transform, false); }
                    var renderer = node.GetComponent<SpriteRenderer>();
                    if (renderer == null) renderer = node.gameObject.AddComponent<SpriteRenderer>();
                    lower.Add(renderer);
                }
                var item = lower[i]; item.gameObject.SetActive(source && i < VisibleCupCount - 1);
                item.sprite = nested; item.transform.localPosition = new Vector3(0, i * StackStep, -.001f * (i + 1));
                Fit(item, cup.width, cup.height);
            }
            if (source)
            {
                view.hitbox.size = new Vector2(cup.width, cup.height + extra);
                view.hitbox.offset = new Vector2(0, extra * .5f);
                if (view.label != null) view.label.transform.localPosition = new Vector3(0, -cup.height * .5f - .18f, -.1f);
            }
            SyncRenderers();
        }
        private static void Fit(SpriteRenderer renderer, float width, float height)
        {
            var size = renderer.sprite.bounds.size; float fit = Mathf.Min(width / size.x, height / size.y);
            renderer.transform.localScale = new Vector3(fit, fit, 1);
        }
        private void LateUpdate() { SyncRenderers(); }
        private void SyncRenderers()
        {
            if (view == null || view.body == null) return;
            for (int i = 0; i < lower.Count; i++)
            {
                lower[i].sortingLayerID = view.body.sortingLayerID;
                lower[i].sortingOrder = view.body.sortingOrder + Mathf.Max(0, VisibleCupCount - 1 - i);
                lower[i].color = view.body.color;
                lower[i].transform.localRotation = view.body.transform.localRotation;
            }
        }
    }
}
