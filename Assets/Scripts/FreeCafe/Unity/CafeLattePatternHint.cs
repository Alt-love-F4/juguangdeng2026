using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    public sealed class CafeLattePatternHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform panel;
        public TMP_Text heading, body;
        public string patternName, meaning;
        [Header("Optional detail rows")]
        public TMP_Text subtitleLabel, tipLabel;
        public Image emblemImage;
        public string subtitle, tip;
        public Sprite emblem;
        public void OnPointerEnter(PointerEventData data)
        {
            if(panel==null) return;
            heading.text=patternName;body.text=meaning;
            if(subtitleLabel!=null) subtitleLabel.text=subtitle ?? string.Empty;
            if(tipLabel!=null) tipLabel.text=tip ?? string.Empty;
            if(emblemImage!=null)
            {
                emblemImage.sprite=emblem;emblemImage.gameObject.SetActive(emblem!=null);
                // Keep the heading clear of the emblem only when one is shown.
                var offset=emblem!=null ? 92f : 24f;
                foreach(var label in new[]{heading,subtitleLabel})
                    if(label!=null) label.rectTransform.offsetMin=new Vector2(offset,label.rectTransform.offsetMin.y);
            }
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
