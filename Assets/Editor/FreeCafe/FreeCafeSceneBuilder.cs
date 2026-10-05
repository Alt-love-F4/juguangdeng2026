using System;
using System.Collections.Generic;
using System.IO;
using Spotlight.FreeCafe;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Spotlight.FreeCafe.Editor
{
    public static class FreeCafeSceneBuilder
    {
        private const string Root="Assets/Resources/FreeCafe";
        private const string Main="Assets/Scenes/CoffeeShop.unity";
        private const string Sandbox="Assets/Scenes/CoffeeUIEntitySandbox.unity";
        private static TMP_FontAsset Font=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/SimHei SDF.asset");
        private static readonly string[] SketchBackgrounds={
            "Assets/Art/Background/一楼背景.png",
            "Assets/Art/Background/出餐台背景.png",
            "Assets/Art/Background/二楼背景.png",
            "Assets/Art/Background/地下室背景.png"
        };
        [MenuItem("Tools/Free Cafe/Build Whitebox Scene")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first");
            var catalogAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/catalog.json");
            var briefAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/art-briefs.json");
            if(catalogAsset==null || briefAsset==null || Font==null) throw new InvalidOperationException("Catalog, art briefs or font missing");
            var catalog=JsonUtility.FromJson<CafeCatalog>(catalogAsset.text);
            var briefs=JsonUtility.FromJson<ArtBriefFile>(briefAsset.text);
            // Validate rules before changing any scene; originals remain outside Assets in recovery backups.
            new CafeWorld(catalog);
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string backup=Path.Combine("RecoveryBackups-FreeCafe-20261005","Build-"+DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(backup);
            if(File.Exists(Main)) File.Copy(Main,Path.Combine(backup,"CoffeeShop.unity"));
            if(File.Exists(Sandbox)) File.Copy(Sandbox,Path.Combine(backup,"CoffeeUIEntitySandbox.unity"));
            var library=BuildLibrary(briefs);
            var prefab=BuildPrefab(library);
            var prefabLibrary=BuildPrefabLibrary(catalog,library,prefab);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var app=new GameObject("FreeCafeApp");var runtime=app.AddComponent<FreeCafeRuntime>();
            runtime.catalogAsset=catalogAsset;runtime.artLibrary=library;runtime.entityPrefab=prefab;runtime.prefabLibrary=prefabLibrary;
            var cameraGo=new GameObject("Main Camera",typeof(Camera),typeof(Physics2DRaycaster),typeof(FreeCafeViewport));
            cameraGo.tag="MainCamera";cameraGo.transform.position=new Vector3(0,0,-10);
            var camera=cameraGo.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.2f);
            runtime.worldCamera=camera;
            new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var building=new GameObject("BuildingViews"); runtime.areaRoots=new Transform[4];
            foreach(var area in catalog.areas)
            {
                var room=new GameObject(area.id.ToString());room.transform.SetParent(building.transform,false);runtime.areaRoots[(int)area.id]=room.transform;
                var back=new GameObject("Background_WHITEBOX",typeof(SpriteRenderer),typeof(ArtRequirementTag));back.transform.SetParent(room.transform,false);
                var sr=back.GetComponent<SpriteRenderer>();sr.sprite=LoadSketchSprite(SketchBackgrounds[(int)area.id]) ?? library.Resolve(area.artId);sr.sortingOrder=-100;
                var size=sr.sprite.bounds.size;back.transform.localScale=new Vector3(area.halfWidth*2/size.x,area.halfHeight*2/size.y,1);
                back.GetComponent<ArtRequirementTag>().Bind(library,area.artId);
                var note=new GameObject("AreaArtNote",typeof(TextMeshPro),typeof(ArtRequirementTag));note.transform.SetParent(room.transform,false);note.transform.localPosition=new Vector3(0,4.8f,0);
                var label=note.GetComponent<TextMeshPro>();label.font=Font;label.fontSize=2.4f;label.alignment=TextAlignmentOptions.Center;label.text="白模 / "+area.label+" / "+area.artId;label.color=new Color(.32f,.36f,.39f);label.GetComponent<MeshRenderer>().sortingOrder=0;
                note.GetComponent<ArtRequirementTag>().Bind(library,area.artId);
                room.SetActive(area.id==AreaId.Cafe);
            }
            Portal(runtime,library,runtime.areaRoots[0],AreaId.Counter,7.8f,0,"进入出餐台 →");
            Portal(runtime,library,runtime.areaRoots[1],AreaId.Cafe,8f,3.5f,"← 营业区");
            FuturePlaceholder(library,runtime.areaRoots[2],"future.topic","未来：对话 / 热点词条",-4,1);
            FuturePlaceholder(library,runtime.areaRoots[3],"future.press","未来：夜间印刷（未实现）",-4,1);
            FuturePlaceholder(library,runtime.areaRoots[3],"future.newspaper","未来：报纸自由排版（未实现）",3,-1);
            runtime.authoringPreviews=new GameObject[4];
            for(int i=0;i<4;i++)
            {
                var previews=new GameObject("EditorOnly_LayoutPreview");previews.transform.SetParent(runtime.areaRoots[i],false);runtime.authoringPreviews[i]=previews;
            }
            foreach(var initial in catalog.layout)
            {
                EntitySpec spec=null;foreach(var item in catalog.entities) if(item.id==initial.specId) { spec=item;break; }
                var preview=new GameObject("WHITEBOX_"+initial.id,typeof(SpriteRenderer),typeof(ArtRequirementTag));preview.transform.SetParent(runtime.authoringPreviews[(int)initial.area].transform,false);preview.transform.localPosition=new Vector3(initial.x,initial.y,0);
                var sr=preview.GetComponent<SpriteRenderer>();sr.sprite=library.Resolve(spec.artId);sr.sortingOrder=100-Mathf.RoundToInt(initial.y*10);
                float fit=Mathf.Min(spec.width/sr.sprite.bounds.size.x,spec.height/sr.sprite.bounds.size.y);preview.transform.localScale=new Vector3(fit,fit,1);preview.GetComponent<ArtRequirementTag>().Bind(library,spec.artId);
                var labelGo=new GameObject("Label",typeof(TextMeshPro));labelGo.transform.SetParent(runtime.authoringPreviews[(int)initial.area].transform,false);labelGo.transform.localPosition=new Vector3(initial.x,initial.y-spec.height*.5f-.18f,0);
                var label=labelGo.GetComponent<TextMeshPro>();label.font=Font;label.fontSize=2;label.text=spec.label;label.alignment=TextAlignmentOptions.Center;label.color=new Color(.2f,.24f,.28f);label.GetComponent<MeshRenderer>().sortingOrder=200;
            }
            BuildHud(runtime,library);
            cameraGo.GetComponent<FreeCafeViewport>().Apply();
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene,Main)) throw new IOException("Cannot save "+Main);
            // Overwrite scene contents, not GUIDs. Production build settings and references stay intact.
            if(!EditorSceneManager.SaveScene(scene,Sandbox,true)) throw new IOException("Cannot save "+Sandbox);
            AssetDatabase.SaveAssets();
            Debug.Log("[FreeCafeBuild] Independent freeform whitebox scene saved; 38 art contracts, no legacy rules or workbench slots. Backup: "+backup);
        }
        private static readonly Dictionary<string,string> GameplayArt = new Dictionary<string,string>
        {
            { "cup.small", "cup_small.png" },
            { "cup.large", "cup_latte.png" },
            { "beans", "ing_coffee_beans.png" },
            { "grounds", "ing_coffee_beans.png" },
            { "coffee", "cup_latte.png" },
            { "milk", "ing_milk.png" },
            { "hotmilk", "ing_milk.png" },
            { "matcha.powder", "ing_matcha.png" },
            { "matcha.drink", "ing_matcha.png" },
            { "cocoa.powder", "ing_chocolate.png" },
            { "cocoa.drink", "ing_chocolate.png" },
            { "honey", "ing_honey.png" },
            { "source.cups.small", "cup_small.png" },
            { "source.cups.large", "cup_latte.png" },
            { "source.beans", "ing_coffee_beans.png" },
            { "source.milk", "ing_milk.png" },
            { "source.matcha", "ing_matcha.png" },
            { "source.cocoa", "ing_chocolate.png" },
            { "source.honey", "ing_honey.png" },
            { "machine.grinder", "machine_grinder.png" },
            { "machine.brewer", "machine_espresso.png" },
            { "machine.heater", "machine_milk_pot.png" },
            { "machine.mixer", "machine_matcha_grinder.png" },
            { "customer", "customer_1.png" },
            { "delivery", "serve_window.png" }
        };
        private static Sprite LoadGameplaySprite(string id)
        {
            if(!GameplayArt.TryGetValue(id,out var fileName)) return null;
            var path="Assets/Art/Gameplay/"+fileName;
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null) return null;
            if(importer.textureType!=TextureImporterType.Sprite || importer.spriteImportMode!=SpriteImportMode.Single)
            {
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;importer.alphaIsTransparency=true;importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static CafeArtLibrary BuildLibrary(ArtBriefFile file)
        {
            string path=Root+"/ArtLibrary.asset";
            var library=AssetDatabase.LoadAssetAtPath<CafeArtLibrary>(path);
            if(library==null) { library=ScriptableObject.CreateInstance<CafeArtLibrary>();AssetDatabase.CreateAsset(library,path); }
            var entries=new List<CafeArtEntry>();
            foreach(var brief in file.briefs)
            {
                string png="Assets/Art/FreeCafe/Whitebox/"+brief.id+".png";
                var importer=AssetImporter.GetAtPath(png) as TextureImporter;
                if(importer==null) throw new InvalidOperationException("Whitebox missing: "+png);
                if(importer.textureType!=TextureImporterType.Sprite)
                {
                    importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;importer.alphaIsTransparency=true;importer.SaveAndReimport();
                }
                var existing=library.Find(brief.id);
                var variants=existing!=null ? existing.variants : null;
                if(variants==null) { variants=new ArtVariant[brief.states.Length];for(int i=0;i<variants.Length;i++) variants[i]=new ArtVariant { state=brief.states[i] }; }
                var gameplay=LoadGameplaySprite(brief.id);
                entries.Add(new CafeArtEntry { brief=brief,whitebox=gameplay!=null ? gameplay : AssetDatabase.LoadAssetAtPath<Sprite>(png),variants=variants });
            }
            library.entries=entries.ToArray();EditorUtility.SetDirty(library);return library;
        }
        private static Sprite LoadSketchSprite(string path)
        {
            if(string.IsNullOrEmpty(path)) return null;
            foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path)) if(asset is Sprite sprite) return sprite;
            return null;
        }
        private static FreeEntityView BuildPrefab(CafeArtLibrary library) => BuildEntityPrefabAsset("FreeEntity",null,library,null);
        private static FreeCafeEntityPrefabLibrary BuildPrefabLibrary(CafeCatalog catalog,CafeArtLibrary library,FreeEntityView fallback)
        {
            const string path="Assets/Resources/FreeCafe/EntityPrefabLibrary.asset";
            var registry=AssetDatabase.LoadAssetAtPath<FreeCafeEntityPrefabLibrary>(path);
            if(registry==null){registry=ScriptableObject.CreateInstance<FreeCafeEntityPrefabLibrary>();AssetDatabase.CreateAsset(registry,path);}
            var entries=new List<FreeCafeEntityPrefabEntry>();
            foreach(var spec in catalog.entities)
            {
                var prefab=BuildEntityPrefabAsset("FreeEntity_"+SafeName(spec.id),spec,library,fallback);
                entries.Add(new FreeCafeEntityPrefabEntry{specId=spec.id,prefab=prefab});
            }
            registry.entries=entries.ToArray();EditorUtility.SetDirty(registry);return registry;
        }
        private static string SafeName(string value)
        {
            foreach(var c in System.IO.Path.GetInvalidFileNameChars()) value=value.Replace(c,'_');
            return value.Replace('.','_');
        }
        private static FreeEntityView BuildEntityPrefabAsset(string name,EntitySpec spec,CafeArtLibrary library,FreeEntityView fallback)
        {
            Directory.CreateDirectory("Assets/Prefabs/FreeCafe");
            var go=new GameObject(name,typeof(FreeEntityView),typeof(BoxCollider2D),typeof(ArtRequirementTag));
            var body=new GameObject("Art",typeof(SpriteRenderer));body.transform.SetParent(go.transform,false);
            var label=new GameObject("Label",typeof(TextMeshPro));label.transform.SetParent(go.transform,false);
            var text=label.GetComponent<TextMeshPro>();text.font=Font;text.fontSize=2;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.2f,.24f,.28f);text.rectTransform.sizeDelta=new Vector2(3.3f,.7f);
            var view=go.GetComponent<FreeEntityView>();view.body=body.GetComponent<SpriteRenderer>();view.label=text;view.hitbox=go.GetComponent<BoxCollider2D>();view.hitbox.isTrigger=true;view.art=go.GetComponent<ArtRequirementTag>();view.art.library=library;
            if(spec!=null)
            {
                view.art.Bind(library,spec.artId);
                view.body.sprite=library.Resolve(spec.artId);
                view.name=name;
            }
            var path="Assets/Prefabs/FreeCafe/"+name+".prefab";
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,path);Object.DestroyImmediate(go);return prefab.GetComponent<FreeEntityView>();
        }
        private static void Portal(FreeCafeRuntime runtime,CafeArtLibrary library,Transform parent,AreaId destination,float x,float y,string text)
        {
            var go=new GameObject("Portal_"+destination,typeof(BoxCollider2D),typeof(SpriteRenderer),typeof(FreeCafePortal),typeof(ArtRequirementTag));go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(x,y,0);
            go.GetComponent<BoxCollider2D>().size=new Vector2(1.6f,1.4f);
            var sr=go.GetComponent<SpriteRenderer>();sr.sprite=library.Resolve("ui.portal");sr.sortingOrder=10;
            var size=sr.sprite.bounds.size;go.transform.localScale=new Vector3(1.1f/size.x,1.1f/size.y,1);
            go.GetComponent<BoxCollider2D>().size=new Vector2(size.x,size.y);
            var portal=go.GetComponent<FreeCafePortal>();portal.runtime=runtime;portal.destination=destination;portal.visual=sr;go.GetComponent<ArtRequirementTag>().Bind(library,"ui.portal");
            var label=new GameObject("Label",typeof(TextMeshPro));label.transform.SetParent(parent,false);label.transform.localPosition=new Vector3(x,y-1,0);
            var tmp=label.GetComponent<TextMeshPro>();tmp.font=Font;tmp.fontSize=2.2f;tmp.alignment=TextAlignmentOptions.Center;tmp.text=text;tmp.color=new Color(.25f,.28f,.3f);tmp.GetComponent<MeshRenderer>().sortingOrder=11;
        }
        private static void FuturePlaceholder(CafeArtLibrary library,Transform parent,string id,string caption,float x,float y)
        {
            var go=new GameObject("WHITEBOX_"+id,typeof(SpriteRenderer),typeof(ArtRequirementTag));go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(x,y,0);
            var sr=go.GetComponent<SpriteRenderer>();sr.sprite=library.Resolve(id);sr.color=new Color(.85f,.87f,.89f,.65f);sr.sortingOrder=1;go.GetComponent<ArtRequirementTag>().Bind(library,id);
            var label=new GameObject("ScopeNote",typeof(TextMeshPro));label.transform.SetParent(go.transform,false);label.transform.localPosition=new Vector3(0,-1.5f,0);var tmp=label.GetComponent<TextMeshPro>();tmp.font=Font;tmp.fontSize=2.1f;tmp.alignment=TextAlignmentOptions.Center;tmp.text=caption;tmp.color=new Color(.32f,.36f,.4f);tmp.GetComponent<MeshRenderer>().sortingOrder=2;
        }
        private static void BuildHud(FreeCafeRuntime runtime,CafeArtLibrary library)
        {
            var go=new GameObject("FreeCafeHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(FreeCafeHud));
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;canvas.planeDistance=1f;canvas.overrideSorting=true;canvas.sortingOrder=600;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=0;
            var root=(RectTransform)go.transform;root.localPosition=Vector3.zero;root.localScale=Vector3.one;root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;root.sizeDelta=Vector2.zero;
            var top=Box(root,"TopBlackBar",Vector2.zero,new Vector2(1920,180),Color.black);top.anchorMin=new Vector2(0,1);top.anchorMax=Vector2.one;top.pivot=new Vector2(.5f,1);top.offsetMin=new Vector2(0,-180);top.offsetMax=Vector2.zero;top.gameObject.AddComponent<ArtRequirementTag>().Bind(library,"ui.panel");
            var bottom=Box(root,"BottomBlackBar",Vector2.zero,new Vector2(1920,180),Color.black);bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,0);bottom.pivot=new Vector2(.5f,0);bottom.offsetMin=Vector2.zero;bottom.offsetMax=new Vector2(0,180);bottom.gameObject.AddComponent<ArtRequirementTag>().Bind(library,"ui.panel");
            var hud=go.GetComponent<FreeCafeHud>();hud.runtime=runtime;
            hud.summary=Text(top,"Summary","自由制作白模",new Vector2(0,58),new Vector2(1830,42),24);
            hud.orderButtons=new Button[3];hud.orderLabels=new TMP_Text[3];
            for(int i=0;i<3;i++)
            {
                var button=Button(top,"Order"+i,"等待顾客",new Vector2(-570+i*560,-10),new Vector2(520,92),20,library);
                hud.orderButtons[i]=button;hud.orderLabels[i]=button.GetComponentInChildren<TMP_Text>();
            }
            Text(top,"RuleNote","订单只评价交付结果，不限制备料顺序 · 错误配方也可交付并影响收入与声望",new Vector2(0,-70),new Vector2(1750,28),17);
            var controls=Box(bottom,"Controls",new Vector2(-215,0),new Vector2(1450,180),Color.clear);controls.GetComponent<Image>().raycastTarget=false;
            hud.hint=Text(controls,"InteractionHint","取出物品，自由摆放",new Vector2(0,64),new Vector2(1390,32),19);
            hud.selection=Text(controls,"Selection","无需先接单",new Vector2(0,27),new Vector2(1390,32),18);
            hud.startButton=Button(controls,"StartBusiness","开始营业",new Vector2(-625,-20),new Vector2(155,40),18,library);
            hud.refillButton=Button(controls,"Refill","补货",new Vector2(-455,-20),new Vector2(155,40),18,library);
            hud.rotateButton=Button(controls,"Rotate","旋转选中物",new Vector2(-275,-20),new Vector2(175,40),18,library);
            hud.carryButton=Button(controls,"Carry","跨区域携带：关",new Vector2(-65,-20),new Vector2(220,40),18,library);
            hud.pauseButton=Button(controls,"Pause","暂停 / Esc",new Vector2(160,-20),new Vector2(155,40),18,library);
            hud.saveButton=Button(controls,"Save","存到空槽位",new Vector2(335,-20),new Vector2(175,40),18,library);
            hud.artButton=Button(controls,"ArtIds","美术编号",new Vector2(530,-20),new Vector2(175,40),18,library);
            hud.recipeButton=Button(controls,"RecipeTips","制作提示",new Vector2(720,-20),new Vector2(175,40),18,library);
            Text(controls,"Help","拖拽组合 / 加工 · 点击「制作提示」查看配表流程 · Shift 仅摆放 · Esc 取消拖拽",new Vector2(0,-67),new Vector2(1390,30),17);
            var recipePanel=Box(root,"RecipePanel",Vector2.zero,new Vector2(760,650),new Color(.04f,.045f,.05f,.97f));
            recipePanel.anchorMin=recipePanel.anchorMax=new Vector2(.5f,.5f); recipePanel.pivot=new Vector2(.5f,.5f); recipePanel.anchoredPosition=Vector2.zero;
            recipePanel.gameObject.SetActive(false); hud.recipePanel=recipePanel.gameObject;
            hud.recipeTitle=Text(recipePanel,"RecipeTitle","餐品制作提示（配表）",new Vector2(0,270),new Vector2(700,42),24);
            hud.recipeBody=Text(recipePanel,"RecipeBody","",new Vector2(0,-5),new Vector2(700,485),14); hud.recipeBody.alignment=TextAlignmentOptions.TopLeft; hud.recipeBody.enableWordWrapping=true; hud.recipeBody.raycastTarget=false;
            hud.recipeCloseButton=Button(recipePanel,"CloseRecipeTips","关闭",new Vector2(0,-285),new Vector2(160,42),18,library);
            var nav=Box(bottom,"Navigation",new Vector2(735,0),new Vector2(390,180),Color.black);
            hud.areaName=Text(nav,"CurrentArea","一楼",new Vector2(0,61),new Vector2(375,28),20);
            hud.areaButtons=new Button[4];string[] labels={"一楼营业区","一楼出餐台","顶楼","地下室"};
            for(int i=0;i<4;i++) hud.areaButtons[i]=Button(nav,"Area"+i,labels[i],new Vector2(i%2==0 ? -94 : 94,i<2 ? 12 : -45),new Vector2(178,46),18,library);
        }
        private static RectTransform Box(Transform parent,string name,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;go.GetComponent<Image>().color=color;return rect;
        }
        private static TMP_Text Text(Transform parent,string name,string text,Vector2 position,Vector2 size,int fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;var tmp=go.GetComponent<TextMeshProUGUI>();tmp.font=Font;tmp.fontSize=fontSize;tmp.text=text;tmp.alignment=TextAlignmentOptions.Center;tmp.color=new Color(.9f,.91f,.92f);tmp.raycastTarget=false;return tmp;
        }
        private static Button Button(Transform parent,string name,string text,Vector2 position,Vector2 size,int fontSize,CafeArtLibrary library)
        {
            var rect=Box(parent,name,position,size,new Color(.26f,.29f,.32f));var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();Text(rect,"Label",text,Vector2.zero,size-new Vector2(10,4),fontSize);rect.gameObject.AddComponent<ArtRequirementTag>().Bind(library,"ui.button");return button;
        }
        [MenuItem("Tools/Free Cafe/Fix HUD Input Routing")]
        public static void FixHudInputRouting()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first");
            foreach (var path in new[] { Main, Sandbox })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var hud = GameObject.Find("FreeCafeHUD");
                var canvas = hud != null ? hud.GetComponent<Canvas>() : null;
                if (canvas == null) continue;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[FreeCafeInput] HUD switched to ScreenSpaceOverlay; world camera remains cropped independently.");
        }
        [MenuItem("Tools/Free Cafe/Capture Preview")]
        public static void Capture()
        {
            if(!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required");
            var runtime=Object.FindFirstObjectByType<FreeCafeRuntime>();if(runtime==null) throw new InvalidOperationException("No FreeCafe world");
            foreach(var graphic in Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None)) graphic.SetAllDirty();Canvas.ForceUpdateCanvases();
            Directory.CreateDirectory(".unity/capture");ScreenCapture.CaptureScreenshot(".unity/capture/freecafe-"+runtime.World.State.currentArea+".png");
        }
    }
}
