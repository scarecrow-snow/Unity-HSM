using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityUtils;

namespace HSM {
    public class ActivityExecutor {
        List<UniTask> runningTasks = new List<UniTask>();
        CancellationTokenSource currentCts;

        public bool IsExecuting {
            get {
                if (runningTasks.Count == 0) return false;

                // Automatically check and clear completed tasks
                AreTasksComplete();

                return runningTasks.Count > 0;
            }
        }

        /// <summary>
        /// Execute a collection of activities in parallel (zero allocation)
        /// </summary>
        public void ExecuteActivitiesParallel(IReadOnlyList<IActivity> activities, bool isDeactivate, CancellationToken ct = default) {
            Clear();

            currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            for (int i = 0; i < activities.Count; i++) {
                var activity = activities[i];
                bool shouldExecute = isDeactivate ? (activity.Mode == ActivityMode.Active)
                                                 : (activity.Mode == ActivityMode.Inactive);
                if (!shouldExecute) continue;

                var task = isDeactivate ? activity.DeactivateAsync(currentCts.Token)
                                       : activity.ActivateAsync(currentCts.Token);
                runningTasks.Add(task);
            }
        }

        /// <summary>
        /// Execute activities from states using the phase step pattern (for TransitionSequencer)
        /// </summary>
        public void ExecutePhaseSteps(List<PhaseStep> steps, CancellationToken ct = default) {
            Clear();

            currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            for (int i = 0; i < steps.Count; i++) {
                runningTasks.Add(steps[i].Execute(currentCts.Token));
            }
        }

        /// <summary>
        /// Execute a collection of activities sequentially (zero allocation)
        /// </summary>
        public async UniTask ExecuteActivitiesSequentialAsync(IReadOnlyList<IActivity> activities, bool isDeactivate, CancellationToken ct = default) {
            Clear();

            currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            for (int i = 0; i < activities.Count; i++) {
                var activity = activities[i];
                bool shouldExecute = isDeactivate ? (activity.Mode == ActivityMode.Active)
                                                 : (activity.Mode == ActivityMode.Inactive);
                if (!shouldExecute) continue;

                var task = isDeactivate ? activity.DeactivateAsync(currentCts.Token)
                                       : activity.ActivateAsync(currentCts.Token);
                await task;
            }
        }

        /// <summary>
        /// Gather phase steps from state chain (extracted from TransitionSequencer)
        /// </summary>
        public static void GatherPhaseSteps(IReadOnlyList<State> chain, bool deactivate, List<PhaseStep> result) {
            result.Clear();
            for (int i = 0; i < chain.Count; i++) {
                var st = chain[i];
                var acts = st.Activities;
                for (int j = 0; j < acts.Count; j++) {
                    var a = acts[j];
                    bool include = deactivate ? (a.Mode == ActivityMode.Active)
                        : (a.Mode == ActivityMode.Inactive);
                    if (!include) continue;

                    result.Add(new PhaseStep(a, deactivate));
                }
            }
        }

        /// <summary>
        /// Gather phase steps from state chain (List overload for TransitionSequencer compatibility)
        /// </summary>
        public static void GatherPhaseSteps(List<State> chain, bool deactivate, List<PhaseStep> result) {
            // Delegate to the IReadOnlyList version (List<T> implements IReadOnlyList<T>)
            GatherPhaseSteps((IReadOnlyList<State>)chain, deactivate, result);
        }

        /// <summary>
        /// Check if all running tasks are complete
        /// </summary>
        public bool AreTasksComplete() {
            if (runningTasks.Count == 0) return true;

            bool allCompleted = runningTasks.TrueForAll(t => t.GetAwaiter().IsCompleted);

            // If all tasks completed synchronously, clear them immediately
            if (allCompleted) {
                runningTasks.Clear();
            }

            return allCompleted;
        }

        /// <summary>
        /// Cancel all running tasks
        /// </summary>
        public void Cancel() {
            currentCts?.Cancel();
            Clear();
        }

        /// <summary>
        /// Clear all running tasks
        /// </summary>
        public void Clear() {
            runningTasks.Clear();
            currentCts?.Dispose();
            currentCts = null;
        }

        /// <summary>
        /// Execute activities from a state using zero allocation (for external use with pooled collections)
        /// </summary>
        public void ExecuteStateActivitiesParallel(State state, bool isDeactivate, CancellationToken ct = default) {
            ExecuteActivitiesParallel(state.Activities, isDeactivate, ct);
        }

        /// <summary>
        /// Execute activities from a state sequentially using zero allocation
        /// </summary>
        public async UniTask ExecuteStateActivitiesSequentialAsync(State state, bool isDeactivate, CancellationToken ct = default) {
            await ExecuteActivitiesSequentialAsync(state.Activities, isDeactivate, ct);
        }

        /// <summary>
        /// Execute activities from multiple states using pooled collections (zero allocation)
        /// </summary>
        public void ExecuteMultipleStatesActivitiesParallel(IReadOnlyList<State> states, bool isDeactivate, CancellationToken ct = default) {
            Clear();

            currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            // Use pooled collection for gathering activities
            var tempSteps = TempCollectionPool<List<PhaseStep>, PhaseStep>.Get();
            try {
                GatherPhaseSteps(states, isDeactivate, tempSteps);
                ExecutePhaseSteps(tempSteps, ct);
            } finally {
                TempCollectionPool<List<PhaseStep>, PhaseStep>.Release(tempSteps);
            }
        }
    }
}