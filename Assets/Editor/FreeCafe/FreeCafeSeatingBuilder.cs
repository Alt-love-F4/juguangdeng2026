using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.FreeCafe.Editor
{
    [InitializeOnLoad]
    public static class FreeCafeSeatingBuilder
    {
        private const string ArtRoot = "Assets/Art/FreeCafe/Seating/";
        private const string ScenePath = "Assets/Scenes/CoffeeShop.unity";
        private const string PreviewKey = "FreeCafe.Seating.Acceptance";
        private const string PrefabPath = "Assets/Prefabs/FreeCafe/CafeChair.prefab";

        [Serializable]
        private sealed class SourcePlacement
        {
            public string sprite;
            public int[] rect;
            public int table;
        }

        [Serializable]
        private sealed class SourceLayout
        {
            public SourcePlacement[] placements;
        }

        static FreeCafeSeatingBuilder()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Tools/Free Cafe/Apply Cafe Seating Art")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("先退出 Play Mode");
            var scene = SceneManager.GetActiveScene();
            if (scene.isDirty) throw new InvalidOperationException("请先保存当前场景，再接入座位素材");
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            string backup = "output/seating-backup/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            File.Copy(ScenePath, backup + "/CoffeeShop.unity");
            var runtime = UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            if (runtime == null) throw new InvalidOperationException("CoffeeShop 缺少 FreeCafeRuntime");
            var cafe = runtime.areaRoots[(int)AreaId.Cafe];
            var background = cafe.Find("Background_WHITEBOX")?.GetComponent<SpriteRenderer>();
            if (background == null)
                foreach (var candidate in cafe.GetComponentsInChildren<SpriteRenderer>(true))
                    if (AssetDatabase.GetAssetPath(candidate.sprite) == "Assets/Art/Background/game/SCE_一楼大厅场景底图.png")
                    { background = candidate; break; }
            if (background == null) throw new InvalidOperationException("缺少一楼经营底图 renderer");
            var old = cafe.Find("CafeSeating");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject("CafeSeating");
            root.transform.SetParent(cafe, false);
            Undo.RegisterCreatedObjectUndo(root, "Configure cafe seating");
            var seating = root.AddComponent<FreeCafeSeating>();
            seating.runtime = runtime;
            seating.background = background;
            seating.tables = new[]
            {
                Table("front_left", 955, 1535, 1f, false, 150),
                Table("back_left", 1360, 1405, .86f, true, 100),
                Table("back_right", 2120, 1405, .86f, false, 100),
                Table("front_right", 2560, 1535, 1f, true, 150)
            };
            seating.tables[1].rightSeatAdjustmentPixels = new Vector2(-70, 0);
            seating.tables[2].leftSeatAdjustmentPixels = new Vector2(70, 0);
            Undo.RecordObject(background, "Use chair-free background");
            background.sprite = ImportSprite("Assets/Art/Background/game/SCE_一楼大厅场景底图_无椅.png", new Vector2(.5f,.5f), false);
            seating.suppliedCustomer = ImportSprite(ArtRoot + "customer_seated.png", new Vector2(.42f,.29f), true);
            seating.suppliedGuestName = "佩杖绅士";
            seating.useSuppliedGuestForFirstSeat = true;
            var canonical = ImportSprite(ArtRoot + "chair_left.png", new Vector2(.5f,.36f), true);
            var template = new GameObject("CafeChair", typeof(SpriteRenderer));
            var templateRenderer = template.GetComponent<SpriteRenderer>();
            templateRenderer.sprite = canonical;
            templateRenderer.sharedMaterial = background.sharedMaterial;
            var prefab = PrefabUtility.SaveAsPrefabAsset(template, PrefabPath);
            UnityEngine.Object.DestroyImmediate(template);
            var source = JsonUtility.FromJson<SourceLayout>(File.ReadAllText(ArtRoot + "source-layout.json"));
            seating.idleChairs = new CafeIdleChair[5];
            seating.tableOccluders = new SpriteRenderer[4];
            for (int index = 0; index < source.placements.Length; index++)
            {
                var placement = source.placements[index];
                bool chair = index < seating.idleChairs.Length;
                var pivot = index == 0 ? new Vector2(.5f,.36f) : new Vector2(.5f,.5f);
                var sprite = index == 0 ? canonical : ImportSprite(ArtRoot + placement.sprite + ".png", pivot, true);
                var item = chair ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform)
                    : new GameObject(placement.sprite, typeof(SpriteRenderer));
                if (!chair) item.transform.SetParent(root.transform, false);
                item.name = chair ? "IdleChair_" + index : "TableForeground_" + placement.table;
                var renderer = item.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = background.sharedMaterial;
                var rect = placement.rect;
                var pixel = new Vector2(rect[0] + (rect[2]-rect[0])*pivot.x,
                    rect[1] + (rect[3]-rect[1])*(1-pivot.y));
                item.transform.localPosition = seating.PixelToArea(pixel);
                item.transform.localScale = seating.ArtworkScale(1f);
                renderer.sortingOrder = seating.tables[placement.table].sortingOrder + (chair ? 0 : 3);
                if (chair) seating.idleChairs[index] = new CafeIdleChair { tableIndex = placement.table, renderer = renderer };
                else { seating.tableOccluders[placement.table] = renderer; renderer.enabled = false; }
            }
            seating.seatChairs = new SpriteRenderer[8];
            for (int index = 0; index < seating.seatChairs.Length; index++)
            {
                var table = seating.tables[index % seating.tables.Length];
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                item.name = "Seat_customer_" + index;
                item.transform.localPosition = seating.SeatPosition(index);
                item.transform.localScale = seating.ArtworkScale(table.perspectiveScale);
                var renderer = item.GetComponent<SpriteRenderer>();
                seating.TrySeat("customer_" + index, out _, out _, out bool mirrored);
                renderer.flipX = mirrored;
                renderer.sortingOrder = table.sortingOrder;
                renderer.enabled = false;
                seating.seatChairs[index] = renderer;
            }
            Undo.RecordObject(runtime, "Bind cafe seating");
            runtime.seating = seating;
            EditorUtility.SetDirty(runtime);
            EditorUtility.SetDirty(seating);
            EditorUtility.SetDirty(background);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("[Cafe Seating] 四桌、八个左右座位、五把空椅和正式坐姿素材已接入。原底图未覆盖。备份：" + backup);
        }

        private static CafeTablePlacement Table(string id, float horizontal, float vertical, float scale, bool mirrored, int order)
        {
            return new CafeTablePlacement { id = id, tabletopPixel = new Vector2(horizontal,vertical),
                perspectiveScale = scale, firstSeatOnRight = mirrored, sortingOrder = order };
        }

        private static Sprite ImportSprite(string path, Vector2 pivot, bool transparent)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.spritePivot = pivot;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = transparent;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        [MenuItem("Tools/Free Cafe/Preview Supplied Seated Customer")]
        public static void Preview()
        {
            if (EditorApplication.isPlaying)
            {
                UnityEngine.Object.FindFirstObjectByType<FreeCafeSeating>()?.SpawnAcceptanceCustomer();
                return;
            }
            var scene = SceneManager.GetActiveScene();
            if (scene.isDirty) throw new InvalidOperationException("请先保存场景");
            if (scene.path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(PreviewKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PreviewKey, false)) return;
            SessionState.EraseBool(PreviewKey);
            EditorApplication.delayCall += SpawnPreview;
        }

        private static void SpawnPreview()
        {
            if (!EditorApplication.isPlaying) return;
            var seating = UnityEngine.Object.FindFirstObjectByType<FreeCafeSeating>();
            if (seating == null || seating.runtime == null || seating.runtime.World == null) return;
            seating.SpawnAcceptanceCustomer();
            seating.runtime.areaManager.SnapNow();
        }
    }
}
