using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    /// <summary>Immutable shared recipe. Per-actor timers and scratch buffers belong to the controller.</summary>
    public abstract class StatusEffectDefinition : ScriptableObject
    {
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("How long the effect lasts in scaled gameplay seconds. Reapplication refreshes this duration.")]
        private float duration = 5f;
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Seconds between effect ticks. The first tick happens immediately on initial application.")]
        private float tickInterval = 1f;
        [SerializeField]
        [Tooltip("RGB color used by StatusZone outlines and translucent debug faces. Face opacity uses the shared debug setting.")]
        private Color debugColor = new Color(0.65f, 0.25f, 0.85f, 1f);

        public float Duration => Mathf.Max(0.01f, duration);
        public float TickInterval => Mathf.Max(0.01f, tickInterval);
        public Color DebugColor { get => debugColor; set => debugColor = value; }
        public virtual bool CanApply(GameObject actor) => actor != null && actor.activeInHierarchy;
        /// <summary>Server/offline only. Scratch storage belongs to this active effect; do not retain it.</summary>
        public abstract bool ApplyTick(GameObject actor, List<TaggedFloatValue> scratch);
    }
}
