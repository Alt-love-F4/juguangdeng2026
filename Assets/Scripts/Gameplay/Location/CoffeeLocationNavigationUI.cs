using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.Gameplay
{
    /// <summary>右下角地点导航 UI。按钮只发送地点意图，不直接操作场景对象。</summary>
    public sealed class CoffeeLocationNavigationUI : MonoBehaviour
    {
        public CoffeeLocationController controller;
        public Button cafeButton;
        public Button counterButton;
        public Button secondFloorButton;
        public Button basementButton;
        public TMP_Text currentLocationText;

        private void OnEnable()
        {
            if (controller == null) controller = FindFirstObjectByType<CoffeeLocationController>();
            if (controller != null) controller.LocationChanged += OnLocationChanged;
            if (cafeButton != null) cafeButton.onClick.AddListener(OnCafeClicked);
            if (counterButton != null) counterButton.onClick.AddListener(OnCounterClicked);
            if (secondFloorButton != null) secondFloorButton.onClick.AddListener(OnSecondFloorClicked);
            if (basementButton != null) basementButton.onClick.AddListener(OnBasementClicked);
            Refresh();
        }

        private void OnDisable()
        {
            if (controller != null) controller.LocationChanged -= OnLocationChanged;
            if (cafeButton != null) cafeButton.onClick.RemoveListener(OnCafeClicked);
            if (counterButton != null) counterButton.onClick.RemoveListener(OnCounterClicked);
            if (secondFloorButton != null) secondFloorButton.onClick.RemoveListener(OnSecondFloorClicked);
            if (basementButton != null) basementButton.onClick.RemoveListener(OnBasementClicked);
        }

        private bool wasBlocked;
        private void Update()
        {
            if (controller != null && wasBlocked != controller.NavigationBlocked) Refresh();
        }
        private void OnCafeClicked() { if (controller != null) controller.Enter(CoffeeLocationId.FirstFloorCafe); }
        private void OnCounterClicked() { if (controller != null) controller.Enter(CoffeeLocationId.FirstFloorServingCounter); }
        private void OnSecondFloorClicked() { if (controller != null) controller.Enter(CoffeeLocationId.SecondFloor); }
        private void OnBasementClicked() { if (controller != null) controller.Enter(CoffeeLocationId.Basement); }
        private void OnLocationChanged(CoffeeLocationId _) => Refresh();

        private void Refresh()
        {
            if (controller == null) return;
            wasBlocked = controller.NavigationBlocked;
            SetInteractable(cafeButton, CoffeeLocationId.FirstFloorCafe);
            SetInteractable(counterButton, CoffeeLocationId.FirstFloorServingCounter);
            SetInteractable(secondFloorButton, CoffeeLocationId.SecondFloor);
            SetInteractable(basementButton, CoffeeLocationId.Basement);
            if (currentLocationText != null)
                currentLocationText.text = LocationName(controller.CurrentLocation);
        }

        private void SetInteractable(Button button, CoffeeLocationId location)
        {
            if (button != null) button.interactable = !controller.NavigationBlocked && controller.HasLocation(location) && controller.CurrentLocation != location;
        }

        public static string LocationName(CoffeeLocationId location) => location switch
        {
            CoffeeLocationId.FirstFloorCafe => "一楼 · 营业区",
            CoffeeLocationId.FirstFloorServingCounter => "一楼 · 出餐台",
            CoffeeLocationId.SecondFloor => "顶楼",
            CoffeeLocationId.Basement => "地下室",
            _ => location.ToString()
        };
    }
}
