using Unity.Netcode;
using UnityEngine;
using Framework.GameplayTag;

namespace UnscriptedEngine.Gameplay
{
    /// <summary>Shared authority and actor boundaries for contact effects.</summary>
    public static class ZoneEffectTarget
    {
        public static bool HasAuthority => NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        public static GameObject Resolve(Collider collider)
        {
            NetworkObject networkObject = collider.GetComponentInParent<NetworkObject>();
            if (networkObject != null) return networkObject.gameObject;
            GameplayTags tags = collider.GetComponentInParent<GameplayTags>();
            if (tags != null) return tags.gameObject;
            if (collider.attachedRigidbody != null) return collider.attachedRigidbody.gameObject;
            TaggedFloatValue value = collider.GetComponentInParent<TaggedFloatValue>();
            return value != null ? value.gameObject : collider.transform.root.gameObject;
        }

        public static bool BelongsTo(GameObject root, Component component)
        {
            if (component == null || !component.gameObject.activeInHierarchy) return false;
            NetworkObject nested = component.GetComponentInParent<NetworkObject>();
            NetworkObject owner = root.GetComponentInParent<NetworkObject>();
            if (nested != owner) return false;
            GameplayTags tags = component.GetComponentInParent<GameplayTags>();
            GameplayTags rootTags = root.GetComponentInParent<GameplayTags>();
            return tags == rootTags;
        }
    }
}
