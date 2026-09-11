using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// A mesh's vertices as colours with positions, each vertex normalized into the mesh's own bounds.
    /// </summary>
    /// <remarks>
    /// This is where 3D sampling earns its keep. Sampling a mesh into a 1D gradient can only take its
    /// palette and then has to invent an order; here the vertex positions are exactly the gradient's
    /// domain, so the result reconstructs the mesh's actual colour volume.
    /// <para>
    /// <see cref="MeshColorSource"/> is left alone rather than extended, so nothing about the existing 1D
    /// mesh sampling changes.
    /// </para>
    /// </remarks>
    internal readonly struct MeshPositionColorSource : IPositionedColorSource
    {
        private readonly Color32[] colors;
        private readonly Vector3[] positions;

        /// <summary>Non-null when the mesh could not be sampled; check before using the source.</summary>
        public string Error { get; }

        public MeshPositionColorSource(Mesh mesh)
        {
            if (mesh == null)
            {
                colors = System.Array.Empty<Color32>();
                positions = System.Array.Empty<Vector3>();
                Error = "No mesh selected.";
                return;
            }

            var meshColors = mesh.colors32;
            var vertices = mesh.vertices;

            if (meshColors == null || meshColors.Length == 0)
            {
                colors = System.Array.Empty<Color32>();
                positions = System.Array.Empty<Vector3>();
                Error = $"'{mesh.name}' has no vertex colours.";
                return;
            }

            int count = Mathf.Min(meshColors.Length, vertices.Length);
            colors = meshColors;
            positions = new Vector3[count];

            var bounds = mesh.bounds;
            Vector3 min = bounds.min;
            Vector3 size = bounds.size;

            for (int i = 0; i < count; i++)
                positions[i] = Normalize(vertices[i], min, size);

            Error = null;
        }

        public int Count => positions.Length;

        public Color32 this[int index] => colors[index];

        public Vector3 GetPosition(int index) => positions[index];

        /// <summary>
        /// Maps a vertex into [0, 1] per axis. A flat mesh has zero extent on one axis, which would divide
        /// by zero; those axes collapse to the middle of the cube instead.
        /// </summary>
        private static Vector3 Normalize(Vector3 vertex, Vector3 min, Vector3 size) => new Vector3(
            size.x > 1e-6f ? Mathf.Clamp01((vertex.x - min.x) / size.x) : 0.5f,
            size.y > 1e-6f ? Mathf.Clamp01((vertex.y - min.y) / size.y) : 0.5f,
            size.z > 1e-6f ? Mathf.Clamp01((vertex.z - min.z) / size.z) : 0.5f);
    }
}
