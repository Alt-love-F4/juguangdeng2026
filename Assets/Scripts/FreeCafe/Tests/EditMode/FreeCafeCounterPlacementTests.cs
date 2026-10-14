using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeCounterPlacementTests
    {
        private CafeWorld world;
        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction(); world.EnterProduction();
        }
        private WorldEntity Cup(string source = "s.cups.small")
        { Assert.That(world.Take(source, AreaId.Counter, 0, -1, out var cup).success, Is.True); return cup; }
        [Test] public void SilverKettleHeatsItsOwnContentsWithoutStoveAndRetainsThem()
        {
            world.TakeAndInteract("s.water", "m.heater", AreaId.Counter, 0, 0);
            var kettle = world.Find("m.heater"); float volume = world.LiquidVolume(kettle);
            Assert.That(world.OperateProduction(kettle.id).success, Is.True);
            world.Tick(2); Assert.That(world.TemperatureCelsius(kettle), Is.InRange(59.9f, 60.1f));
            var copy = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(world.Restore(copy).success, Is.True); world.Tick(2); kettle = world.Find("m.heater");
            Assert.That(world.TemperatureCelsius(kettle), Is.EqualTo(100).Within(.001));
            Assert.That(world.LiquidVolume(kettle), Is.EqualTo(volume));
            Assert.That(kettle.production.phase, Is.EqualTo(ProductionPhase.Boiling));
        }
        [Test] public void EmptyKettleDoesNotStartAndLiftingStopsHeat()
        {
            Assert.That(world.StartSelfHeating("m.heater").success, Is.False);
            world.TakeAndInteract("s.water", "m.heater", AreaId.Counter, 0, 0); world.StartSelfHeating("m.heater");
            world.Tick(1); world.SuspendContainerHeating("m.heater"); float t = world.TemperatureCelsius(world.Find("m.heater"));
            world.Tick(10); Assert.That(world.TemperatureCelsius(world.Find("m.heater")), Is.EqualTo(t));
        }
        [TestCase("m.heater")][TestCase("m.brewer")][TestCase("tool.milk-pitcher")][TestCase("v.extractor")]
        public void ReusableVesselsReturnWithoutLosingContents(string id)
        {
            var vessel = world.Find(id); world.TakeAndInteract("s.water", id, AreaId.Counter, 0, 0);
            float volume = world.LiquidVolume(vessel); world.Move(id, AreaId.Counter, 0, -1);
            Assert.That(world.ReturnVesselHome(id).success, Is.True);
            var home = System.Array.Find(world.Catalog.layout, e => e.id == id);
            Assert.That(vessel.x, Is.EqualTo(home.x)); Assert.That(vessel.y, Is.EqualTo(home.y));
            Assert.That(world.LiquidVolume(vessel), Is.EqualTo(volume));
        }
        [Test] public void DetachedTowerParksSeparatelyAndCanReassemble()
        {
            var jug = world.Find("v.extractor"); var filter = world.ExtractionFilter(jug);
            world.Move(jug.id, AreaId.Counter, 0, -1);
            world.Move(filter.id, AreaId.Counter, 0, -1); world.ReturnVesselHome(filter.id);
            Assert.That(world.ExtractionFilter(jug), Is.Null); Assert.That(filter.x, Is.Not.EqualTo(jug.x));
            Assert.That(world.AttachExtractionFilter(filter.id, jug.id).success, Is.True);
            Assert.That(filter.y, Is.EqualTo(jug.y + world.Catalog.production.extractionFilterOffsetY));
        }
        [Test] public void MovingLowerDetachesAndLeavesUpperExactlyWhereItWas()
        {
            var jug = world.Find("v.extractor"); var filter = world.ExtractionFilter(jug);
            float x = filter.x, y = filter.y;
            Assert.That(world.Move(jug.id, AreaId.Counter, 0, -1).success, Is.True);
            Assert.That(world.ExtractionFilter(jug), Is.Null);
            Assert.That(world.ExtractionJug(filter), Is.Null);
            Assert.That(filter.x, Is.EqualTo(x)); Assert.That(filter.y, Is.EqualTo(y));
        }
        [Test] public void UpperMovesBothAndReturnsWholeAssemblyHomeWithContentsIntact()
        {
            var jug = world.Find("v.extractor"); var filter = world.ExtractionFilter(jug);
            float homeX = jug.x, homeY = jug.y;
            jug.contents.Add(new Portion { specId = "water", units = 1, milliliters = 10 });
            Assert.That(world.Move(filter.id, AreaId.Counter, 0, 0).success, Is.True);
            Assert.That(world.ExtractionFilter(jug), Is.SameAs(filter));
            Assert.That(jug.x, Is.EqualTo(-world.Catalog.production.extractionFilterOffsetX));
            Assert.That(jug.y, Is.EqualTo(-world.Catalog.production.extractionFilterOffsetY));
            Assert.That(world.ReturnVesselHome(filter.id).success, Is.True);
            Assert.That(jug.x, Is.EqualTo(homeX)); Assert.That(jug.y, Is.EqualTo(homeY));
            Assert.That(world.LiquidVolume(jug), Is.EqualTo(10));
            Assert.That(world.Restore(JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State))).success, Is.True);
            Assert.That(world.ExtractionJug(world.Find(filter.id)), Is.SameAs(world.Find(jug.id)));
        }
        [Test] public void InvalidUpperGroupMoveIsAtomic()
        {
            var jug = world.Find("v.extractor"); var filter = world.ExtractionFilter(jug);
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.Move(filter.id, AreaId.Counter, 999, 999).success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
            // Upper fits here, but its companion lower jug extends below the area's bound.
            var area = System.Array.Find(world.Catalog.areas, e => e.id == AreaId.Counter);
            float y = -area.halfHeight + world.Spec(filter.specId).height * .5f + .01f;
            Assert.That(world.CanPosition(filter.specId, AreaId.Counter, 0, y).success, Is.True);
            Assert.That(world.Move(filter.id, AreaId.Counter, 0, y).success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }
        [Test] public void TemporaryCupWithoutIdentityNeverHasACupAboveIt()
        {
            Assert.That(world.CupAbove(new WorldEntity { specId = "cup.small" }), Is.Null);
            Assert.That(world.CupAbove(new WorldEntity { id = "", specId = "cup.large" }), Is.Null);
        }
        [TestCase("small", 4, .22f)] [TestCase("large", 5, .58f)]
        public void CupSpacingAndInitialStockCanBePreviewedWithoutEnteringPlayMode(string size, int count, float spacing)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FreeCafe/FreeEntity_source_cups_" + size + ".prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<FreeEntityView>(); var stack = instance.GetComponent<FreeCafeCupStackVisual>();
                stack.RefreshAuthoringPreview(); stack.cupSpacing = spacing; stack.RefreshAuthoringPreview();
                Assert.That(view.Runtime, Is.Null, "Preview must not initialize gameplay");
                Assert.That(stack.VisibleCupCount, Is.EqualTo(count)); Assert.That(stack.StackStep, Is.EqualTo(spacing));
                Assert.That(view.body.transform.localPosition.y, Is.EqualTo((count - 1) * spacing).Within(.0001f));
                Assert.That(view.body.sprite, Is.SameAs(Resources.Load<CafeArtLibrary>("FreeCafe/ArtLibrary").Resolve("cup." + size)));
                Assert.That(instance.transform.Find("NestedCup1").localPosition.y, Is.EqualTo(spacing).Within(.0001f));
                stack.maxVisibleCups = 2; stack.RefreshAuthoringPreview();
                Assert.That(stack.VisibleCupCount, Is.EqualTo(2));
                Assert.That(view.body.transform.localPosition.y, Is.EqualTo(spacing).Within(.0001f));
                Assert.That(instance.transform.Find("NestedCup1").gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(instance); }
        }
        [Test] public void EmptyCupsNestWithIndividualIdentitiesAndUnstackFromTheTop()
        {
            var bottom = Cup(); var middle = Cup(); var top = Cup();
            Assert.That(world.Interact(middle.id, bottom.id).success, Is.True);
            Assert.That(world.Interact(top.id, bottom.id).success, Is.True);
            Assert.That(top.cupStackBelowId, Is.EqualTo(middle.id)); Assert.That(middle.cupStackBelowId, Is.EqualTo(bottom.id));
            Assert.That(world.Move(bottom.id, AreaId.Counter, 1, -1).success, Is.False);
            Assert.That(world.Move(top.id, AreaId.Counter, 1, -1).success, Is.True);
            Assert.That(top.cupStackBelowId, Is.Null); Assert.That(top.x, Is.EqualTo(1));
            Assert.That(world.ReturnVesselHome(top.id).success, Is.False);
        }
        [Test] public void MismatchedOrFilledCupsDoNotStackAndCyclesCannotRestore()
        {
            var a = Cup(); var b = Cup(); var large = Cup("s.cups.large");
            Assert.That(world.Interact(a.id, large.id).success, Is.False);
            world.Interact(b.id, a.id);
            Assert.That(world.Interact(a.id, b.id).success, Is.False);
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            snapshot.entities.Find(e => e.id == a.id).cupStackBelowId = b.id;
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.Restore(snapshot).success, Is.False); Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }
        [Test] public void LooseCupStackSurvivesSaveRestore()
        {
            var a = Cup(); var b = Cup(); world.Interact(b.id, a.id);
            Assert.That(world.Restore(JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State))).success, Is.True);
            Assert.That(world.Find(b.id).cupStackBelowId, Is.EqualTo(a.id));
        }
    }
}
