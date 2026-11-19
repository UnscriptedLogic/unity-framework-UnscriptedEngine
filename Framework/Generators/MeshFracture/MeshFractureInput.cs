using System.Collections.Generic;
using UnityEngine;

namespace Framework.Generators
{
    /// <summary>
    /// One closed solid in output-local space. A range can address a module already packed into a single-submesh mesh.
    /// </summary>
    public readonly struct MeshFractureInput
    {
        public Mesh Mesh { get; }
        public Matrix4x4 LocalTransform { get; }
        public int FirstTriangle { get; }
        public int TriangleCount { get; }
        public string Name { get; }

        /// <param name="triangleCount">Zero selects the entire mesh. Positive counts select a range in submesh zero.</param>
        public MeshFractureInput(Mesh mesh, Matrix4x4 localTransform, string name = null,
            int firstTriangle = 0, int triangleCount = 0)
        {
            Mesh = mesh;
            LocalTransform = localTransform;
            Name = name;
            FirstTriangle = firstTriangle;
            TriangleCount = triangleCount;
        }

        public MeshFractureInput TransformBy(Matrix4x4 transform, string name = null) =>
            new MeshFractureInput(Mesh, transform * LocalTransform, name ?? Name, FirstTriangle, TriangleCount);
    }

    /// <summary>Supplies the closed solids behind a component's combined render mesh, in component-local space.</summary>
    public interface IMeshFractureSource
    {
        IEnumerable<MeshFractureInput> GetFractureInputs();
    }
}
