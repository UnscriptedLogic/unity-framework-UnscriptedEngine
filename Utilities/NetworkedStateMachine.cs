using System;
using System.Collections.Generic;
using Unity.Netcode;

public class NetworkedStateMachine<EState> : NetworkBehaviour where EState : Enum
{
    protected Dictionary<EState, NetworkedBaseState<EState>> States = new Dictionary<EState, NetworkedBaseState<EState>>();

    protected NetworkedBaseState<EState> CurrentState;

    private bool isTransitioning;

    protected virtual void Start()
    {
        CurrentState.EnterState();
    }

    protected virtual void Update()
    {
        EState nextStateKey = CurrentState.GetNextState();
        if (!isTransitioning && nextStateKey.Equals(CurrentState.StateKey))
        {
            CurrentState.UpdateState();
        }
        else if (!isTransitioning)
        {
            TransitionToState(nextStateKey);
        }
    }

    private void TransitionToState(EState nextState)
    {
        isTransitioning = true;
        CurrentState.ExitState();
        CurrentState = States[nextState];
        CurrentState.EnterState();
        isTransitioning = false;
    }
}
