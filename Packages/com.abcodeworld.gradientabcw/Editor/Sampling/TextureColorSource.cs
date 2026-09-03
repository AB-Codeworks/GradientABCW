using UnityEditor;
using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    internal readonly struct TextureColorSource : IColorSource
    {
        private readonly Color32[] colors;

        /// <summary>Non-null when the texture could not be sampled; check before using the source.</summary>
        public string Error { get; }

        public TextureColorSource(Texture2D texture)
        {
            if (texture == null)
            {
                colors = System.Array.Empty<Color32>();
                Error = "No texture selected.";
                return;
            }
            if (texture.width <= 0 || texture.height <= 0)
            {
                colors = System.Array.Empty<Color32>();
                Error = "Texture has no pixels.";
                return;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is TextureImporter importer && !importer.isReadable)
            {
                colors = System.Array.Empty<Color32>();
                Error = "Texture is not readable. Enable Read/Write on the importer.";
                return;
            }

            colors = texture.GetPixels32();
            Error = null;
        }

        public int Count => colors.Length;
        public Color32 this[int index] => colors[index];
    }
}
