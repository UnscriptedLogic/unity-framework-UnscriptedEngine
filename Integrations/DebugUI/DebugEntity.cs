using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.UI
{
    [Flags]
    public enum DebugEntityViews { None = 0, Inspector = 1, Hud = 2, World = 4, All = Inspector | Hud | World }

    /// <summary>Opt-in debug identity. Child colliders resolve to this component; sources live on this GameObject.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Unscripted Engine/Debug/Debug Entity")]
    public sealed class DebugEntity : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional display name. Empty uses the GameObject name.")]
        private string displayName = string.Empty;
        [SerializeField, Tooltip("Debug presentations this entity permits. Global settings control which are visible.")]
        private DebugEntityViews views = DebugEntityViews.All;
        [SerializeField, Tooltip("Local-space point used to position HUD and world labels.")]
        private Vector3 labelOffset = Vector3.up * 1.5f;
        private readonly List<MonoBehaviour> sources = new List<MonoBehaviour>();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public DebugEntityViews Views { get => views; set => views = value; }
        public Vector3 LabelOffset { get => labelOffset; set => labelOffset = value; }
        public Vector3 LabelPosition => transform.TransformPoint(labelOffset);
        public bool Supports(DebugEntityViews view) => isActiveAndEnabled && (views & view) != 0;

        private void OnEnable() => DebugEntities.Register(this);
        private void OnDisable() => DebugEntities.Unregister(this);

        internal string Capture(DebugInfoWriter writer)
        {
            writer.Clear();
            writer.Add("Entity", DisplayName);
            writer.Add("ID", gameObject.GetEntityId());
            writer.Add("Position", transform.position);
            GetComponents(sources);
            foreach (MonoBehaviour source in sources)
            {
                if (source == null || !source.isActiveAndEnabled || !(source is IDebugInfoSource provider)) continue;
                try { provider.CollectDebugInfo(writer); }
                catch (Exception exception) { writer.Add("Debug source error", exception.Message); }
            }
            return writer.ToString();
        }
    }
}
