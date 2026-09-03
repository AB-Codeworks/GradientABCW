using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEngine;
using ABCodeworld.Gradients.Tests.Editor.Support;

namespace ABCodeworld.Gradients.Tests.Editor.Runtime
{
    [TestFixture]
    [Category("Performance")]
    internal sealed class GradientPerformanceTests
    {
        private const int SampleCount = 4096;

        [Test, Performance]
        public void Bake_Managed()
        {
            var g = TestGradients.WithModulation();
            var lut = new Color32[SampleCount];
            Measure.Method(() => GradientLut.Bake(g, lut, GradientLutOptions.Final))
                .WarmupCount(3)
                .MeasurementCount(10)
                .Run();
        }

        [Test, Performance]
        public void Bake_Job()
        {
            var g = TestGradients.WithModulation();
            var native = NativeGradient.From(g);
            using var result = new NativeArray<float4>(SampleCount, Allocator.Persistent);

            Measure.Method(() => GradientLut.ScheduleBake(in native, result, GradientLutOptions.Final).Complete())
                .WarmupCount(3)
                .MeasurementCount(10)
                .Run();
        }
    }
}
