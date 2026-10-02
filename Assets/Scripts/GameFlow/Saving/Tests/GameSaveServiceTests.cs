using NUnit.Framework;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class GameSaveServiceTests
    {
        private MemorySaveStorage mStorage;
        private GameSaveService mService;

        [SetUp]
        public void SetUp()
        {
            mStorage = new MemorySaveStorage();
            mService = new GameSaveService(mStorage);
        }

        [TearDown]
        public void TearDown()
        {
            SaveKit.Reset();
        }

        [Test]
        public void NewServiceReportsSixEmptySlotsAndFirstManualSlot()
        {
            var slots = mService.List();

            Assert.That(slots, Has.Count.EqualTo(GameSaveService.SlotCount));
            Assert.That(slots[0].State, Is.EqualTo(GameSaveSlotState.Empty));
            Assert.That(mService.FindFirstEmptyManualSlot(), Is.EqualTo(GameSaveService.FirstManualSlot));
        }

        [Test]
        public void SaveLoadAndListExposeGameMetadata()
        {
            var input = new GameSaveData
            {
                SceneName = "SampleScene",
                ElapsedPlaytimeSeconds = 42.5,
                CheckpointId = "checkpoint_a",
                State = "playing"
            };
            string error;

            Assert.That(mService.TrySaveManual(1, input, out error), Is.True, error);
            Assert.That(mService.TryLoad(1, out var loaded, out error), Is.True, error);
            Assert.That(loaded.SceneName, Is.EqualTo(input.SceneName));
            Assert.That(loaded.ElapsedPlaytimeSeconds, Is.EqualTo(input.ElapsedPlaytimeSeconds));

            var slot = mService.GetSlot(1);
            Assert.That(slot.State, Is.EqualTo(GameSaveSlotState.Occupied));
            Assert.That(slot.SceneName, Is.EqualTo("SampleScene"));
            Assert.That(slot.ElapsedPlaytimeSeconds, Is.EqualTo(42.5));
            Assert.That(slot.SavedAtUtc.HasValue, Is.True);
        }

        [Test]
        public void FirstEmptySlotDoesNotOverwriteOccupiedSlots()
        {
            string error;
            Assert.That(mService.TrySaveManual(1, CreateData("first"), out error), Is.True, error);
            Assert.That(mService.FindFirstEmptyManualSlot(), Is.EqualTo(2));

            Assert.That(mService.TrySaveManual(1, CreateData("replacement"), out error), Is.True, error);
            Assert.That(mService.TryLoad(1, out var loaded, out error), Is.True, error);
            Assert.That(loaded.SceneName, Is.EqualTo("replacement"));
        }

        [Test]
        public void CorruptSlotIsOccupiedAndBlocksAutosaveReplacement()
        {
            var target = SaveTarget.Slot(0);
            mStorage.Write(target, new byte[] { 0x01, 0x02, 0x03 });
            string error;

            Assert.That(mService.GetSlot(0).State, Is.EqualTo(GameSaveSlotState.Corrupt));
            Assert.That(mService.TryAutoSave(CreateData("new"), out error), Is.False);
            StringAssert.Contains("unreadable", error);
            Assert.That(mStorage.Read(target), Is.EqualTo(new byte[] { 0x01, 0x02, 0x03 }));
        }

        [Test]
        public void DeleteManualSlotMakesItEmpty()
        {
            string error;
            Assert.That(mService.TrySaveManual(5, CreateData("delete me"), out error), Is.True, error);
            Assert.That(mService.TryDeleteManual(5, out error), Is.True, error);
            Assert.That(mService.GetSlot(5).State, Is.EqualTo(GameSaveSlotState.Empty));
            Assert.That(mService.TryDeleteManual(5, out error), Is.False);
        }

        [Test]
        public void InvalidAndAutosaveDeleteRequestsReturnErrors()
        {
            string error;
            Assert.That(mService.TrySaveManual(0, CreateData("invalid"), out error), Is.False);
            Assert.That(mService.TrySaveManual(6, CreateData("invalid"), out error), Is.False);
            Assert.That(mService.TryDeleteManual(0, out error), Is.False);
            Assert.That(mService.TryLoad(6, out _, out error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        private static GameSaveData CreateData(string scene)
        {
            return new GameSaveData { SceneName = scene, State = "playing" };
        }
    }
}
