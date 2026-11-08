using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>Feeds world-space steering to the existing server-owned character motor.</summary>
    public sealed class NpcNavigationAdapter : INpcNavigation
    {
        private readonly NavMeshMovementComponent navigation;
        private readonly CharacterMovementComponent movement;

        public NpcNavigationAdapter(NavMeshMovementComponent navigation, CharacterMovementComponent movement)
        {
            this.navigation = navigation;
            this.movement = movement;
        }

        public bool IsReady => navigation != null && navigation.isActiveAndEnabled
            && movement != null && movement.isActiveAndEnabled && movement.HasBegunPlay
            && movement.IsSpawned && movement.IsServer && movement.IsOwner && !movement.IsFlying;
        public Vector3 Position => movement != null ? movement.SimulationPosition : Vector3.zero;
        public NavMeshMovementComponent.NavigationStatus Status => navigation != null
            ? navigation.Status : NavMeshMovementComponent.NavigationStatus.Failed;

        public bool MoveTo(Vector3 destination, float stoppingDistance) =>
            IsReady && navigation.MoveTo(destination, stoppingDistance);

        public void Tick()
        {
            if (!IsReady) { Stop(); return; }
            Vector3 input = navigation.GetMovementInput();
            // A zero control yaw makes world X/Z input valid for either motor orientation setting.
            movement.SetControlYaw(0f);
            movement.SetMovementInput(new Vector2(input.x, input.z));
            movement.SetVerticalMovementInput(0f);
            if (input.sqrMagnitude > 0.0001f)
                movement.transform.rotation = Quaternion.LookRotation(input, Vector3.up);
        }

        public void Stop()
        {
            if (navigation != null) navigation.Stop();
            if (movement != null && movement.IsSpawned && movement.IsServer && movement.IsOwner)
            {
                movement.SetMovementInput(Vector2.zero);
                movement.SetVerticalMovementInput(0f);
            }
        }
    }
}
