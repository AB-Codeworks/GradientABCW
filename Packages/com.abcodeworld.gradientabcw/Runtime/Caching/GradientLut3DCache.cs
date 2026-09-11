using System;
using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>Re-bakes a 3D gradient's lookup table only when its content, options or size actually changed.</summary>
    /// <remarks>
    /// Worth more here than the 1D equivalent: a 32-cubed bake evaluates 32768 cells, each of which walks
    /// every key, so a redundant rebuild is not a rounding error in a frame.
    /// </remarks>
    public sealed class GradientLut3DCache
    {
        private Color32[] cache;
        private int cachedVersion = -1;
        private int cachedSize = -1;
        private bool cachedIncludeModulation;
        private bool cachedToLinear;

        public ReadOnlySpan<Color32> Get(GradientABCW3D gradient, GradientLutOptions options, int size = GradientLut3D.DefaultSize)
        {
            if (gradient is null)
                throw new ArgumentNullException(nameof(gradient));

            bool dirty = cache == null || cachedSize != size ||
                         cachedVersion != gradient.Version ||
                         cachedIncludeModulation != options.includeModulation ||
                         cachedToLinear != options.toLinear;

            if (dirty)
            {
                int count = GradientLut3D.VoxelCount(size);
                if (cache == null || cache.Length != count)
                    cache = new Color32[count];

                GradientLut3D.Bake(gradient, cache, size, options);
                cachedVersion = gradient.Version;
                cachedSize = size;
                cachedIncludeModulation = options.includeModulation;
                cachedToLinear = options.toLinear;
            }

            return cache;
        }
    }
}
