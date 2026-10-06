using UnityEngine;
using UnityEngine.InputSystem;

namespace Spotlight.GameFlow
{
    /// <summary>仅编辑器/Development Build 启用 F8，不在正式构建留下测试快捷键。</summary>
    public sealed class DialoguePreviewHotkey : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GameFlowBootstrap flow;
        private DialogueScriptAsset sample;
        private void Awake() => flow = GetComponent<GameFlowBootstrap>();
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f8Key.wasPressedThisFrame || DialoguePanel.IsAnyPlaying) return;
            if (flow != null && !flow.CanPreviewDialogue) return;
            if (sample == null) sample = Resources.Load<DialogueScriptAsset>("Dialogue/DialoguePreview");
            if (sample == null)
            {
                Debug.LogError("[Dialogue] Missing DialoguePreview.asset. Run Tools/Game Flow/Create Missing Dialogue Assets.", this);
                return;
            }
            DialoguePlayback.Play(sample);
        }
        private void OnDisable() => DialoguePlayback.Stop();
#endif
    }
}
