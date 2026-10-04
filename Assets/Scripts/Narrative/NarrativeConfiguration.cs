using System.Collections.Generic;

namespace Spotlight.Narrative
{
    public sealed class NarrativeConfiguration
    {
        public List<NarrativeTemplateDefinition> templates = new List<NarrativeTemplateDefinition>();
        public List<NarrativeSetupParamsDefinition> setups = new List<NarrativeSetupParamsDefinition>();
        public List<NarrativeChoiceDefinition> choices = new List<NarrativeChoiceDefinition>();
        public List<NarrativeTextDefinition> texts = new List<NarrativeTextDefinition>();
        public List<NarrativeEventDateDefinition> eventDates = new List<NarrativeEventDateDefinition>();
        public List<NarrativeObjectDefinition> objects = new List<NarrativeObjectDefinition>();
        public List<NarrativeBarDefinition> bars = new List<NarrativeBarDefinition>();
        public List<NarrativeTriggerDefinition> triggers = new List<NarrativeTriggerDefinition>();
        public List<NarrativeEventDefinition> events = new List<NarrativeEventDefinition>();
        public List<NarrativeResultDefinition> results = new List<NarrativeResultDefinition>();
        public List<NarrativeConditionDefinition> conditions = new List<NarrativeConditionDefinition>();
    }
}
