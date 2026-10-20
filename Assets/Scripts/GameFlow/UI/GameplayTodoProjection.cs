using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>Read-only projection of existing action state. No task state or content tables.</summary>
    public static class GameplayTodoProjection
    {
        public const string RestTodoId = "guide.cafe.sleep";

        public static IOrderedEnumerable<MapTaskItem> Order(IEnumerable<MapTaskItem> items)
            => (items ?? Enumerable.Empty<MapTaskItem>()).Where(i => i != null).OrderBy(i => i.completed)
                .ThenBy(i => !i.available).ThenBy(i => i.optional).ThenByDescending(i => i.important)
                .ThenBy(i => i.remainingDays > 0 ? i.remainingDays : int.MaxValue)
                .ThenBy(i => i.priority).ThenBy(i => i.id, StringComparer.Ordinal);

        /// <summary>One recommendation for both the player and the visible walkthrough; no copied task state.</summary>
        public static MapTaskItem SelectNext(IEnumerable<MapTaskItem> items, string trackedId = null)
        {
            var pending = Order(items).Where(i => !i.completed).ToList();
            // A tracked optional/locked item must not conceal a mandatory playable step.
            return pending.Find(i => i.available && !i.optional && i.important)
                ?? pending.Find(i => i.available && !i.optional && i.id == trackedId)
                ?? pending.Find(i => i.available && !i.optional)
                ?? pending.Find(i => !i.optional && i.preventsRest)
                ?? pending.Find(i => i.available && i.id == trackedId)
                ?? pending.FirstOrDefault();
        }

        private static string DescribeBlockers(List<NarrativeActionCheck> checks, Func<string, string> resolve)
        {
            if (checks == null) return "";
            return string.Join("；", checks.Where(c => !c.satisfied).Select(c => {
                string requirement = c.requirement ?? "";
                string label = requirement.StartsWith("ActionPoint(") ? "行动点不足，先查看其他可进行事项；无必做事项时可回宿舍休息"
                    : requirement.StartsWith("Item(") ? "缺少所需物品，请查看行动详情中的物品需求"
                    : requirement == "MustExecute" ? "请先完成标为必做的事项"
                    : string.IsNullOrWhiteSpace(c.messageKey) ? null : resolve(c.messageKey);
                if (string.IsNullOrWhiteSpace(label) || label == c.messageKey)
                    label = "请查看行动详情中的解锁条件";
                return label + (string.IsNullOrEmpty(c.requiredValue) ? "" : "（当前 " + c.currentValue + " / 需要 " + c.requiredValue + "）");
            }).Distinct());
        }

        public static void AppendRestGuidance(List<MapTaskItem> items, NarrativeRuntime runtime, bool available)
        {
            if (!available || runtime?.Actions == null || runtime.Controller.IsEnded || runtime.Controller.IsBusy
                || runtime.Actions.IsExecuting || HasActionableContent(runtime)
                || items.Any(i => !i.completed && i.preventsRest)) return;
            items.Add(new MapTaskItem {
                id = RestTodoId, name = "前往咖啡馆三楼睡觉", location = "咖啡馆 · 三楼宿舍",
                description = "目前没有其他需要处理的事项，可以回咖啡馆三楼睡觉，迎接新的一天。",
                currentTask = "前往咖啡馆三楼宿舍，点击床睡觉。前往不会推进日期，点击床后才会进入下一天。",
                navigationLabel = "前往咖啡馆三楼", status = "暂无其他事项 · 可以休息", priority = 5
            });
        }

        public static bool HasActionableContent(NarrativeRuntime runtime)
        {
            var actions = runtime?.Actions;
            return actions != null && (actions.HasBlockingActions() || actions.GetMapPositions()
                .SelectMany(actions.GetDisplayableActions).Any(a =>
                    !(a.type == NarrativeActionType.Repeat && !a.mustExecute && actions.IsDailyPoolAction(a.actionKey))
                    && actions.IsAvailable(a.actionKey, out _)));
        }

        public static List<MapTaskItem> Build(NarrativeRuntime runtime, Func<string, bool> allowsPosition = null)
        {
            var result = new List<MapTaskItem>();
            var actions = runtime?.Actions;
            if (actions == null) return result;
            var seen = new HashSet<string>();
            foreach (string position in actions.GetMapPositions())
            {
                if (allowsPosition != null && !allowsPosition(position)) continue;
                var location = runtime.Configuration?.mapPositions?.Find(p => p != null && p.positionKey == position);
                string locationName = location == null ? position : runtime.ResolveText(location.nameText);
                foreach (var action in actions.GetDisplayableActions(position))
                {
                    if (!seen.Add(action.actionKey)) continue;
                    bool optionalPool = action.type == NarrativeActionType.Repeat && !action.mustExecute
                        && actions.IsDailyPoolAction(action.actionKey);
                    bool done = actions.IsCompleted(action.actionKey) || actions.HasExecutedToday(action.actionKey)
                        && (action.type == NarrativeActionType.Daily || action.IsDelivery || actions.IsDailyPoolAction(action.actionKey));
                    string name = runtime.ResolveText(action.nameText);
                    string status;
                    List<NarrativeActionCheck> checks = null;
                    bool available = false;
                    if (!done && action.type != NarrativeActionType.Future)
                        available = actions.IsAvailable(action.actionKey, out checks);
                    // An empty daily event pool is a map-card state, not a task. Do not pollute
                    // the todo list with a location that currently has nothing the player can do.
                    if (!done && action.type != NarrativeActionType.Future && !available
                        && checks != null && checks.Any(c => !c.satisfied && c.requirement == "DailyPool")) continue;
                    if (done) status = actions.IsCompleted(action.actionKey) ? "已完成" : "今日已完成，明天可继续";
                    else if (action.type == NarrativeActionType.Future) status = "等待 " + actions.GetRemainingDays(action.actionKey) + " 天后开放";
                    else if (available) status = optionalPool ? "自选日常 · 每天一次" : "可进行";
                    else if (checks != null && checks.Any(c => !c.satisfied && c.requirement.StartsWith("ActionPoint("))) status = "行动点不足，查看详情确认消耗";
                    else if (checks != null && checks.Any(c => !c.satisfied && c.requirement.StartsWith("Item("))) status = "所需物品不足，查看详情确认需求";
                    else status = "暂不可进行，请先处理当前事项";
                    string blockers = !done && !available ? DescribeBlockers(checks, runtime.ResolveText) : "";
                    if (action.IsProgress) status += " · 进度 " + actions.GetProgress(action.actionKey) + "/" + action.progressTarget;
                    result.Add(new MapTaskItem {
                        id = action.actionKey, actionKey = action.actionKey, name = name,
                        description = runtime.ResolveText(action.descText), completed = done,
                        available = !done && available, optional = optionalPool, blockedReason = blockers,
                        currentTask = optionalPool
                            ? "这是自选日常，不是未完成任务的分段。每项每天可完成一次，明天可再进行。"
                            : !available ? (action.type == NarrativeActionType.Future ? "开放后前往「" + locationName + "」。" : blockers)
                            : "点击下方「查看地点与行动」，前往「" + locationName + "」，选择「" + name + "」并确认执行。"
                                + (action.IsProgress ? " 每次执行后查看进度，达到 " + action.progressTarget + " 次后完成。" : "")
                                + " 对话中的选项由你决定；完成后查看下一条待办。",
                        location = locationName, positionKey = position, status = status,
                        navigationLabel = "查看地点与行动", priority = action.mustExecute && !done ? 0 : 10,
                        important = action.mustExecute && !done && action.type != NarrativeActionType.Future
                            && !actions.HasExecutedToday(action.actionKey),
                        remainingDays = actions.GetRemainingDays(action.actionKey),
                        preventsRest = !done && !optionalPool && (action.mustExecute || available)
                    });
                }
                // Completed one-time actions no longer belong to the displayable map projection.
                // Their persisted counters, not a second UI history, are the source of completion.
                foreach (var action in actions.Actions.Where(a => actions.IsCompleted(a.actionKey)))
                {
                    // Only actions actually mounted here belong to this session's journal.
                    if (!actions.IsMounted(position, action.actionKey) || !seen.Add(action.actionKey)) continue;
                    result.Add(new MapTaskItem { id = action.actionKey, actionKey = action.actionKey,
                        name = runtime.ResolveText(action.nameText), description = runtime.ResolveText(action.descText),
                        completed = true, location = locationName, status = "已完成" });
                }
            }
            return Order(result).ToList();
        }

        /// <summary>Guide only to a real, currently visible mandatory action, never invent a future prerequisite.</summary>
        public static bool ShouldGuideToImportantTodo(NarrativeRuntime runtime, NarrativeActionDefinition action,
            IReadOnlyList<NarrativeActionCheck> checks)
        {
            var actions = runtime?.Actions;
            if (actions == null || action == null || !runtime.Controller.ValuesReady || runtime.Controller.IsBusy
                || runtime.Controller.IsEnded || actions.IsExecuting || actions.IsCompleted(action.actionKey)
                || action.type == NarrativeActionType.Future
                || ((action.type == NarrativeActionType.Daily || action.IsDelivery || actions.IsDailyPoolAction(action.actionKey)) && actions.HasExecutedToday(action.actionKey))
                || checks.Any(check => !check.satisfied && (check.requirement.StartsWith("ActionPoint(") || check.requirement.StartsWith("Item(")))) return false;
            bool storyBlocked = action.endDay && actions.HasBlockingActions()
                || checks.Any(check => !check.satisfied && check.messageKey == "action.condition.missing");
            return storyBlocked && Build(runtime).Any(item => item.important && !item.completed);
        }
    }
}
