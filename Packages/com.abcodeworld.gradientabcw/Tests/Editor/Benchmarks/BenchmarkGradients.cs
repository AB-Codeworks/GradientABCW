using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ABCodeworld.Gradients.Tests.Editor.Benchmarks
{
    /// <summary>How much of the modulation pipeline a benchmark or equivalence case exercises.</summary>
    internal enum ModulationCase
    {
        /// <summary>Identity modulation: evaluation takes the cheap base path.</summary>
        None,

        /// <summary>Brightness and alpha only. Neutral hue and saturation, so the HSV round-trip is avoidable.</summary>
        BrightnessAlphaOnly,

        /// <summary>Hue and saturation only, which genuinely require the HSV round-trip.</summary>
        HueSaturationOnly,

        /// <summary>Repeats, offset and reverse, with neutral colour adjustment.</summary>
        DomainOnly,

        /// <summary>Everything at once.</summary>
        Full,
    }

    /// <summary>
    /// The gradient matrix the benchmarks and equivalence tests share. Key counts are deliberately
    /// weighted toward the small end: a two-key ramp is by far the most common shape in real projects,
    /// and an optimization that only pays off at 32 keys is not worth much.
    /// </summary>
    internal static class BenchmarkGradients
    {
        public static readonly int[] KeyCounts = { 2, 8, 32 };

        public static readonly ModulationCase[] ModulationCases =
        {
            ModulationCase.None,
            ModulationCase.BrightnessAlphaOnly,
            ModulationCase.HueSaturationOnly,
            ModulationCase.DomainOnly,
            ModulationCase.Full,
        };

        public static GradientABCW Build(int keyCount, ModulationCase modulation, BlendMode blend = BlendMode.Smooth)
        {
            var g = GradientABCW.CreateDefault();

            var colorKeys = new ColorKey[keyCount];
            var alphaKeys = new AlphaKey[keyCount];
            for (int i = 0; i < keyCount; i++)
            {
                float t = keyCount > 1 ? (float)i / (keyCount - 1) : 0f;
                // A hue sweep with varying saturation and value, so no channel is trivially constant and
                // the HSV path has real work to do.
                colorKeys[i] = new ColorKey(Color.HSVToRGB(t, 0.35f + 0.6f * Mathf.Abs(0.5f - t), 0.2f + 0.75f * t), t);
                alphaKeys[i] = new AlphaKey(0.15f + 0.8f * Mathf.Abs(Mathf.Sin(t * 3.1f)), t);
            }

            g.SetKeys(colorKeys, alphaKeys);
            g.BlendMode = blend;
            g.Modulation = ModulationFor(modulation);
            return g;
        }

        public static GradientModulation ModulationFor(ModulationCase modulation)
        {
            var m = GradientModulation.Identity;
            switch (modulation)
            {
                case ModulationCase.None:
                    break;

                case ModulationCase.BrightnessAlphaOnly:
                    m.brightness = -0.35f;
                    m.alpha = 0.4f;
                    break;

                case ModulationCase.HueSaturationOnly:
                    m.hueShift = 0.27f;
                    m.saturation = -0.45f;
                    break;

                case ModulationCase.DomainOnly:
                    m.repeats = 3.7f;
                    m.repeatMode = RepeatMode.Mirror;
                    m.offset = 0.23f;
                    m.reverse = true;
                    break;

                case ModulationCase.Full:
                    m.repeats = 2.5f;
                    m.repeatMode = RepeatMode.Wrap;
                    m.offset = 0.15f;
                    m.reverse = true;
                    m.hueShift = 0.2f;
                    m.saturation = 0.3f;
                    m.brightness = -0.2f;
                    m.alpha = 0.1f;
                    break;
            }
            return m;
        }

        /// <summary>Every (key count, modulation, blend mode) combination, for equivalence coverage.</summary>
        public static IEnumerable<TestCaseData> AllCases()
        {
            foreach (int keys in KeyCounts)
            foreach (var modulation in ModulationCases)
            foreach (var blend in new[] { BlendMode.Smooth, BlendMode.Stepped })
            {
                yield return new TestCaseData(keys, modulation, blend)
                    .SetName($"Case(keys={keys},mod={modulation},blend={blend})");
            }
        }

        /// <summary>A representative subset, so a benchmark run stays quick enough to iterate on.</summary>
        public static IEnumerable<TestCaseData> BenchmarkCases()
        {
            foreach (int keys in new[] { 2, 32 })
            foreach (var modulation in new[] { ModulationCase.None, ModulationCase.BrightnessAlphaOnly, ModulationCase.Full })
            {
                yield return new TestCaseData(keys, modulation)
                    .SetName($"Bench(keys={keys},mod={modulation})");
            }
        }
    }
}
