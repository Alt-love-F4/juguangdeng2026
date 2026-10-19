using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>
    /// 咖啡宝典：书本式的配方目录 / 练习教程 / 器具订购 / 原料补货。
    /// 书本外框、页签与动效节点由 prefab 提供；页面内容按当前 CafeWorld 即时排版。
    /// 购买与解锁全部走 CafeWorld 现有接口，本面板只负责展示与发起。
    /// </summary>
    public sealed class CoffeeCompendiumPanel : MonoBehaviour
    {
        public enum Tab { Recipes, Tutorials, Shop, Restock }

        [Serializable]
        public sealed class RecipeCopy
        {
            public string productId;
            public Sprite icon;
            public string tagline;
            [TextArea(2, 5)] public string story;
            [TextArea(1, 3)] public string[] steps;
            [TextArea(1, 3)] public string tip;
        }

        [Serializable]
        public sealed class ShopItemCopy
        {
            public bool latte;
            public string title;
            public string subtitle;
            [TextArea(2, 5)] public string pitch;
            public string[] artIds;
        }

        [Serializable]
        public sealed class PracticeCopy
        {
            public bool extraction;
            public bool milkPitcher;
            public string title;
            public Sprite icon;
            [TextArea(2, 4)] public string description;
        }

        [Header("Bindings")]
        public FreeCafeRuntime runtime;
        public CanvasGroup rootGroup;
        public RectTransform bookRoot;
        public RectTransform leftPage, rightPage;
        public CanvasGroup leftGroup, rightGroup;
        public RectTransform flipLeft, flipRight;
        public Button closeButton;
        public TMP_Text moneyLabel, toastLabel;
        public CanvasGroup toastGroup;
        public Button[] tabButtons = new Button[4];
        public Image[] tabImages = new Image[4];
        public TMP_Text[] tabLabels = new TMP_Text[4];

        [Header("Style")]
        public TMP_FontAsset font;
        public Sprite rowCard, couponCard, sealRed, sealGray, divider, arrow, lockIcon, pill, stampRing, ornament;
        public Sprite tabIdleLeft, tabSelectedLeft, tabIdleRight, tabSelectedRight;
        public Color ink = new Color(.23f, .14f, .085f), inkSoft = new Color(.42f, .30f, .20f),
            rust = new Color(.54f, .17f, .08f), good = new Color(.27f, .42f, .18f), paper = new Color(.97f, .91f, .80f);

        [Header("Copy")]
        public string[] recipeOrder;
        public RecipeCopy[] recipes = Array.Empty<RecipeCopy>();
        public ShopItemCopy[] shopItems = Array.Empty<ShopItemCopy>();
        public PracticeCopy[] practices = Array.Empty<PracticeCopy>();
        [TextArea(1, 3)] public string[] counterGuide = Array.Empty<string>();
        [TextArea(2, 4)] public string catalogIntro, shopMasthead, shopFinePrint, restockIntro, tutorialIntro;

        [Header("Motion")]
        [Min(.05f)] public float openSeconds = .42f;
        [Min(.05f)] public float flipSeconds = .34f;

        public Tab CurrentTab { get; private set; } = Tab.Recipes;
        /// <summary>-1 shows the table of contents; otherwise the index into <see cref="ProductOrder"/>.</summary>
        public int RecipeIndex { get; private set; } = -1;
        public bool IsOpen => gameObject.activeSelf;
        public bool IsAnimating => motion != null;
        public event Action Closed;

        private readonly List<string> productOrder = new List<string>();
        private Vector2[] tabHome;
        private Coroutine motion, toastRoutine;
        private int beanQuality = -1;
        private int celebrateLatte = -1;
        private RectTransform celebrateStamp;
        private string signature;
        private float nextSignatureCheck;
        private bool listening;

        public IReadOnlyList<string> ProductOrder { get { BuildOrder(); return productOrder; } }

        // ---------------------------------------------------------------- lifecycle

        private void OnEnable()
        {
            Fit();
            if (listening) return;
            listening = true;
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                Tab tab = (Tab)i;
                tabButtons[i].onClick.AddListener(() => SelectTab(tab));
            }
            if (runtime != null) runtime.ViewChanged += MarkDirty;
        }

        private void OnDisable()
        {
            if (!listening) return;
            listening = false;
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            foreach (Button button in tabButtons) if (button != null) button.onClick.RemoveAllListeners();
            if (runtime != null) runtime.ViewChanged -= MarkDirty;
            StopMotion();
            if (toastRoutine != null) { StopCoroutine(toastRoutine); toastRoutine = null; }
            if (toastGroup != null) toastGroup.alpha = 0f;
        }

        private void OnRectTransformDimensionsChange() => Fit();

        private void Update()
        {
            // World ticks (patience, timers) fire ViewChanged often; only re-lay pages when what we show changes.
            if (motion != null || Time.unscaledTime < nextSignatureCheck) return;
            nextSignatureCheck = Time.unscaledTime + .25f;
            string current = Signature();
            if (current != signature) RenderPages();
        }

        private void MarkDirty() => nextSignatureCheck = 0f;

        private void Fit()
        {
            if (bookRoot == null) return;
            var rect = ((RectTransform)transform).rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            float scale = Mathf.Clamp(Mathf.Min(rect.width / 1760f, rect.height / 1060f), .2f, 1.25f);
            bookRoot.localScale = Vector3.one * scale;
        }

        // ---------------------------------------------------------------- public API

        public void Open(FreeCafeRuntime owner, string productId = null)
        {
            if (owner != null && owner != runtime)
            {
                if (listening && runtime != null) runtime.ViewChanged -= MarkDirty;
                runtime = owner;
                if (listening) runtime.ViewChanged += MarkDirty;
            }
            if (runtime == null || runtime.World == null) return;
            bool wasOpen = gameObject.activeSelf;
            runtime.CancelDrag();
            gameObject.SetActive(true);
            CacheTabs();
            BuildOrder();
            if (!string.IsNullOrEmpty(productId) && productOrder.Contains(productId))
            {
                CurrentTab = Tab.Recipes;
                RecipeIndex = productOrder.IndexOf(productId);
            }
            RenderAll();
            if (!wasOpen)
            {
                Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.PanelOpen);
                Play(OpenRoutine());
            }
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            if (motion != null && closing) return;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.PanelClose);
            Play(CloseRoutine());
        }

        /// <summary>Closes immediately (scene change, HUD hidden, tests).</summary>
        public void CloseImmediate()
        {
            StopMotion();
            closing = false;
            if (gameObject.activeSelf) { gameObject.SetActive(false); Closed?.Invoke(); }
        }

        public void SelectTab(Tab tab)
        {
            if (motion != null) return;
            if (tab == CurrentTab && !(tab == Tab.Recipes && RecipeIndex >= 0)) return;
            bool forward = (int)tab >= (int)CurrentTab;
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Tab);
            Turn(forward, () => { CurrentTab = tab; if (tab == Tab.Recipes) RecipeIndex = -1; RefreshTabs(); });
        }

        public void ShowRecipe(int index)
        {
            if (motion != null) return;
            BuildOrder();
            if (productOrder.Count == 0) return;
            index = (index % productOrder.Count + productOrder.Count) % productOrder.Count;
            bool forward = RecipeIndex < 0 || index > RecipeIndex || (RecipeIndex == productOrder.Count - 1 && index == 0);
            if (RecipeIndex == 0 && index == productOrder.Count - 1) forward = false;
            Turn(forward, () => { CurrentTab = Tab.Recipes; RecipeIndex = index; RefreshTabs(); });
        }

        public void ShowContents()
        {
            if (motion != null || RecipeIndex < 0) return;
            Turn(false, () => RecipeIndex = -1);
        }

        // ---------------------------------------------------------------- motion

        private bool closing;

        private void Play(IEnumerator routine)
        {
            StopMotion();
            motion = StartCoroutine(Run(routine));
        }

        private IEnumerator Run(IEnumerator routine)
        {
            yield return routine;
            motion = null;
        }

        private void StopMotion()
        {
            if (motion != null) { StopCoroutine(motion); motion = null; }
            if (flipLeft != null) flipLeft.gameObject.SetActive(false);
            if (flipRight != null) flipRight.gameObject.SetActive(false);
            if (leftGroup != null) leftGroup.alpha = 1f;
            if (rightGroup != null) rightGroup.alpha = 1f;
            if (bookRoot != null) { bookRoot.anchoredPosition = Vector2.zero; bookRoot.localRotation = Quaternion.identity; }
            if (rootGroup != null) { rootGroup.alpha = 1f; rootGroup.interactable = rootGroup.blocksRaycasts = true; }
            Fit();
            RestTabs(1f);
        }

        private IEnumerator OpenRoutine()
        {
            closing = false;
            Fit();
            float baseScale = bookRoot.localScale.x;
            rootGroup.interactable = false;
            for (float t = 0f; t < openSeconds; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.Clamp01(t / openSeconds);
                rootGroup.alpha = Smooth(Mathf.Clamp01(p / .6f));
                bookRoot.localScale = Vector3.one * baseScale * Mathf.LerpUnclamped(.86f, 1f, OutBack(p));
                bookRoot.anchoredPosition = new Vector2(0f, Mathf.Lerp(-70f, 0f, OutCubic(p)));
                bookRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-2.5f, 0f, OutCubic(p)));
                RestTabs(Mathf.Clamp01((p - .35f) / .65f));
                yield return null;
            }
            rootGroup.alpha = 1f;
            rootGroup.interactable = true;
            bookRoot.localScale = Vector3.one * baseScale;
            bookRoot.anchoredPosition = Vector2.zero;
            bookRoot.localRotation = Quaternion.identity;
            RestTabs(1f);
        }

        private IEnumerator CloseRoutine()
        {
            closing = true;
            rootGroup.interactable = false;
            float baseScale = bookRoot.localScale.x;
            const float seconds = .22f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.Clamp01(t / seconds);
                rootGroup.alpha = 1f - Smooth(p);
                bookRoot.localScale = Vector3.one * baseScale * Mathf.Lerp(1f, .92f, p);
                bookRoot.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, -40f, p));
                yield return null;
            }
            closing = false;
            motion = null;
            bookRoot.localScale = Vector3.one * baseScale;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        private void Turn(bool forward, Action change)
        {
            if (!gameObject.activeInHierarchy) { change(); RenderAll(); return; }
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Page);
            Play(TurnRoutine(forward, change));
        }

        /// <summary>A page lifts from one side, folds over the spine, and settles on the other side.</summary>
        private IEnumerator TurnRoutine(bool forward, Action change)
        {
            RectTransform first = forward ? flipRight : flipLeft, second = forward ? flipLeft : flipRight;
            CanvasGroup fromGroup = forward ? rightGroup : leftGroup, toGroup = forward ? leftGroup : rightGroup;
            float half = flipSeconds * .5f;
            first.gameObject.SetActive(true);
            first.localScale = Vector3.one;
            Image firstImage = first.GetComponent<Image>();
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.Clamp01(t / half);
                fromGroup.alpha = 1f - Mathf.Clamp01(p * 2.5f);
                first.localScale = new Vector3(Mathf.Cos(p * Mathf.PI * .5f), 1f + .02f * Mathf.Sin(p * Mathf.PI), 1f);
                if (firstImage != null) firstImage.color = Color.Lerp(Color.white, new Color(.72f, .62f, .5f), p);
                yield return null;
            }
            first.gameObject.SetActive(false);
            change();
            RenderAll();
            leftGroup.alpha = rightGroup.alpha = 0f;
            fromGroup.alpha = 0f;
            second.gameObject.SetActive(true);
            Image secondImage = second.GetComponent<Image>();
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.Clamp01(t / half);
                second.localScale = new Vector3(Mathf.Sin(p * Mathf.PI * .5f), 1f + .02f * Mathf.Sin((1f - p) * Mathf.PI), 1f);
                if (secondImage != null) secondImage.color = Color.Lerp(new Color(.72f, .62f, .5f), Color.white, p);
                fromGroup.alpha = Mathf.Clamp01(p * 1.6f - .3f);
                yield return null;
            }
            second.gameObject.SetActive(false);
            for (float t = 0f; t < .14f; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.Clamp01(t / .14f);
                toGroup.alpha = Smooth(p);
                fromGroup.alpha = Mathf.Max(fromGroup.alpha, Smooth(p));
                yield return null;
            }
            leftGroup.alpha = rightGroup.alpha = 1f;
        }

        private void CacheTabs()
        {
            if (tabHome != null && tabHome.Length == tabButtons.Length) return;
            tabHome = new Vector2[tabButtons.Length];
            for (int i = 0; i < tabButtons.Length; i++)
                if (tabButtons[i] != null) tabHome[i] = ((RectTransform)tabButtons[i].transform).anchoredPosition;
        }

        /// <summary>Tabs slide out from behind the cover; the selected one sticks out a little further.</summary>
        private void RestTabs(float reveal)
        {
            if (tabHome == null) return;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                bool left = i < 2;
                float stagger = Mathf.Clamp01(reveal * 1.4f - i * .12f);
                float outward = (i == (int)CurrentTab ? 16f : 0f) - (1f - OutBack(stagger)) * 90f;
                ((RectTransform)tabButtons[i].transform).anchoredPosition = tabHome[i] + new Vector2(left ? -outward : outward, 0f);
            }
        }

        private void RefreshTabs()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                bool selected = i == (int)CurrentTab;
                bool left = i < 2;
                if (tabImages[i] != null) tabImages[i].sprite = selected ? (left ? tabSelectedLeft : tabSelectedRight) : (left ? tabIdleLeft : tabIdleRight);
                if (tabLabels[i] != null) tabLabels[i].color = selected ? rust : inkSoft;
                // Selected tab is drawn in front of the cover so it reads as the open section.
                if (tabButtons[i] != null) tabButtons[i].transform.SetSiblingIndex(selected ? tabButtons[i].transform.parent.childCount - 1 : 0);
            }
            RestTabs(1f);
        }

        // ---------------------------------------------------------------- rendering

        private CafeWorld World => runtime == null ? null : runtime.World;

        private void RenderAll()
        {
            RefreshTabs();
            RenderPages();
        }

        private void RenderPages()
        {
            if (World == null || leftPage == null || rightPage == null) return;
            signature = Signature();
            Clear(leftPage);
            Clear(rightPage);
            if (moneyLabel != null) moneyLabel.text = "王国币  " + World.State.money;
            switch (CurrentTab)
            {
                case Tab.Recipes:
                    if (RecipeIndex < 0) RenderContents(); else RenderRecipe(RecipeIndex);
                    break;
                case Tab.Tutorials: RenderTutorials(); break;
                case Tab.Shop: RenderShop(); break;
                case Tab.Restock: RenderRestock(); break;
            }
        }

        private string Signature()
        {
            var w = World;
            if (w == null) return string.Empty;
            var s = w.State;
            var b = new StringBuilder();
            b.Append((int)CurrentTab).Append('|').Append(RecipeIndex).Append('|').Append(s.money).Append('|').Append(s.successfulOrders)
                .Append('|').Append(s.open).Append('|').Append(s.unlockedProducts.Count).Append('|').Append(s.precisionEquipmentActive)
                .Append(s.latteEquipmentActive).Append('|').Append(s.currentArea).Append('|').Append(s.beanQuality).Append('|').Append(beanQuality)
                .Append('|').Append(s.focusedRequestId).Append('|').Append(runtime.PracticeTutorialActive);
            if (CurrentTab == Tab.Restock)
                foreach (var source in Sources()) b.Append(source.id).Append(source.stock);
            return b.ToString();
        }

        private void BuildOrder()
        {
            productOrder.Clear();
            if (World == null) return;
            if (recipeOrder != null)
                foreach (string id in recipeOrder)
                    if (World.Product(id) != null && !productOrder.Contains(id)) productOrder.Add(id);
            foreach (var product in World.Catalog.products)
                if (product != null && !productOrder.Contains(product.id)) productOrder.Add(product.id);
        }

        private RecipeCopy Copy(string productId)
        {
            foreach (var copy in recipes) if (copy != null && copy.productId == productId) return copy;
            return null;
        }

        // ---- 咖啡目录 ----

        private void RenderContents()
        {
            BuildOrder();
            int learned = 0;
            foreach (string id in productOrder) if (World.State.unlockedProducts.Contains(id)) learned++;
            float y = 0f;
            y = Title(leftPage, "咖啡宝典", y, 56f);
            y = Line(leftPage, "— 圣诺特 · 巷口咖啡馆手记 —", y + 4f, 22f, inkSoft, TextAlignmentOptions.Center);
            y = Divider(leftPage, y + 10f);
            y = Para(leftPage, catalogIntro, y + 6f, 21f);
            y = Progress(leftPage, "已掌握配方", learned, productOrder.Count, y + 12f);
            y += 18f;
            int split = Mathf.Min(4, productOrder.Count);
            for (int i = 0; i < productOrder.Count; i++)
            {
                RectTransform page = i < split ? leftPage : rightPage;
                if (i == split) y = Line(rightPage, "目  录", 0f, 26f, rust, TextAlignmentOptions.Center) + 10f;
                y = ContentsRow(page, i, y) + 8f;
            }
        }

        private float ContentsRow(RectTransform page, int index, float y)
        {
            var product = World.Product(productOrder[index]);
            var copy = Copy(product.id);
            string reason = World.ProductLockReason(product);
            bool locked = reason != null;
            const float height = 92f;
            var row = Card(page, rowCard, y, height, out Button button);
            int captured = index;
            button.onClick.AddListener(() => ShowRecipe(captured));
            var icon = Img(row, copy?.icon, new Vector2(16f, -6f), new Vector2(80f, 80f), TextAnchor.UpperLeft);
            if (icon != null && locked) icon.color = new Color(.55f, .5f, .45f, .85f);
            Text(row, (index + 1).ToString("00") + "  " + FreeCafeProductionText.Recipe(product.label), new Vector2(110f, -12f), new Vector2(330f, 34f), 27f, ink, TextAlignmentOptions.TopLeft, TextAnchor.UpperLeft);
            string sub = locked ? "<color=#" + ColorUtility.ToHtmlStringRGB(rust) + ">未解锁</color>  " + reason
                : "<color=#" + ColorUtility.ToHtmlStringRGB(good) + ">已掌握</color>  售价 " + product.price + " 王国币";
            Text(row, sub, new Vector2(110f, -50f), new Vector2(390f, 30f), 17f, inkSoft, TextAlignmentOptions.TopLeft, TextAnchor.UpperLeft);
            if (locked) Img(row, lockIcon, new Vector2(-46f, -26f), new Vector2(28f, 32f), TextAnchor.UpperRight, rust);
            Img(row, arrow, new Vector2(-12f, -32f), new Vector2(30f, 20f), TextAnchor.UpperRight, inkSoft);
            return y + height;
        }

        // ---- 配方详情 ----

        private void RenderRecipe(int index)
        {
            BuildOrder();
            index = Mathf.Clamp(index, 0, productOrder.Count - 1);
            var w = World;
            var product = w.Product(productOrder[index]);
            var copy = Copy(product.id);
            string reason = w.ProductLockReason(product);
            bool locked = reason != null;

            // Left page: portrait and introduction.
            var back = TextButton(leftPage, "‹  返回目录", new Vector2(0f, 0f), new Vector2(170f, 36f), 20f, TextAnchor.UpperLeft);
            back.onClick.AddListener(ShowContents);
            Text(leftPage, (index + 1).ToString("00") + " / " + productOrder.Count.ToString("00"), new Vector2(0f, 0f), new Vector2(160f, 36f), 20f, inkSoft, TextAlignmentOptions.TopRight, TextAnchor.UpperRight);
            var portrait = Img(leftPage, copy?.icon, new Vector2(0f, -40f), new Vector2(270f, 270f), TextAnchor.UpperCenter);
            if (portrait != null && locked) portrait.color = new Color(.5f, .45f, .4f, .8f);
            if (locked) Stamp(leftPage, "未解锁", new Vector2(150f, -200f), gray: true, animate: false);
            float y = 320f;
            y = Title(leftPage, FreeCafeProductionText.Recipe(product.label), y, 46f);
            if (!string.IsNullOrEmpty(copy?.tagline)) y = Line(leftPage, "<i>" + FreeCafeProductionText.Recipe(copy.tagline) + "</i>", y + 2f, 21f, inkSoft, TextAlignmentOptions.Center);
            y = Divider(leftPage, y + 10f);
            y = Para(leftPage, FreeCafeProductionText.Recipe(string.IsNullOrEmpty(copy?.story) ? product.description : copy.story), y + 4f, 20f);
            string temperature = Temperature(product.requiredTemperature);
            y = Line(leftPage, "售价 <b>" + product.price + "</b> 王国币   ·   " + w.Spec(product.cupSpecId)?.label + "   ·   " + temperature, y + 14f, 20f, ink, TextAlignmentOptions.Center);
            Banner(leftPage, locked ? "尚未解锁 · " + reason : "已掌握 · 可以接单制作", locked, y + 16f);

            // Right page: how to make it.
            y = Title(rightPage, "制作方式", 0f, 36f);
            var focused = w.Request(w.State.focusedRequestId);
            bool forOrder = focused != null && focused.productId == product.id;
            float scale = forOrder ? w.OrderPortionScale(focused) : 1f;
            y = Line(rightPage, forOrder ? "当前订单用量（已按需求杯型换算）" : "参考用量 · 自由操作，不锁步骤", y + 2f, 18f, inkSoft, TextAlignmentOptions.Center);
            y += 12f;
            var cupSpec = w.Spec(forOrder ? w.RequiredCupSpecId(focused) : product.cupSpecId);
            y = IngredientRow(rightPage, cupSpec?.artId, "杯型", (cupSpec?.label ?? "-") + " · " + temperature, y);
            if (product.ingredients != null)
                foreach (var portion in product.ingredients)
                {
                    var spec = w.Spec(portion.specId);
                    string amount = portion.milliliters > 0 ? (portion.milliliters * scale).ToString("0") + " ml"
                        : portion.grams > 0 ? (portion.grams * scale).ToString("0.#") + " g" : (portion.units * scale).ToString("0.#") + " 份";
                    y = IngredientRow(rightPage, spec?.artId ?? portion.specId, FreeCafeProductionText.Recipe(spec?.label ?? portion.specId), amount, y);
                }
            y = Divider(rightPage, y + 8f);
            string[] steps = copy?.steps;
            if (steps == null || steps.Length == 0) y = Para(rightPage, FreeCafeProductionText.Recipe(product.tutorial), y + 4f, 18f);
            else
                for (int i = 0; i < steps.Length; i++)
                    y = Para(rightPage, "<color=#" + ColorUtility.ToHtmlStringRGB(rust) + "><b>" + (i + 1) + ".</b></color>  " + FreeCafeProductionText.Recipe(steps[i]), y + 6f, 19f);
            if (!string.IsNullOrEmpty(copy?.tip)) Note(rightPage, "小贴士  " + FreeCafeProductionText.Recipe(copy.tip), Mathf.Max(y + 12f, 636f));
            var previous = TextButton(rightPage, "‹ 上一页", new Vector2(0f, -712f), new Vector2(150f, 36f), 20f, TextAnchor.UpperLeft);
            previous.onClick.AddListener(() => ShowRecipe(RecipeIndex - 1));
            var next = TextButton(rightPage, "下一页 ›", new Vector2(0f, -712f), new Vector2(150f, 36f), 20f, TextAnchor.UpperRight);
            next.onClick.AddListener(() => ShowRecipe(RecipeIndex + 1));
        }

        private float IngredientRow(RectTransform page, string artId, string label, string amount, float y)
        {
            Sprite sprite = runtime.artLibrary != null && !string.IsNullOrEmpty(artId) ? runtime.artLibrary.Resolve(artId) : null;
            Img(page, sprite, new Vector2(18f, -y), new Vector2(44f, 44f), TextAnchor.UpperLeft);
            Text(page, label, new Vector2(76f, -y - 6f), new Vector2(290f, 34f), 21f, ink, TextAlignmentOptions.TopLeft, TextAnchor.UpperLeft);
            Text(page, amount, new Vector2(-12f, -y - 6f), new Vector2(200f, 34f), 21f, rust, TextAlignmentOptions.TopRight, TextAnchor.UpperRight);
            Img(page, pill, new Vector2(0f, -y - 46f), new Vector2(PageWidth(page) - 30f, 2f), TextAnchor.UpperCenter, new Color(inkSoft.r, inkSoft.g, inkSoft.b, .25f));
            return y + 50f;
        }

        // ---- 制作教程 ----

        private void RenderTutorials()
        {
            float y = Title(leftPage, "制作教程", 0f, 44f);
            y = Divider(leftPage, y + 8f);
            y = Para(leftPage, tutorialIntro, y + 4f, 20f);
            y += 12f;
            bool hasMilkPitcher = false;
            foreach (var practice in practices)
            {
                if (practice == null) continue;
                hasMilkPitcher |= practice.milkPitcher;
                y = PracticeCard(leftPage, practice, y) + 12f;
            }
            if (!hasMilkPitcher)
                PracticeCard(leftPage, new PracticeCopy { milkPitcher = true, title = "拉花杯操作教程",
                    description = "加牛奶、点击打奶、拖到饮品杯开启拉花。\n跟随鼠标提示练习，结束后恢复原工作台。" }, y);
            y = Title(rightPage, "吧台须知", 0f, 36f);
            y = Divider(rightPage, y + 8f);
            for (int i = 0; i < counterGuide.Length; i++)
                y = Para(rightPage, "<color=#" + ColorUtility.ToHtmlStringRGB(rust) + "><b>" + (i + 1) + ".</b></color>  " + FreeCafeProductionText.Recipe(counterGuide[i]), y + 8f, 19f);
            Note(rightPage, "每款咖啡的配方与步骤，翻到「咖啡目录」逐页查看。", 640f);
        }

        private float PracticeCard(RectTransform page, PracticeCopy practice, float y)
        {
            const float height = 170f;
            var card = Card(page, couponCard, y, height, out Button cardButton);
            cardButton.interactable = false;
            Img(card, practice.icon, new Vector2(18f, -18f), new Vector2(84f, 84f), TextAnchor.UpperLeft);
            Text(card, practice.title, new Vector2(116f, -14f), new Vector2(394f, 34f), 25f, rust, TextAlignmentOptions.TopLeft, TextAnchor.UpperLeft);
            var desc = Text(card, practice.description, new Vector2(116f, -50f), new Vector2(394f, 62f), 17f, ink, TextAlignmentOptions.TopLeft, TextAnchor.UpperLeft);
            desc.textWrappingMode = TextWrappingModes.Normal;
            string blocker = PracticeBlocker(practice.extraction, practice.milkPitcher);
            var button = Seal(card, blocker == null ? "开始练习" : "暂不可用", blocker == null, new Vector2(-24f, -114f), new Vector2(170f, 44f));
            Text(card, blocker ?? "练习不消耗店内原料，结束后台面复原", new Vector2(18f, -124f), new Vector2(320f, 30f), 16f, blocker == null ? inkSoft : rust, TextAlignmentOptions.TopLeft, TextAnchor.UpperLeft);
            button.onClick.AddListener(() =>
            {
                string reason = PracticeBlocker(practice.extraction, practice.milkPitcher);
                if (reason != null) { Deny(button.transform, reason); return; }
                StartPractice(practice.extraction, practice.milkPitcher);
            });
            return y + height;
        }

        private string PracticeBlocker(bool extraction, bool milkPitcher = false)
        {
            var w = World;
            if (runtime.WorkstationTutorial == null) return "当前场景没有吧台练习";
            if (runtime.CafeTutorialActive || runtime.PracticeTutorialActive) return "正在进行教程";
            if (!w.ProductionEnabled || w.State.currentArea != AreaId.Counter) return "请先走到吧台再开始";
            if (milkPitcher && !w.LatteEquipmentAvailable) return "需先订购「拉花套装」";
            if (extraction && !w.PrecisionEquipmentAvailable) return "需先订购「精粹萃取组」";
            return null;
        }

        private void StartPractice(bool extraction, bool milkPitcher = false)
        {
            // The open book counts as a modal; close it first so the workstation accepts the tutorial.
            CloseImmediate();
            if (!(milkPitcher ? runtime.StartEquipmentTutorial(true) : runtime.WorkstationTutorial.Begin(extraction)))
                runtime.Report(ActionResult.No("暂时无法开始练习：请先清空手上的操作再试"));
        }

        // ---- 器具订购 ----

        private void RenderShop()
        {
            float y = Title(leftPage, "圣诺特器具商行", 0f, 40f);
            y = Line(leftPage, "秋 季 订 购 目 录  ·  第 七 期", y + 2f, 17f, inkSoft, TextAlignmentOptions.Center);
            y = Divider(leftPage, y + 8f);
            y = Para(leftPage, shopMasthead, y + 2f, 19f);
            ShopItemCopy precision = null, latte = null;
            foreach (var item in shopItems) { if (item == null) continue; if (item.latte) latte = item; else precision = item; }
            if (precision != null) ShopCard(leftPage, precision, y + 12f);
            if (latte != null) y = ShopCard(rightPage, latte, 0f); else y = 0f;
            y = Divider(rightPage, y + 16f);
            y = Line(rightPage, "订 购 须 知", y + 2f, 22f, rust, TextAlignmentOptions.Center);
            y = Para(rightPage, shopFinePrint, y + 6f, 17f);
            Line(rightPage, "<i>—— 器具商行掌柜  谨启</i>", y + 8f, 18f, inkSoft, TextAlignmentOptions.Right);
        }

        private float ShopCard(RectTransform page, ShopItemCopy item, float y)
        {
            var w = World;
            const float height = 470f;
            var card = Card(page, couponCard, y, height, out Button cardButton);
            cardButton.interactable = false;
            int required = item.latte ? CafeWorld.LatteOrdersRequired : CafeWorld.PrecisionOrdersRequired;
            int cost = item.latte ? CafeWorld.LatteActivationCost : CafeWorld.PrecisionActivationCost;
            bool owned = item.latte ? w.LatteEquipmentAvailable : w.PrecisionEquipmentAvailable;
            bool freeMode = !w.State.equipmentProgressionEnabled || w.State.sandboxEquipment;
            Text(card, item.title, new Vector2(0f, -18f), new Vector2(480f, 40f), 31f, rust, TextAlignmentOptions.Top, TextAnchor.UpperCenter);
            Text(card, item.subtitle, new Vector2(0f, -58f), new Vector2(480f, 28f), 17f, inkSoft, TextAlignmentOptions.Top, TextAnchor.UpperCenter);
            float x = 22f;
            if (item.artIds != null)
                foreach (string artId in item.artIds)
                {
                    Sprite sprite = runtime.artLibrary != null ? runtime.artLibrary.Resolve(artId) : null;
                    if (sprite == null) continue;
                    Img(card, sprite, new Vector2(x, -96f), new Vector2(110f, 110f), TextAnchor.UpperLeft);
                    x += 104f;
                }
            var pitch = Text(card, item.pitch, new Vector2(-20f, -96f), new Vector2(item.artIds != null && item.artIds.Length > 1 ? 280f : 380f, 120f), 17f, ink, TextAlignmentOptions.TopLeft, TextAnchor.UpperRight);
            pitch.textWrappingMode = TextWrappingModes.Normal;
            float cy = 236f;
            if (item.latte) cy = Check(card, w.PrecisionEquipmentAvailable, "已配备「精粹萃取组」", cy);
            cy = Check(card, w.State.successfulOrders >= required, "累计成功出品 " + Mathf.Min(w.State.successfulOrders, required) + " / " + required + " 单", cy);
            cy = Check(card, !w.State.open && !w.HasTutorialCustomer, "营业前或打烊后办理", cy);
            cy = Check(card, w.State.money >= cost, "货款 " + cost + " 王国币（现有 " + w.State.money + "）", cy);
            Text(card, "<size=20>售价</size>  " + cost + " <size=20>王国币</size>", new Vector2(24f, -400f), new Vector2(280f, 44f), 32f, rust, TextAlignmentOptions.TopLeft, TextAnchor.UpperLeft);
            if (owned)
            {
                bool celebrate = celebrateLatte == (item.latte ? 1 : 0);
                var stamp = Stamp(card, freeMode ? "已配备" : "已到货", new Vector2(-40f, -250f), gray: false, animate: celebrate);
                if (celebrate) { celebrateStamp = stamp; celebrateLatte = -1; }
            }
            else
            {
                ActionResult check = w.EquipmentOrderCheck(item.latte);
                var order = Seal(card, "订  购", check.success, new Vector2(-24f, -398f), new Vector2(170f, 54f));
                order.onClick.AddListener(() => OrderEquipment(item.latte, order, card));
            }
            return y + height;
        }

        private float Check(RectTransform card, bool met, string label, float y)
        {
            string mark = met ? "<color=#" + ColorUtility.ToHtmlStringRGB(good) + ">√</color>" : "<color=#" + ColorUtility.ToHtmlStringRGB(rust) + ">×</color>";
            Text(card, mark + "  " + label, new Vector2(26f, -y), new Vector2(470f, 30f), 19f, met ? ink : inkSoft, TextAlignmentOptions.TopLeft, TextAnchor.UpperLeft);
            return y + 34f;
        }

        private void OrderEquipment(bool latte, Button button, RectTransform card)
        {
            if (motion != null) return;
            var result = World.ActivateEquipment(latte);
            if (!result.success) { Deny(button.transform, result.message); return; }
            runtime.Report(result);
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Confirm);
            Toast(result.message);
            celebrateLatte = latte ? 1 : 0;
            RenderPages();
            Play(Celebrate(celebrateStamp));
            celebrateStamp = null;
        }

        /// <summary>The delivery stamp slams onto the freshly owned card and the book jolts.</summary>
        private IEnumerator Celebrate(RectTransform stamp)
        {
            for (float t = 0f; t < .45f; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.Clamp01(t / .45f);
                if (stamp != null)
                {
                    float s = Mathf.LerpUnclamped(2.4f, 1f, OutBack(Mathf.Clamp01(p * 1.6f)));
                    stamp.localScale = Vector3.one * s;
                    stamp.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(p * 4f);
                }
                float kick = p > .3f ? Mathf.Sin((p - .3f) * 40f) * (1f - p) * 6f : 0f;
                bookRoot.anchoredPosition = new Vector2(0f, -kick);
                yield return null;
            }
            bookRoot.anchoredPosition = Vector2.zero;
            if (stamp != null) { stamp.localScale = Vector3.one; stamp.GetComponent<CanvasGroup>().alpha = 1f; }
        }

        // ---- 原料补货 ----

        private List<WorldEntity> Sources()
        {
            var list = new List<WorldEntity>();
            var seen = new HashSet<string>();
            foreach (var entity in World.State.entities)
            {
                var spec = World.Spec(entity.specId);
                if (spec == null || !spec.Has(Capability.Source) || spec.initialStock <= 0) continue;
                if (spec.unitCost <= 0 && spec.refillCost <= 0 && spec.sourceItemId != "beans") continue;
                if (!seen.Add(entity.specId)) continue;
                list.Add(entity);
            }
            list.Sort((a, b) => (World.Spec(b.specId).sourceItemId == "beans").CompareTo(World.Spec(a.specId).sourceItemId == "beans"));
            return list;
        }

        private void RenderRestock()
        {
            var w = World;
            int quality = beanQuality < 0 ? w.State.beanQuality : beanQuality;
            float y = Title(leftPage, "原料补货", 0f, 44f);
            y = Line(leftPage, "河岸街杂货铺  ·  当面结清，即刻上架", y + 2f, 18f, inkSoft, TextAlignmentOptions.Center);
            y = Divider(leftPage, y + 8f);
            y = Para(leftPage, restockIntro, y + 2f, 19f);
            y = Line(leftPage, "咖啡豆品质", y + 12f, 22f, rust, TextAlignmentOptions.Left);
            string[] grades = { "寻常豆", "精选豆", "特级豆" };
            var prices = w.Catalog.economy.beanUnitPrices;
            float width = (PageWidth(leftPage) - 20f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                string price = prices != null && i < prices.Length ? prices[i] + " 币/份" : "-";
                var button = Seal(leftPage, grades[i] + "\n<size=15>" + price + "</size>", i == quality, new Vector2(i * (width + 10f), -(y + 8f)), new Vector2(width, 62f), TextAnchor.UpperLeft);
                button.onClick.AddListener(() => { beanQuality = captured; Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Select); RenderPages(); });
            }
            y += 86f;
            var sources = Sources();
            int total = 0;
            foreach (var source in sources) { total += w.RestockQuote(source, quality, out int missing); }
            int split = Mathf.Min(3, sources.Count);
            for (int i = 0; i < sources.Count; i++)
            {
                if (i == split) y = 0f;
                y = RestockRow(i < split ? leftPage : rightPage, sources[i], quality, y) + 8f;
            }
            float by = Mathf.Max(y + 10f, 640f);
            var all = Seal(rightPage, total > 0 ? "全部补满  ·  " + total + " 王国币" : "货架都是满的", total > 0 && total <= w.State.money, new Vector2(0f, -by), new Vector2(330f, 56f), TextAnchor.UpperCenter);
            all.onClick.AddListener(() => RestockAll(all, quality));
        }

        private float RestockRow(RectTransform page, WorldEntity source, int quality, float y)
        {
            var w = World;
            var spec = w.Spec(source.specId);
            const float height = 96f;
            var row = Card(page, rowCard, y, height, out Button rowButton);
            rowButton.interactable = false;
            Sprite sprite = runtime.artLibrary != null ? runtime.artLibrary.Resolve(spec.artId) : null;
            Img(row, sprite, new Vector2(14f, -10f), new Vector2(76f, 76f), TextAnchor.UpperLeft);
            Text(row, FreeCafeProductionText.Recipe(spec.label), new Vector2(102f, -12f), new Vector2(220f, 32f), 24f, ink, TextAlignmentOptions.TopLeft, TextAnchor.UpperLeft);
            float fill = spec.initialStock <= 0 ? 0f : Mathf.Clamp01(source.stock / (float)spec.initialStock);
            Img(row, pill, new Vector2(102f, -54f), new Vector2(190f, 16f), TextAnchor.UpperLeft, new Color(inkSoft.r, inkSoft.g, inkSoft.b, .25f));
            if (fill > 0f) Img(row, pill, new Vector2(102f, -54f), new Vector2(Mathf.Max(16f, 190f * fill), 16f), TextAnchor.UpperLeft, fill < .35f ? rust : good);
            Text(row, source.stock + " / " + spec.initialStock, new Vector2(300f, -48f), new Vector2(80f, 28f), 17f, inkSoft, TextAlignmentOptions.TopLeft, TextAnchor.UpperLeft);
            int cost = w.RestockQuote(source, quality, out int missing);
            bool can = missing > 0 && cost <= w.State.money;
            var buy = Seal(row, missing <= 0 ? "已满" : "补满\n<size=15>" + cost + " 币</size>", can, new Vector2(-14f, -16f), new Vector2(118f, 62f), TextAnchor.UpperRight);
            string sourceId = source.id;
            buy.onClick.AddListener(() =>
            {
                var result = w.BuySource(sourceId, w.Spec(w.Find(sourceId)?.specId)?.sourceItemId == "beans" ? quality : -1);
                if (!result.success) { Deny(buy.transform, result.message); return; }
                Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Place);
                runtime.Report(result);
                Toast(result.message);
                RenderPages();
            });
            return y + height;
        }

        private void RestockAll(Button button, int quality)
        {
            var w = World;
            int total = 0;
            var sources = Sources();
            foreach (var source in sources) total += w.RestockQuote(source, quality, out int missing);
            if (total <= 0) { Deny(button.transform, "货架都是满的"); return; }
            if (total > w.State.money) { Deny(button.transform, "全部补满需要 " + total + " 王国币"); return; }
            int spent = 0;
            foreach (var source in sources)
            {
                int cost = w.RestockQuote(source, quality, out int missing);
                if (missing <= 0) continue;
                var result = w.BuySource(source.id, w.Spec(source.specId).sourceItemId == "beans" ? quality : -1);
                if (result.success) spent += cost;
            }
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Place);
            runtime.Report(ActionResult.Yes("全部补满 -" + spent + " 王国币"));
            Toast("全部补满，花费 " + spent + " 王国币");
            RenderPages();
        }

        // ---------------------------------------------------------------- feedback

        private void Deny(Transform target, string reason)
        {
            Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Error);
            Toast(reason);
            if (target is RectTransform rect) StartCoroutine(Shake(rect));
        }

        private IEnumerator Shake(RectTransform rect)
        {
            Vector2 home = rect.anchoredPosition;
            for (float t = 0f; t < .32f && rect != null; t += Time.unscaledDeltaTime)
            {
                float p = t / .32f;
                rect.anchoredPosition = home + new Vector2(Mathf.Sin(p * 34f) * 9f * (1f - p), 0f);
                yield return null;
            }
            if (rect != null) rect.anchoredPosition = home;
        }

        private void Toast(string message)
        {
            if (toastLabel == null || toastGroup == null || string.IsNullOrEmpty(message)) return;
            toastLabel.text = message;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            var rect = (RectTransform)toastGroup.transform;
            Vector2 home = new Vector2(rect.anchoredPosition.x, -440f);
            for (float t = 0f; t < .18f; t += Time.unscaledDeltaTime)
            {
                float p = t / .18f;
                toastGroup.alpha = p;
                rect.anchoredPosition = home + new Vector2(0f, -14f * (1f - OutCubic(p)));
                yield return null;
            }
            toastGroup.alpha = 1f;
            rect.anchoredPosition = home;
            yield return new WaitForSecondsRealtime(2.2f);
            for (float t = 0f; t < .3f; t += Time.unscaledDeltaTime) { toastGroup.alpha = 1f - t / .3f; yield return null; }
            toastGroup.alpha = 0f;
            toastRoutine = null;
        }

        // ---------------------------------------------------------------- layout helpers (top-down, page-local)

        private static void Clear(RectTransform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private static float PageWidth(RectTransform page) => page.rect.width > 1f ? page.rect.width : 535f;

        private static RectTransform Node(Transform parent, string name, Vector2 position, Vector2 size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Vector2 a = anchor switch
            {
                TextAnchor.UpperLeft => new Vector2(0f, 1f),
                TextAnchor.UpperRight => new Vector2(1f, 1f),
                TextAnchor.UpperCenter => new Vector2(.5f, 1f),
                _ => new Vector2(.5f, .5f)
            };
            rect.anchorMin = rect.anchorMax = rect.pivot = a;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private TMP_Text Text(Transform parent, string value, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment, TextAnchor anchor)
        {
            var rect = Node(parent, "Text", position, size, anchor);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.richText = true;
            text.text = value ?? string.Empty;
            return text;
        }

        private Image Img(Transform parent, Sprite sprite, Vector2 position, Vector2 size, TextAnchor anchor, Color? tint = null)
        {
            if (sprite == null) return null;
            var rect = Node(parent, sprite.name, position, size, anchor);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            bool sliced = sprite.border.sqrMagnitude > 0f;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = !sliced && sprite != divider;
            image.raycastTarget = false;
            image.color = tint ?? Color.white;
            return image;
        }

        private float Title(RectTransform page, string value, float y, float size)
        {
            Text(page, value, new Vector2(0f, -y), new Vector2(PageWidth(page), size + 12f), size, rust, TextAlignmentOptions.Top, TextAnchor.UpperCenter);
            return y + size + 12f;
        }

        private float Line(RectTransform page, string value, float y, float size, Color color, TextAlignmentOptions alignment)
        {
            Text(page, value, new Vector2(0f, -y), new Vector2(PageWidth(page), size + 10f), size, color, alignment | TextAlignmentOptions.Top, TextAnchor.UpperCenter);
            return y + size + 10f;
        }

        private float Para(RectTransform page, string value, float y, float size)
        {
            if (string.IsNullOrEmpty(value)) return y;
            var text = Text(page, value, new Vector2(0f, -y), new Vector2(PageWidth(page) - 16f, 10f), size, ink, TextAlignmentOptions.TopLeft, TextAnchor.UpperCenter);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.lineSpacing = 8f;
            float height = text.GetPreferredValues(value, PageWidth(page) - 16f, 0f).y;
            text.rectTransform.sizeDelta = new Vector2(PageWidth(page) - 16f, height);
            return y + height;
        }

        private float Divider(RectTransform page, float y)
        {
            Img(page, divider, new Vector2(0f, -y), new Vector2(PageWidth(page) - 40f, 18f), TextAnchor.UpperCenter);
            return y + 22f;
        }

        private float Progress(RectTransform page, string label, int value, int max, float y)
        {
            float width = PageWidth(page) - 60f;
            Text(page, label + "  <b>" + value + " / " + max + "</b>", new Vector2(0f, -y), new Vector2(width, 30f), 20f, ink, TextAlignmentOptions.TopLeft, TextAnchor.UpperCenter);
            Img(page, pill, new Vector2(0f, -y - 34f), new Vector2(width, 14f), TextAnchor.UpperCenter, new Color(inkSoft.r, inkSoft.g, inkSoft.b, .25f));
            if (value > 0)
            {
                var fill = Img(page, pill, new Vector2(-width * .5f, -y - 34f), new Vector2(Mathf.Max(14f, width * value / Mathf.Max(1f, max)), 14f), TextAnchor.UpperCenter, rust);
                if (fill != null) { fill.rectTransform.pivot = new Vector2(0f, 1f); fill.rectTransform.anchoredPosition = new Vector2(-width * .5f, -y - 34f); }
            }
            return y + 52f;
        }

        private void Banner(RectTransform page, string value, bool warning, float y)
        {
            var rect = Node(page, "Banner", new Vector2(0f, -y), new Vector2(PageWidth(page) - 40f, 46f), TextAnchor.UpperCenter);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = pill;
            image.type = Image.Type.Sliced;
            image.color = warning ? new Color(rust.r, rust.g, rust.b, .14f) : new Color(good.r, good.g, good.b, .16f);
            image.raycastTarget = false;
            var text = Text(rect, value, Vector2.zero, rect.sizeDelta, 19f, warning ? rust : good, TextAlignmentOptions.Center, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.sizeDelta = Vector2.zero;
        }

        private void Note(RectTransform page, string value, float y)
        {
            var card = Node(page, "Note", new Vector2(0f, -y), new Vector2(PageWidth(page) - 20f, 60f), TextAnchor.UpperCenter);
            var image = card.gameObject.AddComponent<Image>();
            image.sprite = couponCard;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, .85f);
            image.raycastTarget = false;
            var text = Text(card, value, new Vector2(0f, 0f), new Vector2(PageWidth(page) - 60f, 50f), 17f, inkSoft, TextAlignmentOptions.Center, TextAnchor.MiddleCenter);
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        private RectTransform Card(RectTransform page, Sprite sprite, float y, float height, out Button button)
        {
            var rect = Node(page, "Card", new Vector2(0f, -y), new Vector2(PageWidth(page), height), TextAnchor.UpperCenter);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, .96f, .88f);
            colors.pressedColor = new Color(.9f, .82f, .7f);
            colors.disabledColor = Color.white;
            button.colors = colors;
            Spotlight.Audio.UIButtonAudio.Configure(button, string.Empty);
            return rect;
        }

        private Button TextButton(RectTransform page, string label, Vector2 position, Vector2 size, float fontSize, TextAnchor anchor)
        {
            var rect = Node(page, "Link", position, size, anchor);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = Text(rect, label, Vector2.zero, size, fontSize, rust, anchor == TextAnchor.UpperRight ? TextAlignmentOptions.Right : TextAlignmentOptions.Left, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.sizeDelta = Vector2.zero;
            Spotlight.Audio.UIButtonAudio.Configure(button, string.Empty);
            return button;
        }

        /// <summary>Wax-seal style action button. Disabled actions stay clickable so the player hears why.</summary>
        private Button Seal(Transform parent, string label, bool enabled, Vector2 position, Vector2 size, TextAnchor anchor = TextAnchor.UpperRight)
        {
            var rect = Node(parent, "Seal", position, size, anchor);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = pill;
            image.type = Image.Type.Sliced;
            image.color = enabled ? rust : new Color(.62f, .55f, .47f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(.8f, .75f, .7f);
            button.colors = colors;
            var text = Text(rect, label, Vector2.zero, size, 21f, paper, TextAlignmentOptions.Center, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.sizeDelta = Vector2.zero;
            text.lineSpacing = -18f;
            Spotlight.Audio.UIButtonAudio.Configure(button, enabled ? Spotlight.Audio.AudioKeys.Confirm : string.Empty);
            return button;
        }

        private RectTransform Stamp(RectTransform parent, string label, Vector2 position, bool gray, bool animate)
        {
            if (parent == null || stampRing == null) return null;
            var rect = Node(parent, "Stamp", position, new Vector2(170f, 170f), TextAnchor.UpperRight);
            rect.localRotation = Quaternion.Euler(0f, 0f, -14f);
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = animate ? 0f : 1f;
            Color color = gray ? new Color(.45f, .4f, .36f, .85f) : new Color(rust.r, rust.g, rust.b, .9f);
            var ring = rect.gameObject.AddComponent<Image>();
            ring.sprite = stampRing;
            ring.color = color;
            ring.raycastTarget = false;
            var text = Text(rect, label, Vector2.zero, new Vector2(150f, 60f), 34f, color, TextAlignmentOptions.Center, TextAnchor.MiddleCenter);
            text.characterSpacing = 6f;
            return rect;
        }

        private static string Temperature(LiquidTemperature temperature)
            => temperature == LiquidTemperature.Boiling ? "热饮" : temperature == LiquidTemperature.Warm ? "温饮" : "常温";

        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static float OutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
        private static float OutBack(float t) { const float c1 = 1.5f, c3 = c1 + 1f; float u = t - 1f; return 1f + c3 * u * u * u + c1 * u * u; }
    }
}
