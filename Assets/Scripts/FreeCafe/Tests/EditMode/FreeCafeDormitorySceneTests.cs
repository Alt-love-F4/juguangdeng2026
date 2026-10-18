using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeDormitorySceneTests
    {
        [Test]
        public void DormitoryHasNoLegacyMockupsAndArrowPointsAtClickableForegroundBed()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/CoffeeShop.unity");
            try
            {
                var runtime = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<FreeCafeRuntime>(true)).Single();
                Transform dormitory = runtime.areaRoots[(int)AreaId.Basement];
                foreach (string name in new[] { "WHITEBOX_future.press", "WHITEBOX_future.newspaper", "AreaArtNote" })
                    Assert.That(dormitory.Find(name), Is.Null, name);
                Transform entrance = dormitory.Find("DayBedProp/BedEntrance");
                Assert.That(entrance, Is.Not.Null);
                Assert.That(entrance.GetComponent<FreeCafeBedDayEnd>().runtime, Is.SameAs(runtime));
                var prop = entrance.GetComponent<FreeCafeInteractivePropView>();
                Assert.That(AssetDatabase.GetAssetPath(prop.artwork.sprite),
                    Is.EqualTo("Assets/Art/FreeCafe/Whitebox/ui.portal.png"));
                Assert.That(prop.highlight, Is.Not.Null);
                Vector2 direction = dormitory.InverseTransformDirection(prop.artwork.transform.right);
                Assert.That(Vector2.Dot(direction.normalized, Vector2.down), Is.GreaterThan(.99f));
                Vector3 arrow = dormitory.InverseTransformPoint(prop.artwork.transform.position);
                Assert.That(arrow.x, Is.InRange(-24f, -14f), "Arrow must be over the left foreground bed, not the room centre.");
                Assert.That(arrow.y, Is.InRange(-5f, -2f));
                var hitbox = entrance.GetComponent<BoxCollider2D>();
                Physics2D.SyncTransforms();
                Vector3 bed = dormitory.TransformPoint(new Vector3(-19.2f, -7f, 0f));
                Assert.That(hitbox.OverlapPoint(bed), Is.True, "Bed itself must remain clickable.");
                Assert.That(hitbox.OverlapPoint(prop.artwork.transform.position), Is.True, "Arrow must remain clickable.");
                Assert.That(runtime.areaManager.areas.Length, Is.EqualTo(4));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
