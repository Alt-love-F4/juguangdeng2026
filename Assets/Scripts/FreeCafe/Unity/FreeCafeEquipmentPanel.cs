using System;
using Spotlight.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>Player-facing equipment activation; the world owns prices and saved unlocks.</summary>
    public sealed class FreeCafeEquipmentPanel : MonoBehaviour
    {
        private FreeCafeRuntime runtime;
        private GameObject root;
        private Button entry, precision, latte;
        private TMP_Text progress, precisionText, latteText;
        private TMP_FontAsset font;
        private IDisposable pause;
        public bool IsOpen => root != null && root.activeSelf;

        public void Initialize(FreeCafeRuntime owner, FreeCafeHud hud)
        {
            runtime = owner;
            if (hud == null) return;
            var sample = hud.GetComponentInChildren<TMP_Text>(true);
            font = sample != null ? sample.font : Resources.Load<TMP_FontAsset>("Fonts/乐米曲奇方块体 SDF");
            entry = Button(hud.transform, "EquipmentActivation", "设备升级", new Vector2(172, 52), out _);
            var er = (RectTransform)entry.transform;
            er.anchorMin = er.anchorMax = new Vector2(.5f, 1);
            er.anchoredPosition = new Vector2(0, -180);
            entry.onClick.AddListener(Open);
            entry.gameObject.SetActive(runtime.showEquipmentUpgradeUI);
            root = new GameObject("EquipmentActivationPanel", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(hud.transform, false);
            var backdrop = root.GetComponent<Image>();
            backdrop.color = new Color(0, 0, 0, .65f);
            var rr = (RectTransform)root.transform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(root.transform, false);
            card.GetComponent<Image>().color = new Color(.12f, .1f, .085f, 1);
            var cr = (RectTransform)card.transform; cr.sizeDelta = new Vector2(820, 490);
            Text(card.transform, "Title", "设备升级", new Vector2(740, 55), new Vector2(0, 195), 32);
            progress = Text(card.transform, "Progress", "", new Vector2(740, 90), new Vector2(0, 115), 23);
            precision = Button(card.transform, "ActivatePrecision", "", new Vector2(740, 88), out precisionText);
            ((RectTransform)precision.transform).anchoredPosition = new Vector2(0, 15);
            precision.onClick.AddListener(() => Activate(false));
            latte = Button(card.transform, "ActivateLatte", "", new Vector2(740, 88), out latteText);
            ((RectTransform)latte.transform).anchoredPosition = new Vector2(0, -90);
            latte.onClick.AddListener(() => Activate(true));
            var close = Button(card.transform, "Close", "返回", new Vector2(170, 50), out _);
            ((RectTransform)close.transform).anchoredPosition = new Vector2(0, -195);
            close.onClick.AddListener(Close);
            root.SetActive(false);
        }

        private void Open()
        {
            if (runtime == null || !runtime.showEquipmentUpgradeUI || runtime.InputBlocked || runtime.IsDragging || runtime.PracticeTutorialActive || runtime.CafeTutorialActive) return;
            runtime.CancelDrag();
            root.SetActive(true); root.transform.SetAsLastSibling();
            pause = PresentationPauseRegistry.Acquire("Cafe equipment activation");
            Refresh();
        }
        private void Activate(bool isLatte)
        {
            if (runtime == null || runtime.CafeTutorialActive || runtime.PracticeTutorialActive) return;
            runtime.Report(runtime.World.ActivateEquipment(isLatte));
            Refresh();
        }
        private void Refresh()
        {
            var w = runtime.World; var s = w.State;
            progress.text = runtime.IsSandboxMode ? "自由测试模式：全部道具已开放，客人随机生成。" : "累计成功订单：" + s.successfulOrders + "　可用王国币：" + s.money + "\n先学基础调配，再学习精粹，最后学习拉花。";
            precisionText.text = w.PrecisionEquipmentAvailable ? "精粹设备　已激活" : "精粹设备　成功 " + CafeWorld.PrecisionOrdersRequired + " 单 / " + CafeWorld.PrecisionActivationCost + " 王国币\n解锁均匀注水萃取与精粹咖啡配方";
            latteText.text = w.LatteEquipmentAvailable ? "拉花　已激活" : "拉花　成功 " + CafeWorld.LatteOrdersRequired + " 单 / " + CafeWorld.LatteActivationCost + " 王国币\n需先激活精粹设备；解锁打奶、绘制图案与拉花饮品";
            bool ready = !s.open && !w.HasTutorialCustomer && !runtime.IsSandboxMode;
            precision.interactable = ready && !w.PrecisionEquipmentAvailable && s.successfulOrders >= CafeWorld.PrecisionOrdersRequired && s.money >= CafeWorld.PrecisionActivationCost;
            latte.interactable = ready && w.PrecisionEquipmentAvailable && !w.LatteEquipmentAvailable && s.successfulOrders >= CafeWorld.LatteOrdersRequired && s.money >= CafeWorld.LatteActivationCost;
            if (s.open) progress.text += "\n请打烊后激活设备。";
        }
        private void Update()
        {
            if (entry == null || runtime == null || runtime.World == null) return;
            bool visible = runtime.showEquipmentUpgradeUI && !runtime.CafeTutorialActive && !runtime.PracticeTutorialActive;
            entry.gameObject.SetActive(visible);
            if (!visible && IsOpen) Close();
            entry.interactable = !IsOpen && !runtime.InputBlocked;
            if (IsOpen && UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }
        public void Close()
        {
            if (root != null) root.SetActive(false);
            pause?.Dispose(); pause = null;
        }
        private void OnDisable() => Close();
        private void OnDestroy()
        {
            Close();
            if (entry != null) Destroy(entry.gameObject);
            if (root != null) Destroy(root);
        }
        private TMP_Text Text(Transform parent, string name, string value, Vector2 size, Vector2 position, float pointSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = pointSize;
            text.text = value; text.color = new Color(1, .94f, .8f); text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position;
            return text;
        }
        private Button Button(Transform parent, string name, string value, Vector2 size, out TMP_Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            go.GetComponent<Image>().color = new Color(.31f, .22f, .15f);
            label = Text(go.transform, "Label", value, size - new Vector2(20, 8), Vector2.zero, 23);
            return go.GetComponent<Button>();
        }
    }
}
