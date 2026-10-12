using System;
using System.Collections.Generic;
using System.IO;
using Spotlight.FreeCafe;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>增量补齐规格 Prefab 并修复公共结构，不重建/覆盖已有美术部件与自定义组件。</summary>
    public static class FreeCafeEntityPrefabBuilder
    {
        private const string LibraryPath="Assets/Resources/FreeCafe/EntityPrefabLibrary.asset";
        public const string PrefabDirectory="Assets/Prefabs/FreeCafe";
        [MenuItem("Tools/Free Cafe/Rebuild Entity Prefabs And Layout")]
        public static void Rebuild()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("先退出 Play Mode 并等待编译完成");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.isDirty) throw new InvalidOperationException("先保存场景；不会覆盖未保存的修改");
            if(scene.path!=FreeCafeProductionSceneBuilder.ScenePath) scene=EditorSceneManager.OpenScene(FreeCafeProductionSceneBuilder.ScenePath);
            var runtime=UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            if(runtime==null) throw new InvalidOperationException("CoffeeShop 缺少 FreeCafeRuntime");
            string backup="output/entity-prefab-validation/"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);File.Copy(scene.path,backup+"/CoffeeShop.unity");
            // 保存旧 Prefab 的副本；现有资产、GUID、动画和用户自定义部件均保留。
            foreach(var path in Directory.GetFiles(PrefabDirectory,"*.prefab")) File.Copy(path,backup+"/"+Path.GetFileName(path));
            var catalog=JsonUtility.FromJson<CafeCatalog>(runtime.catalogAsset.text);new CafeWorld(catalog);
            runtime.prefabLibrary=EnsureCatalog(catalog,runtime.artLibrary,runtime.entityPrefab);
            RebuildAuthoredLayout(runtime,catalog);
            FreeCafeEntityCaptionBuilder.MigrateLegacyLabels(runtime);
            EditorUtility.SetDirty(runtime);EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("[FreeCafe Entities] "+catalog.entities.Length+" 种独立 Prefab，"+catalog.layout.Length+" 个场景 Prefab 实例；备份 "+backup);
        }
        public static FreeCafeEntityPrefabLibrary EnsureCatalog(CafeCatalog catalog,CafeArtLibrary art,FreeEntityView template,ISet<string> updateSpecIds=null)
        {
            if(catalog==null || art==null || template==null) throw new InvalidOperationException("缺少实体目录、美术库或通用结构模板");
            if(!AssetDatabase.IsValidFolder(PrefabDirectory)) throw new InvalidOperationException("实体 Prefab 目录缺失");
            // 提前检查所有规格的路径/美术，避免做到一半才发现命名冲突。
            var paths=new HashSet<string>();
            foreach(var spec in catalog.entities)
            {
                if(!paths.Add(PathFor(spec.id))) throw new InvalidOperationException("Prefab 文件名冲突："+spec.id);
                if((updateSpecIds==null || updateSpecIds.Contains(spec.id)) && art.Resolve(spec.artId,"empty")==null) throw new InvalidOperationException("实体初始美术缺失："+spec.id+" / "+spec.artId);
            }
            var registry=AssetDatabase.LoadAssetAtPath<FreeCafeEntityPrefabLibrary>(LibraryPath);
            if(registry==null) { registry=ScriptableObject.CreateInstance<FreeCafeEntityPrefabLibrary>();AssetDatabase.CreateAsset(registry,LibraryPath); }
            var entries=new List<FreeCafeEntityPrefabEntry>();
            foreach(var spec in catalog.entities)
            {
                var prior=registry.Resolve(spec.id);
                if(updateSpecIds!=null && !updateSpecIds.Contains(spec.id) && prior!=null)
                { entries.Add(new FreeCafeEntityPrefabEntry { specId=spec.id,prefab=prior }); continue; }
                string path=prior!=null ? AssetDatabase.GetAssetPath(prior) : PathFor(spec.id);
                if(prior==null && AssetDatabase.LoadAssetAtPath<GameObject>(path)==null)
                {
                    var go=UnityEngine.Object.Instantiate(template.gameObject);go.name=Path.GetFileNameWithoutExtension(path);
                    try { PrefabUtility.SaveAsPrefabAsset(go,path); }
                    finally { UnityEngine.Object.DestroyImmediate(go); }
                }
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view=root.GetComponent<FreeEntityView>();
                    if(view==null || view.body==null || view.label==null || view.hitbox==null)
                        throw new InvalidOperationException("已有实体 Prefab 缺少组件，不自动覆盖："+path);
                    view.ConfigureAuthoring(spec,art,"",0,0);
                    view.effectsRoot=Anchor(root.transform,"Effects",view.effectsRoot,Vector3.zero);
                    view.liquidRoot=Anchor(root.transform,"Liquid",view.liquidRoot,Vector3.zero);
                    view.pourOrigin=Anchor(root.transform,"PourOrigin",view.pourOrigin,new Vector3(spec.width*.4f,spec.height*.2f,0));
                    view.ConfigureGlassCup();
                    StateNode(root.transform,"FillStatus",true,view);
                    StateNode(root.transform,"FillBarBackground",false,view);
                    StateNode(root.transform,"FillBar",false,view);
                    FreeCafePeoplePrefabBuilder.Configure(view,spec);
                    FreeCafeEntityCaptionBuilder.Configure(view,spec);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                entries.Add(new FreeCafeEntityPrefabEntry { specId=spec.id,prefab=AssetDatabase.LoadAssetAtPath<FreeEntityView>(path) });
            }
            registry.entries=entries.ToArray();EditorUtility.SetDirty(registry);registry.Validate(catalog);return registry;
        }
        public static string PathFor(string id) => PrefabDirectory+"/FreeEntity_"+id.Replace('.','_')+".prefab";
        private static Transform Anchor(Transform root,string name,Transform prior,Vector3 position)
        {
            if(prior!=null) return prior;
            var found=root.Find(name);if(found!=null) return found;
            var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=position;return go.transform;
        }
        private static void StateNode(Transform root,string name,bool text,FreeEntityView view)
        {
            var found=root.Find(name);
            if(found==null)
            {
                var go=new GameObject(name);go.transform.SetParent(root,false);found=go.transform;
                if(text)
                {
                    var label=go.AddComponent<TextMeshPro>();label.font=view.label.font;label.fontSize=.62f;
                    label.color=new Color(.18f,.12f,.08f,.95f);label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
                }
                else go.AddComponent<SpriteRenderer>();
                found=go.transform; // TMP 将 Transform 替换为 RectTransform，必须重新取引用。
            }
            found.gameObject.SetActive(false);
        }
        public static void RebuildAuthoredLayout(FreeCafeRuntime runtime,CafeCatalog catalog)
        {
            Undo.RecordObject(runtime,"Repair authored entity prefabs without moving them");
            runtime.prefabLibrary.Validate(catalog);
            if(runtime.authoringPreviews==null || runtime.authoringPreviews.Length!=runtime.areaRoots.Length)
                runtime.authoringPreviews=new GameObject[runtime.areaRoots.Length];
            var existing=new Dictionary<string,FreeEntityView>();
            foreach(var view in FreeCafeSceneLayout.FindAuthoredEntities(runtime)) existing.Add(view.AuthoringEntityId,view);
            for(int i=0;i<runtime.areaRoots.Length;i++)
            {
                var root=runtime.authoringPreviews[i];
                if(root==null)
                {
                    root=new GameObject("AuthoredEntities");root.transform.SetParent(runtime.areaRoots[i],false);
                    Undo.RegisterCreatedObjectUndo(root,"Create entity layout root");runtime.authoringPreviews[i]=root;
                }
                bool legacy=root.name=="EditorOnly_LayoutPreview";
                root.name="AuthoredEntities";root.tag="Untagged";
                // 只迁移旧构建器的散图预览，不删除用户添加的分组/子物体。
                if(legacy)
                    for(int n=root.transform.childCount-1;n>=0;n--)
                        if(root.transform.GetChild(n).GetComponent<FreeEntityView>()==null)
                            Undo.DestroyObjectImmediate(root.transform.GetChild(n).gameObject);
            }
            foreach(var entry in catalog.layout)
            {
                if(existing.TryGetValue(entry.id,out var prior))
                {
                    if(prior.PrefabSpecId!=entry.specId)
                        throw new InvalidOperationException("已有场景实体规格冲突，不自动覆盖："+entry.id);
                    continue; // 已有实例的位置、父级、缩放、文字和 Inspector override 全部保留。
                }
                var root=runtime.authoringPreviews[(int)entry.area];
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(runtime.prefabLibrary.Require(entry.specId).gameObject,root.transform);
                Undo.RegisterCreatedObjectUndo(instance,"Create missing authored entity instance");var view=instance.GetComponent<FreeEntityView>();
                view.gameObject.name=entry.id;
                view.ConfigureAuthoring(Array.Find(catalog.entities,s=>s.id==entry.specId),runtime.artLibrary,entry.id,entry.x,entry.y);
                // 新增实体的配表坐标是区域坐标，不是可能已偏移的编辑分组坐标。
                view.transform.position=runtime.areaRoots[(int)entry.area].TransformPoint(new Vector3(entry.x,entry.y,0));
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.label);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.label.transform);
            }
            EditorUtility.SetDirty(runtime);
        }
    }
}
