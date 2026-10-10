using System;
using System.IO;
using Spotlight.FreeCafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spotlight.FreeCafe.Editor
{
    public static class FreeCafeAreaSceneSetup
    {
        [MenuItem("Tools/Free Cafe/Configure In-Scene Areas")]
        public static void Configure()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first");
            var app=UnityEngine.Object.FindFirstObjectByType<FreeCafeRuntime>();
            if(app==null || app.areaRoots==null || app.areaRoots.Length!=4) throw new InvalidOperationException("Four FreeCafe area roots required");
            var scene=app.gameObject.scene;
            string backup=".unity/recovery/area-layout-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);
            if(!EditorSceneManager.SaveScene(scene,backup+"/"+scene.name+".unity",true)) throw new IOException("Cannot back up scene");
            var building=app.areaRoots[0].parent;
            if(building!=null && building.name!="BuildingViews") building=building.parent;
            if(building==null || building.name!="BuildingViews") throw new InvalidOperationException("Expected BuildingViews hierarchy");
            var floor1=Floor(building,"floor1",0);var floor2=Floor(building,"floor2",40);var floor3=Floor(building,"floor3",80);
            Undo.RecordObject(app,"Configure cafe area navigation");
            var manager=app.GetComponent<FreeCafeAreaManager>();if(manager==null) manager=Undo.AddComponent<FreeCafeAreaManager>(app.gameObject);
            Undo.RecordObject(manager,"Configure cafe camera anchors");
            app.areaManager=manager;manager.areas=new CafeAreaAnchor[4];
            for(int i=0;i<4;i++)
            {
                var area=(AreaId)i;var root=app.areaRoots[i];var floor=area==AreaId.TopFloor ? floor2 : area==AreaId.Basement ? floor3 : floor1;
                Undo.RecordObject(root,"Place area under its floor");
                // 工作台是一楼场景的嵌套子场景：作为大厅的子物体，位于其下方，镜头平滑平移触发
                if(area==AreaId.Counter)
                {
                    if(root.parent!=app.areaRoots[(int)AreaId.Cafe]) Undo.SetTransformParent(root,app.areaRoots[(int)AreaId.Cafe],"Place workbench under cafe");
                    root.localPosition=new Vector3(0,-40,0);
                }
                else if(root.parent!=floor){Undo.SetTransformParent(root,floor,"Place area under its floor");root.localPosition=Vector3.zero;}
                var anchor=root.Find("CameraAnchor");
                if(anchor==null){var go=new GameObject("CameraAnchor");Undo.RegisterCreatedObjectUndo(go,"Create camera anchor");anchor=go.transform;anchor.SetParent(root,false);}
                root.gameObject.SetActive(true);floor.gameObject.SetActive(true);
                var entry=new CafeAreaAnchor{area=area,floor=area==AreaId.TopFloor ? CafeFloor.Second : area==AreaId.Basement ? CafeFloor.Third : CafeFloor.First,contentRoot=root,cameraAnchor=anchor};
                // 工作台是嵌套子场景：聚焦时根停靠到一楼镜头前，摄像头不平移
                if(area==AreaId.Counter)
                {
                    var dock=root.GetComponent<FreeCafeSubSceneDock>();if(dock==null) dock=Undo.AddComponent<FreeCafeSubSceneDock>(root.gameObject);dock.areaManager=manager;
                    entry.dockToView=true;entry.dock=dock;
                }
                manager.areas[i]=entry;
            }
            var camera=app.worldCamera!=null ? app.worldCamera : Camera.main;
            if(camera==null) throw new InvalidOperationException("Assign the gameplay camera");
            app.worldCamera=camera;Undo.RecordObject(camera.transform,"Focus lobby");
            manager.Initialize(camera);manager.Focus(AreaId.Cafe,true);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("[FreeCafe Areas] Three floors / four camera destinations configured without rebuilding artwork. Backup: "+backup);
        }
        private static Transform Floor(Transform parent,string name,float y)
        {
            var floor=parent.Find(name);if(floor!=null) return floor;
            var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Create floor anchor");go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(0,y,0);return go.transform;
        }
    }
}
