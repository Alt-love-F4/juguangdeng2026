using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    public sealed class CoffeeBookPanelView : MonoBehaviour
    {
        public RectTransform artRoot, marker;
        public TMP_Text title, body, note, page;
        public Button previous, next, close;
        public Button[] entries;
        public TMP_Text[] entryLabels;
        public ScrollRect bodyScroll;
        public event Action<int> RecipeSelected;

        private int firstRecipe;
        private int selectedRecipe = -1;
        private bool listening;

        private void OnEnable()
        {
            Fit();
            if(entries == null || entries.Length != 5 || listening) return;
            entries[0].onClick.AddListener(SelectFirst);
            entries[1].onClick.AddListener(SelectSecond);
            entries[2].onClick.AddListener(SelectThird);
            entries[3].onClick.AddListener(SelectFourth);
            entries[4].onClick.AddListener(SelectFifth);
            listening = true;
        }

        private void OnDisable()
        {
            if(!listening) return;
            entries[0].onClick.RemoveListener(SelectFirst);
            entries[1].onClick.RemoveListener(SelectSecond);
            entries[2].onClick.RemoveListener(SelectThird);
            entries[3].onClick.RemoveListener(SelectFourth);
            entries[4].onClick.RemoveListener(SelectFifth);
            listening = false;
        }

        private void OnRectTransformDimensionsChange() => Fit();
        private void SelectFirst() => RecipeSelected?.Invoke(firstRecipe);
        private void SelectSecond() => RecipeSelected?.Invoke(firstRecipe + 1);
        private void SelectThird() => RecipeSelected?.Invoke(firstRecipe + 2);
        private void SelectFourth() => RecipeSelected?.Invoke(firstRecipe + 3);
        private void SelectFifth() => RecipeSelected?.Invoke(firstRecipe + 4);

        private void Fit()
        {
            if(artRoot == null) return;
            var rect = ((RectTransform)transform).rect;
            if(rect.width <= 0f || rect.height <= 0f) return;
            float scale = Mathf.Max(.01f, Mathf.Min(rect.width / 1650f, rect.height / 850f));
            artRoot.localScale = Vector3.one * scale;
            artRoot.anchoredPosition = new Vector2(-65f * scale, 0f);
        }

        public void Show(FreeCafeRuntime runtime, int index)
        {
            if(runtime == null || runtime.World == null) return;
            var world = runtime.World;
            var learned = world.State.unlockedProducts;
            bool empty = learned.Count == 0;
            index = empty ? 0 : (index % learned.Count + learned.Count) % learned.Count;
            firstRecipe = index / entries.Length * entries.Length;
            for(int row = 0; row < entries.Length; row++)
            {
                int recipe = firstRecipe + row;
                var product = recipe < learned.Count ? world.Product(learned[recipe]) : null;
                entries[row].interactable = product != null;
                entryLabels[row].text = product == null ? "尚未解锁" : FreeCafeProductionText.Recipe(product.label);
                entryLabels[row].color = recipe == index && !empty
                    ? new Color(1f, .67f, .29f) : new Color(.98f, .91f, .78f, product == null ? .4f : 1f);
            }
            marker.gameObject.SetActive(!empty);
            marker.anchoredPosition = new Vector2(-400f, 187f - (index - firstRecipe) * 83f);
            previous.interactable = next.interactable = learned.Count > 1;
            page.text = empty ? "00 / 00" : (index + 1).ToString("00") + " / " + learned.Count.ToString("00");
            if(empty)
            {
                title.text = "咖啡谱";
                body.text = "<b>尚未学会咖啡</b>\n\n经营并解锁后，这里会显示制作说明。";
            }
            else
            {
                var product = world.Product(learned[index]);
                title.text = FreeCafeProductionText.Recipe(product.label);
                var text = new StringBuilder();
                text.AppendLine("<b>参考目标</b>\n").AppendLine(FreeCafeProductionText.Recipe(product.description));
                text.AppendLine().AppendLine(FreeCafeProductionText.Recipe(product.tutorial));
                text.AppendLine().AppendLine("<b>杯型 / 温度</b>\n");
                text.Append(world.Spec(product.cupSpecId).label).Append(" / ");
                text.AppendLine(product.requiredTemperature == LiquidTemperature.Boiling ? "沸腾" : product.requiredTemperature == LiquidTemperature.Warm ? "温热" : "常温");
                text.AppendLine().AppendLine("<b>参考用量</b>（自由操作，不锁步骤）\n");
                if(product.ingredients != null)
                    foreach(var portion in product.ingredients)
                        text.Append(FreeCafeProductionText.Recipe(world.Spec(portion.specId).label)).Append("  ")
                            .Append(portion.milliliters > 0 ? portion.milliliters.ToString("0") + " ml" : portion.units + " 份").AppendLine();
                body.text = text.ToString();
            }
            note.text = "<align=center><b>注意</b></align>\n\n计时注水完成的咖啡，品质由时间、水量和均匀性决定。\n\n任何容器都能自由加料；咖啡粉与水混合是冷萃，放到炉台并点击炉台加热完成萃取。\n\n空杯、半成品也能出品，但顾客会按实际质量评价。";
            if(selectedRecipe != index && bodyScroll != null) bodyScroll.verticalNormalizedPosition = 1f;
            selectedRecipe = index;
            Fit();
        }

        public void ShowPreview()
        {
            title.text = "精粹咖啡";
            body.text = "<b>参考目标</b>\n\n小杯 · 精粹咖啡 200 ml\n咖啡水准至少 70。\n\n研磨咖啡豆后，将咖啡粉装入精密萃取滤杯，以热水计时注入。\n\n参考 7.5 秒、200 ml，并保持均匀。\n停止注水结算后才得到精粹咖啡。\n加热普通冷萃只能得到黑咖啡，不能代替精粹。\n\n倒入杯中交付；按最终成分与含量评价。\n\n<b>杯型 / 温度</b>\n\n小杯 / 沸腾\n\n<b>参考用量</b>\n\n咖啡液 200 ml";
            note.text = "<align=center><b>注意</b></align>\n\n计时注水完成的精粹咖啡，品质由时间、水量和均匀性决定。\n\n任何容器都能自由加料；咖啡粉与水混合是冷萃，放到炉台并点击炉台加热完成萃取。\n\n空杯、半成品也能出品，但顾客会按实际质量评价。";
            page.text = "01 / 02";
            for(int index = 0; index < entries.Length; index++)
            {
                entryLabels[index].text = index == 0 ? "精粹咖啡" : index == 1 ? "黑咖啡" : "尚未解锁";
                entryLabels[index].color = index == 0 ? new Color(1f, .67f, .29f) : new Color(1f, .95f, .85f, index > 1 ? .4f : 1f);
                entries[index].interactable = index < 2;
            }
            marker.anchoredPosition = new Vector2(-400f, 187f);
            Fit();
        }
    }
}
