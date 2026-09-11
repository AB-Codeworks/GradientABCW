using Unity.Mathematics;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// 3D gradient evaluation math, shared by the managed evaluation path and the Burst-compiled jobs in
    /// <see cref="GradientLut3D"/>. The 3D counterpart of <see cref="GradientMath"/>, and like it not
    /// itself Burst-compiled: it is called both from plain C# (<see cref="GradientABCW3D.Evaluate"/>) and,
    /// inlined, from inside the bake and evaluate jobs, which are what pick up Burst's speed for bulk work.
    /// </summary>
    /// <remarks>
    /// <para><b>Smooth</b> is inverse-distance weighting over every key — Shepard's method — with the
    /// exponent taken from <see cref="GradientABCW3D.FalloffPower"/>:</para>
    /// <code>c(p) = sum(c_i * w_i) / sum(w_i),  w_i = 1 / |p - p_i|^k</code>
    /// <para>written here in the equivalent form that divides every weight through by the distance to the
    /// nearest key. That rescaling cancels out of the quotient, so it changes nothing about the result,
    /// but it changes everything about the arithmetic: the nearest key's weight becomes exactly 1 and
    /// every other weight lands in (0, 1], so the sum is bounded by the key count and cannot overflow at
    /// any exponent. The naive form cannot survive k = 8 — a sample a micron from a key gives a weight
    /// around 1e48, which is infinity in float32, and infinity over infinity is NaN. Distant keys instead
    /// underflow harmlessly to zero, and the sum always contains that leading 1, so the division is never
    /// by zero.</para>
    /// <para><b>Stepped</b> is the nearest key outright, which partitions the cube into Voronoi cells —
    /// the 3D reading of what stepped blending does to a line.</para>
    /// <para>Two keys are allowed to share a position, exactly as two 1D keys may share a time. Away from
    /// the shared point they carry equal weight and average; at it, the lower-indexed key wins, because
    /// the nearest-key search below breaks ties towards the lower index.</para>
    /// <para>Each lane runs its key search twice for a smooth sample: once to find the nearest key, once
    /// to accumulate. Caching the distances would trade that for a stack buffer, and is left to a later
    /// optimisation pass along with vectorising the loops — which is what the structure-of-arrays layout
    /// in <see cref="NativeGradient3D"/> exists for.</para>
    /// </remarks>
    public static unsafe class GradientMath3D
    {
        /// <summary>
        /// Squared distance below which a sample counts as sitting exactly on a key, and takes that key's
        /// value verbatim.
        /// </summary>
        /// <remarks>
        /// This is what makes "the colour at a key is that key's colour" an invariant rather than a
        /// coincidence of rounding, and it is also the case that would otherwise divide by zero.
        /// 1e-12 in squared terms is a distance of a micron across a unit cube — far below anything the
        /// authoring UI can express, and far above where float32 stops being able to divide safely.
        /// </remarks>
        internal const float ExactHitSqEpsilon = 1e-12f;

        public static float4 Evaluate(in NativeGradient3D g, float3 p)
        {
            p = math.saturate(p);
            bool modulate = g.modEffective != 0;
            float3 sampleP = modulate ? TransformP(in g, p) : p;

            float4 c = g.stepped != 0 ? SampleNearest(in g, sampleP) : SampleSmooth(in g, sampleP);
            if (modulate)
                c = GradientDomain.ApplyHsba(c, g.modNeedsHsv, g.modHueShift, g.modSaturation, g.modBrightness, g.modAlpha);
            return c;
        }

        public static float4 EvaluateBase(in NativeGradient3D g, float3 p)
        {
            p = math.saturate(p);
            return g.stepped != 0 ? SampleNearest(in g, p) : SampleSmooth(in g, p);
        }

        /// <summary>
        /// Maps the evaluation domain through offset, repeat count, repeat mode and reverse, applying the
        /// same one-dimensional transform independently to each axis.
        /// </summary>
        /// <remarks>
        /// Every domain parameter is therefore shared across the three axes rather than being per-axis,
        /// which has two consequences worth stating: a repeat count of 2 tiles the cube 2x2x2, eight times
        /// over rather than twice, and reverse mirrors all three axes at once, which flips handedness
        /// rather than spinning the cube around.
        /// </remarks>
        internal static float3 TransformP(in NativeGradient3D g, float3 p) => new float3(
            GradientDomain.TransformT(p.x, g.modOffset, g.modRepeats, g.modRepeatMode, g.modReverse),
            GradientDomain.TransformT(p.y, g.modOffset, g.modRepeats, g.modRepeatMode, g.modReverse),
            GradientDomain.TransformT(p.z, g.modOffset, g.modRepeats, g.modRepeatMode, g.modReverse));

        private static float4 SampleSmooth(in NativeGradient3D g, float3 p)
        {
            float3 rgb = EvaluateColorSmooth(in g, p);
            float a = EvaluateAlphaSmooth(in g, p);
            return new float4(rgb, a);
        }

        private static float4 SampleNearest(in NativeGradient3D g, float3 p)
        {
            float3 rgb = SampleNearestColor(in g, p);
            float a = SampleNearestAlpha(in g, p);
            return new float4(rgb, a);
        }

        // The colour and alpha lanes below are the same algorithm over different buffers, kept as two
        // copies for the reason recorded in GradientMath: a shared helper taking a float* was measured
        // there and reverted, because the Editor's Mono JIT would not inline it. What the two lanes do
        // share is the nearest-key search, which is a lane-typed helper rather than a pointer-typed one,
        // and which a smooth sample needs anyway before it can normalize its weights. Keep the two copies
        // in step by hand.

        /// <summary>
        /// Index of the colour key nearest <paramref name="p"/>, with its squared distance. Ties go to the
        /// lower index, so a position shared by two keys resolves to whichever was added first.
        /// </summary>
        private static int NearestColorIndex(in NativeGradient3D g, float3 p, out float nearestSq)
        {
            int n = g.colorCount;
            int nearest = 0;
            float best = float.PositiveInfinity;

            for (int i = 0; i < n; i++)
            {
                float3 d = p - new float3(g.colorX[i], g.colorY[i], g.colorZ[i]);
                float d2 = math.dot(d, d);
                if (d2 < best)
                {
                    best = d2;
                    nearest = i;
                }
            }

            nearestSq = best;
            return nearest;
        }

        /// <inheritdoc cref="NearestColorIndex"/>
        private static int NearestAlphaIndex(in NativeGradient3D g, float3 p, out float nearestSq)
        {
            int n = g.alphaCount;
            int nearest = 0;
            float best = float.PositiveInfinity;

            for (int i = 0; i < n; i++)
            {
                float3 d = p - new float3(g.alphaX[i], g.alphaY[i], g.alphaZ[i]);
                float d2 = math.dot(d, d);
                if (d2 < best)
                {
                    best = d2;
                    nearest = i;
                }
            }

            nearestSq = best;
            return nearest;
        }

        private static float3 EvaluateColorSmooth(in NativeGradient3D g, float3 p)
        {
            int n = g.colorCount;
            if (n <= 0)
                return float3.zero;

            int nearest = NearestColorIndex(in g, p, out float nearestSq);

            // Sitting on a key, or as good as. Also covers the single-key case, where the loop below would
            // give the same answer the long way round.
            if (nearestSq <= ExactHitSqEpsilon)
                return new float3(g.colorR[nearest], g.colorG[nearest], g.colorB[nearest]);

            bool squared = g.falloffIsSquared != 0;
            float3 acc = float3.zero;
            float weightSum = 0f;

            for (int i = 0; i < n; i++)
            {
                float3 d = p - new float3(g.colorX[i], g.colorY[i], g.colorZ[i]);
                float ratio = math.dot(d, d) / nearestSq;

                // The nearest key's ratio is exactly 1, so its weight is exactly 1 and every other weight
                // is at most 1. That is the whole reason for dividing by nearestSq.
                float w = squared ? 1f / ratio : math.pow(ratio, g.falloffExponent);

                acc += new float3(g.colorR[i], g.colorG[i], g.colorB[i]) * w;
                weightSum += w;
            }

            return acc / weightSum;
        }

        private static float EvaluateAlphaSmooth(in NativeGradient3D g, float3 p)
        {
            int n = g.alphaCount;
            if (n <= 0)
                return 1f;

            int nearest = NearestAlphaIndex(in g, p, out float nearestSq);

            if (nearestSq <= ExactHitSqEpsilon)
                return g.alphaValues[nearest];

            bool squared = g.falloffIsSquared != 0;
            float acc = 0f;
            float weightSum = 0f;

            for (int i = 0; i < n; i++)
            {
                float3 d = p - new float3(g.alphaX[i], g.alphaY[i], g.alphaZ[i]);
                float ratio = math.dot(d, d) / nearestSq;
                float w = squared ? 1f / ratio : math.pow(ratio, g.falloffExponent);

                acc += g.alphaValues[i] * w;
                weightSum += w;
            }

            return acc / weightSum;
        }

        private static float3 SampleNearestColor(in NativeGradient3D g, float3 p)
        {
            if (g.colorCount <= 0)
                return float3.zero;

            int nearest = NearestColorIndex(in g, p, out _);
            return new float3(g.colorR[nearest], g.colorG[nearest], g.colorB[nearest]);
        }

        private static float SampleNearestAlpha(in NativeGradient3D g, float3 p)
        {
            if (g.alphaCount <= 0)
                return 1f;

            return g.alphaValues[NearestAlphaIndex(in g, p, out _)];
        }
    }
}
