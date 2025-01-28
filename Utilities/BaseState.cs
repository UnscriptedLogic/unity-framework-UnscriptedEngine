using System;
using UnityEngine;

public abstract class BaseState<EState> where EState : Enum
{
    public EState StateKey { get; private set; }

    public BaseState(EState stateKey)
    {
        StateKey = stateKey;
    }

    public abstract void EnterState();
    public abstract void ExitState();
    public abstract void UpdateState();
    public abstract EState GetNextState();
}
