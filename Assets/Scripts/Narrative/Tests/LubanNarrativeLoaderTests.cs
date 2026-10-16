using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class LubanNarrativeLoaderTests
    {
        private static Dictionary<string, JArray> Data()
        {
            return new Dictionary<string, JArray>
            {
                ["tbncscafevisit"] = new JArray(), ["tbncscafevisithook"] = new JArray(),
                ["tbncsabilitycheck"] = new JArray(), ["tbncsabilitycheckcondition"] = new JArray(),
                ["tbcafetutorialstep"] = new JArray(),
                ["tbplayerconfig"] = new JArray(), ["tbplayerattribute"] = new JArray(),
                ["tbcreationgroup"] = new JArray(), ["tbcreationoption"] = new JArray(), ["tbcreationbonus"] = new JArray(),
                ["tbncsaction"] = new JArray(), ["tbncsmapposition"] = new JArray(),
                ["tbncsconditionkey"] = new JArray(), ["tbncsending"] = new JArray(), ["tbncsactionmilestone"] = new JArray(),
                ["tbncspresentationindex"] = JArray.Parse("[{ 'presentationKey':'talk', 'presentationClass':'Conversation', 'logTag':'' }]"),
                ["tbncsconversationpage"] = new JArray(), ["tbncsconversationline"] = new JArray(),
                ["tbncsnotice"] = new JArray(), ["tbncsreadingpage"] = new JArray(), ["tbncsreadingline"] = new JArray(),
                ["tbopeningline"] = new JArray(), ["tbopeningpresentation"] = new JArray(),
                ["tbtext"] = JArray.Parse("[{ 'key':'title', 'text':'Localized title', 'comment':'shared table' }]"),
                ["tbrecipe"] = new JArray(), ["tbingredient"] = new JArray(), ["tbdevice"] = new JArray(),
                ["tbgameparam"] = new JArray(), ["tbworldobject"] = new JArray(), ["tbrecipestep"] = new JArray(), ["tbnpc"] = new JArray(),
                ["tbncsobject"] = JArray.Parse("[{ 'objId':'world', 'objType':'world', 'nameText':'title', 'iconPath':'icon', 'descText':'title', 'tags':'test' }, { 'objId':'npc', 'objType':'npc', 'nameText':'title', 'iconPath':'', 'descText':'', 'tags':'' }]"),
                ["tbncsbar"] = new JArray(Bar("world", "calendar"), BaseBar()),
                ["tbncstrigger"] = JArray.Parse("[{ 'triggerId':'trigger', 'barKey':'world.calendar', 'threshold':3, 'direction':'both', 'crossType':'hold', 'onceType':'repeat', 'reachCount':'all', 'resetOnExit':true, 'eventId':'event', 'priority':9, 'enabled':true }]"),
                ["tbncsevent"] = JArray.Parse("[{ 'eventId':'event', 'eventKind':'world', 'presentationClass':'Conversation', 'presentationKey':'talk', 'presentationLogTag':'presentation', 'eventProperty':'bg=night|fade=0.5', 'actorId':'world', 'targetId':'npc', 'titleText':'title', 'contentKey':'title| second ', 'setupType':'template', 'setupKey':'setup', 'onceOnly':true, 'logTag':'log' }]"),
                ["tbncseventdate"] = JArray.Parse("[{ 'eventId':'event', 'day':0, 'timeSlot':'night', 'repeatRule':'weekly', 'priority':8 }, { 'eventId':'event', 'day':5, 'timeSlot':'morning', 'repeatRule':'once', 'priority':2 }]"),
                ["tbncsresult"] = JArray.Parse("[{ 'resultId':'result', 'eventId':'event', 'seq':2, 'resultType':'REL_ADD', 'target':'rel:npc>world', 'value':'4', 'note':'note' }]"),
                ["tbncscondition"] = JArray.Parse("[{ 'conditionId':'condition', 'ownerType':'', 'ownerId':'event', 'groupIndex':2, 'condType':'BAR_GTE', 'target':'bar:world.calendar', 'value':'3' }]"),
                ["tbncstemplate"] = JArray.Parse("[{ 'tplName':'template', 'descText':'title', 'interruptType':'none', 'atomSeq':'Dialogue', 'slotDef':'text', 'defaultSkip':true }]"),
                ["tbncssetupparams"] = JArray.Parse("[{ 'setupKey':'setup', 'tplName':'template', 'slotValues':'text=title,duration=1.5', 'checkId':'', 'skipAble':true, 'autoAdvance':true }]"),
                ["tbncschoice"] = JArray.Parse("[{ 'choiceId':'choice', 'groupKey':'group', 'textKey':'title', 'resultOwnerId':'event', 'checkId':'', 'seq':7 }]")
            };
        }

        private static JObject Bar(string owner, string id) => JObject.Parse("{ 'barId':'" + id + "', 'ownerId':'" + owner + "', 'targetId':'npc', 'barType':'world', 'min':0, 'max':9999, 'direction':'lowIsGood', 'sourceMode':'ncs', 'providerId':'', 'sourceKey':'', 'advanceMode':'derived', 'initValue':1, 'reverseInitValue':0, 'nameText':'title', 'derivedFormula':'weighted_sum', 'derivedSources':'npc.calendar|npc.calendar', 'derivedWeights':'0.25|0.75', 'autoStep':2 }");
        private static JObject BaseBar()
        {
            var bar = Bar("npc", "calendar");
            bar["advanceMode"] = "event_driven";
            bar["derivedSources"] = ""; bar["derivedWeights"] = ""; bar["derivedFormula"] = "";
            return bar;
        }
        private static NarrativeConfiguration Adapt(Dictionary<string, JArray> data) => LubanNarrativeLoader.FromTables(new cfg.Tables(name => data[name]));

        [TestCase("Once|Timer|Progress", NarrativeActionType.Daily)]
        [TestCase("OneTime", NarrativeActionType.Once)]
        [TestCase("ONCE", NarrativeActionType.Once)]
        [TestCase("EndDay", NarrativeActionType.Daily)]
        [TestCase("Repeatble", NarrativeActionType.Repeat)]
        [TestCase("Upcoming", NarrativeActionType.Future)]
        public void MapsActionDesignTypesAndLegacyTypes(string type, NarrativeActionType expected)
        {
            var data = Data();
            data["tbncsaction"].Add(new JObject { ["actionKey"]="action", ["type"]=type, ["mustExecute"]=false,
                ["nameText"]="title", ["descText"]="title", ["requirements"]="", ["resultEvents"]="", ["durationDays"]=2,
                ["progressTarget"]=type.Contains("Progress") ? 2 : 0, ["nextActionKey"]="", ["deliveryMode"]="None" });
            var action = Adapt(data).actions[0];
            Assert.That(action.type, Is.EqualTo(expected));
            Assert.That(action.endDay, Is.EqualTo(type == "EndDay"));
            if (type.Contains("Progress")) Assert.That(action.IsProgress && action.IsTimed, Is.True);
        }

        [TestCase("Repeatble|Once")]
        [TestCase("Once|OneTime")]
        [TestCase("Upcoming|Timer")]
        public void RejectsMutuallyExclusiveActionTypes(string type)
        {
            var data = Data();
            data["tbncsaction"].Add(new JObject { ["actionKey"]="action", ["type"]=type, ["mustExecute"]=false,
                ["nameText"]="title", ["descText"]="title", ["requirements"]="", ["resultEvents"]="", ["durationDays"]=2,
                ["progressTarget"]=0, ["nextActionKey"]="", ["deliveryMode"]="None" });
            Assert.Throws<FormatException>(() => Adapt(data));
        }

        [Test]
        public void ResultOnlyEventRequiresResultsAndNoPresentationPayload()
        {
            var data = Data();
            var ev = data["tbncsevent"][0];
            ev["setupType"] = ev["setupKey"] = ev["presentationClass"] = ev["presentationKey"] = ev["contentKey"] = ev["eventProperty"] = "";
            Assert.That(Adapt(data).events[0].eventId, Is.EqualTo("event"));
            data["tbncsresult"].Clear();
            Assert.Throws<FormatException>(() => Adapt(data));
        }

        [Test]
        public void IndexedPresentationDoesNotRequireLegacySetup()
        {
            var data = Data();
            data["tbncsevent"][0]["setupKey"] = "";
            data["tbncsevent"][0]["setupType"] = "";
            data["tbncsevent"][0]["presentationClass"] = "";
            var config = Adapt(data);
            Assert.That(config.events[0].presentationKey, Is.EqualTo("talk"));
            Assert.That(config.events[0].setupKey, Is.Empty);
        }

        [Test]
        public void MapsGeneratedTablesWithoutAnAuthoringAsset()
        {
            NarrativeConfiguration config = Adapt(Data());
            Assert.That(config.objects[0].objId, Is.EqualTo("world"));
            Assert.That(config.objects[0].objType, Is.EqualTo(NarrativeObjectType.World));
            Assert.That(config.objects[0].iconPath, Is.EqualTo("icon"));
            Assert.That(config.objects[0].nameText, Is.EqualTo("title"));
            Assert.That(config.objects[0].descText, Is.EqualTo("title"));
            Assert.That(config.objects[0].tags, Is.EqualTo("test"));
            Assert.That(config.objects[1].objType, Is.EqualTo(NarrativeObjectType.Npc));
            Assert.That(config.bars.ConvertAll(bar => bar.barKey), Is.EqualTo(new[] { "world.calendar", "npc.calendar" }));
            NarrativeBarDefinition bar = config.bars[0];
            Assert.That(bar.ownerId, Is.EqualTo("world")); Assert.That(bar.targetId, Is.EqualTo("npc"));
            Assert.That(bar.barType, Is.EqualTo(NarrativeBarType.World));
            Assert.That(bar.min, Is.Zero); Assert.That(bar.max, Is.EqualTo(9999)); Assert.That(bar.initValue, Is.EqualTo(1));
            Assert.That(bar.direction, Is.EqualTo(NarrativeDirection.LowIsGood));
            Assert.That(bar.advanceMode, Is.EqualTo(NarrativeAdvanceMode.Derived));
            Assert.That(bar.derivedFormula, Is.EqualTo("weighted_sum"));
            Assert.That(bar.derivedSources, Is.EqualTo(new[] { "npc.calendar", "npc.calendar" }));
            Assert.That(bar.derivedWeights, Is.EqualTo(new[] { 0.25f, 0.75f }));
            Assert.That(bar.autoStep, Is.EqualTo(2));
            NarrativeTriggerDefinition trigger = config.triggers[0];
            Assert.That(trigger.triggerId, Is.EqualTo("trigger")); Assert.That(trigger.barKey, Is.EqualTo("world.calendar"));
            Assert.That(trigger.threshold, Is.EqualTo(3)); Assert.That(trigger.priority, Is.EqualTo(9));
            Assert.That(trigger.eventId, Is.EqualTo("event")); Assert.That(trigger.reachCount, Is.EqualTo("all"));
            Assert.That(trigger.direction, Is.EqualTo(NarrativeTriggerDirection.Both));
            Assert.That(trigger.crossType, Is.EqualTo(NarrativeCrossType.Hold));
            Assert.That(trigger.onceType, Is.EqualTo(NarrativeOnceType.Repeat));
            Assert.That(trigger.resetOnExit && trigger.enabled, Is.True);
            NarrativeEventDefinition ev = config.events[0];
            Assert.That(ev.eventKind, Is.EqualTo(NarrativeEventKind.World));
            Assert.That(ev.actorId, Is.EqualTo("world")); Assert.That(ev.targetId, Is.EqualTo("npc"));
            Assert.That(ev.titleText, Is.EqualTo("title")); Assert.That(ev.contentKeys, Is.EqualTo(new[] { "title", "second" }));
            Assert.That(ev.setupType, Is.EqualTo("template")); Assert.That(ev.setupKey, Is.EqualTo("setup"));
Assert.That(ev.presentationClass, Is.EqualTo("Conversation")); Assert.That(ev.presentationKey, Is.EqualTo("talk"));
Assert.That(ev.presentationLogTag, Is.EqualTo("presentation"));
Assert.That(ev.eventProperties, Has.Count.EqualTo(2)); Assert.That(ev.eventProperties[0].property, Is.EqualTo("bg")); Assert.That(ev.eventProperties[0].value, Is.EqualTo("night"));
var slots = PresentationSlots.Parse(config.setups[0]); PresentationSlots.ApplyEventProperties(slots, ev);
Assert.That(slots["bg"], Is.EqualTo("night")); Assert.That(slots["fade"], Is.EqualTo("0.5"));
            Assert.That(ev.onceOnly, Is.True); Assert.That(ev.logTag, Is.EqualTo("log"));
            Assert.That(ev.conditionOwnerId, Is.EqualTo("event"));
            Assert.That(config.eventDates[0].rowIndex, Is.LessThan(config.eventDates[1].rowIndex));
            Assert.That(config.eventDates[0].day, Is.EqualTo(1)); Assert.That(config.eventDates[0].priority, Is.EqualTo(8));
            Assert.That(config.eventDates[0].timeSlot, Is.EqualTo(NarrativeTimeSlot.Night));
            Assert.That(config.eventDates[0].repeatRule, Is.EqualTo(NarrativeRepeatRule.Weekly));
            Assert.That(config.eventDates[1].day, Is.EqualTo(5));
            Assert.That(config.results[0].resultType, Is.EqualTo(NarrativeResultType.RelAdd));
            Assert.That(config.results[0].target, Is.EqualTo("rel:npc>world")); Assert.That(config.results[0].value, Is.EqualTo("4"));
            Assert.That(config.results[0].seq, Is.EqualTo(2)); Assert.That(config.results[0].note, Is.EqualTo("note"));
            NarrativeConditionDefinition condition = config.conditions[0];
            Assert.That(condition.ownerType, Is.EqualTo("event")); Assert.That(condition.ownerId, Is.EqualTo("event"));
            Assert.That(condition.groupIndex, Is.EqualTo(2)); Assert.That(condition.condType, Is.EqualTo("BAR_GTE"));
            Assert.That(condition.target, Is.EqualTo("bar:world.calendar")); Assert.That(condition.op, Is.Null.Or.Empty);
            Assert.That(condition.value, Is.EqualTo("3")); Assert.That(condition.notFlag, Is.False);
            Assert.That(config.templates[0].tplName, Is.EqualTo("template")); Assert.That(config.templates[0].defaultSkip, Is.True);
            Assert.That(config.templates[0].interruptType, Is.EqualTo("none")); Assert.That(config.templates[0].atomSeq, Is.EqualTo("Dialogue"));
            Assert.That(config.templates[0].slotDef, Is.EqualTo("text"));
            Assert.That(config.setups[0].skipAble && config.setups[0].autoAdvance, Is.True);
            Assert.That(PresentationSlots.Parse(config.setups[0])["duration"], Is.EqualTo("1.5"));
            Assert.That(config.setups[0].parameters, Has.Count.EqualTo(2));
            Assert.That(config.choices[0].choiceId, Is.EqualTo("choice")); Assert.That(config.choices[0].groupKey, Is.EqualTo("group"));
            Assert.That(config.choices[0].textKey, Is.EqualTo("title")); Assert.That(config.choices[0].resultOwnerId, Is.EqualTo("event"));
            Assert.That(config.choices[0].seq, Is.EqualTo(7));
            Assert.That(config.texts[0].key, Is.EqualTo("title")); Assert.That(config.texts[0].content, Is.EqualTo("Localized title"));
        }

        [TestCase("OBJ_STATE", NarrativeResultType.ObjectState)]
        [TestCase("OBJECT_STATE", NarrativeResultType.ObjectState)]
        [TestCase("AWARD_ACH", NarrativeResultType.AwardAchievement)]
        [TestCase("AWARD_ACHIEVEMENT", NarrativeResultType.AwardAchievement)]
        public void MapsDocumentedAndLegacyResultNames(string value, NarrativeResultType expected)
        {
            var data = Data(); data["tbncsresult"][0]["resultType"] = value;
            Assert.That(Adapt(data).results[0].resultType, Is.EqualTo(expected));
        }

        [TestCase("tbncsobject", "objType")]
        [TestCase("tbncsbar", "barType")]
        [TestCase("tbncsbar", "direction")]
        [TestCase("tbncsbar", "advanceMode")]
        [TestCase("tbncstrigger", "direction")]
        [TestCase("tbncstrigger", "crossType")]
        [TestCase("tbncstrigger", "onceType")]
        [TestCase("tbncsevent", "eventKind")]
        [TestCase("tbncseventdate", "timeSlot")]
        [TestCase("tbncseventdate", "repeatRule")]
        [TestCase("tbncsresult", "resultType")]
        public void RejectsUnknownEnumStrings(string table, string field)
        {
            var data = Data(); data[table][0][field] = "INVALID";
            Assert.Throws<FormatException>(() => Adapt(data));
        }

        [Test]
        public void RejectsDuplicateCompositeBarKeyAndNonFiniteWeights()
        {
            var data = Data(); data["tbncsbar"].Add(Bar("world", "calendar"));
            Assert.Throws<ArgumentException>(() => Adapt(data));
            data = Data(); data["tbncsbar"][0]["derivedWeights"] = "NaN";
            Assert.Throws<FormatException>(() => Adapt(data));
        }

        [TestCase("tbncstrigger", "barKey")]
        [TestCase("tbncstrigger", "eventId")]
        [TestCase("tbncsbar", "ownerId")]
        [TestCase("tbncsevent", "setupKey")]
        [TestCase("tbncssetupparams", "tplName")]
        [TestCase("tbncschoice", "textKey")]
        public void RejectsMissingLinks(string table, string field)
        {
            var data = Data(); data[table][0][field] = "missing";
            Assert.Throws<FormatException>(() => Adapt(data));
        }

        [TestCase("FLAG_SET")]
        [TestCase("CHANCE_PCT")]
        [TestCase("ACTOR_IS")]
        public void RejectsRemovedConditionTypes(string type)
        {
            var data = Data(); data["tbncscondition"][0]["condType"] = type;
            Assert.Throws<FormatException>(() => Adapt(data));
        }

        [Test]
        public void UnknownBoolRequiringFalseCannotPassLoading()
        {
            var data = Data(); data["tbncscondition"][0]["condType"] = "BOOL_EQ";
            data["tbncscondition"][0]["target"] = "typo"; data["tbncscondition"][0]["value"] = "false";
            var error = Assert.Throws<FormatException>(() => Adapt(data));
            Assert.That(error.Message, Does.Contain("condition").And.Contain("unknown bool key typo"));
        }

        [TestCase("ITEM_ADD", NarrativeResultType.ItemAdd)]
        [TestCase("ITEM_REMOVE", NarrativeResultType.ItemRemove)]
        public void MapsItemResultsAndRejectsNonPositiveQuantity(string type, NarrativeResultType expected)
        {
            var data = Data(); data["tbncsresult"][0]["resultType"] = type;
            data["tbncsresult"][0]["target"] = "medicine";
            Assert.That(Adapt(data).results[0].resultType, Is.EqualTo(expected));
            data["tbncsresult"][0]["value"] = "0";
            Assert.Throws<FormatException>(() => Adapt(data));
        }

        [Test]
        public void GeneratedConfigurationIncludesGatedDeliverySamplesAndDisabledEndings()
        {
            var c = LubanNarrativeLoader.Load();
            Assert.That(c.conditionKeys.Exists(x => x.key == "specialization.player.letter.science"), Is.True);
            Assert.That(c.conditions.Exists(x => x.condType == "FLAG_SET" || x.notFlag || !string.IsNullOrEmpty(x.op)), Is.False);
            foreach (var mode in new[] { NarrativeDeliveryMode.Cumulative, NarrativeDeliveryMode.Consecutive })
            {
                var action = c.actions.Find(x => x.deliveryMode == mode);
                Assert.That(action, Is.Not.Null);
                Assert.That(action.progressTarget, Is.EqualTo(5));
                Assert.That(action.milestones, Has.Count.EqualTo(1));
                Assert.That(action.milestones[0].atProgress, Is.EqualTo(2));
                Assert.That(c.conditions.Exists(x => x.ownerType == "action" && x.ownerId == action.actionKey && x.target == "test.delivery.enabled"), Is.True);
                Assert.That(c.mapPositions.Exists(x => x.defaultAction == action.actionKey || x.extendedActions.Contains(action.actionKey)), Is.False);
            }
            Assert.That(c.endings, Has.Count.EqualTo(2));
            Assert.That(c.endings.TrueForAll(x => !x.enabled), Is.True);
            Assert.That(c.conditionKeys.Find(x => x.key == "test.delivery.enabled").initialValue, Is.False);
        }

        [Test]
        public void RejectsNullTables() => Assert.Throws<ArgumentNullException>(() => LubanNarrativeLoader.FromTables(null));
    }
}
