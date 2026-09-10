using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>Creates and updates <see cref="Texture3D"/> lookup volumes from a 3D gradient.</summary>
    /// <remarks>
    /// This is the point of baking to a fixed grid at all: a shader sampling the result gets hardware
    /// trilinear filtering for nothing, so 32 cubed and 128 KB is granular enough to use directly.
    /// Positions must be corrected by <see cref="GradientLut3D.ShaderScaleOffset"/> before sampling,
    /// because the bake puts cells on the domain's endpoints while a GPU samples cell centres.
    /// <para>
    /// Unlike <see cref="GradientTextureUtility"/>, this always stores sRGB bytes in an sRGB texture
    /// rather than following the project's colour space. Two reasons, both about 8-bit precision: the
    /// hardware sRGB read is both free and higher quality than a baked conversion, and storing linear
    /// values in 8 bits crushes the dark end into visible banding — which matters far more for a volume
    /// that is mostly interior than for a 1-pixel-tall strip. It is the same reasoning the editor previews
    /// already use, and the same convention as Unity's own colour-grading LUTs.
    /// </para>
    /// </remarks>
    public static class GradientTexture3DUtility
    {
        public static Texture3D CreateTexture(GradientABCW3D gradient, int size = GradientLut3D.DefaultSize, GradientLutOptions? options = null, string name = "GradientABCW3D")
        {
            size = Mathf.Clamp(size, GradientLut3D.MinSize, GradientLut3D.MaxSize);

            var tex = new Texture3D(size, size, size, TextureFormat.RGBA32, mipChain: false)
            {
                wrapMode = TextureWrapMode.Clamp,

                // Bilinear on a 3D texture is trilinear: the hardware blends across all three axes.
                filterMode = FilterMode.Bilinear,
                name = name,
            };
            UpdateTexture(gradient, tex, options);
            return tex;
        }

        /// <summary>Re-bakes <paramref name="tex"/> in place; does not resize or reallocate it.</summary>
        public static void UpdateTexture(GradientABCW3D gradient, Texture3D tex, GradientLutOptions? options = null)
        {
            if (tex == null)
                return;

            var opts = options ?? GradientLutOptions.Final;
            var data = tex.GetPixelData<Color32>(0);
            GradientLut3D.Bake(gradient, data, tex.width, opts);
            tex.Apply(false, false);
        }
    }
}
