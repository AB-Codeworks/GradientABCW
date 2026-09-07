using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEngine;

namespace ABCodeworld.Gradients.Tests.Editor.Benchmarks
{
    /// <summary>
    /// Measures the optimized evaluation core against the frozen pre-optimization baseline, both in the
    /// same run so the numbers are directly comparable. Every Phase 2 optimization has to earn its place
    /// here before it is kept.
    /// </summary>
    /// <remarks>
    /// Each measured action does a whole sweep rather than a single evaluation: at a few nanoseconds per
    /// call, timer overhead would otherwise dominate and every result would look identical. Results are
    /// accumulated into <see cref="Sink"/> so nothing gets optimized away for being unused.
    /// </remarks>
    [TestFixture]
    [Category("Performance")]
    internal sealed class GradientEvaluationBenchmarks
    {
        private const int SweepSamples = 1024;
        private const int WarmupCount = 5;
        private const int MeasurementCount = 25;

        /// <summary>Consumes benchmark results so the work cannot be eliminated as dead code.</summary>
        internal static volatile float Sink;

        [Test, Performance]
        [TestCaseSource(typeof(BenchmarkGradients), nameof(BenchmarkGradients.BenchmarkCases))]
        public void Evaluate_Scalar(int keyCount, ModulationCase modulation)
        {
            var g = BenchmarkGradients.Build(keyCount, modulation);
            var native = g.Native;

            Measure.Method(() =>
                {
                    float acc = 0f;
                    for (int i = 0; i < SweepSamples; i++)
                        acc += BaselineGradientMath.Evaluate(in native, (float)i / (SweepSamples - 1)).x;
                    Sink = acc;
                })
                .SampleGroup("baseline")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();

            Measure.Method(() =>
                {
                    float acc = 0f;
                    for (int i = 0; i < SweepSamples; i++)
                        acc += GradientMath.Evaluate(in native, (float)i / (SweepSamples - 1)).x;
                    Sink = acc;
                })
                .SampleGroup("current")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();
        }

        [Test, Performance]
        [TestCaseSource(typeof(BenchmarkGradients), nameof(BenchmarkGradients.BenchmarkCases))]
        public void Bake_Managed_256(int keyCount, ModulationCase modulation) => MeasureBake(keyCount, modulation, 256);

        [Test, Performance]
        [TestCaseSource(typeof(BenchmarkGradients), nameof(BenchmarkGradients.BenchmarkCases))]
        public void Bake_Managed_4096(int keyCount, ModulationCase modulation) => MeasureBake(keyCount, modulation, 4096);

        private static void MeasureBake(int keyCount, ModulationCase modulation, int size)
        {
            var g = BenchmarkGradients.Build(keyCount, modulation);
            var native = g.Native;
            var managed = new Color32[size];

            using var scratch = new NativeArray<Color32>(size, Allocator.Persistent);

            Measure.Method(() => BaselineGradientMath.Bake(in native, scratch, GradientLutOptions.Final))
                .SampleGroup("baseline")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();

            Measure.Method(() => GradientLut.Bake(g, managed.AsSpan(), GradientLutOptions.Final))
                .SampleGroup("current")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();
        }

        /// <summary>
        /// Stepped blending scans key midpoints. The scan was linear, so this is where a 32-key gradient
        /// paid the most; measured separately because the main matrix only covers smooth blending.
        /// </summary>
        [Test, Performance]
        public void Evaluate_Stepped([Values(2, 8, 32)] int keyCount)
        {
            var g = BenchmarkGradients.Build(keyCount, ModulationCase.None, BlendMode.Stepped);
            var native = g.Native;

            Measure.Method(() =>
                {
                    float acc = 0f;
                    for (int i = 0; i < SweepSamples; i++)
                        acc += BaselineGradientMath.Evaluate(in native, (float)i / (SweepSamples - 1)).x;
                    Sink = acc;
                })
                .SampleGroup("baseline")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();

            Measure.Method(() =>
                {
                    float acc = 0f;
                    for (int i = 0; i < SweepSamples; i++)
                        acc += GradientMath.Evaluate(in native, (float)i / (SweepSamples - 1)).x;
                    Sink = acc;
                })
                .SampleGroup("current")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();
        }

        /// <summary>
        /// The scheduling-overhead question: at LUT sizes the editor actually uses, is a parallel job
        /// worth the dispatch, or does a plain managed loop win outright?
        /// </summary>
        [Test, Performance]
        public void Bake_ManagedVsJob_AcrossSizes([Values(64, 256, 1024, 4096, 65536)] int size)
        {
            var g = BenchmarkGradients.Build(8, ModulationCase.Full);
            var native = g.Native;
            var managed = new Color32[size];

            using var results = new NativeArray<float4>(size, Allocator.Persistent);

            Measure.Method(() => GradientLut.Bake(g, managed.AsSpan(), GradientLutOptions.Final))
                .SampleGroup("managed")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();

            Measure.Method(() => GradientJobs.ScheduleBake(in native, results, GradientLutOptions.Final).Complete())
                .SampleGroup("jobParallelFor")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();
        }
    }
}
