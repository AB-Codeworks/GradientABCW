using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace ABCodeworld.Gradients.Tests.Runtime
{
    /// <summary>
    /// Proves the 3D core works outside the Editor, where none of the editor assembly exists.
    /// </summary>
    [TestFixture]
    internal sealed class Gradient3DRuntimeSmokeTests
    {
        [Test]
        public void Evaluate_WorksAtRuntime()
        {
            var g = GradientABCW3D.CreateDefault();

            // The default runs black at the origin to white at the far corner, so the centre of the cube
            // sits exactly between the two.
            var c = g.Evaluate(new Vector3(0.5f, 0.5f, 0.5f));

            Assert.That(c.r, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(c.a, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Bake_WorksAtRuntime()
        {
            var g = GradientABCW3D.CreateDefault();
            var lut = new Color32[GradientLut3D.VoxelCount(8)];

            GradientLut3D.Bake(g, lut, 8, GradientLutOptions.Final);

            Assert.That(lut.Length, Is.EqualTo(512));
            Assert.That(lut[0].r, Is.EqualTo(0));
            Assert.That(lut[lut.Length - 1].r, Is.EqualTo(255));
        }

        [Test]
        public void SampleTrilinear_WorksAtRuntime()
        {
            var g = GradientABCW3D.CreateDefault();
            var lut = new Color32[GradientLut3D.VoxelCount(8)];
            GradientLut3D.Bake(g, lut, 8, GradientLutOptions.Base);

            var c = GradientLut3D.SampleTrilinear(lut, 8, new Vector3(0.5f, 0.5f, 0.5f));

            Assert.That(c.r, Is.EqualTo(0.5f).Within(0.05f));
        }

        [Test]
        public void Job_CompletesAtRuntime()
        {
            var g = GradientABCW3D.CreateDefault();
            g.PrepareNative();

            using var result = new NativeArray<float4>(GradientLut3D.VoxelCount(4), Allocator.TempJob);
            GradientJobs3D.ScheduleBake(in g.Native, result, 4, GradientLutOptions.Final).Complete();

            Assert.That(result[0].x, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void CreateTexture_WorksAtRuntime()
        {
            var g = GradientABCW3D.CreateDefault();
            var tex = GradientTexture3DUtility.CreateTexture(g, 8);

            Assert.That(tex.width, Is.EqualTo(8));
            Assert.That(tex.height, Is.EqualTo(8));
            Assert.That(tex.depth, Is.EqualTo(8));

            Object.Destroy(tex);
        }
    }
}
