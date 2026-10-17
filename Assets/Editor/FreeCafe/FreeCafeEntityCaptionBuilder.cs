using System;
using System.Collections.Generic;
using Spotlight.FreeCafe;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>设备/区域说明属于对应的实体 Prefab，旧版散落的标题只做一次增量迁移。</summary>
    public static class FreeCafeEntityCaptionBuilder
    {
        private static readonly Dictionary<string,string> Captions=new Dictionary<string,string>
        {
            {"source.cups.small","杯具"}, {"source.beans","原料 · 每次一份"}, {"source.honey","小料"},
            {"machine.grinder","研磨 · 点击加速"}, {"machine.brewer","滴滤咖啡 · 注水萃取"},
            {"machine.heater","加热 · 4 秒"}, {"machine.mixer","抹茶加工"}, {"delivery","出餐托盘"},
            {"source.water","清水壶"}, {"source.sugar","方糖"}
        };
        private static readonly Dictionary<string,string> LegacyOwners=new Dictionary<string,string>
        {
            {"Cups","s.cups.small"}, {"Ingredients","s.beans"}, {"Toppings","s.honey"},
            {"Grinder","m.grinder"}, {"Brewer","m.brewer"}, {"Heater","m.heater"}, {"Mixer","m.mixer"},
            {"Tray","service"}, {"WaterSugar","s.water"}, {"ExtraHeater2","m.heater"}, {"ExtraHeater3","m.heater"}
        };
        public static void Configure(FreeEntityView view,EntitySpec spec)
        {
            if(Captions.TryGetValue(spec.id,out var content))
            {
                if(view.workspaceCaption==null)
                    view.workspaceCaption=Caption(view.transform,"WorkspaceCaption",content,view.label.font,
                        new Vector3(0,spec.height*.5f+.55f,-.1f),spec.Has(Capability.Source) ? 2.1f : 1.9f);
                view.workspaceCaption.raycastTarget=false;
                view.workspaceCaption.GetComponent<MeshRenderer>().sortingOrder=view.body.sortingOrder+2;
            }
            if(spec.id=="machine.heater")
            {
                var group=view.transform.Find("UpgradeHints");
                if(group==null) { var go=new GameObject("UpgradeHints");go.transform.SetParent(view.transform,false);group=go.transform; }
                Caption(group,"ExtraHeater2","加热壶 ②\n升级预留",view.label.font,new Vector3(1.8f,.1f,0),1.5f);
                Caption(group,"ExtraHeater3","加热壶 ③\n升级预留",view.label.font,new Vector3(3.1f,.1f,0),1.5f);
            }
        }
        private static TMP_Text Caption(Transform parent,string name,string content,TMP_FontAsset font,Vector3 position,float size)
        {
            var prior=parent.Find(name);if(prior!=null) return prior.GetComponent<TMP_Text>();
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshPro));go.transform.SetParent(parent,false);
            var text=go.GetComponent<TextMeshPro>();text.font=font;text.fontSize=size;text.text=content;
            text.color=new Color(.22f,.13f,.065f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            text.rectTransform.sizeDelta=new Vector2(3.3f,.8f);text.transform.localPosition=position;
            text.GetComponent<MeshRenderer>().sortingOrder=250;return text;
        }
        public static void MigrateLegacyLabels(FreeCafeRuntime runtime)
        {
            var root=runtime.areaRoots[(int)AreaId.Counter];var group=root.Find("ProductionWorkspaceLabels");
            if(group==null) return;
            var views=new Dictionary<string,FreeEntityView>();
            foreach(var view in FreeCafeSceneLayout.FindAuthoredEntities(runtime)) views.Add(view.AuthoringEntityId,view);
            int migrated=0;
            for(int n=group.childCount-1;n>=0;n--)
            {
                var child=group.GetChild(n);
                if(!LegacyOwners.TryGetValue(child.name,out var ownerId) || !views.TryGetValue(ownerId,out var owner))
                { Debug.LogWarning("保留无法判定归属的旧说明："+child.name);continue; }
                var oldText=child.GetComponent<TMP_Text>();
                var target=child.name.StartsWith("ExtraHeater",StringComparison.Ordinal)
                    ? owner.transform.Find("UpgradeHints/"+child.name).GetComponent<TMP_Text>() : owner.workspaceCaption;
                if(oldText==null || target==null) throw new InvalidOperationException("说明迁移缺少文本节点："+child.name);
                Undo.RecordObject(target,"Preserve legacy entity caption text");
                // 保留用户修改过的文字和字体，位置随实体；拆开原来共享的清水/方糖标题。
                target.text=child.name=="WaterSugar" && oldText.text=="清水 / 方糖" ? "清水" : oldText.text;
                target.font=oldText.font;target.fontSize=oldText.fontSize;target.color=oldText.color;
                target.alignment=oldText.alignment;target.raycastTarget=false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                Undo.DestroyObjectImmediate(child.gameObject);migrated++;
            }
            if(group.childCount==0) Undo.DestroyObjectImmediate(group.gameObject);
            Debug.Log("[FreeCafe Labels] 已归属 "+migrated+" 个制作台说明，实体位置未更改");
        }

        [MenuItem("Tools/Free Cafe/Migrate Workspace Labels To Entity Prefabs")]
        public static void MigrateScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("先退出 Play Mode 并等待编译");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.isDirty) throw new InvalidOperationException("请先保存场景，迁移不会覆盖未保存修改");
            if(scene.path!=FreeCafeProductionSceneBuilder.ScenePath) scene=EditorSceneManager.OpenScene(FreeCafeProductionSceneBuilder.ScenePath);
            var runtime=UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            string backup="output/entity-prefab-validation/scene-layout-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
            System.IO.Directory.CreateDirectory(backup);System.IO.File.Copy(scene.path,backup+"/CoffeeShop.unity");
            foreach(var path in System.IO.Directory.GetFiles(FreeCafeEntityPrefabBuilder.PrefabDirectory,"*.prefab"))
                System.IO.File.Copy(path,backup+"/"+System.IO.Path.GetFileName(path));
            var before=FreeCafeSceneLayout.Capture(runtime);
            System.IO.File.WriteAllText(backup+"/layout-before.json",JsonUtility.ToJson(new CafeCatalog{layout=before},true));
            var catalog=JsonUtility.FromJson<CafeCatalog>(runtime.catalogAsset.text);
            runtime.prefabLibrary=FreeCafeEntityPrefabBuilder.EnsureCatalog(catalog,runtime.artLibrary,runtime.entityPrefab);
            MigrateLegacyLabels(runtime); // 不重建布局，不添加或移动任何实体。
            var after=FreeCafeSceneLayout.Capture(runtime);
            if(before.Length!=after.Length) throw new InvalidOperationException("说明迁移意外改变了实体数量");
            foreach(var original in before)
            {
                var updated=Array.Find(after,e=>e.id==original.id);
                if(updated==null || updated.specId!=original.specId || updated.area!=original.area
                    || !Mathf.Approximately(updated.x,original.x) || !Mathf.Approximately(updated.y,original.y))
                    throw new InvalidOperationException("说明迁移意外改变了布局："+original.id);
            }
            System.IO.File.WriteAllText(backup+"/layout-after.json",JsonUtility.ToJson(new CafeCatalog{layout=after},true));
            EditorUtility.SetDirty(runtime);EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("[FreeCafe Layout] 已迁移说明；以后直接编辑 Scene 位置并运行，无需重建。备份 "+backup);
        }
    }
}
