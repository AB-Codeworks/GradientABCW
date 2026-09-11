namespace ABCodeworld.Gradients
{
    /// <summary>
    /// An unmanaged, Burst-friendly snapshot of a <see cref="GradientABCW3D"/>: keys as structure-of-arrays
    /// fixed buffers plus flattened blending and modulation parameters. Built once per
    /// <see cref="GradientABCW3D.Version"/> change and consumed by <see cref="GradientMath3D"/>,
    /// <see cref="GradientLut3D"/> and the evaluation jobs.
    /// </summary>
    /// <remarks>
    /// About 2.6 KiB, roughly three times the 1D snapshot: 64 keys per lane rather than 32, and three
    /// position components per key rather than one time. That is still fine to pass by value into a job —
    /// <c>IJobParallelFor</c> copies the struct once per <c>Schedule</c>, not once per <c>Execute</c>, and
    /// against a 32768-cell bake one 2.6 KiB copy is noise. It also still fits comfortably in L1, so the
    /// inner loop streams from cache. The managed path never copies it at all, because
    /// <see cref="GradientABCW3D.Native"/> hands back a <c>ref readonly</c>.
    /// <para>
    /// Structure-of-arrays rather than interleaved positions, so that a later vectorisation pass can load
    /// four keys' x, y and z as three <c>float4</c>s. Interleaving would be the same size and strictly
    /// harder to widen.
    /// </para>
    /// <para>
    /// Not marked <c>readonly</c>: <see cref="From"/> fills the struct in place after default-constructing
    /// it, exactly as <see cref="NativeGradient.From"/> does.
    /// </para>
    /// </remarks>
    public unsafe struct NativeGradient3D
    {

        internal fixed float colorX[GradientABCW3D.MaxKeys];
        internal fixed float colorY[GradientABCW3D.MaxKeys];
        internal fixed float colorZ[GradientABCW3D.MaxKeys];
        internal fixed float colorR[GradientABCW3D.MaxKeys];
        internal fixed float colorG[GradientABCW3D.MaxKeys];
        internal fixed float colorB[GradientABCW3D.MaxKeys];
        internal fixed float alphaX[GradientABCW3D.MaxKeys];
        internal fixed float alphaY[GradientABCW3D.MaxKeys];
        internal fixed float alphaZ[GradientABCW3D.MaxKeys];
        internal fixed float alphaValues[GradientABCW3D.MaxKeys];
        internal int colorCount;
        internal int alphaCount;
        internal byte stepped;

        /// <summary>
        /// Precomputed as -0.5 * falloff power, so the weight is one <c>pow</c> of a squared distance
        /// ratio rather than a <c>sqrt</c> followed by a <c>pow</c>.
        /// </summary>
        internal float falloffExponent;

        /// <summary>
        /// Precomputed: the falloff power is exactly 2, the default and by far the most common value, for
        /// which the weight collapses to a single divide and no <c>pow</c> at all.
        /// </summary>
        internal byte falloffIsSquared;

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

        public static NativeGradient3D From(GradientABCW3D gradient)
        {
            if (gradient is null)
                throw new System.ArgumentNullException(nameof(gradient));

            var native = default(NativeGradient3D);

            var colorKeys = gradient.ColorKeys;
            native.colorCount = colorKeys.Length;
            for (int i = 0; i < colorKeys.Length; i++)
            {
                native.colorX[i] = colorKeys[i].position.x;
                native.colorY[i] = colorKeys[i].position.y;
                native.colorZ[i] = colorKeys[i].position.z;
                native.colorR[i] = colorKeys[i].color.r;
                native.colorG[i] = colorKeys[i].color.g;
                native.colorB[i] = colorKeys[i].color.b;
            }

            var alphaKeys = gradient.AlphaKeys;
            native.alphaCount = alphaKeys.Length;
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                native.alphaX[i] = alphaKeys[i].position.x;
                native.alphaY[i] = alphaKeys[i].position.y;
                native.alphaZ[i] = alphaKeys[i].position.z;
                native.alphaValues[i] = alphaKeys[i].alpha;
            }

            native.stepped = (byte)(gradient.BlendMode == BlendMode.Stepped ? 1 : 0);

            float falloff = gradient.FalloffPower;
            native.falloffExponent = -0.5f * falloff;
            native.falloffIsSquared = (byte)(falloff == 2f ? 1 : 0);

            var m = gradient.Modulation;
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
