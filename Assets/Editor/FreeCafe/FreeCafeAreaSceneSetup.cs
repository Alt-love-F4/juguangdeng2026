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
            var floor1=Floor(building,"floor1",0);var floor2=Floor(building,"floor2",40);var floorB1=Floor(building,"floorB1",-40);
            Undo.RecordObject(app,"Configure cafe area navigation");
            var manager=app.GetComponent<FreeCafeAreaManager>();if(manager==null) manager=Undo.AddComponent<FreeCafeAreaManager>(app.gameObject);
            Undo.RecordObject(manager,"Configure cafe camera anchors");
            app.areaManager=manager;manager.areas=new CafeAreaAnchor[4];
            for(int i=0;i<4;i++)
            {
                var area=(AreaId)i;var root=app.areaRoots[i];var floor=area==AreaId.TopFloor ? floor2 : area==AreaId.Basement ? floorB1 : floor1;
                Undo.RecordObject(root,"Place area under its floor");
                if(root.parent!=floor){Undo.SetTransformParent(root,floor,"Place area under its floor");root.localPosition=Vector3.zero;}
                if(area==AreaId.Counter && Vector3.Distance(root.position,app.areaRoots[0].position)<.1f) root.localPosition=app.areaRoots[0].localPosition+new Vector3(60,0,0);
                var anchor=root.Find("CameraAnchor");
                if(anchor==null){var go=new GameObject("CameraAnchor");Undo.RegisterCreatedObjectUndo(go,"Create camera anchor");anchor=go.transform;anchor.SetParent(root,false);}
                root.gameObject.SetActive(true);floor.gameObject.SetActive(true);
                manager.areas[i]=new CafeAreaAnchor{area=area,floor=area==AreaId.TopFloor ? CafeFloor.Second : area==AreaId.Basement ? CafeFloor.Basement : CafeFloor.First,contentRoot=root,cameraAnchor=anchor};
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
