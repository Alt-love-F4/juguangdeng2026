using NUnit.Framework;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class NarrativeReachabilityResultTests
    {
        [Test] public void OnlyMatchingUnassistedEndingProvesReachability()
        {
            Assert.That(NarrativeReachabilityRunner.Classify("a", "a", "ending", false), Is.EqualTo("已见证开局可达"));
            Assert.That(NarrativeReachabilityRunner.Classify("a", "a", "ending", true), Does.Contain("非严格证明"));
            Assert.That(NarrativeReachabilityRunner.Classify("a", "b", "ending", false), Does.Contain("目标未证明"));
            Assert.That(NarrativeReachabilityRunner.Classify("a", null, "unproven", false), Does.Contain("未证明"));
            Assert.That(NarrativeReachabilityRunner.Classify("a", "a", "error", false), Does.Contain("未证明"));
        }
    }
}
