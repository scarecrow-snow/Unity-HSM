using System;
using System.Collections.Generic;
using UnityUtils;

namespace HSM
{
    public class StateMachine : IDisposable
    {
        public readonly State Root;
        public readonly TransitionSequencer Sequencer;
        bool started;
        bool disposed;

        public StateMachine(State root)
        {
            Root = root;
            Sequencer = new TransitionSequencer(this);
        }

        public void Start()
        {
            if (started) return;

            started = true;

            // TransitionSequencerを使用して初期遷移を実行
            // Root.Start()ではなく、ここで行うことで、初期遷移もSequencerの管理下に置く
            State initialLeaf = Root;
            while (initialLeaf.InternalGetInitialState() != null)
            {
                initialLeaf = initialLeaf.InternalGetInitialState();
            }
            Sequencer.RequestTransition(null, initialLeaf);
        }

        public void Tick(float deltaTime)
        {
            if (!started) Start();
            Sequencer.Tick(deltaTime);
        }

        internal void InternalTick(float deltaTime) => Root.Update(deltaTime);

        // Perform the actual switch from 'from' to 'to' by exiting up to the shared ancestor, then entering down to the target.
        // Note: This is called by TransitionSequencer, which handles Activity activation/deactivation separately
        public void ChangeState(State from, State to)
        {
            //UnityEngine.Debug.Log($"[ChangeState] START: {from?.GetType().Name} -> {to?.GetType().Name}");
            if (from == to || to == null) return;

            State lca = from == null ? null : TransitionSequencer.Lca(from, to);
            //UnityEngine.Debug.Log($"[ChangeState] LCA: {lca?.GetType().Name}");

            // Exit current branch up to (but not including) LCA
            if (from != null)
            {
                for (State s = from; s != lca; s = s.Parent) {
                    //UnityEngine.Debug.Log($"[ChangeState] Exiting: {s.GetType().Name}");
                    // ActiveChildのクリアとOnExitのみ実行（再帰的なExitは行わない）
                    s.ActiveChild = null;
                    s.OnExitInternal();
                }
            }

            //UnityEngine.Debug.Log($"[ChangeState] Exit complete, entering...");

            // toから GetInitialState() を辿ってリーフまで到達
            State targetLeaf = to;
            while (targetLeaf != null && targetLeaf.InternalGetInitialState() != null)
            {
                targetLeaf = targetLeaf.InternalGetInitialState();
            }

            // Enter target branch from LCA down to target leaf using pooled stack
            using (var scope = TempStackPool<State>.GetScoped())
            {
                var stack = scope.Value;
                for (State s = targetLeaf; s != null && s != lca; s = s.Parent) stack.Push(s);
                while (stack.Count > 0) {
                    var state = stack.Pop();
                    //UnityEngine.Debug.Log($"[ChangeState] Entering: {state.GetType().Name}");
                    // ActiveChildの設定とOnEnterのみ実行（再帰的なEnterは行わない）
                    if (state.Parent != null) state.Parent.ActiveChild = state;
                    state.OnEnterInternal();
                }
            }
            //UnityEngine.Debug.Log($"[ChangeState] COMPLETE");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            // Dispose all states in the tree (will be set by StateMachineBuilder)
            DisposeAllStates();
        }

        // This will be assigned by StateMachineBuilder during Build()
        internal Action DisposeAllStates = () => { };
    }
}
