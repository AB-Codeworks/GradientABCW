using UnityEngine;
using Object = UnityEngine.Object;

namespace ABCodeworld.Gradients.Editor
{
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

            // sRGB storage (linear: false), paired with writing sRGB bytes rather than pow()-converted
            // ones. Storing linear values in 8 bits crushes precision at the dark end, which showed as
            // banding in the darker part of a gradient; going through the texture's own sRGB read gives
            // both better quality and one less conversion per pixel.
            texture = new Texture2D(width, 1, TextureFormat.RGBA32, false, linear: false)
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
