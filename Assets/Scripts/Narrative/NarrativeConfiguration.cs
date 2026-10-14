using System.Collections.Generic;

namespace Spotlight.Narrative
{
    public sealed class NarrativeConfiguration
    {
        public List<NarrativeCafeVisitDefinition> cafeVisits = new List<NarrativeCafeVisitDefinition>();
        public List<NarrativeCafeVisitHookDefinition> cafeVisitHooks = new List<NarrativeCafeVisitHookDefinition>();
        public List<NarrativeTemplateDefinition> templates = new List<NarrativeTemplateDefinition>();
        public List<NarrativeSetupParamsDefinition> setups = new List<NarrativeSetupParamsDefinition>();
        public List<NarrativeChoiceDefinition> choices = new List<NarrativeChoiceDefinition>();
        public List<NarrativeAbilityCheckDefinition> abilityChecks = new List<NarrativeAbilityCheckDefinition>();
        public List<NarrativeAbilityCheckConditionDefinition> abilityCheckConditions = new List<NarrativeAbilityCheckConditionDefinition>();
        public List<NarrativeTextDefinition> texts = new List<NarrativeTextDefinition>();
        public List<NarrativeEventDateDefinition> eventDates = new List<NarrativeEventDateDefinition>();
        public List<NarrativeObjectDefinition> objects = new List<NarrativeObjectDefinition>();
        public List<NarrativeBarDefinition> bars = new List<NarrativeBarDefinition>();
        public List<NarrativeTriggerDefinition> triggers = new List<NarrativeTriggerDefinition>();
        public List<NarrativeEventDefinition> events = new List<NarrativeEventDefinition>();
        public List<NarrativeResultDefinition> results = new List<NarrativeResultDefinition>();
        public List<NarrativeActionDefinition> actions = new List<NarrativeActionDefinition>();
        public List<NarrativeMapPositionDefinition> mapPositions = new List<NarrativeMapPositionDefinition>();
        public List<NarrativeConditionDefinition> conditions = new List<NarrativeConditionDefinition>();
        public List<NarrativeConditionKeyDefinition> conditionKeys = new List<NarrativeConditionKeyDefinition>();
        public List<NarrativeEndingDefinition> endings = new List<NarrativeEndingDefinition>();
        public List<NarrativeOpeningPresentationDefinition> openingPresentations = new List<NarrativeOpeningPresentationDefinition>();
        public List<NarrativeOpeningLineDefinition> openingLines = new List<NarrativeOpeningLineDefinition>();
        public NarrativePresentationConfiguration presentations = new NarrativePresentationConfiguration();
    }
}
