using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Spotlight.Gameplay
{
    /// <summary>
    /// CoffeeShop 沙盒测试引导：直接从 CoffeeShop 场景进入 Play Mode 时，
    /// 自动补齐 EventSystem / Physics2DRaycaster / DOTween / 控制器 / 面板，
    /// 无需经过 SampleScene 的 GameFlowBootstrap 流程。
    /// </summary>
    public static class CoffeeShopSandbox
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            // 正式咖啡店与独立 UI/实体测试场景使用同一引导。
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "CoffeeShop" && scene.name != "CoffeeUIEntitySandbox") return;

            EnsureEventSystem();
            EnsureCameraRaycaster();
            InitDOTween();

            // 确保控制器存在
            var ctrl = CoffeeGameplayController.EnsureExists();

            // 激活场景内面板
            var panel = Object.FindFirstObjectByType<CoffeeGameplayPanel>(FindObjectsInactive.Include);
            if (panel != null)
            {
                panel.gameObject.SetActive(true);
                // 沙盒模式下没有 GameFlowBootstrap，直接打开
                panel.OpenFromFlow(null);
            }

            Debug.Log("[Sandbox] CoffeeShop 沙盒模式已启动，可直接测试玩法。");
        }

        /// <summary>UI 按钮点击依赖 EventSystem；项目用新 Input System，必须配 InputSystemUIInputModule。</summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Debug.Log("[Sandbox] 已自动创建 EventSystem (InputSystemUIInputModule)");
        }

        /// <summary>世界物体的 OnMouseDown/OnMouseEnter 需要相机挂 Physics2DRaycaster。</summary>
        private static void EnsureCameraRaycaster()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = GameObject.Find("Main Camera");
                if (camGo != null) cam = camGo.GetComponent<Camera>();
            }
            if (cam == null)
            {
                Debug.LogWarning("[Sandbox] 场景中没有 Main Camera，世界物体点击不可用");
                return;
            }
            if (cam.GetComponent<Physics2DRaycaster>() == null)
            {
                cam.gameObject.AddComponent<Physics2DRaycaster>();
                Debug.Log("[Sandbox] 已给 Main Camera 添加 Physics2DRaycaster（世界物体点击）");
            }
        }

        /// <summary>CoffeeShopSceneView 的动画依赖 DOTween，绕过 GameFlow 时需手动初始化。</summary>
        private static void InitDOTween()
        {
            DOTween.Init(recycleAllByDefault: true, useSafeMode: true, logBehaviour: LogBehaviour.ErrorsOnly);
        }
    }
}
