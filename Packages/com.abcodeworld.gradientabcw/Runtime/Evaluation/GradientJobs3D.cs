using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ABCodeworld.Gradients
{
    /// <summary>Schedules the 3D gradient evaluation jobs defined in this file.</summary>
    public static class GradientJobs3D
    {
        public static JobHandle ScheduleBake(in NativeGradient3D native, NativeArray<float4> dst, int size, GradientLutOptions options, JobHandle deps = default, int batch = 64)
        {
            var job = new BakeLut3DJob { Gradient = native, Options = options, Size = size, Result = dst };
            return job.Schedule(dst.Length, batch, deps);
        }

        public static JobHandle ScheduleEvaluate(in NativeGradient3D native, NativeArray<float3> positions, NativeArray<float4> results, bool includeModulation, JobHandle deps = default, int batch = 64)
        {
            var job = new EvaluateBatch3DJob { Gradient = native, Positions = positions, Results = results, IncludeModulation = includeModulation };
            return job.Schedule(positions.Length, batch, deps);
        }
    }

    /// <summary>
    /// Bakes straight to 8-bit colour, which is what every texture-bound caller actually wants.
    /// </summary>
    /// <remarks>
    /// Going through <see cref="BakeLut3DJob"/> instead means writing float4 and converting afterwards on
    /// the managed side: four times the bandwidth, plus a second pass the Burst-compiled job can do for
    /// free while the value is still in a register. At 32 cubed that is 512 KB of traffic saved.
    /// </remarks>
    [BurstCompile]
    internal struct BakeLut3DColor32Job : IJobParallelFor
    {
        public NativeGradient3D Gradient;
        public GradientLutOptions Options;
        public int Size;
        [WriteOnly] public NativeArray<Color32> Result;

        public void Execute(int index) =>
            Result[index] = GradientLut.ToColor32(GradientLut3D.SampleAt(in Gradient, GradientLut3D.VoxelPosition(index, Size), in Options));
    }

    [BurstCompile]
    internal struct BakeLut3DJob : IJobParallelFor
    {
        public NativeGradient3D Gradient;
        public GradientLutOptions Options;
        public int Size;
        [WriteOnly] public NativeArray<float4> Result;

        public void Execute(int index) =>
            Result[index] = GradientLut3D.SampleAt(in Gradient, GradientLut3D.VoxelPosition(index, Size), in Options);
    }

    [BurstCompile]
    internal struct EvaluateBatch3DJob : IJobParallelFor
    {
        public NativeGradient3D Gradient;
        [ReadOnly] public NativeArray<float3> Positions;
        public bool IncludeModulation;
        [WriteOnly] public NativeArray<float4> Results;

        public void Execute(int index)
        {
            float3 p = Positions[index];
            Results[index] = IncludeModulation ? GradientMath3D.Evaluate(in Gradient, p) : GradientMath3D.EvaluateBase(in Gradient, p);
        }
    }
}
