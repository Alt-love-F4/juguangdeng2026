using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.GameFlow
{
    public sealed class LoadingTipSequence
    {
        public const float DefaultTipDurationSeconds = 4f;

        private readonly List<string> tips;
        private readonly List<int> order;
        private readonly List<int> history;
        private readonly System.Random random;
        private int historyCursor;
        private int orderCursor;
        private float elapsed;

        public LoadingTipSequence(IEnumerable<string> sourceTips, int? seed = null)
        {
            tips = CopyTips(sourceTips);
            random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            order = new List<int>(tips.Count);
            history = new List<int>(tips.Count);
            historyCursor = -1;
            orderCursor = 0;
            elapsed = 0f;

            RebuildOrder(-1);
            if (order.Count > 0)
            {
                history.Add(order[0]);
                historyCursor = 0;
                orderCursor = 1;
            }
        }

        public string CurrentTip => CurrentIndex >= 0 ? tips[CurrentIndex] : string.Empty;
        public int CurrentIndex => historyCursor >= 0 ? history[historyCursor] : -1;
        public int Count => tips.Count;
        public float ElapsedSeconds => elapsed;
        public bool CanMovePrevious => historyCursor > 0;
        public bool CanMoveNext => historyCursor >= 0 && historyCursor < history.Count - 1 || order.Count > 0;

        public string Next()
        {
            if (tips.Count == 0)
                return string.Empty;

            if (historyCursor < history.Count - 1)
            {
                historyCursor++;
            }
            else
            {
                int nextIndex = TakeNextIndex();
                history.Add(nextIndex);
                historyCursor++;
            }

            elapsed = 0f;
            return CurrentTip;
        }

        public string Previous()
        {
            if (historyCursor > 0) historyCursor--;
            elapsed = 0f;
            return CurrentTip;
        }

        public bool MoveNext()
        {
            if (tips.Count == 0 || !CanMoveNext)
                return false;

            int previous = CurrentIndex;
            Next();
            return previous != CurrentIndex;
        }

        public bool MovePrevious()
        {
            if (!CanMovePrevious)
                return false;

            int previous = CurrentIndex;
            Previous();
            return previous != CurrentIndex;
        }

        public bool Tick()
        {
            return Tick(Time.unscaledDeltaTime);
        }

        public bool Tick(float unscaledDeltaTime)
        {
            if (tips.Count == 0 || float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime)
                || unscaledDeltaTime < 0f)
                return false;

            elapsed += unscaledDeltaTime;
            bool changed = false;
            while (elapsed >= DefaultTipDurationSeconds)
            {
                elapsed -= DefaultTipDurationSeconds;
                float remaining = elapsed;
                int previous = CurrentIndex;
                Next();
                elapsed = remaining;
                changed |= previous != CurrentIndex;
            }
            return changed;
        }

        public void ResetTimer()
        {
            elapsed = 0f;
        }

        private int TakeNextIndex()
        {
            if (orderCursor >= order.Count)
            {
                int previous = CurrentIndex;
                RebuildOrder(previous);
                orderCursor = 0;
            }
            return order[orderCursor++];
        }

        private void RebuildOrder(int previousIndex)
        {
            order.Clear();
            for (int i = 0; i < tips.Count; i++)
                order.Add(i);

            for (int i = order.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                int value = order[i];
                order[i] = order[swapIndex];
                order[swapIndex] = value;
            }

            if (previousIndex >= 0 && order.Count > 1 && order[0] == previousIndex)
            {
                int swapIndex = 1 + random.Next(order.Count - 1);
                int value = order[0];
                order[0] = order[swapIndex];
                order[swapIndex] = value;
            }
        }

        private static List<string> CopyTips(IEnumerable<string> sourceTips)
        {
            var result = new List<string>();
            if (sourceTips == null)
                return result;

            foreach (string tip in sourceTips)
            {
                if (tip != null)
                    result.Add(tip);
            }
            return result;
        }
    }
}
