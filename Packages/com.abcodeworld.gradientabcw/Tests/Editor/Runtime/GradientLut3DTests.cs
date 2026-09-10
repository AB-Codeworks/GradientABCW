using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Runtime")]
    internal sealed class GradientLut3DTests
    {
        [Test]
        public void VoxelIndexVariesXFastest()
        {
            const int size = 4;

            Assert.That(GradientLut3D.VoxelIndex(1, 0, 0, size), Is.EqualTo(1));
            Assert.That(GradientLut3D.VoxelIndex(0, 1, 0, size), Is.EqualTo(size));
            Assert.That(GradientLut3D.VoxelIndex(0, 0, 1, size), Is.EqualTo(size * size));
            Assert.That(GradientLut3D.VoxelIndex(3, 3, 3, size), Is.EqualTo(GradientLut3D.VoxelCount(size) - 1));
        }

        [Test]
        public void CellsSpanTheDomainEndToEnd()
        {
            const int size = 8;

            var first = GradientLut3D.VoxelPosition(0, size);
            var last = GradientLut3D.VoxelPosition(GradientLut3D.VoxelCount(size) - 1, size);

            Assert.That(first.x, Is.EqualTo(0f));
            Assert.That(first.y, Is.EqualTo(0f));
            Assert.That(first.z, Is.EqualTo(0f));
            Assert.That(last.x, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(last.y, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(last.z, Is.EqualTo(1f).Within(1e-6f));
        }

        [TestCase(2)]
        [TestCase(4)]
        [TestCase(16)]
        [TestCase(32)]
        public void BakeMatchesPerCellEvaluate(int size)
        {
            var g = Test3DGradients.WithModulation();
            var lut = new Color32[GradientLut3D.VoxelCount(size)];
            GradientLut3D.Bake(g, lut, size, GradientLutOptions.Final);

            for (int i = 0; i < lut.Length; i++)
            {
                float3 p = GradientLut3D.VoxelPosition(i, size);
                var expected = g.Evaluate(new Vector3(p.x, p.y, p.z));

                Assert.That(lut[i].r, Is.EqualTo(ToByte(expected.r)).Within(1), $"r at cell {i}");
                Assert.That(lut[i].g, Is.EqualTo(ToByte(expected.g)).Within(1), $"g at cell {i}");
                Assert.That(lut[i].b, Is.EqualTo(ToByte(expected.b)).Within(1), $"b at cell {i}");
                Assert.That(lut[i].a, Is.EqualTo(ToByte(expected.a)).Within(1), $"a at cell {i}");
            }
        }

        /// <summary>
        /// The Burst path and the managed fallback must not diverge, or a bake would change appearance
        /// depending on how big it happened to be.
        /// </summary>
        [Test]
        public void BurstBakeMatchesTheManagedFallbackByteForByte()
        {
            var g = Test3DGradients.Lattice27();
            const int size = 8;
            int count = GradientLut3D.VoxelCount(size);

            using var burst = new NativeArray<Color32>(count, Allocator.Temp);
            GradientLut3D.Bake(in g.Native, burst, size, GradientLutOptions.Final);

            using var managed = new NativeArray<Color32>(count, Allocator.Temp);
            GradientLut3D.BakeManaged(in g.Native, managed, size, GradientLutOptions.Final);

            for (int i = 0; i < count; i++)
                Assert.That(burst[i], Is.EqualTo(managed[i]), $"cell {i}");
        }

        [Test]
        public void ScheduledBakeMatchesTheManagedBake()
        {
            var g = Test3DGradients.Corners8();
            const int size = 8;
            int count = GradientLut3D.VoxelCount(size);

            using var scheduled = new NativeArray<float4>(count, Allocator.TempJob);
            GradientJobs3D.ScheduleBake(in g.Native, scheduled, size, GradientLutOptions.Final).Complete();

            using var direct = new NativeArray<float4>(count, Allocator.Temp);
            GradientLut3D.Bake(in g.Native, direct, size, GradientLutOptions.Final);

            for (int i = 0; i < count; i++)
                Assert.That(math.all(math.abs(scheduled[i] - direct[i]) < 1e-5f), Is.True, $"cell {i}");
        }

        [Test]
        public void TrilinearSamplingAtACellCentreReturnsThatCell()
        {
            var g = Test3DGradients.Lattice27();
            const int size = 8;
            var lut = new Color32[GradientLut3D.VoxelCount(size)];
            GradientLut3D.Bake(g, lut, size, GradientLutOptions.Base);

            for (int z = 0; z < size; z += 3)
            {
                for (int y = 0; y < size; y += 3)
                {
                    for (int x = 0; x < size; x += 3)
                    {
                        var p = new Vector3(x / (size - 1f), y / (size - 1f), z / (size - 1f));
                        var sampled = GradientLut3D.SampleTrilinear(lut, size, p);
                        var cell = lut[GradientLut3D.VoxelIndex(x, y, z, size)];

                        Assert.That(sampled.r, Is.EqualTo(cell.r / 255f).Within(1e-4f), $"r at ({x}, {y}, {z})");
                        Assert.That(sampled.a, Is.EqualTo(cell.a / 255f).Within(1e-4f), $"a at ({x}, {y}, {z})");
                    }
                }
            }
        }

        [Test]
        public void TrilinearSamplingClampsOutsideTheCube()
        {
            var g = Test3DGradients.Corners8();
            const int size = 8;
            var lut = new Color32[GradientLut3D.VoxelCount(size)];
            GradientLut3D.Bake(g, lut, size, GradientLutOptions.Base);

            Assert.That(GradientLut3D.SampleTrilinear(lut, size, new Vector3(-3f, -3f, -3f)),
                Is.EqualTo(GradientLut3D.SampleTrilinear(lut, size, Vector3.zero)));
            Assert.That(GradientLut3D.SampleTrilinear(lut, size, new Vector3(3f, 3f, 3f)),
                Is.EqualTo(GradientLut3D.SampleTrilinear(lut, size, Vector3.one)));
        }

        [Test]
        public void BaseAndFinalDifferWhenModulationIsEffective()
        {
            var g = Test3DGradients.WithModulation();
            const int size = 8;
            int count = GradientLut3D.VoxelCount(size);

            var basic = new Color32[count];
            var final = new Color32[count];
            GradientLut3D.Bake(g, basic, size, GradientLutOptions.Base);
            GradientLut3D.Bake(g, final, size, GradientLutOptions.Final);

            Assert.That(basic, Is.Not.EqualTo(final));
        }

        [Test]
        public void ToLinearMatchesUnitysOwnConversion()
        {
            var g = Test3DGradients.Corners8();
            const int size = 4;
            int count = GradientLut3D.VoxelCount(size);

            var srgb = new Color32[count];
            var linear = new Color32[count];
            GradientLut3D.Bake(g, srgb, size, new GradientLutOptions { includeModulation = false, toLinear = false });
            GradientLut3D.Bake(g, linear, size, new GradientLutOptions { includeModulation = false, toLinear = true });

            for (int i = 0; i < count; i++)
            {
                var expected = new Color(srgb[i].r / 255f, srgb[i].g / 255f, srgb[i].b / 255f).linear;
                Assert.That(linear[i].r, Is.EqualTo(ToByte(expected.r)).Within(2), $"r at cell {i}");
            }
        }

        [Test]
        public void ShaderScaleOffsetMapsTheDomainOntoCellCentres()
        {
            const int size = 32;
            Vector4 so = GradientLut3D.ShaderScaleOffset(size);

            // p = 0 must land on the first cell's centre, p = 1 on the last.
            Assert.That(0f * so.x + so.y, Is.EqualTo(0.5f / size).Within(1e-6f));
            Assert.That(1f * so.x + so.y, Is.EqualTo((size - 0.5f) / size).Within(1e-6f));
        }

        [Test]
        public void AMismatchedDestinationIsRejected()
        {
            var g = Test3DGradients.Corners8();
            Assert.Throws<System.ArgumentException>(() => GradientLut3D.Bake(g, new Color32[10], 4, GradientLutOptions.Base));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => GradientLut3D.Bake(g, new Color32[1], 1, GradientLutOptions.Base));
        }

        private static byte ToByte(float value) => (byte)(Mathf.Clamp01(value) * 255f + 0.5f);
    }
}
