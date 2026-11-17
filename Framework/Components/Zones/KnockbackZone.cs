using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    /// <summary>Applies one directional impulse per Rigidbody visit, for trigger or collision contacts.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Unscripted Engine/Gameplay/Knockback Zone")]
    public sealed class KnockbackZone : PhysicsForceZone
    {
        protected override bool Instant => true;
        private void Reset() => RequireLineOfSight = false;
    }
}
