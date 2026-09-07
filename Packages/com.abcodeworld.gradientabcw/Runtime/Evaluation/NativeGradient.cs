namespace ABCodeworld.Gradients
{
    /// <summary>
    /// An unmanaged, Burst-friendly snapshot of a <see cref="GradientABCW"/>: keys as structure-of-arrays
    /// fixed buffers plus flattened modulation parameters. Built once per <see cref="GradientABCW.Version"/>
    /// change and consumed by <see cref="GradientMath"/>, <see cref="GradientLut"/> and the evaluation jobs.
    /// </summary>
    public unsafe struct NativeGradient
    {
        public const int MaxKeys = GradientABCW.MaxKeys;

        internal fixed float colorTimes[MaxKeys];
        internal fixed float colorR[MaxKeys];
        internal fixed float colorG[MaxKeys];
        internal fixed float colorB[MaxKeys];
        internal fixed float alphaTimes[MaxKeys];
        internal fixed float alphaValues[MaxKeys];
        internal int colorCount;
        internal int alphaCount;
        internal byte stepped;

        internal byte modBypass;
        internal byte modReverse;
        internal float modRepeats;
        internal byte modRepeatMode;
        internal float modOffset;
        internal float modHueShift;
        internal float modSaturation;
        internal float modBrightness;
        internal float modAlpha;

        /// <summary>Precomputed so the hot path is a single branch: modulation is neither bypassed nor at its identity value.</summary>
        internal byte modEffective;

        /// <summary>
        /// Precomputed: true only when hue or saturation is actually adjusted, and the RGB to HSV round
        /// trip is therefore unavoidable. Brightness and alpha are plain lerps in RGB, so the common case
        /// of dimming or fading a gradient can skip the conversion entirely.
        /// </summary>
        internal byte modNeedsHsv;

        public static NativeGradient From(GradientABCW gradient)
        {
            if (gradient is null)
                throw new System.ArgumentNullException(nameof(gradient));

            var native = default(NativeGradient);

            var colorKeys = gradient.ColorKeys;
            native.colorCount = colorKeys.Length;
            for (int i = 0; i < colorKeys.Length; i++)
            {
                native.colorTimes[i] = colorKeys[i].time;
                native.colorR[i] = colorKeys[i].color.r;
                native.colorG[i] = colorKeys[i].color.g;
                native.colorB[i] = colorKeys[i].color.b;
            }

            var alphaKeys = gradient.AlphaKeys;
            native.alphaCount = alphaKeys.Length;
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                native.alphaTimes[i] = alphaKeys[i].time;
                native.alphaValues[i] = alphaKeys[i].alpha;
            }

            native.stepped = (byte)(gradient.BlendMode == BlendMode.Stepped ? 1 : 0);

            var m = gradient.Modulation;
            native.modBypass = (byte)(m.bypass ? 1 : 0);
            native.modReverse = (byte)(m.reverse ? 1 : 0);
            native.modRepeats = m.repeats;
            native.modRepeatMode = (byte)m.repeatMode;
            native.modOffset = m.offset;
            native.modHueShift = m.hueShift;
            native.modSaturation = m.saturation;
            native.modBrightness = m.brightness;
            native.modAlpha = m.alpha;
            native.modEffective = (byte)(m.IsEffective ? 1 : 0);
            native.modNeedsHsv = (byte)(UnityEngine.Mathf.Abs(m.hueShift) > 1e-6f || UnityEngine.Mathf.Abs(m.saturation) > 1e-6f ? 1 : 0);

            return native;
        }
    }
}
