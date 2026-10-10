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
        [SerializeField] private RawImage thumbnail;
        private Action<SaveSlotView> selected;
        private Action<int> load;
        private Action<int> save;
        private Action<int> delete;
        private int index;
        private Texture2D thumbnailTexture;
        private string thumbnailData;
        private SaveSlotButtonFeedback loadFeedback;
        private SaveSlotButtonFeedback saveFeedback;
        private SaveSlotButtonFeedback deleteFeedback;
        private bool selectedState;
        private bool initialized;

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            // UIKit can bind/refresh this row under its inactive storage root.
            // A later Awake must not hide actions already populated by OnOpen.
            loadFeedback = InitializeFeedback(loadButton);
            saveFeedback = InitializeFeedback(saveButton);
            deleteFeedback = InitializeFeedback(deleteButton);
            if (loadButton != null) loadButton.onClick.AddListener(Load);
            if (saveButton != null) saveButton.onClick.AddListener(Save);
            if (deleteButton != null) deleteButton.onClick.AddListener(Delete);
            if (selectButton != null)
            {
                selectButton.navigation = new Navigation { mode = Navigation.Mode.None };
                selectButton.onClick.AddListener(Select);
            }
            SetAction(loadButton, loadFeedback, false);
            SetAction(saveButton, saveFeedback, false);
            SetAction(deleteButton, deleteFeedback, false);
            SetSelected(false);
        }

        private static SaveSlotButtonFeedback InitializeFeedback(Button button)
        {
            if (button == null) return null;
            SaveSlotButtonFeedback feedback = button.GetComponent<SaveSlotButtonFeedback>();
            if (feedback == null) feedback = button.gameObject.AddComponent<SaveSlotButtonFeedback>();
            feedback.Initialize();
            return feedback;
        }

        private void OnDestroy()
        {
            if (loadButton != null) loadButton.onClick.RemoveListener(Load);
            if (saveButton != null) saveButton.onClick.RemoveListener(Save);
            if (deleteButton != null) deleteButton.onClick.RemoveListener(Delete);
            if (selectButton != null) selectButton.onClick.RemoveListener(Select);
            selected = null;
            load = save = delete = null;
            if (thumbnailTexture != null) Destroy(thumbnailTexture);
        }

        public void BindSelection(Action<SaveSlotView> onSelected)
        {
            EnsureInitialized();
            selected = onSelected;
        }

        public void SetSelected(bool value)
        {
            EnsureInitialized();
            selectedState = value;
            UpdateActionsVisibility();
            if (selectionFrame != null) selectionFrame.SetActive(value);
            Color textColor = value ? new Color(1f, .93f, .83f) : new Color(.28f, .12f, .055f);
            if (slotLabel != null) slotLabel.color = textColor;
            if (detailsText != null) detailsText.color = textColor;
        }

        public void Bind(Action<int> onLoad, Action<int> onSave, Action<int> onDelete)
        {
            EnsureInitialized();
            load = onLoad;
            save = onSave;
            delete = onDelete;
        }

        public void SetThumbnail(string encoded)
        {
            if (encoded == thumbnailData) return;
            thumbnailData = encoded;
            if (thumbnailTexture != null) Destroy(thumbnailTexture);
            thumbnailTexture = null;
            if (!string.IsNullOrEmpty(encoded))
            {
                try
                {
                    thumbnailTexture = new Texture2D(2, 2);
                    thumbnailTexture.LoadImage(Convert.FromBase64String(encoded));
                }
                catch
                {
                    if (thumbnailTexture != null) Destroy(thumbnailTexture);
                    thumbnailTexture = null;
                }
            }
            if (thumbnail != null)
            {
                thumbnail.texture = thumbnailTexture;
                thumbnail.enabled = thumbnailTexture != null;
            }
        }

        public void Refresh(GameSaveSlotInfo slot, bool fromGameplay)
        {
            EnsureInitialized();
            if (slot == null)
            {
                SetAction(loadButton, loadFeedback, false);
                SetAction(saveButton, saveFeedback, false);
                SetAction(deleteButton, deleteFeedback, false);
                UpdateActionsVisibility();
                return;
            }
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

            // The selected slot exposes only operations that are meaningful for
            // this slot and this entry context. Hidden operations cannot be clicked
            // and do not leave a stray background/icon behind.
            bool canLoad = slot.CanLoad;
            bool canSave = fromGameplay && !slot.IsAutoSave && slot.State != GameSaveSlotState.Unavailable;
            bool canDelete = !slot.IsAutoSave && slot.IsOccupied
                && slot.State != GameSaveSlotState.Unavailable;
            SetAction(loadButton, loadFeedback, canLoad);
            SetAction(saveButton, saveFeedback, canSave);
            SetAction(deleteButton, deleteFeedback, canDelete);
            SetLabel(saveButton, slot.IsOccupied ? "覆盖保存" : "保存");
            UpdateActionsVisibility();
        }

        private static void SetAction(Button button, SaveSlotButtonFeedback feedback, bool available)
        {
            if (button == null) return;
            button.interactable = available;
            button.gameObject.SetActive(available);
            if (feedback != null) feedback.RefreshAvailability();
        }

        private static void SetLabel(Button button, string text)
        {
            if (button == null) return;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
        }

        private void UpdateActionsVisibility()
        {
            bool hasActions = Available(loadButton) || Available(saveButton) || Available(deleteButton);
            if (actionsRoot != null) actionsRoot.SetActive(selectedState && hasActions);
            // Keyboard/controller focus follows only visible actions, not hidden
            // save/delete controls or unrelated cards elsewhere in the grid.
            ConfigureNavigation(loadButton, deleteButton, saveButton);
            ConfigureNavigation(saveButton, loadButton, deleteButton);
            ConfigureNavigation(deleteButton, saveButton, loadButton);
        }

        private void ConfigureNavigation(Button button, Button previous, Button next)
        {
            if (button == null) return;
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = selectedState && Available(previous) ? previous : selectedState && Available(next) ? next : null,
                selectOnDown = selectedState && Available(next) ? next : selectedState && Available(previous) ? previous : null
            };
        }

        private static bool Available(Button button) => button != null && button.gameObject.activeSelf && button.interactable;
        private bool CanInvoke(Button button) => selectedState && button != null && button.IsActive() && button.IsInteractable();
        private void Select() => selected?.Invoke(this);
        private void Load() { if (CanInvoke(loadButton)) load?.Invoke(index); }
        private void Save() { if (CanInvoke(saveButton)) save?.Invoke(index); }
        private void Delete() { if (CanInvoke(deleteButton)) delete?.Invoke(index); }

        private static string FormatTime(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return string.Format("{0:00}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds);
        }
    }
}
