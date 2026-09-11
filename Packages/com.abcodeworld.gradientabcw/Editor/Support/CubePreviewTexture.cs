using UnityEngine;
using Object = UnityEngine.Object;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Owns exactly one cube-preview <see cref="Texture2D"/>, resizing and re-rendering it in place.</summary>
    /// <remarks>
    /// The 3D counterpart of <see cref="GradientPreviewTexture"/>, and carries the same lifetime
    /// discipline: the texture is <see cref="HideFlags.HideAndDontSave"/>, so nothing else will collect
    /// it, and every path that replaces or drops it destroys the old one first.
    /// </remarks>
    internal sealed class CubePreviewTexture
    {
        /// <summary>
        /// Named apart from the 1D preview texture on purpose, so a leak check can tell which kind of
        /// preview stranded a texture.
        /// </summary>
        internal const string TextureName = "GradientABCW3DCubePreview";

        private Texture2D texture;
        private Color32[] pixels;

        public Texture2D Texture => texture;

        /// <summary>Pixels of the last render, in the texture's own bottom-up row order. Exposed for tests.</summary>
        internal Color32[] Pixels => pixels;

        public void Ensure(int size)
        {
            size = Mathf.Max(1, size);
            if (texture != null && texture.width == size)
                return;

            if (texture != null)
                Object.DestroyImmediate(texture);

            // sRGB storage (linear: false), paired with the sRGB bytes CubePreviewRasterizer writes, for
            // the reason GradientPreviewTexture documents: 8-bit linear storage crushes the dark end.
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false, linear: false)
            {
                name = TextureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            pixels = new Color32[size * size];
        }

        public void Render(GradientABCW3D gradient, in CubeView view, bool includeModulation,
            CubeBackdrop backdrop = CubeBackdrop.Transparent)
        {
            if (texture == null || gradient == null)
                return;

            CubePreviewRasterizer.Render(gradient, in view, texture.width, pixels, includeModulation, backdrop);
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, false);
        }

        public void Dispose()
        {
            if (texture != null)
                Object.DestroyImmediate(texture);
            texture = null;
            pixels = null;
        }
    }
}
