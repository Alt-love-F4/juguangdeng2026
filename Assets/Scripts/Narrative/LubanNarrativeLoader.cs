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
                    progressTarget = row.ProgressTarget, nextActionKey = row.NextActionKey, deliveryMode = DeliveryMode(row.DeliveryMode),
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
            foreach (var row in tables.TbNcsActionMilestone.DataList)
            {
                var action = config.actions.Find(x => x.actionKey == row.ActionKey);
                if (action == null) throw new FormatException("ActionMilestone " + row.MilestoneId + ": unknown action " + row.ActionKey);
                action.milestones.Add(new NarrativeActionMilestoneDefinition
                {
                    milestoneId = row.MilestoneId, actionKey = row.ActionKey, atProgress = row.AtProgress, eventId = row.EventId, seq = row.Seq
                });
            }
            foreach (var row in tables.TbNcsConditionKey.DataList)
                config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = row.Key, nameText = row.NameText, initialValue = row.InitialValue, onlySetTrue = row.OnlySetTrue });
            foreach (var row in tables.TbNcsEnding.DataList)
                config.endings.Add(new NarrativeEndingDefinition { endingId = row.EndingId, enabled = row.Enabled, priority = row.Priority, titleText = row.TitleText, messageText = row.MessageText });
            foreach (var row in tables.TbNcsMapPosition.DataList)
                config.mapPositions.Add(new NarrativeMapPositionDefinition
                {
                    positionKey = row.PositionKey, nameText = row.NameText,
                    position = new UnityEngine.Vector2(row.PosX, row.PosY), isImportant = row.IsImportant,
                    defaultAction = row.DefaultAction, extendedActions = Strings(row.ExtendedActions),
                    imgPath = row.ImgPath, enabled = row.Enabled
                });
            foreach (var row in tables.TbNcsCafeVisit.DataList)
                config.cafeVisits.Add(new NarrativeCafeVisitDefinition
                {
                    visitKey = row.VisitKey, characterId = row.CharacterId,
                    preferredSeatId = row.PreferredSeatId, seatedVisualPath = row.SeatedVisualPath,
                    arrivalDelaySeconds = CafeSeconds(row.ArrivalDelaySeconds, row.VisitKey, "arrivalDelaySeconds", false) ?? 0f,
                    patienceSeconds = CafeSeconds(row.PatienceSeconds, row.VisitKey, "patienceSeconds", true),
                    leaveAfterServed = CafeLeaveAfterServed(row.LeaveAfterServed, row.VisitKey)
                });
            foreach (var row in tables.TbNcsCafeVisitHook.DataList)
                config.cafeVisitHooks.Add(new NarrativeCafeVisitHookDefinition
                { visitKey = row.VisitKey, stage = row.Stage, trigger = row.Trigger, seq = row.Seq, eventId = row.EventId });
            if (config.cafeVisits.Count == 0) UnityEngine.Debug.LogWarning("[CafeVisit/default] 特殊来访配置为空；有效角色到场使用默认行为。");
            if (config.cafeVisitHooks.Count == 0) UnityEngine.Debug.LogWarning("[CafeVisit/no_content] 阶段剧情未配置；演出功能未验证。");
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
                    sourceMode = SourceMode(row.SourceMode), providerId = row.ProviderId, sourceKey = row.SourceKey,
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
                    value = row.Value
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
                    setupKey = row.SetupKey, tplName = row.TplName, slotValues = row.SlotValues, checkId = row.CheckId,
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
                    resultOwnerId = row.ResultOwnerId, seq = row.Seq, checkId = row.CheckId
                });
            foreach (var row in tables.TbNcsAbilityCheck.DataList)
                config.abilityChecks.Add(new NarrativeAbilityCheckDefinition
                {
                    checkId = row.CheckId, successEventId = row.SuccessEventId, failureEventId = row.FailureEventId,
                    successResultOwnerId = row.SuccessResultOwnerId, failureResultOwnerId = row.FailureResultOwnerId
                });
            foreach (var row in tables.TbNcsAbilityCheckCondition.DataList)
                config.abilityCheckConditions.Add(new NarrativeAbilityCheckConditionDefinition
                {
                    conditionId = row.ConditionId, checkId = row.CheckId, attributeId = row.AttributeId,
                    threshold = row.Threshold, groupIndex = row.GroupIndex, seq = row.Seq
                });
            foreach (var row in tables.TbText.DataList)
                config.texts.Add(new NarrativeTextDefinition { key = row.Key, content = row.Text });
            foreach (var row in tables.TbCafeTutorialStep.DataList)
                config.cafeTutorialSteps.Add(new CafeTutorialStepDefinition { tutorialId = row.TutorialId, stepId = row.StepId,
                    presentationId = row.PresentationId, focusTargetKey = row.FocusTargetKey,
                    completionSignalId = row.CompletionSignalId, nextStepId = row.NextStepId, optionalDemo = row.OptionalDemo });
            foreach (var row in tables.TbOpeningPresentation.DataList)
                config.openingPresentations.Add(new NarrativeOpeningPresentationDefinition { presentationId = row.PresentationId, titleKey = row.TitleKey, leftNameKey = row.LeftNameKey, rightNameKey = row.RightNameKey, visualsResource = row.VisualsResource, centerNameKey = row.CenterNameKey });
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
            List<string> errors = ValidateConditionConfiguration(config);
            errors.AddRange(ValidateAbilityCheckConfiguration(config, tables));
            if (errors.Count > 0) throw new FormatException(string.Join("\n", errors));
            return config;
        }

        public static List<string> ValidateAbilityCheckConfiguration(NarrativeConfiguration c, cfg.Tables tables)
        {
            var errors = new List<string>();
            var checks = new Dictionary<string, NarrativeAbilityCheckDefinition>(StringComparer.Ordinal);
            foreach (var check in c.abilityChecks)
            {
                if (check == null || string.IsNullOrWhiteSpace(check.checkId) || !checks.TryAdd(check.checkId, check)) { errors.Add("AbilityCheck: missing or duplicate checkId " + check?.checkId); continue; }
                if (!c.events.Any(e => e != null && e.eventId == check.successEventId)) errors.Add("AbilityCheck " + check.checkId + ": unknown successEventId " + check.successEventId);
                if (!c.events.Any(e => e != null && e.eventId == check.failureEventId)) errors.Add("AbilityCheck " + check.checkId + ": unknown failureEventId " + check.failureEventId);
            }
            var conditionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var condition in c.abilityCheckConditions)
            {
                if (condition == null || string.IsNullOrWhiteSpace(condition.conditionId) || !conditionIds.Add(condition.conditionId)) { errors.Add("AbilityCheckCondition: missing or duplicate conditionId " + condition?.conditionId); continue; }
                if (!checks.ContainsKey(condition.checkId)) errors.Add("AbilityCheckCondition " + condition.conditionId + ": unknown checkId " + condition.checkId);
                var attribute = tables.TbPlayerAttribute.GetOrDefault(condition.attributeId);
                if (attribute == null) errors.Add("AbilityCheckCondition " + condition.conditionId + ": unknown attributeId " + condition.attributeId);
                if (condition.groupIndex < 1) errors.Add("AbilityCheckCondition " + condition.conditionId + ": groupIndex must be positive.");
                if (attribute != null)
                {
                    NarrativeBarDefinition bar = c.bars.Find(b => b != null && b.barKey == attribute.BarKey);
                    if (bar == null) errors.Add("AbilityCheckCondition " + condition.conditionId + ": attribute Bar is missing " + attribute.BarKey);
                    else if (condition.threshold < bar.min || condition.threshold > bar.max) errors.Add("AbilityCheckCondition " + condition.conditionId + ": threshold outside attribute range.");
                }
            }
            foreach (var check in checks.Values)
            {
                if (!c.abilityCheckConditions.Any(x => x != null && x.checkId == check.checkId)) errors.Add("AbilityCheck " + check.checkId + ": requires conditions.");
                foreach (string owner in new[] { check.successResultOwnerId, check.failureResultOwnerId })
                    if (!string.IsNullOrWhiteSpace(owner) && !c.events.Any(e => e != null && e.eventId == owner) && !c.choices.Any(x => x != null && x.choiceId == owner)) errors.Add("AbilityCheck " + check.checkId + ": unknown result owner " + owner);
            }
            foreach (var choice in c.choices) if (!string.IsNullOrWhiteSpace(choice.checkId) && !checks.ContainsKey(choice.checkId)) errors.Add("Choice " + choice.choiceId + ": unknown checkId " + choice.checkId);
            foreach (var setup in c.setups) if (!string.IsNullOrWhiteSpace(setup.checkId) && !checks.ContainsKey(setup.checkId)) errors.Add("SetupParams " + setup.setupKey + ": unknown checkId " + setup.checkId);
            return errors;
        }

        /// <summary>Shared fail-closed validation for authoring tools and the runtime table loader.</summary>
        public static List<string> ValidateConditionConfiguration(NarrativeConfiguration c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            var errors = new List<string>();
            var keys = new Dictionary<string, NarrativeConditionKeyDefinition>(StringComparer.Ordinal);
            var texts = new HashSet<string>(c.texts.Select(x => x.key), StringComparer.Ordinal);
            foreach (var key in c.conditionKeys)
            {
                if (string.IsNullOrWhiteSpace(key.key) || keys.ContainsKey(key.key)) errors.Add("ConditionKey: missing or duplicate key " + key.key);
                else keys.Add(key.key, key);
                if (IsInternalKey(key.key)) errors.Add("ConditionKey: reserved internal key " + key.key);
                if (!texts.Contains(key.nameText)) errors.Add("ConditionKey " + key.key + ": unknown nameText " + key.nameText);
            }
            var endingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ending in c.endings)
            {
                if (string.IsNullOrWhiteSpace(ending.endingId) || !endingIds.Add(ending.endingId)) errors.Add("Ending: missing or duplicate ID " + ending.endingId);
                if (!texts.Contains(ending.titleText) || !texts.Contains(ending.messageText)) errors.Add("Ending " + ending.endingId + ": unknown title/message text.");
                if (!c.conditions.Any(x => string.Equals(x.ownerType, "ending", StringComparison.OrdinalIgnoreCase) && x.ownerId == ending.endingId)) errors.Add("Ending " + ending.endingId + ": requires condition rows, including when disabled.");
            }
            foreach (var row in c.conditions)
            {
                string label = "Condition " + row.conditionId + ": ";
                string owner = (row.ownerType ?? "").ToLowerInvariant();
                bool knownOwner = owner switch
                {
                    "event" => c.events.Any(x => (string.IsNullOrWhiteSpace(x.conditionOwnerId) ? x.eventId : x.conditionOwnerId) == row.ownerId),
                    "choice" => c.choices.Any(x => x.choiceId == row.ownerId),
                    "action" => c.actions.Any(x => x.actionKey == row.ownerId),
                    "ending" => endingIds.Contains(row.ownerId),
                    _ => false
                };
                if (!knownOwner || string.IsNullOrWhiteSpace(row.ownerId)) errors.Add(label + "unknown " + owner + " owner " + row.ownerId);
                if (row.groupIndex < 1) errors.Add(label + "groupIndex must be positive.");
                if (row.notFlag || !string.IsNullOrWhiteSpace(row.op)) errors.Add(label + "op/notFlag are obsolete; encode the comparison in condType/value.");
                string type = (row.condType ?? "").ToUpperInvariant();
                string target = row.target ?? "";
                string key = target.Contains(":") ? target.Substring(target.IndexOf(':') + 1) : target;
                switch (type)
                {
                    case "BOOL_EQ":
                        if (!keys.ContainsKey(target)) errors.Add(label + "unknown bool key " + target);
                        if (!bool.TryParse(row.value, out _)) errors.Add(label + "expected true or false.");
                        break;
                    case "ITEM_GTE":
                    case "ACTION_PROGRESS_GTE":
                        if (string.IsNullOrWhiteSpace(key)) errors.Add(label + "missing target.");
                        if (!int.TryParse(row.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantity) || quantity < 0) errors.Add(label + "quantity must be a non-negative integer.");
                        if (type == "ACTION_PROGRESS_GTE" && !c.actions.Any(x => x.actionKey == key)) errors.Add(label + "unknown action " + target);
                        break;
                    case "ACTION_COMPLETED":
                        if (!c.actions.Any(x => x.actionKey == key)) errors.Add(label + "unknown action " + target);
                        if (!string.IsNullOrWhiteSpace(row.value)) errors.Add(label + "ACTION_COMPLETED requires empty value.");
                        break;
                    case "BAR_GTE": case "BAR_LTE": case "BAR_BETWEEN": case "REL_GTE":
                        bool found = c.bars.Any(x => x.barKey == key && (type != "REL_GTE" || x.barType == NarrativeBarType.Relation));
                        if (!found && type == "REL_GTE" && key.Contains(">"))
                        {
                            var pair = key.Split('>');
                            found = pair.Length == 2 && c.bars.Count(x => x.barType == NarrativeBarType.Relation && (x.ownerId == pair[0] && x.targetId == pair[1] || x.ownerId == pair[1] && x.targetId == pair[0])) == 1;
                        }
                        if (!found) errors.Add(label + "unknown bar " + target);
                        if (type == "BAR_BETWEEN")
                        {
                            var range = (row.value ?? "").Split(new[] { '.', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (range.Length != 2 || !int.TryParse(range[0], out int low) || !int.TryParse(range[1], out int high) || low > high) errors.Add(label + "expected inclusive min..max.");
                        }
                        else if (!int.TryParse(row.value, out _)) errors.Add(label + "integer value required.");
                        break;
                    case "EVENT_HAPPENED": case "EVENT_NOT_HAPPENED":
                        if (!c.events.Any(x => x.eventId == key)) errors.Add(label + "unknown event " + target);
                        break;
                    default: errors.Add(label + "unsupported condType " + row.condType); break;
                }
            }
            foreach (var result in c.results)
            {
                string label = "Result " + result.resultId + ": ";
                if (!Enum.IsDefined(typeof(NarrativeResultType), result.resultType)) errors.Add(label + "unsupported resultType.");
                if (result.resultType == NarrativeResultType.SetFlag || result.resultType == NarrativeResultType.ClearFlag)
                {
                    string target = result.target ?? "";
                    if (target.StartsWith("flag:", StringComparison.OrdinalIgnoreCase)) target = target.Substring(5);
                    if (!keys.TryGetValue(target, out var key)) errors.Add(label + "unknown bool key " + result.target);
                    bool valid = bool.TryParse(result.value, out bool value);
                    if (result.resultType == NarrativeResultType.ClearFlag && string.IsNullOrWhiteSpace(result.value)) valid = true;
                    if (!valid || result.resultType == NarrativeResultType.ClearFlag && value) errors.Add(label + "expected true/false; CLEAR_FLAG requires empty or false.");
                    if (key != null && key.onlySetTrue && (result.resultType == NarrativeResultType.ClearFlag || valid && !value)) errors.Add(label + "cannot clear onlySetTrue/death condition " + result.target);
                }
                if (result.resultType == NarrativeResultType.ItemAdd || result.resultType == NarrativeResultType.ItemRemove)
                {
                    if (string.IsNullOrWhiteSpace(result.target)) errors.Add(label + "missing item key.");
                    if (!int.TryParse(result.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantity) || quantity <= 0) errors.Add(label + "item quantity must be a positive integer.");
                }
            }
            var actionIds = new HashSet<string>(StringComparer.Ordinal);
            var milestoneIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var action in c.actions)
            {
                string label = "Action " + action.actionKey + ": ";
                if (string.IsNullOrWhiteSpace(action.actionKey) || !actionIds.Add(action.actionKey)) errors.Add(label + "missing or duplicate ID.");
                if (!Enum.IsDefined(typeof(NarrativeDeliveryMode), action.deliveryMode)) errors.Add(label + "unknown deliveryMode.");
                if (action.deliveryMode != NarrativeDeliveryMode.None && (action.progressTarget <= 0 || action.type == NarrativeActionType.Future || action.endDay)) errors.Add(label + "delivery requires positive progressTarget and cannot be Future/EndDay.");
                try
                {
                    var requirements = NarrativeActionRequirementParser.Parse(action.requirements);
                    if (requirements.Any(x => x.type != NarrativeActionRequirementType.Item && x.type != NarrativeActionRequirementType.ActionPoint)) errors.Add(label + "read-only requirements must migrate to Condition rows.");
                }
                catch (FormatException ex) { errors.Add(label + ex.Message); }
                foreach (string eventId in action.resultEvents)
                    if (!c.events.Any(x => x.eventId == eventId)) errors.Add(label + "unknown result event " + eventId);
                if (!string.IsNullOrWhiteSpace(action.nextActionKey) && !c.actions.Any(x => x.actionKey == action.nextActionKey)) errors.Add(label + "unknown next action " + action.nextActionKey);
                foreach (var milestone in action.milestones)
                {
                    string ml = "ActionMilestone " + milestone.milestoneId + ": ";
                    if (string.IsNullOrWhiteSpace(milestone.milestoneId) || !milestoneIds.Add(milestone.milestoneId)) errors.Add(ml + "missing or duplicate ID.");
                    if (milestone.actionKey != action.actionKey) errors.Add(ml + "unknown/mismatched action " + milestone.actionKey);
                    if (action.deliveryMode == NarrativeDeliveryMode.None || milestone.atProgress < 1 || milestone.atProgress >= action.progressTarget) errors.Add(ml + "requires delivery and 1 <= atProgress < progressTarget.");
                    if (!c.events.Any(x => x.eventId == milestone.eventId)) errors.Add(ml + "unknown event " + milestone.eventId);
                }
            }
            return errors;
        }

        private static bool IsInternalKey(string key) => key != null && (key.StartsWith("event.locked.", StringComparison.Ordinal) || key.StartsWith("trigger.once.", StringComparison.Ordinal) || key.StartsWith("date.once.", StringComparison.Ordinal) || key.StartsWith("event.once.", StringComparison.Ordinal) || key.StartsWith("achievement.", StringComparison.Ordinal));

        public static bool IsResultOnlyEvent(NarrativeConfiguration config, NarrativeEventDefinition definition)
        {
            if (definition == null || NarrativePresenter.RequiresPresentation(definition, null) || !string.IsNullOrWhiteSpace(definition.presentationClass)) return false;
            return config.results.Exists(result => result != null && result.eventId == definition.eventId);
        }

        public static List<string> ValidateValueConfiguration(NarrativeConfiguration config)
        {
            var errors = new List<string>();
            var facts = new HashSet<string>(StringComparer.Ordinal);
            var bars = new Dictionary<string, NarrativeBarDefinition>(StringComparer.Ordinal);
            foreach (var bar in config.bars)
            {
                if (bar == null || string.IsNullOrWhiteSpace(bar.barKey)) continue;
                bars[bar.barKey] = bar;
                if (bar.sourceMode == NarrativeSourceMode.External)
                {
                    if (string.IsNullOrWhiteSpace(bar.providerId) || string.IsNullOrWhiteSpace(bar.sourceKey) || bar.advanceMode != NarrativeAdvanceMode.EventDriven)
                        errors.Add($"Bar {bar.barKey}: external requires providerId/sourceKey and event_driven mode.");
                    if (!facts.Add(bar.providerId + ":" + bar.sourceKey)) errors.Add($"Bar {bar.barKey}: duplicate external business fact.");
                    if (bar.barType == NarrativeBarType.Relation) errors.Add($"Bar {bar.barKey}: external relation requires directional provider support, not supported in this version.");
                }
                else if (!string.IsNullOrEmpty(bar.providerId) || !string.IsNullOrEmpty(bar.sourceKey)) errors.Add($"Bar {bar.barKey}: ncs source must not specify external fields.");
            }
            foreach (var bar in bars.Values)
                ValidateDerived(bar.barKey, bars, new HashSet<string>(StringComparer.Ordinal), errors);
            foreach (var result in config.results)
            {
                if (result.resultType == NarrativeResultType.MoneyAdd || result.resultType == NarrativeResultType.MoneyRemove)
                {
                    if (result.target != "cafe" || !int.TryParse(result.value, out int amount) || amount <= 0)
                        errors.Add($"Result {result.resultId}: money requires target cafe and positive integer value.");
                }
                if (result.resultType != NarrativeResultType.BarAdd && result.resultType != NarrativeResultType.BarSet) continue;
                string key = (result.target ?? string.Empty).Replace("bar:", "");
                if (!bars.TryGetValue(key, out var target) || target.sourceMode == NarrativeSourceMode.External || target.advanceMode == NarrativeAdvanceMode.Derived)
                    errors.Add($"Result {result.resultId}: bar {key} is external, derived or unknown; cannot write.");
            }
            return errors;
        }

        private static void ValidateDerived(string key, Dictionary<string, NarrativeBarDefinition> bars, HashSet<string> visiting, List<string> errors)
        {
            if (!bars.TryGetValue(key, out var bar)) { errors.Add("Unknown derived source: " + key); return; }
            if (bar.advanceMode != NarrativeAdvanceMode.Derived) return;
            if (!visiting.Add(key)) { errors.Add("Derived source cycle: " + key); return; }
            if (bar.derivedSources.Count == 0) errors.Add("Derived bar requires sources: " + key);
            foreach (string source in bar.derivedSources) ValidateDerived(source, bars, visiting, errors);
            visiting.Remove(key);
        }

        private static void ValidateLinks(NarrativeConfiguration config)
        {
            var valueErrors = ValidateValueConfiguration(config);
            if (valueErrors.Count > 0) throw new FormatException(string.Join("\n", valueErrors));
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
                if (string.IsNullOrWhiteSpace(item.setupType) && string.IsNullOrWhiteSpace(resolvedPresentationClass) && !IsResultOnlyEvent(config, item))
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
                if (item.speaker != "narrator" && item.speaker != "left" && item.speaker != "right" && item.speaker != "center") throw Unknown("opening speaker", item.speaker);
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
        private static NarrativeSourceMode SourceMode(string value) => value switch
        {
            null => NarrativeSourceMode.Ncs, "" => NarrativeSourceMode.Ncs, "ncs" => NarrativeSourceMode.Ncs,
            "external" => NarrativeSourceMode.External, _ => throw Unknown("sourceMode", value)
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
        private static NarrativeDeliveryMode DeliveryMode(string value) => value switch
        {
            "None" => NarrativeDeliveryMode.None, "Cumulative" => NarrativeDeliveryMode.Cumulative,
            "Consecutive" => NarrativeDeliveryMode.Consecutive, _ => throw Unknown("deliveryMode", value)
        };
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
        private static float? CafeSeconds(string text, string key, string field, bool patience)
        {
            if (string.IsNullOrWhiteSpace(text)) return patience ? (float?)null : 0f;
            if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value)
                && !float.IsNaN(value) && !float.IsInfinity(value) && (patience ? value > 0f : value >= 0f)) return value;
            UnityEngine.Debug.LogWarning($"[CafeVisit/configuration_error] {key}.{field}={text} 非法，回退{(patience ? "普通默认耐心" : "0 秒")}。");
            return patience ? (float?)null : 0f;
        }
        private static bool CafeLeaveAfterServed(string text, string key)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (bool.TryParse(text, out bool value)) return value;
            UnityEngine.Debug.LogWarning($"[CafeVisit/configuration_error] {key}.leaveAfterServed={text} 非法，回退 true。");
            return true;
        }
        private static NarrativeResultType ResultType(string value) => value switch
        {
            "MONEY_ADD" => NarrativeResultType.MoneyAdd, "MONEY_REMOVE" => NarrativeResultType.MoneyRemove,
                "ITEM_ADD" => NarrativeResultType.ItemAdd, "ITEM_REMOVE" => NarrativeResultType.ItemRemove,
            "BAR_ADD" => NarrativeResultType.BarAdd, "BAR_SET" => NarrativeResultType.BarSet,
            "REL_ADD" => NarrativeResultType.RelAdd, "SET_FLAG" => NarrativeResultType.SetFlag,
            "CLEAR_FLAG" => NarrativeResultType.ClearFlag, "SET_SWITCH" => NarrativeResultType.SetSwitch,
            "OBJ_STATE" => NarrativeResultType.ObjectState, "OBJECT_STATE" => NarrativeResultType.ObjectState, "UNLOCK_EVENT" => NarrativeResultType.UnlockEvent,
            "LOCK_EVENT" => NarrativeResultType.LockEvent, "ADVANCE_DAY" => NarrativeResultType.AdvanceDay,
            "AWARD_ACH" => NarrativeResultType.AwardAchievement, "AWARD_ACHIEVEMENT" => NarrativeResultType.AwardAchievement, "PLAY_SETUP" => NarrativeResultType.PlaySetup,
            "BAR_HINT" => NarrativeResultType.BarHint, "SPAWN_NPC" => NarrativeResultType.SpawnNpc,
            "DESPAWN_NPC" => NarrativeResultType.DespawnNpc,
            "RESERVE_CAFE_VISIT" => NarrativeResultType.ReserveCafeVisit,
            "SHOWPOSITION" or "SHOW_POSITION" => NarrativeResultType.ShowPosition,
            "HIDEPOSITION" or "HIDE_POSITION" => NarrativeResultType.HidePosition,
            "ADDTEMPPOSITION" or "ADD_TEMP_POSITION" => NarrativeResultType.AddTempPosition,
            "REMOVETEMPPOSITION" or "REMOVE_TEMP_POSITION" => NarrativeResultType.RemoveTempPosition,
            _ => throw Unknown("resultType", value)
        };
    }
}
