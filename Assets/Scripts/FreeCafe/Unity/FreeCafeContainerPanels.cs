using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>
    /// Read-only, pooled world-anchored container cards plus device gauges: vertical time / hot-water
    /// judgment bars beside the extractor and progress bars above processing devices.
    /// Everything lives on a canvas without a GraphicRaycaster so pours and drops are never consumed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FreeCafeContainerPanels : MonoBehaviour
    {
        [Header("Paper cards")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private TMP_FontAsset titleFont;
        [SerializeField] private Sprite paperFrame;
        [Tooltip("Sliced frame multiplier; larger values draw the frame ornaments smaller.")]
        [SerializeField, Range(1, 5)] private float frameScale = 2.6f;
        [SerializeField] private Color paper = new Color(.98f, .94f, .85f, .98f);
        [SerializeField] private Color ink = new Color(.25f, .15f, .095f);
        [SerializeField] private Color accent = new Color(.55f, .22f, .11f);
        [SerializeField] private Color green = new Color(.51f, .62f, .36f);
        [SerializeField] private Color gold = new Color(.88f, .63f, .22f);
        [SerializeField, Range(.05f, .5f)] private float refreshSeconds = .1f;
        [SerializeField, Range(4, 32)] private int maximumPanels = 16;
        [SerializeField, Range(220, 400)] private float cardWidth = 300;
        [SerializeField, Min(0)] private float screenPadding = 14;
        [SerializeField] private Vector2 anchorOffset = new Vector2(26, 42);
        [Header("Device gauges")]
        [SerializeField, Range(14, 48)] private float judgmentWidth = 24;
        [SerializeField, Range(.4f, 1)] private float judgmentHeight = .72f;
        [SerializeField, Range(80, 260)] private float progressWidth = 168;

        private FreeCafeRuntime runtime;
        private FreeCafeProductionHud hud;
        private readonly Vector3[] hudCorners = new Vector3[4];
        private RectTransform canvasRect;
        private GameObject canvasRoot;
        private string inspectedId;
        private float nextRefresh;
        private readonly List<Card> cards = new List<Card>(16);
        private readonly List<Progress> progress = new List<Progress>(4);
        private readonly List<Judgment> judgments = new List<Judgment>(2);
        private readonly List<string> ingredients = new List<string>(12);
        private readonly List<string> ingredientLabels = new List<string>(12);
        private readonly List<float> gramsPerUnits = new List<float>(12);
        private readonly List<float> volumes = new List<float>(12);
        private readonly List<float> masses = new List<float>(12);
        private readonly StringBuilder text = new StringBuilder(512);
        private int activeCount, progressCount, judgmentCount;

        private sealed class Card
        {
            public string id;
            public RectTransform root, fillBar, fill, temperature, pointer;
            public TMP_Text title, summary, contents, status, temperatureCaption;
            public Rect occupied;
        }
        private sealed class Progress
        {
            public string id;
            public RectTransform root, fill;
            public TMP_Text caption;
        }
        private sealed class Gauge
        {
            public RectTransform root, green, gold, level, pointer;
            public Image levelImage;
            public TMP_Text caption;
            public RectTransform tutorialFocus;
            public Image tutorialFocusImage;
        }
        private sealed class Judgment
        {
            public string id;
            public Gauge time, water;
        }

        public void Initialize(FreeCafeRuntime owner)
        {
            runtime = owner;
            hud=owner!=null ? owner.GetComponent<FreeCafeProductionHud>() : null;
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
            for (int i = 0; i < progressCount; i++) PositionProgress(progress[i]);
            for (int i = 0; i < judgmentCount; i++)
            {
                var judgment = judgments[i];
                PositionJudgment(judgment);
                bool guided = runtime.WorkstationTutorial != null && runtime.WorkstationTutorial.HighlightsExtractionGauges(judgment.id);
                HighlightGauge(judgment.time, guided);
                HighlightGauge(judgment.water, guided);
            }
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
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/Map/SourceHanSansSC-Regular-2 SDF");
            if (font == null && runtime.entityPrefab != null && runtime.entityPrefab.label != null)
                font = runtime.entityPrefab.label.font;
            if (titleFont == null) titleFont = Resources.Load<TMP_FontAsset>("Fonts/Map/SourceHanSerifCN-Heavy-4 SDF");
            if (titleFont == null) titleFont = font;
            canvasRoot = new GameObject("ContainerPanels", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 720;
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
            activeCount = progressCount = judgmentCount = 0;
            if (visible)
            {
                var inspected = world.Find(inspectedId);
                if (Eligible(inspected)) Show(inspected);
                var entities = world.State.entities;
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    if (!Eligible(entity)) continue;
                    var machine = world.MachineProgress(entity);
                    if (machine.active) ShowProgress(entity, machine);
                    if (world.IsPrecisionExtractor(entity)) ShowJudgment(entity);
                    if (entity.id == inspectedId || activeCount >= maximumPanels) continue;
                    // Pouring into the extractor keeps its card open without hovering it.
                    var filter = world.ExtractionFilter(entity);
                    bool receiving = runtime.View(entity.id).IsReceivingPour
                        || filter != null && (world.LiquidVolume(filter) > .001f
                        || runtime.View(filter.id) != null && runtime.View(filter.id).IsReceivingPour);
                    if (receiving) Show(entity);
                }
            }
            for (int i = activeCount; i < cards.Count; i++) cards[i].root.gameObject.SetActive(false);
            for (int i = progressCount; i < progress.Count; i++) progress[i].root.gameObject.SetActive(false);
            for (int i = judgmentCount; i < judgments.Count; i++) SetJudgmentVisible(judgments[i], false);
        }

        private bool Eligible(WorldEntity entity)
        {
            return entity != null && entity.area == runtime.World.State.currentArea
                && (runtime.World.IsContainer(entity) || runtime.World.Spec(entity.specId).Has(Capability.Processor))
                && runtime.View(entity.id) != null;
        }

        // ── Container card ───────────────────────────────────────────────

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
            var machine = world.MachineProgress(entity);
            var extraction = world.ExtractionSnapshot(entity);
            bool container = world.IsContainer(entity);
            card.title.text = world.Spec(entity.specId).label;

            if (container)
            {
                text.Clear().Append(snapshot.temperatureCelsius.ToString("0")).Append(" °C   ")
                    .Append(snapshot.volumeMl.ToString("0.#")).Append(" / ").Append(snapshot.capacityMl.ToString("0.#")).Append(" ml");
                Assign(card.summary, text);
            }
            else card.summary.text = machine.active ? "加工中 · 取出前请等待完成" : world.ProductionCaption(entity);
            card.fillBar.gameObject.SetActive(container);
            if (container) SetBand(card.fill, 0, snapshot.capacityMl > 0 ? snapshot.volumeMl / snapshot.capacityMl : 0, false);

            if (extraction.available && (extraction.inProgress || extraction.completed))
            {
                var filter = world.ExtractionFilter(entity);
                float remainingGrounds = filter != null ? world.SolidMass(filter) : 0;
                text.Clear().Append(extraction.inProgress ? "萃取中" : "萃取完成");
                if (extraction.completed && !string.IsNullOrEmpty(extraction.qualityLabel)) text.Append(" · ").Append(extraction.qualityLabel);
                else text.Append(" · 剩余咖啡粉 ").Append(remainingGrounds.ToString("0.#")).Append(" g");
                Assign(card.status, text);
            }
            else if (machine.active)
            {
                text.Clear().Append(ProcessVerb(entity)).Append("中 · 还需 ").Append(Mathf.Max(0, machine.remaining).ToString("0.0")).Append(" 秒");
                Assign(card.status, text);
            }
            else card.status.text = world.CompositionName(entity);

            BuildIngredients(entity);
            Assign(card.contents, text);
            float contentHeight = Mathf.Max(24, ingredients.Count * 23 + 2);
            card.contents.rectTransform.offsetMin = new Vector2(20, -(118 + contentHeight));
            card.contents.rectTransform.offsetMax = new Vector2(-20, -118);
            float y = 118 + contentHeight + 10;
            card.temperature.gameObject.SetActive(container);
            if (container)
            {
                card.temperature.offsetMin = new Vector2(20, -y - 40);
                card.temperature.offsetMax = new Vector2(-20, -y);
                UpdateTemperature(card, snapshot.temperatureCelsius);
                y += 46;
            }
            card.root.sizeDelta = new Vector2(cardWidth, y + 16);
        }

        private string ProcessVerb(WorldEntity entity) => entity.specId == "machine.grinder" ? "研磨" : "加热";

        private void BuildIngredients(WorldEntity entity)
        {
            ingredients.Clear(); ingredientLabels.Clear(); gramsPerUnits.Clear(); volumes.Clear(); masses.Clear();
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
                    ingredients.Add(key); ingredientLabels.Add(world.IngredientLabel(portion));
                    gramsPerUnits.Add(world.IngredientDefinition(portion.specId)?.gramsPerUnit ?? 12);
                    volumes.Add(portion.milliliters); masses.Add(portion.grams);
                }
                else { volumes[index] += portion.milliliters; masses[index] += portion.grams; }
            }
            text.Clear();
            if (ingredients.Count == 0) { text.Append("<color=#8a6a50>空 · 可自由加入原料</color>"); return; }
            for (int i = 0; i < ingredients.Count; i++)
            {
                if (i > 0) text.Append('\n');
                if (ingredients[i] == "milkfoam:" && world.IsLatteArtCup(entity) && entity.latteArt)
                {
                    text.Append(FreeCafeProductionText.LatteArtContents(volumes[i], entity.latteArtPattern));
                    continue;
                }
                text.Append(ingredientLabels[i]).Append("  <color=#8a5634>");
                if (volumes[i] > 0) text.Append(volumes[i].ToString("0.#")).Append(" ml");
                if (masses[i] > 0)
                {
                    if (volumes[i] > 0) text.Append(" + ");
                    text.Append((masses[i] / Mathf.Max(.001f, gramsPerUnits[i])).ToString("0.#")).Append(" 份 · ")
                        .Append(masses[i].ToString("0.#")).Append(" g");
                }
                if (volumes[i] <= 0 && masses[i] <= 0) text.Append("0 g");
                text.Append("</color>");
            }
        }

        private void UpdateTemperature(Card card, float celsius)
        {
            text.Clear().Append("温度  ").Append(celsius.ToString("0")).Append(" °C");
            Assign(card.temperatureCaption, text);
            float position = Mathf.Clamp01(celsius / 100f);
            card.pointer.anchorMin = new Vector2(position, 0); card.pointer.anchorMax = new Vector2(position, 1);
            card.pointer.GetComponent<Image>().color = Color.Lerp(new Color(.26f, .45f, .62f), new Color(.72f, .20f, .10f), position);
        }

        private Card BuildCard()
        {
            var root = Frame(canvasRect, "ContainerCard");
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
            root.pivot = new Vector2(0, 1);
            var card = new Card { root = root };
            card.title = Label(root, "Title", titleFont, 21, ink, 20, 16, 30);
            card.summary = Label(root, "TemperatureAndFill", font, 16, ink, 20, 50, 24);
            card.fillBar = Graphic(root, "VolumeBar", new Color(.85f, .76f, .63f));
            card.fillBar.anchorMin = new Vector2(0, 1); card.fillBar.anchorMax = new Vector2(1, 1); card.fillBar.pivot = new Vector2(.5f, 1);
            card.fillBar.offsetMin = new Vector2(20, -82); card.fillBar.offsetMax = new Vector2(-20, -77);
            card.fill = Graphic(card.fillBar, "VolumeFill", new Color(.52f, .30f, .16f));
            card.status = Label(root, "ProcessStatus", titleFont, 17, accent, 20, 88, 26);
            card.contents = Label(root, "Ingredients", font, 16, ink, 20, 118, 24);
            card.contents.textWrappingMode = TextWrappingModes.NoWrap;
            card.contents.lineSpacing = 6;

            card.temperature = new GameObject("Temperature", typeof(RectTransform)).GetComponent<RectTransform>();
            card.temperature.SetParent(root, false);
            card.temperature.anchorMin = new Vector2(0, 1); card.temperature.anchorMax = new Vector2(1, 1); card.temperature.pivot = new Vector2(0, 1);
            card.temperatureCaption = Label(card.temperature, "Caption", font, 14, ink, 0, 0, 20);
            var scale = Label(card.temperature, "Scale", font, 12, new Color(.45f, .33f, .24f), 0, 0, 20);
            scale.alignment = TextAlignmentOptions.Right; scale.text = "0 · 50 · 100";
            var track = Graphic(card.temperature, "Track", Color.white);
            track.anchorMin = new Vector2(0, 0); track.anchorMax = new Vector2(1, 0); track.pivot = Vector2.zero;
            track.offsetMin = new Vector2(0, 2); track.offsetMax = new Vector2(0, 12);
            // Cold, warm and hot thirds; the domain's thresholds are coarse so the bands stay qualitative.
            var cold = Graphic(track, "Cold", new Color(.66f, .76f, .80f)); SetBand(cold, 0, .4f, false);
            var warm = Graphic(track, "Warm", new Color(.93f, .76f, .49f)); SetBand(warm, .4f, .8f, false);
            var hot = Graphic(track, "Hot", new Color(.85f, .47f, .32f)); SetBand(hot, .8f, 1, false);
            for (int i = 0; i <= 2; i++)
            {
                var tick = Graphic(track, "Tick" + i * 50, new Color(.35f, .22f, .14f, .7f));
                tick.anchorMin = tick.anchorMax = new Vector2(i * .5f, .5f); tick.sizeDelta = new Vector2(1.5f, 14);
            }
            card.pointer = Graphic(track, "Pointer", ink);
            card.pointer.pivot = new Vector2(.5f, .5f); card.pointer.sizeDelta = new Vector2(5, 8); card.pointer.anchoredPosition = Vector2.zero;
            return card;
        }

        // ── Device progress (grinder / heater) ───────────────────────────

        private void ShowProgress(WorldEntity entity, CafeMachineProgress machine)
        {
            if (progressCount == progress.Count) progress.Add(BuildProgress());
            var bar = progress[progressCount++];
            bar.id = entity.id;
            bar.root.gameObject.SetActive(true);
            float fraction = machine.Fraction;
            text.Clear().Append(ProcessVerb(entity)).Append("中  ").Append(Mathf.RoundToInt(fraction * 100)).Append('%');
            Assign(bar.caption, text);
            SetBand(bar.fill, 0, fraction, false);
        }

        private Progress BuildProgress()
        {
            var root = new GameObject("ProgressGauge", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvasRect, false);
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f); root.pivot = new Vector2(.5f, 0);
            root.sizeDelta = new Vector2(progressWidth, 48);
            var plate = Plate(root, "Plate");
            plate.anchorMin = Vector2.zero; plate.anchorMax = Vector2.one; plate.offsetMin = plate.offsetMax = Vector2.zero;
            var caption = Label(root, "Caption", titleFont, 15, ink, 12, 5, 20);
            caption.alignment = TextAlignmentOptions.Center;
            caption.rectTransform.offsetMax = new Vector2(-12, -5);
            var track = Track(root, "Track");
            track.anchorMin = new Vector2(0, 0); track.anchorMax = new Vector2(1, 0); track.pivot = new Vector2(.5f, 0);
            track.offsetMin = new Vector2(14, 9); track.offsetMax = new Vector2(-14, 19);
            var fill = Graphic(track, "ProgressFill", new Color(.63f, .40f, .20f));
            return new Progress { root = root, fill = fill, caption = caption };
        }

        private void PositionProgress(Progress bar)
        {
            if (!ScreenBounds(bar.id, null, out var bounds)) { bar.root.gameObject.SetActive(false); return; }
            bar.root.gameObject.SetActive(true);
            var area = canvasRect.rect;
            float x = Mathf.Clamp(bounds.center.x, area.xMin + progressWidth * .5f + screenPadding, area.xMax - progressWidth * .5f - screenPadding);
            float y = Mathf.Min(bounds.yMax + 6, area.yMax - bar.root.sizeDelta.y - screenPadding);
            bar.root.anchoredPosition = new Vector2(x, y);
        }

        // ── Extraction judgment bars ─────────────────────────────────────

        private void ShowJudgment(WorldEntity jug)
        {
            var world = runtime.World;
            var extraction = world.ExtractionSnapshot(jug);
            bool inspected = inspectedId == jug.id || world.ExtractionFilter(jug) != null && inspectedId == world.ExtractionFilter(jug).id;
            bool guided = runtime.WorkstationTutorial != null && runtime.WorkstationTutorial.HighlightsExtractionGauges(jug.id);
            if (!extraction.available || !(guided || extraction.inProgress || extraction.completed && inspected)) return;
            if (judgmentCount == judgments.Count) judgments.Add(BuildJudgment());
            var judgment = judgments[judgmentCount++];
            judgment.id = jug.id;
            SetJudgmentVisible(judgment, true);
            UpdateGauge(judgment.time, "时间", extraction.elapsedSeconds, extraction.idealSeconds, extraction.greenTimeMin, extraction.greenTimeMax,
                extraction.goldTimeMin, extraction.goldTimeMax, "s");
            UpdateGauge(judgment.water, "热水", extraction.injectedHotWaterMl, extraction.idealMl, extraction.greenMlMin, extraction.greenMlMax,
                extraction.goldMlMin, extraction.goldMlMax, "ml");
        }

        private Judgment BuildJudgment() => new Judgment { time = BuildGauge("TimeGauge"), water = BuildGauge("WaterGauge") };

        private static void SetJudgmentVisible(Judgment judgment, bool visible)
        {
            judgment.time.root.gameObject.SetActive(visible);
            judgment.water.root.gameObject.SetActive(visible);
        }

        private Gauge BuildGauge(string name)
        {
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvasRect, false);
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f); root.pivot = new Vector2(.5f, .5f);
            var track = Track(root, "Track");
            track.anchorMin = new Vector2(.5f, 0); track.anchorMax = new Vector2(.5f, 1); track.pivot = new Vector2(.5f, .5f);
            track.offsetMin = new Vector2(-judgmentWidth * .5f, 52); track.offsetMax = new Vector2(judgmentWidth * .5f, 0);
            var greenBand = Graphic(track, "GreenZone", new Color(green.r, green.g, green.b, .85f));
            var goldBand = Graphic(track, "GoldZone", gold);
            var level = Graphic(track, "LevelFill", new Color(.42f, .25f, .14f, .55f));
            var pointer = Graphic(track, "Pointer", ink);
            pointer.anchorMin = new Vector2(-.25f, 0); pointer.anchorMax = new Vector2(1.25f, 0); pointer.pivot = new Vector2(.5f, .5f);
            pointer.sizeDelta = new Vector2(0, 4);
            var plate = Plate(root, "CaptionPlate");
            plate.anchorMin = new Vector2(.5f, 0); plate.anchorMax = new Vector2(.5f, 0); plate.pivot = new Vector2(.5f, 0);
            plate.sizeDelta = new Vector2(92, 46); plate.anchoredPosition = Vector2.zero;
            var caption = Label(root, "Caption", titleFont, 14, ink, 0, 0, 40);
            var rect = caption.rectTransform;
            rect.anchorMin = new Vector2(.5f, 0); rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, 0);
            rect.sizeDelta = new Vector2(88, 40); rect.anchoredPosition = new Vector2(0, 3);
            caption.alignment = TextAlignmentOptions.Center; caption.lineSpacing = -8;
            var focus = Graphic(root, "TutorialFocus", Color.clear);
            focus.anchorMin = Vector2.zero; focus.anchorMax = Vector2.one;
            focus.offsetMin = new Vector2(-8, -6); focus.offsetMax = new Vector2(8, 8);
            focus.SetAsFirstSibling();
            var focusImage = focus.GetComponent<Image>();
            focusImage.raycastTarget = false;
            var outline = focus.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1, .73f, .25f, .95f);
            outline.effectDistance = new Vector2(3, 3);
            focus.gameObject.SetActive(false);
            return new Gauge { root = root, green = greenBand, gold = goldBand, level = level, levelImage = level.GetComponent<Image>(), pointer = pointer, caption = caption,
                tutorialFocus = focus, tutorialFocusImage = focusImage };
        }

        private static void HighlightGauge(Gauge gauge, bool guided)
        {
            gauge.tutorialFocus.gameObject.SetActive(guided);
            gauge.caption.fontStyle = guided ? FontStyles.Bold : FontStyles.Normal;
            if (!guided) return;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 4);
            gauge.tutorialFocusImage.color = new Color(1, .73f, .25f, .10f + pulse * .12f);
        }

        private void UpdateGauge(Gauge gauge, string label, float value, float ideal, float greenMin, float greenMax, float goldMin, float goldMax, string unit)
        {
            float limit = Mathf.Max(.001f, Mathf.Max(ideal * 1.5f, greenMax * 1.15f));
            text.Clear().Append(label).Append('\n').Append(value.ToString("0.#")).Append(" / ").Append(ideal.ToString("0.#")).Append(unit);
            Assign(gauge.caption, text);
            SetBand(gauge.green, greenMin / limit, greenMax / limit, true);
            SetBand(gauge.gold, goldMin / limit, goldMax / limit, true);
            gauge.green.gameObject.SetActive(greenMax > greenMin);
            gauge.gold.gameObject.SetActive(goldMax > goldMin);
            float position = Mathf.Clamp01(value / limit);
            SetBand(gauge.level, 0, position, true);
            // The level reads the judgment directly: gold, green, short or overshot.
            gauge.levelImage.color = value >= goldMin && value <= goldMax ? new Color(gold.r, gold.g, gold.b, .9f)
                : value >= greenMin && value <= greenMax ? new Color(green.r * .8f, green.g * .8f, green.b * .8f, .75f)
                : value > greenMax ? new Color(.68f, .20f, .12f, .75f) : new Color(.42f, .25f, .14f, .45f);
            gauge.pointer.anchorMin = new Vector2(-.25f, position);
            gauge.pointer.anchorMax = new Vector2(1.25f, position);
            gauge.pointer.anchoredPosition = Vector2.zero;
        }

        private void PositionJudgment(Judgment judgment)
        {
            var jug = runtime.World.Find(judgment.id);
            var filter = jug == null ? null : runtime.World.ExtractionFilter(jug);
            if (!ScreenBounds(judgment.id, filter?.id, out var bounds)) { SetJudgmentVisible(judgment, false); return; }
            float height = Mathf.Max(180, bounds.height * judgmentHeight + 52);
            var area = canvasRect.rect;
            float y = Mathf.Clamp(bounds.center.y - 20, area.yMin + height * .5f + screenPadding, area.yMax - height * .5f - screenPadding);
            judgment.time.root.sizeDelta = judgment.water.root.sizeDelta = new Vector2(92, height);
            judgment.time.root.anchoredPosition = new Vector2(bounds.xMin - 22, y);
            judgment.water.root.anchoredPosition = new Vector2(bounds.xMax + 22, y);
        }

        // ── Shared helpers ───────────────────────────────────────────────

        private bool ScreenBounds(string id, string secondId, out Rect rect)
        {
            rect = default;
            var view = runtime.View(id);
            if (view == null || view.body == null) return false;
            var bounds = view.body.bounds;
            var second = secondId == null ? null : runtime.View(secondId);
            if (second != null && second.body != null) bounds.Encapsulate(second.body.bounds);
            var camera = runtime.worldCamera;
            var min = camera.WorldToScreenPoint(bounds.min);
            var max = camera.WorldToScreenPoint(bounds.max);
            if (min.z <= 0 || max.z <= 0) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, min, null, out var a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, max, null, out var b);
            rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            return true;
        }

        private static void SetBand(RectTransform rect, float minimum, float maximum, bool vertical)
        {
            minimum = Mathf.Clamp01(minimum); maximum = Mathf.Clamp01(maximum);
            rect.anchorMin = vertical ? new Vector2(0, minimum) : new Vector2(minimum, 0);
            rect.anchorMax = vertical ? new Vector2(1, maximum) : new Vector2(maximum, 1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Assign(TMP_Text target, StringBuilder builder)
        {
            string prior = target.text;
            bool same = prior != null && prior.Length == builder.Length;
            for (int i = 0; same && i < builder.Length; i++) same = prior[i] == builder[i];
            if (!same) target.SetText(builder);
        }

        private RectTransform Frame(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), paperFrame != null ? typeof(Image) : typeof(CafeHudSurface));
            root.transform.SetParent(parent, false);
            var surface = root.GetComponent<CafeHudSurface>();
            if (surface != null)
            { surface.color = paper; surface.border = new Color(.45f, .30f, .20f); surface.radius = 10; surface.raycastTarget = false; }
            var image = root.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = paperFrame; image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = frameScale; image.raycastTarget = false;
            }
            return (RectTransform)root.transform;
        }

        // Small labels use a drawn plate: the sliced paper frame needs more height than they have.
        private RectTransform Plate(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(CafeHudSurface));
            obj.transform.SetParent(parent, false);
            var surface = obj.GetComponent<CafeHudSurface>();
            surface.color = paper; surface.border = new Color(.40f, .24f, .14f);
            surface.radius = 9; surface.borderWidth = 2; surface.shadow = true; surface.raycastTarget = false;
            return (RectTransform)obj.transform;
        }

        private RectTransform Track(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(CafeHudSurface));
            obj.transform.SetParent(parent, false);
            var surface = obj.GetComponent<CafeHudSurface>();
            surface.color = new Color(.99f, .96f, .89f); surface.border = new Color(.36f, .21f, .12f);
            surface.radius = 4; surface.borderWidth = 2; surface.shadow = false; surface.raycastTarget = false;
            return (RectTransform)obj.transform;
        }

        private TMP_Text Label(Transform parent, string name, TMP_FontAsset face, float size, Color color, float x, float y, float height)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(0, 1);
            rect.offsetMin = new Vector2(x, -y - height); rect.offsetMax = new Vector2(-x, -y);
            var label = obj.GetComponent<TextMeshProUGUI>();
            if (face != null) label.font = face;
            label.fontSize = size; label.color = color; label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static RectTransform Graphic(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return image.rectTransform;
        }

        private void AvoidHud(ref Vector2 point,Vector2 size)
        {
            if(hud==null || hud.productionRoot==null || !hud.productionRoot.activeInHierarchy) return;
            AvoidHudRect(hud.orderTitle!=null ? hud.orderTitle.transform.parent as RectTransform : null,ref point,size);
            AvoidHudRect(hud.bookButton!=null ? hud.bookButton.transform.parent as RectTransform : null,ref point,size);
        }

        private void AvoidHudRect(RectTransform target,ref Vector2 point,Vector2 size)
        {
            if(target==null || !target.gameObject.activeInHierarchy) return;
            target.GetWorldCorners(hudCorners);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,RectTransformUtility.WorldToScreenPoint(null,hudCorners[0]),null,out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,RectTransformUtility.WorldToScreenPoint(null,hudCorners[2]),null,out var max);
            if(new Rect(point.x,point.y-size.y,size.x,size.y).Overlaps(Rect.MinMaxRect(min.x,min.y,max.x,max.y)))
                point.y=Mathf.Max(canvasRect.rect.yMin+size.y+screenPadding,min.y-8);
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
            AvoidHud(ref point,size);
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
            AvoidHud(ref point,size);
            card.root.anchoredPosition = point;
            card.occupied = new Rect(point.x, point.y - size.y, size.x, size.y);
        }
    }
}
