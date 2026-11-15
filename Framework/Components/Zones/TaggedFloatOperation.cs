using System;
using System.Collections.Generic;
using Framework.GameplayTag;
using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    public enum TaggedValueOperation { Add, Set }

    [Serializable]
    public sealed class TaggedFloatOperation
    {
        [SerializeField]
        [Tooltip("Required tag on the TaggedFloatValue, such as Stats.Health. Missing tag applies nothing.")]
        private GameplayTag valueTag;
        [SerializeField]
        [Tooltip("Add a signed amount, or Set an absolute value. Negative Add amounts deal damage.")]
        private TaggedValueOperation operation = TaggedValueOperation.Add;
        [SerializeField]
        [Tooltip("Signed Add amount or absolute Set value. Respects the target value's clamps.")]
        private float amount = -10f;

        public GameplayTag ValueTag { get => valueTag; set => valueTag = value; }
        public TaggedValueOperation Operation { get => operation; set => operation = value; }
        public float Amount { get => amount; set => amount = value; }

        /// <summary>Uses caller-owned scratch storage. Applies to every matching value within this actor.</summary>
        public bool Apply(GameObject actor, List<TaggedFloatValue> scratch)
        {
            if (actor == null || valueTag == null || !valueTag.IsValid || !ZoneEffectTarget.HasAuthority) return false;
            scratch.Clear();
            actor.GetComponentsInChildren(false, scratch);
            return ApplyToValues(actor, scratch);
        }

        /// <summary>Applies to an existing actor-value snapshot without repeating the hierarchy search.</summary>
        public bool ApplyToValues(GameObject actor, List<TaggedFloatValue> values)
        {
            if (actor == null || valueTag == null || !valueTag.IsValid || !ZoneEffectTarget.HasAuthority) return false;
            bool applied = false;
            // Value listeners may destroy or disable targets; always recheck before writing.
            foreach (TaggedFloatValue target in values)
            {
                if (actor == null) break;
                if (target == null || !target.isActiveAndEnabled || !ZoneEffectTarget.BelongsTo(actor, target) || !target.Tags.Has(valueTag)) continue;
                float next = operation == TaggedValueOperation.Set ? amount : target.Value + amount;
                applied |= target.TrySetValue(next);
            }
            return applied;
        }
    }
}
