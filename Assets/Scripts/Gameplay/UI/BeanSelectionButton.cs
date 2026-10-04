using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.Gameplay
{
    /// <summary>
    /// 可复用的“筛选咖啡豆 / 当前制作动作”按钮。
    /// 视觉、交互状态和点击入口封装在预制体内，业务面板只需要订阅 Clicked。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeanSelectionButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image targetGraphic;

        public event Action Clicked;
        public Button Button => button;
        public bool Interactable => button != null && button.interactable;
        public string Text => label == null ? string.Empty : label.text;

        private void Awake()
        {
            ResolveReferences();
            button?.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            button?.onClick.RemoveListener(HandleClick);
            Clicked = null;
        }

        public void Bind(Action callback)
        {
            Clicked = null;
            if (callback != null) Clicked += callback;
        }

        public void AddListener(Action callback)
        {
            if (callback != null) Clicked += callback;
        }

        public void RemoveListener(Action callback)
        {
            if (callback != null) Clicked -= callback;
        }

        public void SetState(bool interactable, string text)
        {
            ResolveReferences();
            if (button != null) button.interactable = interactable;
            if (label != null && !string.IsNullOrEmpty(text)) label.text = text;
        }

        public void SetLabel(string text)
        {
            ResolveReferences();
            if (label != null) label.text = text ?? string.Empty;
        }

        public void SetReferences(Button buttonReference, TMP_Text labelReference, Image graphicReference = null)
        {
            button = buttonReference;
            label = labelReference;
            targetGraphic = graphicReference != null ? graphicReference : buttonReference?.targetGraphic as Image;
        }

        private void ResolveReferences()
        {
            if (button == null) button = GetComponent<Button>();
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            if (targetGraphic == null) targetGraphic = button?.targetGraphic as Image ?? GetComponent<Image>();
            if (button != null && button.targetGraphic == null) button.targetGraphic = targetGraphic;
        }

        private void HandleClick() => Clicked?.Invoke();
    }
}
