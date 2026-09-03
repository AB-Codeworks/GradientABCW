using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>
    /// Managed placeholder for gradient evaluation math, ported line-for-line from the legacy
    /// implementation. Superseded by the Burst-compiled <c>GradientMath</c> in the evaluation core;
    /// kept here only until that lands so <see cref="GradientABCW"/> has a correct <c>Evaluate</c> from day one.
    /// </summary>
    internal static class GradientMathTemp
    {
        public static float TransformT(in GradientModulation m, float t)
        {
            if (t >= 1f) t = 1f - 1e-7f;

            float td = t + m.offset;

            float r = m.repeats <= 0f ? 1f : m.repeats;
            bool repeating = r != 1f;
            bool periodic = m.repeatMode == RepeatMode.Wrap || m.repeatMode == RepeatMode.Mirror;

            if (!repeating && !periodic && m.repeatMode == RepeatMode.Clamp)
                td = Mathf.Clamp01(td);
            else
                td -= Mathf.Floor(td);

            if (repeating || periodic)
            {
                float scaled = td * r;
                switch (m.repeatMode)
                {
                    case RepeatMode.Clamp:
                        td = Mathf.Min(scaled, 1f);
                        break;
                    case RepeatMode.Wrap:
                        td = scaled - Mathf.Floor(scaled);
                        break;
                    case RepeatMode.Mirror:
                    {
                        int cycle = (int)Mathf.Floor(scaled);
                        float f = scaled - cycle;
                        bool odd = (cycle & 1) == 1;
                        td = odd ? (1f - f) : f;
                        break;
                    }
                }
            }

            if (m.reverse)
                td = 1f - td;

            return Mathf.Clamp01(td);
        }

        public static Color SampleSmooth(ColorKey[] colorKeys, AlphaKey[] alphaKeys, float t)
        {
            var c = EvaluateColorSmooth(colorKeys, t);
            c.a = EvaluateAlphaSmooth(alphaKeys, t);
            return c;
        }

        public static Color SampleStepped(ColorKey[] colorKeys, AlphaKey[] alphaKeys, float t)
        {
            var c = SampleSteppedColor(colorKeys, t);
            c.a = SampleSteppedAlpha(alphaKeys, t);
            return c;
        }

        private static Color EvaluateColorSmooth(ColorKey[] arr, float t)
        {
            if (arr.Length == 1) return arr[0].color;
            if (t <= arr[0].time) return arr[0].color;
            if (t >= arr[arr.Length - 1].time) return arr[arr.Length - 1].color;

            int lo = 0, hi = arr.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (t >= arr[mid].time) lo = mid;
                else hi = mid;
            }
            float span = arr[hi].time - arr[lo].time;
            float u = span > 1e-9f ? (t - arr[lo].time) / span : 0f;
            return Color.LerpUnclamped(arr[lo].color, arr[hi].color, u);
        }

        private static float EvaluateAlphaSmooth(AlphaKey[] arr, float t)
        {
            if (arr.Length == 1) return arr[0].alpha;
            if (t <= arr[0].time) return arr[0].alpha;
            if (t >= arr[arr.Length - 1].time) return arr[arr.Length - 1].alpha;

            int lo = 0, hi = arr.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (t >= arr[mid].time) lo = mid;
                else hi = mid;
            }
            float span = arr[hi].time - arr[lo].time;
            float u = span > 1e-9f ? (t - arr[lo].time) / span : 0f;
            return Mathf.LerpUnclamped(arr[lo].alpha, arr[hi].alpha, u);
        }

        private static Color SampleSteppedColor(ColorKey[] arr, float t)
        {
            if (arr.Length == 1) return arr[0].color;

            int last = arr.Length - 1;
            for (int i = 0; i < last; i++)
            {
                float mid = 0.5f * (arr[i].time + arr[i + 1].time);
                if (t < mid) return arr[i].color;
            }
            return arr[last].color;
        }

        private static float SampleSteppedAlpha(AlphaKey[] arr, float t)
        {
            if (arr.Length == 1) return arr[0].alpha;

            int last = arr.Length - 1;
            for (int i = 0; i < last; i++)
            {
                float mid = 0.5f * (arr[i].time + arr[i + 1].time);
                if (t < mid) return arr[i].alpha;
            }
            return arr[last].alpha;
        }

        public static Color ApplyHsba(in GradientModulation m, Color c)
        {
            Color.RGBToHSV(new Color(c.r, c.g, c.b, 1f), out float h, out float s, out float v);

            h += m.hueShift;
            h -= Mathf.Floor(h);

            if (m.saturation >= 0f) s = Mathf.Lerp(s, 1f, m.saturation);
            else s = Mathf.Lerp(s, 0f, -m.saturation);
            s = Mathf.Clamp01(s);

            var rgb = Color.HSVToRGB(h, s, v, true);
            float a = c.a;

            if (Mathf.Abs(m.brightness) > 1e-6f)
            {
                rgb = m.brightness > 0f
                    ? Color.Lerp(rgb, Color.white, Mathf.Clamp01(m.brightness))
                    : Color.Lerp(rgb, Color.black, Mathf.Clamp01(-m.brightness));
            }

            if (m.alpha > 0f) a = Mathf.Lerp(a, 1f, Mathf.Clamp01(m.alpha));
            else if (m.alpha < 0f) a = Mathf.Lerp(a, 0f, Mathf.Clamp01(-m.alpha));

            rgb.a = Mathf.Clamp01(a);
            return rgb;
        }
    }
}
