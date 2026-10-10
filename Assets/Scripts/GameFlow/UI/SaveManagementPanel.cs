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
        private bool initialized;

        protected override void Awake()
        {
            base.Awake();
            InitializeSlots();
        }

        protected override void OnInit(YokiFrame.IUIData data = null)
        {
            base.OnInit(data);
            InitializeSlots();
        }

        private void InitializeSlots()
        {
            if (initialized) return;
            initialized = true;
            // OnInit runs before OnOpen, even while UIKit's storage root is inactive.
            ResolveSlotViews();
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

        private void ResolveSlotViews()
        {
            // Replacing/duplicating the template row in the prefab can invalidate
            // Inspector references. Discover the existing grid rows, not new UI.
            bool missing = slots == null || slots.Length == 0;
            if (!missing)
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] == null) { missing = true; break; }
            if (!missing) return;
            GridLayoutGroup grid = GetComponentInChildren<GridLayoutGroup>(true);
            slots = grid != null ? grid.GetComponentsInChildren<SaveSlotView>(true)
                : GetComponentsInChildren<SaveSlotView>(true);
            System.Array.Sort(slots, (left, right) =>
                left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
        }

        protected override void OnOpen(YokiFrame.IUIData data = null)
        {
            base.OnOpen(data);
            InitializeSlots();
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
                    : "从标题界面进入时可读取存档、删除手动存档；不可保存";
            var saveSlots = Flow != null ? Flow.ListSaveSlots() : null;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null)
                {
                    GameSaveSlotInfo slot = saveSlots != null && i < saveSlots.Count ? saveSlots[i] : null;
                    slots[i].Refresh(slot, fromGameplay);
                    slots[i].SetThumbnail(slot != null && slot.CanLoad && Flow != null ? Flow.GetThumbnail(slot.Index) : null);
                }
        }
    }
}
