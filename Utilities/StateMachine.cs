using System;
using System.Collections.Generic;
using UnityEngine;

public class StateMachine<EState> : MonoBehaviour where EState : Enum
{
    protected Dictionary<EState, BaseState<EState>> States = new Dictionary<EState, BaseState<EState>>();

    protected BaseState<EState> CurrentState;

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
