using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.GameFlow
{
    /// <summary>将正文中的 TMP link 点击与悬停转发给信件面板。</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class FirstDayLetterLinks : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
    {
        private FirstDayLetterPanel panel;
        private TMP_Text text;
        private void Awake()
        {
            text = GetComponent<TMP_Text>();
            panel = GetComponentInParent<FirstDayLetterPanel>();
        }
        private string Hit(PointerEventData eventData)
        {
            int index = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventData.pressEventCamera);
            if (index < 0) return null;
            TMP_LinkInfo link = text.textInfo.linkInfo[index];
            // 不允许点击逐字演出中尚未出现的选项。
            if (link.linkTextfirstCharacterIndex + link.linkTextLength > text.maxVisibleCharacters) return null;
            return link.GetLinkID();
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) panel?.ActivateLetterLink(Hit(eventData));
        }
        public void OnPointerMove(PointerEventData eventData) => panel?.HoverLetterLink(Hit(eventData));
        public void OnPointerExit(PointerEventData eventData) => panel?.HoverLetterLink(null);
    }
}
