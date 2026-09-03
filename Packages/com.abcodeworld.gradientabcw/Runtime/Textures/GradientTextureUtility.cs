using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>Creates and updates 1D preview/runtime textures from a gradient's LUT.</summary>
    public static class GradientTextureUtility
    {
        public static bool ProjectIsLinear => QualitySettings.activeColorSpace == ColorSpace.Linear;

        public static Texture2D CreateTexture(GradientABCW gradient, int width = 256, GradientLutOptions? options = null, string name = "GradientABCW")
        {
            var tex = new Texture2D(Mathf.Max(2, width), 1, TextureFormat.RGBA32, false, ProjectIsLinear)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = name,
            };
            UpdateTexture(gradient, tex, options);
            return tex;
        }

        /// <summary>Re-bakes <paramref name="tex"/> in place; does not resize or reallocate it.</summary>
        public static void UpdateTexture(GradientABCW gradient, Texture2D tex, GradientLutOptions? options = null)
        {
            var opts = options ?? GradientLutOptions.Project(final: true);
            var data = tex.GetPixelData<Color32>(0);
            GradientLut.Bake(gradient, data, opts);
            tex.Apply(false, false);
        }
    }
}
