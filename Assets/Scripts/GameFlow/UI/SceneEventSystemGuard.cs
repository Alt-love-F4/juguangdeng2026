using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.GameFlow
{
    // Scene-local input remains usable when opened alone, but yields to the persistent menu system.
    [DefaultExecutionOrder(-10000)]
    [RequireComponent(typeof(EventSystem))]
    public sealed class SceneEventSystemGuard : MonoBehaviour
    {
        private void Awake()
        {
            var local = GetComponent<EventSystem>();
            foreach (var other in FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other == local || !other.isActiveAndEnabled || other.gameObject.scene.name != "DontDestroyOnLoad") continue;
                local.enabled = false;
                foreach (var module in GetComponents<BaseInputModule>()) module.enabled = false;
                break;
            }
        }
    }
}
