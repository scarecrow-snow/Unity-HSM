using System;
using System.Collections.Generic;
using System.Threading;

using UnityUtils;


namespace HSM {
    public class TransitionSequencer {
        public readonly StateMachine Machine;
        public readonly ActivityExecutor ActivityExecutor;

        Action nextPhase;                    // switch structure between phases
        (State from, State to)? pending;     // coalesce a single pending request
        State lastFrom, lastTo;

        // Fields to avoid closure allocation in nextPhase lambda
        State transitionFrom, transitionTo, transitionLca;
        bool hasEnterPhase;

        CancellationTokenSource cts;

        // Pooled collections for zero-allocation transitions
        List<State> exitChainPool, enterChainPool;
        List<PhaseStep> exitStepsPool, enterStepsPool;

        public TransitionSequencer(StateMachine machine) {
            Machine = machine;
            ActivityExecutor = new ActivityExecutor();

            // Initialize nextPhase delegate to avoid closure allocation
            nextPhase = ExecuteEnterPhase;
        }

        // Request a transition from one state to another
        public void RequestTransition(State from, State to) {
            if (to == null || from == to) {
                return;
            }

            // Check for duplicate transition requests
            if (IsSameTransition(from, to)) {
                return;
            }

            if (ActivityExecutor.IsExecuting) {
                pending = (from, to);
                return;
            }
            BeginTransition(from, to);
        }

        
        // States to exit: from → ... up to (but excluding) lca; bottom→up order.
        static void StatesToExit(State from, State lca, List<State> result) {
            result.Clear();
            for (var s = from; s != null && s != lca; s = s.Parent) {
                result.Add(s);
            }
        }
        
        // States to enter: path from 'to' up to (but excluding) lca; returned in enter order (top→down).
        static void StatesToEnter(State to, State lca, List<State> result) {
            result.Clear();
            // Build the path in reverse order
            for (var s = to; s != lca; s = s.Parent) {
                result.Add(s);
            }
            // Reverse to get top→down order
            result.Reverse();
        }

        bool IsSameTransition(State from, State to) {
            // Check if currently transitioning with exact same from->to
            if (ActivityExecutor.IsExecuting && transitionFrom == from && transitionTo == to) {
                return true;
            }

            // Check if exact same transition is already pending
            if (pending.HasValue) {
                var (pendingFrom, pendingTo) = pending.Value;
                if (pendingFrom == from && pendingTo == to) {
                    return true;
                }
            }

            return false;
        }


        void BeginTransition(State from, State to) {

            cts?.Cancel();
            cts = new CancellationTokenSource();

            // Clean up any existing pooled collections from previous transition
            ReleasePooledCollections();

            var lca = Lca(from, to);
            //Debug.Log($"[TransitionSequencer] LCA: {lca?.GetType().Name}");

            // Get pooled collections for exit phase
            exitChainPool = TempCollectionPool<List<State>, State>.Get();
            exitStepsPool = TempCollectionPool<List<PhaseStep>, PhaseStep>.Get();

            StatesToExit(from, lca, exitChainPool);
            //Debug.Log($"[TransitionSequencer] States to exit: {exitChainPool.Count}");

            ActivityExecutor.GatherPhaseSteps(exitChainPool, deactivate: true, exitStepsPool);
            //Debug.Log($"[TransitionSequencer] Exit steps: {exitStepsPool.Count}");

            // 1. Deactivate the "old branch" using parallel execution
            ActivityExecutor.ExecutePhaseSteps(exitStepsPool, cts.Token);

            // Store transition parameters to avoid closure allocation
            transitionFrom = from;
            transitionTo = to;
            transitionLca = lca;
            hasEnterPhase = true;

            //Debug.Log($"[TransitionSequencer] BeginTransition complete, hasEnterPhase: {hasEnterPhase}");

            // Check if exit phase completed synchronously
            if (ActivityExecutor.AreTasksComplete() && hasEnterPhase) {
                //Debug.Log($"[TransitionSequencer] Exit phase completed synchronously, proceeding to enter phase");
                nextPhase();
            }
        }

        void ReleasePooledCollections() {
            if (exitChainPool != null) {
                TempCollectionPool<List<State>, State>.Release(exitChainPool);
                exitChainPool = null;
            }
            if (exitStepsPool != null) {
                TempCollectionPool<List<PhaseStep>, PhaseStep>.Release(exitStepsPool);
                exitStepsPool = null;
            }
            if (enterChainPool != null) {
                TempCollectionPool<List<State>, State>.Release(enterChainPool);
                enterChainPool = null;
            }
            if (enterStepsPool != null) {
                TempCollectionPool<List<PhaseStep>, PhaseStep>.Release(enterStepsPool);
                enterStepsPool = null;
            }
        }

        void ExecuteEnterPhase() {
            //Debug.Log($"[TransitionSequencer] ExecuteEnterPhase: {transitionFrom?.GetType().Name} -> {transitionTo?.GetType().Name}");

            // 2. ChangeState
            Machine.ChangeState(transitionFrom, transitionTo);

            // 3. Activate the "new branch"
            enterChainPool = TempCollectionPool<List<State>, State>.Get();
            enterStepsPool = TempCollectionPool<List<PhaseStep>, PhaseStep>.Get();

            StatesToEnter(transitionTo, transitionLca, enterChainPool);
            //Debug.Log($"[TransitionSequencer] States to enter: {enterChainPool.Count}");

            ActivityExecutor.GatherPhaseSteps(enterChainPool, deactivate: false, enterStepsPool);
            //Debug.Log($"[TransitionSequencer] Enter steps: {enterStepsPool.Count}");

            // Execute enter phase using parallel execution
            ActivityExecutor.ExecutePhaseSteps(enterStepsPool, cts.Token);
            hasEnterPhase = false; // Clear flag after execution

            //Debug.Log($"[TransitionSequencer] ExecuteEnterPhase complete, hasEnterPhase: {hasEnterPhase}");

            // Check if enter phase completed synchronously
            if (ActivityExecutor.AreTasksComplete()) {
                //Debug.Log($"[TransitionSequencer] Enter phase completed synchronously, ending transition");
                EndTransition();
            }
        }

        void EndTransition() {
            //Debug.Log($"[TransitionSequencer] EndTransition: clearing tasks");
            ActivityExecutor.Clear();
            ReleasePooledCollections();

            if (pending.HasValue) {
                (State from, State to) p = pending.Value;
                pending = null;
                //Debug.Log($"[TransitionSequencer] Processing pending transition: {p.from?.GetType().Name} -> {p.to?.GetType().Name}");
                BeginTransition(p.from, p.to);
            } else {
                //Debug.Log($"[TransitionSequencer] Transition complete, no pending transitions");
            }
        }

        public void Tick(float deltaTime) {
            if (ActivityExecutor.IsExecuting) {
                //Debug.Log($"[TransitionSequencer] Tick: tasks running, hasEnterPhase: {hasEnterPhase}");
                if (ActivityExecutor.AreTasksComplete()) {
                    if (hasEnterPhase) {
                        //Debug.Log($"[TransitionSequencer] Calling nextPhase (ExecuteEnterPhase)");
                        nextPhase();
                    } else {
                        //Debug.Log($"[TransitionSequencer] Calling EndTransition");
                        EndTransition();
                    }
                }
                return; // while transitioning, we don't run normal updates
            }
            Machine.InternalTick(deltaTime);
        }

        // Compute the Lowest Common Ancestor of two states.
        public static State Lca(State a, State b) {
            // Create a set of all parents of 'a'
            var ap = TempCollectionPool<HashSet<State>, State>.Get();
            try {
                for (var s = a; s != null; s = s.Parent) ap.Add(s);

                // Find the first parent of 'b' that is also a parent of 'a'
                for (var s = b; s != null; s = s.Parent)
                    if (ap.Contains(s))
                        return s;

                // If no common ancestor found, return null
                return null;
            } finally {
                TempCollectionPool<HashSet<State>, State>.Release(ap);
            }
        }
    }
}