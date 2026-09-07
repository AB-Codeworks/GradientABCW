using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientLutTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(256)]
        [TestCase(1024)]
        public void ManagedBake_MatchesPerSampleEvaluate(int size)
        {
            var g = TestGradients.WithModulation();
            var lut = new Color32[size];
            GradientLut.Bake(g, lut, GradientLutOptions.Final);

            for (int i = 0; i < size; i++)
            {
                float t = size > 1 ? (float)i / (size - 1) : 0f;
                var expected = g.Evaluate(t);
                var actual = lut[i];

                Assert.That(actual.r, Is.EqualTo((byte)(Mathf.Clamp01(expected.r) * 255f + 0.5f)).Within(1));
                Assert.That(actual.g, Is.EqualTo((byte)(Mathf.Clamp01(expected.g) * 255f + 0.5f)).Within(1));
                Assert.That(actual.b, Is.EqualTo((byte)(Mathf.Clamp01(expected.b) * 255f + 0.5f)).Within(1));
                Assert.That(actual.a, Is.EqualTo((byte)(Mathf.Clamp01(expected.a) * 255f + 0.5f)).Within(1));
            }
        }

        [Test]
        public void ScheduleBake_MatchesManagedBake()
        {
            var g = TestGradients.WithModulation();
            var native = NativeGradient.From(g);
            const int size = 256;

            using var jobResult = new NativeArray<float4>(size, Allocator.TempJob);
            GradientJobs.ScheduleBake(in native, jobResult, GradientLutOptions.Final).Complete();

            using var managedResult = new NativeArray<float4>(size, Allocator.Temp);
            GradientLut.Bake(in native, managedResult, GradientLutOptions.Final);

            for (int i = 0; i < size; i++)
            {
                Assert.That(jobResult[i].x, Is.EqualTo(managedResult[i].x).Within(1e-6f));
                Assert.That(jobResult[i].y, Is.EqualTo(managedResult[i].y).Within(1e-6f));
                Assert.That(jobResult[i].z, Is.EqualTo(managedResult[i].z).Within(1e-6f));
                Assert.That(jobResult[i].w, Is.EqualTo(managedResult[i].w).Within(1e-6f));
            }
        }

        [Test]
        public void ScheduleEvaluate_MatchesPerSampleEvaluate()
        {
            var g = TestGradients.WithModulation();
            var native = NativeGradient.From(g);

            var times = new[] { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 1f };
            using var nativeTimes = new NativeArray<float>(times, Allocator.TempJob);
            using var results = new NativeArray<float4>(times.Length, Allocator.TempJob);

            GradientJobs.ScheduleEvaluate(in native, nativeTimes, results, includeModulation: true).Complete();

            for (int i = 0; i < times.Length; i++)
            {
                var expected = g.Evaluate(times[i]);
                Assert.That(results[i].x, Is.EqualTo(expected.r).Within(1e-5f));
                Assert.That(results[i].y, Is.EqualTo(expected.g).Within(1e-5f));
                Assert.That(results[i].z, Is.EqualTo(expected.b).Within(1e-5f));
                Assert.That(results[i].w, Is.EqualTo(expected.a).Within(1e-5f));
            }
        }

        [Test]
        public void Bake_BaseVsFinal_DifferWhenModulationEffective()
        {
            var g = TestGradients.WithModulation();
            var baseLut = new Color32[64];
            var finalLut = new Color32[64];
            GradientLut.Bake(g, baseLut, GradientLutOptions.Base);
            GradientLut.Bake(g, finalLut, GradientLutOptions.Final);

            bool anyDifferent = false;
            for (int i = 0; i < baseLut.Length; i++)
            {
                if (!baseLut[i].Equals(finalLut[i])) { anyDifferent = true; break; }
            }
            Assert.That(anyDifferent, Is.True);
        }

        [Test]
        public void Bake_ToLinear_MatchesColorLinearConversion()
        {
            var g = TestGradients.Rainbow7();
            var srgbLut = new Color32[16];
            var linearLut = new Color32[16];
            GradientLut.Bake(g, srgbLut, new GradientLutOptions { includeModulation = false, toLinear = false });
            GradientLut.Bake(g, linearLut, new GradientLutOptions { includeModulation = false, toLinear = true });

            for (int i = 0; i < srgbLut.Length; i++)
            {
                var srgb = (Color)srgbLut[i];
                var expectedLinear = srgb.linear;
                var actualLinear = (Color)linearLut[i];

                Assert.That(actualLinear.r, Is.EqualTo(expectedLinear.r).Within(0.01f));
                Assert.That(actualLinear.g, Is.EqualTo(expectedLinear.g).Within(0.01f));
                Assert.That(actualLinear.b, Is.EqualTo(expectedLinear.b).Within(0.01f));
            }
        }
    }
}
