using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using System;
using ZLinq;

namespace HSM
{
    public class ActivityExecutor : IDisposable
    {
        CancellationTokenSource currentCts;
        bool isExecuting;
        readonly List<IActivity> managedActivities = new();
        readonly List<UniTask> taskList = new(); // Reusable task list for zero allocation

        public bool IsExecuting => isExecuting;

        /// <summary>
        /// Activate a collection of activities in parallel
        /// </summary>
        public async UniTask ActivateAsync(IReadOnlyList<IActivity> activities, CancellationToken ct = default)
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

            isExecuting = true;
            currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                taskList.Clear();
                for (int i = 0; i < activities.Count; i++)
                {
                    var activity = activities[i];
                    if (activity.Mode == ActivityMode.Inactive)
                    {
                        taskList.Add(ExecuteActivitySafe(activity, true, currentCts.Token));
                    }
                }

                await taskList;
                //await UniTask.WhenAll(taskList);
            }
            finally
            {
                isExecuting = false;
            }
        }

        /// <summary>
        /// Activate a single activity
        /// </summary>
        public async UniTask ActivateAsync(IActivity activity, CancellationToken ct = default)
        {
            Cancel();

            // Track activity for disposal
            if (!managedActivities.Contains(activity))
            {
                managedActivities.Add(activity);
            }

            isExecuting = true;
            currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                if (activity.Mode == ActivityMode.Inactive)
                {
                    await ExecuteActivitySafe(activity, true, currentCts.Token);
                }
            }
            finally
            {
                isExecuting = false;
            }
        }

        /// <summary>
        /// Deactivate a collection of activities in parallel
        /// </summary>
        public async UniTask DeactivateAsync(IReadOnlyList<IActivity> activities, CancellationToken ct = default)
        {
            Cancel();

            isExecuting = true;
            currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                taskList.Clear();
                for (int i = 0; i < activities.Count; i++)
                {
                    var activity = activities[i];
                    if (activity.Mode == ActivityMode.Active)
                    {
                        taskList.Add(ExecuteActivitySafe(activity, false, currentCts.Token));
                    }
                }

                await taskList;
                //await UniTask.WhenAll(taskList);
            }
            finally
            {
                isExecuting = false;
            }
        }

        /// <summary>
        /// Deactivate a single activity
        /// </summary>
        public async UniTask DeactivateAsync(IActivity activity, CancellationToken ct = default)
        {
            Cancel();

            isExecuting = true;
            currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                if (activity.Mode == ActivityMode.Active)
                {
                    await ExecuteActivitySafe(activity, false, currentCts.Token);
                }
            }
            finally
            {
                isExecuting = false;
            }
        }

        async UniTask ExecuteActivitySafe(IActivity activity, bool isActivate, CancellationToken ct)
        {
            try
            {
                if (isActivate)
                    await activity.ActivateAsync(ct);
                else
                    await activity.DeactivateAsync(ct);
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