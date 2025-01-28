using UnityEngine;
using static UnscriptedEngine.UObject;

public interface IUsesCamera
{
    public Camera CurrentCam { get; }
    public Bindable<GameObject> CurrentlyLookingAt { get; }
    public Bindable<Vector3> CurrentlyLookingPoint { get; }
    public Bindable<RaycastHit> CurrentlyLookingHit { get; }
    public Bindable<Vector3> CurrentlyLookingPointLimitless { get; }
}