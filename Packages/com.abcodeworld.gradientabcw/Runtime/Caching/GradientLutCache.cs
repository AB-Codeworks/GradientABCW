using System;
using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>Re-bakes a gradient's LUT only when its content, options or size actually changed.</summary>
    public sealed class GradientLutCache
    {
        private Color32[] cache;
        private int cachedVersion = -1;
        private bool cachedIncludeModulation;
        private bool cachedToLinear;

        public ReadOnlySpan<Color32> Get(GradientABCW gradient, GradientLutOptions options, int size = 256)
        {
            if (gradient is null)
                throw new ArgumentNullException(nameof(gradient));

            bool dirty = cache == null || cache.Length != size ||
                         cachedVersion != gradient.Version ||
                         cachedIncludeModulation != options.includeModulation ||
                         cachedToLinear != options.toLinear;

            if (dirty)
            {
                if (cache == null || cache.Length != size)
                    cache = new Color32[size];

                GradientLut.Bake(gradient, cache, options);
                cachedVersion = gradient.Version;
                cachedIncludeModulation = options.includeModulation;
                cachedToLinear = options.toLinear;
            }

            return cache;
        }
    }
}
