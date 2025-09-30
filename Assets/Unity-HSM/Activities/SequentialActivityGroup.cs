using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace HSM {
    public class SequentialActivityGroup : Activity {
        readonly List<IActivity> activities = new List<IActivity>();

        public void AddActivity(IActivity activity) {
            if (activity != null) activities.Add(activity);
        }

        public override async UniTask ActivateAsync(CancellationToken ct) {
            if (Mode != ActivityMode.Inactive) return;

            Mode = ActivityMode.Activating;

            for (int i = 0; i < activities.Count; i++) {
                await activities[i].ActivateAsync(ct);
            }

            Mode = ActivityMode.Active;
        }

        public override async UniTask DeactivateAsync(CancellationToken ct) {
            if (Mode != ActivityMode.Active) return;

            Mode = ActivityMode.Deactivating;

            // Deactivate in reverse order
            for (int i = activities.Count - 1; i >= 0; i--) {
                await activities[i].DeactivateAsync(ct);
            }

            Mode = ActivityMode.Inactive;
        }
    }
}