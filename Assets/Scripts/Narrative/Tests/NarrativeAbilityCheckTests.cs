using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeAbilityCheckTests
    {
        [Test]
        public void EqualityPassesAndGroupsUseAndOr()
        {
            var definition = new NarrativeAbilityCheckDefinition { checkId = "check" };
            var conditions = new List<NarrativeAbilityCheckConditionDefinition>
            {
                new NarrativeAbilityCheckConditionDefinition { conditionId = "a", checkId = "check", attributeId = "perception", threshold = 3, groupIndex = 1 },
                new NarrativeAbilityCheckConditionDefinition { conditionId = "b", checkId = "check", attributeId = "wisdom", threshold = 2, groupIndex = 1 },
                new NarrativeAbilityCheckConditionDefinition { conditionId = "c", checkId = "check", attributeId = "language", threshold = 4, groupIndex = 2 }
            };
            var result = NarrativeAbilityCheckEvaluator.Evaluate(definition, conditions, id => (true, id == "perception" ? 3 : id == "wisdom" ? 1 : 4));
            Assert.That(result.passed, Is.True, "group 1 fails, group 2 passes; equality at threshold passes");
            Assert.That(result.values, Has.Count.EqualTo(3));
            Assert.That(result.values[0].passed, Is.True);
            Assert.That(result.values[0].displayName, Is.EqualTo("perception"));
        }

        [Test]
        public void EvaluatorUsesProvidedDisplayNameResolver()
        {
            var definition = new NarrativeAbilityCheckDefinition { checkId = "check" };
            var conditions = new[] { new NarrativeAbilityCheckConditionDefinition { conditionId = "a", attributeId = "wisdom", threshold = 2, groupIndex = 1 } };
            var result = NarrativeAbilityCheckEvaluator.Evaluate(definition, conditions, _ => (true, 2), id => "能力:" + id);
            Assert.That(result.values[0].displayName, Is.EqualTo("能力:wisdom"));
        }

        [Test]
        public void MissingAttributeReaderFailsClosed()
        {
            var definition = new NarrativeAbilityCheckDefinition { checkId = "check" };
            var conditions = new[] { new NarrativeAbilityCheckConditionDefinition { conditionId = "a", attributeId = "perception", threshold = 3, groupIndex = 1 } };
            Assert.Throws<InvalidOperationException>(() => NarrativeAbilityCheckEvaluator.Evaluate(definition, conditions, _ => (false, 0)));
        }
    }
}
