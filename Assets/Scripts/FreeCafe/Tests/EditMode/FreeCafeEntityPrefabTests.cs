using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeEntityPrefabTests
    {
        private CafeCatalog catalog;
        private FreeCafeEntityPrefabLibrary library;
        [SetUp] public void Setup()
        {
            catalog=JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text);
            library=Resources.Load<FreeCafeEntityPrefabLibrary>("FreeCafe/EntityPrefabLibrary");
        }
        [Test] public void EveryCatalogEntityHasItsOwnPersistentPrefabAndExtensionAnchors()
        {
            Assert.DoesNotThrow(()=>library.Validate(catalog));
            var paths=new HashSet<string>();
            Assert.That(library.entries.Length,Is.EqualTo(catalog.entities.Length));
            foreach(var spec in catalog.entities)
            {
                var prefab=library.Require(spec.id);string path=AssetDatabase.GetAssetPath(prefab);
                Assert.That(path,Does.EndWith(".prefab"));Assert.That(paths.Add(path),Is.True,spec.id);
                Assert.That(prefab.PrefabSpecId,Is.EqualTo(spec.id));Assert.That(prefab.AuthoringEntityId,Is.Empty);
                Assert.That(prefab.effectsRoot,Is.Not.Null,spec.id);Assert.That(prefab.liquidRoot,Is.Not.Null,spec.id);
                Assert.That(prefab.pourOrigin,Is.Not.Null,spec.id);
                foreach(string child in new[]{"Art","Label","FillStatus","FillBar","FillBarBackground"})
                    Assert.That(prefab.transform.Find(child),Is.Not.Null,spec.id+" / "+child);
            }
        }
        [Test] public void MissingOrDuplicateMappingsAreRejectedRatherThanUsingTheGenericPrefab()
        {
            Assert.Throws<InvalidOperationException>(()=>library.Require("unconfigured.entity"));
            var invalid=ScriptableObject.CreateInstance<FreeCafeEntityPrefabLibrary>();
            try
            {
                invalid.entries=new[]{library.entries[0]};Assert.Throws<InvalidOperationException>(()=>invalid.Validate(catalog));
                invalid.entries=new[]{library.entries[0],library.entries[0]};Assert.Throws<InvalidOperationException>(()=>invalid.Validate(catalog));
            }
            finally { UnityEngine.Object.DestroyImmediate(invalid); }
        }
        [Test] public void SavedCoffeeShopLayoutContainsConnectedPrefabInstancesNotLooseSpritePreviews()
        {
            const string path="Assets/Scenes/CoffeeShop.unity";
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                FreeCafeRuntime runtime=null;
                foreach(var root in scene.GetRootGameObjects()) { runtime=root.GetComponentInChildren<FreeCafeRuntime>();if(runtime!=null) break; }
                Assert.That(runtime,Is.Not.Null);int count=0;var ids=new HashSet<string>();
                foreach(var root in runtime.authoringPreviews)
                {
                    Assert.That(root.name,Is.EqualTo("AuthoredEntities"));
                    foreach(Transform child in root.transform)
                    {
                        var view=child.GetComponent<FreeEntityView>();Assert.That(view,Is.Not.Null,child.name);
                        Assert.That(ids.Add(view.AuthoringEntityId),Is.True);count++;
                        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(view),Is.SameAs(library.Require(view.PrefabSpecId)));
                        Assert.That(view.Runtime,Is.Null,"编辑状态不能创建世界");
                    }
                }
                Assert.That(count,Is.EqualTo(catalog.layout.Length));
                foreach(var entry in catalog.layout) Assert.That(ids.Contains(entry.id),Is.True,entry.id);
            }
            finally { if(opened) EditorSceneManager.CloseScene(scene,true); }
        }
    }
}
