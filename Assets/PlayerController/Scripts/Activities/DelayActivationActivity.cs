using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HSM {
    public class DelayActivationActivity : Activity {
        public float seconds = 0.2f;
     
        public override async UniTask ActivateAsync(CancellationToken ct) {
            //Debug.Log($"Activating {GetType().Name} (mode={this.Mode}) after {seconds} seconds");
            await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct);
            await base.ActivateAsync(ct);
        }
    }
}