using NUnit.Framework;

namespace Spotlight.GameFlow.Tests
{
    public sealed class DialogueHistoryTests
    {
        [Test]
        public void RevealUpdatesOnlyThisPlaybackLineAndKeepsRepeatedSpeech()
        {
            var service = new DialogueHistoryService();
            long first = service.BeginPlayback();
            service.Reveal(first, 0, "阿黛尔", "你");
            service.Reveal(first, 0, "阿黛尔", "你好");
            Assert.That(service.Entries.Count, Is.EqualTo(1));
            Assert.That(service.Entries[0].Text, Is.EqualTo("你好"));
            service.Reveal(service.BeginPlayback(), 0, "阿黛尔", "你好");
            Assert.That(service.Entries.Count, Is.EqualTo(2));
        }
        [Test]
        public void CapacityEvictsOldWholeEntriesAndSessionsAreIndependent()
        {
            var formal = new DialogueHistoryService { Capacity = 2 };
            var preview = new DialogueHistoryService();
            formal.Reveal(formal.BeginPlayback(), 0, "旁白", "第一句");
            formal.Choice("接受");
            formal.Reveal(formal.BeginPlayback(), 0, "旁白", "最后一句\n仍然完整");
            Assert.That(formal.Entries.Count, Is.EqualTo(2));
            Assert.That(formal.Entries[0].Speaker, Is.EqualTo("玩家选择"));
            Assert.That(formal.Entries[1].Text, Does.EndWith("仍然完整"));
            Assert.That(preview.Entries, Is.Empty);
            formal.Clear();
            Assert.That(formal.Entries, Is.Empty);
        }
    }
}
