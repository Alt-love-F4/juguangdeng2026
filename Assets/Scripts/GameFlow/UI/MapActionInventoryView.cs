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
        private MapSidebarView sidebar;
        public Image PointsFill;
        public Image[] PointsSegments;
        public TextMeshProUGUI DateLabel;
        public RectTransform PointsRect => PointsSegments != null && PointsSegments.Length > 0 && PointsSegments[0] != null
            ? PointsSegments[0].transform.parent as RectTransform
            : PointsFill != null ? PointsFill.rectTransform : points.rectTransform;
        public bool IsVisible => sidebar != null && sidebar.IsVisible;
        public void Toggle() => SetVisible(!IsVisible);
        public void SetVisible(bool value) => sidebar.SetVisible(value);
        public void Initialize(TextMeshProUGUI pointsLabel)
        {
            points = pointsLabel;
            sidebar = GetComponent<MapSidebarView>();
            if (sidebar == null) sidebar = gameObject.AddComponent<MapSidebarView>();
            sidebar.Initialize();
            if (transform.Find("Viewport/Items") is RectTransform existingContent)
            {
                content = existingContent;
                scroll = transform.Find("Viewport").GetComponent<ScrollRect>();
                ConfigureContent();
                signature = "__uninitialized";
                Refresh();
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
            if (grid == null) grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.spacing = new Vector2(10, 10);
            grid.padding = new RectOffset();
            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport.offsetMin = new Vector2(32, 32);
            scroll.viewport.offsetMax = new Vector2(-32, -154);
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
            if (next == signature) return; signature = next;
            for (int childIndex = content.childCount - 1; childIndex >= 0; childIndex--)
            {
                var child = content.GetChild(childIndex).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            slots.Clear(); int index = 0;
            emptyHint = null;
            foreach (var item in items ?? new Dictionary<string, int>())
            {
                var slot = MapActionDetailsView.Rect("Item_" + item.Key, content);
                index++;
                MapVisualTheme.Skin(slot.gameObject.AddComponent<Image>(), MapVisualTheme.Current?.requirement);
                var label = MapActionDetailsView.Label("NameAndCount", slot, MapActionDetailsView.Text("item." + item.Key + ".name", item.Key) + "\n× " + item.Value, 23);
                MapActionDetailsView.Stretch(label.rectTransform, 5); label.alignment = TextAlignmentOptions.Center;
                slots[item.Key] = slot;
            }
            // Only item slots participate in the grid, including the six-slot empty state.
            for (int emptyIndex = index; emptyIndex < 6; emptyIndex++)
            {
                var slot = MapActionDetailsView.Rect("EmptySlot_" + emptyIndex, content);
                MapVisualTheme.Skin(slot.gameObject.AddComponent<Image>(), MapVisualTheme.Current?.requirement);
            }
            grid.padding.bottom = index == 0 ? 84 : 0;
            if (index == 0)
            {
                emptyHint = MapActionDetailsView.Label("Empty", content, "物品栏为空", 18);
                emptyHint.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                emptyHint.alignment = TextAlignmentOptions.Center;
                emptyHint.raycastTarget = false;
            }
            UpdateGridSize();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
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
