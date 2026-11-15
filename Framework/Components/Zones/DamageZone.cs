using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Unscripted Engine/Gameplay/Damage Zone")]
    public sealed class DamageZone : ContactZoneEffect, IZoneDebugColorProvider, ISerializationCallbackReceiver
    {
        [SerializeField]
        [Tooltip("Apply each tagged value change in list order. Positive net Add is green; otherwise red. Set operations are target-dependent and remain red.")]
        private List<TaggedFloatOperation> valueChanges = new List<TaggedFloatOperation> { new TaggedFloatOperation() };
        // Retained for automatic migration of previously authored single-operation zones.
        [SerializeField]
        [HideInInspector]
        [Tooltip("Legacy single value change, migrated automatically into Value Changes.")]
        private TaggedFloatOperation valueChange;
        [SerializeField]
        [HideInInspector]
        [Tooltip("Tracks migration from the original single value change format.")]
        private bool valueChangesMigrated;
        private readonly List<TaggedFloatValue> scratch = new List<TaggedFloatValue>();
        public IReadOnlyList<TaggedFloatOperation> ValueChanges => valueChanges;
        /// <summary>Compatibility access to the first change. Returns null for an empty list.</summary>
        public TaggedFloatOperation ValueChange => valueChanges.Count > 0 ? valueChanges[0] : null;

        public void AddValueChange(TaggedFloatOperation change)
        {
            if (change != null) valueChanges.Add(change);
        }

        protected override bool TryApply(GameObject actor)
        {
            scratch.Clear();
            actor.GetComponentsInChildren(false, scratch);
            bool applied = false;
            for (int index = 0; index < valueChanges.Count && isActiveAndEnabled && actor != null; index++)
            {
                TaggedFloatOperation change = valueChanges[index];
                if (change != null) applied |= change.ApplyToValues(actor, scratch);
            }
            return applied;
        }

        public bool TryGetZoneDebugColor(out Color color)
        {
            double sum = 0d;
            bool hasSet = false;
            foreach (TaggedFloatOperation change in valueChanges)
            {
                if (change == null || float.IsNaN(change.Amount) || float.IsInfinity(change.Amount)) continue;
                if (change.Operation == TaggedValueOperation.Set) hasSet = true;
                else sum += change.Amount;
            }
            color = !hasSet && sum > 0d ? Color.green : Color.red;
            return true;
        }

        public void OnBeforeSerialize() => MigrateValueChanges();
        public void OnAfterDeserialize() => MigrateValueChanges();

        private void MigrateValueChanges()
        {
            if (!valueChangesMigrated && valueChange != null)
            {
                valueChanges = new List<TaggedFloatOperation> { valueChange };
                valueChange = null;
            }
            valueChangesMigrated = true;
            if (valueChanges == null) valueChanges = new List<TaggedFloatOperation>();
        }
    }
}
