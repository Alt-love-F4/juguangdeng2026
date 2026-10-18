using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>Read-only projection of existing action state. No task state or content tables.</summary>
    public static class GameplayTodoProjection
    {
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
                    bool done = actions.IsCompleted(action.actionKey) || actions.HasExecutedToday(action.actionKey)
                        && (action.type == NarrativeActionType.Daily || action.IsDelivery);
                    string name = runtime.ResolveText(action.nameText);
                    string status;
                    if (done) status = actions.IsCompleted(action.actionKey) ? "已完成" : "今日已完成，明天可继续";
                    else if (action.type == NarrativeActionType.Future) status = "等待 " + actions.GetRemainingDays(action.actionKey) + " 天后开放";
                    else if (actions.IsAvailable(action.actionKey, out var checks)) status = "可进行";
                    else if (checks.Any(c => !c.satisfied && c.requirement.StartsWith("ActionPoint("))) status = "行动点不足，查看详情确认消耗";
                    else if (checks.Any(c => !c.satisfied && c.requirement.StartsWith("Item("))) status = "所需物品不足，查看详情确认需求";
                    else status = "暂不可进行，请先处理当前事项";
                    if (action.IsProgress) status += " · 进度 " + actions.GetProgress(action.actionKey) + "/" + action.progressTarget;
                    result.Add(new MapTaskItem {
                        id = action.actionKey, actionKey = action.actionKey, name = name,
                        description = runtime.ResolveText(action.descText), completed = done,
                        currentTask = "前往「" + locationName + "」，在行动详情中选择「" + name + "」。",
                        location = locationName, positionKey = position, status = status,
                        navigationLabel = "查看地点与行动", priority = action.mustExecute && !done ? 0 : 10,
                        remainingDays = actions.GetRemainingDays(action.actionKey)
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
            return result;
        }
    }
}
