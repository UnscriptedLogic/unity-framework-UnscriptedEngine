using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    /// <summary>Server/offline status lifetime owner. Definitions refresh without stacking or delaying ticks.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Unscripted Engine/Gameplay/Status Effect Controller")]
    public sealed class StatusEffectController : MonoBehaviour
    {
        private sealed class ActiveEffect
        {
            internal StatusEffectDefinition Definition;
            internal float ExpiresAt;
            internal float NextTick;
            internal readonly List<TaggedFloatValue> Values = new List<TaggedFloatValue>();
        }

        private readonly List<ActiveEffect> effects = new List<ActiveEffect>();
        private readonly List<ActiveEffect> snapshot = new List<ActiveEffect>();
        public int ActiveCount => effects.Count;

        public bool Apply(StatusEffectDefinition definition)
        {
            if (!isActiveAndEnabled || !ZoneEffectTarget.HasAuthority || definition == null || !definition.CanApply(gameObject)) return false;
            foreach (ActiveEffect active in effects)
            {
                if (active.Definition != definition) continue;
                active.ExpiresAt = Time.time + definition.Duration;
                return true;
            }
            ActiveEffect effect = new ActiveEffect
            {
                Definition = definition,
                ExpiresAt = Time.time + definition.Duration,
                NextTick = Time.time + definition.TickInterval
            };
            effects.Add(effect);
            definition.ApplyTick(gameObject, effect.Values);
            return true;
        }

        public bool Remove(StatusEffectDefinition definition)
        {
            for (int index = effects.Count - 1; index >= 0; index--)
            {
                if (effects[index].Definition != definition) continue;
                effects.RemoveAt(index); return true;
            }
            return false;
        }

        public void ClearEffects() { effects.Clear(); snapshot.Clear(); }
        private void OnEnable() => ClearEffects();
        private void OnDisable() => ClearEffects();

        private void Update()
        {
            if (!ZoneEffectTarget.HasAuthority) { ClearEffects(); return; }
            snapshot.Clear(); snapshot.AddRange(effects);
            for (int index = 0; index < snapshot.Count && isActiveAndEnabled; index++)
            {
                ActiveEffect effect = snapshot[index];
                if (!effects.Contains(effect)) continue;
                if (effect.Definition == null || Time.time >= effect.ExpiresAt)
                {
                    effects.Remove(effect); continue;
                }
                if (Time.time < effect.NextTick) continue;
                // At most one tick per frame: long stalls cannot cause an unbounded damage burst.
                effect.NextTick = Time.time + effect.Definition.TickInterval;
                effect.Definition.ApplyTick(gameObject, effect.Values);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetSessionEffects()
        {
            foreach (StatusEffectController controller in Object.FindObjectsByType<StatusEffectController>()) controller.ClearEffects();
        }
    }
}
