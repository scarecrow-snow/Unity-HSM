using System.Threading;
using Cysharp.Threading.Tasks;


namespace HSM {
    public enum ActivityMode { Inactive, Activating, Active, Deactivating }

    public interface IActivity {
        ActivityMode Mode { get; }
        UniTask ActivateAsync(CancellationToken ct);
        UniTask DeactivateAsync(CancellationToken ct);
    }

    public abstract class Activity : IActivity {
        public ActivityMode Mode { get; protected set; } = ActivityMode.Inactive;

        public virtual async UniTask ActivateAsync(CancellationToken ct) {
            if (Mode != ActivityMode.Inactive) return;
            
            Mode = ActivityMode.Activating;
            await UniTask.CompletedTask;
            Mode = ActivityMode.Active;
            //Debug.Log($"Activated {GetType().Name} (mode={Mode})");
        }

        public virtual async UniTask DeactivateAsync(CancellationToken ct) {
            if (Mode != ActivityMode.Active) return;
            
            Mode = ActivityMode.Deactivating;
            await UniTask.CompletedTask;
            Mode = ActivityMode.Inactive;
            //Debug.Log($"Deactivated {GetType().Name} (mode={Mode})");
        }
    }
}