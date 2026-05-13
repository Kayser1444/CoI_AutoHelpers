using System;
using System.Collections.Generic;

namespace CoI.AutoHelpers.Localization
{
    public static class LaterTextExtensions
    {
        public static DeferredUiRefreshQueue CreateDeferredRefreshQueue()
        {
            return new DeferredUiRefreshQueue();
        }

        public static void QueueRefresh(this DeferredUiRefreshQueue queue, Action refreshAction)
        {
            if (queue == null)
            {
                throw new ArgumentNullException(nameof(queue));
            }

            queue.Enqueue(refreshAction);
        }

        public static int FlushDeferredRefreshes(this DeferredUiRefreshQueue queue)
        {
            if (queue == null)
            {
                throw new ArgumentNullException(nameof(queue));
            }

            return queue.Flush();
        }
    }

    public sealed class DeferredUiRefreshQueue
    {
        private readonly List<Action> m_actions = new List<Action>();

        public int PendingCount => m_actions.Count;

        public void Enqueue(Action refreshAction)
        {
            if (refreshAction == null)
            {
                throw new ArgumentNullException(nameof(refreshAction));
            }

            m_actions.Add(refreshAction);
        }

        public int Flush()
        {
            int executed = 0;
            Action[] actions = m_actions.ToArray();
            m_actions.Clear();

            foreach (Action action in actions)
            {
                try
                {
                    action();
                    executed++;
                }
                catch
                {
                    // Deferred refreshes are best-effort; a single UI callback should not abort the whole batch.
                }
            }

            return executed;
        }
    }
}
