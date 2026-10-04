using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Spotlight.Gameplay
{
    /// <summary>
    /// 豆筛选小游戏：4x5 格子中点选好豆（深棕色），避开坏豆（浅色/黑色）。
    /// </summary>
    public sealed class BeanSelectionMinigame : MonoBehaviour
    {
        [Header("UI 引用")]
        public GridLayoutGroup grid;
        public GameObject cellPrefab;
        public Button confirmButton;
        public TMP_Text scoreText;
        public TMP_Text hintText;

        [Header("游戏参数")]
        public int rows = 4;
        public int cols = 5;
        public int goodBeanCount = 8;
        public Color goodColor = new Color(0.4f, 0.2f, 0.1f);   // 深棕
        public Color badLightColor = new Color(0.8f, 0.7f, 0.6f); // 浅
        public Color badDarkColor = new Color(0.1f, 0.1f, 0.1f);  // 黑

        private readonly List<BeanCell> cells = new();
        private int selectedCount;
        private System.Action<bool, int> onComplete;

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(Confirm);
        }

        public void Open(System.Action<bool, int> callback)
        {
            onComplete = callback;
            gameObject.SetActive(true);
            GenerateGrid();
            if (hintText != null)
                hintText.text = "点击选择深棕色的好豆\n避开浅色和黑色的坏豆";
        }

        public void Cancel() => onComplete = null;
        private void OnDisable() => Cancel();

        private void GenerateGrid()
        {
            // 清理旧格子
            foreach (var cell in cells)
                if (cell != null) Destroy(cell.gameObject);
            cells.Clear();
            selectedCount = 0;
            UpdateScore();

            // 随机分配好豆位置
            var goodIndices = new HashSet<int>();
            int total = Mathf.Max(1, rows * cols);
            goodBeanCount = Mathf.Clamp(goodBeanCount, 1, total);
            while (goodIndices.Count < goodBeanCount)
                goodIndices.Add(Random.Range(0, total));

            // 创建格子
            for (int i = 0; i < total; i++)
            {
                var go = Instantiate(cellPrefab, grid.transform);
                var cell = go.GetComponent<BeanCell>();
                if (cell == null) cell = go.AddComponent<BeanCell>();
                bool isGood = goodIndices.Contains(i);
                Color color = isGood ? goodColor : (Random.value > 0.5f ? badLightColor : badDarkColor);
                cell.Init(isGood, color, OnCellClicked);
                cells.Add(cell);
            }
        }

        private void OnCellClicked(BeanCell cell)
        {
            if (cell.IsSelected) return;
            cell.Select();
            selectedCount++;
            UpdateScore();
            if (selectedCount >= goodBeanCount)
                Confirm();
        }

        private void UpdateScore()
        {
            if (scoreText != null)
                scoreText.text = $"已选: {selectedCount}/{goodBeanCount}";
        }

        private void Confirm()
        {
            int correct = 0, wrong = 0;
            foreach (var cell in cells)
            {
                if (!cell.IsSelected) continue;
                if (cell.IsGood) correct++;
                else wrong++;
            }
            bool success = correct >= goodBeanCount * 0.8f && wrong <= 2;
            var callback = onComplete; onComplete = null;
            gameObject.SetActive(false);
            callback?.Invoke(success, correct * 10 - wrong * 5);
        }

        /// <summary>单个豆格子</summary>
        public sealed class BeanCell : MonoBehaviour
        {
            public bool IsGood { get; private set; }
            public bool IsSelected { get; private set; }

            private Image image;
            private System.Action<BeanCell> onClick;

            public void Init(bool isGood, Color color, System.Action<BeanCell> callback)
            {
                IsGood = isGood;
                IsSelected = false;
                onClick = callback;
                image = GetComponent<Image>();
                if (image != null) image.color = color;
                var btn = GetComponent<Button>();
                if (btn == null) btn = gameObject.AddComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onClick?.Invoke(this));
            }

            public void Select()
            {
                IsSelected = true;
                if (image != null)
                    image.color = Color.Lerp(image.color, Color.white, 0.3f);
            }
        }
    }
}
