using System;
using NUnit.Framework;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativeWalkthroughLeaseTests
    {
        [Test] public void WalkthroughExcludesOtherRunsAndReleasesIdempotently()
        {
            Assert.That(NarrativeAutoRunner.IsActive || NarrativeAutoRunner.IsWalkthroughActive, Is.False);
            var lease = NarrativeAutoRunner.AcquireWalkthrough();
            try
            {
                Assert.That(NarrativeAutoRunner.IsWalkthroughActive, Is.True);
                Assert.Throws<InvalidOperationException>(() => NarrativeAutoRunner.AcquireWalkthrough());
            }
            finally { lease.Dispose(); lease.Dispose(); }
            Assert.That(NarrativeAutoRunner.IsWalkthroughActive, Is.False);
            using (NarrativeAutoRunner.AcquireWalkthrough()) Assert.That(NarrativeAutoRunner.IsWalkthroughActive, Is.True);
            Assert.That(NarrativeAutoRunner.IsWalkthroughActive, Is.False);
        }
    }
}
