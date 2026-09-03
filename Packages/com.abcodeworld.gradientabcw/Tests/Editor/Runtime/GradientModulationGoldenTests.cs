using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LegacyGradient = ABCodeworld.Gradients.Tests.Legacy.LegacyGradient;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    /// <summary>
    /// Compares the new evaluation core against the frozen legacy oracle across the modulation
    /// parameter space, at a fixed set of sample points per combination.
    /// </summary>
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientModulationGoldenTests
    {
        private const int SamplePoints = 17; // 0, 1/16, ..., 1
        private const float DomainTolerance = 1e-5f;
        private const float HsbaTolerance = 2e-3f;

        private static readonly Color[] ColorRgb =
        {
            Color.red, new Color(1f, 0.5f, 0f), Color.yellow, Color.green,
            Color.blue, new Color(0.29f, 0f, 0.51f), new Color(0.56f, 0f, 1f),
        };
        private static readonly float[] AlphaValues = { 1f, 0.5f, 1f };
        private static readonly float[] AlphaTimes = { 0f, 0.5f, 1f };

        public static IEnumerable<TestCaseData> DomainCombos()
        {
            float[] repeatsValues = { 0.5f, 1f, 2f, 3.7f };
            RepeatMode[] modes = { RepeatMode.Clamp, RepeatMode.Wrap, RepeatMode.Mirror };
            float[] offsets = { 0f, 0.25f, 0.999f };
            bool[] reverses = { false, true };
            BlendMode[] blends = { BlendMode.Smooth, BlendMode.Stepped };

            foreach (var repeats in repeatsValues)
            foreach (var mode in modes)
            foreach (var offset in offsets)
            foreach (var reverse in reverses)
            foreach (var blend in blends)
            {
                yield return new TestCaseData(repeats, mode, offset, reverse, blend)
                    .SetName($"Domain(repeats={repeats},mode={mode},offset={offset},reverse={reverse},blend={blend})");
            }
        }

        [TestCaseSource(nameof(DomainCombos))]
        public void MatchesLegacy_AcrossDomainModulation(float repeats, RepeatMode mode, float offset, bool reverse, BlendMode blend)
        {
            var modulation = new GradientModulation
            {
                repeats = repeats,
                repeatMode = mode,
                offset = offset,
                reverse = reverse,
            };

            var g = BuildNew(blend, modulation);
            var legacy = BuildLegacy(blend == BlendMode.Stepped, repeats, ToLegacyMode(mode), offset, reverse, GradientModulation.Identity);

            AssertMatchesLegacy(g, legacy, DomainTolerance);
        }

        public static IEnumerable<TestCaseData> HsbaCombos()
        {
            (float hue, float sat, float bri, float alpha)[] cases =
            {
                (0.3f, 1f, 0.5f, 0.7f),
                (-0.3f, -1f, -0.5f, -0.7f),
                (0.3f, -1f, 0.5f, -0.7f),
                (-0.3f, 1f, -0.5f, 0.7f),
            };
            foreach (var c in cases)
                yield return new TestCaseData(c.hue, c.sat, c.bri, c.alpha)
                    .SetName($"Hsba(hue={c.hue},sat={c.sat},bri={c.bri},alpha={c.alpha})");
        }

        [TestCaseSource(nameof(HsbaCombos))]
        public void MatchesLegacy_AcrossHsbaModulation(float hue, float sat, float bri, float alpha)
        {
            var modulation = new GradientModulation
            {
                repeats = 1f,
                repeatMode = RepeatMode.Clamp,
                offset = 0f,
                hueShift = hue,
                saturation = sat,
                brightness = bri,
                alpha = alpha,
            };

            var g = BuildNew(BlendMode.Smooth, modulation);
            var legacy = BuildLegacy(false, 1f, LegacyGradient.RepeatMode.Clamp, 0f, false, modulation);

            AssertMatchesLegacy(g, legacy, HsbaTolerance);
        }

        private static void AssertMatchesLegacy(GradientABCW g, LegacyGradient legacy, float tolerance)
        {
            for (int i = 0; i < SamplePoints; i++)
            {
                float t = (float)i / (SamplePoints - 1);
                Color expected = legacy.Evaluate(t);
                Color actual = g.Evaluate(t);

                Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance), $"r at t={t}");
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance), $"g at t={t}");
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance), $"b at t={t}");
                Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance), $"a at t={t}");
            }
        }

        private static GradientABCW BuildNew(BlendMode blend, GradientModulation modulation)
        {
            var g = GradientABCW.CreateDefault();
            var colorKeys = new ColorKey[ColorRgb.Length];
            for (int i = 0; i < ColorRgb.Length; i++)
                colorKeys[i] = new ColorKey(ColorRgb[i], (float)i / (ColorRgb.Length - 1));

            var alphaKeys = new AlphaKey[AlphaValues.Length];
            for (int i = 0; i < AlphaValues.Length; i++)
                alphaKeys[i] = new AlphaKey(AlphaValues[i], AlphaTimes[i]);

            g.SetKeys(colorKeys, alphaKeys);
            g.BlendMode = blend;
            g.Modulation = modulation;
            return g;
        }

        private static LegacyGradient BuildLegacy(bool fixedBlend, float repeats, LegacyGradient.RepeatMode mode, float offset, bool reverse, GradientModulation hsba)
        {
            var g = LegacyGradient.CreateDefault();
            var colorKeys = new LegacyGradient.ColorKey[ColorRgb.Length];
            for (int i = 0; i < ColorRgb.Length; i++)
                colorKeys[i] = new LegacyGradient.ColorKey(ColorRgb[i], (float)i / (ColorRgb.Length - 1));

            var alphaKeys = new LegacyGradient.AlphaKey[AlphaValues.Length];
            for (int i = 0; i < AlphaValues.Length; i++)
                alphaKeys[i] = new LegacyGradient.AlphaKey(AlphaValues[i], AlphaTimes[i]);

            g.colorKeys = colorKeys;
            g.alphaKeys = alphaKeys;
            g.fixedBlendMode = fixedBlend;
            g.repeats = repeats;
            g.repeatMode = mode;
            g.evalOffset = offset;
            g.reverse = reverse;
            g.hueShift = hsba.hueShift;
            g.saturationAdj = hsba.saturation;
            g.brightnessAdj = hsba.brightness;
            g.alphaAdj = hsba.alpha;
            return g.Validate();
        }

        private static LegacyGradient.RepeatMode ToLegacyMode(RepeatMode mode) => mode switch
        {
            RepeatMode.Clamp => LegacyGradient.RepeatMode.Clamp,
            RepeatMode.Wrap => LegacyGradient.RepeatMode.Wrap,
            RepeatMode.Mirror => LegacyGradient.RepeatMode.Mirror,
            _ => LegacyGradient.RepeatMode.Clamp,
        };
    }
}
