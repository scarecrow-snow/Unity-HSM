using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace HSM {
    public class ColorPhaseActivity : Activity {
        readonly Renderer renderer;
        readonly Material mat; // cached instance to avoid repeated .material allocations

        public Color enterColor = Color.red;
        public Color exitColor  = Color.yellow;

        public ColorPhaseActivity(Renderer r){
            renderer = r;
            if (renderer != null){
                mat = renderer.material; // clone once per activity/state
            }
        }

        public override UniTask ActivateAsync(CancellationToken ct){
            if (this.Mode != ActivityMode.Inactive || mat == null) return UniTask.CompletedTask;
            this.Mode = ActivityMode.Activating;
            mat.color = enterColor;
            this.Mode = ActivityMode.Active;
            return UniTask.CompletedTask;
        }

        public override UniTask DeactivateAsync(CancellationToken ct){
            if (this.Mode != ActivityMode.Active || mat == null) return UniTask.CompletedTask;
            this.Mode = ActivityMode.Deactivating;
            mat.color = exitColor;
            this.Mode = ActivityMode.Inactive;
            return UniTask.CompletedTask;
        }
    }
}