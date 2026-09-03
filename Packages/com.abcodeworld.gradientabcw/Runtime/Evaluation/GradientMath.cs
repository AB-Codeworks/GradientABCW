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
        private const int Clamp = 0;
        private const int Wrap = 1;
        private const int Mirror = 2;

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

        internal static float TransformT(in NativeGradient g, float t)
        {
            if (t >= 1f) t = 1f - 1e-7f;

            float td = t + g.modOffset;

            float r = g.modRepeats <= 0f ? 1f : g.modRepeats;
            bool repeating = r != 1f;
            bool periodic = g.modRepeatMode == Wrap || g.modRepeatMode == Mirror;

            if (!repeating && !periodic && g.modRepeatMode == Clamp)
                td = math.saturate(td);
            else
                td -= math.floor(td);

            if (repeating || periodic)
            {
                float scaled = td * r;
                if (g.modRepeatMode == Clamp)
                {
                    td = math.min(scaled, 1f);
                }
                else if (g.modRepeatMode == Wrap)
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

            if (g.modReverse != 0)
                td = 1f - td;

            return math.saturate(td);
        }

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

        private static float3 EvaluateColorSmooth(in NativeGradient g, float t)
        {
            int n = g.colorCount;
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

        private static float3 SampleSteppedColor(in NativeGradient g, float t)
        {
            int n = g.colorCount;
            if (n == 1)
                return new float3(g.colorR[0], g.colorG[0], g.colorB[0]);

            int last = n - 1;
            for (int i = 0; i < last; i++)
            {
                float mid = 0.5f * (g.colorTimes[i] + g.colorTimes[i + 1]);
                if (t < mid)
                    return new float3(g.colorR[i], g.colorG[i], g.colorB[i]);
            }
            return new float3(g.colorR[last], g.colorG[last], g.colorB[last]);
        }

        private static float SampleSteppedAlpha(in NativeGradient g, float t)
        {
            int n = g.alphaCount;
            if (n == 1)
                return g.alphaValues[0];

            int last = n - 1;
            for (int i = 0; i < last; i++)
            {
                float mid = 0.5f * (g.alphaTimes[i] + g.alphaTimes[i + 1]);
                if (t < mid)
                    return g.alphaValues[i];
            }
            return g.alphaValues[last];
        }

        private static float4 ApplyHsba(in NativeGradient g, float4 c)
        {
            float3 hsv = RgbToHsv(c.xyz);

            float h = hsv.x + g.modHueShift;
            h -= math.floor(h);

            float s = g.modSaturation >= 0f ? math.lerp(hsv.y, 1f, g.modSaturation) : math.lerp(hsv.y, 0f, -g.modSaturation);
            s = math.saturate(s);

            float3 rgb = HsvToRgb(new float3(h, s, hsv.z));
            float a = c.w;

            if (math.abs(g.modBrightness) > 1e-6f)
            {
                rgb = g.modBrightness > 0f
                    ? math.lerp(rgb, new float3(1f), math.saturate(g.modBrightness))
                    : math.lerp(rgb, float3.zero, math.saturate(-g.modBrightness));
            }

            if (g.modAlpha > 0f) a = math.lerp(a, 1f, math.saturate(g.modAlpha));
            else if (g.modAlpha < 0f) a = math.lerp(a, 0f, math.saturate(-g.modAlpha));

            return new float4(rgb, math.saturate(a));
        }

        /// <summary>Ported from <c>UnityEngine.Color.RGBToHSV</c> so hue/saturation/value match Unity's own conversion.</summary>
        internal static float3 RgbToHsv(float3 c)
        {
            float h, s, v;
            if (c.z > c.y && c.z > c.x)
                RgbToHsvHelper(4f, c.z, c.x, c.y, out h, out s, out v);
            else if (c.y > c.x)
                RgbToHsvHelper(2f, c.y, c.z, c.x, out h, out s, out v);
            else
                RgbToHsvHelper(0f, c.x, c.y, c.z, out h, out s, out v);
            return new float3(h, s, v);
        }

        private static void RgbToHsvHelper(float offset, float dominant, float colorOne, float colorTwo, out float h, out float s, out float v)
        {
            v = dominant;
            if (v != 0f)
            {
                float small = math.min(colorOne, colorTwo);
                float diff = v - small;
                if (diff != 0f)
                {
                    s = diff / v;
                    colorOne = (v - colorOne) / diff;
                    colorTwo = (v - colorTwo) / diff;
                    h = offset + colorTwo - colorOne;
                }
                else
                {
                    s = 0f;
                    h = offset + colorTwo - colorOne;
                }
                h /= 6f;
                if (h < 0f)
                    h += 1f;
            }
            else
            {
                s = 0f;
                h = 0f;
            }
        }

        /// <summary>Ported from <c>UnityEngine.Color.HSVToRGB</c> (HDR variant: no output clamping).</summary>
        internal static float3 HsvToRgb(float3 hsv)
        {
            float h = hsv.x, s = hsv.y, v = hsv.z;
            if (s == 0f)
                return new float3(v, v, v);
            if (v == 0f)
                return float3.zero;

            float num = h * 6f;
            int num2 = (int)math.floor(num);
            float num3 = num - num2;
            float num4 = v * (1f - s);
            float num5 = v * (1f - s * num3);
            float num6 = v * (1f - s * (1f - num3));

            switch (num2 + 1)
            {
                case 0: return new float3(v, num4, num5);
                case 1: return new float3(v, num6, num4);
                case 2: return new float3(num5, v, num4);
                case 3: return new float3(num4, v, num6);
                case 4: return new float3(num4, num5, v);
                case 5: return new float3(num6, num4, v);
                case 6: return new float3(v, num4, num5);
                case 7: return new float3(v, num6, num4);
                default: return new float3(1f, 1f, 1f);
            }
        }

        /// <summary>Ported from <c>Mathf.GammaToLinearSpace</c>, matching <c>UnityEngine.Color.linear</c> per channel.</summary>
        internal static float GammaToLinear(float value)
        {
            if (value <= 0f) return 0f;
            if (value <= 0.04045f) return value / 12.92f;
            if (value < 1f) return math.pow((value + 0.055f) / 1.055f, 2.4f);
            return math.pow(value, 2.4f);
        }

        public static float3 SrgbToLinear(float3 c) => new float3(GammaToLinear(c.x), GammaToLinear(c.y), GammaToLinear(c.z));
    }
}
