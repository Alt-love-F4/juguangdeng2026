using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeSceneLayoutTests
    {
        private Scene preview;
        private FreeCafeRuntime runtime;
        private CafeCatalog catalog;
        [SetUp] public void Setup()
        {
            // 读取同一个正式场景的编辑预览，绝不保存测试位置或改写用户已打开的场景。
            preview=EditorSceneManager.OpenPreviewScene("Assets/Scenes/CoffeeShop.unity");
            foreach(var root in preview.GetRootGameObjects())
            { runtime=root.GetComponentInChildren<FreeCafeRuntime>(true);if(runtime!=null) break; }
            catalog=JsonUtility.FromJson<CafeCatalog>(runtime.catalogAsset.text);
        }
        [TearDown] public void Cleanup() { if(preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
        private FreeEntityView Find(string id)
        {
            foreach(var view in FreeCafeSceneLayout.FindAuthoredEntities(runtime)) if(view.AuthoringEntityId==id) return view;
            Assert.Fail(id);return null;
        }
        [Test] public void ScenePositionOverridesJsonWithoutWritingBackTheCatalogAsset()
        {
            var view=Find("m.grinder");var original=Array.Find(catalog.layout,e=>e.id=="m.grinder");
            float x=original.x+1.1f,y=original.y-.7f;
            view.transform.position=runtime.areaRoots[(int)AreaId.Counter].TransformPoint(new Vector3(x,y,0));
            catalog.layout=FreeCafeSceneLayout.Capture(runtime);FreeCafeSceneLayout.FitAuthoredBounds(runtime,catalog,FreeCafeSceneLayout.FindAuthoredEntities(runtime));var world=new CafeWorld(catalog);
            Assert.That(world.Find("m.grinder").x,Is.EqualTo(x).Within(.001f));
            Assert.That(world.Find("m.grinder").y,Is.EqualTo(y).Within(.001f));
            var unchanged=JsonUtility.FromJson<CafeCatalog>(runtime.catalogAsset.text);
            Assert.That(Array.Find(unchanged.layout,e=>e.id=="m.grinder").x,Is.EqualTo(original.x));
        }
        [Test] public void LayoutOutsideOldWhiteboxBoundsStartsWithoutClampingOrChangingTheAsset()
        {
            float originalWidth=Array.Find(catalog.areas,area=>area.id==AreaId.Counter).halfWidth;
            var view=Find("s.cups.small");view.transform.position=runtime.areaRoots[1].TransformPoint(new Vector3(-22,9,0));
            view.transform.localScale=Vector3.one*3;
            catalog.layout=FreeCafeSceneLayout.Capture(runtime);
            FreeCafeSceneLayout.FitAuthoredBounds(runtime,catalog,FreeCafeSceneLayout.FindAuthoredEntities(runtime));
            var world=new CafeWorld(catalog);
            Assert.That(world.Find("s.cups.small").x,Is.EqualTo(-22).Within(.001f));
            Assert.That(world.Area(AreaId.Counter).halfWidth,Is.GreaterThan(22));
            Assert.That(world.CanPosition("cup.large",AreaId.Counter,-22,9).success,Is.True);
            Assert.That(world.CanPosition("cup.large",AreaId.Counter,1000,1000).success,Is.False);
            var asset=JsonUtility.FromJson<CafeCatalog>(runtime.catalogAsset.text);
            Assert.That(Array.Find(asset.areas,a=>a.id==AreaId.Counter).halfWidth,Is.EqualTo(originalWidth));
        }
        [Test] public void GroupOffsetsAndReparentingUseAreaCoordinatesNotStalePrefabLocals()
        {
            var view=Find("m.grinder");var group=new GameObject("LayoutGroup").transform;
            SceneManager.MoveGameObjectToScene(group.gameObject,preview);group.SetParent(runtime.areaRoots[(int)AreaId.Counter],false);
            group.localPosition=new Vector3(1.2f,-.4f,0);view.transform.SetParent(group,false);view.transform.localPosition=new Vector3(-4,1.1f,0);
            var layout=FreeCafeSceneLayout.Capture(runtime);var entry=Array.Find(layout,e=>e.id=="m.grinder");
            Assert.That(entry.x,Is.EqualTo(-2.8f).Within(.001));Assert.That(entry.y,Is.EqualTo(.7f).Within(.001));
            view.transform.SetParent(runtime.areaRoots[(int)AreaId.TopFloor],false);view.transform.localPosition=new Vector3(1,-2,0);
            entry=Array.Find(FreeCafeSceneLayout.Capture(runtime),e=>e.id=="m.grinder");
            Assert.That(entry.area,Is.EqualTo(AreaId.TopFloor));Assert.That(entry.x,Is.EqualTo(1));Assert.That(entry.y,Is.EqualTo(-2));
        }
        [Test] public void MissingSceneEntityDoesNotGetSilentlyRecreatedFromJsonDuringStartup()
        {
            UnityEngine.Object.DestroyImmediate(Find("s.honey").gameObject);
            catalog.layout=FreeCafeSceneLayout.Capture(runtime);FreeCafeSceneLayout.FitAuthoredBounds(runtime,catalog,FreeCafeSceneLayout.FindAuthoredEntities(runtime));var world=new CafeWorld(catalog);
            Assert.That(world.Find("s.honey"),Is.Null);
        }
        [Test] public void ManualRepairDoesNotResetEditedPositionParentScaleOrCreateDuplicates()
        {
            var view=Find("m.grinder");var group=new GameObject("UserGrouping").transform;
            SceneManager.MoveGameObjectToScene(group.gameObject,preview);group.SetParent(runtime.areaRoots[1],false);
            view.transform.SetParent(group,false);view.transform.localPosition=new Vector3(-4,-.5f,0);view.transform.localScale=new Vector3(1.15f,.9f,1);
            Vector3 position=view.transform.position,scale=view.transform.localScale;int count=FreeCafeSceneLayout.Capture(runtime).Length;
            var builder=Type.GetType("Spotlight.FreeCafe.Editor.FreeCafeEntityPrefabBuilder, Assembly-CSharp-Editor",true);
            var repair=builder.GetMethod("RebuildAuthoredLayout",BindingFlags.Public|BindingFlags.Static);
            repair.Invoke(null,new object[]{runtime,catalog});repair.Invoke(null,new object[]{runtime,catalog});
            Assert.That(view.transform.parent,Is.SameAs(group));Assert.That(view.transform.position,Is.EqualTo(position));
            Assert.That(view.transform.localScale,Is.EqualTo(scale));Assert.That(FreeCafeSceneLayout.Capture(runtime).Length,Is.EqualTo(count));
        }
        [Test] public void SpawnScaleUsesIndividualSourceOverridesRatherThanOneHardcodedMultiplier()
        {
            var source=Find("s.milk");source.transform.SetParent(runtime.areaRoots[1],true);
            source.transform.localScale=new Vector3(4,2,1);
            catalog.layout=FreeCafeSceneLayout.Capture(runtime);
            var scales=new FreeCafeSpawnScale(runtime,FreeCafeSceneLayout.FindAuthoredEntities(runtime),catalog);
            Vector3 expected=Vector3.Scale(runtime.prefabLibrary.Require("milk").transform.localScale,new Vector3(4,2,1));
            Assert.That(Vector3.Distance(scales.Resolve(AreaId.Counter,"milk",Vector3.one),expected),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(scales.Resolve(AreaId.Basement,"milk",Vector3.one),expected),Is.LessThan(.001f));
            // 直接改来源根不会改变另一个物品的生成尺寸。
            Assert.That(scales.Resolve(AreaId.Counter,"water",Vector3.one).x,Is.Not.EqualTo(4));
        }
        [Test] public void DuplicateAuthoredIdsFailClearlyInsteadOfChoosingAnArbitraryLayout()
        {
            var source=Find("m.grinder");UnityEngine.Object.Instantiate(source.gameObject,source.transform.parent);
            Assert.Throws<InvalidOperationException>(()=>FreeCafeSceneLayout.Capture(runtime));
        }
    }
}
