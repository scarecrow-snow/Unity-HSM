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
        public override UniTask ActivateAsync(CancellationToken ct)
        {
            if (Mode != ActivityMode.Inactive) return UniTask.CompletedTask;

            Mode = ActivityMode.Activating;
            Debug.Log($"Activateing MessageActivity: {Message}");
            Mode = ActivityMode.Active;

            return UniTask.CompletedTask;
        }

        public override UniTask DeactivateAsync(CancellationToken ct)
        {
            if (Mode != ActivityMode.Active) return UniTask.CompletedTask;

            Mode = ActivityMode.Deactivating;
            Debug.Log($"Deactivating MessageActivity: {Message}");
            Mode = ActivityMode.Inactive;

            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            Debug.Log($"Disposed MessageActivity: {Message}");
        }
    }
}