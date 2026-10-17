#if UNITY_EDITOR
using System;
using System.IO;
using Spotlight.GameFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Editor
{
    public static class FreeCafeFinishingBuilder
    {
        public const string ToastPath="Assets/Resources/Art/UIPrefab/OperationToastPanel.prefab";
        [MenuItem("Tools/Free Cafe/Apply Delivery Zone And Feedback")]
        public static void Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!="Assets/Scenes/CoffeeShop.unity" || scene.isDirty)
                throw new InvalidOperationException("请在已保存的 CoffeeShop 编辑场景执行");
            CreateToast();
            FreeCafeDeliveryDesignBuilder.Apply();
        }
        private static void CreateToast()
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(ToastPath)!=null) return;
            var root=new GameObject("OperationToastPanel",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup),typeof(OperationToastPanel));
            try
            {
                root.GetComponent<Canvas>().overrideSorting=true;root.GetComponent<Canvas>().sortingOrder=100;
                var group=root.GetComponent<CanvasGroup>();group.blocksRaycasts=group.interactable=false;group.alpha=0;
                var go=new GameObject("Banner",typeof(RectTransform),typeof(Image));go.transform.SetParent(root.transform,false);
                var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.6f);rect.sizeDelta=new Vector2(650,54);
                var image=go.GetComponent<Image>();image.color=new Color(.22f,.12f,.075f,.95f);image.raycastTarget=false;
                var textGo=new GameObject("Message",typeof(RectTransform),typeof(TextMeshProUGUI));textGo.transform.SetParent(go.transform,false);
                var text=textGo.GetComponent<TextMeshProUGUI>();text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SimHei SDF.asset");text.text="操作提示";text.fontSize=24;text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=24;text.color=new Color(1,.9f,.72f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.richText=false;
                text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(16,4);text.rectTransform.offsetMax=new Vector2(-16,-4);
                var so=new SerializedObject(root.GetComponent<OperationToastPanel>());so.FindProperty("group").objectReferenceValue=group;so.FindProperty("banner").objectReferenceValue=rect;so.FindProperty("messageText").objectReferenceValue=text;so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,ToastPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
#endif
