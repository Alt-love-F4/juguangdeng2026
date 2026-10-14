using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>Read-only, pooled world-anchored ingredient, heating and extraction cards.</summary>
    [DisallowMultipleComponent]
    public sealed class FreeCafeContainerPanels : MonoBehaviour
    {
        [Header("Paper cards")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Color paper = new Color(.98f, .94f, .85f, .98f);
        [SerializeField] private Color ink = new Color(.25f, .15f, .095f);
        [SerializeField] private Color green = new Color(.51f, .69f, .39f);
        [SerializeField] private Color gold = new Color(.88f, .65f, .22f);
        [SerializeField, Range(.05f, .5f)] private float refreshSeconds = .1f;
        [SerializeField, Range(4, 32)] private int maximumPanels = 16;
        [SerializeField, Range(220, 400)] private float cardWidth = 284;
        [SerializeField, Min(0)] private float screenPadding = 14;
        [SerializeField] private Vector2 anchorOffset = new Vector2(26, 42);

        private FreeCafeRuntime runtime;
        private RectTransform canvasRect;
        private GameObject canvasRoot;
        private string inspectedId;
        private float nextRefresh;
        private readonly List<Card> cards = new List<Card>(16);
        private readonly List<string> ingredients = new List<string>(12);
        private readonly List<string> ingredientLabels = new List<string>(12);
        private readonly List<float> gramsPerUnits = new List<float>(12);
        private readonly List<float> volumes = new List<float>(12);
        private readonly List<float> masses = new List<float>(12);
        private readonly StringBuilder text = new StringBuilder(512);
        private int activeCount;

        private sealed class Gauge
        {
            public RectTransform root, green, gold, pointer;
            public TMP_Text caption;
        }
        private sealed class Card
        {
            public string id;
            public RectTransform root;
            public TMP_Text title, summary, contents, status;
            public Gauge temperature, time, water;
            public Rect occupied;
        }

        public void Initialize(FreeCafeRuntime owner)
        {
            runtime = owner;
            inspectedId = null;
            nextRefresh = 0;
            if (owner != null && canvasRoot == null) BuildCanvas();
        }

        public void Inspect(string id)
        {
            inspectedId = id;
            nextRefresh = 0;
        }

        // Ignore an old pointer-exit arriving after another container's pointer-enter.
        public void ClearInspection(string id)
        {
            if (inspectedId != id) return;
            inspectedId = null;
            nextRefresh = 0;
        }

        private void Update()
        {
            if (runtime == null || runtime.World == null || canvasRoot == null) return;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + Mathf.Max(.05f, refreshSeconds);
            Refresh();
        }

        private void LateUpdate()
        {
            if (canvasRect == null || runtime == null || runtime.World == null || runtime.worldCamera == null) return;
            for (int i = 0; i < activeCount; i++) Position(cards[i], i);
        }

        private void OnDisable()
        {
            if (canvasRoot != null) canvasRoot.SetActive(false);
        }
        private void OnEnable()
        {
            if (canvasRoot != null) canvasRoot.SetActive(true);
            nextRefresh = 0;
        }
        private void OnDestroy()
        {
            if (canvasRoot != null) Destroy(canvasRoot);
        }

        private void BuildCanvas()
        {
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/乐米曲奇方块体 SDF");
            if (font == null && runtime.entityPrefab != null && runtime.entityPrefab.label != null)
                font = runtime.entityPrefab.label.font;
            canvasRoot = new GameObject("ContainerPanels", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 65;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var group = canvasRoot.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            canvasRect = (RectTransform)canvasRoot.transform;
            // No GraphicRaycaster: these secondary panels never consume a pour/drop.
        }

        private void Refresh()
        {
            var world = runtime.World;
            bool visible = world.ProductionEnabled && !runtime.InputBlocked;
            canvasRoot.SetActive(visible);
            activeCount = 0;
            if (visible)
            {
                var inspected = world.Find(inspectedId);
                if (Eligible(inspected)) Show(inspected);
                var entities = world.State.entities;
                for (int i = 0; i < entities.Count && activeCount < maximumPanels; i++)
                {
                    var entity = entities[i];
                    if (entity.id == inspectedId || !Eligible(entity)) continue;
                    bool heating = entity.production != null && entity.production.phase == ProductionPhase.Processing;
                    var extraction = world.ExtractionSnapshot(entity);
                    var filter = world.ExtractionFilter(entity);
                    bool receiving = runtime.View(entity.id).IsReceivingPour
                        || filter != null && (world.LiquidVolume(filter) > .001f
                        || runtime.View(filter.id) != null && runtime.View(filter.id).IsReceivingPour);
                    if (heating || receiving) Show(entity);
                }
            }
            for (int i = activeCount; i < cards.Count; i++) cards[i].root.gameObject.SetActive(false);
        }

        private bool Eligible(WorldEntity entity)
        {
            return entity != null && entity.area == runtime.World.State.currentArea && runtime.World.IsContainer(entity)
                && runtime.View(entity.id) != null;
        }

        private void Show(WorldEntity entity)
        {
            entity = runtime.World.ExtractionJug(entity) ?? entity;
            for (int index = 0; index < activeCount; index++) if (cards[index].id == entity.id) return;
            if (activeCount >= maximumPanels) return;
            if (activeCount == cards.Count) cards.Add(BuildCard());
            var card = cards[activeCount++];
            card.id = entity.id;
            card.root.gameObject.SetActive(true);
            var world = runtime.World;
            var snapshot = world.LiquidSnapshot(entity);
            bool heating = entity.production != null && entity.production.phase == ProductionPhase.Processing;
            var extraction = world.ExtractionSnapshot(entity);
            bool extractionVisible = extraction.available && (extraction.groundsGrams > 0 || extraction.inProgress || extraction.completed);
            card.title.text = world.Spec(entity.specId).label;
            text.Clear().Append(snapshot.temperatureCelsius.ToString("0.#")).Append(" °C")
                .Append("    ").Append(snapshot.volumeMl.ToString("0.#")).Append(" / ").Append(snapshot.capacityMl.ToString("0.#")).Append(" ml");
            Assign(card.summary, text);
            UpdateTemperatureGauge(card.temperature,snapshot.temperatureCelsius);
            BuildIngredients(entity);
            Assign(card.contents, text);
            card.time.root.gameObject.SetActive(heating || extractionVisible);
            card.water.root.gameObject.SetActive(extractionVisible);
            if (extractionVisible)
            {
                var filter=world.ExtractionFilter(entity);
                float remainingGrounds=filter!=null ? world.SolidMass(filter) : 0;
                text.Clear().Append(extraction.inProgress ? "萃取进行中" : extraction.completed ? "萃取完成" : "待注入热水")
                    .Append(" · 剩余咖啡粉 ").Append(remainingGrounds.ToString("0.#")).Append(" g");
                Assign(card.status, text);
                UpdateGauge(card.time, "时间", extraction.elapsedSeconds, extraction.idealSeconds, extraction.greenTimeMin, extraction.greenTimeMax,
                    extraction.goldTimeMin, extraction.goldTimeMax, "s");
                UpdateGauge(card.water, "热水", extraction.injectedHotWaterMl, extraction.idealMl, extraction.greenMlMin, extraction.greenMlMax,
                    extraction.goldMlMin, extraction.goldMlMax, "ml");
            }
            else if (heating)
            {
                float duration = Mathf.Max(.001f, world.Catalog.production.heatSeconds);
                float elapsed = Mathf.Clamp(duration - entity.production.remaining, 0, duration);
                text.Clear().Append("加热中 · ").Append(Mathf.RoundToInt(elapsed / duration * 100)).Append("%");
                Assign(card.status, text);
                UpdateGauge(card.time, "加热", elapsed, duration, 0, 0, 0, 0, "s", true);
                card.time.green.gameObject.SetActive(false);
                card.time.gold.gameObject.SetActive(false);
            }
            else card.status.text = world.CompositionName(entity);
            float contentHeight = Mathf.Max(44, ingredients.Count * 20 + 6);
            card.contents.rectTransform.sizeDelta = new Vector2(card.contents.rectTransform.sizeDelta.x, contentHeight);
            float gaugeTop = 99 + contentHeight + 10;
            MoveGauge(card.temperature.root,gaugeTop);
            gaugeTop+=50;
            MoveGauge(card.time.root, gaugeTop);
            MoveGauge(card.water.root, gaugeTop + 50);
            float height = extractionVisible ? gaugeTop + 95 : heating ? gaugeTop + 45 : gaugeTop;
            card.root.sizeDelta = new Vector2(cardWidth, height);
        }

        private void BuildIngredients(WorldEntity entity)
        {
            ingredients.Clear(); ingredientLabels.Clear();gramsPerUnits.Clear();volumes.Clear(); masses.Clear();
            var world = runtime.World;
            var filter = world.ExtractionFilter(entity);
            int lowerCount = entity.contents.Count;
            int count = lowerCount + (filter != null ? filter.contents.Count : 0);
            for (int i = 0; i < count; i++)
            {
                var portion = i < lowerCount ? entity.contents[i] : filter.contents[i - lowerCount];
                // Temperature variants are the same ingredient, not extra inventory rows.
                string key = world.IngredientIdentity(portion);
                int index = ingredients.IndexOf(key);
                if (index < 0)
                {
                    ingredients.Add(key);ingredientLabels.Add(world.IngredientLabel(portion));gramsPerUnits.Add(world.IngredientDefinition(portion.specId)?.gramsPerUnit ?? 12); volumes.Add(portion.milliliters); masses.Add(portion.grams);
                }
                else { volumes[index] += portion.milliliters; masses[index] += portion.grams; }
            }
            text.Clear();
            if (ingredients.Count == 0) { text.Append("空容器\n可自由加入原料"); return; }
            for (int i = 0; i < ingredients.Count; i++)
            {
                if (i > 0) text.Append('\n');
                if (ingredients[i] == "milkfoam:" && world.IsLatteArtCup(entity) && entity.latteArt)
                {
                    text.Append(FreeCafeProductionText.LatteArtContents(volumes[i], entity.latteArtPattern));
                    continue;
                }
                text.Append(ingredientLabels[i]).Append("   ");
                if (volumes[i] > 0) text.Append(volumes[i].ToString("0.#")).Append(" ml");
                if (masses[i] > 0)
                {
                    if (volumes[i] > 0) text.Append(" + ");
                    text.Append((masses[i] / Mathf.Max(.001f,gramsPerUnits[i])).ToString("0.#")).Append(" 份 · ")
                        .Append(masses[i].ToString("0.#")).Append(" g");
                }
                if (volumes[i] <= 0 && masses[i] <= 0) text.Append("0 g");
            }
        }

        private void UpdateTemperatureGauge(Gauge gauge,float celsius)
        {
            text.Clear().Append("温度  ").Append(celsius.ToString("0.#")).Append(" °C    0 ─ 50 ─ 100");
            Assign(gauge.caption,text);
            float position=Mathf.Clamp01(celsius/100f);
            gauge.pointer.anchorMin=new Vector2(position,0);gauge.pointer.anchorMax=new Vector2(position,1);
            var indicator=gauge.pointer.GetComponent<Image>();
            indicator.color=Color.Lerp(new Color(.26f,.55f,.72f),new Color(.86f,.29f,.14f),position);
        }

        private static void MoveGauge(RectTransform root, float y)
        {
            root.offsetMin = new Vector2(14, -y - 38);
            root.offsetMax = new Vector2(-14, -y);
        }

        private void UpdateGauge(Gauge gauge, string label, float value, float ideal, float greenMin, float greenMax, float goldMin, float goldMax, string unit, bool fullRange = false)
        {
            float limit = Mathf.Max(.001f, fullRange ? ideal : Mathf.Max(ideal * 1.5f, greenMax * 1.15f));
            text.Clear().Append(label).Append("  ").Append(value.ToString("0.#")).Append(" / ").Append(ideal.ToString("0.#")).Append(' ').Append(unit);
            Assign(gauge.caption, text);
            SetBand(gauge.green, greenMin / limit, greenMax / limit);
            SetBand(gauge.gold, goldMin / limit, goldMax / limit);
            gauge.green.gameObject.SetActive(greenMax > greenMin);
            gauge.gold.gameObject.SetActive(goldMax > goldMin);
            float position = Mathf.Clamp01(value / limit);
            gauge.pointer.anchorMin = new Vector2(position, 0);
            gauge.pointer.anchorMax = new Vector2(position, 1);
        }

        private static void SetBand(RectTransform rect, float minimum, float maximum)
        {
            rect.anchorMin = new Vector2(Mathf.Clamp01(minimum), 0);
            rect.anchorMax = new Vector2(Mathf.Clamp01(maximum), 1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Assign(TMP_Text target, StringBuilder builder)
        {
            string prior = target.text;
            bool same = prior != null && prior.Length == builder.Length;
            for (int i = 0; same && i < builder.Length; i++) same = prior[i] == builder[i];
            if (!same) target.SetText(builder);
        }

        private Card BuildCard()
        {
            var root = new GameObject("ContainerCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(CafeHudSurface));
            root.transform.SetParent(canvasRect, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(0, 1);
            var surface = root.GetComponent<CafeHudSurface>();
            surface.color = paper; surface.border = new Color(.45f, .30f, .20f); surface.radius = 12; surface.raycastTarget = false;
            var card = new Card { root = rect };
            card.title = Label(rect, "Title", 18, 14, 12, 30, true);
            card.summary = Label(rect, "TemperatureAndFill", 14, 14, 43, 24, false);
            card.status = Label(rect, "ProcessStatus", 14, 14, 71, 24, true);
            card.contents = Label(rect, "Ingredients", 14, 14, 99, 106, false);
            card.temperature=BuildGauge(rect,"Temperature",211);
            card.temperature.green.gameObject.SetActive(false);card.temperature.gold.gameObject.SetActive(false);
            var temperatureTrack=card.temperature.pointer.parent as RectTransform;
            for(int i=0;i<=10;i++)
            {
                var tick=Graphic(temperatureTrack,"Tick"+(i*10),new Color(.45f,.35f,.26f,.65f));
                tick.anchorMin=new Vector2(i/10f,0);tick.anchorMax=new Vector2(i/10f,1);
                tick.sizeDelta=new Vector2(1,i%5==0 ? 4 : -4);tick.anchoredPosition=Vector2.zero;
            }
            card.temperature.pointer.SetAsLastSibling();
            card.time = BuildGauge(rect, "Time", 261);
            card.water = BuildGauge(rect, "Water", 311);
            return card;
        }

        private TMP_Text Label(Transform parent, string name, float size, float x, float y, float height, bool bold)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(0, 1);
            rect.offsetMin = new Vector2(x, -y - height); rect.offsetMax = new Vector2(-14, -y);
            var label = obj.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = size; label.color = ink; label.raycastTarget = false;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private Gauge BuildGauge(RectTransform parent, string name, float y)
        {
            var root = new GameObject(name + "Gauge", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(0, 1);
            rect.offsetMin = new Vector2(14, -y - 38); rect.offsetMax = new Vector2(-14, -y);
            var caption = Label(rect, "Caption", 13, 0, 0, 22, false);
            var track = Graphic(rect, "WhiteTrack", Color.white);
            track.anchorMin = new Vector2(0, 0); track.anchorMax = new Vector2(1, 0); track.pivot = Vector2.zero;
            track.offsetMin = Vector2.zero; track.offsetMax = new Vector2(0, 12);
            var greenBand = Graphic(track, "GreenZone", green);
            var goldBand = Graphic(track, "GoldZone", gold);
            var pointer = Graphic(track, "Pointer", ink);
            pointer.pivot = new Vector2(.5f, .5f); pointer.sizeDelta = new Vector2(3, 8); pointer.anchoredPosition = Vector2.zero;
            return new Gauge { root = rect, caption = caption, green = greenBand, gold = goldBand, pointer = pointer };
        }

        private static RectTransform Graphic(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return image.rectTransform;
        }

        private void Position(Card card, int index)
        {
            var view = runtime.View(card.id);
            if (view == null) { card.root.gameObject.SetActive(false); return; }
            var screen = runtime.worldCamera.WorldToScreenPoint(view.transform.position);
            if (screen.z <= 0) { card.root.gameObject.SetActive(false); return; }
            card.root.gameObject.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var anchor);
            Rect bounds = canvasRect.rect;
            Vector2 size = card.root.sizeDelta;
            Vector2 point = anchor + anchorOffset + Vector2.up * size.y;
            if (point.x + size.x > bounds.xMax - screenPadding) point.x = anchor.x - anchorOffset.x - size.x;
            point.x = Mathf.Clamp(point.x, bounds.xMin + screenPadding, Mathf.Max(bounds.xMin + screenPadding, bounds.xMax - size.x - screenPadding));
            point.y = Mathf.Clamp(point.y, bounds.yMin + size.y + screenPadding, Mathf.Max(bounds.yMin + size.y + screenPadding, bounds.yMax - screenPadding));
            // A bounded pass avoids cards directly covering one another when vessels sit together.
            for (int j = 0; j < index; j++)
            {
                var occupied = cards[j].occupied;
                var proposed = new Rect(point.x, point.y - size.y, size.x, size.y);
                if (!proposed.Overlaps(occupied)) continue;
                float right = occupied.xMax + 8;
                float left = occupied.xMin - size.x - 8;
                if (right + size.x <= bounds.xMax - screenPadding) point.x = right;
                else if (left >= bounds.xMin + screenPadding) point.x = left;
                else if (occupied.yMin - size.y - 8 >= bounds.yMin + screenPadding) point.y = occupied.yMin - 8;
            }
            card.root.anchoredPosition = point;
            card.occupied = new Rect(point.x, point.y - size.y, size.x, size.y);
        }
    }
}
