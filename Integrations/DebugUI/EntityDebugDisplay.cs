using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.UI
{
    /// <summary>Bounded pooled presentations. Data and physics refresh at 10 Hz; labels follow every frame.</summary>
    internal sealed class EntityDebugDisplay
    {
        private sealed class Label
        {
            internal DebugEntity Entity;
            internal Text Hud;
            internal Text World;
            internal bool ShowHud;
            internal bool ShowWorld;
        }

        private readonly struct Candidate
        {
            internal readonly DebugEntity Entity;
            internal readonly float DistanceSquared;
            internal Candidate(DebugEntity entity, float distanceSquared)
            { Entity = entity; DistanceSquared = distanceSquared; }
        }

        private readonly RectTransform hudRoot;
        private readonly GameObject worldRoot;
        private readonly Font font;
        private readonly DebugInfoWriter writer = new DebugInfoWriter();
        private readonly Dictionary<DebugEntity, string> snapshots = new Dictionary<DebugEntity, string>();
        private readonly List<Candidate> candidates = new List<Candidate>(64);
        private readonly List<Label> labels = new List<Label>();
        private float maximumDistance;

        internal EntityDebugDisplay(GameObject host, GameObject display, Font font)
        {
            hudRoot = display.GetComponent<RectTransform>();
            this.font = font;
            worldRoot = new GameObject("Entity World Labels");
            worldRoot.transform.SetParent(host.transform, false);
        }

        internal void UpdateTarget()
        {
            Camera camera = DebugEntities.ViewCamera;
            bool inspect = SettingsRuntime.Service.Get(DebugEntities.InspectSetting);
            DebugEntities.SetLookedAt(camera != null && inspect
                ? DebugEntities.FindTarget(camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)),
                    SettingsRuntime.Service.Get(DebugEntities.DistanceSetting)) : null);
        }

        internal void Refresh(StringBuilder overlay)
        {
            snapshots.Clear();
            UpdateTarget();
            bool inspect = SettingsRuntime.Service.Get(DebugEntities.InspectSetting);
            DebugEntity target = DebugEntities.Inspected;
            if (inspect && target != null)
            {
                if (overlay.Length > 0) overlay.Append("\n\n");
                overlay.Append(DebugEntities.Pinned != null ? "[Pinned entity | F4: release]\n" : "[Looked-at entity | F4: pin]\n");
                overlay.Append(Capture(target));
            }

            Camera camera = DebugEntities.ViewCamera;
            bool hud = SettingsRuntime.Service.Get(DebugEntities.HudSetting);
            bool world = SettingsRuntime.Service.Get(DebugEntities.WorldSetting);
            maximumDistance = SettingsRuntime.Service.Get(DebugEntities.DistanceSetting);
            candidates.Clear();
            if (camera != null && (hud || world)) SelectCandidates(camera, hud, world);

            int used = 0;
            bool occlusion = SettingsRuntime.Service.Get(DebugEntities.OcclusionSetting);
            foreach (Candidate candidate in candidates)
            {
                DebugEntity entity = candidate.Entity;
                if (occlusion && IsOccluded(camera, entity)) continue;
                if (used == labels.Count) labels.Add(new Label());
                Label label = labels[used++];
                label.Entity = entity;
                label.ShowHud = hud && entity.Supports(DebugEntityViews.Hud);
                label.ShowWorld = world && entity.Supports(DebugEntityViews.World);
                string text = Capture(entity);
                if (label.ShowHud)
                {
                    if (label.Hud == null) label.Hud = CreateLabel(false);
                    SetText(label.Hud, text);
                }
                if (label.ShowWorld)
                {
                    if (label.World == null) label.World = CreateLabel(true);
                    SetText(label.World, text);
                }
                SetActive(label.Hud, label.ShowHud);
                SetActive(label.World, label.ShowWorld);
            }
            for (int index = used; index < labels.Count; index++)
            {
                labels[index].Entity = null;
                SetActive(labels[index].Hud, false);
                SetActive(labels[index].World, false);
            }
            worldRoot.SetActive(world);
            UpdatePositions();
        }

        private void SelectCandidates(Camera camera, bool hud, bool world)
        {
            int limit = SettingsRuntime.Service.Get(DebugEntities.LabelLimitSetting);
            float distanceSquared = maximumDistance * maximumDistance;
            foreach (DebugEntity entity in DebugEntities.Registered)
            {
                if (entity == null || !(hud && entity.Supports(DebugEntityViews.Hud) || world && entity.Supports(DebugEntityViews.World))) continue;
                Vector3 point = entity.LabelPosition;
                float distance = (camera.transform.position - point).sqrMagnitude;
                if (distance > distanceSquared || !IsOnScreen(camera, point)) continue;
                // Retain only the closest N. Memory remains bounded even in large entity populations.
                int insertion = 0;
                while (insertion < candidates.Count && candidates[insertion].DistanceSquared <= distance) insertion++;
                if (insertion >= limit) continue;
                candidates.Insert(insertion, new Candidate(entity, distance));
                if (candidates.Count > limit) candidates.RemoveAt(limit);
            }
        }

        private static bool IsOnScreen(Camera camera, Vector3 point)
        {
            Vector3 viewport = camera.WorldToViewportPoint(point);
            return viewport.z >= camera.nearClipPlane && viewport.z <= camera.farClipPlane &&
                viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        }

        internal static bool IsOccluded(Camera camera, DebugEntity entity)
        {
            Vector3 offset = entity.LabelPosition - camera.transform.position;
            if (!Physics.Raycast(camera.transform.position, offset.normalized, out RaycastHit hit,
                offset.magnitude, DebugEntities.PhysicsLayers, QueryTriggerInteraction.Ignore)) return false;
            return hit.collider.GetComponentInParent<DebugEntity>() != entity;
        }

        private string Capture(DebugEntity entity)
        {
            if (!snapshots.TryGetValue(entity, out string text))
            {
                text = entity.Capture(writer);
                snapshots.Add(entity, text);
            }
            return text;
        }

        internal void UpdatePositions()
        {
            Camera camera = DebugEntities.ViewCamera;
            foreach (Label label in labels)
            {
                DebugEntity entity = label.Entity;
                bool visible = camera != null && entity != null && entity.isActiveAndEnabled &&
                    (entity.LabelPosition - camera.transform.position).sqrMagnitude <= maximumDistance * maximumDistance &&
                    IsOnScreen(camera, entity.LabelPosition);
                bool hud = visible && label.ShowHud && entity.Supports(DebugEntityViews.Hud);
                bool world = visible && label.ShowWorld && entity.Supports(DebugEntityViews.World);
                SetActive(label.Hud, hud);
                SetActive(label.World, world);
                if (hud)
                {
                    Vector3 screen = camera.WorldToScreenPoint(entity.LabelPosition);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRoot, screen, null, out Vector2 local);
                    ((RectTransform)label.Hud.transform.parent).anchoredPosition = local + Vector2.up * 12f;
                }
                if (world)
                {
                    Transform panel = label.World.transform.parent;
                    panel.position = entity.LabelPosition;
                    panel.rotation = camera.transform.rotation;
                    panel.GetComponent<Canvas>().worldCamera = camera;
                }
            }
        }

        private Text CreateLabel(bool world)
        {
            GameObject panel = new GameObject(world ? "World Entity" : "HUD Entity", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(world ? worldRoot.transform : hudRoot, false);
            Image background = panel.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.7f);
            background.raycastTarget = false;
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(280f, 90f);
            if (world)
            {
                Canvas canvas = panel.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                rect.localScale = Vector3.one * 0.005f;
            }
            GameObject content = new GameObject("Details", typeof(RectTransform), typeof(Text));
            content.transform.SetParent(panel.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(5f, 3f);
            contentRect.offsetMax = new Vector2(-5f, -3f);
            Text text = content.GetComponent<Text>();
            text.font = font;
            text.fontSize = 14;
            text.color = Color.white;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.alignment = TextAnchor.UpperLeft;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void SetText(Text label, string text)
        {
            if (label.text != text) label.text = text;
            RectTransform panel = (RectTransform)label.transform.parent;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(label.preferredHeight + 6f, 240f));
        }

        private static void SetActive(Text label, bool active)
        {
            if (label != null && label.transform.parent.gameObject.activeSelf != active)
                label.transform.parent.gameObject.SetActive(active);
        }

        internal void Hide()
        {
            foreach (Label label in labels)
            {
                label.Entity = null;
                SetActive(label.Hud, false);
                SetActive(label.World, false);
            }
            snapshots.Clear();
            candidates.Clear();
            DebugEntities.SetLookedAt(null);
            worldRoot.SetActive(false);
        }
    }
}
