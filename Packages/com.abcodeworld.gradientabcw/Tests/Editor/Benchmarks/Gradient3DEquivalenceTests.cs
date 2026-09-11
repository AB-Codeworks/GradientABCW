using NUnit.Framework;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Benchmarks
{
    /// <summary>
    /// Pins the 3D core against the 1D one it shares its modulation with.
    /// </summary>
    /// <remarks>
    /// A 3D gradient whose keys all sit on one axis at the same y and z reduces to a 1D gradient under
    /// stepped blending: every key is the same distance from the sample in y and z, so those two axes
    /// contribute an identical constant to every squared distance and drop out of the nearest-key search
    /// entirely — leaving it picking exactly the key the 1D midpoint partition picks. That holds with
    /// modulation applied too, because the per-axis transform moves y and z by the same amount for the
    /// sample as for nothing at all: the keys do not move, and the constant simply changes value.
    /// <para>
    /// So this is the strongest available check that the per-axis reuse of the shared domain transform,
    /// and the colour adjustment after it, are wired up correctly — it compares against a core covered by
    /// its own golden tests rather than against a hand-written expectation.
    /// </para>
    /// <para>
    /// Smooth blending is deliberately not compared. Inverse-distance weighting is not piecewise-linear
    /// interpolation and is not meant to be; only the endpoints agree.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Category("Runtime")]
    internal sealed class Gradient3DEquivalenceTests
    {
        /// <summary>
        /// A sample landing exactly on a midpoint between two keys is the one place the two cores are
        /// allowed to disagree: the 1D partition takes the key above the midpoint, while the 3D
        /// nearest-key search sees an exact tie and takes the lower index. Samples that close to a
        /// midpoint are skipped rather than asserted.
        /// </summary>
        private const float MidpointGuard = 1e-4f;

        private static readonly RepeatMode[] Modes = { RepeatMode.Clamp, RepeatMode.Wrap, RepeatMode.Mirror };

        [Test]
        public void SteppedMatchesTheOneDimensionalGradientAlongItsAxis(
            [Values(false, true)] bool reverse,
            [Values(1f, 2f, 3.5f)] float repeats,
            [Values(0f, 0.25f)] float offset,
            [Values(0, 1, 2)] int modeIndex)
        {
            var flat = TestGradients.Rainbow7();
            flat.BlendMode = BlendMode.Stepped;
            flat.Modulation = new GradientModulation
            {
                bypass = false,
                reverse = reverse,
                repeats = repeats,
                repeatMode = Modes[modeIndex],
                offset = offset,
                hueShift = 0.2f,
                saturation = 0.3f,
                brightness = -0.2f,
                alpha = 0.1f,
            };

            var cube = Test3DGradients.AxisAlignedFrom(flat);

            for (int i = 0; i < 17; i++)
            {
                // Deliberately off the lattice the keys sit on, so a sample never lands exactly on an
                // untransformed key midpoint.
                float t = (i + 0.5f) / 17f;

                float sampled = GradientMath.TransformT(in flat.Native, t);
                if (IsNearAMidpoint(flat, sampled))
                    continue;

                var expected = flat.Evaluate(t);
                var actual = cube.Evaluate(new Vector3(t, 0.5f, 0.5f));

                Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-5f), $"r at t={t}");
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-5f), $"g at t={t}");
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-5f), $"b at t={t}");
                Assert.That(actual.a, Is.EqualTo(expected.a).Within(1e-5f), $"a at t={t}");
            }
        }

        [Test]
        public void SmoothAgreesWithTheOneDimensionalGradientAtItsEndpoints()
        {
            var flat = TestGradients.Rainbow7();
            var cube = Test3DGradients.AxisAlignedFrom(flat);

            foreach (float t in new[] { 0f, 1f })
            {
                var expected = flat.EvaluateBase(t);
                var actual = cube.EvaluateBase(new Vector3(t, 0.5f, 0.5f));

                Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-4f), $"r at t={t}");
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-4f), $"g at t={t}");
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-4f), $"b at t={t}");
            }
        }

        private static bool IsNearAMidpoint(GradientABCW flat, float sampled)
        {
            if (IsNearAMidpoint(flat.ColorKeys, sampled))
                return true;

            var alphas = flat.AlphaKeys;
            for (int i = 1; i < alphas.Length; i++)
            {
                if (Mathf.Abs(sampled - 0.5f * (alphas[i - 1].time + alphas[i].time)) < MidpointGuard)
                    return true;
            }
            return false;
        }

        private static bool IsNearAMidpoint(System.ReadOnlySpan<ColorKey> keys, float sampled)
        {
            for (int i = 1; i < keys.Length; i++)
            {
                if (Mathf.Abs(sampled - 0.5f * (keys[i - 1].time + keys[i].time)) < MidpointGuard)
                    return true;
            }
            return false;
        }
    }
}
