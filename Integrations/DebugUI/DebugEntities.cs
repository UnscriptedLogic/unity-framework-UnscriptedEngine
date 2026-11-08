using System.Collections.Generic;
using UnityEngine;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.UI
{
    /// <summary>Main-thread entity registry and shared view settings. No per-frame scene searches.</summary>
    public static class DebugEntities
    {
        private static readonly HashSet<DebugEntity> entities = new HashSet<DebugEntity>();
        private static Camera cameraOverride;
        private static DebugEntity lookedAt;
        private static DebugEntity pinned;
        private static int physicsLayers = Physics.DefaultRaycastLayers;

        public static readonly Setting<bool> InspectSetting = Setting.Bool(
            "debug.entities.inspect", true, "Inspect looked-at entity", "Debug/Entities",
            description: "Show target details in the F3 overlay. F4 pins or releases the target.");
        public static readonly Setting<bool> HudSetting = Setting.Bool(
            "debug.entities.hud", false, "Entity HUD labels", "Debug/Entities",
            description: "Show screen-space labels over nearby debug entities while F3 is open.");
        public static readonly Setting<bool> WorldSetting = Setting.Bool(
            "debug.entities.world", false, "Entity world labels", "Debug/Entities",
            description: "Show billboard world-space labels while F3 is open.");
        public static readonly Setting<float> DistanceSetting = Setting.Float(
            "debug.entities.distance", 30f, 1f, 500f, "Entity debug distance", "Debug/Entities",
            description: "Maximum inspection and label distance in world units. Pinned inspection ignores distance.");
        public static readonly Setting<int> LabelLimitSetting = Setting.Int(
            "debug.entities.limit", 12, 1, 64, "Maximum labeled entities", "Debug/Entities",
            description: "Nearest on-screen candidates get labels. Bounds UI, data collection and occlusion checks.");
        public static readonly Setting<bool> OcclusionSetting = Setting.Bool(
            "debug.entities.occlusion", true, "Hide occluded entity labels", "Debug/Entities",
            description: "Check physics line of sight at 10 Hz. Pinned overlay details remain visible.");

        internal static IEnumerable<DebugEntity> Registered => entities;
        internal static SettingDefinition[] Definitions => new SettingDefinition[]
        { InspectSetting, HudSetting, WorldSetting, DistanceSetting, LabelLimitSetting, OcclusionSetting };

        /// <summary>Optional actual rendering camera. Null falls back to Camera.main (not a Cinemachine virtual camera).</summary>
        public static Camera CameraOverride { get => cameraOverride; set => cameraOverride = value; }
        /// <summary>Layers used by inspection and occlusion. Exclude the local player's colliders if necessary.</summary>
        public static LayerMask PhysicsLayers { get => physicsLayers; set => physicsLayers = value; }
        public static DebugEntity LookedAt => IsValid(lookedAt) ? lookedAt : null;
        public static DebugEntity Pinned
        {
            get
            {
                if (!IsValid(pinned)) pinned = null;
                return pinned;
            }
        }
        public static DebugEntity Inspected => Pinned != null ? Pinned : LookedAt;
        internal static Camera ViewCamera => cameraOverride != null && cameraOverride.isActiveAndEnabled
            ? cameraOverride : Camera.main;

        public static void TogglePin() { pinned = Pinned != null ? null : LookedAt; }
        public static void ClearPin() => pinned = null;
        internal static void SetLookedAt(DebugEntity entity) => lookedAt = IsValid(entity) ? entity : null;
        internal static void Register(DebugEntity entity) => entities.Add(entity);
        internal static void Unregister(DebugEntity entity)
        {
            entities.Remove(entity);
            if (lookedAt == entity) lookedAt = null;
            if (pinned == entity) pinned = null;
        }

        internal static bool IsValid(DebugEntity entity) => entity != null && entity.Supports(DebugEntityViews.Inspector);

        internal static DebugEntity FindTarget(Ray ray, float distance)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, distance, physicsLayers, QueryTriggerInteraction.Ignore)) return null;
            DebugEntity entity = hit.collider.GetComponentInParent<DebugEntity>();
            return IsValid(entity) ? entity : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            entities.Clear();
            cameraOverride = null;
            lookedAt = null;
            pinned = null;
            physicsLayers = Physics.DefaultRaycastLayers;
        }

        // OnEnable is not guaranteed to repeat when both domain and scene reload are disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RecoverSceneEntities()
        {
            foreach (DebugEntity entity in Object.FindObjectsByType<DebugEntity>())
                if (entity.isActiveAndEnabled) Register(entity);
        }
    }
}
