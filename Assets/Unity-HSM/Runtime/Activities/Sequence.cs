using System.Threading;
using Cysharp.Threading.Tasks;

namespace HSM {
    // One activity operation (activate OR deactivate) to run for this phase.
    public readonly struct PhaseStep {
        readonly IActivity activity;
        readonly bool isDeactivate;

        public PhaseStep(IActivity activity, bool isDeactivate) {
            this.activity = activity;
            this.isDeactivate = isDeactivate;
        }

        public UniTask Execute(CancellationToken ct) {
            return isDeactivate ? activity.DeactivateAsync(ct) : activity.ActivateAsync(ct);
        }
    }
}