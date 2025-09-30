using System.Collections.Generic;

namespace HSM {
    public class StateMachine {
        public readonly State Root;
        public readonly TransitionSequencer Sequencer;
        readonly Stack<State> transitionStack;
        bool started;

        public StateMachine(State root) {
            Root = root;
            Sequencer = new TransitionSequencer(this);
            transitionStack = new Stack<State>();
        }

        public void Start() {
            if (started) return;
            
            started = true;
            Root.Enter();
        }

        public void Tick(float deltaTime) {
            if (!started) Start();
            Sequencer.Tick(deltaTime);
        }
        
        internal void InternalTick(float deltaTime) => Root.Update(deltaTime);
        
        // Perform the actual switch from 'from' to 'to' by exiting up to the shared ancestor, then entering down to the target.
        public void ChangeState(State from, State to) {
            if (from == to || from == null || to == null) return;
            
            State lca = TransitionSequencer.Lca(from, to);
            
            // Exit current branch up to (but not including) LCA
            for (State s = from; s != lca; s = s.Parent) s.Exit();
            
            // Enter target branch from LCA down to target
            transitionStack.Clear();
            for (State s = to; s != lca; s = s.Parent) transitionStack.Push(s);
            while (transitionStack.Count > 0) transitionStack.Pop().Enter();
        }
    }
}