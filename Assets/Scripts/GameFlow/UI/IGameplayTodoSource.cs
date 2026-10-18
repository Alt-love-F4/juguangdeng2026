using System.Collections.Generic;

namespace Spotlight.GameFlow
{
    /// <summary>Existing gameplay controls expose guidance without adding narrative table rows.</summary>
    public interface IGameplayTodoSource
    {
        void CollectTodos(List<MapTaskItem> items);
        bool AllowsTodoPosition(string positionKey);
        bool NavigateTodo(MapTaskItem item);
    }
}
