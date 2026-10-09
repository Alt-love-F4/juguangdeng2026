using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed class SaveSlotView : MonoBehaviour
    {
        [SerializeField] private TMP_Text slotLabel;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button selectButton;
        [SerializeField] private GameObject actionsRoot;
        [SerializeField] private GameObject selectionFrame;
        [SerializeField] private TMP_Text detailsText;
        private Action<SaveSlotView> selected;
        private Action<int> load;
        private Action<int> save;
        private Action<int> delete;
        private int index;
        [SerializeField] private RawImage thumbnail;
        private Texture2D thumbnailTexture;
        private string thumbnailData;
        public void SetThumbnail(string encoded)
        {
            if (encoded == thumbnailData) return;
            thumbnailData = encoded;
            if (thumbnailTexture != null) Destroy(thumbnailTexture);
            thumbnailTexture = null;
            if (!string.IsNullOrEmpty(encoded))
            {
                try { thumbnailTexture = new Texture2D(2, 2); thumbnailTexture.LoadImage(Convert.FromBase64String(encoded)); }
                catch { if (thumbnailTexture != null) Destroy(thumbnailTexture); thumbnailTexture = null; }
            }
            if (thumbnail != null) { thumbnail.texture = thumbnailTexture; thumbnail.enabled = thumbnailTexture != null; }
        }
        private void OnDestroy() { if (thumbnailTexture != null) Destroy(thumbnailTexture); }

        public void BindSelection(Action<SaveSlotView> onSelected)
        {
            selected = onSelected;
            selectButton?.onClick.AddListener(() => selected?.Invoke(this));
        }

        public void SetSelected(bool value)
        {
            if (actionsRoot != null) actionsRoot.SetActive(value);
            if (selectionFrame != null) selectionFrame.SetActive(value);
            Color textColor = value ? new Color(1f, .93f, .83f) : new Color(.28f, .12f, .055f);
            if (slotLabel != null) slotLabel.color = textColor;
            if (detailsText != null) detailsText.color = textColor;
        }

        public void Bind(Action<int> onLoad, Action<int> onSave, Action<int> onDelete)
        {
            load = onLoad;
            save = onSave;
            delete = onDelete;
            loadButton?.onClick.AddListener(Load);
            saveButton?.onClick.AddListener(Save);
            deleteButton?.onClick.AddListener(Delete);
        }

        public void Refresh(GameSaveSlotInfo slot, bool fromGameplay)
        {
            if (slot == null) return;
            index = slot.Index;
            string state = slot.State switch
            {
                GameSaveSlotState.Empty => "空存档位",
                GameSaveSlotState.Corrupt => "损坏",
                GameSaveSlotState.Incompatible => "版本不兼容",
                GameSaveSlotState.Unavailable => "不可用",
                _ => (string.IsNullOrEmpty(slot.SceneName) ? "未知场景" : slot.SceneName) + "  " + FormatTime(slot.ElapsedPlaytimeSeconds)
            };
            if (slotLabel != null)
                slotLabel.text = slot.State == GameSaveSlotState.Empty
                    ? "空白存档"
                    : "槽 " + (slot.Index + 1) + (slot.IsAutoSave ? "（自动存档）" : "");
            if (detailsText != null)
                detailsText.text = slot.CanLoad
                    ? (slot.SavedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? string.Empty)
                        + "\n游玩时长 " + FormatTime(slot.ElapsedPlaytimeSeconds)
                        + "\n" + (string.IsNullOrEmpty(slot.SceneName) ? "未知场景" : slot.SceneName)
                    : slot.State == GameSaveSlotState.Empty ? string.Empty : state;
            if (loadButton != null) loadButton.interactable = slot.CanLoad;
            if (saveButton != null)
            {
                saveButton.gameObject.SetActive(true);
                saveButton.interactable = fromGameplay && !slot.IsAutoSave;
                var label = saveButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = "保存";
            }
            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(true);
                deleteButton.interactable = !slot.IsAutoSave && slot.IsOccupied;
            }
        }

        private void Load() => load?.Invoke(index);
        private void Save() => save?.Invoke(index);
        private void Delete() => delete?.Invoke(index);

        private static string FormatTime(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return string.Format("{0:00}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds);
        }
    }
}
