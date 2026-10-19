using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Editor.Tests
{
    public sealed class NarrativeRuntimeIntegrationValidationTests
    {
        [Test] public void NewOwnedRangesAreAcceptedWithoutLooseningOrdinaryBars()
        {
            var c = new NarrativeConfiguration(); c.objects.Add(new NarrativeObjectDefinition { objId = "player" });
            c.bars.Add(new NarrativeBarDefinition { barKey = "training.language", ownerId = "player", min = 0, max = 9 });
            c.bars.Add(new NarrativeBarDefinition { barKey = "faction.rose", ownerId = "player", min = -20, max = 20 });
            c.bars.Add(new NarrativeBarDefinition { barKey = "adele.affection", ownerId = "player", min = -20, max = 20 });
            Assert.That(NarrativeConfigurationValidator.Validate(c), Is.Empty);
            c.bars.Add(new NarrativeBarDefinition { barKey = "ordinary", ownerId = "player", min = 0, max = 9 });
            Assert.That(NarrativeConfigurationValidator.Validate(c).Any(e => e.Contains("ordinary") && e.Contains("range must")));
        }
    }
}
