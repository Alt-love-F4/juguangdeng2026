using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Gameplay
{
    /// <summary>切换可见区域，不卸载营业模型、库存或放置记录。</summary>
    public sealed class CoffeeLocationController : MonoBehaviour
    {
        [Serializable]
        public sealed class LocationBinding
        {
            public CoffeeLocationId id;
            public GameObject root;
            public GameObject[] visibleRoots;
        }

        public LocationBinding[] locations = Array.Empty<LocationBinding>();
        public CoffeeLocationId initialLocation = CoffeeLocationId.FirstFloorCafe;
        public CoffeeWorldRuntime interactionWorld;
        public CoffeeLocationId CurrentLocation { get; private set; }
        public bool NavigationBlocked => Time.timeScale <= 0f || CoffeeGameplayPanel.IsMinigameOpen;
        public event Action<CoffeeLocationId> LocationChanged;
        private bool initialized;

        private void Awake()
        {
            if (locations == null) locations = Array.Empty<LocationBinding>();
            if (interactionWorld == null) interactionWorld = FindFirstObjectByType<CoffeeWorldRuntime>();
        }
        private void Start() => Enter(initialLocation, true);
        public bool HasLocation(CoffeeLocationId id)
        {
            foreach (var binding in locations)
                if (binding != null && binding.id == id && binding.root != null) return true;
            return false;
        }
        public void Enter(CoffeeLocationId id) => Enter(id, false);
        private void Enter(CoffeeLocationId id, bool force)
        {
            if (!force && NavigationBlocked) return;
            if (initialized && !force && CurrentLocation == id) return;
            if (!HasLocation(id))
            {
                Debug.LogWarning($"[CoffeeLocation] 找不到地点绑定：{id}", this);
                return;
            }
            // 切换前释放拖拽所有权；临时取出的物品不落账，已放置物品回到原位。
            if (interactionWorld != null) interactionWorld.CancelDrag();
            CurrentLocation = id;
            ApplyVisibility(locations, id);
            initialized = true;
            LocationChanged?.Invoke(id);
            Debug.Log($"[CoffeeLocation] 已进入：{id}", this);
        }

        /// <summary>共享根节点取可见性的并集，避免重复绑定把共享 HUD 误关掉。</summary>
        public static void ApplyVisibility(LocationBinding[] bindings, CoffeeLocationId active)
        {
            var visibility = new Dictionary<GameObject, bool>();
            if (bindings == null) return;
            foreach (var binding in bindings)
            {
                if (binding == null) continue;
                Include(binding.root, binding.id == active);
                if (binding.visibleRoots != null)
                    foreach (var root in binding.visibleRoots) Include(root, binding.id == active);
            }
            foreach (var entry in visibility)
                if (entry.Key.activeSelf != entry.Value) entry.Key.SetActive(entry.Value);
            void Include(GameObject root, bool visible)
            {
                if (root == null) return;
                visibility.TryGetValue(root, out var previous);
                visibility[root] = previous || visible;
            }
        }
    }
}
