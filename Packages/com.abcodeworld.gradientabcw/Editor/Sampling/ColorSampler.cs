using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// Builds a gradient by sampling colours from a <see cref="IColorSource"/> (a mesh's vertex colours or a
    /// texture's pixels). Deterministic given the same seed: replaces <c>UnityEngine.Random</c> with a seeded
    /// <see cref="Unity.Mathematics.Random"/>, and unifies what were two ~150-line duplicated implementations
    /// (mesh sampling and texture-random sampling) into one.
    /// </summary>
    internal static class ColorSampler
    {
        public static bool TrySampleRandom(IColorSource source, int keyCount, uint seed, out GradientABCW result, out string error)
        {
            result = GradientABCW.CreateDefault();
            error = null;

            if (source.Count == 0)
            {
                error = "Source has no colors to sample.";
                return false;
            }

            var rng = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
            var selected = SelectCandidates(source, rng);

            if (selected.Count < 2)
            {
                if (source.Count < 2)
                {
                    error = "Not enough color variety in the source.";
                    return false;
                }
                selected.Clear();
                selected.Add(source[0]);
                selected.Add(source[source.Count - 1]);
            }

            SortCandidates(selected, rng);

            BuildKeys(selected, keyCount, out var colorKeys, out var alphaKeys);
            result.SetKeys(colorKeys, alphaKeys);
            return true;
        }

        /// <summary>Strict: use a 1xN vertical LUT (first column, bottom-to-top), up to 16 entries.</summary>
        public static bool TrySampleStrict(Texture2D texture, out GradientABCW result, out string error)
        {
            result = GradientABCW.CreateDefault();
            var source = new TextureColorSource(texture);
            if (source.Error != null)
            {
                error = source.Error;
                return false;
            }

            int height = texture.height;
            int count = Mathf.Min(16, height);
            if (count <= 0)
            {
                error = "Texture has no pixels.";
                return false;
            }

            var strip = texture.GetPixels(0, 0, 1, height);
            if (strip == null || strip.Length == 0)
            {
                error = "Unable to read pixels.";
                return false;
            }

            if (count == 1)
            {
                var c = strip[0];
                result.SetKeys(
                    new[] { new ColorKey(new Color(c.r, c.g, c.b, 1f), 0f), new ColorKey(new Color(c.r, c.g, c.b, 1f), 1f) },
                    new[] { new AlphaKey(c.a, 0f), new AlphaKey(c.a, 1f) });
                error = null;
                return true;
            }

            var colorKeys = new ColorKey[count];
            var alphaKeys = new AlphaKey[count];
            for (int i = 0; i < count; i++)
            {
                var c = strip[i];
                float t = (float)i / (count - 1);
                colorKeys[i] = new ColorKey(new Color(c.r, c.g, c.b, 1f), t);
                alphaKeys[i] = new AlphaKey(c.a, t);
            }
            result.SetKeys(colorKeys, alphaKeys);
            error = null;
            return true;
        }

        private static List<Color32> SelectCandidates(IColorSource source, Unity.Mathematics.Random rng)
        {
            // The strategy is part of the seeded sequence: rng is drawn from before anything else so that
            // the same seed keeps choosing the same strategy, which is what ColorSamplerTests pins.
            int strategy = rng.NextInt(0, 4);
            int targetCount = rng.NextInt(4, 9);

            var selected = new List<Color32>();
            var seenBuckets = new HashSet<int>();

            switch (strategy)
            {
                case 0: SelectRandomDistinct(source, ref rng, targetCount, selected, seenBuckets); break;
                case 1: SelectLuminanceExtremes(source, ref rng, targetCount, selected, seenBuckets); break;
                case 2: SelectByHueSpread(source, ref rng, targetCount, selected, seenBuckets); break;
                default: SelectAroundASeedColour(source, ref rng, targetCount, selected, seenBuckets); break;
            }

            return selected;
        }

        /// <summary>Random picks, rejecting colours that quantize into an already-used bucket.</summary>
        private static void SelectRandomDistinct(IColorSource source, ref Unity.Mathematics.Random rng, int targetCount, List<Color32> selected, HashSet<int> seenBuckets)
        {
            for (int i = 0; i < 30 && selected.Count < targetCount; i++)
            {
                var c = source[rng.NextInt(0, source.Count)];
                if (seenBuckets.Add(QuantizeHash(c, 15f)))
                    selected.Add(c);
            }
        }

        /// <summary>Anchors on the darkest and brightest colours, then fills in randomly.</summary>
        private static void SelectLuminanceExtremes(IColorSource source, ref Unity.Mathematics.Random rng, int targetCount, List<Color32> selected, HashSet<int> seenBuckets)
        {
            var darkest = new Color32(255, 255, 255, 255);
            var brightest = new Color32(0, 0, 0, 255);
            float minLum = 1f, maxLum = 0f;

            for (int i = 0; i < source.Count; i++)
            {
                var c = source[i];
                float lum = Luminance(c);
                if (lum < minLum) { minLum = lum; darkest = c; }
                if (lum > maxLum) { maxLum = lum; brightest = c; }
            }

            selected.Add(darkest);
            selected.Add(brightest);
            SelectRandomDistinct(source, ref rng, targetCount, selected, seenBuckets);
        }

        /// <summary>Strides the source taking one colour per hue bucket, skipping dark and washed-out ones.</summary>
        private static void SelectByHueSpread(IColorSource source, ref Unity.Mathematics.Random rng, int targetCount, List<Color32> selected, HashSet<int> seenBuckets)
        {
            int stride = Mathf.Max(1, source.Count / 30);
            for (int i = 0; i < source.Count && selected.Count < targetCount; i += stride)
            {
                var c = source[i];
                Color.RGBToHSV(c, out float h, out float s, out float v);
                if (v < 0.1f || s < 0.1f)
                    continue;
                if (seenBuckets.Add(Mathf.RoundToInt(h * 20f)))
                    selected.Add(c);
            }

            if (selected.Count < 3)
            {
                for (int i = 0; i < 20 && selected.Count < targetCount; i++)
                    selected.Add(source[rng.NextInt(0, source.Count)]);
            }
        }

        /// <summary>Picks a seed colour, then alternates between colours close to it and far from it.</summary>
        private static void SelectAroundASeedColour(IColorSource source, ref Unity.Mathematics.Random rng, int targetCount, List<Color32> selected, HashSet<int> seenBuckets)
        {
            var seedColor = source[rng.NextInt(0, source.Count)];
            selected.Add(seedColor);

            int stride = Mathf.Max(1, source.Count / 40);
            for (int i = 0; i < source.Count && selected.Count < targetCount; i += stride)
            {
                var c = source[i];
                float similarity = ColorSimilarity(seedColor, c);
                bool takeNear = i % 2 == 0 && similarity < 0.3f;
                bool takeFar = i % 2 == 1 && similarity > 0.5f;
                if ((takeNear || takeFar) && seenBuckets.Add(QuantizeHash(c, 20f)))
                    selected.Add(c);
            }
        }

        private static void SortCandidates(List<Color32> selected, Unity.Mathematics.Random rng)
        {
            switch (rng.NextInt(0, 3))
            {
                case 0:
                    selected.Sort((a, b) => Luminance(a).CompareTo(Luminance(b)));
                    break;
                case 1:
                    selected.Sort((a, b) =>
                    {
                        Color.RGBToHSV(a, out float ha, out _, out _);
                        Color.RGBToHSV(b, out float hb, out _, out _);
                        return ha.CompareTo(hb);
                    });
                    break;
                default:
                    if (selected.Count > 3)
                    {
                        var mid = selected.GetRange(1, selected.Count - 2);
                        for (int i = mid.Count - 1; i > 0; i--)
                        {
                            int j = rng.NextInt(0, i + 1);
                            (mid[i], mid[j]) = (mid[j], mid[i]);
                        }
                        var first = selected[0];
                        var last = selected[selected.Count - 1];
                        selected.Clear();
                        selected.Add(first);
                        selected.AddRange(mid);
                        selected.Add(last);
                    }
                    break;
            }
        }

        private static void BuildKeys(List<Color32> selected, int keyCount, out ColorKey[] colorKeys, out AlphaKey[] alphaKeys)
        {
            int n = Mathf.Clamp(keyCount, GradientABCW.MinKeys, GradientABCW.MaxKeys);
            colorKeys = new ColorKey[n];
            alphaKeys = new AlphaKey[n];

            int segments = Mathf.Max(1, selected.Count - 1);
            for (int i = 0; i < n; i++)
            {
                float u = n > 1 ? (float)i / (n - 1) : 0f;
                float f = u * segments;
                int segment = Mathf.Clamp(Mathf.FloorToInt(f), 0, segments - 1);
                float v = Mathf.Clamp01(f - segment);

                Color32 c0 = selected[segment];
                Color32 c1 = selected[Mathf.Min(segment + 1, selected.Count - 1)];

                var rgb = Color.Lerp(c0, c1, v);
                float a = Mathf.Lerp(c0.a / 255f, c1.a / 255f, v);

                colorKeys[i] = new ColorKey(new Color(rgb.r, rgb.g, rgb.b, 1f), u);
                alphaKeys[i] = new AlphaKey(a, u);
            }
        }

        private static int QuantizeHash(Color32 c, float divisor)
        {
            int qr = (int)(c.r / divisor), qg = (int)(c.g / divisor), qb = (int)(c.b / divisor);
            return (qr << 16) | (qg << 8) | qb;
        }

        private static float Luminance(Color32 c) =>
            0.299f * (c.r / 255f) + 0.587f * (c.g / 255f) + 0.114f * (c.b / 255f);

        private static float ColorSimilarity(Color32 a, Color32 b)
        {
            float rDiff = Mathf.Abs(a.r - b.r) / 255f;
            float gDiff = Mathf.Abs(a.g - b.g) / 255f;
            float bDiff = Mathf.Abs(a.b - b.b) / 255f;
            return (rDiff + gDiff + bDiff) / 3f;
        }
    }
}
