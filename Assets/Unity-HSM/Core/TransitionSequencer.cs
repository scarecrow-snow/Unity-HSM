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
        

        // Fields to avoid closure allocation in nextPhase lambda
        State transitionFrom, transitionTo, transitionLca;
        bool hasEnterPhase;

        CancellationTokenSource cts;

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

            var lca = Lca(from, to);
            //Debug.Log($"[TransitionSequencer] LCA: {lca?.GetType().Name}");

            // Use temporary pooled collections for exit phase preparation
            using (var exitChainScope = TempCollectionPool<List<State>, State>.GetScoped())
            using (var exitStepsScope = TempCollectionPool<List<PhaseStep>, PhaseStep>.GetScoped()) {
                var exitChain = exitChainScope.Collection;
                var exitSteps = exitStepsScope.Collection;

                StatesToExit(from, lca, exitChain);
                //Debug.Log($"[TransitionSequencer] States to exit: {exitChain.Count}");

                ActivityExecutor.GatherPhaseSteps(exitChain, deactivate: true, exitSteps);
                //Debug.Log($"[TransitionSequencer] Exit steps: {exitSteps.Count}");

                // 1. Deactivate the "old branch" using parallel execution
                ActivityExecutor.ExecutePhaseSteps(exitSteps, cts.Token);
            }

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

        void ExecuteEnterPhase() {
            //Debug.Log($"[TransitionSequencer] ExecuteEnterPhase: {transitionFrom?.GetType().Name} -> {transitionTo?.GetType().Name}");

            // 2. ChangeState
            Machine.ChangeState(transitionFrom, transitionTo);

            // 3. Activate the "new branch"
            using (var enterChainScope = TempCollectionPool<List<State>, State>.GetScoped())
            using (var enterStepsScope = TempCollectionPool<List<PhaseStep>, PhaseStep>.GetScoped()) {
                var enterChain = enterChainScope.Collection;
                var enterSteps = enterStepsScope.Collection;

                StatesToEnter(transitionTo, transitionLca, enterChain);
                //Debug.Log($"[TransitionSequencer] States to enter: {enterChain.Count}");

                ActivityExecutor.GatherPhaseSteps(enterChain, deactivate: false, enterSteps);
                //Debug.Log($"[TransitionSequencer] Enter steps: {enterSteps.Count}");

                // Execute enter phase using parallel execution
                ActivityExecutor.ExecutePhaseSteps(enterSteps, cts.Token);
            }

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
            using (var scope = TempCollectionPool<HashSet<State>, State>.GetScoped()) {
                var ap = scope.Collection;
                for (var s = a; s != null; s = s.Parent) ap.Add(s);

                // Find the first parent of 'b' that is also a parent of 'a'
                for (var s = b; s != null; s = s.Parent)
                    if (ap.Contains(s))
                        return s;

                // If no common ancestor found, return null
                return null;
            }
        }
    }
}