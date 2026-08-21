using System;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(UButtonComponent))]
public class NetworkSeat : NetworkBehaviour
{
    [Header("Seat Pose")]
    [SerializeField] private Transform sittingPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField, Min(0f)] private float maximumUseDistance = 3f;

    private readonly NetworkVariable<NetworkObjectReference> occupantReference =
        new NetworkVariable<NetworkObjectReference>(
            new NetworkObjectReference((NetworkObject)null),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public event Action<NetworkSittingComponent> OccupantChanged;

    public Transform SittingPoint => sittingPoint != null ? sittingPoint : transform;
    public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
    public bool IsOccupied => TryGetOccupant(out _);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        occupantReference.OnValueChanged += HandleOccupantChanged;
        NotifyOccupantChanged();
    }

    public override void OnNetworkDespawn()
    {
        occupantReference.OnValueChanged -= HandleOccupantChanged;

        if (IsServer && TryGetOccupant(out NetworkSittingComponent occupant))
        {
            occupant.ForceStandServer(this);
            occupantReference.Value = new NetworkObjectReference((NetworkObject)null);
        }

        base.OnNetworkDespawn();
    }

    internal bool TryOccupyServer(NetworkSittingComponent sitter)
    {
        if (!IsServer || sitter == null || IsOccupied || sitter.IsSitting)
        {
            return false;
        }

        if (Vector3.Distance(sitter.transform.position, SittingPoint.position) > maximumUseDistance)
        {
            return false;
        }

        occupantReference.Value = sitter.NetworkObject;
        sitter.SitServer(this);
        return true;
    }

    internal bool TryVacateServer(NetworkSittingComponent sitter)
    {
        if (!IsServer || sitter == null || !TryGetOccupant(out NetworkSittingComponent occupant) || occupant != sitter)
        {
            return false;
        }

        occupantReference.Value = new NetworkObjectReference((NetworkObject)null);
        sitter.StandServer(this);
        return true;
    }

    public bool TryGetOccupant(out NetworkSittingComponent occupant)
    {
        occupant = null;

        return occupantReference.Value.TryGet(out NetworkObject occupantObject) &&
               occupantObject != null &&
               occupantObject.TryGetComponent(out occupant);
    }

    private void HandleOccupantChanged(NetworkObjectReference previous, NetworkObjectReference current)
    {
        NotifyOccupantChanged();
    }

    private void NotifyOccupantChanged()
    {
        TryGetOccupant(out NetworkSittingComponent occupant);
        OccupantChanged?.Invoke(occupant);
    }

    private void OnDrawGizmosSelected()
    {
        Transform seat = SittingPoint;
        Transform exit = ExitPoint;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(seat.position, 0.15f);
        Gizmos.DrawRay(seat.position, seat.forward * 0.5f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(exit.position, 0.15f);
        Gizmos.DrawRay(exit.position, exit.forward * 0.5f);
    }
}
