using UnityEngine;
using Object = UnityEngine.Object;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>A shared, repeating checkerboard used as the transparency backdrop behind gradient previews.</summary>
    internal static class EditorTextures
    {
        private static Texture2D checker;

        public static Texture2D Checker => checker != null ? checker : (checker = BuildChecker());

        private static Texture2D BuildChecker()
        {
            const int size = 16, cell = 8;
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, linear)
            {
                name = "GradientABCW_Checker",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                int cy = (y / cell) % 2;
                for (int x = 0; x < size; x++)
                {
                    int cx = (x / cell) % 2;
                    bool even = ((cx + cy) & 1) == 0;
                    Color c = even ? new Color(0.82f, 0.82f, 0.82f) : new Color(0.67f, 0.67f, 0.67f);
                    pixels[y * size + x] = linear ? (Color32)c.linear : (Color32)c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }

    /// <summary>Owns exactly one preview <see cref="Texture2D"/>, resizing and re-baking it in place.</summary>
    internal sealed class GradientPreviewTexture
    {
        private Texture2D texture;

        public Texture2D Texture => texture;

        public void Ensure(int width)
        {
            width = Mathf.Max(2, width);
            if (texture != null && texture.width == width)
                return;

            if (texture != null)
                Object.DestroyImmediate(texture);

            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            texture = new Texture2D(width, 1, TextureFormat.RGBA32, false, linear)
            {
                name = "GradientABCWPreview",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        public void Update(GradientABCW gradient, GradientLutOptions options)
        {
            if (texture == null)
                return;
            GradientTextureUtility.UpdateTexture(gradient, texture, options);
        }

        public void Dispose()
        {
            if (texture != null)
                Object.DestroyImmediate(texture);
            texture = null;
        }
    }
}
