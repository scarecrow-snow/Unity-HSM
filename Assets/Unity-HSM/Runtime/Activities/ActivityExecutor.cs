using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using System;


namespace HSM
{
    public class ActivityExecutor : IDisposable
    {
        CancellationTokenSource currentCts;
        bool isExecuting;
        bool isDeactivate;
        readonly List<IActivity> managedActivities = new();

        public bool IsExecuting => isExecuting;

        /// <summary>
        /// Execute a collection of activities in parallel
        /// </summary>
        public async UniTask ExecuteAsync(IReadOnlyList<IActivity> activities, bool isDeactivate, CancellationToken ct = default)
        {
            Cancel();

            // Track activities for disposal
            for (int i = 0; i < activities.Count; i++)
            {
                if (!managedActivities.Contains(activities[i]))
                {
                    managedActivities.Add(activities[i]);
                }
            }

            this.isDeactivate = isDeactivate;
            this.isExecuting = true;
            currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                var tasks = new List<UniTask>();
                for (int i = 0; i < activities.Count; i++)
                {
                    var activity = activities[i];
                    bool shouldExecute = isDeactivate ? (activity.Mode == ActivityMode.Active)
                                                     : (activity.Mode == ActivityMode.Inactive);
                    if (!shouldExecute) continue;

                    tasks.Add(ExecuteActivitySafe(activity, currentCts.Token));
                }

                await UniTask.WhenAll(tasks);
            }
            finally
            {
                isExecuting = false;
            }
        }

        /// <summary>
        /// Execute a single activity
        /// </summary>
        public async UniTask ExecuteAsync(IActivity activity, bool isDeactivate, CancellationToken ct = default)
        {
            Cancel();

            // Track activity for disposal
            if (!managedActivities.Contains(activity))
            {
                managedActivities.Add(activity);
            }

            this.isDeactivate = isDeactivate;
            this.isExecuting = true;
            currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                bool shouldExecute = isDeactivate ? (activity.Mode == ActivityMode.Active)
                                                 : (activity.Mode == ActivityMode.Inactive);
                if (shouldExecute)
                {
                    await ExecuteActivitySafe(activity, currentCts.Token);
                }
            }
            finally
            {
                isExecuting = false;
            }
        }

        async UniTask ExecuteActivitySafe(IActivity activity, CancellationToken ct)
        {
            try
            {
                if (isDeactivate)
                    await activity.DeactivateAsync(ct);
                else
                    await activity.ActivateAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected, suppress
            }
            catch (Exception)
            {
                // Other exceptions are also suppressed to prevent leak detection
            }
        }

        /// <summary>
        /// Cancel all running tasks
        /// </summary>
        public void Cancel()
        {
            currentCts?.Cancel();
            Clear();
        }

        /// <summary>
        /// Clear all running tasks
        /// </summary>
        public void Clear()
        {
            currentCts?.Dispose();
            currentCts = null;
        }

        /// <summary>
        /// Dispose all managed activities and release resources
        /// </summary>
        public void Dispose()
        {
            Cancel();

            foreach (var activity in managedActivities)
            {
                activity?.Dispose();
            }
            managedActivities.Clear();
        }
    }
}