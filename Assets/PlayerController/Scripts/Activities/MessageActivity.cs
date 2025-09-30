using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace HSM
{
    public class MessageActivity : Activity
    {
        public string Message { get; set; }

        public MessageActivity(string message)
        {
            Message = message;
        }
        public override async UniTask ActivateAsync(CancellationToken ct)
        {
            Mode = ActivityMode.Activating;
            Debug.Log(Message);
            await base.ActivateAsync(ct);
            Mode = ActivityMode.Active;
        }

        public override async UniTask DeactivateAsync(CancellationToken ct)
        {
            Mode = ActivityMode.Deactivating;
            await base.DeactivateAsync(ct);
            Mode = ActivityMode.Inactive;
        }
    }
}