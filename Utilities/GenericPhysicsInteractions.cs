using UnityEngine;

public static class GenericPhysicsInteractions
{
    public static void CreateExplosion(this GameObject gameObject, float force, float radius)
    {
        Collider[] colliders = new Collider[32];
        int nc = Physics.OverlapSphereNonAlloc(gameObject.transform.position, radius, colliders, 1, QueryTriggerInteraction.Ignore);
        for (int i = nc; i-- > 0;)
        {
            Rigidbody rb = colliders[i].attachedRigidbody;
            if (rb != null) rb.AddExplosionForce(force, gameObject.transform.position, radius, 5.0f, ForceMode.Impulse);
        }
    }
}
