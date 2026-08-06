using Unity.Netcode;
using UnityEngine;

namespace Framework.Components
{
    public abstract class UObjectComponent : NetworkBehaviour
    {
        public UObject Owner { get; private set; }

        public bool HasBegunPlay { get; private set; }

        protected virtual bool CanTick => HasBegunPlay;

        protected virtual void Awake()
        {
            RegisterOwner();
            TryBeginPlayFromOwner();
        }

        protected virtual void OnEnable()
        {
            RegisterOwner();
            TryBeginPlayFromOwner();
        }

        protected virtual void Start()
        {
            TryBeginPlayFromOwner();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            RegisterOwner();
            TryBeginPlayFromOwner();
        }

        protected virtual void Update()
        {
            TryBeginPlayFromOwner();

            if (CanTick)
            {
                Tick(Time.deltaTime);
            }
        }

        protected virtual void FixedUpdate()
        {
            TryBeginPlayFromOwner();

            if (CanTick)
            {
                FixedTick(Time.fixedDeltaTime);
            }
        }

        protected virtual void OnDisable()
        {
            UnregisterOwner();
        }

        public override void OnDestroy()
        {
            UnregisterOwner();
            base.OnDestroy();
        }

        protected virtual void BeginPlay()
        {
        }

        protected virtual void Tick(float deltaTime)
        {
        }

        protected virtual void FixedTick(float fixedDeltaTime)
        {
        }

        protected virtual void OnOwnerAssigned(UObject owner)
        {
        }

        private void RegisterOwner()
        {
            UObject owner = GetComponent<UObject>();

            if (owner == null)
            {
                owner = GetComponentInParent<UObject>();
            }

            if (Owner == owner)
            {
                return;
            }

            UnregisterOwner();
            Owner = owner;

            if (Owner == null)
            {
                return;
            }

            Owner.BeganPlay += HandleOwnerBeganPlay;
            OnOwnerAssigned(Owner);
        }

        private void UnregisterOwner()
        {
            if (Owner != null)
            {
                Owner.BeganPlay -= HandleOwnerBeganPlay;
                Owner = null;
            }
        }

        private void HandleOwnerBeganPlay(UObject owner)
        {
            BeginPlayInternal(owner);
        }

        private void TryBeginPlayFromOwner()
        {
            RegisterOwner();

            if (IsSpawned && Owner != null && Owner.HasBegunPlay)
            {
                BeginPlayInternal(Owner);
            }
        }

        private void BeginPlayInternal(UObject owner)
        {
            if (HasBegunPlay || owner == null)
            {
                return;
            }

            HasBegunPlay = true;
            BeginPlay();
        }
    }
}
