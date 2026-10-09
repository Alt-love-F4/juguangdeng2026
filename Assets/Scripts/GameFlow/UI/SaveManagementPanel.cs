using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed class SaveManagementPanel : GameFlowPanel
    {
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private SaveSlotView[] slots = new SaveSlotView[6];
        [SerializeField] private Button backButton;
        private bool fromGameplay;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < slots.Length; i++)
            {
                SaveSlotView slot = slots[i];
                if (slot != null)
                    slot.Bind(
                        index => Flow?.ConfirmLoad(index),
                        index => Flow?.ConfirmSave(index),
                        index => Flow?.ConfirmDelete(index));
                if (slot != null) slot.BindSelection(SelectSlot);
            }
            backButton?.onClick.AddListener(() => Flow?.BackToPrevious());
        }

        protected override void OnOpen(YokiFrame.IUIData data = null)
        {
            base.OnOpen(data);
            GameFlowPanelData panelData = data as GameFlowPanelData;
            fromGameplay = panelData != null && panelData.FromGameplay;
            SelectSlot(null);
            RefreshSlots();
        }

        private void SelectSlot(SaveSlotView selected)
        {
            foreach (SaveSlotView slot in slots)
                if (slot != null) slot.SetSelected(slot == selected);
        }

        public void RefreshSlots()
        {
            if (descriptionText != null)
                descriptionText.text = fromGameplay
                    ? "自动存档位仅可读取；手动存档位可读取、保存或删除"
                    : "从标题界面进入时仅可读取存档";
            var saveSlots = Flow != null ? Flow.ListSaveSlots() : null;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && saveSlots != null && i < saveSlots.Count)
                {
                    slots[i].Refresh(saveSlots[i], fromGameplay);
                    slots[i].SetThumbnail(saveSlots[i].CanLoad ? Flow.GetThumbnail(i) : null);
                }
        }
    }
}
