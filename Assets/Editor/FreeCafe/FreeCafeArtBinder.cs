using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spotlight.FreeCafe;
using UnityEditor;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>
    /// 扫描 Assets/Art/FreeCafe/Entities/&lt;artId&gt;/&lt;state&gt;.png，
    /// 把每张图接进 ArtLibrary 对应条目的 variants，使运行时按状态解析到正确素材。
    /// 只写 variants，不动 whitebox 兜底；库里没有的 artId 会自动补条目，不需要重建场景。
    /// </summary>
    public static class FreeCafeArtBinder
    {
        private const string EntitiesRoot="Assets/Art/FreeCafe/Entities";
        private const string LibraryPath="Assets/Resources/FreeCafe/ArtLibrary.asset";

        [MenuItem("Tools/Free Cafe/Bind Entity Art Variants")]
        public static void Bind()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("先退出 Play Mode");
            if(!AssetDatabase.IsValidFolder(EntitiesRoot)) throw new InvalidOperationException("缺少目录："+EntitiesRoot);
            var library=AssetDatabase.LoadAssetAtPath<CafeArtLibrary>(LibraryPath);
            if(library==null) throw new InvalidOperationException("缺少美术库："+LibraryPath);

            var entries=new List<CafeArtEntry>(library.entries ?? Array.Empty<CafeArtEntry>());
            int bound=0,created=0;var log=new List<string>();
            foreach(var folder in AssetDatabase.GetSubFolders(EntitiesRoot))
            {
                string artId=Path.GetFileName(folder);
                var found=Collect(folder);
                if(found.Count==0) continue;
                var entry=entries.Find(e=>e!=null && e.brief!=null && e.brief.id==artId);
                if(entry==null)
                {
                    entry=new CafeArtEntry { brief=BuildBrief(artId,found.Keys.ToArray()),whitebox=found.Values.First(),variants=new ArtVariant[0] };
                    entries.Add(entry);created++;
                    log.Add($"[新增] {artId}：{string.Join(",",found.Keys)}");
                }
                else bound++;
                entry.variants=ToVariants(found);
            }
            library.entries=entries.ToArray();
            EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            foreach(var line in log) Debug.Log("[FreeCafeArtBinder] "+line);
            Debug.Log($"[FreeCafeArtBinder] 完成：接线 {bound} 条已有条目，新增 {created} 条条目，共 {entries.Count} 条。");
        }

        /// <summary>
        /// 把已接线的美术写回实体 Prefab 的初始外观（body.sprite），让场景实例在编辑态也能看到新素材。
        /// 只处理「已拿到真实美术」的 artId，不动场景层级、位置与其它组件，也不重建场景。
        /// </summary>
        [MenuItem("Tools/Free Cafe/Apply Entity Art To Prefabs")]
        public static void ApplyToPrefabs()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("先退出 Play Mode");
            var catalogAsset=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/FreeCafe/catalog.json");
            if(catalogAsset==null) throw new InvalidOperationException("缺少 catalog.json");
            var library=AssetDatabase.LoadAssetAtPath<CafeArtLibrary>(LibraryPath);
            if(library==null) throw new InvalidOperationException("缺少美术库："+LibraryPath);

            var catalog=JsonUtility.FromJson<CafeCatalog>(catalogAsset.text);
            int updated=0,skipped=0;var log=new List<string>();
            foreach(var spec in catalog.entities)
            {
                if(!HasRealArt(library,spec.artId)) { skipped++; continue; }
                string path=FreeCafeEntityPrefabBuilder.PathFor(spec.id);
                if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null) { skipped++; continue; }
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view=root.GetComponent<FreeEntityView>();
                    if(view==null || view.body==null) { skipped++; continue; }
                    var sprite=library.Resolve(spec.artId,"empty");
                    if(sprite==null) { skipped++; continue; }
                    view.body.sprite=sprite;
                    var size=sprite.bounds.size;
                    float fit=Mathf.Min(spec.width/size.x,spec.height/size.y);
                    view.body.transform.localScale=new Vector3(fit,fit,1);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                    updated++;
                    log.Add($"{spec.id} ← {spec.artId}/{sprite.name}");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            foreach(var line in log) Debug.Log("[FreeCafeArtBinder] "+line);
            Debug.Log($"[FreeCafeArtBinder] 实体 Prefab 初始美术更新 {updated} 个，跳过 {skipped} 个。");
        }

        /// <summary>该 artId 是否已接上真实美术（存在非空 variant）。</summary>
        private static bool HasRealArt(CafeArtLibrary library,string artId)
        {
            var entry=library.Find(artId);
            if(entry==null || entry.variants==null) return false;
            foreach(var variant in entry.variants) if(variant!=null && variant.sprite!=null) return true;
            return false;
        }

        /// <summary>读取一个 artId 目录下的所有 &lt;state&gt;.png，并统一 Sprite 导入设置。</summary>
        private static Dictionary<string,Sprite> Collect(string folder)
        {
            var map=new Dictionary<string,Sprite>();
            foreach(var file in Directory.GetFiles(folder,"*.png").OrderBy(f=>f))
            {
                string path=file.Replace('\\','/');
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer!=null && (importer.textureType!=TextureImporterType.Sprite
                    || importer.spriteImportMode!=SpriteImportMode.Single
                    || Mathf.Abs(importer.spritePixelsPerUnit-100f)>.01f
                    || !importer.alphaIsTransparency))
                {
                    importer.textureType=TextureImporterType.Sprite;
                    importer.spriteImportMode=SpriteImportMode.Single;
                    importer.spritePixelsPerUnit=100f;
                    importer.alphaIsTransparency=true;
                    importer.SaveAndReimport();
                }
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if(sprite!=null) map[Path.GetFileNameWithoutExtension(file)]=sprite;
            }
            return map;
        }

        /// <summary>把目录里的状态图整理成 variants；以目录内容为准，替换掉旧的状态集合。</summary>
        private static ArtVariant[] ToVariants(Dictionary<string,Sprite> found)
        {
            return found.OrderBy(pair=>pair.Key)
                .Select(pair=>new ArtVariant { state=pair.Key,sprite=pair.Value })
                .ToArray();
        }

        private static ArtBrief BuildBrief(string artId,string[] states) => new ArtBrief
        {
            id=artId,label=artId,category="entity",pivot="center (0.5,0.5)",
            outputFolder=EntitiesRoot+"/"+artId,
            prompt="手绘二维经营游戏，温暖复古咖啡店气质，干净深棕色线稿，低饱和暖色，轻微纸张质感，简洁易读的轮廓，固定正面略俯视视角。",
            negativePrompt="无文字、无数字、无水印、无UI、无背景场景、无其他物体。",
            width=512,height=512,transparent=true,worldWidth=1f,worldHeight=1f,
            states=states
        };
    }
}