using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    [CreateAssetMenu(fileName = "Poison", menuName = "UnscriptedEngine/Status Effects/Poison Effect")]
    public sealed class PoisonEffect : StatusEffectDefinition
    {
        [SerializeField]
        [Tooltip("Tagged value modified by each poison tick. Default Add -10; assign the actor's health value tag.")]
        private TaggedFloatOperation valueChange = new TaggedFloatOperation();

        public TaggedFloatOperation ValueChange => valueChange;
        public override bool CanApply(GameObject actor) => base.CanApply(actor) &&
            valueChange.ValueTag != null && valueChange.ValueTag.IsValid;
        public override bool ApplyTick(GameObject actor, List<TaggedFloatValue> scratch) => valueChange.Apply(actor, scratch);
    }
}
