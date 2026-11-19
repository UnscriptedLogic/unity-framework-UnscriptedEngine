using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Framework.Generators
{
    [Serializable]
    public sealed class FractureSettings
    {
        public Texture2D NoiseTexture;
        [Min(0)] public float NoiseStrength = 0.08f;
        [Min(0.0001f)] public float NoiseScale = 0.25f;
        [Min(0)] public float MinPieceSize = 0.15f;
        [Min(0.0001f)] public float MaxPieceSize = 0.5f;
        [Range(1, 128)] public int MaxPieces = 16;
        public int Seed = 1;
    }

    public static class MeshFracture
    {
        public readonly struct Piece
        {
            public Mesh Mesh { get; }
            public Vector3 LocalPosition { get; }
            public float Size { get; }

            internal Piece(Mesh mesh, Vector3 localPosition, float size)
            {
                Mesh = mesh;
                LocalPosition = localPosition;
                Size = size;
            }
        }

        private readonly struct Vertex
        {
            public readonly Vector3 Position;
            public readonly Vector3 Normal;
            public readonly Vector2 UV;

            public Vertex(Vector3 position, Vector3 normal, Vector2 uv)
            {
                Position = position;
                Normal = normal;
                UV = uv;
            }

            public static Vertex Intersect(Vertex a, Vertex b, float da, float db)
            {
                // Always interpolate in the same direction across shared edges.
                if (da > db)
                    return Intersect(b, a, db, da);
                float t = da / (da - db);
                return new Vertex(Vector3.LerpUnclamped(a.Position, b.Position, t),
                    Vector3.LerpUnclamped(a.Normal, b.Normal, t).normalized,
                    Vector2.LerpUnclamped(a.UV, b.UV, t));
            }
        }

        private readonly struct Triangle
        {
            public readonly Vertex A, B, C;
            public readonly int Material;

            public Triangle(Vertex a, Vertex b, Vertex c, int material)
            {
                A = a;
                B = b;
                C = c;
                Material = material;
            }
        }

        private sealed class Solid
        {
            public readonly List<Triangle> Triangles;
            public readonly Bounds Bounds;
            public readonly float Size;

            public Solid(List<Triangle> triangles)
            {
                Triangles = triangles;
                var bounds = new Bounds(triangles[0].A.Position, Vector3.zero);
                Vector3 origin = triangles[0].A.Position;
                double volume = 0;
                foreach (Triangle triangle in triangles)
                {
                    bounds.Encapsulate(triangle.A.Position);
                    bounds.Encapsulate(triangle.B.Position);
                    bounds.Encapsulate(triangle.C.Position);
                    volume += Vector3.Dot(triangle.A.Position - origin,
                        Vector3.Cross(triangle.B.Position - origin, triangle.C.Position - origin));
                }
                Bounds = bounds;
                SignedVolume = volume / 6;
                Size = Mathf.Pow((float)Math.Abs(SignedVolume), 1f / 3f);
            }

            public double SignedVolume { get; }
        }

        /// <summary>
        /// Splits a readable, connected, outward-wound closed mesh in local space.
        /// The caller owns the returned meshes. The last submesh contains fracture faces.
        /// </summary>
        public static Piece[] Generate(Mesh mesh, FractureSettings settings)
        {
            if (mesh == null || !mesh.isReadable)
                throw new ArgumentException("Choose a CPU-readable mesh.", nameof(mesh));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (!float.IsFinite(settings.MinPieceSize) || !float.IsFinite(settings.MaxPieceSize) ||
                settings.MinPieceSize < 0 || settings.MaxPieceSize <= 0 ||
                settings.MaxPieceSize < settings.MinPieceSize || settings.MaxPieces < 1 || settings.MaxPieces > 128)
                throw new ArgumentException("Use a valid piece size range and a piece limit of 1–128.");
            if (!float.IsFinite(settings.NoiseStrength) || settings.NoiseStrength < 0 ||
                !float.IsFinite(settings.NoiseScale) || settings.NoiseScale <= 0)
                throw new ArgumentException("Use a positive noise scale and a nonnegative strength.");
            if (settings.NoiseTexture != null && settings.NoiseStrength > 0 &&
                (!settings.NoiseTexture.isReadable ||
                 settings.NoiseTexture.format == TextureFormat.DXT1Crunched ||
                 settings.NoiseTexture.format == TextureFormat.DXT5Crunched ||
                 settings.NoiseTexture.format == TextureFormat.ETC_RGB4Crunched ||
                 settings.NoiseTexture.format == TextureFormat.ETC2_RGBA8Crunched))
                throw new ArgumentException("Enable Read/Write and disable Crunch on the noise texture.");

            var triangles = ReadTriangles(mesh);
            var source = new Solid(triangles);
            float epsilon = source.Bounds.size.magnitude * 0.000001f;
            if (!(source.SignedVolume > 0) || !(epsilon > 0) || !IsClosedAndConnected(triangles, epsilon))
                throw new ArgumentException("The mesh must be a connected, watertight solid with outward winding.");

            var random = new System.Random(settings.Seed);
            var pending = new List<Solid> { source };
            var finished = new List<Solid>();
            while (pending.Count > 0)
            {
                int largest = 0;
                for (int i = 1; i < pending.Count; i++)
                {
                    if (pending[i].Size > pending[largest].Size)
                        largest = i;
                }

                Solid solid = pending[largest];
                pending.RemoveAt(largest);
                if (solid.Size > settings.MaxPieceSize &&
                    pending.Count + finished.Count + 2 <= settings.MaxPieces &&
                    TrySplit(solid, settings, mesh.subMeshCount, epsilon, random, out Solid first, out Solid second))
                {
                    pending.Add(first);
                    pending.Add(second);
                }
                else
                {
                    finished.Add(solid);
                }
            }

            var pieces = new Piece[finished.Count];
            for (int i = 0; i < pieces.Length; i++)
                pieces[i] = BuildMesh(finished[i], mesh.subMeshCount + 1, i);
            return pieces;
        }

        private static List<Triangle> ReadTriangles(Mesh mesh)
        {
            Vector3[] positions = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            var result = new List<Triangle>();
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                    throw new ArgumentException("Only triangle meshes can be fractured.");
                int[] indices = mesh.GetTriangles(submesh);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    Vector3 normal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]).normalized;
                    result.Add(new Triangle(Read(a, normal), Read(b, normal), Read(c, normal), submesh));
                }
            }

            if (result.Count == 0)
                throw new ArgumentException("The mesh contains no triangles.");
            return result;

            Vertex Read(int index, Vector3 normal) => new Vertex(positions[index],
                normals.Length == positions.Length ? normals[index] : normal,
                uvs.Length == positions.Length ? uvs[index] : Vector2.zero);
        }

        private static bool TrySplit(Solid solid, FractureSettings settings, int capMaterial, float epsilon,
            System.Random random, out Solid first, out Solid second)
        {
            Vector3 dimensions = solid.Bounds.size;
            Vector3 major = dimensions.x >= dimensions.y && dimensions.x >= dimensions.z
                ? Vector3.right : dimensions.y >= dimensions.z ? Vector3.up : Vector3.forward;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector3 jitter = new Vector3(Next(), Next(), Next());
                Vector3 normal = (major + jitter * 0.75f).normalized;
                Vector3 center = solid.Bounds.center + Vector3.Scale(dimensions, jitter) * 0.15f;
                if (Split(solid, center, normal, settings, capMaterial, epsilon, out first, out second) &&
                    first.Size >= settings.MinPieceSize && second.Size >= settings.MinPieceSize)
                    return true;
            }

            first = second = null;
            return false;

            float Next() => (float)(random.NextDouble() * 2 - 1);
        }

        private static bool Split(Solid solid, Vector3 center, Vector3 normal, FractureSettings settings,
            int capMaterial, float epsilon, out Solid first, out Solid second)
        {
            first = second = null;
            var negative = new List<Triangle>();
            var positive = new List<Triangle>();
            var segments = new List<(Vector3, Vector3)>();
            // Reuse scratch buffers for every triangle in the cut.
            var vertices = new Vertex[3];
            var distances = new float[3];
            var intersections = new Vector3[2];
            var below = new Vertex[4];
            var above = new Vertex[4];

            foreach (Triangle triangle in solid.Triangles)
            {
                vertices[0] = triangle.A;
                vertices[1] = triangle.B;
                vertices[2] = triangle.C;
                for (int i = 0; i < 3; i++)
                {
                    distances[i] = Vector3.Dot(vertices[i].Position - center, normal);
                    if (Mathf.Abs(distances[i]) <= epsilon)
                        return false;
                }

                int belowCount = 0, aboveCount = 0, crossingCount = 0;
                for (int i = 0; i < 3; i++)
                {
                    int next = (i + 1) % 3;
                    bool isBelow = distances[i] < 0;
                    if (isBelow)
                        below[belowCount++] = vertices[i];
                    else
                        above[aboveCount++] = vertices[i];

                    if (isBelow == (distances[next] < 0))
                        continue;

                    Vertex crossing = Vertex.Intersect(vertices[i], vertices[next], distances[i], distances[next]);
                    below[belowCount++] = crossing;
                    above[aboveCount++] = crossing;
                    intersections[crossingCount++] = crossing.Position;
                }

                if (crossingCount == 2)
                    segments.Add((intersections[0], intersections[1]));
                for (int i = 1; i < belowCount - 1; i++)
                    negative.Add(new Triangle(below[0], below[i], below[i + 1], triangle.Material));
                for (int i = 1; i < aboveCount - 1; i++)
                    positive.Add(new Triangle(above[0], above[i], above[i + 1], triangle.Material));
            }

            if (!TryTraceLoop(segments, center, epsilon, out List<Vector3> loop))
                return false;

            Vector3 axis = Vector3.Cross(Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right, normal).normalized;
            Vector3 vertical = Vector3.Cross(normal, axis);
            var projected = new List<Vector2>(loop.Count);
            foreach (Vector3 vertex in loop)
            {
                Vector3 local = vertex - center;
                projected.Add(new Vector2(Vector3.Dot(local, axis), Vector3.Dot(local, vertical)));
            }

            if (!TryTriangulate(projected, epsilon * epsilon, out List<int> indices))
                return false;

            for (int i = 0; i < indices.Count; i += 3)
                AddCap(indices[i], indices[i + 1], indices[i + 2]);

            first = new Solid(negative);
            second = new Solid(positive);
            double minimumVolume = epsilon * solid.Bounds.size.sqrMagnitude;
            return first.SignedVolume > minimumVolume && second.SignedVolume > minimumVolume &&
                IsClosedAndConnected(negative, epsilon) && IsClosedAndConnected(positive, epsilon);

            void AddCap(int a, int b, int c)
            {
                var va = new Vertex(loop[a], normal, projected[a] / settings.NoiseScale);
                var vb = new Vertex(loop[b], normal, projected[b] / settings.NoiseScale);
                var vc = new Vertex(loop[c], normal, projected[c] / settings.NoiseScale);
                AddRelief(va, vb, vc, 0);
            }

            void AddRelief(Vertex a, Vertex b, Vertex c, int depth)
            {
                Texture2D texture = settings.NoiseTexture;
                if (texture == null || settings.NoiseStrength == 0)
                {
                    AddFace(a, b, c);
                    return;
                }

                Vector2 uv = (a.UV + b.UV + c.UV) / 3f;
                Vector3 position = (a.Position + b.Position + c.Position) / 3f;
                float edge = Mathf.Min((a.Position - b.Position).magnitude,
                    Mathf.Min((b.Position - c.Position).magnitude, (c.Position - a.Position).magnitude));
                float height = (texture.GetPixelBilinear(uv.x + 0.5f, uv.y + 0.5f).grayscale * 2 - 1) *
                    Mathf.Min(settings.NoiseStrength, edge * 0.2f);
                float shift = ClampToSolid(solid, position, normal, height, epsilon);
                var middle = new Vertex(position + normal * shift, normal, uv);
                if (depth >= 1 || edge < settings.NoiseScale)
                {
                    AddFace(a, b, middle);
                    AddFace(b, c, middle);
                    AddFace(c, a, middle);
                    return;
                }

                AddRelief(a, b, middle, depth + 1);
                AddRelief(b, c, middle, depth + 1);
                AddRelief(c, a, middle, depth + 1);
            }

            void AddFace(Vertex a, Vertex b, Vertex c)
            {
                Vector3 faceNormal = Vector3.Cross(b.Position - a.Position, c.Position - a.Position).normalized;
                negative.Add(new Triangle(WithNormal(a, faceNormal), WithNormal(b, faceNormal),
                    WithNormal(c, faceNormal), capMaterial));
                positive.Add(new Triangle(WithNormal(c, -faceNormal), WithNormal(b, -faceNormal),
                    WithNormal(a, -faceNormal), capMaterial));
            }
        }

        private static Vertex WithNormal(Vertex vertex, Vector3 normal) =>
            new Vertex(vertex.Position, normal, vertex.UV);

        private static float ClampToSolid(Solid solid, Vector3 origin, Vector3 normal, float shift, float epsilon)
        {
            // Keep relief away from the exterior along the sample ray.
            Vector3 ray = shift < 0 ? -normal : normal;
            float distance = Mathf.Abs(shift);
            foreach (Triangle triangle in solid.Triangles)
            {
                Vector3 a = triangle.A.Position;
                Vector3 ab = triangle.B.Position - a;
                Vector3 ac = triangle.C.Position - a;
                Vector3 p = Vector3.Cross(ray, ac);
                float determinant = Vector3.Dot(ab, p);
                if (Mathf.Abs(determinant) <= epsilon * epsilon)
                    continue;
                Vector3 offset = origin - a;
                float u = Vector3.Dot(offset, p) / determinant;
                Vector3 q = Vector3.Cross(offset, ab);
                float v = Vector3.Dot(ray, q) / determinant;
                float t = Vector3.Dot(ac, q) / determinant;
                if (u >= 0 && v >= 0 && u + v <= 1 && t > 0)
                    distance = Mathf.Min(distance, t * 0.4f);
            }
            return shift < 0 ? -distance : distance;
        }

        private static Vector3Int Key(Vector3 point, Vector3 origin, float epsilon)
        {
            Vector3 cell = (point - origin) / epsilon;
            return new Vector3Int(Mathf.RoundToInt(cell.x), Mathf.RoundToInt(cell.y), Mathf.RoundToInt(cell.z));
        }

        private static bool TryTraceLoop(List<(Vector3, Vector3)> segments, Vector3 origin, float epsilon,
            out List<Vector3> loop)
        {
            loop = new List<Vector3>();
            if (segments.Count < 3)
                return false;

            var neighbors = new Dictionary<Vector3Int, List<Vector3Int>>();
            var positions = new Dictionary<Vector3Int, Vector3>();
            foreach ((Vector3 a, Vector3 b) in segments)
            {
                Vector3Int ka = Key(a, origin, epsilon), kb = Key(b, origin, epsilon);
                if (ka == kb)
                    return false;
                Add(ka, kb, a);
                Add(kb, ka, b);
            }

            foreach (List<Vector3Int> connections in neighbors.Values)
            {
                if (connections.Count != 2 || connections[0] == connections[1])
                    return false;
            }

            Vector3Int start = Key(segments[0].Item1, origin, epsilon);
            Vector3Int previous = neighbors[start][1];
            Vector3Int current = start;
            do
            {
                loop.Add(positions[current]);
                List<Vector3Int> connections = neighbors[current];
                Vector3Int next = connections[0] == previous ? connections[1] : connections[0];
                previous = current;
                current = next;
            } while (current != start && loop.Count < neighbors.Count);

            // Multiple loops can represent holes or disconnected components.
            return current == start && loop.Count == neighbors.Count;

            void Add(Vector3Int from, Vector3Int to, Vector3 position)
            {
                if (!neighbors.TryGetValue(from, out List<Vector3Int> connections))
                {
                    connections = new List<Vector3Int>(2);
                    neighbors.Add(from, connections);
                    positions.Add(from, position);
                }
                connections.Add(to);
            }
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
            (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        private static bool TryTriangulate(List<Vector2> polygon, float tolerance, out List<int> result)
        {
            result = new List<int>();
            var remaining = new List<int>(polygon.Count);
            float area = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                remaining.Add(i);
                area += Cross(Vector2.zero, polygon[i], polygon[(i + 1) % polygon.Count]);
            }
            if (area < 0)
                remaining.Reverse();

            while (remaining.Count > 3)
            {
                bool found = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int b = remaining[i];
                    int c = remaining[(i + 1) % remaining.Count];
                    if (Cross(polygon[a], polygon[b], polygon[c]) <= tolerance)
                        continue;
                    bool occupied = false;
                    foreach (int index in remaining)
                    {
                        if (index != a && index != b && index != c &&
                            Cross(polygon[a], polygon[b], polygon[index]) >= -tolerance &&
                            Cross(polygon[b], polygon[c], polygon[index]) >= -tolerance &&
                            Cross(polygon[c], polygon[a], polygon[index]) >= -tolerance)
                        {
                            occupied = true;
                            break;
                        }
                    }
                    if (occupied)
                        continue;
                    result.Add(a);
                    result.Add(b);
                    result.Add(c);
                    remaining.RemoveAt(i);
                    found = true;
                    break;
                }
                if (!found)
                    return false;
            }

            if (Cross(polygon[remaining[0]], polygon[remaining[1]], polygon[remaining[2]]) <= tolerance)
                return false;
            result.AddRange(remaining);
            return true;
        }

        private static bool IsClosedAndConnected(List<Triangle> triangles, float epsilon)
        {
            Vector3 origin = triangles[0].A.Position;
            var edges = new Dictionary<(Vector3Int, Vector3Int), int>();
            var parents = new int[triangles.Count];
            for (int i = 0; i < triangles.Count; i++)
            {
                parents[i] = i;
                Triangle triangle = triangles[i];
                Vector3Int a = Key(triangle.A.Position, origin, epsilon);
                Vector3Int b = Key(triangle.B.Position, origin, epsilon);
                Vector3Int c = Key(triangle.C.Position, origin, epsilon);
                if (!Add(a, b, i) || !Add(b, c, i) || !Add(c, a, i))
                    return false;
            }
            foreach (var entry in edges)
            {
                if (!edges.TryGetValue((entry.Key.Item2, entry.Key.Item1), out int opposite))
                    return false;
                parents[Find(entry.Value)] = Find(opposite);
            }
            int root = Find(0);
            for (int i = 1; i < parents.Length; i++)
            {
                if (Find(i) != root)
                    return false;
            }
            return true;

            bool Add(Vector3Int a, Vector3Int b, int triangle) => a != b && edges.TryAdd((a, b), triangle);

            int Find(int index)
            {
                while (parents[index] != index)
                {
                    parents[index] = parents[parents[index]];
                    index = parents[index];
                }
                return index;
            }
        }

        private static Piece BuildMesh(Solid solid, int materials, int index)
        {
            Vector3 pivot = solid.Bounds.center;
            var vertices = new List<Vector3>(solid.Triangles.Count * 3);
            var normals = new List<Vector3>(vertices.Capacity);
            var uvs = new List<Vector2>(vertices.Capacity);
            var indices = new List<int>[materials];
            for (int i = 0; i < materials; i++)
                indices[i] = new List<int>();

            foreach (Triangle triangle in solid.Triangles)
            {
                Add(triangle.A, indices[triangle.Material]);
                Add(triangle.B, indices[triangle.Material]);
                Add(triangle.C, indices[triangle.Material]);
            }

            var mesh = new Mesh
            {
                name = $"Fracture Piece {index + 1}",
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                subMeshCount = materials
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            for (int i = 0; i < materials; i++)
                mesh.SetTriangles(indices[i], i, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return new Piece(mesh, pivot, solid.Size);

            void Add(Vertex vertex, List<int> submesh)
            {
                submesh.Add(vertices.Count);
                vertices.Add(vertex.Position - pivot);
                normals.Add(vertex.Normal);
                uvs.Add(vertex.UV);
            }
        }
    }
}
