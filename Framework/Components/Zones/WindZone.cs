using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    /// <summary>Continuously pushes contacted rigidbodies along the source plane normal.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Unscripted Engine/Gameplay/Wind Zone")]
    public sealed class WindZone : PhysicsForceZone { }
}
