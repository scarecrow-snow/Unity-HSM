
using System.Linq;
using UnityEngine;


namespace HSM
{
    public class Idle : State
    {
        readonly PlayerContext ctx;

        public Idle(StateMachine m, State parent, PlayerContext ctx) : base(m, parent)
        {
            this.ctx = ctx;
            var Sequence = new SequentialActivityGroup();
            Sequence.AddActivity(new MessageActivity("Idle 1"));
            Sequence.AddActivity(new DelayActivationActivity(1f));
            Sequence.AddActivity(new MessageActivity("Idle 2"));
            Add(Sequence);
            Add(new MessageActivity("Idle 3"));

        }

        protected override State GetTransition()
        {
            return Mathf.Abs(ctx.move.x) > 0.01f ? ((Grounded)Parent).Move : null;
        }

        protected override void OnEnter()
        {
            ctx.velocity.x = 0f;
        }

        public override void Dispose()
        {
            Debug.Log("Disposed Idle State");
        }

    }
}