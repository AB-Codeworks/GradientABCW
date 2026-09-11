using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    /// <summary>
    /// A grey has no hue, so modulation must not invent one for it.
    /// </summary>
    /// <remarks>
    /// <c>RgbToHsv</c> reports a hue for a grey anyway — its three-way dominant-channel branch resolves on
    /// whichever of r, g and b is a last-ulp larger, so the answer is whichever of the six sectors the
    /// rounding fell into. Left alone that is harmless, because <c>HsvToRgb</c> rebuilds the same grey from
    /// any hue. A positive saturation is what makes it matter: it promotes the arbitrary hue to a fully
    /// saturated colour, and the result then depends on nothing but rounding.
    /// <para>
    /// That is not a hypothetical. Nine cells of a modulated 16-cubed bake, all on or beside the cube's
    /// grey diagonal, came out 43/255 apart between the Burst job and the managed loop — red one way and
    /// cyan the other, each path having landed in a different sector. Neither answer was more correct.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientAchromaticModulationTests
    {
        [TestCase(0f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(0.6891373f)] // The exact grey the 3D bake produced on its diagonal.
        [TestCase(1f)]
        public void SaturatingAGreyLeavesItGrey(float level)
        {
            var grey = new float4(level, level, level, 1f);

            float4 boosted = GradientDomain.ApplyHsba(grey, needsHsv: 1, hueShift: 0f, saturation: 0.3f,
                brightness: 0f, alpha: 0f);

            Assert.That(boosted.x, Is.EqualTo(level).Within(1e-6f), "r");
            Assert.That(boosted.y, Is.EqualTo(level).Within(1e-6f), "g");
            Assert.That(boosted.z, Is.EqualTo(level).Within(1e-6f), "b");
        }

        /// <summary>
        /// The same, for a grey that is only grey to within evaluation noise — which is the form the bug
        /// actually took, since no two channels of an interpolated colour are ever exactly equal.
        /// </summary>
        [Test]
        public void SaturatingANearlyGreyLeavesItGrey()
        {
            // 1.2e-7 apart, the spread measured between the Burst and managed evaluations on the diagonal,
            // and far below the 1/255 an 8-bit channel can represent.
            var almost = new float4(0.689137161f, 0.6891373f, 0.6891373f, 1f);

            float4 boosted = GradientDomain.ApplyHsba(almost, needsHsv: 1, hueShift: 0f, saturation: 0.3f,
                brightness: 0f, alpha: 0f);

            Assert.That(math.cmax(boosted.xyz) - math.cmin(boosted.xyz), Is.LessThan(1e-6f),
                "a colour indistinguishable from grey must not be given a hue");
        }

        /// <summary>A colour that does have a hue must still be saturated, or the guard has gone too far.</summary>
        [Test]
        public void SaturatingAColouredSampleStillWidensIt()
        {
            var red = new float4(0.6f, 0.3f, 0.3f, 1f);

            float4 boosted = GradientDomain.ApplyHsba(red, needsHsv: 1, hueShift: 0f, saturation: 0.3f,
                brightness: 0f, alpha: 0f);

            float before = 0.6f - 0.3f;
            Assert.That(math.cmax(boosted.xyz) - math.cmin(boosted.xyz), Is.GreaterThan(before),
                "saturation should still widen the gap between the channels of a coloured sample");
            Assert.That(boosted.x, Is.GreaterThan(boosted.y), "red must stay the dominant channel");
        }

        /// <summary>
        /// The 1D counterpart of the 3D Burst-vs-managed guard, which the package did not have. The two
        /// share <see cref="GradientDomain"/>, so a divergence in modulation would show up in both.
        /// </summary>
        [Test]
        public void TheModulated1DBakeAgreesBetweenBurstAndManaged()
        {
            var g = TestGradients.WithModulation();
            const int count = 256;

            using var burst = new NativeArray<Color32>(count, Allocator.TempJob);
            GradientLut.Bake(in g.Native, burst, GradientLutOptions.Final);

            using var managed = new NativeArray<Color32>(count, Allocator.TempJob);
            GradientLut.BakeManaged(in g.Native, managed, GradientLutOptions.Final);

            // Within a byte, not byte-for-byte. Burst and Mono round a float that lands within half an
            // LSB of a byte boundary differently, and entry 7 of this gradient does exactly that in its
            // alpha lane — 196 against 197. That is the noise floor of comparing two compilers' float
            // output at 8 bits, and it is three orders of magnitude away from the 43/255 hue flip this
            // guard exists to catch. The 3D guard next door can afford byte-for-byte; this one cannot.
            for (int i = 0; i < count; i++)
            {
                Assert.That(burst[i].r, Is.EqualTo(managed[i].r).Within(1), $"entry {i} r");
                Assert.That(burst[i].g, Is.EqualTo(managed[i].g).Within(1), $"entry {i} g");
                Assert.That(burst[i].b, Is.EqualTo(managed[i].b).Within(1), $"entry {i} b");
                Assert.That(burst[i].a, Is.EqualTo(managed[i].a).Within(1), $"entry {i} a");
            }
        }
    }
}
