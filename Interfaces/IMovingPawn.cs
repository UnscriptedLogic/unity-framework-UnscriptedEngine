using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IMovingPawn
{
    public bool IsMoving { get; }
    public MovementSettings MoveSettings { get; }
}
