using System;
using NUnit.Framework;
using Unity.Mathematics;

namespace ABCodeworld.Gradients.Tests.Editor.Benchmarks
{
    /// <summary>
    /// Holds the optimized evaluation core to the output of the frozen pre-optimization baseline, over a
    /// dense sweep of the whole parameter matrix. This is the gate on every Phase 2 change: an
    /// optimization that shifts output is not silently acceptable, it has to be surfaced as a deliberate
    /// visual decision, and this fixture is what measures how far it actually moved.
    /// </summary>
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientEquivalenceTests
    {
        private const int SweepSamples = 1024;

        /// <summary>
        /// Bit-exact is the intent. The allowance here exists only to absorb float reassociation the JIT
        /// is free to do differently between two textually distinct methods; anything larger means the
        /// maths genuinely changed and belongs in the visual-change register instead of being waved past.
        /// </summary>
        private const float Tolerance = 1e-6f;

        /// <summary>
        /// A uniform sweep plus every exact key time and key midpoint.
        /// </summary>
        /// <remarks>
        /// The boundary points are not padding. Stepped sampling decides on <c>t &lt; midpoint</c>, so a
        /// sample sitting exactly on a midpoint is the only place a difference in how that midpoint is
        /// rounded can change which key is selected — and a uniform sweep over 1024 points essentially
        /// never lands on one. A real regression hid behind exactly that gap.
        /// </remarks>
        private static float[] SamplePointsFor(GradientABCW g)
        {
            var points = new System.Collections.Generic.List<float>(SweepSamples + 256);

            for (int i = 0; i < SweepSamples; i++)
                points.Add((float)i / (SweepSamples - 1));

            void AddBoundaries(System.ReadOnlySpan<float> times)
            {
                for (int i = 0; i < times.Length; i++)
                {
                    points.Add(times[i]);
                    if (i + 1 < times.Length)
                    {
                        float mid = 0.5f * (times[i] + times[i + 1]);
                        points.Add(mid);
                        points.Add(NextRepresentable(mid, -1));
                        points.Add(NextRepresentable(mid, +1));
                    }
                }
            }

            var colorTimes = new float[g.ColorKeys.Length];
            for (int i = 0; i < colorTimes.Length; i++) colorTimes[i] = g.ColorKeys[i].time;
            var alphaTimes = new float[g.AlphaKeys.Length];
            for (int i = 0; i < alphaTimes.Length; i++) alphaTimes[i] = g.AlphaKeys[i].time;

            AddBoundaries(colorTimes);
            AddBoundaries(alphaTimes);

            return points.ToArray();
        }

        /// <summary>
        /// The adjacent representable float, one ulp away. Valid for the positive finite values used here,
        /// where the bit pattern increases monotonically with magnitude.
        /// </summary>
        private static float NextRepresentable(float value, int direction) =>
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(value) + direction);

        [TestCaseSource(typeof(BenchmarkGradients), nameof(BenchmarkGradients.AllCases))]
        public void Evaluate_MatchesPreOptimizationBaseline(int keyCount, ModulationCase modulation, BlendMode blend)
        {
            var g = BenchmarkGradients.Build(keyCount, modulation, blend);
            ref readonly var native = ref g.Native;

            float worst = 0f;
            float worstAt = 0f;

            foreach (float t in SamplePointsFor(g))
            {
                float4 expected = BaselineGradientMath.Evaluate(in native, t);
                float4 actual = GradientMath.Evaluate(in native, t);

                float delta = math.cmax(math.abs(expected - actual));
                if (delta > worst)
                {
                    worst = delta;
                    worstAt = t;
                }
            }

            Assert.That(worst, Is.LessThanOrEqualTo(Tolerance),
                $"worst per-channel delta {worst:E3} at t={worstAt:R} for keys={keyCount}, mod={modulation}, blend={blend}");
        }

        [TestCaseSource(typeof(BenchmarkGradients), nameof(BenchmarkGradients.AllCases))]
        public void EvaluateBase_MatchesPreOptimizationBaseline(int keyCount, ModulationCase modulation, BlendMode blend)
        {
            var g = BenchmarkGradients.Build(keyCount, modulation, blend);
            ref readonly var native = ref g.Native;

            float worst = 0f;
            foreach (float t in SamplePointsFor(g))
            {
                float4 expected = BaselineGradientMath.EvaluateBase(in native, t);
                float4 actual = GradientMath.EvaluateBase(in native, t);
                worst = math.max(worst, math.cmax(math.abs(expected - actual)));
            }

            Assert.That(worst, Is.LessThanOrEqualTo(Tolerance), $"worst per-channel delta {worst:E3}");
        }

        /// <summary>
        /// The golden fixture uses seven colour keys against only three alpha keys. The generated matrix
        /// above always gives both lanes the same count, which turned out to hide a real difference, so
        /// the mismatched shape is pinned explicitly here.
        /// </summary>
        [Test]
        public void Evaluate_MismatchedLaneCounts_MatchesPreOptimizationBaseline(
            [Values(BlendMode.Smooth, BlendMode.Stepped)] BlendMode blend,
            [Values(ModulationCase.None, ModulationCase.DomainOnly, ModulationCase.BrightnessAlphaOnly, ModulationCase.Full)] ModulationCase modulation)
        {
            var g = GradientABCW.CreateDefault();
            var colors = new[]
            {
                UnityEngine.Color.red, new UnityEngine.Color(1f, 0.5f, 0f), UnityEngine.Color.yellow,
                UnityEngine.Color.green, UnityEngine.Color.blue, new UnityEngine.Color(0.29f, 0f, 0.51f),
                new UnityEngine.Color(0.56f, 0f, 1f),
            };
            var colorKeys = new ColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                colorKeys[i] = new ColorKey(colors[i], (float)i / (colors.Length - 1));

            g.SetKeys(colorKeys, new[] { new AlphaKey(1f, 0f), new AlphaKey(0.5f, 0.5f), new AlphaKey(1f, 1f) });
            g.BlendMode = blend;
            g.Modulation = BenchmarkGradients.ModulationFor(modulation);

            ref readonly var native = ref g.Native;

            float worst = 0f;
            float worstAt = 0f;
            float4 worstExpected = default, worstActual = default;

            foreach (float t in SamplePointsFor(g))
            {
                float4 expected = BaselineGradientMath.Evaluate(in native, t);
                float4 actual = GradientMath.Evaluate(in native, t);
                float delta = math.cmax(math.abs(expected - actual));
                if (delta > worst)
                {
                    worst = delta;
                    worstAt = t;
                    worstExpected = expected;
                    worstActual = actual;
                }
            }

            Assert.That(worst, Is.LessThanOrEqualTo(Tolerance),
                $"worst delta {worst:E3} at t={worstAt:F5}: expected {worstExpected}, got {worstActual}");
        }

        [TestCaseSource(typeof(BenchmarkGradients), nameof(BenchmarkGradients.AllCases))]
        public void BakedLut_MatchesPreOptimizationBaseline(int keyCount, ModulationCase modulation, BlendMode blend)
        {
            var g = BenchmarkGradients.Build(keyCount, modulation, blend);
            ref readonly var native = ref g.Native;

            const int size = 512;
            var expected = new UnityEngine.Color32[size];
            var actual = new UnityEngine.Color32[size];

            using (var scratch = new Unity.Collections.NativeArray<UnityEngine.Color32>(size, Unity.Collections.Allocator.Temp))
            {
                BaselineGradientMath.Bake(in native, scratch, GradientLutOptions.Final);
                scratch.CopyTo(expected);
            }

            GradientLut.Bake(g, actual.AsSpan(), GradientLutOptions.Final);

            int worst = 0;
            int worstIndex = -1;
            for (int i = 0; i < size; i++)
            {
                int delta = math.max(
                    math.max(math.abs(expected[i].r - actual[i].r), math.abs(expected[i].g - actual[i].g)),
                    math.max(math.abs(expected[i].b - actual[i].b), math.abs(expected[i].a - actual[i].a)));
                if (delta > worst)
                {
                    worst = delta;
                    worstIndex = i;
                }
            }

            // Byte-exact: the bake writes 8-bit output, so any visible change shows up here as a
            // non-zero difference in quantized channel values.
            Assert.That(worst, Is.Zero, $"worst channel delta {worst}/255 at LUT index {worstIndex}");
        }
    }
}
