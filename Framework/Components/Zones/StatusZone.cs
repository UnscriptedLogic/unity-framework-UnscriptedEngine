using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Unscripted Engine/Gameplay/Status Zone")]
    public sealed class StatusZone : ContactZoneEffect, IZoneDebugColorProvider
    {
        [SerializeField]
        [Tooltip("Status definition to apply. Reapplying the same definition refreshes its duration without stacking.")]
        private StatusEffectDefinition effect;
        public StatusEffectDefinition Effect { get => effect; set => effect = value; }

        public bool TryGetZoneDebugColor(out Color color)
        {
            color = effect != null ? effect.DebugColor : Color.white;
            return effect != null;
        }

        protected override bool TryApply(GameObject actor)
        {
            if (effect == null || !effect.CanApply(actor)) return false;
            StatusEffectController controller = actor.GetComponent<StatusEffectController>();
            if (controller == null) controller = actor.AddComponent<StatusEffectController>();
            return controller.Apply(effect);
        }
    }
}
