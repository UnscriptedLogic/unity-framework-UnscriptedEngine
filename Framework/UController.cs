using Unity.Netcode;
using UnityEngine;

namespace UnscriptedEngine
{
    public abstract class UController : UObject
    {
        private readonly NetworkVariable<NetworkObjectReference> possessedPawnReference =
            new NetworkVariable<NetworkObjectReference>(
                new NetworkObjectReference((NetworkObject)null),
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        public UPawn Pawn { get; private set; }

        public bool HasPawn => Pawn != null;

        protected override void OnSpawned()
        {
            base.OnSpawned();
            possessedPawnReference.OnValueChanged += HandlePossessedPawnReferenceChanged;
            ApplyPossessedPawnReference(possessedPawnReference.Value);
        }

        protected override void OnDespawned()
        {
            possessedPawnReference.OnValueChanged -= HandlePossessedPawnReferenceChanged;
            base.OnDespawned();
        }

        public virtual bool Possess(UPawn pawn)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"Only the server can possess pawns in {nameof(UController)}.");
                return false;
            }

            if (pawn == null)
            {
                Debug.LogWarning($"{nameof(UController)} cannot possess a null {nameof(UPawn)}.");
                return false;
            }

            if (!pawn.IsSpawned)
            {
                Debug.LogWarning($"{nameof(UController)} cannot possess an unspawned {nameof(UPawn)}.");
                return false;
            }

            if (Pawn == pawn)
            {
                return true;
            }

            UnPossess();

            if (pawn.Controller != null && pawn.Controller != this)
            {
                pawn.Controller.UnPossess();
            }

            possessedPawnReference.Value = pawn.NetworkObject;
            pawn.SetControllerReferenceFromPossession(this);
            SetPawnFromPossession(pawn);
            return true;
        }

        public virtual void UnPossess()
        {
            if (!IsServer)
            {
                Debug.LogWarning($"Only the server can unpossess pawns in {nameof(UController)}.");
                return;
            }

            if (Pawn == null)
            {
                return;
            }

            UPawn oldPawn = Pawn;

            if (oldPawn.Controller == this)
            {
                oldPawn.SetControllerReferenceFromPossession(null);
            }

            possessedPawnReference.Value = new NetworkObjectReference((NetworkObject)null);
            SetPawnFromPossession(null);
        }

        protected override void OnDestroyed()
        {
            if (IsServer)
            {
                UnPossess();
            }

            base.OnDestroyed();
        }

        protected virtual void OnPossess(UPawn pawn)
        {
        }

        protected virtual void OnUnPossess(UPawn pawn)
        {
        }

        private void HandlePossessedPawnReferenceChanged(NetworkObjectReference previousReference, NetworkObjectReference newReference)
        {
            ApplyPossessedPawnReference(newReference);
        }

        private void ApplyPossessedPawnReference(NetworkObjectReference pawnReference)
        {
            if (pawnReference.TryGet(out NetworkObject pawnObject) && pawnObject.TryGetComponent(out UPawn pawn))
            {
                SetPawnFromPossession(pawn);
                return;
            }

            SetPawnFromPossession(null);
        }

        private void SetPawnFromPossession(UPawn pawn)
        {
            if (Pawn == pawn)
            {
                return;
            }

            UPawn previousPawn = Pawn;
            Pawn = pawn;

            if (previousPawn != null && previousPawn.Controller == this)
            {
                previousPawn.SetControllerFromPossession(null);
                OnUnPossess(previousPawn);
            }

            if (Pawn != null)
            {
                Pawn.SetControllerFromPossession(this);
                OnPossess(Pawn);
            }
        }
    }
}
