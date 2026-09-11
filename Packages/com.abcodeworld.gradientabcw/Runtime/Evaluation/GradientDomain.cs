using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// The two halves of evaluation-time modulation, expressed over loose parameters rather than over a
    /// particular gradient snapshot: <see cref="TransformT"/> maps one axis of the evaluation domain, and
    /// <see cref="ApplyHsba"/> adjusts one sampled colour.
    /// </summary>
    /// <remarks>
    /// Both bodies were moved here verbatim from <see cref="GradientMath"/> so that the 1D and 3D cores
    /// share one implementation: a 3D gradient's domain modulation is this same per-axis transform applied
    /// to x, y and z, and its colour modulation is byte-for-byte the 1D one. Nothing here knows what a key
    /// is, which is exactly why it can be shared.
    /// <para>
    /// Do not reorder any float operation in this file. <c>GradientModulationGoldenTests</c> and
    /// <c>GradientEquivalenceTests</c> compare live output against a frozen 1.0.0 oracle at 1e-5 and 1e-6
    /// respectively, and the two load-bearing details below are the ones that move results if touched:
    /// the <c>t &gt;= 1f</c> pull-in at the top of <see cref="TransformT"/> (which decides what Wrap and
    /// Mirror return at exactly 1), and the <c>needsHsv</c> / brightness guards in
    /// <see cref="ApplyHsba"/> (which decide whether the RGB-HSV round trip runs at all).
    /// </para>
    /// <para>
    /// Both are marked <see cref="MethodImplOptions.AggressiveInlining"/>. They were a private and an
    /// internal method of their caller's own class before this extraction, so the Editor's Mono JIT could
    /// inline them freely; the attribute keeps that true now that they live behind a class boundary. The
    /// package has already measured one 7% regression from a helper the JIT declined to inline (see the
    /// comment above the duplicated colour and alpha lanes in <see cref="GradientMath"/>), so this is not
    /// a theoretical concern.
    /// </para>
    /// </remarks>
    internal static class GradientDomain
    {
        internal const int Clamp = 0;
        internal const int Wrap = 1;
        internal const int Mirror = 2;

        /// <summary>
        /// Maps <paramref name="t"/> through offset, repeat count, repeat mode and reverse, in that order,
        /// and returns the position actually sampled. Saturated on both entry and exit.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float TransformT(float t, float offset, float repeats, byte repeatMode, byte reverse)
        {
            if (t >= 1f) t = 1f - 1e-7f;

            float td = t + offset;

            float r = repeats <= 0f ? 1f : repeats;
            bool repeating = r != 1f;
            bool periodic = repeatMode == Wrap || repeatMode == Mirror;

            if (!repeating && !periodic && repeatMode == Clamp)
                td = math.saturate(td);
            else
                td -= math.floor(td);

            if (repeating || periodic)
            {
                float scaled = td * r;
                if (repeatMode == Clamp)
                {
                    td = math.min(scaled, 1f);
                }
                else if (repeatMode == Wrap)
                {
                    td = scaled - math.floor(scaled);
                }
                else // Mirror
                {
                    int cycle = (int)math.floor(scaled);
                    float f = scaled - cycle;
                    bool odd = (cycle & 1) == 1;
                    td = odd ? (1f - f) : f;
                }
            }

            if (reverse != 0)
                td = 1f - td;

            return math.saturate(td);
        }

        /// <summary>
        /// Chroma, relative to the brightest channel, below which a colour counts as grey and its hue as
        /// meaningless. Two orders of magnitude above the evaluation noise that produced the bug below,
        /// and two and a half below the smallest chroma an 8-bit channel can even represent (1/255 of a
        /// mid grey is about 5.7e-3), so no colour a bake can distinguish is caught by it.
        /// </summary>
        private const float AchromaticChroma = 1e-5f;

        /// <summary>
        /// Whether <paramref name="rgb"/> has a hue at all — that is, whether its channels differ by more
        /// than evaluation noise.
        /// </summary>
        /// <remarks>
        /// A grey has no hue, and <see cref="ColorSpaceMath.RgbToHsv"/> cannot say so: its three-way
        /// dominant-channel branch resolves on whichever of r, g and b happens to be a last-ulp larger, so
        /// the hue it reports for a grey is whichever of the six sectors the rounding fell into. That is
        /// harmless as long as saturation is left alone, because <c>HsvToRgb</c> reconstructs the same grey
        /// from any hue — but a positive saturation promotes that arbitrary hue to a fully saturated
        /// colour, and then the answer depends on nothing but float rounding.
        /// <para>
        /// This was not theoretical. A 3D gradient's grey diagonal baked red through the managed loop and
        /// cyan through the Burst job, 43/255 apart, because the two rounded the evaluation differently
        /// enough to land in different sectors — nine cells of a 16-cubed table, all of them on or beside
        /// the diagonal, each picking one of exactly six hues. Neither answer was more correct than the
        /// other; both were arbitrary. Saturating a grey is a no-op and shifting its hue is a no-op, so
        /// skipping the round trip is both the fix and the definition.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasHue(float3 rgb)
        {
            float max = math.cmax(rgb);
            return max > 0f && (max - math.cmin(rgb)) > AchromaticChroma * max;
        }

        /// <summary>
        /// Applies hue, saturation, brightness and alpha adjustment to one sampled colour.
        /// <paramref name="needsHsv"/> is precomputed by the caller's snapshot and is non-zero only when
        /// hue or saturation is actually adjusted.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float4 ApplyHsba(float4 c, byte needsHsv, float hueShift, float saturation, float brightness, float alpha)
        {
            float3 rgb = c.xyz;

            // Only hue and saturation need HSV. Brightness and alpha are plain lerps in RGB, so dimming or
            // fading a gradient — a common case on its own — no longer pays for a full round trip per
            // sample. When hue and saturation are both neutral the round trip was returning its input.
            if (needsHsv != 0 && HasHue(rgb))
            {
                float3 hsv = ColorSpaceMath.RgbToHsv(rgb);

                float h = hsv.x + hueShift;
                h -= math.floor(h);

                float s = saturation >= 0f ? math.lerp(hsv.y, 1f, saturation) : math.lerp(hsv.y, 0f, -saturation);
                s = math.saturate(s);

                rgb = ColorSpaceMath.HsvToRgb(new float3(h, s, hsv.z));
            }

            float a = c.w;

            if (math.abs(brightness) > 1e-6f)
            {
                rgb = brightness > 0f
                    ? math.lerp(rgb, new float3(1f), math.saturate(brightness))
                    : math.lerp(rgb, float3.zero, math.saturate(-brightness));
            }

            if (alpha > 0f) a = math.lerp(a, 1f, math.saturate(alpha));
            else if (alpha < 0f) a = math.lerp(a, 0f, math.saturate(-alpha));

            return new float4(rgb, math.saturate(a));
        }
    }
}
