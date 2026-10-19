using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Upgrade the existing ResultPanel prefab in place; no new panel system or scene.</summary>
    public static class NarrativeEndingPanelBuilder
    {
        public const string PrefabPath = "Assets/Resources/Art/UIPrefab/ResultPanel.prefab";
        [MenuItem("Tools/Narrative/Upgrade Existing Ending Result Panel")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before editing the result prefab.");
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var panel=root.GetComponent<ResultPanel>();if(panel==null)throw new InvalidOperationException("Existing ResultPanel is missing.");
                var serial=new SerializedObject(panel);
                var title=serial.FindProperty("titleText").objectReferenceValue as TMP_Text;
                var message=serial.FindProperty("messageText").objectReferenceValue as TMP_Text;
                var close=serial.FindProperty("closeButton").objectReferenceValue as Button;
                if(title==null||message==null||close==null)throw new InvalidOperationException("Existing result bindings are incomplete.");
                var parent=title.transform.parent;
                var badge=parent.Find("EndingIdentity")?.GetComponent<TextMeshProUGUI>();
                if(badge==null){var go=new GameObject("EndingIdentity",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);badge=go.GetComponent<TextMeshProUGUI>();}
                badge.font=title.font;badge.fontSize=22;badge.color=new Color(.65f,.48f,.27f);badge.alignment=TextAlignmentOptions.Center;badge.raycastTarget=false;badge.enableWordWrapping=true;
                Anchors(badge.rectTransform,new Vector2(.13f,.79f),new Vector2(.87f,.85f));badge.text="ENDING";badge.gameObject.SetActive(false);
                var decor=parent.Find("EndingDivider")?.GetComponent<Image>();
                if(decor==null){var go=new GameObject("EndingDivider",typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);decor=go.GetComponent<Image>();}
                decor.color=new Color(.65f,.48f,.27f,.45f);decor.raycastTarget=false;Anchors(decor.rectTransform,new Vector2(.35f,.745f),new Vector2(.65f,.748f));decor.gameObject.SetActive(false);
                title.enableAutoSizing=true;title.fontSizeMin=28;title.fontSizeMax=48;title.alignment=TextAlignmentOptions.Center;Anchors(title.rectTransform,new Vector2(.12f,.61f),new Vector2(.88f,.73f));
                message.enableAutoSizing=true;message.fontSizeMin=19;message.fontSizeMax=28;message.enableWordWrapping=true;message.alignment=TextAlignmentOptions.Center;Anchors(message.rectTransform,new Vector2(.16f,.38f),new Vector2(.84f,.59f));
                Anchors((RectTransform)close.transform,new Vector2(.35f,.23f),new Vector2(.65f,.32f));
                serial.FindProperty("endingIdText").objectReferenceValue=badge;serial.FindProperty("endingDecoration").objectReferenceValue=decor.gameObject;serial.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static void Anchors(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;}
    }
}
