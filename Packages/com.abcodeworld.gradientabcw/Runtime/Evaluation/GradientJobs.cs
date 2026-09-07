using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>Schedules the gradient evaluation jobs defined in this file.</summary>
    public static class GradientJobs
    {
        public static JobHandle ScheduleBake(in NativeGradient native, NativeArray<float4> dst, GradientLutOptions options, JobHandle deps = default, int batch = 64)
        {
            var job = new BakeLutJob { Gradient = native, Options = options, Count = dst.Length, Result = dst };
            return job.Schedule(dst.Length, batch, deps);
        }

        public static JobHandle ScheduleEvaluate(in NativeGradient native, NativeArray<float> times, NativeArray<float4> results, bool includeModulation, JobHandle deps = default, int batch = 64)
        {
            var job = new EvaluateBatchJob { Gradient = native, Times = times, Results = results, IncludeModulation = includeModulation };
            return job.Schedule(times.Length, batch, deps);
        }
    }

    /// <summary>
    /// Bakes straight to 8-bit colour, which is what every texture-bound caller actually wants.
    /// </summary>
    /// <remarks>
    /// Going through <see cref="BakeLutJob"/> instead means writing float4 and converting afterwards on
    /// the managed side: four times the bandwidth, plus a second pass the Burst-compiled job can do for
    /// free while the value is still in a register.
    /// </remarks>
    [BurstCompile]
    internal struct BakeLutColor32Job : IJobParallelFor
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

    [BurstCompile]
    internal struct BakeLutJob : IJobParallelFor
    {
        public NativeGradient Gradient;
        public GradientLutOptions Options;
        public int Count;
        [WriteOnly] public NativeArray<float4> Result;

        public void Execute(int index)
        {
            float t = Count > 1 ? (float)index / (Count - 1) : 0f;
            Result[index] = GradientLut.SampleAt(in Gradient, t, in Options);
        }
    }

    [BurstCompile]
    internal struct EvaluateBatchJob : IJobParallelFor
    {
        public NativeGradient Gradient;
        [ReadOnly] public NativeArray<float> Times;
        public bool IncludeModulation;
        [WriteOnly] public NativeArray<float4> Results;

        public void Execute(int index)
        {
            float t = Times[index];
            Results[index] = IncludeModulation ? GradientMath.Evaluate(in Gradient, t) : GradientMath.EvaluateBase(in Gradient, t);
        }
    }
}
