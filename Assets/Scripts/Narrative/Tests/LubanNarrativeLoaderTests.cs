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
                ["tbncsitem"] = new JArray(),
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
        private static JObject Item(string key) => new JObject
        {
            ["itemKey"] = key, ["nameText"] = "title", ["iconPath"] = "", ["descText"] = "title",
            ["inventoryOwner"] = "MapInventory"
        };

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
                ["nameText"]="title", ["descText"]="title", ["requirements"]="", ["resultEvents"]="", ["durationDays"]=type.Equals("EndDay", StringComparison.OrdinalIgnoreCase) ? 0 : 2,
                ["replacesLocationActions"]=false, ["focusLocationOnComplete"]=false,
                ["progressTarget"]=type.Contains("Progress") ? 2 : 0, ["nextActionKey"]="", ["deliveryMode"]="None",
                ["executionKind"]="", ["navigationKey"]="", ["executeTextKey"]="", ["sortOrder"]=0,
                ["successResultOwnerId"]="", ["onExpireEvents"]="", ["onExpirePolicy"]="", ["notifyNew"]=true });
            if (expected == NarrativeActionType.Future)
            {
                data["tbncsaction"][0]["nextActionKey"] = "next";
                data["tbncsaction"].Add(Action("next"));
            }
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
                ["nameText"]="title", ["descText"]="title", ["requirements"]="", ["resultEvents"]="", ["durationDays"]=type.Equals("EndDay", StringComparison.OrdinalIgnoreCase) ? 0 : 2,
                ["replacesLocationActions"]=false, ["focusLocationOnComplete"]=false,
                ["progressTarget"]=0, ["nextActionKey"]="", ["deliveryMode"]="None",
                ["executionKind"]="", ["navigationKey"]="", ["executeTextKey"]="", ["sortOrder"]=0,
                ["successResultOwnerId"]="", ["onExpireEvents"]="", ["onExpirePolicy"]="", ["notifyNew"]=true });
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
            Assert.That(config.items, Is.Empty);
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
            data["tbncsitem"].Add(Item("medicine"));
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

        private static JObject Action(string key, string type = "Repeat") => new JObject
        {
            ["actionKey"] = key, ["type"] = type, ["mustExecute"] = false,
            ["nameText"] = "title", ["descText"] = "title", ["requirements"] = "", ["resultEvents"] = "",
            ["durationDays"] = 0, ["progressTarget"] = 0, ["nextActionKey"] = "", ["deliveryMode"] = "None",
            ["replacesLocationActions"] = false, ["focusLocationOnComplete"] = false,
            ["executionKind"] = "", ["navigationKey"] = "", ["executeTextKey"] = "", ["sortOrder"] = 0,
            ["successResultOwnerId"] = "", ["onExpireEvents"] = "", ["onExpirePolicy"] = "", ["notifyNew"] = true
        };

        [Test]
        public void AcceptsMoneyCostAndValidFreeCafeNavigation()
        {
            var data = Data();
            var paid = Action("paid"); paid["requirements"] = "Money(12)";
            var cafe = Action("cafe"); cafe["executionKind"] = "Navigate"; cafe["navigationKey"] = "cafe.enter";
            data["tbncsaction"].Add(paid); data["tbncsaction"].Add(cafe);
            var config = Adapt(data);
            Assert.That(config.actions[0].requirements, Is.EqualTo("Money(12)"));
            Assert.That(config.actions[1].navigationKey, Is.EqualTo("cafe.enter"));
        }

        [TestCase("Money(0)")]
        [TestCase("Money(2147483647);Money(1)")]
        public void RejectsInvalidOrOverflowingMoneyCosts(string requirements)
        {
            var data = Data(); var action = Action("paid"); action["requirements"] = requirements;
            data["tbncsaction"].Add(action);
            Assert.Throws<FormatException>(() => Adapt(data));
        }

        [Test]
        public void RejectsInvalidNavigationEndDayAndResultOwnerReferences()
        {
            var data = Data(); var cafe = Action("cafe"); cafe["executionKind"] = "Navigate"; cafe["navigationKey"] = "scene/path";
            data["tbncsaction"].Add(cafe);
            Assert.Throws<FormatException>(() => Adapt(data));

            data = Data(); var endDay = Action("end", "EndDay"); endDay["executionKind"] = "Event"; endDay["requirements"] = "Money(1)";
            data["tbncsaction"].Add(endDay);
            Assert.Throws<FormatException>(() => Adapt(data));

            data = Data(); var owner = Action("reward"); owner["successResultOwnerId"] = "missing";
            data["tbncsaction"].Add(owner);
            Assert.Throws<FormatException>(() => Adapt(data));
        }

        [Test]
        public void ValidatesResultOwnerCyclesAndMustExecuteReachability()
        {
            var data = Data();
            var reward = Action("reward"); reward["successResultOwnerId"] = "event";
            var first = Action("first"); first["nextActionKey"] = "second";
            var second = Action("second"); second["nextActionKey"] = "first";
            var required = Action("required"); required["mustExecute"] = true;
            data["tbncsaction"].Add(reward); data["tbncsaction"].Add(first); data["tbncsaction"].Add(second); data["tbncsaction"].Add(required);
            var error = Assert.Throws<FormatException>(() => Adapt(data));
            Assert.That(error.Message, Does.Contain("nextActionKey cycle").And.Contain("mustExecute has no enabled map position"));
        }

        [Test]
        public void ExchangeResultOwnerDoesNotRequirePlaceholderEvent()
        {
            var data = Data();
            data["tbncsitem"].Add(Item("medicine"));
            var exchange = Action("buy"); exchange["executionKind"] = "Exchange";
            exchange["requirements"] = "Money(12)"; exchange["successResultOwnerId"] = "buy.reward";
            data["tbncsaction"].Add(exchange);
            data["tbncsresult"].Add(new JObject
            {
                ["resultId"] = "buy.item", ["eventId"] = "buy.reward", ["seq"] = 1,
                ["resultType"] = "ITEM_ADD", ["target"] = "medicine", ["value"] = "2", ["note"] = ""
            });
            var config = Adapt(data);
            Assert.That(config.events.Exists(x => x.eventId == "buy.reward"), Is.False);
            Assert.That(LubanNarrativeLoader.ValidateActionConfiguration(config), Is.Empty);
            Assert.That(LubanNarrativeLoader.IsKnownResultOwner(config, "buy.reward"), Is.True);
        }

        [TestCase("missingOwner")]
        [TestCase("emptyOwner")]
        [TestCase("eventChain")]
        [TestCase("unsupportedResult")]
        public void RejectsUnsupportedExchangeConfiguration(string invalid)
        {
            var data = Data();
            data["tbncsitem"].Add(Item("medicine"));
            var exchange = Action("buy"); exchange["executionKind"] = "Exchange";
            exchange["successResultOwnerId"] = "buy.reward";
            data["tbncsaction"].Add(exchange);
            if (invalid != "emptyOwner") data["tbncsresult"].Add(new JObject
            {
                ["resultId"] = "buy.result", ["eventId"] = "buy.reward", ["seq"] = 1,
                ["resultType"] = invalid == "unsupportedResult" ? "REL_ADD" : "ITEM_ADD",
                ["target"] = "medicine", ["value"] = "2", ["note"] = ""
            });
            if (invalid == "missingOwner") exchange["successResultOwnerId"] = "";
            if (invalid == "eventChain") exchange["resultEvents"] = "event";
            var error = Assert.Throws<FormatException>(() => Adapt(data));
            Assert.That(error.Message, Does.Contain(invalid == "emptyOwner" ? "has no Result rows" : "Exchange"));
        }

        [Test]
        public void RejectsExpiryEventUsingMapActionEntry()
        {
            var data = Data();
            data["tbncseventdate"].Clear();
            data["tbncsevent"][0]["eventProperty"] = "entry=MapAction";
            var timed = Action("timed", "TIMED"); timed["durationDays"] = 2;
            timed["onExpirePolicy"] = "RunEvents"; timed["onExpireEvents"] = "event";
            data["tbncsaction"].Add(timed);
            var error = Assert.Throws<FormatException>(() => Adapt(data));
            Assert.That(error.Message, Does.Contain("expiry event cannot use MapAction entry"));
        }

        [Test]
        public void MapsItemCatalogMetadataWithOptionalIcon()
        {
            var data = Data();
            data["tbtext"].Add(new JObject { ["key"] = "item.description", ["text"] = "Item description" });
            var medicine = Item("medicine");
            medicine["iconPath"] = "Icons/medicine";
            medicine["descText"] = "item.description";
            data["tbncsitem"].Add(medicine);
            data["tbncsitem"].Add(Item("tea"));

            var config = Adapt(data);
            Assert.That(config.items, Has.Count.EqualTo(2));
            var item = config.items[0];
            Assert.That(item.itemKey, Is.EqualTo("medicine"));
            Assert.That(item.nameText, Is.EqualTo("title"));
            Assert.That(item.iconPath, Is.EqualTo("Icons/medicine"));
            Assert.That(item.descText, Is.EqualTo("item.description"));
            Assert.That(item.inventoryOwner, Is.EqualTo(NarrativeInventoryOwner.MapInventory));
            Assert.That(config.items[1].iconPath, Is.Empty);
        }

        [TestCase("nameText", "missing")]
        [TestCase("nameText", "")]
        [TestCase("descText", "missing")]
        [TestCase("descText", "")]
        public void RejectsItemMetadataWithoutValidText(string field, string textKey)
        {
            var data = Data();
            var item = Item("medicine"); item[field] = textKey;
            data["tbncsitem"].Add(item);
            Assert.Throws<FormatException>(() => Adapt(data));
        }

        [Test]
        public void RejectsUnsupportedCafeSupplyItemOwner()
        {
            var data = Data();
            var item = Item("medicine"); item["inventoryOwner"] = "CafeSupply";
            data["tbncsitem"].Add(item);
            var error = Assert.Throws<FormatException>(() => Adapt(data));
            Assert.That(error.Message, Does.Contain("medicine").And.Contain("CafeSupply"));
        }

        [TestCase("cost", "missing")]
        [TestCase("ITEM_ADD", "missing")]
        [TestCase("ITEM_ADD", "item:missing")]
        [TestCase("ITEM_REMOVE", "missing")]
        [TestCase("ITEM_REMOVE", "item:missing")]
        [TestCase("ITEM_GTE", "missing")]
        [TestCase("ITEM_GTE", "item:missing")]
        public void RejectsUnknownItemReferences(string source, string target)
        {
            var data = Data();
            data["tbncsitem"].Add(Item("medicine"));
            if (source == "cost")
            {
                var action = Action("paid"); action["requirements"] = "Item(" + target + ",1)";
                data["tbncsaction"].Add(action);
            }
            else if (source == "ITEM_GTE")
            {
                data["tbncscondition"][0]["condType"] = source;
                data["tbncscondition"][0]["target"] = target;
                data["tbncscondition"][0]["value"] = "1";
            }
            else
            {
                data["tbncsresult"][0]["resultType"] = source;
                data["tbncsresult"][0]["target"] = target;
                data["tbncsresult"][0]["value"] = "1";
            }
            var error = Assert.Throws<FormatException>(() => Adapt(data));
            Assert.That(error.Message, Does.Contain("missing").And.Contain("item"));
        }

        [TestCase("Item(medicine,1)", "ITEM_REMOVE", "medicine")]
        [TestCase("Item(medicine,1)", "ITEM_REMOVE", "item:medicine")]
        [TestCase("Item(medicine,1)", "ITEM_REMOVE", "ITEM:medicine")]
        [TestCase("Money(12)", "MONEY_REMOVE", "cafe")]
        public void RejectsRequirementAndResultChargingTheSameCost(string requirements, string resultType, string target)
        {
            var data = Data();
            data["tbncsitem"].Add(Item("medicine"));
            var action = Action("paid"); action["requirements"] = requirements;
            action["resultEvents"] = "event";
            data["tbncsaction"].Add(action);
            data["tbncsresult"][0]["resultType"] = resultType;
            data["tbncsresult"][0]["target"] = target;
            data["tbncsresult"][0]["value"] = "1";
            var error = Assert.Throws<FormatException>(() => Adapt(data));
            Assert.That(error.Message, Does.Contain("duplicates a requirements cost"));
        }

        [Test]
        public void FiveMergedCostsAndOrOfAndEligibilityUseSixDisplayedItems()
        {
            var data = Data();
            foreach (string key in new[] { "medicine", "tea", "food", "key" })
                data["tbncsitem"].Add(Item(key));
            var action = Action("paid");
            action["requirements"] = "Item(medicine,1);Item(medicine,2);Item(tea,1);Item(food,1);Money(12);Money(3);ActionPoint(1)";
            data["tbncsaction"].Add(action);
            foreach (int group in new[] { 1, 2 })
            {
                data["tbncscondition"].Add(new JObject
                {
                    ["conditionId"] = "eligible.bar." + group, ["ownerType"] = "actionExecute", ["ownerId"] = "paid",
                    ["groupIndex"] = group, ["condType"] = "BAR_GTE", ["target"] = "bar:world.calendar", ["value"] = group.ToString()
                });
                data["tbncscondition"].Add(new JObject
                {
                    ["conditionId"] = "eligible.item." + group, ["ownerType"] = "actionExecute", ["ownerId"] = "paid",
                    ["groupIndex"] = group, ["condType"] = "ITEM_GTE", ["target"] = "item:medicine", ["value"] = group.ToString()
                });
            }

            var config = Adapt(data);
            Assert.That(LubanNarrativeLoader.ValidateActionConfiguration(config), Is.Empty);
            Assert.That(config.conditions.FindAll(x => x.ownerType == "actionExecute" && x.groupIndex == 1), Has.Count.EqualTo(2));
            Assert.That(config.conditions.FindAll(x => x.ownerType == "actionExecute" && x.groupIndex == 2), Has.Count.EqualTo(2));

            action["requirements"] = action.Value<string>("requirements") + ";Item(key,1)";
            var error = Assert.Throws<FormatException>(() => Adapt(data));
            Assert.That(error.Message, Does.Contain("exceed 6 displayed items"));
        }

        [Test]
        public void RejectsNullTables() => Assert.Throws<ArgumentNullException>(() => LubanNarrativeLoader.FromTables(null));
    }
}
