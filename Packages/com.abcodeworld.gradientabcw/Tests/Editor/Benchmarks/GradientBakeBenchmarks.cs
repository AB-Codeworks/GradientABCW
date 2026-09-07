using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEngine;

namespace ABCodeworld.Gradients.Tests.Editor.Benchmarks
{
    /// <summary>
    /// Locates the size at which handing a LUT bake to Burst beats running it on the managed thread, and
    /// checks that writing 8-bit colour straight out of the job beats writing float4 and converting after.
    /// The threshold in <see cref="GradientLut"/> comes from these numbers.
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    internal sealed class GradientBakeBenchmarks
    {
        private const int WarmupCount = 5;
        private const int MeasurementCount = 25;

        /// <summary>
        /// Managed scalar loop against the Burst job, at every size from "smaller than any real preview"
        /// upward, so the crossover is measured rather than guessed at.
        /// </summary>
        [Test, Performance]
        public void Bake_Color32_ManagedVsBurst([Values(4, 8, 16, 32, 64, 128, 256)] int size)
        {
            var g = BenchmarkGradients.Build(8, ModulationCase.Full);
            var native = g.Native;

            using var dst = new NativeArray<Color32>(size, Allocator.Persistent);

            Measure.Method(() => GradientLut.BakeManaged(in native, dst, GradientLutOptions.Final))
                .SampleGroup("managed")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();

            Measure.Method(() =>
                    new BakeLutColor32Job
                    {
                        Gradient = native,
                        Options = GradientLutOptions.Final,
                        Count = size,
                        Result = dst,
                    }.Schedule(size, GradientLut.DefaultBatchSize).Complete())
                .SampleGroup("burst")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();
        }

        /// <summary>
        /// Writing Color32 from inside the job against the existing float4 job plus a managed conversion
        /// pass, which is what a texture-bound caller would otherwise have to do.
        /// </summary>
        [Test, Performance]
        public void Bake_Color32Job_VsFloat4JobPlusConversion([Values(256, 4096)] int size)
        {
            var g = BenchmarkGradients.Build(8, ModulationCase.Full);
            var native = g.Native;

            // Not `using var`: the conversion arm below writes through the indexer, which C# treats as
            // modifying a using variable. Disposed in the finally instead.
            var color32 = new NativeArray<Color32>(size, Allocator.Persistent);
            var float4s = new NativeArray<float4>(size, Allocator.Persistent);
            try
            {
            Measure.Method(() =>
                    new BakeLutColor32Job
                    {
                        Gradient = native,
                        Options = GradientLutOptions.Final,
                        Count = size,
                        Result = color32,
                    }.Schedule(size, GradientLut.DefaultBatchSize).Complete())
                .SampleGroup("color32Job")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();

            Measure.Method(() =>
                {
                    GradientJobs.ScheduleBake(in native, float4s, GradientLutOptions.Final).Complete();
                    for (int i = 0; i < size; i++)
                        color32[i] = GradientLut.ToColor32(float4s[i]);
                })
                .SampleGroup("float4JobThenConvert")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();
            }
            finally
            {
                color32.Dispose();
                float4s.Dispose();
            }
        }

        /// <summary>
        /// The Span-taking entry point cannot hand its memory to a job. Is it worth staging through a
        /// temporary native array to reach Burst, or does the copy eat the gain?
        /// </summary>
        [Test, Performance]
        public void Bake_Span_ManagedVsStagedThroughBurst([Values(256, 4096, 65536)] int size)
        {
            var g = BenchmarkGradients.Build(8, ModulationCase.Full);
            var native = g.Native;
            var managed = new Color32[size];

            Measure.Method(() =>
                {
                    var dst = managed.AsSpan();
                    for (int i = 0; i < size; i++)
                    {
                        float t = size > 1 ? (float)i / (size - 1) : 0f;
                        dst[i] = GradientLut.ToColor32(GradientLut.SampleAt(in native, t, GradientLutOptions.Final));
                    }
                })
                .SampleGroup("managedSpan")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();

            Measure.Method(() =>
                {
                    using var staging = new NativeArray<Color32>(size, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    new BakeLutColor32Job
                    {
                        Gradient = native,
                        Options = GradientLutOptions.Final,
                        Count = size,
                        Result = staging,
                    }.Schedule(size, GradientLut.DefaultBatchSize).Complete();
                    staging.CopyTo(managed);
                })
                .SampleGroup("stagedThroughBurst")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();
        }
    }
}
