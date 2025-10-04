using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace HSM.Examples {
    /// <summary>
    /// Example demonstrating external ActivityExecutor usage
    /// </summary>
    public class ActivityExecutorExample : MonoBehaviour {
        ActivityExecutor activityExecutor = new();

        async void Start() {
            try {
                // Example 1: Execute activities in parallel
                ExecuteActivitiesParallelExample().Forget();

                // Example 2: Execute activities sequentially
                await ExecuteActivitiesSequentialExample();

                // Example 3: Custom activity execution control
                await CustomActivityControlExample();
            } catch (System.Exception e) {
                Debug.LogError($"ActivityExecutor example failed: {e.Message}");
            }
        }

        async UniTask ExecuteActivitiesParallelExample()
        {
            Debug.Log("=== Parallel Activity Execution Example (Zero Allocation) ===");

            // Use List<T> which implements IReadOnlyList<T> - no allocation overhead
            var activities = new List<IActivity> {
                new MessageActivity("Parallel Task 1"),
                new MessageActivity("Parallel Task 2"),
                new MessageActivity("Parallel Task 3")
            };

            // Execute all activities in parallel and await completion
            await activityExecutor.ActivateAsync(activities, destroyCancellationToken);

            Debug.Log("Parallel Activate completed!");

            // Deactivate when done
            await activityExecutor.DeactivateAsync(activities, destroyCancellationToken);
            Debug.Log("Parallel Deactivate completed!");
        }

        async UniTask ExecuteActivitiesSequentialExample()
        {
            Debug.Log("=== Sequential Activity Execution Example (Zero Allocation) ===");

            SequentialActivityGroup sequens = new();
            sequens.AddActivity(new MessageActivity("Sequential Task 1"));
            sequens.AddActivity(new DelayActivationActivity(0.5f));
            sequens.AddActivity(new MessageActivity("Sequential Task 2"));
            sequens.AddActivity(new DelayActivationActivity(0.5f));
            sequens.AddActivity(new MessageActivity("Sequential Task 3"));

            // Execute all activities sequentially and await completion
            await activityExecutor.ActivateAsync(sequens, destroyCancellationToken);

            Debug.Log("Sequential Activate completed!");

            // Deactivate when done
            await activityExecutor.DeactivateAsync(sequens, destroyCancellationToken);
            Debug.Log("Sequential Deactivate completed!");
        }

        async UniTask CustomActivityControlExample() {
            Debug.Log("=== Custom Activity Control Example (Zero Allocation) ===");

            var activities = new List<IActivity> {
                new MessageActivity("Custom Task 1"),
                new MessageActivity("Custom Task 2")
            };

            // Execute activities with timeout
            var cts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, cts.Token);

            var executeTask = activityExecutor.ActivateAsync(activities, linkedCts.Token);
            var timeoutTask = UniTask.Delay(System.TimeSpan.FromSeconds(5f), cancellationToken: destroyCancellationToken);

            var completedTask = await UniTask.WhenAny(executeTask, timeoutTask);

            if (completedTask == 1) {
                Debug.Log("Timeout! Cancelling activities...");
                cts.Cancel();
            } else {
                // Deactivate when done
                await activityExecutor.DeactivateAsync(activities, destroyCancellationToken);
            }

            linkedCts.Dispose();
            cts.Dispose();

            Debug.Log("Custom control example completed!");
        }

        void OnDestroy()
        {
            // Dispose ActivityExecutor - this will cancel and dispose all managed activities
            activityExecutor?.Dispose();
            Debug.Log("ActivityExecutor disposed");
        }
    }
}