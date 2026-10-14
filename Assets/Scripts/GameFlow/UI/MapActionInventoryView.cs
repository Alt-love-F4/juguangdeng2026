using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed class MapActionInventoryView : MonoBehaviour
    {
        private ScrollRect scroll;
        private RectTransform content;
        private GridLayoutGroup grid;
        private TextMeshProUGUI emptyHint;
        private TextMeshProUGUI points;
        private readonly Dictionary<string, RectTransform> slots = new Dictionary<string, RectTransform>();
        private string signature = "__uninitialized";
        private float nextRefresh;
        private bool initialized;
        private MapSidebarView sidebar;
        [SerializeField] private bool showEditorPreview = true;
        [SerializeField] private RectTransform itemSlotTemplate;
        [SerializeField] private RectTransform emptySlotTemplate;
        public Image PointsFill;
        public Image[] PointsSegments;
        public TextMeshProUGUI DateLabel;
        public RectTransform PointsRect => PointsSegments != null && PointsSegments.Length > 0 && PointsSegments[0] != null
            ? PointsSegments[0].transform.parent as RectTransform
            : PointsFill != null ? PointsFill.rectTransform : points.rectTransform;
        public bool IsVisible => sidebar != null && sidebar.IsVisible;
        public void Toggle() => SetVisible(!IsVisible);
        public void SetVisible(bool value)
        {
            if (sidebar == null) Initialize(points);
            sidebar.SetVisible(value);
        }
        public void Initialize(TextMeshProUGUI pointsLabel)
        {
            points = pointsLabel;
            // Corrupted/legacy prefab references can point back to this component or its
            // own panel. Accept only the authored slot objects; never clone the panel itself.
            if (itemSlotTemplate != null && (itemSlotTemplate == transform || itemSlotTemplate.IsChildOf(transform)))
                itemSlotTemplate = itemSlotTemplate.name == "ItemSlotTemplate" ? itemSlotTemplate : null;
            if (emptySlotTemplate != null && (emptySlotTemplate == transform || emptySlotTemplate.IsChildOf(transform)))
                emptySlotTemplate = emptySlotTemplate.name.StartsWith("EmptySlot_", StringComparison.Ordinal) ? emptySlotTemplate : null;
            sidebar = GetComponent<MapSidebarView>();
            if (sidebar == null) sidebar = gameObject.AddComponent<MapSidebarView>();
            sidebar.Initialize();
            if (transform.Find("Viewport/Items") is RectTransform existingContent)
            {
                content = existingContent;
                scroll = transform.Find("Viewport").GetComponent<ScrollRect>();
                ConfigureContent();
                signature = "__uninitialized";
                if (Application.isPlaying || !showEditorPreview) Refresh();
                else
                {
                    signature = "__editor-preview";
                    if (content.childCount > 0) emptySlotTemplate = content.GetChild(0) as RectTransform;
                    UpdateGridSize();
                }
                return;
            }
            MapVisualTheme.Skin(GetComponent<Image>(), MapVisualTheme.Current?.panel);
            var rect = (RectTransform)transform;
            var title = MapVisualTheme.Art("InventoryTitle", rect, MapVisualTheme.Current?.inventoryTitle, Vector2.zero, new Vector2(430, 114));
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(.5f, 1);
            title.rectTransform.anchoredPosition = new Vector2(0, -78);
            var viewport = MapActionDetailsView.Rect("Viewport", rect); MapActionDetailsView.Stretch(viewport, 16);
            viewport.offsetMax = new Vector2(-16, -102);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            content = MapActionDetailsView.Rect("Items", viewport);
            content.anchorMin = content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 1);
            scroll.content = content; ConfigureContent(); Refresh();
        }
        private void ConfigureContent()
        {
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            grid = content.GetComponent<GridLayoutGroup>();
            bool createdGrid = grid == null;
            if (createdGrid) grid = content.gameObject.AddComponent<GridLayoutGroup>();
            // Existing Prefab layout is authored data; defaults are for generated UIs only.
            if (createdGrid)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 3;
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.childAlignment = TextAnchor.UpperLeft;
                grid.spacing = new Vector2(10, 10);
                grid.padding = new RectOffset();
            }
            var fitter = content.GetComponent<ContentSizeFitter>();
            bool createdFitter = fitter == null;
            if (createdFitter) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            if (createdFitter)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scroll.viewport.offsetMin = new Vector2(32, 32);
                scroll.viewport.offsetMax = new Vector2(-32, -154);
            }
        }
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .2f; Refresh();
        }
        public void Refresh()
        {
            if (content == null || scroll == null) return;
            UpdateGridSize();
            var runtime = NarrativeRuntime.Instance;
            // The fixed HUD is refreshed by MapActionPointsView even when this panel is hidden.
            var items = runtime?.MapInventory?.Items;
            string next = items != null ? string.Join("|", items.Select(p => p.Key + ":" + p.Value)) : "empty";
            if (itemSlotTemplate == null && content.Find("ItemSlotTemplate") is RectTransform authoredItem) itemSlotTemplate = authoredItem;
            if (emptySlotTemplate == null && content.Find("EmptySlot_0") is RectTransform authoredEmpty) emptySlotTemplate = authoredEmpty;
            if (signature == next && initialized) return;
            signature = next;
            initialized = true;
            for (int childIndex = content.childCount - 1; childIndex >= 0; childIndex--)
            {
                var child = content.GetChild(childIndex).gameObject;
                if (itemSlotTemplate != null && child == itemSlotTemplate.gameObject) continue;
                if (emptySlotTemplate != null && child == emptySlotTemplate.gameObject)
                {
                    child.SetActive(!Application.isPlaying && showEditorPreview);
                    continue;
                }
                if (child.name.StartsWith("EmptySlot_", StringComparison.Ordinal))
                {
                    if (Application.isPlaying || !showEditorPreview)
                    {
                        child.SetActive(false);
                        if (emptySlotTemplate == null || child != emptySlotTemplate.gameObject)
                        {
                            if (Application.isPlaying) Destroy(child);
                            else DestroyImmediate(child);
                        }
                    }
                    continue;
                }
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            slots.Clear(); int index = 0;
            emptyHint = null;
            foreach (var item in items ?? new Dictionary<string, int>())
            {
                RectTransform slot;
                if (itemSlotTemplate != null) slot = Instantiate(itemSlotTemplate, content, false);
                else slot = CreateEditorFallbackSlot();
                slot.gameObject.SetActive(true); slot.name = "Item_" + item.Key;
                var label = slot.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = MapActionDetailsView.Text("item." + item.Key + ".name", item.Key) + "\n× " + item.Value;
                index++;
                slots[item.Key] = slot;
            }
            // Empty slots use the authored visual and are cloned, never assembled at runtime.
            int emptySlotCount = Mathf.Max(0, 6 - index);
            for (int slotIndex = 0; slotIndex < emptySlotCount; slotIndex++)
            {
                int emptyIndex = index + slotIndex;
                RectTransform slot;
                if (emptySlotTemplate != null) slot = Instantiate(emptySlotTemplate, content, false);
                else
                {
                    slot = MapActionDetailsView.Rect("EmptySlot_" + emptyIndex, content);
                    slot.sizeDelta = grid.cellSize;
                    slot.gameObject.AddComponent<Image>().color = new Color(.96f, .90f, .71f);
                }
                slot.gameObject.SetActive(true); slot.name = "EmptySlot_" + emptyIndex;
            }
            bool showPreviewTemplates = !Application.isPlaying && showEditorPreview;
            if (itemSlotTemplate != null) itemSlotTemplate.gameObject.SetActive(showPreviewTemplates);
            if (emptySlotTemplate != null) emptySlotTemplate.gameObject.SetActive(showPreviewTemplates);
            grid.padding.bottom = index == 0 ? 84 : 0;
            if (index == 0)
            {
                emptyHint = content.Find("Empty")?.GetComponent<TextMeshProUGUI>();
                if (emptyHint == null) emptyHint = MapActionDetailsView.Label("Empty", content, "物品栏为空", 18);
                else emptyHint.text = "物品栏为空";
                var emptyLayout = emptyHint.GetComponent<LayoutElement>();
                if (emptyLayout == null) emptyLayout = emptyHint.gameObject.AddComponent<LayoutElement>();
                emptyLayout.ignoreLayout = true;
                emptyHint.alignment = TextAlignmentOptions.Center;
                emptyHint.raycastTarget = false;
                emptyHint.gameObject.SetActive(!Application.isPlaying && showEditorPreview);
            }
            UpdateGridSize();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
        private RectTransform CreateEditorFallbackSlot()
        {
            var slot = MapActionDetailsView.Rect("ItemSlot", content);
            slot.sizeDelta = grid.cellSize;
            var image = slot.gameObject.AddComponent<Image>();
            image.color = new Color(.96f, .90f, .71f);
            var label = MapActionDetailsView.Label("Label", slot, "物品", 18);
            MapActionDetailsView.Stretch(label.rectTransform, 8);
            label.alignment = TextAlignmentOptions.Center;
            return slot;
        }

        private void UpdateGridSize()
        {
            float width = Mathf.Max(1, scroll.viewport.rect.width);
            // Reduce the gaps too on very narrow canvases so three cells always fit.
            float gap = Mathf.Min(10, width / 6);
            float size = Mathf.Max(0, (width - gap * 2) / 3);
            var cellSize = new Vector2(size, size);
            if (grid.cellSize != cellSize || grid.spacing.x != gap)
            {
                grid.spacing = new Vector2(gap, gap);
                grid.cellSize = cellSize;
            }
            if (emptyHint != null)
            {
                var hintRect = emptyHint.rectTransform;
                hintRect.anchorMin = new Vector2(0, 1);
                hintRect.anchorMax = new Vector2(1, 1);
                hintRect.pivot = new Vector2(.5f, 1);
                hintRect.anchoredPosition = new Vector2(0, -(2 * size + gap + 24));
                hintRect.sizeDelta = new Vector2(0, 60);
            }
        }
        public RectTransform FindSlot(string key) => slots.TryGetValue(key, out var slot) ? slot : null;
        public IEnumerator Reveal(string key)
        {
            if (!IsVisible)
            {
                transform.parent.GetComponentInChildren<MapTaskPanel>(true)?.SetVisible(false);
                SetVisible(true);
                yield return new WaitForSecondsRealtime(.23f);
            }
            Refresh(); Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var slot = FindSlot(key); if (slot == null) yield break;
            scroll.StopMovement();
            float max = Mathf.Max(0, content.rect.height - scroll.viewport.rect.height);
            float top = content.InverseTransformPoint(slot.TransformPoint(new Vector3(0, slot.rect.yMax, 0))).y;
            float target = max > 0 ? 1f - Mathf.Clamp01(-top / max) : 1;
            float start = scroll.verticalNormalizedPosition;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / .25f)
            {
                scroll.verticalNormalizedPosition = Mathf.Lerp(start, target, t); yield return null;
            }
            scroll.verticalNormalizedPosition = target;
        }
    }
}
