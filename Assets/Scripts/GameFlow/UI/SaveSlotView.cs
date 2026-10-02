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
        private Action<int> load;
        private Action<int> save;
        private Action<int> delete;
        private int index;

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
                GameSaveSlotState.Empty => "空",
                GameSaveSlotState.Corrupt => "损坏",
                GameSaveSlotState.Incompatible => "版本不兼容",
                GameSaveSlotState.Unavailable => "不可用",
                _ => (string.IsNullOrEmpty(slot.SceneName) ? "未知场景" : slot.SceneName) + "  " + FormatTime(slot.ElapsedPlaytimeSeconds)
            };
            if (slotLabel != null)
                slotLabel.text = (slot.IsAutoSave ? "自动存档" : "存档 " + slot.Index) + "  " + state;
            if (loadButton != null) loadButton.interactable = slot.CanLoad;
            if (saveButton != null) saveButton.interactable = fromGameplay && !slot.IsAutoSave;
            if (deleteButton != null) deleteButton.interactable = fromGameplay && !slot.IsAutoSave && slot.IsOccupied;
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
