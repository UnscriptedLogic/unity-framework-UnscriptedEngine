using System;
using Unity.Netcode;

namespace UnscriptedEngine
{
    /// <summary>
    /// Enum state machine supporting local state or server-authoritative network state.
    /// For network state, declare the backing NetworkVariable
    /// directly on the owning NetworkBehaviour so NGO discovers and replicates it,
    /// including its current value for late joiners. Construct in Awake and dispose
    /// in OnDestroy. Use an int-backed enum and a server-write/everyone-read variable.
    /// </summary>
    public sealed class UStateMachine<T> : IDisposable where T : struct, Enum
    {
        private readonly NetworkBehaviour owner;
        private readonly NetworkVariable<int> state;
        private readonly Func<T, T, bool> canTransition;
        private T localState;
        public T Current => state == null ? localState : (T)Enum.ToObject(typeof(T), state.Value);
        public event Action<T, T> StateChanged;

        /// <summary>Create a local state machine without network ownership requirements.</summary>
        public UStateMachine(T initialState, Func<T, T, bool> canTransition = null)
        {
            if (!Enum.IsDefined(typeof(T), initialState))
                throw new ArgumentOutOfRangeException(nameof(initialState));
            localState = initialState;
            this.canTransition = canTransition;
        }

        public UStateMachine(NetworkBehaviour owner, NetworkVariable<int> replicatedState,
            Func<T, T, bool> canTransition = null)
        {
            if (Enum.GetUnderlyingType(typeof(T)) != typeof(int))
                throw new ArgumentException("State enums must use int as their underlying type.");
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            state = replicatedState ?? throw new ArgumentNullException(nameof(replicatedState));
            this.canTransition = canTransition;
            state.OnValueChanged += HandleChanged;
        }

        public bool TryTransition(T next)
        {
            if ((state != null && (!owner.IsSpawned || !owner.IsServer)) || !Enum.IsDefined(typeof(T), next)
                || Current.Equals(next) || (canTransition != null && !canTransition(Current, next)))
                return false;
            if (state != null) state.Value = Convert.ToInt32(next);
            else
            {
                T previous = localState;
                localState = next;
                StateChanged?.Invoke(previous, next);
            }
            return true;
        }

        private void HandleChanged(int previous, int next) => StateChanged?.Invoke(
            (T)Enum.ToObject(typeof(T), previous), (T)Enum.ToObject(typeof(T), next));

        public void Dispose()
        {
            if (state != null) state.OnValueChanged -= HandleChanged;
            StateChanged = null;
        }
    }
}
