using System;

public abstract class NetworkedBaseState<EState> where EState : Enum
{
    public EState StateKey { get; private set; }

    public NetworkedBaseState(EState stateKey)
    {
        StateKey = stateKey;
    }

    public abstract void EnterState();
    public abstract void ExitState();
    public abstract void UpdateState();
    public abstract EState GetNextState();
}
