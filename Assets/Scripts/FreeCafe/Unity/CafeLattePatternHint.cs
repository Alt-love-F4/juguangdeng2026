using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.FreeCafe
{
    public sealed class CafeLattePatternHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform panel;
        public TMP_Text heading, body;
        public string patternName, meaning;
        public void OnPointerEnter(PointerEventData data)
        {
            if(panel==null) return;
            heading.text=patternName;body.text=meaning;
            panel.gameObject.SetActive(true);panel.SetAsLastSibling();
            var parent=panel.parent as RectTransform;
            if(parent!=null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,data.position,data.enterEventCamera,out var local))
            {
                var bounds=parent.rect;var size=panel.sizeDelta;
                panel.anchoredPosition=new Vector2(Mathf.Clamp(local.x+24,bounds.xMin,bounds.xMax-size.x),
                    Mathf.Clamp(local.y+35,bounds.yMin+size.y,bounds.yMax));
            }
        }
        public void OnPointerExit(PointerEventData data) { if(panel!=null) panel.gameObject.SetActive(false); }
        private void OnDisable() { if(panel!=null) panel.gameObject.SetActive(false); }
    }
}
