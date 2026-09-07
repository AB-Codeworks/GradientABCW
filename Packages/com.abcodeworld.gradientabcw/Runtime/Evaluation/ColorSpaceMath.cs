using Unity.Mathematics;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// Colour-space conversions used by gradient evaluation, ported from Unity's own implementations so
    /// that hue, saturation and value match what the rest of the editor shows.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="GradientMath"/>, which is about gradients, not about colour spaces. These
    /// are deliberately verbatim ports: a "cleaner" reformulation would drift from Unity's results, and
    /// the golden tests compare against an oracle that uses Unity's versions.
    /// </remarks>
    public static class ColorSpaceMath
    {
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
