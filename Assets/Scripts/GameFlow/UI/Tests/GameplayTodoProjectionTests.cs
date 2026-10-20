using System;
using System.Linq;
using NUnit.Framework;

namespace Spotlight.GameFlow.Tests
{
    public sealed class GameplayTodoProjectionTests
    {
        [Test] public void MandatoryPlayableTaskWinsOverTrackedOptionalAndLockedTask()
        {
            var optional = new MapTaskItem { id = "repeat", optional = true };
            var locked = new MapTaskItem { id = "locked", important = true, available = false, preventsRest = true };
            var playable = new MapTaskItem { id = "story", important = true, available = true };
            Assert.That(GameplayTodoProjection.SelectNext(new[] { optional, locked, playable }, "repeat"), Is.SameAs(playable));
        }
        [Test] public void AvailableTaskWinsOverUnavailableImportantTask()
        {
            var blocked = new MapTaskItem { id = "blocked", important = true, available = false };
            var next = new MapTaskItem { id = "next" };
            Assert.That(GameplayTodoProjection.SelectNext(new[] { blocked, next }), Is.SameAs(next));
        }
        [Test] public void OptionalDailyAndUtilityEntriesDoNotPreventRestRecommendation()
        {
            var rest = new MapTaskItem { id = GameplayTodoProjection.RestTodoId, priority = 5 };
            var optional = new MapTaskItem { id = "recipes", optional = true, priority = 0 };
            Assert.That(GameplayTodoProjection.SelectNext(new[] { optional, rest }, "recipes"), Is.SameAs(rest));
        }
        [Test] public void CompletedTrackedTaskMovesOnToNextRealTask()
        {
            var completed = new MapTaskItem { id = "old", completed = true, important = true };
            var next = new MapTaskItem { id = "next" };
            Assert.That(GameplayTodoProjection.SelectNext(new[] { completed, next }, "old"), Is.SameAs(next));
        }
        [Test] public void DeadlineAndStableIdentityBreakEqualPriorityTies()
        {
            var items = new[] { new MapTaskItem { id = "z" }, new MapTaskItem { id = "b", remainingDays = 1 },
                new MapTaskItem { id = "a", remainingDays = 1 }, new MapTaskItem { id = "later", remainingDays = 3 } };
            Assert.That(GameplayTodoProjection.Order(items).Select(i => i.id), Is.EqualTo(new[] { "a", "b", "later", "z" }));
        }
        [Test] public void LockedMandatoryTaskIsExplainedRatherThanReplacedByOptionalChores()
        {
            var locked = new MapTaskItem { id = "locked", available = false, important = true, preventsRest = true, blockedReason = "缺少物品" };
            Assert.That(GameplayTodoProjection.SelectNext(new[] { new MapTaskItem { id = "repeat", optional = true }, locked }), Is.SameAs(locked));
        }
        [Test] public void TrackedPlayableTaskIsRespectedWhenNoMandatoryTaskExists()
        {
            var tracked = new MapTaskItem { id = "tracked", priority = 20 };
            Assert.That(GameplayTodoProjection.SelectNext(new[] { new MapTaskItem { id = "first" }, tracked }, "tracked"), Is.SameAs(tracked));
        }
        [Test] public void EmptyAndCompletedListsHaveNoNextStep()
        {
            Assert.That(GameplayTodoProjection.SelectNext(null), Is.Null);
            Assert.That(GameplayTodoProjection.SelectNext(new[] { new MapTaskItem { id = "done", completed = true } }), Is.Null);
        }
    }
}
