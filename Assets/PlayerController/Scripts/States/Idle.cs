
using System.Linq;
using UnityEngine;


namespace HSM {
    public class Idle : State {
        readonly PlayerContext ctx;

        public Idle(StateMachine m, State parent, PlayerContext ctx) : base(m, parent)
        {
            this.ctx = ctx;
            var Sequence = new SequentialActivityGroup();
            Sequence.AddActivity(new MessageActivity("Idle 1"));
            Sequence.AddActivity(new MessageActivity("Idle 2"));
            Add(Sequence);
            
        }

        protected override State GetTransition() {
            return Mathf.Abs(ctx.move.x) > 0.01f ? ((Grounded)Parent).Move : null;
        }

        protected override void OnEnter() {
            ctx.velocity.x = 0f;
            
       }
    }
}