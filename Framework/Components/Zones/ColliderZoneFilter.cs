using System;
using Framework.GameplayTag;
using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    [Serializable]
    public sealed class ColliderZoneFilter
    {
        [SerializeField]
        [Tooltip("Allowed layers on the contacting collider. Everything is allowed by default.")]
        private LayerMask layers = ~0;
        [SerializeField]
        [Tooltip("Require at least one listed tag. Empty disables this condition.")]
        private GameplayTag[] requireAny = Array.Empty<GameplayTag>();
        [SerializeField]
        [Tooltip("Require every listed tag. Empty disables this condition.")]
        private GameplayTag[] requireAll = Array.Empty<GameplayTag>();
        [SerializeField]
        [Tooltip("Reject when any listed tag matches. Empty disables this condition.")]
        private GameplayTag[] rejectAny = Array.Empty<GameplayTag>();
        [SerializeField]
        [Tooltip("Reject only when every listed tag matches. Empty disables this condition.")]
        private GameplayTag[] rejectAll = Array.Empty<GameplayTag>();

        public LayerMask Layers { get => layers; set => layers = value; }

        public bool Allows(Collider other)
        {
            if (other == null || (layers.value & (1 << other.gameObject.layer)) == 0) return false;
            if (!HasEntries(requireAny) && !HasEntries(requireAll) && !HasEntries(rejectAny) && !HasEntries(rejectAll)) return true;
            GameplayTags owner = other.GetComponentInParent<GameplayTags>();
            return MatchesTags(owner != null ? owner.Tags : null);
        }

        public bool MatchesTags(GameplayTagContainer tags)
        {
            return (!HasEntries(requireAny) || tags != null && tags.HasAny(requireAny))
                && (!HasEntries(requireAll) || tags != null && tags.HasAll(requireAll))
                && (!HasEntries(rejectAny) || tags == null || !tags.HasAny(rejectAny))
                && (!HasEntries(rejectAll) || tags == null || !tags.HasAll(rejectAll));
        }

        private static bool HasEntries(GameplayTag[] tags) => tags != null && tags.Length > 0;
    }
}
