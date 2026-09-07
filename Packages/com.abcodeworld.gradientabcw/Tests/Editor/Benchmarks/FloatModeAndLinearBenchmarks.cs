using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEngine;

namespace ABCodeworld.Gradients.Tests.Editor.Benchmarks
{
    /// <summary>
    /// The two register items that trade exactness for speed: Burst's fast float mode, and replacing the
    /// per-channel pow() in the sRGB-to-linear conversion with a lookup table. Both live here rather than
    /// in the package until they have earned their place, and both report the output delta they cause as
    /// well as the time they save.
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    internal sealed class FloatModeAndLinearBenchmarks
    {
        private const int WarmupCount = 5;
        private const int MeasurementCount = 25;

        /// <summary>Candidate variant of the production bake job, compiled with relaxed float rules.</summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct FastBakeLutColor32Job : IJobParallelFor
        {
            public NativeGradient Gradient;
            public GradientLutOptions Options;
            public int Count;
            [WriteOnly] public NativeArray<Color32> Result;

            public void Execute(int index)
            {
                float t = Count > 1 ? (float)index / (Count - 1) : 0f;
                Result[index] = GradientLut.ToColor32(GradientLut.SampleAt(in Gradient, t, in Options));
            }
        }

        [Test, Performance]
        public void V2_FloatModeFast_VsStandard([Values(256, 4096, 65536)] int size)
        {
            var g = BenchmarkGradients.Build(8, ModulationCase.Full);
            var native = g.Native;

            using var standard = new NativeArray<Color32>(size, Allocator.Persistent);
            using var fast = new NativeArray<Color32>(size, Allocator.Persistent);

            Measure.Method(() =>
                    new BakeLutColor32Job
                    {
                        Gradient = native, Options = GradientLutOptions.Final, Count = size, Result = standard,
                    }.Schedule(size, GradientLut.DefaultBatchSize).Complete())
                .SampleGroup("standard")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();

            Measure.Method(() =>
                    new FastBakeLutColor32Job
                    {
                        Gradient = native, Options = GradientLutOptions.Final, Count = size, Result = fast,
                    }.Schedule(size, GradientLut.DefaultBatchSize).Complete())
                .SampleGroup("floatModeFast")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();
        }

        /// <summary>Reports how far, if at all, fast float mode moves the 8-bit output.</summary>
        [Test]
        public void V2_FloatModeFast_OutputDelta()
        {
            const int size = 4096;
            int worst = 0;

            foreach (var modulation in BenchmarkGradients.ModulationCases)
            foreach (int keys in BenchmarkGradients.KeyCounts)
            {
                var g = BenchmarkGradients.Build(keys, modulation);
                var native = g.Native;

                using var standard = new NativeArray<Color32>(size, Allocator.Persistent);
                using var fast = new NativeArray<Color32>(size, Allocator.Persistent);

                new BakeLutColor32Job { Gradient = native, Options = GradientLutOptions.Final, Count = size, Result = standard }
                    .Schedule(size, GradientLut.DefaultBatchSize).Complete();
                new FastBakeLutColor32Job { Gradient = native, Options = GradientLutOptions.Final, Count = size, Result = fast }
                    .Schedule(size, GradientLut.DefaultBatchSize).Complete();

                for (int i = 0; i < size; i++)
                {
                    worst = math.max(worst, math.abs(standard[i].r - fast[i].r));
                    worst = math.max(worst, math.abs(standard[i].g - fast[i].g));
                    worst = math.max(worst, math.abs(standard[i].b - fast[i].b));
                    worst = math.max(worst, math.abs(standard[i].a - fast[i].a));
                }
            }

            Debug.Log($"V2 FloatMode.Fast worst channel delta across the matrix: {worst}/255");
        }

        /// <summary>A 1024-entry sRGB-to-linear table, the candidate replacement for the per-channel pow().</summary>
        private static readonly float[] LinearTable = BuildLinearTable();

        private static float[] BuildLinearTable()
        {
            var table = new float[1025];
            for (int i = 0; i < table.Length; i++)
                table[i] = ColorSpaceMath.GammaToLinear((float)i / 1024);
            return table;
        }

        private static float LerpTable(float[] table, int i, float frac) =>
            math.lerp(table[i], table[math.min(i + 1, table.Length - 1)], frac);

        [Test, Performance]
        public void V4_SrgbToLinear_PowVsTable()
        {
            const int samples = 8192;
            var values = new float[samples];
            var rng = new Unity.Mathematics.Random(12345u);
            for (int i = 0; i < samples; i++)
                values[i] = rng.NextFloat();

            Measure.Method(() =>
                {
                    float acc = 0f;
                    for (int i = 0; i < samples; i++)
                        acc += ColorSpaceMath.GammaToLinear(values[i]);
                    GradientEvaluationBenchmarks.Sink = acc;
                })
                .SampleGroup("pow")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();

            Measure.Method(() =>
                {
                    float acc = 0f;
                    for (int i = 0; i < samples; i++)
                    {
                        float scaled = math.saturate(values[i]) * 1024f;
                        int idx = (int)scaled;
                        acc += LerpTable(LinearTable, idx, scaled - idx);
                    }
                    GradientEvaluationBenchmarks.Sink = acc;
                })
                .SampleGroup("table1024")
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).Run();
        }

        [Test]
        public void V4_SrgbToLinear_TableOutputDelta()
        {
            // Worst case over the 8-bit output range, which is what the conversion actually feeds.
            float worst = 0f;
            int worstByteDelta = 0;

            for (int i = 0; i <= 4096; i++)
            {
                float v = (float)i / 4096;
                float exact = ColorSpaceMath.GammaToLinear(v);

                float scaled = math.saturate(v) * 1024f;
                int idx = (int)scaled;
                float approx = LerpTable(LinearTable, idx, scaled - idx);

                worst = math.max(worst, math.abs(exact - approx));
                worstByteDelta = math.max(worstByteDelta,
                    math.abs((int)(math.saturate(exact) * 255f + 0.5f) - (int)(math.saturate(approx) * 255f + 0.5f)));
            }

            Debug.Log($"V4 table worst float delta {worst:E3}, worst 8-bit channel delta {worstByteDelta}/255");
        }
    }
}
