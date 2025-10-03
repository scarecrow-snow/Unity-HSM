using UnityEngine;

namespace HSM
{
    /// <summary>
    /// Interface for components that contain a StateMachine.
    /// Implement this interface to make any MonoBehaviour compatible with HSM Editor tools.
    /// </summary>
    public interface IStateMachineProvider
    {
        StateMachine Machine { get; }
    }

}
