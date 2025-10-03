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

        async UniTask ExecuteActivitiesParallelExample() {
            Debug.Log("=== Parallel Activity Execution Example (Zero Allocation) ===");

            // Use List<T> which implements IReadOnlyList<T> - no allocation overhead
            var activities = new List<IActivity> {
                new MessageActivity("Parallel Task 1"),
                new MessageActivity("Parallel Task 2"),
                new MessageActivity("Parallel Task 3")
            };

            // Execute all activities in parallel (zero allocation iteration)
            activityExecutor.Execute(activities, isDeactivate: false, destroyCancellationToken);

            // Wait for completion with timeout protection
            float timeout = 10f;
            float startTime = Time.time;
            while (activityExecutor.IsExecuting) {
                if (Time.time - startTime > timeout) {
                    Debug.LogWarning("Parallel execution timeout!");
                    activityExecutor.Cancel();
                    break;
                }
                await UniTask.Yield(destroyCancellationToken);
            }

            Debug.Log("Parallel execution completed!");
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
            

            // Execute all activities sequentially (zero allocation iteration)
            activityExecutor.Execute(sequens, isDeactivate: false, destroyCancellationToken);

            Debug.Log("Sequential execution completed!");
            await UniTask.CompletedTask;
        }

        async UniTask CustomActivityControlExample() {
            Debug.Log("=== Custom Activity Control Example (Zero Allocation) ===");

            var activities = new List<IActivity> {
                new MessageActivity("Custom Task 1"),
                new MessageActivity("Custom Task 2")
            };

            // Start execution (zero allocation)
            activityExecutor.Execute(activities, isDeactivate: false, destroyCancellationToken);

            // Monitor progress
            float startTime = Time.time;
            while (activityExecutor.IsExecuting) {
                if (Time.time - startTime > 5f) {
                    Debug.Log("Timeout! Cancelling activities...");
                    activityExecutor.Cancel();
                    break;
                }
                await UniTask.Yield(destroyCancellationToken);
            }

            Debug.Log("Custom control example completed!");
        }

        void OnDestroy() {
            // Clean up - destroyCancellationToken handles MonoBehaviour lifecycle automatically
            activityExecutor?.Cancel();
        }
    }
}