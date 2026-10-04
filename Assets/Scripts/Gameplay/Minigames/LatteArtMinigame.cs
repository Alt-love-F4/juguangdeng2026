using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
namespace Spotlight.Gameplay
{
    public sealed class LatteArtMinigame : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public RawImage canvasImage;
        public Button finishButton, resetButton;
        public Slider progressSlider;
        public TMP_Text hintText;
        public int canvasSize = 256;
        public Color coffeeColor = new(.4f, .2f, .1f), milkColor = new(1f, .96f, .86f);
        public float baseBrushSize = 7, maxBrushSize = 15, thickenSpeed = 4;
        private Texture2D texture;
        private Color[] pixels;
        private bool drawing;
        private Vector2 last;
        private float brush;
        private Action<float, string> completed;
        private void Awake() { finishButton?.onClick.AddListener(Finish); resetButton?.onClick.AddListener(Reset); }
        public void Open(Action<float, string> callback) { completed = callback; gameObject.SetActive(true); Reset(); }
        public void Cancel() { drawing = false; completed = null; }
        private void OnDisable() => Cancel();
        private void OnDestroy() { if (texture != null) Destroy(texture); }
        public void Reset()
        {
            drawing = false; if (texture != null) Destroy(texture);
            texture = new Texture2D(canvasSize, canvasSize, TextureFormat.RGBA32, false); pixels = new Color[canvasSize * canvasSize];
            Array.Fill(pixels, coffeeColor); texture.SetPixels(pixels); texture.Apply(); canvasImage.texture = texture;
            if (hintText != null) hintText.text = "按住并拖动绘制 · 停留可加粗 · 覆盖约15%最佳";
            RefreshProgress();
        }
        private Vector2 Position(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasImage.rectTransform, e.position, e.pressEventCamera, out var p);
            var rect = canvasImage.rectTransform.rect; return new Vector2((p.x / rect.width + .5f) * canvasSize, (p.y / rect.height + .5f) * canvasSize);
        }
        public void OnPointerDown(PointerEventData e) { if (Time.timeScale <= 0 || texture == null) return; drawing = true; brush = baseBrushSize; last = Position(e); Stamp(last); Apply(); }
        public void OnPointerUp(PointerEventData e) => drawing = false;
        public void OnDrag(PointerEventData e)
        {
            if (!drawing || Time.timeScale <= 0) return;
            var next = Position(e); int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(last, next) / 2));
            for (int i = 1; i <= steps; i++) Stamp(Vector2.Lerp(last, next, i / (float)steps)); last = next; Apply();
        }
        private void Update() { if (drawing && Time.timeScale > 0) { brush = Mathf.Min(maxBrushSize, brush + thickenSpeed * Time.deltaTime); Stamp(last); Apply(); } }
        private void Stamp(Vector2 pos)
        {
            int radius = Mathf.CeilToInt(brush);
            for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
            {
                int px = Mathf.RoundToInt(pos.x) + x, py = Mathf.RoundToInt(pos.y) + y;
                if (px < 0 || py < 0 || px >= canvasSize || py >= canvasSize || x * x + y * y > brush * brush) continue;
                pixels[py * canvasSize + px] = milkColor;
            }
        }
        private void Apply() { texture.SetPixels(pixels); texture.Apply(); RefreshProgress(); }
        private float Coverage() { int count = 0; foreach (var p in pixels) if (p != coffeeColor) count++; return count / (float)pixels.Length; }
        private void RefreshProgress() { if (progressSlider != null) progressSlider.value = Mathf.Clamp01(Coverage() / .3f); }
        public void Finish()
        {
            float coverage = Coverage(); float score = coverage <= 0 ? 0 : Mathf.Clamp(100f * (1 - Mathf.Abs(coverage - .15f) / .15f), 10, 100);
            var callback = completed; completed = null; drawing = false; gameObject.SetActive(false); callback?.Invoke(score, "自由拉花");
        }
    }
}
