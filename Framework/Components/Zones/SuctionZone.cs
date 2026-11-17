using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    /// <summary>Continuously pulls contacted rigidbodies toward their nearest point on the source plane.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Unscripted Engine/Gameplay/Suction Zone")]
    public sealed class SuctionZone : PhysicsForceZone
    {
        protected override bool PullToPlane => true;
    }
}
