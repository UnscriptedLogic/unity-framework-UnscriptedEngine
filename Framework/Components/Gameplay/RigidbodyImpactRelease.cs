using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>Lets physical impacts and the shared force utility release a held body group.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RigidbodyImpactRelease : MonoBehaviour
    {
        public const float DefaultMinimumImpactSpeed = 3f;

        [SerializeField] private RigidbodyReleaseGroup group;
        [Tooltip("Minimum collision speed along the contact normal, in metres per second.")]
        [SerializeField, Min(0.01f)] private float minimumImpactSpeed = DefaultMinimumImpactSpeed;

        internal bool CanRelease => isActiveAndEnabled && group != null && group.isActiveAndEnabled && group.IsHolding;

        internal bool TryRelease() => CanRelease && group.TryRelease();

        private void OnCollisionEnter(Collision collision)
        {
            if (!CanRelease || collision.relativeVelocity.sqrMagnitude < minimumImpactSpeed * minimumImpactSpeed)
                return;

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if (Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal)) < minimumImpactSpeed)
                    continue;

                if (TryRelease())
                {
                    // The solver handled the impact while this body was kinematic.
                    GetComponent<Rigidbody>().AddForceAtPosition(
                        contact.normal * collision.impulse.magnitude, contact.point, ForceMode.Impulse);
                }
                return;
            }
        }
    }
}
