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


        public override async UniTask ActivateAsync(CancellationToken ct)
        {
            if (Mode != ActivityMode.Inactive) return;

            Mode = ActivityMode.Activating;
            await UniTask.Delay(delay, cancellationToken: ct);
            Mode = ActivityMode.Active;
        }
    }
}