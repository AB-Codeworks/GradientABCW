using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    internal readonly struct MeshColorSource : IColorSource
    {
        private readonly Color32[] colors;

        public MeshColorSource(Mesh mesh)
        {
            colors = mesh != null ? mesh.colors32 : System.Array.Empty<Color32>();
        }

        public int Count => colors.Length;
        public Color32 this[int index] => colors[index];
    }
}
