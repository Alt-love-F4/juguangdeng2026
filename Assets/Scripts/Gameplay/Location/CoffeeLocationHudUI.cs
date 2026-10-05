using System;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.Gameplay
{
    /// <summary>常驻 HUD 的区域上下文和可折叠管理面板。只管理显示，不保存营业数据。</summary>
    public sealed class CoffeeLocationHudUI : MonoBehaviour
    {
        public CoffeeLocationController controller;
        public GameObject[] businessControls = Array.Empty<GameObject>();
        public GameObject[] counterControls = Array.Empty<GameObject>();
        public Button ordersButton, inventoryButton, upgradesButton;
        public GameObject ordersPanel, inventoryPanel, upgradesPanel;
        private void OnEnable()
        {
            if (controller == null) controller = FindFirstObjectByType<CoffeeLocationController>();
            if (controller != null) controller.LocationChanged += Refresh;
            if (ordersButton != null) ordersButton.onClick.AddListener(ToggleOrders);
            if (inventoryButton != null) inventoryButton.onClick.AddListener(ToggleInventory);
            if (upgradesButton != null) upgradesButton.onClick.AddListener(ToggleUpgrades);
            if (controller != null) Refresh(controller.CurrentLocation);
        }
        private void OnDisable()
        {
            if (controller != null) controller.LocationChanged -= Refresh;
            if (ordersButton != null) ordersButton.onClick.RemoveListener(ToggleOrders);
            if (inventoryButton != null) inventoryButton.onClick.RemoveListener(ToggleInventory);
            if (upgradesButton != null) upgradesButton.onClick.RemoveListener(ToggleUpgrades);
        }
        private void Refresh(CoffeeLocationId location)
        {
            bool counter = location == CoffeeLocationId.FirstFloorServingCounter;
            bool business = counter || location == CoffeeLocationId.FirstFloorCafe;
            foreach (var go in businessControls) if (go != null) go.SetActive(business);
            foreach (var go in counterControls) if (go != null) go.SetActive(counter);
            HidePanels();
        }
        private void ToggleOrders() => Toggle(ordersPanel);
        private void ToggleInventory() => Toggle(inventoryPanel);
        private void ToggleUpgrades() => Toggle(upgradesPanel);
        private void Toggle(GameObject panel)
        {
            if (panel == null || (controller != null && controller.NavigationBlocked)) return;
            bool show = !panel.activeSelf; HidePanels(); panel.SetActive(show);
        }
        private void HidePanels()
        {
            if (ordersPanel != null) ordersPanel.SetActive(false);
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            if (upgradesPanel != null) upgradesPanel.SetActive(false);
        }
    }
}
