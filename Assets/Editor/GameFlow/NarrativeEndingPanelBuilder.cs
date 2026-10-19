using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Idempotent in-place upgrade of the production ResultPanel, not a new scene/panel.</summary>
    public static class NarrativeEndingPanelBuilder
    {
        public const string PrefabPath = "Assets/Resources/Art/UIPrefab/ResultPanel.prefab";
        [MenuItem("Tools/Narrative/Upgrade Existing Ending Result Panel")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before editing the result prefab.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var panel = root.GetComponent<ResultPanel>();
                if (panel == null) throw new InvalidOperationException("Existing ResultPanel is missing.");
                var serial = new SerializedObject(panel);
                var title = serial.FindProperty("titleText").objectReferenceValue as TMP_Text;
                var message = serial.FindProperty("messageText").objectReferenceValue as TMP_Text;
                var close = serial.FindProperty("closeButton").objectReferenceValue as Button;
                if (title == null || message == null || close == null) throw new InvalidOperationException("Existing result bindings are incomplete.");
                var parent = title.transform.parent;
                var badge = Text(parent, "EndingIdentity", title, 18);
                badge.alignment = TextAlignmentOptions.Center;
                badge.color = new Color(.64f, .65f, .68f);
                badge.text = "ENDING";
                Anchors(badge.rectTransform, new Vector2(.12f,.84f), new Vector2(.88f,.88f));
                var divider = Image(parent, "EndingDivider", new Color(.66f,.60f,.46f,.5f));
                Anchors(divider.rectTransform, new Vector2(.35f,.835f), new Vector2(.65f,.837f));
                var frame = Image(parent, "EndingArtworkFrame", new Color(.07f,.08f,.10f,1f));
                Anchors(frame.rectTransform, new Vector2(.25f,.365f), new Vector2(.75f,.825f));
                var art = Image(parent, "EndingArtwork", Color.white);
                Anchors(art.rectTransform, new Vector2(.255f,.37f), new Vector2(.745f,.82f));
                art.preserveAspect = true;
                var status = Text(parent, "EndingArtworkStatus", title, 19);
                status.color = new Color(.8f,.8f,.81f);
                status.alignment = TextAlignmentOptions.Center;
                Anchors(status.rectTransform, new Vector2(.28f,.49f), new Vector2(.72f,.66f));
                // Instantiate the existing game DialoguePanel as a nested prefab: its real
                // DialogueBox sprite, speaker plate, font, body button and continue arrow stay linked.
                var obsolete=parent.Find("EndingDialogueFrame");
                if(obsolete!=null)UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
                var embedded=parent.Find("EndingDialoguePanel");
                if(embedded==null)
                {
                    var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Art/UIPrefab/DialoguePanel.prefab");
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);
                    instance.name="EndingDialoguePanel";embedded=instance.transform;
                }
                Anchors((RectTransform)embedded,Vector2.zero,Vector2.one);
                serial.FindProperty("endingDialoguePanel").objectReferenceValue=embedded.GetComponent<DialoguePanel>();
                // The existing message component remains the production text binding.
                message.enableAutoSizing = true; message.fontSizeMin = 21; message.fontSizeMax = 29;
                message.enableWordWrapping = true; message.overflowMode = TextOverflowModes.Overflow;
                message.alignment = TextAlignmentOptions.TopLeft;
                title.enableAutoSizing = true; title.fontSizeMin = 30; title.fontSizeMax = 46;
                title.alignment = TextAlignmentOptions.Center;
                serial.FindProperty("endingIdText").objectReferenceValue = badge;
                serial.FindProperty("endingDecoration").objectReferenceValue = divider.gameObject;
                serial.FindProperty("endingArtwork").objectReferenceValue = art;
                serial.FindProperty("endingArtworkFrame").objectReferenceValue = frame.gameObject;
                serial.FindProperty("endingArtworkStatus").objectReferenceValue = status;
                serial.ApplyModifiedPropertiesWithoutUndo();
                frame.gameObject.SetActive(false); art.gameObject.SetActive(false); status.gameObject.SetActive(false);
                embedded.gameObject.SetActive(false); badge.gameObject.SetActive(false); divider.gameObject.SetActive(false);
                // Keep painter's order explicit: background -> frame/art -> dialogue -> text -> button.
                frame.transform.SetSiblingIndex(2); art.transform.SetSiblingIndex(3); status.transform.SetSiblingIndex(4);
                embedded.SetSiblingIndex(5); message.transform.SetAsLastSibling(); title.transform.SetAsLastSibling();
                badge.transform.SetAsLastSibling(); divider.transform.SetAsLastSibling(); close.transform.SetAsLastSibling();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[NarrativeEnding] Upgraded existing ResultPanel: title / CG / bottom narration box.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static Image Image(Transform parent,string name,Color color)
        {
            var child=parent.Find(name);
            var image=child!=null?child.GetComponent<Image>():null;
            if(image==null){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);image=go.GetComponent<Image>();}
            image.sprite=null;image.color=color;image.raycastTarget=false;return image;
        }
        private static TextMeshProUGUI Text(Transform parent,string name,TMP_Text source,float size)
        {
            var child=parent.Find(name);var text=child!=null?child.GetComponent<TextMeshProUGUI>():null;
            if(text==null){var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);text=go.GetComponent<TextMeshProUGUI>();}
            text.font=source.font;text.fontSize=size;text.enableAutoSizing=false;text.raycastTarget=false;text.enableWordWrapping=true;return text;
        }
        private static void Anchors(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;}
    }
}
