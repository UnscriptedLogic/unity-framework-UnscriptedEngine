using System;
using Framework.Components;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterMovementComponent))]
public class NetworkSittingComponent : NetworkBehaviour
{
    private readonly NetworkVariable<NetworkObjectReference> seatReference =
        new NetworkVariable<NetworkObjectReference>(
            new NetworkObjectReference((NetworkObject)null),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private CharacterController characterController;
    private CharacterMovementComponent movementComponent;
    private NetworkSeat currentSeat;

    public event Action<NetworkSeat> SittingChanged;

    public bool IsSitting => currentSeat != null;
    public NetworkSeat CurrentSeat => currentSeat;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        movementComponent = GetComponent<CharacterMovementComponent>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        seatReference.OnValueChanged += HandleSeatChanged;
        ApplySeatReference(default, seatReference.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && currentSeat != null)
        {
            currentSeat.TryVacateServer(this);
        }

        seatReference.OnValueChanged -= HandleSeatChanged;
        RestoreStandingState(currentSeat);
        base.OnNetworkDespawn();
    }

    private void LateUpdate()
    {
        if (currentSeat == null)
        {
            NetworkSeat resolvedSeat = ResolveSeat(seatReference.Value);
            if (resolvedSeat != null)
            {
                ApplySeatReference(default, seatReference.Value);
            }
        }

        if (currentSeat != null)
        {
            SnapTo(currentSeat.SittingPoint);
        }
    }

    public void RequestSit(NetworkSeat seat)
    {
        if (!IsOwner || seat == null || !seat.IsSpawned)
        {
            return;
        }

        RequestSitRpc(seat.NetworkObject);
    }

    public void RequestStand()
    {
        if (IsOwner && IsSitting)
        {
            RequestStandRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestSitRpc(NetworkObjectReference requestedSeatReference)
    {
        if (!requestedSeatReference.TryGet(out NetworkObject seatObject) ||
            seatObject == null ||
            !seatObject.TryGetComponent(out NetworkSeat seat))
        {
            return;
        }

        seat.TryOccupyServer(this);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestStandRpc()
    {
        currentSeat?.TryVacateServer(this);
    }

    internal void SitServer(NetworkSeat seat)
    {
        if (IsServer && seat != null)
        {
            seatReference.Value = seat.NetworkObject;
        }
    }

    internal void StandServer(NetworkSeat seat)
    {
        if (IsServer && currentSeat == seat)
        {
            seatReference.Value = new NetworkObjectReference((NetworkObject)null);
        }
    }

    internal void ForceStandServer(NetworkSeat seat)
    {
        StandServer(seat);
    }

    private void HandleSeatChanged(NetworkObjectReference previous, NetworkObjectReference current)
    {
        ApplySeatReference(previous, current);
    }

    private void ApplySeatReference(NetworkObjectReference previous, NetworkObjectReference current)
    {
        NetworkSeat previousSeat = ResolveSeat(previous);
        NetworkSeat nextSeat = ResolveSeat(current);

        currentSeat = nextSeat;

        if (nextSeat != null)
        {
            movementComponent.SetMovementInput(Vector2.zero);
            movementComponent.StopMovementImmediately();
            characterController.enabled = false;
            SnapTo(nextSeat.SittingPoint);
        }
        else
        {
            RestoreStandingState(previousSeat);
        }

        SittingChanged?.Invoke(currentSeat);
    }

    private void RestoreStandingState(NetworkSeat previousSeat)
    {
        if (previousSeat != null)
        {
            SnapTo(previousSeat.ExitPoint);
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }

    private void SnapTo(Transform target)
    {
        if (target != null)
        {
            transform.SetPositionAndRotation(target.position, target.rotation);
        }
    }

    private static NetworkSeat ResolveSeat(NetworkObjectReference reference)
    {
        return reference.TryGet(out NetworkObject seatObject) &&
               seatObject != null &&
               seatObject.TryGetComponent(out NetworkSeat seat)
            ? seat
            : null;
    }
}
