#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spotlight.GameFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Explicit offline generation only. Does not modify the current scene or source PSD prefab.</summary>
    public static class MapPanelPrefabBuilder
    {
        public const string SourcePath = "Assets/Art/Map/新地图系统/地图.prefab";
        public const string PanelPath = "Assets/Resources/Art/UIPrefab/MapPanel.prefab";
        public const string ThemePath = "Assets/Resources/MapVisualTheme.asset";
        private const string ArtFolder = "Assets/Art/Map/GeneratedUI";
        private const float Scale = 2400f / 2048f;
        private static readonly string[] Keys = { "temporary", "圣西尔万郊区", "圣徒街", "cafe", "market", "首都法院", "市政厅", "library", "花园区", "大堂区" };
        private static readonly Vector2[] Anchors = {
            new Vector2(292,463), new Vector2(-584,412), new Vector2(794,389),
            new Vector2(-774,-224), new Vector2(-28,230), new Vector2(536,139),
            new Vector2(-322,90), new Vector2(161,-117), new Vector2(453,-310), new Vector2(-958,367) };

        [MenuItem("Tools/GameFlow/Build Map Panel Prefab")]
        public static void BuildDefault() => Build(SourcePath, PanelPath, ThemePath);

        /// <summary>Exports PNG sprites and a theme, then saves an inactive-built RectTransform panel prefab.</summary>
        public static GameObject Build(string sourcePath, string panelPath, string themePath)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Build in Edit Mode only.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new FileNotFoundException("Map PSD prefab not found", sourcePath);
            var layers = source.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.sprite != null).ToArray();
            SpriteRenderer Layer(string name) => layers.FirstOrDefault(r => r.name == name);
            var backdrop = Layer("组 1");
            var icons = Layer("地点图标");
            if (backdrop == null || icons == null) throw new InvalidOperationException("Source needs 组 1 and 地点图标 sprite layers.");
            // Work in source prefab world coordinates; renderer bounds include pivot, parent scale and sprite offsets.
            var sourceBounds = backdrop.bounds;
            float worldToUi = 2400f / sourceBounds.size.x;
            Vector2 MapPosition(SpriteRenderer r) => (Vector2)(r.bounds.center - sourceBounds.center) * worldToUi;
            Vector2 MapSize(SpriteRenderer r) => (Vector2)r.bounds.size * worldToUi;
            Sprite Export(string field, string name)
            {
                var layer = Layer(name);
                return layer == null ? null : ExportSprite(layer.sprite, field);
            }

            EnsureFolder(ArtFolder); EnsureFolder(Path.GetDirectoryName(panelPath).Replace('\\','/'));
            EnsureFolder(Path.GetDirectoryName(themePath).Replace('\\','/'));
            var theme = AssetDatabase.LoadAssetAtPath<MapVisualTheme>(themePath);
            if (theme == null) { theme = ScriptableObject.CreateInstance<MapVisualTheme>(); AssetDatabase.CreateAsset(theme, themePath); }
            theme.pointsBase = Export("pointsBase", "行动点底色");
            var baseLayer = Layer("行动点底色");
            if (baseLayer != null)
            {
                var emptyTexture = ReadSprite(baseLayer.sprite);
                try
                {
                    var empty = emptyTexture.GetPixels();
                    for (int p = 0; p < empty.Length; p++) empty[p] = new Color(.57f, .55f, .50f, empty[p].a);
                    theme.pointsBase = SavePixels("pointsBase", emptyTexture.width, emptyTexture.height, empty);
                }
                finally { UnityEngine.Object.DestroyImmediate(emptyTexture); }
            }
            theme.pointsOutline = Export("pointsOutline", "行动点线稿");
            theme.pointsLabel = Export("pointsLabel", "行动点");
            theme.dateFrame = Export("dateFrame", "时间牌");
            var dateLayer = Layer("时间牌");
            if (dateLayer != null)
            {
                var texture = ReadSprite(dateLayer.sprite);
                try
                {
                    int left = Mathf.RoundToInt(texture.width * .08f), width = Mathf.RoundToInt(texture.width * .84f);
                    theme.dateFrame = SavePixels("dateFrame", width, texture.height, texture.GetPixels(left, 0, width, texture.height));
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            theme.details = Export("details", "游戏详情"); theme.bag = Export("bag", "背包"); theme.tasks = Export("tasks", "任务");
            theme.shortcuts = Export("shortcuts", "图层 1"); theme.panel = Export("panel", "底色");
            theme.inventoryTitle = Export("inventoryTitle", "物品栏标题"); theme.taskTitle = Export("taskTitle", "待办事宜标题");
            theme.actionCard = Export("actionCard", "地区介绍"); theme.requirement = Export("requirement", "条件框");
            var actionLayer = Layer("地区介绍");
            if (actionLayer != null)
            {
                var texture = ReadSprite(actionLayer.sprite);
                try
                {
                    float scaleX = texture.width / 1067f, scaleY = texture.height / 554f;
                    Sprite Crop(string name, int x, int y, int width, int height) => SavePixels(name,
                        Mathf.RoundToInt(width * scaleX), Mathf.RoundToInt(height * scaleY),
                        texture.GetPixels(Mathf.RoundToInt(x * scaleX), Mathf.RoundToInt(y * scaleY),
                            Mathf.RoundToInt(width * scaleX), Mathf.RoundToInt(height * scaleY)));
                    theme.actionEmblem = Crop("actionEmblem", 52, 390, 150, 136);
                    theme.actionDivider = Crop("actionDivider", 50, 379, 830, 16);
                    int width = Mathf.RoundToInt(492 * scaleX), height = Mathf.RoundToInt(212 * scaleY);
                    var arrow = texture.GetPixels(Mathf.RoundToInt(575 * scaleX), 0, width, height);
                    var mask = new bool[arrow.Length];
                    for (int pixel = 0; pixel < arrow.Length; pixel++)
                    {
                        Color color = arrow[pixel];
                        mask[pixel] = color.a > .01f && color.r > .6f && color.r > color.g * 1.3f && color.b < .3f;
                    }
                    for (int pixel = 0; pixel < arrow.Length; pixel++)
                    {
                        bool keep = false;
                        int x = pixel % width, y = pixel / width;
                        for (int offsetY = -3; offsetY <= 3 && !keep; offsetY++)
                            for (int offsetX = -3; offsetX <= 3 && !keep; offsetX++)
                                if (x + offsetX >= 0 && x + offsetX < width && y + offsetY >= 0 && y + offsetY < height)
                                    keep = mask[(y + offsetY) * width + x + offsetX];
                        if (!keep) arrow[pixel] = Color.clear;
                    }
                    theme.execute = SavePixels("execute", width, height, arrow);
                    theme.disabledExecute = theme.execute;
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            theme.taskCard = Export("taskCard", "任务框"); theme.tracking = Export("tracking", "追踪框");
            theme.timer = Export("timer", "时限图标"); theme.zoomBar = Export("zoomBar", "缩放底");
            theme.zoomHandle = Export("zoomHandle", "缩放针"); theme.progress = Export("progress", "进度");
            // Five is the number of illustrated slots in this PSD, not the gameplay maximum.
            var points = Layer("行动点底色");
            if (points != null)
            {
                var texture = ReadSprite(points.sprite);
                try
                {
                    var segments = new Sprite[5];
                    for (int i = 0; i < segments.Length; i++)
                    {
                        int[] boundaries = { 204, 357, 510, 663, 816, 971 };
                        int left = Mathf.RoundToInt(boundaries[i] * texture.width / 971f);
                        int right = Mathf.RoundToInt(boundaries[i + 1] * texture.width / 971f);
                        var colors = texture.GetPixels(left, 0, right-left, texture.height);
                        // Normalize the PSD's prefilled grey/orange demo slots into one reusable fill art.
                        for (int p = 0; p < colors.Length; p++) colors[p] = new Color(.86f, .56f, .23f, colors[p].a);
                        segments[i] = SavePixels("pointSegment" + i, right-left, texture.height, colors);
                    }
                    theme.pointsSegments = segments;
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            theme.headingFont = theme.headingFont != null ? theme.headingFont : Resources.Load<TMPro.TMP_FontAsset>("Fonts/CactusClassicalSerif SDF");
            theme.bodyFont = Resources.Load<TMPro.TMP_FontAsset>("Fonts/SimHei SDF") ?? theme.headingFont;
            theme.locationHighlight = Export("locationHighlight", "地点高亮");
            EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets();

            var root = new GameObject("MapPanel", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(2400, 1350); scaler.matchWidthOrHeight = .5f;
                var controller = root.AddComponent<MapSceneController>();
                var data = new SerializedObject(controller);
                void SetSprite(string field, string layer) => data.FindProperty(field).objectReferenceValue = Export(field, layer);
                SetSprite("mapBackgroundSprite", "组 1"); SetSprite("locationSignsSprite", "标牌");
                SetSprite("locationNamesSprite", "地点名称"); SetSprite("pinSprite", "其他位置图钉");
                SetSprite("cityTitleSprite", "圣诺特城"); SetSprite("compassSprite", "指北针");
                data.FindProperty("feelConfig").objectReferenceValue = Resources.Load<MapFeelConfig>("MapFeelConfig");
                var locations = data.FindProperty("locationArtwork"); locations.arraySize = Keys.Length;
                var cropPositions = new Vector2[Keys.Length];
                var cropSizes = new Vector2[Keys.Length];
                var pixels = ReadSprite(icons.sprite);
                try
                {
                    Vector2 iconCenter = MapPosition(icons), iconSize = MapSize(icons);
                    var all = pixels.GetPixels();
                    // Assign each visible pixel to its closest legacy anchor, then tightly crop each independent building.
                    // This avoids rectangular crops accidentally including a neighbouring building.
                    var assignments = new int[all.Length];
                    for (int p = 0; p < all.Length; p++)
                    {
                        assignments[p] = -1;
                        if (all[p].a < .01f) continue;
                        Vector2 position = iconCenter + new Vector2(((p % pixels.width + .5f) / pixels.width - .5f) * iconSize.x,
                            ((p / pixels.width + .5f) / pixels.height - .5f) * iconSize.y);
                        float nearest = float.PositiveInfinity;
                        for (int i = 0; i < Anchors.Length; i++)
                        {
                            float distance = (position - Anchors[i]).sqrMagnitude;
                            if (distance < nearest) { nearest = distance; assignments[p] = i; }
                        }
                    }
                    // Flood-fill alpha islands and assign whole components, so a building cannot be cut at a Voronoi boundary.
                    var visited = new bool[all.Length];
                    for (int seed = 0; seed < all.Length; seed++)
                    {
                        if (visited[seed] || all[seed].a < .01f) continue;
                        var component = new List<int>(); var queue = new Queue<int>();
                        queue.Enqueue(seed); visited[seed] = true; Vector2 sum = Vector2.zero;
                        while (queue.Count > 0)
                        {
                            int p = queue.Dequeue(); component.Add(p);
                            int x = p % pixels.width, y = p / pixels.width;
                            sum += iconCenter + new Vector2(((x + .5f) / pixels.width - .5f) * iconSize.x, ((y + .5f) / pixels.height - .5f) * iconSize.y);
                            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= pixels.width || ny >= pixels.height) continue;
                                int n = ny * pixels.width + nx;
                                if (visited[n] || all[n].a < .01f) continue;
                                visited[n] = true; queue.Enqueue(n);
                            }
                        }
                        Vector2 centroid = sum / component.Count; int nearest = 0; float distance = float.PositiveInfinity;
                        for (int i = 0; i < Anchors.Length; i++)
                            if ((centroid - Anchors[i]).sqrMagnitude < distance) { distance = (centroid - Anchors[i]).sqrMagnitude; nearest = i; }
                        foreach (int p in component) assignments[p] = nearest;
                    }

                    for (int i = 0; i < Keys.Length; i++)
                    {
                        int minX=pixels.width, minY=pixels.height, maxX=-1, maxY=-1;
                        for (int p=0;p<assignments.Length;p++) if(assignments[p]==i)
                        { int x=p%pixels.width,y=p/pixels.width; minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y); }
                        if(maxX<0) throw new InvalidOperationException("No icon pixels for " + Keys[i]);
                        int width=maxX-minX+1,height=maxY-minY+1;
                        var crop=new Color[width*height];
                        for(int y=0;y<height;y++) for(int x=0;x<width;x++)
                        { int p=(y+minY)*pixels.width+x+minX;if(assignments[p]==i)crop[y*width+x]=all[p]; }
                        var item=locations.GetArrayElementAtIndex(i);
                        item.FindPropertyRelative("positionKey").stringValue=Keys[i];
                        item.FindPropertyRelative("sprite").objectReferenceValue=SavePixels("location"+i,width,height,crop);
                        // Focus remains at the old anchor, but visual button uses the exact crop center below.
                        item.FindPropertyRelative("position").vector2Value=Anchors[i];
                        cropPositions[i] = iconCenter + new Vector2(((minX + width * .5f) / pixels.width - .5f) * iconSize.x, ((minY + height * .5f) / pixels.height - .5f) * iconSize.y);
                        cropSizes[i] = new Vector2((float)width / pixels.width * iconSize.x, (float)height / pixels.height * iconSize.y);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(pixels); }
                data.ApplyModifiedPropertiesWithoutUndo();
                controller.BuildEditorHierarchy();
                var map = root.transform.Find("MapViewport/MapContent");
                var background = map.Find("Background").GetComponent<RectTransform>();
                background.sizeDelta = new Vector2(2400,1350); background.anchoredPosition = Vector2.zero;
                foreach(var pair in new[]{ new[]{"LocationSigns","标牌"},new[]{"LocationNames","地点名称"},new[]{"LocationPins","其他位置图钉"} })
                {
                    var layer=Layer(pair[1]);var target=map.Find("Decorations/"+pair[0]) as RectTransform;
                    if(layer!=null&&target!=null){target.anchoredPosition=MapPosition(layer);target.sizeDelta=MapSize(layer);}
                }
                foreach (var pair in new[] { new[] { "Title", "圣诺特城" }, new[] { "Compass", "指北针" } })
                {
                    var layer = Layer(pair[1]); var target = root.transform.Find("HUD/" + pair[0]) as RectTransform;
                    if (layer != null && target != null)
                    {
                        target.sizeDelta = MapSize(layer);
                        bool title = pair[0] == "Title";
                        target.anchoredPosition = title ? new Vector2(102, 84) : new Vector2(-105, -124);
                        if (title) target.sizeDelta = new Vector2(376, 152);
                    }
                }
                // Rects use the actual source PNG pixel dimensions. All nine locations retain their legacy focus anchors.
                // The artwork pivot is centered so independent images align with their tight crops.
                for(int i=0;i<Keys.Length;i++)
                {
                    var rect=map.Find("Entities/Location:"+Keys[i]) as RectTransform;
                    rect.anchoredPosition=cropPositions[i];
                    rect.sizeDelta=cropSizes[i];
                }
                // Freeze editor-generated structure; activation in play only binds callbacks and runtime data.
                root.SetActive(true);
                var result=PrefabUtility.SaveAsPrefabAsset(root,panelPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Generated "+panelPath+" and "+themePath+". Source prefab and scenes were not modified.");
                return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Sprite ExportSprite(Sprite source,string name)
        {
            var texture=ReadSprite(source);
            try{return SavePixels(name,texture.width,texture.height,texture.GetPixels());}
            finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        private static Texture2D ReadSprite(Sprite sprite)
        {
            // Read back through the GPU; the source TextureImporter does not need Read/Write enabled.
            var previous=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(sprite.texture.width,sprite.texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var result=new Texture2D((int)sprite.rect.width,(int)sprite.rect.height,TextureFormat.RGBA32,false);
            try
            {
                Graphics.Blit(sprite.texture,rt);RenderTexture.active=rt;
                result.ReadPixels(sprite.rect,0,0);result.Apply();return result;
            }
            catch{UnityEngine.Object.DestroyImmediate(result);throw;}
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        }
        private static Sprite SavePixels(string name,int width,int height,Color[] pixels)
        {
            string path=ArtFolder+"/"+name+".png";
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try{texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{UnityEngine.Object.DestroyImmediate(texture);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.spriteBorder = name == "panel" || name == "taskCard" || name == "tracking" || name == "requirement"
                ? new Vector4(24,24,24,24) : Vector4.zero;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=8192;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
    }
}
#endif
