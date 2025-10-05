using Cysharp.Threading.Tasks;

namespace HSM {
    public class PlayerRoot : State {
        public readonly Grounded Grounded;
        public readonly Airborne Airborne;
        readonly PlayerContext ctx;

        public PlayerRoot(StateMachine m, PlayerContext ctx) : base(m, null)
        {
            this.ctx = ctx;
            Grounded = new Grounded(m, this, ctx);
            Airborne = new Airborne(m, this, ctx);
            Add(new MessageActivity("PlayerRoot"));
        }
        
        protected override State GetInitialState() => Grounded;

        protected override State GetTransition()
        {
            if (ctx.isJumping) return null;
            
            return ctx.grounded ? null : Airborne;
        }
    }
}