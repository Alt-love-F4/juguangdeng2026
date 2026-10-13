using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.FreeCafe.Editor
{
    public static class FreeCafeWorkstationBuilder
    {
        [MenuItem("Tools/Free Cafe/Apply Illustrated Workstation")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("先退出运行并等待编译");
            var scene = SceneManager.GetActiveScene();
            if (scene.isDirty) throw new InvalidOperationException("请先保存场景，不覆盖未保存修改");
            if (scene.path != FreeCafeProductionSceneBuilder.ScenePath) scene = EditorSceneManager.OpenScene(FreeCafeProductionSceneBuilder.ScenePath);
            Directory.CreateDirectory("output/production-redesign");
            File.Copy(scene.path, "output/production-redesign/CoffeeShop-before-workstation.unity", true);
            var runtime = UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            if (runtime == null) throw new InvalidOperationException("CoffeeShop 缺少运行时");
            var catalog = JsonUtility.FromJson<CafeCatalog>(runtime.catalogAsset.text);
            new CafeWorld(catalog);
            FreeCafeArtBinder.Bind();
            var updateSpecIds = new HashSet<string>();
            foreach (var spec in catalog.entities)
                if (!spec.Has(Capability.Customer) && !spec.Has(Capability.BusinessControl)) updateSpecIds.Add(spec.id);
            runtime.prefabLibrary = FreeCafeEntityPrefabBuilder.EnsureCatalog(catalog, runtime.artLibrary, runtime.entityPrefab, updateSpecIds);
            FreeCafeEntityPrefabBuilder.RebuildAuthoredLayout(runtime, catalog);
            var counter = runtime.areaRoots[(int)AreaId.Counter];
            foreach (var view in FreeCafeSceneLayout.FindAuthoredEntities(runtime))
            {
                var entry = Array.Find(catalog.layout, item => item.id == view.AuthoringEntityId);
                if (entry == null || entry.area != AreaId.Counter) continue;
                var spec = Array.Find(catalog.entities, item => item.id == entry.specId);
                Undo.RecordObject(view.transform, "Arrange illustrated workstation");
                var parentScale = view.transform.parent.lossyScale;
                var counterScale = counter.lossyScale;
                view.transform.localScale = new Vector3(counterScale.x / parentScale.x, counterScale.y / parentScale.y, counterScale.z / parentScale.z);
                view.label.fontSize = 2.2f;
                view.ConfigureAuthoring(spec, runtime.artLibrary, entry.id, entry.x, entry.y);
                view.transform.position = counter.TransformPoint(new Vector3(entry.x, entry.y, 0));
                view.ConfigureGlassCup();
                if (view.workspaceCaption != null) view.workspaceCaption.gameObject.SetActive(false);
                var upgradeHints = view.transform.Find("UpgradeHints"); if (upgradeHints != null) upgradeHints.gameObject.SetActive(false);
                if (entry.id == "service") view.body.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.body);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.body.transform);
                EditorUtility.SetDirty(view);
            }
            const string path = "Assets/Art/FreeCafe/Backgrounds/production-workstation.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var background = counter.Find("Background_WHITEBOX");
            if (background == null) throw new InvalidOperationException("缺少现有背景节点");
            var renderer = background.GetComponent<SpriteRenderer>();
            Undo.RecordObject(renderer, "Apply workstation background");
            Undo.RecordObject(background, "Fit workstation background");
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = -100;
            background.localPosition = Vector3.zero;
            background.localScale = new Vector3(22.044f / sprite.bounds.size.x, 12.4f / sprite.bounds.size.y, 1);
            runtime.areaManager.Find(AreaId.Counter).orthographicSize = 6.2f;
            runtime.productionMode = true;
            var hud = runtime.GetComponent<FreeCafeProductionHud>();
            var header = hud.productionRoot.transform.Find("ProductionHeader");
            SetCard(header.Find("OrderTicket"), new Vector2(.5f, 1), new Vector2(140, -109), new Vector2(560, 168));
            SetCard(header.Find("ExtractionCard"), new Vector2(.5f, 1), new Vector2(140, -262), new Vector2(560, 120));
            if (hud.timeGauge != null) hud.timeGauge.gameObject.SetActive(false);
            if (hud.volumeGauge != null) hud.volumeGauge.gameObject.SetActive(false);
            if (hud.timeCaption != null) hud.timeCaption.gameObject.SetActive(false);
            if (hud.volumeCaption != null) hud.volumeCaption.gameObject.SetActive(false);
            hud.machineStatus.rectTransform.anchoredPosition = Vector2.zero;
            hud.machineStatus.rectTransform.sizeDelta = new Vector2(516, 100);
            hud.machineStatus.fontSize = 18;
            hud.machineStatus.textWrappingMode = TextWrappingModes.Normal;
            SetCard(header.Find("ProductionTools"), new Vector2(1, 1), new Vector2(-258, -61), new Vector2(486, 76));
            EditorUtility.SetDirty(runtime); EditorUtility.SetDirty(runtime.areaManager); EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[Workstation] 同一 CoffeeShop 场景：左上配料、中央制作、右侧出品，已保存");
        }

        [MenuItem("Tools/Free Cafe/Apply Extraction Jug Art And Placement")]
        public static void ApplyExtractionJug()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("先退出运行并等待编译");
            var scene = SceneManager.GetActiveScene();
            if (scene.isDirty) throw new InvalidOperationException("请先保存场景，不覆盖未保存修改");
            if (scene.path != FreeCafeProductionSceneBuilder.ScenePath)
                scene = EditorSceneManager.OpenScene(FreeCafeProductionSceneBuilder.ScenePath);
            var runtime = UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            if (runtime == null) throw new InvalidOperationException("CoffeeShop 缺少运行时");
            FreeEntityView extractor = null;
            foreach (var view in FreeCafeSceneLayout.FindAuthoredEntities(runtime))
                if (view.AuthoringEntityId == "v.extractor") extractor = view;
            if (extractor == null) throw new InvalidOperationException("场景缺少 v.extractor");
            var catalog = JsonUtility.FromJson<CafeCatalog>(runtime.catalogAsset.text);
            var entry = Array.Find(catalog.layout, item => item.id == "v.extractor");
            var spec = Array.Find(catalog.entities, item => item.id == entry.specId);
            Directory.CreateDirectory("output/extractor-art");
            File.Copy(scene.path, "output/extractor-art/CoffeeShop-before-extractor.unity", true);
            FreeCafeArtBinder.Bind();
            runtime.prefabLibrary = FreeCafeEntityPrefabBuilder.EnsureCatalog(catalog, runtime.artLibrary,
                runtime.entityPrefab, new HashSet<string> { spec.id });
            var counter = runtime.areaRoots[(int)AreaId.Counter];
            Undo.RecordObject(extractor.transform, "Place extraction jug on workstation");
            Undo.RecordObject(extractor, "Apply extraction jug art");
            var parentScale = extractor.transform.parent.lossyScale;
            var counterScale = counter.lossyScale;
            extractor.transform.localScale = new Vector3(counterScale.x / parentScale.x,
                counterScale.y / parentScale.y, counterScale.z / parentScale.z);
            extractor.ConfigureAuthoring(spec, runtime.artLibrary, entry.id, entry.x, entry.y);
            extractor.transform.position = counter.TransformPoint(new Vector3(entry.x, entry.y, 0));
            extractor.body.enabled = true;
            extractor.body.color = Color.white;
            extractor.label.fontSize = 2.2f;
            extractor.ConfigureGlassCup();
            foreach (var component in new UnityEngine.Object[] { extractor, extractor.transform, extractor.body,
                extractor.body.transform, extractor.hitbox, extractor.label, extractor.label.transform })
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorUtility.SetDirty(component);
            }
            EditorUtility.SetDirty(runtime);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Workstation] v.extractor 已使用独立玻璃萃取壶素材，放置于研磨器与咖啡壶之间；碰撞范围 "
                + extractor.hitbox.size + "，台面坐标 " + new Vector2(entry.x, entry.y));
        }

        private static void SetCard(Transform target, Vector2 anchor, Vector2 position, Vector2 size)
        {
            if (!(target is RectTransform rect)) return;
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        [MenuItem("Tools/Free Cafe/Preview Illustrated Workstation")]
        public static void Preview()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("先进入 Play Mode");
            UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>().World.EnterProduction();
        }
    }
}
