namespace UnscriptedEngine {
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Inspector-friendly event bridge for connecting interactive components.
/// For example: Lever.OnValueChanged -> Emitter.Emit -> Gauge.SetValue.
/// </summary>
public class SignalEmitter : MonoBehaviour
{
    [System.Serializable]
    public class FloatEvent : UnityEvent<float>
    {
    }

    [Tooltip("Receives and rebroadcasts normalized values such as a lever or gauge value.")]
    public FloatEvent OnValueEmitted = new();

    [Tooltip("Receives signals that do not carry a value.")]
    public UnityEvent OnEmitted = new();

    /// <summary>
    /// Receives a value from another component and broadcasts it to listeners.
    /// </summary>
    public void Emit(float value)
    {
        OnValueEmitted.Invoke(value);
    }

    /// <summary>
    /// Broadcasts a signal to listeners that do not require a value.
    /// </summary>
    public void Emit()
    {
        OnEmitted.Invoke();
    }
}

}
