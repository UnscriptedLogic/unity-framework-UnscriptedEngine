using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnscriptedEngine.Gameplay;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.UI
{
    /// <summary>One bounded mesh with outline and translucent surface submeshes.</summary>
    internal sealed class ColliderZoneDebugDisplay
    {
        internal static readonly Setting<bool> VisibleSetting = Setting.Bool(
            "debug.zones.visible", true, "Nearby collider zones", "Debug/Zones",
            description: "Draw nearby zone wireframes while F3 is open. Blue: trigger; orange: collision; green: accepted entry.");
        internal static readonly Setting<float> FaceOpacitySetting = Setting.Float(
            "debug.zones.faceOpacity", 0.12f, 0f, 0.5f, "Zone face opacity", "Debug/Zones",
            description: "Translucent zone face opacity. Zero hides faces and preserves outlines.");
        private readonly List<Vector3> segments = new List<Vector3>(320);
        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<Color> colors = new List<Color>(4096);
        private readonly List<int> indices = new List<int>(4096);
        private readonly List<int> triangles = new List<int>(8192);
        private Mesh mesh;
        private Material material;
        private float nextRefresh;
        private ColliderZone target;

        internal void Draw(StringBuilder details)
        {
            Camera camera = DebugEntities.ViewCamera;
            if (camera == null) return;
            float distance = SettingsRuntime.Service.Get(DebugEntities.DistanceSetting);
            bool nearby = SettingsRuntime.Service.Get(VisibleSetting);
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (details != null && target != null)
            {
                details.Append("\n\n[Collider Zone] ").Append(target.name)
                    .Append("\nMode: ").Append(target.Shape.isTrigger ? "Trigger" : "Collision")
                    .Append("\nContacts: ").Append(target.ContactCount)
                    .Append(" | Accepted entries: ").Append(target.AcceptedCount)
                    .Append("\nLast contact: ").Append(target.LastContact)
                    .Append("\nLayer mask: ").Append(target.Filter.Layers.value);
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.05f;
                target = null;
                float nearest = distance;
                foreach (ColliderZone zone in ColliderZone.ActiveZones)
                {
                    Collider shape = zone.Shape;
                    if (!zone.ShowGizmos || shape == null || !shape.enabled) continue;
                    // Individual raycasts include triggers without changing shared entity inspection.
                    if (shape.Raycast(ray, out RaycastHit hit, nearest)) { target = zone; nearest = hit.distance; }
                }
                vertices.Clear(); colors.Clear(); indices.Clear(); triangles.Clear();
                int limit = SettingsRuntime.Service.Get(DebugEntities.LabelLimitSetting);
                if (target != null) Append(target);
                int count = target != null ? 1 : 0;
                if (nearby)
                foreach (ColliderZone zone in ColliderZone.ActiveZones)
                {
                    if (count >= limit) break;
                    if (zone == target || !zone.ShowGizmos || zone.Shape == null || !zone.Shape.enabled ||
                        zone.Shape.bounds.SqrDistance(camera.transform.position) > distance * distance) continue;
                    Append(zone); count++;
                }
                if (mesh == null) { mesh = new Mesh { name = "Collider Zone Debug Lines" }; mesh.MarkDynamic(); }
                mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors);
                mesh.subMeshCount = 2;
                mesh.SetIndices(indices, MeshTopology.Lines, 0);
                mesh.SetIndices(triangles, MeshTopology.Triangles, 1);
            }
            if (details != null || mesh == null || mesh.vertexCount == 0) return;
            if (material == null)
            {
                Shader shader = Resources.Load<Shader>("ColliderZoneDebug");
                if (shader == null) return;
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, camera, 1, null,
                UnityEngine.Rendering.ShadowCastingMode.Off, false);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, camera, 0, null,
                UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }

        private void Append(ColliderZone zone)
        {
            ColliderZoneWireframe.Collect(zone.Shape, segments);
            Color color = zone.DebugColor;
            foreach (Vector3 point in segments)
            {
                indices.Add(vertices.Count); vertices.Add(point); colors.Add(color);
            }
            float opacity = SettingsRuntime.Service.Get(FaceOpacitySetting);
            if (opacity <= 0f) return;
            int surfaceStart = vertices.Count;
            ColliderZoneSurface.Append(zone.Shape, vertices, triangles);
            color.a = opacity;
            for (int index = surfaceStart; index < vertices.Count; index++) colors.Add(color);
        }

        internal void Dispose()
        {
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
            mesh = null; material = null;
        }
    }
}
