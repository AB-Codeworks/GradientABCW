using Unity.Mathematics;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// Gradient evaluation math shared by the managed evaluation path and the Burst-compiled jobs
    /// in <see cref="GradientLut"/>. Not itself Burst-compiled: called both from plain C#
    /// (<see cref="GradientABCW.Evaluate"/>) and, inlined, from inside <c>BakeLutJob</c> /
    /// <c>EvaluateBatchJob</c>, which are what pick up the Burst-compiled speed for bulk work.
    /// </summary>
    public static unsafe class GradientMath
    {
        public static float4 Evaluate(in NativeGradient g, float t)
        {
            t = math.saturate(t);
            bool modulate = g.modEffective != 0;
            float sampleT = modulate ? TransformT(in g, t) : t;

            float4 c = g.stepped != 0 ? SampleStepped(in g, sampleT) : SampleSmooth(in g, sampleT);
            if (modulate)
                c = ApplyHsba(in g, c);
            return c;
        }

        public static float4 EvaluateBase(in NativeGradient g, float t)
        {
            t = math.saturate(t);
            return g.stepped != 0 ? SampleStepped(in g, t) : SampleSmooth(in g, t);
        }

        /// <summary>
        /// Maps <paramref name="t"/> through this gradient's offset, repeat count, repeat mode and
        /// reverse. The transform itself lives in <see cref="GradientDomain"/>, shared with the 3D core,
        /// which applies the very same mapping once per axis.
        /// </summary>
        internal static float TransformT(in NativeGradient g, float t) =>
            GradientDomain.TransformT(t, g.modOffset, g.modRepeats, g.modRepeatMode, g.modReverse);

        private static float4 SampleSmooth(in NativeGradient g, float t)
        {
            float3 rgb = EvaluateColorSmooth(in g, t);
            float a = EvaluateAlphaSmooth(in g, t);
            return new float4(rgb, a);
        }

        private static float4 SampleStepped(in NativeGradient g, float t)
        {
            float3 rgb = SampleSteppedColor(in g, t);
            float a = SampleSteppedAlpha(in g, t);
            return new float4(rgb, a);
        }

        // The colour and alpha lanes below run the same search twice over. Factoring it into a shared
        // helper taking a float* was measured and reverted: the Editor's Mono JIT would not inline the
        // helper, and the resulting call plus the `fixed` pin cost ~7% on the unmodulated path, which is
        // the single most common way this code is entered. The duplication buys back that 7%. Keep the
        // two copies in step by hand.

        private static float3 EvaluateColorSmooth(in NativeGradient g, float t)
        {
            int n = g.colorCount;

            // A two-key ramp is by far the most common gradient, and for it the general search below
            // collapses to one lerp. Peeling it out skips the endpoint tests and the loop setup entirely.
            if (n == 2)
            {
                float t0 = g.colorTimes[0], t1 = g.colorTimes[1];
                var lo2 = new float3(g.colorR[0], g.colorG[0], g.colorB[0]);
                if (t <= t0)
                    return lo2;
                var hi2 = new float3(g.colorR[1], g.colorG[1], g.colorB[1]);
                if (t >= t1)
                    return hi2;
                float span2 = t1 - t0;
                return math.lerp(lo2, hi2, span2 > 1e-9f ? (t - t0) / span2 : 0f);
            }

            if (n == 1)
                return new float3(g.colorR[0], g.colorG[0], g.colorB[0]);
            if (t <= g.colorTimes[0])
                return new float3(g.colorR[0], g.colorG[0], g.colorB[0]);
            if (t >= g.colorTimes[n - 1])
                return new float3(g.colorR[n - 1], g.colorG[n - 1], g.colorB[n - 1]);

            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (t >= g.colorTimes[mid]) lo = mid;
                else hi = mid;
            }

            float span = g.colorTimes[hi] - g.colorTimes[lo];
            float u = span > 1e-9f ? (t - g.colorTimes[lo]) / span : 0f;
            var a = new float3(g.colorR[lo], g.colorG[lo], g.colorB[lo]);
            var b = new float3(g.colorR[hi], g.colorG[hi], g.colorB[hi]);
            return math.lerp(a, b, u);
        }

        private static float EvaluateAlphaSmooth(in NativeGradient g, float t)
        {
            int n = g.alphaCount;

            if (n == 2)
            {
                float t0 = g.alphaTimes[0], t1 = g.alphaTimes[1];
                if (t <= t0)
                    return g.alphaValues[0];
                if (t >= t1)
                    return g.alphaValues[1];
                float span2 = t1 - t0;
                return math.lerp(g.alphaValues[0], g.alphaValues[1], span2 > 1e-9f ? (t - t0) / span2 : 0f);
            }

            if (n == 1)
                return g.alphaValues[0];
            if (t <= g.alphaTimes[0])
                return g.alphaValues[0];
            if (t >= g.alphaTimes[n - 1])
                return g.alphaValues[n - 1];

            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (t >= g.alphaTimes[mid]) lo = mid;
                else hi = mid;
            }

            float span = g.alphaTimes[hi] - g.alphaTimes[lo];
            float u = span > 1e-9f ? (t - g.alphaTimes[lo]) / span : 0f;
            return math.lerp(g.alphaValues[lo], g.alphaValues[hi], u);
        }

        // Stepped sampling picks the first key whose midpoint with its successor exceeds t. Midpoints are
        // non-decreasing because key times are sorted, so a binary search selects exactly the same bucket
        // as the linear scan these replace, in O(log n) instead of O(n).
        //
        // The midpoint MUST be rounded to float32 in a local before the comparison, exactly as the linear
        // scan did. Inlining it into the `if` lets the JIT keep the intermediate at extended precision,
        // and for a sample sitting exactly on a midpoint that flips the comparison and selects the
        // neighbouring key. With seven keys at i/6 this is reachable in practice: the midpoint of keys 1
        // and 2 is 0.25 in float32 but 0.2500000075 kept wide, so a sample at exactly 0.25 lands one key
        // to the left. Do not fold these locals back into the conditions.

        private static float3 SampleSteppedColor(in NativeGradient g, float t)
        {
            int n = g.colorCount;
            if (n == 1)
                return new float3(g.colorR[0], g.colorG[0], g.colorB[0]);

            int lo = 0, hi = n - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                float boundary = 0.5f * (g.colorTimes[mid] + g.colorTimes[mid + 1]);
                if (t < boundary) hi = mid;
                else lo = mid + 1;
            }
            return new float3(g.colorR[lo], g.colorG[lo], g.colorB[lo]);
        }

        private static float SampleSteppedAlpha(in NativeGradient g, float t)
        {
            int n = g.alphaCount;
            if (n == 1)
                return g.alphaValues[0];

            int lo = 0, hi = n - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                float boundary = 0.5f * (g.alphaTimes[mid] + g.alphaTimes[mid + 1]);
                if (t < boundary) hi = mid;
                else lo = mid + 1;
            }
            return g.alphaValues[lo];
        }

        /// <summary>
        /// Applies this gradient's hue, saturation, brightness and alpha adjustment. The adjustment itself
        /// lives in <see cref="GradientDomain"/>, shared with the 3D core.
        /// </summary>
        private static float4 ApplyHsba(in NativeGradient g, float4 c) =>
            GradientDomain.ApplyHsba(c, g.modNeedsHsv, g.modHueShift, g.modSaturation, g.modBrightness, g.modAlpha);

    }
}
