using System;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>营业期间的常驻操作，不是一段演出。</summary>
    public sealed class BusinessControlsPanel : UIPanel
    {
        [SerializeField] private Button endBusinessButton;
        private Button mapButton;
        private Action endBusiness;
        private GameFlowBootstrap flow;
        private CanvasGroup group;
        protected override void OnInit(IUIData data = null)
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            if (endBusinessButton != null)
            {
                endBusinessButton.onClick.AddListener(EndBusiness);
                mapButton = Instantiate(endBusinessButton, endBusinessButton.transform.parent);
                mapButton.name = "OpenMap";
                mapButton.onClick = new Button.ButtonClickedEvent();
                mapButton.onClick.AddListener(() => flow?.OpenMap());
                var rect = (RectTransform)mapButton.transform;
                rect.anchoredPosition += Vector2.left * (rect.rect.width + 16f);
                var label = mapButton.GetComponentInChildren<TMPro.TMP_Text>();
                if (label != null) label.text = "地图 / M";
            }
        }
        protected override void OnShow()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
        }
        public void Bind(Action onEndBusiness, GameFlowBootstrap owner)
        {
            endBusiness = onEndBusiness;
            flow = owner;
            RefreshInteraction();
        }
        private void Update() => RefreshInteraction();
        private void RefreshInteraction()
        {
            if (group == null) return;
            bool visible = Time.timeScale > 0f && (flow == null || flow.GameplayControlsAvailable);
            group.alpha = visible ? 1f : 0f;
            group.interactable = group.blocksRaycasts = visible;
        }
        private void EndBusiness()
        {
            if (group != null && group.interactable) endBusiness?.Invoke();
        }
        protected override void OnClose()
        {
            endBusiness = null;
            flow = null;
        }
        protected override void OnBeforeDestroy()
        {
            endBusiness = null;
            flow = null;
            if (endBusinessButton != null) endBusinessButton.onClick.RemoveListener(EndBusiness);
            base.OnBeforeDestroy();
        }
    }
}
