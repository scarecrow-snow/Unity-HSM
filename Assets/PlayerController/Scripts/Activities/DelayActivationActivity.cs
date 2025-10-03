using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HSM {
    public class DelayActivationActivity : Activity
    {
        TimeSpan delay;
        public DelayActivationActivity(float seconds = 0.2f)
        {
            delay = TimeSpan.FromSeconds(seconds);
        }

        
        public override async UniTask ActivateAsync(CancellationToken ct) {
            //Debug.Log($"Activating {GetType().Name} (mode={this.Mode}) after {seconds} seconds");
            await UniTask.Delay(delay, cancellationToken: ct);
            await base.ActivateAsync(ct);
        }
    }
}