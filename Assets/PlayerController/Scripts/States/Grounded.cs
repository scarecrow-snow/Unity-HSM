using UnityEngine;

namespace HSM {
    public class Grounded : State {
        readonly PlayerContext ctx;
        public readonly Idle Idle;
        public readonly Move Move;

        public Grounded(StateMachine m, State parent, PlayerContext ctx) : base(m, parent) {
            this.ctx = ctx;
            Idle = new Idle(m, this, ctx);
            Move = new Move(m, this, ctx);
            Add(new ColorPhaseActivity(ctx.renderer){
                enterColor = Color.yellow,  // runs while Grounded is activating
            });
        }
        
        protected override State GetInitialState() => Idle;

        protected override State GetTransition() {
            if (ctx.jumpPressed) {
                ctx.jumpPressed = false;
                var rb = ctx.rb;

                if (rb != null) {
                    var v = rb.linearVelocity;
                    v.y = ctx.jumpSpeed;
                    rb.linearVelocity = v;
                }
                return ((PlayerRoot)Parent).Airborne;
            }
            return ctx.grounded ? null : ((PlayerRoot)Parent).Airborne;
        }
    }
}