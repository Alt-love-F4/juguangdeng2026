using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Spotlight.Narrative
{
    public static class LubanNarrativeLoader
    {
        public static NarrativeConfiguration Load()
        {
            return FromTables(new cfg.Tables(name =>
            {
                TextAsset asset = Resources.Load<TextAsset>("Luban/" + name);
                if (asset == null) throw new FileNotFoundException("Missing Luban table: Resources/Luban/" + name + ".json");
                return JArray.Parse(asset.text);
            }));
        }

        public static NarrativeConfiguration FromTables(cfg.Tables tables)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            var config = new NarrativeConfiguration();
            foreach (var row in tables.TbNcsObject.DataList)
                config.objects.Add(new NarrativeObjectDefinition
                {
                    objId = row.ObjId, objType = ObjectType(row.ObjType), nameText = row.NameText,
                    iconPath = row.IconPath, portraitPath = row.PortraitPath,
                    portraitIndoorPath = row.PortraitIndoorPath, avtarPath = row.AvtarPath,
                    descText = row.DescText, tags = row.Tags
                });
            foreach (var row in tables.TbNcsAction.DataList)
                config.actions.Add(new NarrativeActionDefinition
                {
                    actionKey = row.ActionKey, type = ActionType(row.Type), mustExecute = row.MustExecute,
                    nameText = row.NameText, descText = row.DescText, requirements = row.Requirements,
                    resultEvents = Strings(row.ResultEvents), durationDays = row.DurationDays,
                    progressTarget = row.ProgressTarget, nextActionKey = row.NextActionKey,
                    endDay = row.Type.Split(new[] { '|', '+', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Any(p => p.Trim().Equals("EndDay", StringComparison.OrdinalIgnoreCase))
                });
            foreach (var row in tables.TbNcsAction.DataList)
            {
                string[] parts = row.Type.Split(new[] { '|', '+', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Any(p => p.Equals("Timer", StringComparison.OrdinalIgnoreCase) || p.Equals("Timed", StringComparison.OrdinalIgnoreCase)) && row.DurationDays <= 0)
                    throw new FormatException("Timer requires positive durationDays: " + row.ActionKey);
                if (parts.Any(p => p.Equals("Progress", StringComparison.OrdinalIgnoreCase)) && row.ProgressTarget <= 0)
                    throw new FormatException("Progress requires positive progressTarget: " + row.ActionKey);
            }
            foreach (var row in tables.TbNcsMapPosition.DataList)
                config.mapPositions.Add(new NarrativeMapPositionDefinition
                {
                    positionKey = row.PositionKey, nameText = row.NameText,
                    position = new UnityEngine.Vector2(row.PosX, row.PosY), isImportant = row.IsImportant,
                    defaultAction = row.DefaultAction, extendedActions = Strings(row.ExtendedActions),
                    imgPath = row.ImgPath, enabled = row.Enabled
                });
            var barKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in tables.TbNcsBar.DataList)
            {
                string key = row.OwnerId + "." + row.BarId;
                if (string.IsNullOrWhiteSpace(row.OwnerId) || string.IsNullOrWhiteSpace(row.BarId) || !barKeys.Add(key))
                    throw new FormatException("Invalid or duplicate narrative bar key: " + key);
                config.bars.Add(new NarrativeBarDefinition
                {
                    barKey = key, ownerId = row.OwnerId, targetId = row.TargetId, barType = BarType(row.BarType),
                    min = row.Min, max = row.Max, direction = Direction(row.Direction), advanceMode = AdvanceMode(row.AdvanceMode),
                    initValue = row.InitValue, reverseInitValue = row.ReverseInitValue, nameText = row.NameText, derivedFormula = row.DerivedFormula,
                    derivedSources = Strings(row.DerivedSources), derivedWeights = Weights(row.DerivedWeights), autoStep = row.AutoStep
                });
            }
            foreach (var row in tables.TbNcsTrigger.DataList)
                config.triggers.Add(new NarrativeTriggerDefinition
                {
                    triggerId = row.TriggerId, barKey = row.BarKey, threshold = row.Threshold,
                    direction = TriggerDirection(row.Direction), crossType = CrossType(row.CrossType), onceType = OnceType(row.OnceType),
                    reachCount = row.ReachCount, resetOnExit = row.ResetOnExit, eventId = row.EventId,
                    priority = row.Priority, enabled = row.Enabled
                });
            foreach (var row in tables.TbNcsEvent.DataList)
                config.events.Add(new NarrativeEventDefinition
                {
                    eventId = row.EventId, eventKind = EventKind(row.EventKind), actorId = row.ActorId, targetId = row.TargetId,
                    titleText = row.TitleText, contentKeys = Strings(row.ContentKey), presentationClass = row.PresentationClass,
                    presentationKey = row.PresentationKey, presentationLogTag = row.PresentationLogTag,
                    setupType = row.SetupType, setupKey = row.SetupKey, onceOnly = row.OnceOnly, logTag = row.LogTag, conditionOwnerId = row.EventId,
                    eventProperties = ParseEventProperties(row.EventProperty), eventKindRaw = row.EventKind
                });
            int dateIndex = 0;
            foreach (var row in tables.TbNcsEventDate.DataList)
            {
                NarrativeRepeatRule repeat = RepeatRule(row.RepeatRule);
                config.eventDates.Add(new NarrativeEventDateDefinition
                {
                    eventId = row.EventId, day = row.Day == 0 && (repeat == NarrativeRepeatRule.Daily || repeat == NarrativeRepeatRule.Weekly) ? 1 : row.Day,
                    timeSlot = TimeSlot(row.TimeSlot), repeatRule = repeat, priority = row.Priority, rowIndex = dateIndex++
                });
            }
            foreach (var row in tables.TbNcsCondition.DataList)
                config.conditions.Add(new NarrativeConditionDefinition
                {
                    conditionId = row.ConditionId, ownerType = string.IsNullOrWhiteSpace(row.OwnerType) ? "event" : row.OwnerType,
                    ownerId = row.OwnerId, groupIndex = row.GroupIndex, condType = row.CondType, target = row.Target,
                    op = row.Op, value = row.Value, notFlag = row.NotFlag
                });
            foreach (var row in tables.TbNcsResult.DataList)
                config.results.Add(new NarrativeResultDefinition
                {
                    resultId = row.ResultId, eventId = row.EventId, seq = row.Seq, resultType = ResultType(row.ResultType),
                    target = row.Target, value = row.Value, note = row.Note
                });
            foreach (var row in tables.TbNcsTemplate.DataList)
                config.templates.Add(new NarrativeTemplateDefinition
                {
                    tplName = row.TplName, descText = row.DescText, interruptType = row.InterruptType,
                    atomSeq = row.AtomSeq, slotDef = row.SlotDef, defaultSkip = row.DefaultSkip
                });
            foreach (var row in tables.TbNcsSetupParams.DataList)
            {
                var setup = new NarrativeSetupParamsDefinition
                {
                    setupKey = row.SetupKey, tplName = row.TplName, slotValues = row.SlotValues,
                    skipAble = row.SkipAble, autoAdvance = row.AutoAdvance
                };
                foreach (var slot in PresentationSlots.Parse(setup))
                    setup.parameters.Add(new NarrativeAtomParameter { slot = slot.Key, value = slot.Value });
                config.setups.Add(setup);
            }
            foreach (var row in tables.TbNcsChoice.DataList)
                config.choices.Add(new NarrativeChoiceDefinition
                {
                    choiceId = row.ChoiceId, groupKey = row.GroupKey, textKey = row.TextKey,
                    resultOwnerId = row.ResultOwnerId, seq = row.Seq
                });
            foreach (var row in tables.TbText.DataList)
                config.texts.Add(new NarrativeTextDefinition { key = row.Key, content = row.Text });
            foreach (var row in tables.TbOpeningPresentation.DataList)
                config.openingPresentations.Add(new NarrativeOpeningPresentationDefinition { presentationId = row.PresentationId, titleKey = row.TitleKey, leftNameKey = row.LeftNameKey, rightNameKey = row.RightNameKey, visualsResource = row.VisualsResource });
            foreach (var row in tables.TbOpeningLine.DataList)
                config.openingLines.Add(new NarrativeOpeningLineDefinition { lineId = row.LineId, presentationId = row.PresentationId, seq = row.Seq, speaker = row.Speaker, textKey = row.TextKey, gesture = row.Gesture, autoAdvanceSeconds = row.AutoAdvanceSeconds });
            foreach (var row in tables.TbNcsPresentationIndex.DataList)
                config.presentations.index.Add(new NarrativePresentationIndexDefinition { presentationKey = row.PresentationKey, presentationClass = row.PresentationClass, logTag = row.LogTag });
            foreach (var row in tables.TbNcsConversationPage.DataList)
                config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = row.PageId, presentationKey = row.PresentationKey, seq = row.Seq, pageCondKeys = row.PageCondKeys, pageBarConds = row.PageBarConds, optionGroupKey = row.OptionGroupKey, bgOverride = row.BgOverride, bgmOverride = row.BgmOverride });
            foreach (var row in tables.TbNcsConversationLine.DataList)
                config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition { lineId = row.LineId, pageId = row.PageId, seq = row.Seq, speakerKey = row.SpeakerKey, side = row.Side, textKey = row.TextKey, voiceKey = row.VoiceKey, emote = row.Emote, sfxKey = row.SfxKey, accentFxKey = row.AccentFxKey, accentTargetKey = row.AccentTargetKey });
            foreach (var row in tables.TbNcsNotice.DataList)
                config.presentations.notices.Add(new NarrativeNoticeDefinition { presentationKey = row.PresentationKey, titleKey = row.TitleKey, timeKey = row.TimeKey, imageKey = row.ImageKey, textKey = row.TextKey, barConds = row.BarConds, noticeBarConds = row.NoticeBarConds });
            foreach (var row in tables.TbNcsReadingPage.DataList)
                config.presentations.readingPages.Add(new NarrativeReadingPageDefinition { pageId = row.PageId, presentationKey = row.PresentationKey, seq = row.Seq, titleKey = row.TitleKey, imageKey = row.ImageKey, pageCondKeys = row.PageCondKeys, pageBarConds = row.PageBarConds });
            foreach (var row in tables.TbNcsReadingLine.DataList)
                config.presentations.readingLines.Add(new NarrativeReadingLineDefinition { lineId = row.LineId, pageId = row.PageId, seq = row.Seq, mode = row.Mode, speakerKey = row.SpeakerKey, textKey = row.TextKey, voiceKey = row.VoiceKey });
            ValidateLinks(config);
            return config;
        }

        private static void ValidateLinks(NarrativeConfiguration config)
        {
            var objects = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in config.objects) AddId(objects, item.objId, "object");
            var bars = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in config.bars)
            {
                AddId(bars, item.barKey, "bar");
                Require(objects, item.ownerId, "bar owner");
                if (item.min > item.max || item.initValue < item.min || item.initValue > item.max)
                    throw new FormatException("Invalid narrative bar range or initial value: " + item.barKey);
                if (item.advanceMode == NarrativeAdvanceMode.Derived && item.derivedWeights.Count > 0 && item.derivedWeights.Count != item.derivedSources.Count)
                    throw new FormatException("Narrative derived weights do not match sources: " + item.barKey);
            }
            var events = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in config.events) AddId(events, item.eventId, "event");
            var templates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in config.templates) AddId(templates, item.tplName, "template");
            var setups = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in config.setups)
            {
                AddId(setups, item.setupKey, "setup");
                Require(templates, item.tplName, "setup template");
            }
            foreach (var item in config.bars)
                foreach (string source in item.derivedSources) Require(bars, source, "derived source");
            foreach (var item in config.events)
            {
                if (!string.IsNullOrWhiteSpace(item.setupType)) Require(templates, item.setupType, "event template");
                if (!string.IsNullOrWhiteSpace(item.setupKey)) Require(setups, item.setupKey, "event setup");
                string resolvedPresentationClass = item.presentationClass;
                if (!string.IsNullOrWhiteSpace(item.presentationKey))
                {
                    NarrativePresentationIndexDefinition presentation = config.presentations.index.Find(candidate => candidate.presentationKey == item.presentationKey);
                    if (presentation == null) throw new FormatException("Missing narrative presentation index: " + item.presentationKey);
                    resolvedPresentationClass = presentation.presentationClass;
                }
                if (string.IsNullOrWhiteSpace(item.setupType) && string.IsNullOrWhiteSpace(resolvedPresentationClass))
                    throw new FormatException("Narrative event has no presentation type: " + item.eventId);
            }
            foreach (var item in config.triggers)
            {
                Require(events, item.eventId, "trigger event");
                Require(bars, item.barKey, "trigger bar");
                NarrativeBarDefinition bar = config.bars.Find(candidate => candidate.barKey == item.barKey);
                if (item.threshold < bar.min || item.threshold > bar.max) throw new FormatException("Narrative trigger threshold is out of range: " + item.triggerId);
            }
            foreach (var item in config.eventDates) Require(events, item.eventId, "date event");
            var texts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in config.texts) AddId(texts, item.key, "text");
            foreach (var item in config.choices) Require(texts, item.textKey, "choice text");
            var presentations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in config.openingPresentations)
            {
                AddId(presentations, item.presentationId, "opening presentation");
                Require(texts, item.titleKey, "opening title");
                Require(texts, item.leftNameKey, "opening left character name");
                Require(texts, item.rightNameKey, "opening right character name");
            }
            var openingLineIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in config.openingLines)
            {
                AddId(openingLineIds, item.lineId, "opening line");
                Require(presentations, item.presentationId, "opening presentation");
                Require(texts, item.textKey, "opening line text");
                if (item.seq < 1 || !float.IsFinite(item.autoAdvanceSeconds) || item.autoAdvanceSeconds < 0)
                    throw new FormatException("Invalid opening line sequence or auto advance: " + item.lineId);
                if (item.speaker != "narrator" && item.speaker != "left" && item.speaker != "right") throw Unknown("opening speaker", item.speaker);
                if (item.gesture != "none" && item.gesture != "nod" && item.gesture != "shake") throw Unknown("opening gesture", item.gesture);
            }
            foreach (string presentationId in presentations)
                if (!config.openingLines.Any(item => item.presentationId == presentationId)) throw new FormatException("Opening presentation has no lines: " + presentationId);
        }

        private static void AddId(HashSet<string> ids, string id, string field)
        {
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new FormatException("Invalid or duplicate narrative " + field + " ID: " + id);
        }

        private static void Require(HashSet<string> ids, string id, string field)
        {
            if (string.IsNullOrWhiteSpace(id) || !ids.Contains(id)) throw new FormatException("Missing narrative " + field + ": " + id);
        }

        private static List<string> Strings(string value)
        {
            var result = new List<string>();
            foreach (string part in (value ?? string.Empty).Split(new[] { '|', ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                if (!string.IsNullOrWhiteSpace(part)) result.Add(part.Trim());
            return result;
        }

        private static List<NarrativeEventProperty> ParseEventProperties(string value)
        {
            var properties = new List<NarrativeEventProperty>();
            foreach (string entry in (value ?? string.Empty).Split(new[] { '|', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = entry.IndexOf('=');
                if (separator <= 0) throw new FormatException("Invalid eventProperty entry: " + entry);
                properties.Add(new NarrativeEventProperty { property = entry.Substring(0, separator).Trim(), value = entry.Substring(separator + 1).Trim() });
            }
            return properties;
        }

        private static List<float> Weights(string value)
        {
            var result = new List<float>();
            foreach (string part in Strings(value))
            {
                float weight = float.Parse(part, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (float.IsNaN(weight) || float.IsInfinity(weight)) throw new FormatException("Non-finite narrative derived weight: " + part);
                result.Add(weight);
            }
            return result;
        }

        private static FormatException Unknown(string field, string value) => new FormatException("Unknown narrative " + field + ": " + value);
        private static NarrativeObjectType ObjectType(string value) => value switch
        {
            "npc" => NarrativeObjectType.Npc, "player" => NarrativeObjectType.Player, "world" => NarrativeObjectType.World,
            _ => throw Unknown("objType", value)
        };
        private static NarrativeBarType BarType(string value) => value switch
        {
            "stat" => NarrativeBarType.Stat, "relation" => NarrativeBarType.Relation, "world" => NarrativeBarType.World,
            _ => throw Unknown("barType", value)
        };
        private static NarrativeDirection Direction(string value) => value switch
        {
            "highIsGood" => NarrativeDirection.HighIsGood, "lowIsGood" => NarrativeDirection.LowIsGood,
            _ => throw Unknown("direction", value)
        };
        private static NarrativeAdvanceMode AdvanceMode(string value) => value switch
        {
            "monotonic" => NarrativeAdvanceMode.Monotonic, "event_driven" => NarrativeAdvanceMode.EventDriven,
            "derived" => NarrativeAdvanceMode.Derived, _ => throw Unknown("advanceMode", value)
        };
        private static NarrativeTriggerDirection TriggerDirection(string value) => value switch
        {
            "up" => NarrativeTriggerDirection.Up, "down" => NarrativeTriggerDirection.Down, "both" => NarrativeTriggerDirection.Both,
            _ => throw Unknown("trigger direction", value)
        };
        private static NarrativeCrossType CrossType(string value) => value switch
        {
            "cross" => NarrativeCrossType.Cross, "hold" => NarrativeCrossType.Hold, _ => throw Unknown("crossType", value)
        };
        private static NarrativeOnceType OnceType(string value) => value switch
        {
            "once" => NarrativeOnceType.Once, "repeat" => NarrativeOnceType.Repeat, _ => throw Unknown("onceType", value)
        };
        private static NarrativeActionType ActionType(string value)
        {
            string[] parts = (value ?? string.Empty).Split(new[] { '|', '+', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                var types = parts.Select(p => ActionType(p.Trim())).ToList();
                if (types.Distinct().Count() != types.Count || types.Contains(NarrativeActionType.Future)) throw Unknown("mutually exclusive action types", value);
                var bases = types.Where(t => t == NarrativeActionType.Repeat || t == NarrativeActionType.Daily || t == NarrativeActionType.Once).ToList();
                if (bases.Count > 1) throw Unknown("mutually exclusive action types", value);
                return bases.Count == 1 ? bases[0] : types.Contains(NarrativeActionType.Progress) ? NarrativeActionType.Progress : NarrativeActionType.Timed;
            }
            // Preserve legacy uppercase ONCE (one-time); design spelling Once means daily.
            if (value == "ONCE") return NarrativeActionType.Once;
            return (value ?? string.Empty).ToUpperInvariant() switch
            {
                "REPEAT" or "REPEATBLE" or "REPEATABLE" => NarrativeActionType.Repeat,
                "DAILY" or "ENDDAY" or "ONCE" => NarrativeActionType.Daily,
                "ONETIME" => NarrativeActionType.Once,
                "TIMED" or "TIMER" => NarrativeActionType.Timed,
                "PROGRESS" => NarrativeActionType.Progress, "FUTURE" or "UPCOMING" => NarrativeActionType.Future,
                _ => throw Unknown("action type", value)
            };
        }
        private static NarrativeEventKind EventKind(string value) => value switch
        {
            "actor" => NarrativeEventKind.Actor, "world" => NarrativeEventKind.World, _ => throw Unknown("eventKind", value)
        };
        private static NarrativeTimeSlot TimeSlot(string value) => value switch
        {
            "morning" => NarrativeTimeSlot.Morning, "noon" => NarrativeTimeSlot.Noon, "night" => NarrativeTimeSlot.Night,
            _ => throw Unknown("timeSlot", value)
        };
        private static NarrativeRepeatRule RepeatRule(string value) => value switch
        {
            "once" => NarrativeRepeatRule.Once, "daily" => NarrativeRepeatRule.Daily, "weekly" => NarrativeRepeatRule.Weekly,
            "yearly" => NarrativeRepeatRule.Yearly, _ => throw Unknown("repeatRule", value)
        };
        private static NarrativeResultType ResultType(string value) => value switch
        {
            "BAR_ADD" => NarrativeResultType.BarAdd, "BAR_SET" => NarrativeResultType.BarSet,
            "REL_ADD" => NarrativeResultType.RelAdd, "SET_FLAG" => NarrativeResultType.SetFlag,
            "CLEAR_FLAG" => NarrativeResultType.ClearFlag, "SET_SWITCH" => NarrativeResultType.SetSwitch,
            "OBJ_STATE" => NarrativeResultType.ObjectState, "OBJECT_STATE" => NarrativeResultType.ObjectState, "UNLOCK_EVENT" => NarrativeResultType.UnlockEvent,
            "LOCK_EVENT" => NarrativeResultType.LockEvent, "ADVANCE_DAY" => NarrativeResultType.AdvanceDay,
            "AWARD_ACH" => NarrativeResultType.AwardAchievement, "AWARD_ACHIEVEMENT" => NarrativeResultType.AwardAchievement, "PLAY_SETUP" => NarrativeResultType.PlaySetup,
            "BAR_HINT" => NarrativeResultType.BarHint, "SPAWN_NPC" => NarrativeResultType.SpawnNpc,
            "DESPAWN_NPC" => NarrativeResultType.DespawnNpc,
            "SHOWPOSITION" or "SHOW_POSITION" => NarrativeResultType.ShowPosition,
            "HIDEPOSITION" or "HIDE_POSITION" => NarrativeResultType.HidePosition,
            "ADDTEMPPOSITION" or "ADD_TEMP_POSITION" => NarrativeResultType.AddTempPosition,
            "REMOVETEMPPOSITION" or "REMOVE_TEMP_POSITION" => NarrativeResultType.RemoveTempPosition,
            _ => throw Unknown("resultType", value)
        };
    }
}
