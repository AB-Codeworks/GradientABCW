using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using ABCodeworld.Gradients;

namespace ABCodeworld.Gradients.Tests.Runtime
{
    [TestFixture]
    internal sealed class GradientRuntimeSmokeTests
    {
        [Test]
        public void Evaluate_WorksAtRuntime()
        {
            var g = GradientABCW.CreateDefault();
            var c = g.Evaluate(0.5f);
            Assert.That(c.r, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void Bake_WorksAtRuntime()
        {
            var g = GradientABCW.CreateDefault();
            var lut = new Color32[32];
            GradientLut.Bake(g, lut, GradientLutOptions.Final);
            Assert.That(lut.Length, Is.EqualTo(32));
        }

        [Test]
        public void Job_CompletesAtRuntime()
        {
            var g = GradientABCW.CreateDefault();
            var native = NativeGradient.From(g);
            using var result = new NativeArray<float4>(32, Allocator.TempJob);
            GradientLut.ScheduleBake(in native, result, GradientLutOptions.Final).Complete();
            Assert.That(result[0].x, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void CreateTexture_WorksAtRuntime()
        {
            var g = GradientABCW.CreateDefault();
            var tex = GradientTextureUtility.CreateTexture(g, 32);
            Assert.That(tex.width, Is.EqualTo(32));
            Object.Destroy(tex);
        }
    }
}
