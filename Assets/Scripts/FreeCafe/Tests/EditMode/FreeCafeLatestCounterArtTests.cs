using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeLatestCounterArtTests
    {
        private const string Sliced = "Assets/Art/Gameplay/咖啡馆/Sliced/";
        [Test]
        public void SuppliedPropsUseExistingDerivedPathsAndAnimatedGrinder()
        {
            var art = Resources.Load<CafeArtLibrary>("FreeCafe/ArtLibrary");
            foreach (string id in new[] { "source.beans", "source.cocoa", "source.matcha", "source.milk", "source.honey",
                "source.cream", "source.sugar", "machine.heater", "container.extractor", "container.extractor.filter", "cup.small", "cup.large" })
                Assert.That(AssetDatabase.GetAssetPath(art.Resolve(id)), Does.StartWith(Sliced + "Entities/" + id + "/"), id);
            Assert.That(AssetDatabase.GetAssetPath(art.Resolve("machine.grinder")),
                Is.EqualTo(Sliced + "Entities/machine.grinder/Animated/body.png"));
        }
        [TestCase("cup.small")]
        [TestCase("cup.large")]
        public void CupStackUsesSeparateOcclusionArtOnMatchingCanvas(string id)
        {
            var art = Resources.Load<CafeArtLibrary>("FreeCafe/ArtLibrary");
            var top = art.Resolve(id); var nested = art.Resolve(id, "nested");
            Assert.That(nested, Is.Not.SameAs(top));
            Assert.That(nested.rect.size, Is.EqualTo(top.rect.size));
            Assert.That(nested.pivot, Is.EqualTo(top.pivot));
            Assert.That(AssetDatabase.GetAssetPath(nested), Does.EndWith("/nested.png"));
        }
        [Test]
        public void JarSwitchKeepsClosedAndOpenArtworkOnSameTransformAndCollider()
        {
            var prefab = Resources.Load<FreeCafeEntityPrefabLibrary>("FreeCafe/EntityPrefabLibrary").Require("source.honey");
            var root = Object.Instantiate(prefab.gameObject);
            try
            {
                var view = root.GetComponent<FreeEntityView>(); var visual = root.GetComponent<FreeCafeSourceJarVisual>();
                Assert.That(visual, Is.Not.Null);
                Assert.That(visual.closedSprite, Is.Not.SameAs(visual.openSprite));
                Assert.That(visual.closedSprite.rect.size, Is.EqualTo(visual.openSprite.rect.size));
                var position = root.transform.localPosition; var scale = view.body.transform.localScale; var collider = view.hitbox.size;
                visual.Present(true);
                Assert.That(visual.IsOpen, Is.True); Assert.That(view.body.sprite, Is.SameAs(visual.openSprite));
                visual.Present(false);
                Assert.That(visual.IsOpen, Is.False); Assert.That(view.body.sprite, Is.SameAs(visual.closedSprite));
                Assert.That(root.transform.localPosition, Is.EqualTo(position));
                Assert.That(view.body.transform.localScale, Is.EqualTo(scale)); Assert.That(view.hitbox.size, Is.EqualTo(collider));
                Assert.That(view.Runtime, Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void CoffeeShopUsesOneExistingSuppliedBackdropAndClosedJar()
        {
            const string path = "Assets/Scenes/CoffeeShop.unity";
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var runtime = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FreeCafeRuntime>(true)).Single();
                var counter = runtime.areaRoots[(int)AreaId.Counter];
                var backdrop = counter.Find("SuppliedCounterBackdrop").GetComponent<SpriteRenderer>();
                Assert.That(backdrop.enabled, Is.True);
                Assert.That(AssetDatabase.GetAssetPath(backdrop.sprite), Is.EqualTo(Sliced + "Environment/counter.png"));
                Assert.That(backdrop.sprite.rect.width, Is.EqualTo(4096));
                Assert.That(backdrop.sprite.rect.height, Is.EqualTo(2304));
                var jar = runtime.authoringPreviews.SelectMany(r=>r.GetComponentsInChildren<FreeEntityView>(true))
                    .Single(v=>v.PrefabSpecId=="source.honey");
                var visual = jar.GetComponent<FreeCafeSourceJarVisual>();
                Assert.That(visual, Is.Not.Null); Assert.That(jar.body.sprite, Is.SameAs(visual.closedSprite));
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
