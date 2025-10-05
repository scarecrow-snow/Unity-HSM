using Unity.VisualScripting;
using UnityEngine;

namespace HSM {
    public class Airborne : State
    {
        readonly PlayerContext ctx;

        public Airborne(StateMachine m, State parent, PlayerContext ctx) : base(m, parent)
        {
            this.ctx = ctx;
            Add(new ColorPhaseActivity(ctx.renderer)
            {
                enterColor = Color.red, // runs while Airborne is activating
            });
        }

        protected override State GetTransition()
        {
            // Don't transition back to Grounded while jumping (velocity is upward)
            if (ctx.rb != null && ctx.rb.linearVelocity.y > 0.1f) return null;

            return ctx.grounded ? ((PlayerRoot)Parent).Grounded : null;
        } 

        protected override void OnEnter()
        {
            // TODO: Update Animator through ctx.anim
            ctx.grounded = false;
            ctx.isJumping = true;
        }

        protected override void OnExit()
        {
            ctx.isJumping = false;
        }
        
        public override void Dispose()
        {
            Debug.Log("Disposing Airborne");
        }
    }
}